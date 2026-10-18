using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Apresentação 3D dos cabos ligados ao PC (só visual; a lógica continua no <see cref="ComputerPower"/>).
/// Vai no root lógico do gabinete, ao lado do ComputerPower. Para cada cabo (<see cref="PcLink"/>):
///  - nada conectado: nada aparece (o item está na mão ou no mundo);
///  - uma ponta conectada: plugue encaixado na porta e o cabo pendente até a superfície logo abaixo,
///    com a outra ponta (plugue solto) deitada ali;
///  - as duas pontas: curva suave entre as portas, descendo até perto da mesa e afastada dos equipamentos.
/// Cada porta é um <see cref="Transform"/> no mundo (<see cref="anchors"/>): posição = face da porta,
/// eixo Z (forward) = direção para FORA da porta. O cabo é um tubo gerado por código (sem física), refeito só
/// quando uma conexão muda ou uma porta se move.
/// </summary>
[RequireComponent(typeof(ComputerPower))]
public class PcCableView3D : MonoBehaviour
{
    [Serializable]
    public class PortAnchor
    {
        public PcPort port;
        [Tooltip("Face da porta; Z (azul) apontando para fora do equipamento.")]
        public Transform anchor;
    }

    [Serializable]
    public class CableStyle
    {
        public CableType type;
        [Tooltip("Plugue da ponta A (tomada, saída do gabinete, ponto de rede).")]
        public Mesh plugA;
        public Material[] plugAMaterials;
        [Tooltip("Plugue da ponta B (gabinete/monitor). Vazio = igual ao A.")]
        public Mesh plugB;
        public Material[] plugBMaterials;
        public Material cableMaterial;
        [Min(0.001f)] public float radius = 0.004f;
    }

    [SerializeField] private List<PortAnchor> anchors = new List<PortAnchor>();
    [SerializeField] private List<CableStyle> styles = new List<CableStyle>();
    [Tooltip("Camadas que o cabo evita atravessar (equipamentos, móveis, paredes).")]
    [SerializeField] private LayerMask obstacles = ~0;
    [Tooltip("Quanto o cabo completo desce abaixo das portas (m), no máximo até a superfície de baixo.")]
    [SerializeField] private float sag = 0.10f;
    [SerializeField, Range(4, 16)] private int sides = 8;
    [SerializeField, Range(8, 64)] private int samples = 32;

    private ComputerPower power;
    private readonly Dictionary<PcLink, GameObject> built = new Dictionary<PcLink, GameObject>();
    private readonly Dictionary<Transform, Vector3> lastPositions = new Dictionary<Transform, Vector3>();
    private bool dirty = true;

    public IReadOnlyList<PortAnchor> Anchors => anchors;

    private void OnEnable()
    {
        power = GetComponent<ComputerPower>();
        power.ConnectionsChanged += MarkDirty;
        dirty = true;
    }

    private void OnDisable()
    {
        if (power != null) power.ConnectionsChanged -= MarkDirty;
    }

    private void MarkDirty() => dirty = true;

    private void LateUpdate()
    {
        foreach (PortAnchor a in anchors) Track(a.anchor);
        if (power.Monitor != null) Track(power.Monitor);   // monitor trocado: as âncoras dele andam com ele
        if (dirty) Rebuild();
    }

    private void Track(Transform t)
    {
        if (t == null) return;
        Vector3 p = t.position;
        if (!lastPositions.TryGetValue(t, out Vector3 old) || (old - p).sqrMagnitude > 1e-6f)
        {
            lastPositions[t] = p;
            dirty = true;
        }
    }

    /// <summary>Refaz todos os cabos (chamado sozinho quando algo muda).</summary>
    public void Rebuild()
    {
        dirty = false;
        foreach (PcLink link in Enum.GetValues(typeof(PcLink)))
        {
            if (built.TryGetValue(link, out GameObject old) && old != null) Destroy(old);
            built.Remove(link);

            // cabo de energia: a ponta A vai na tomada em que ele foi ligado (qualquer uma das duas)
            power.PortsOf(link, out PcPort pa, out PcPort pb);
            bool a = power.IsEndPlugged(link, true), b = power.IsEndPlugged(link, false);
            if (!a && !b) continue;
            Transform ta = Anchor(pa, link), tb = Anchor(pb, link);
            CableStyle style = Style(ComputerPower.CableFor(link));
            if (style == null || (a && ta == null) || (b && tb == null)) continue;
            built[link] = Build(link, style, a ? ta : null, b ? tb : null);
        }
    }

    /// <summary>Quantas partes (tubo e plugues) cada cabo desenhado tem agora; para testes.</summary>
    public int BuiltCount(PcLink link) => built.TryGetValue(link, out GameObject g) && g != null ? g.transform.childCount : 0;

    /// <summary>Portas padrão do cabo. A tomada real de um cabo de energia: <see cref="ComputerPower.PortsOf"/>.</summary>
    public static void Ports(PcLink link, out PcPort endA, out PcPort endB) => ComputerPower.DefaultPorts(link, out endA, out endB);

    // Porta da parede de um cabo ligado no ponto elétrico de OUTRA estação: usa as âncoras daquele ponto.
    private Transform Anchor(PcPort port, PcLink link)
    {
        WallConnectionPoint wall = ComputerPower.IsWallPort(port) ? power.WallOf(link) : null;
        if (wall != null && wall.Source != power)
        {
            Transform t = wall.AnchorFor(port);
            if (t != null) return t;
        }
        // porta do monitor: as âncoras do monitor ATUAL deste PC (ele pode ter sido trocado por um do estoque)
        if (ComputerPower.IsMonitorPort(port) && power.Monitor != null)
        {
            MonitorDisplay md = power.Monitor.GetComponent<MonitorDisplay>();
            Transform t = md != null ? md.AnchorFor(port) : null;
            if (t != null) return t;
        }
        return LocalAnchor(port);
    }

    /// <summary>Âncora configurada neste componente para a porta (null = nenhuma).</summary>
    public Transform LocalAnchor(PcPort port)
    {
        foreach (PortAnchor a in anchors) if (a.port == port && a.anchor != null) return a.anchor;
        return null;
    }

    private CableStyle Style(CableType type)
    {
        foreach (CableStyle s in styles) if (s.type == type) return s;
        return null;
    }

    // ------------------------------------------------------------------ montagem
    private GameObject Build(PcLink link, CableStyle st, Transform ta, Transform tb)
    {
        var root = new GameObject("Cabo3D_" + link);
        root.transform.SetParent(transform, false);
        Mesh plugB = st.plugB != null ? st.plugB : st.plugA;
        Material[] matsB = st.plugB != null ? st.plugBMaterials : st.plugAMaterials;

        Vector3 exitA = Vector3.zero, exitB = Vector3.zero, dirA = Vector3.zero, dirB = Vector3.zero;
        if (ta != null) exitA = PlugIn(root, "Plugue_A", st.plugA, st.plugAMaterials, ta, out dirA);
        if (tb != null) exitB = PlugIn(root, "Plugue_B", plugB, matsB, tb, out dirB);

        List<Vector3> path;
        if (ta != null && tb != null) path = ConnectedPath(exitA, dirA, exitB, dirB, st.radius);
        else
        {
            bool fromA = ta != null;
            Vector3 exit = fromA ? exitA : exitB, dir = fromA ? dirA : dirB;
            path = HangingPath(root, exit, dir, st.radius, fromA ? plugB : st.plugA, fromA ? matsB : st.plugAMaterials);
        }
        var tube = new GameObject("Cabo");
        tube.transform.SetParent(root.transform, false);
        tube.AddComponent<MeshFilter>().sharedMesh = Tube(path, st.radius, sides, root.transform);
        tube.AddComponent<MeshRenderer>().sharedMaterial = st.cableMaterial;
        return root;
    }

    /// <summary>Plugue encaixado na porta; devolve o ponto onde o cabo sai dele e a direção de saída.</summary>
    private static Vector3 PlugIn(GameObject root, string name, Mesh mesh, Material[] mats, Transform port, out Vector3 dir)
    {
        dir = port.forward;
        if (mesh == null) return port.position;
        // O corpo do plugue fica do lado do sinal de bounds.center.z: ele é alinhado para fora da porta.
        float body = Mathf.Sign(mesh.bounds.center.z);
        Quaternion rot = Quaternion.LookRotation(port.forward * body, Vector3.up);
        Place(root, name, mesh, mats, port.position, rot);
        float len = body > 0 ? mesh.bounds.max.z : -mesh.bounds.min.z;
        return port.position + dir * len;
    }

    private static void Place(GameObject root, string name, Mesh mesh, Material[] mats, Vector3 pos, Quaternion rot)
    {
        var g = new GameObject(name);
        g.transform.SetParent(root.transform, false);
        g.transform.SetPositionAndRotation(pos, rot);
        g.AddComponent<MeshFilter>().sharedMesh = mesh;
        g.AddComponent<MeshRenderer>().sharedMaterials = mats;
    }

    private List<Vector3> ConnectedPath(Vector3 a, Vector3 na, Vector3 b, Vector3 nb, float r)
    {
        Vector3 outward = (na + nb).sqrMagnitude > 1e-4f ? (na + nb).normalized : Vector3.up;
        // sai reto da porta e afasta do equipamento antes de descer (não corre rente à traseira)
        Vector3 a1 = a + na * 0.10f, b1 = b + nb * 0.10f;
        Vector3 mid = (a1 + b1) * 0.5f + Flat(outward) * 0.08f;
        float low = Mathf.Min(a1.y, b1.y) - sag - Vector3.Distance(a1, b1) * 0.1f;
        mid.y = Mathf.Max(SurfaceBelow(mid, r) + r * 1.5f, Mathf.Min(mid.y, low));
        var pts = CatmullRom(new[] { a, a1, mid, b1, b }, samples);
        Untangle(pts, r, outward);
        return pts;
    }

    private List<Vector3> HangingPath(GameObject root, Vector3 exit, Vector3 dir, float r, Mesh freePlug, Material[] freeMats)
    {
        Vector3 flat = Flat(dir);
        Vector3 e1 = exit + dir * 0.05f;
        Vector3 hang = e1 + flat * 0.10f;
        float floor = SurfaceBelow(hang, r);
        // plugue solto deitado na superfície, ponta para fora (longe do equipamento), cabo chegando por trás
        float plugH = freePlug != null ? freePlug.bounds.extents.y : r;
        float plugLen = freePlug != null ? freePlug.bounds.size.z : 0.04f;
        Vector3 end = new Vector3(hang.x, floor + r, hang.z);
        Vector3 tip = end + flat * plugLen + Vector3.up * (plugH - r);
        if (freePlug != null)
        {
            float body = Mathf.Sign(freePlug.bounds.center.z);
            Quaternion rot = Quaternion.LookRotation(-flat * body, Vector3.up);
            Place(root, "Plugue_Solto", freePlug, freeMats, tip, rot);
        }
        Vector3 drop = new Vector3(hang.x, Mathf.Lerp(e1.y, end.y, 0.55f), hang.z) - flat * 0.03f;
        var pts = CatmullRom(new[] { exit, e1, drop, end }, samples);
        Untangle(pts, r, flat);
        return pts;
    }

    private static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward;
    }

    private float SurfaceBelow(Vector3 p, float r)
    {
        return Physics.Raycast(p + Vector3.up * 0.02f, Vector3.down, out RaycastHit hit, 5f, obstacles, QueryTriggerInteraction.Ignore)
            ? hit.point.y : p.y - 1f;
    }

    /// <summary>Empurra para fora (e depois para cima) os pontos do meio que estiverem dentro de algum objeto.</summary>
    private void Untangle(List<Vector3> pts, float r, Vector3 outward)
    {
        Vector3 push = Flat(outward);
        int skip = Mathf.Max(2, pts.Count / 10);
        for (int i = skip; i < pts.Count - skip; i++)
            for (int k = 0; k < 25 && Physics.CheckSphere(pts[i], r, obstacles, QueryTriggerInteraction.Ignore); k++)
                pts[i] += (k < 15 ? push : Vector3.up) * 0.01f;
        for (int it = 0; it < 3; it++)                       // suaviza o que foi empurrado
            for (int i = 1; i < pts.Count - 1; i++)
                pts[i] = Vector3.Lerp(pts[i], (pts[i - 1] + pts[i + 1]) * 0.5f, 0.5f);
    }

    private static List<Vector3> CatmullRom(Vector3[] c, int n)
    {
        var pts = new List<Vector3>();
        int segs = c.Length - 1;
        for (int i = 0; i <= n; i++)
        {
            float t = i / (float)n * segs;
            int s = Mathf.Min((int)t, segs - 1);
            float u = t - s;
            Vector3 p0 = c[Mathf.Max(s - 1, 0)], p1 = c[s], p2 = c[s + 1], p3 = c[Mathf.Min(s + 2, segs)];
            if (s == 0) p0 = p1 - (p2 - p1);
            if (s + 2 > segs) p3 = p2 + (p2 - p1);
            pts.Add(0.5f * (2f * p1 + (p2 - p0) * u + (2f * p0 - 5f * p1 + 4f * p2 - p3) * u * u + (3f * p1 - p0 - 3f * p2 + p3) * u * u * u));
        }
        return pts;
    }

    /// <summary>Tubo (anéis com transporte paralelo) ao longo dos pontos, em coordenadas locais de <paramref name="space"/>.</summary>
    public static Mesh Tube(List<Vector3> pts, float r, int sides, Transform space)
    {
        var v = new List<Vector3>(); var nrm = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
        Vector3 prevT = (pts[1] - pts[0]).normalized;
        Vector3 normal = Vector3.Cross(prevT, Mathf.Abs(prevT.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
        float len = 0f;
        for (int i = 0; i < pts.Count; i++)
        {
            Vector3 t = (i < pts.Count - 1 ? pts[i + 1] - pts[i] : pts[i] - pts[i - 1]).normalized;
            if (i > 0) len += Vector3.Distance(pts[i], pts[i - 1]);
            normal = Vector3.ProjectOnPlane(normal, t).normalized;
            if (normal.sqrMagnitude < 1e-6f) normal = Vector3.Cross(t, Vector3.up).normalized;
            Vector3 bin = Vector3.Cross(t, normal);
            for (int s = 0; s <= sides; s++)
            {
                float a = s / (float)sides * Mathf.PI * 2f;
                Vector3 dir = normal * Mathf.Cos(a) + bin * Mathf.Sin(a);
                v.Add(space.InverseTransformPoint(pts[i] + dir * r));
                nrm.Add(space.InverseTransformDirection(dir));
                uv.Add(new Vector2(s / (float)sides, len * 20f));
            }
        }
        int ring = sides + 1;
        for (int i = 0; i < pts.Count - 1; i++)
            for (int s = 0; s < sides; s++)
            {
                int a0 = i * ring + s, a1 = a0 + 1, b0 = a0 + ring, b1 = b0 + 1;
                tri.AddRange(new[] { a0, a1, b0, a1, b1, b0 }); // face visível para fora do tubo
            }
        var m = new Mesh { name = "CaboTubo" };
        m.SetVertices(v); m.SetNormals(nrm); m.SetUVs(0, uv); m.SetTriangles(tri, 0);
        m.RecalculateBounds();
        return m;
    }
}
