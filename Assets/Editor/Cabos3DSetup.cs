using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Cabos e conectores 3D: modelos de cabos.fbx (Tools/blender/cabos_build.py) nos itens Pickup_Cabo*, espelho de parede
/// (tomadas + ponto de rede) na cena, âncoras 3D das portas do gabinete/monitor/parede e o <see cref="PcCableView3D"/>,
/// e, no Canvas, plugues/tomadas renderizados dos mesmos modelos e cabos curvos.
/// Idempotente. Menu: Tools > E-stagiar.io > Configurar cabos 3D. Batch: -executeMethod Cabos3DSetup.Run
/// </summary>
public static class Cabos3DSetup
{
    private const string Fbx = "Assets/Art/Models/cabos.fbx";
    private const string Mats = "Assets/Art/Materials/";
    private const string CabMats = Mats + "Cabos/";
    private const string Sprites = "Assets/Art/UI/";
    private const string Gameplay = "Assets/Prefabs/Gameplay/";
    private const string ScreenPath = "Assets/Prefabs/UI/UI_Screen_Gabinete.prefab";
    private const string ScenePath = "Assets/Scenes/Office_Placeholder.unity";

    [MenuItem("Tools/E-stagiar.io/Configurar cabos 3D")]
    public static void Run()
    {
        AssetDatabase.Refresh();
        ImportSprites();
        ConfigureModel();
        var meshes = AssetDatabase.LoadAllAssetsAtPath(Fbx).OfType<Mesh>().ToDictionary(m => m.name);
        foreach (string n in new[] { "Plugue_Tomada", "Plugue_IEC", "Plugue_VGA", "Plugue_HDMI", "Plugue_RJ45", "PontoEletrico" })
            Debug.Log($"Cabos3DSetup: {n} bounds {meshes[n].bounds.center:F3} {meshes[n].bounds.size:F3}");

        foreach (var (prefab, mesh) in new[] { ("Pickup_CaboEnergia", "Rolo_Energia"), ("Pickup_CaboVideo", "Rolo_VGA"),
                                               ("Pickup_CaboHDMI", "Rolo_HDMI"), ("Pickup_CaboRede", "Rolo_Rede") })
            CableItemVisual(prefab, meshes[mesh]);
        GameObject plate = WallPlatePrefab(meshes["PontoEletrico"]);
        ConfigureCase(meshes);
        ConfigureScreen();
        ConfigureScene(plate);
        AssetDatabase.SaveAssets();
        Debug.Log("Cabos3DSetup: concluído.");
    }

    // ------------------------------------------------------------------ materiais e modelo
    private static Material Mat(string name, Color c, float smooth = 0.3f, float metal = 0f, bool transparent = false)
    {
        Directory.CreateDirectory(CabMats);
        string path = CabMats + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Smoothness", smooth);
        m.SetFloat("_Metallic", metal);
        if (transparent)
        {
            m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    private static void ConfigureModel()
    {
        var remap = new Dictionary<string, Material>
        {
            { "MAT_Cabo_Preto", Mat("M_Cabo_Preto", new Color(0.07f, 0.07f, 0.08f), 0.35f) },
            { "MAT_Cabo_Cinza", Mat("M_Cabo_Cinza", new Color(0.78f, 0.78f, 0.76f), 0.35f) },
            { "MAT_Cabo_Azul", Mat("M_Cabo_Azul", new Color(0.18f, 0.45f, 0.85f), 0.4f) },
            { "MAT_Plug_Preto", Mat("M_Plug_Preto", new Color(0.11f, 0.11f, 0.12f), 0.45f) },
            { "MAT_Plug_Translucido", Mat("M_Plug_Translucido", new Color(0.85f, 0.93f, 1f, 0.55f), 0.85f, 0f, true) },
            { "MAT_Tomada_Branco", Mat("M_Tomada_Branco", new Color(0.93f, 0.93f, 0.91f), 0.4f) },
            { "MAT_Tomada_Interior", Mat("M_Tomada_Interior", new Color(0.30f, 0.30f, 0.32f), 0.3f) },
            { "MAT_Tomada_Rebaixo", Mat("M_Tomada_Rebaixo", new Color(0.74f, 0.74f, 0.72f), 0.35f) },
            { "MAT_Gabinete_Corpo", AssetDatabase.LoadAssetAtPath<Material>(Mats + "M_Escritorio_Escuro.mat") },
        };
        foreach (string n in new[] { "Painel", "Chapa", "Parafuso", "Porta", "VGA", "Pino", "LED", "HDMI" })
            remap["MAT_Gabinete_" + n] = AssetDatabase.LoadAssetAtPath<Material>(Mats + "M_PC_" + n + ".mat");

        var imp = (ModelImporter)AssetImporter.GetAtPath(Fbx);
        imp.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        imp.isReadable = false;
        foreach (var kv in remap)
            if (kv.Value != null) imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), kv.Key), kv.Value);
        imp.SaveAndReimport();
    }

    private static Material[] MaterialsOf(string meshName)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
        var mf = model.GetComponentsInChildren<MeshFilter>(true).First(f => f.sharedMesh != null && f.sharedMesh.name == meshName);
        return mf.GetComponent<MeshRenderer>().sharedMaterials;
    }

    private static void ImportSprites()
    {
        const string src = "Art/Source/Blender/cabos_renders/";
        string template = File.ReadAllText("Assets/Art/UI/PH_pc_back.png.meta");
        foreach (string f in Directory.GetFiles(src, "*.png"))
        {
            string dst = Sprites + Path.GetFileName(f);
            File.Copy(f, dst, true);
            if (!File.Exists(dst + ".meta"))
            {
                string meta = System.Text.RegularExpressions.Regex.Replace(template, @"guid: \w+", "guid: " + System.Guid.NewGuid().ToString("N"), System.Text.RegularExpressions.RegexOptions.None);
                meta = System.Text.RegularExpressions.Regex.Replace(meta, @"spriteID: \w+", "spriteID: " + System.Guid.NewGuid().ToString("N"));
                File.WriteAllText(dst + ".meta", meta);
            }
        }
        AssetDatabase.Refresh();
    }

    // ------------------------------------------------------------------ itens de cabo
    private const float ItemScale = 1.06f, MinPickHeight = 0.05f, PickMargin = 0.005f, MaxItemSize = 0.149f;

    // Margem de mira nos lados, sem passar do tamanho que cabe na gaveta (MaxItemSize; nunca menor que o rolo).
    private static float Widen(float size) => Mathf.Max(size, Mathf.Min(size + 2 * PickMargin, MaxItemSize));

    /// <summary>Só reaplica escala e collider dos itens Pickup_Cabo* (não mexe em cena, gabinete nem sprites).</summary>
    [MenuItem("Tools/E-stagiar.io/Ajustar itens de cabo")]
    public static void AjustarItensDeCabo()
    {
        var meshes = AssetDatabase.LoadAllAssetsAtPath(Fbx).OfType<Mesh>().ToDictionary(m => m.name);
        foreach (var (prefab, mesh) in new[] { ("Pickup_CaboEnergia", "Rolo_Energia"), ("Pickup_CaboVideo", "Rolo_VGA"),
                                               ("Pickup_CaboHDMI", "Rolo_HDMI"), ("Pickup_CaboRede", "Rolo_Rede") })
            CableItemVisual(prefab, meshes[mesh]);
        AssetDatabase.SaveAssets();
    }

    private static void CableItemVisual(string prefabName, Mesh mesh)
    {
        string path = Gameplay + prefabName + ".prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        Transform old = root.transform.Find("Visual");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var visual = new GameObject("Visual");
        visual.transform.SetParent(root.transform, false);
        visual.AddComponent<MeshFilter>().sharedMesh = mesh;
        visual.AddComponent<MeshRenderer>().sharedMaterials = MaterialsOf(mesh.name);
        visual.transform.localScale = Vector3.one * ItemScale;
        // Collider: base no chão do rolo (y = 0, não flutua), um pouco mais largo e no mínimo MinPickHeight de
        // altura PARA CIMA, para a mira acertar com facilidade os rolos baixos (2,7–4 cm).
        Bounds b = mesh.bounds;
        var box = root.GetComponent<BoxCollider>();
        float h = Mathf.Max(b.size.y * ItemScale, MinPickHeight);
        box.size = new Vector3(Widen(b.size.x * ItemScale), h, Widen(b.size.z * ItemScale));
        box.center = new Vector3(b.center.x * ItemScale, b.min.y * ItemScale + h / 2, b.center.z * ItemScale);
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
    }

    // ------------------------------------------------------------------ espelho de parede
    /// <summary>Âncora = face da porta, Z para fora. Os centros vêm dos sub-meshes do próprio modelo.</summary>
    private static GameObject WallPlatePrefab(Mesh mesh)
    {
        string path = Gameplay + "PontoEletrico.prefab";
        var root = new GameObject("PontoEletrico");
        var box = root.AddComponent<BoxCollider>();
        box.size = mesh.bounds.size; box.center = mesh.bounds.center;
        var vis = new GameObject("Visual");
        vis.transform.SetParent(root.transform, false);
        vis.AddComponent<MeshFilter>().sharedMesh = mesh;
        Material[] mats = MaterialsOf(mesh.name);
        vis.AddComponent<MeshRenderer>().sharedMaterials = mats;

        // tomadas = ilhas do material "rebaixo"; ponto de rede = material "interior" (keystone)
        List<Bounds> outlets = Islands(mesh, SubmeshOf(mats, "M_Tomada_Rebaixo"));
        Bounds jack = SubmeshBounds(mesh, SubmeshOf(mats, "M_Tomada_Interior"));
        float outSign = Mathf.Sign(jack.center.z - mesh.bounds.center.z);   // lado das tomadas = frente
        float faceZ = outSign > 0 ? mesh.bounds.max.z : mesh.bounds.min.z;
        Bounds pc = outlets.OrderByDescending(b => Mathf.Abs(b.center.x - jack.center.x)).First();
        Bounds mon = outlets.OrderBy(b => Mathf.Abs(b.center.x - jack.center.x)).First();
        Anchor(root.transform, "Porta3D_Outlet", new Vector3(pc.center.x, pc.center.y, faceZ), Vector3.forward * outSign);
        Anchor(root.transform, "Porta3D_MonitorOutlet", new Vector3(mon.center.x, mon.center.y, faceZ), Vector3.forward * outSign);
        Anchor(root.transform, "Porta3D_NetworkJack", new Vector3(jack.center.x, jack.center.y - 0.002f, faceZ), Vector3.forward * outSign);
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    private static int SubmeshOf(Material[] mats, string name)
    {
        for (int i = 0; i < mats.Length; i++) if (mats[i] != null && mats[i].name == name) return i;
        throw new System.Exception("material não encontrado: " + name);
    }

    private static IEnumerable<Vector3> SubmeshVerts(Mesh mesh, int sub)
    {
        Vector3[] v = mesh.vertices;
        return mesh.GetTriangles(sub).Distinct().Select(i => v[i]);
    }

    private static Bounds Enclose(IEnumerable<Vector3> pts)
    {
        Bounds b = default; bool first = true;
        foreach (Vector3 p in pts) { if (first) { b = new Bounds(p, Vector3.zero); first = false; } else b.Encapsulate(p); }
        return b;
    }

    private static Bounds SubmeshBounds(Mesh mesh, int sub) => Enclose(SubmeshVerts(mesh, sub));

    /// <summary>Agrupa os vértices de um sub-mesh em ilhas separadas por mais de 2 cm (ex.: as duas tomadas).</summary>
    private static List<Bounds> Islands(Mesh mesh, int sub)
    {
        var groups = new List<List<Vector3>>();
        foreach (Vector3 p in SubmeshVerts(mesh, sub))
        {
            var g = groups.FirstOrDefault(l => l.Any(q => (q - p).sqrMagnitude < 0.0004f));
            if (g == null) groups.Add(new List<Vector3> { p }); else g.Add(p);
        }
        return groups.Select(Enclose).ToList();
    }

    private static Transform Anchor(Transform parent, string name, Vector3 localPos, Vector3 localForward)
    {
        Transform t = parent.Find(name);
        if (t == null) { t = new GameObject(name).transform; t.SetParent(parent, false); }
        t.localPosition = localPos;
        t.localRotation = Quaternion.LookRotation(localForward, Vector3.up);
        return t;
    }

    // ------------------------------------------------------------------ portas do gabinete e do monitor
    /// <summary>
    /// Âncoras a partir dos sub-meshes do Visual (materiais dos conectores), em coordenadas do <paramref name="root"/>.
    /// A direção "para fora" é a da traseira: do centro do modelo para o conector, no eixo horizontal dominante.
    /// </summary>
    private static Dictionary<PcPort, (Vector3 pos, Vector3 fwd)> PortsFromVisual(Transform root, Transform visual, bool monitor)
    {
        var mf = visual.GetComponent<MeshFilter>();
        Mesh mesh = mf.sharedMesh;
        Material[] mats = visual.GetComponent<MeshRenderer>().sharedMaterials;
        Matrix4x4 toRoot = root.worldToLocalMatrix * visual.localToWorldMatrix;
        Bounds all = Enclose(mesh.vertices.Select(v => toRoot.MultiplyPoint3x4(v)));
        Bounds B(int sub, System.Func<Vector3, bool> keep = null) =>
            Enclose(SubmeshVerts(mesh, sub).Select(v => toRoot.MultiplyPoint3x4(v)).Where(p => keep == null || keep(p)));

        Bounds vga = B(SubmeshOf(mats, "M_PC_VGA"));
        Vector3 d = vga.center - all.center; d.y = 0;
        Vector3 back = Mathf.Abs(d.x) > Mathf.Abs(d.z) ? new Vector3(Mathf.Sign(d.x), 0, 0) : new Vector3(0, 0, Mathf.Sign(d.z));
        bool OnBack(Vector3 p) => Vector3.Dot(p - all.center, back) > 0f;
        float Face(Bounds b) => Vector3.Dot(b.center, back) + Vector3.Dot(b.extents, new Vector3(Mathf.Abs(back.x), 0, Mathf.Abs(back.z)));
        Vector3 At(Bounds b, float face) => b.center - back * Vector3.Dot(b.center, back) + back * face;

        var r = new Dictionary<PcPort, (Vector3, Vector3)>();
        Bounds hdmi = B(SubmeshOf(mats, "M_PC_HDMI"), OnBack);
        if (!monitor)
        {
            Bounds iec = B(SubmeshOf(mats, "M_PC_Pino"), p => OnBack(p) && p.y > all.center.y);
            Bounds led = B(SubmeshOf(mats, "M_PC_LED"), p => OnBack(p) && p.y < all.center.y);
            r[PcPort.CaseVideo] = (At(vga, Face(vga)), back);
            r[PcPort.CaseHdmi] = (At(hdmi, Face(hdmi)), back);
            r[PcPort.CasePower] = (At(iec, Face(iec)), back);
            Bounds rj = led; rj.center -= Vector3.up * 0.009f;                       // LEDs ficam acima do furo
            r[PcPort.CaseNetwork] = (At(rj, Face(led) - 0.002f), back);
        }
        else
        {
            r[PcPort.MonitorVideo] = (At(vga, Face(vga)), back);
            r[PcPort.MonitorHdmi] = (At(hdmi, Face(hdmi)), back);
            // energia: na mesma linha, em x real +0,105 (VGA em 0, HDMI em -0,085; ver pc_modelos_build.py)
            Bounds pw = vga; pw.center = vga.center + (vga.center - hdmi.center) * (0.105f / 0.085f);
            r[PcPort.MonitorPower] = (At(pw, Face(vga)), back);
        }
        return r;
    }

    private static PcCableView3D.CableStyle Style(CableType t, Dictionary<string, Mesh> meshes, string a, string b, string cableMat, float radius)
    {
        return new PcCableView3D.CableStyle
        {
            type = t,
            plugA = meshes[a], plugAMaterials = MaterialsOf(a),
            plugB = b != null ? meshes[b] : null, plugBMaterials = b != null ? MaterialsOf(b) : null,
            cableMaterial = AssetDatabase.LoadAssetAtPath<Material>(CabMats + cableMat + ".mat"),
            radius = radius,
        };
    }

    private static void ConfigureCase(Dictionary<string, Mesh> meshes)
    {
        string path = Gameplay + "Gabinete.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        var view = root.GetComponent<PcCableView3D>() ?? root.AddComponent<PcCableView3D>();
        var so = new SerializedObject(view);
        var anchors = new List<(PcPort, Transform)>();
        foreach (var kv in PortsFromVisual(root.transform, root.transform.Find("Visual"), false))
            anchors.Add((kv.Key, Anchor(root.transform, "Porta3D_" + kv.Key, kv.Value.pos, kv.Value.fwd)));
        WriteAnchors(so, anchors);
        var styles = new[]
        {
            Style(CableType.Power, meshes, "Plugue_Tomada", "Plugue_IEC", "M_Cabo_Preto", 0.0042f),
            Style(CableType.Video, meshes, "Plugue_VGA", null, "M_Cabo_Cinza", 0.0045f),
            Style(CableType.Hdmi, meshes, "Plugue_HDMI", null, "M_Cabo_Preto", 0.0034f),
            Style(CableType.Network, meshes, "Plugue_RJ45", null, "M_Cabo_Azul", 0.0030f),
        };
        SerializedProperty list = so.FindProperty("styles");
        list.arraySize = styles.Length;
        for (int i = 0; i < styles.Length; i++)
        {
            SerializedProperty e = list.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("type").intValue = (int)styles[i].type;
            e.FindPropertyRelative("plugA").objectReferenceValue = styles[i].plugA;
            e.FindPropertyRelative("plugB").objectReferenceValue = styles[i].plugB;
            SetMats(e.FindPropertyRelative("plugAMaterials"), styles[i].plugAMaterials);
            SetMats(e.FindPropertyRelative("plugBMaterials"), styles[i].plugBMaterials);
            e.FindPropertyRelative("cableMaterial").objectReferenceValue = styles[i].cableMaterial;
            e.FindPropertyRelative("radius").floatValue = styles[i].radius;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
    }

    private static void SetMats(SerializedProperty p, Material[] mats)
    {
        mats = mats ?? new Material[0];
        p.arraySize = mats.Length;
        for (int i = 0; i < mats.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = mats[i];
    }

    private static void WriteAnchors(SerializedObject so, List<(PcPort port, Transform t)> anchors)
    {
        SerializedProperty list = so.FindProperty("anchors");
        list.arraySize = anchors.Count;
        for (int i = 0; i < anchors.Count; i++)
        {
            list.GetArrayElementAtIndex(i).FindPropertyRelative("port").intValue = (int)anchors[i].port;
            list.GetArrayElementAtIndex(i).FindPropertyRelative("anchor").objectReferenceValue = anchors[i].t;
        }
    }

    // ------------------------------------------------------------------ cena
    private static void ConfigureScene(GameObject platePrefab)
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject gab = GameObject.Find("Gabinete");
        GameObject monitor = Object.FindObjectsByType<MonitorDisplay>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Select(m => m.transform).Select(t => { while (t.parent != null) t = t.parent; return t.gameObject; }).First();

        // espelho na parede de trás, à esquerda do gabinete, acima da mesa (o cabo não precisa passar por trás da mesa)
        GameObject old = GameObject.Find("PontoEletrico");
        if (old != null) Object.DestroyImmediate(old);
        var plate = (GameObject)PrefabUtility.InstantiatePrefab(platePrefab);
        Vector3 from = new Vector3(0.95f, 0.95f, gab.transform.position.z);
        Physics.SyncTransforms();
        if (!Physics.Raycast(from, Vector3.forward, out RaycastHit wall, 3f, ~0, QueryTriggerInteraction.Ignore))
            throw new System.Exception("parede de trás não encontrada");
        // a frente do espelho (Z local das âncoras) fica virada para a sala; as costas encostam na parede
        Transform a0 = plate.transform.Find("Porta3D_Outlet");
        Vector3 frontLocal = a0.localRotation * Vector3.forward;
        Vector3 n = new Vector3(wall.normal.x, 0f, wall.normal.z).normalized;
        plate.transform.rotation = Quaternion.LookRotation(n, Vector3.up) * Quaternion.Inverse(Quaternion.LookRotation(frontLocal, Vector3.up));
        var box = plate.GetComponent<BoxCollider>();
        Vector3 backLocal = new Vector3(0f, 0f, box.center.z - Mathf.Sign(frontLocal.z) * box.size.z * 0.5f);
        plate.transform.position = wall.point - plate.transform.rotation * backLocal + n * 0.001f;
        Debug.Log($"Cabos3DSetup: PontoEletrico em {plate.transform.position:F3} (parede {wall.collider.name}, normal {wall.normal:F2})");

        // âncoras do monitor (na instância da cena)
        Transform mVis = monitor.GetComponentsInChildren<MeshRenderer>(true)
            .Where(r => r.sharedMaterials.Any(m => m != null && m.name == "M_PC_VGA")).Select(r => r.transform).First();
        var anchors = new List<(PcPort, Transform)>();
        foreach (var kv in PortsFromVisual(monitor.transform, mVis, true))
            anchors.Add((kv.Key, Anchor(monitor.transform, "Porta3D_" + kv.Key, kv.Value.pos, kv.Value.fwd)));
        foreach (Transform t in new[] { "Porta3D_Outlet", "Porta3D_MonitorOutlet", "Porta3D_NetworkJack" }.Select(n => plate.transform.Find(n)))
            anchors.Add(((PcPort)System.Enum.Parse(typeof(PcPort), t.name.Substring("Porta3D_".Length)), t));
        foreach (PcPort p in new[] { PcPort.CasePower, PcPort.CaseVideo, PcPort.CaseHdmi, PcPort.CaseNetwork })
            anchors.Add((p, gab.transform.Find("Porta3D_" + p)));

        var so = new SerializedObject(gab.GetComponent<PcCableView3D>());
        WriteAnchors(so, anchors);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    // ------------------------------------------------------------------ Canvas
    private static Sprite Sp(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(Sprites + name + ".png");

    private static void ConfigureScreen()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(ScreenPath);
        Transform r = root.transform;
        var plugByPort = new Dictionary<PcPort, string>
        {
            { PcPort.Outlet, "ui_render_plugue_tomada" }, { PcPort.MonitorOutlet, "ui_render_plugue_tomada" },
            { PcPort.CasePower, "ui_render_plugue_energia" }, { PcPort.MonitorPower, "ui_render_plugue_energia" },
            { PcPort.CaseVideo, "ui_render_plugue_vga" }, { PcPort.MonitorVideo, "ui_render_plugue_vga" },
            { PcPort.CaseHdmi, "ui_render_plugue_hdmi" }, { PcPort.MonitorHdmi, "ui_render_plugue_hdmi" },
            { PcPort.CaseNetwork, "ui_render_plugue_rj45" }, { PcPort.NetworkJack, "ui_render_plugue_rj45" },
        };
        foreach (ComputerPortView pv in r.GetComponentsInChildren<ComputerPortView>(true))
        {
            Transform plug = pv.transform.Find("Plugue");
            if (plug == null) continue;
            var img = plug.GetComponent<Image>();
            img.sprite = Sp(plugByPort[pv.Port]); img.preserveAspect = true; img.color = Color.white;
            var prt = (RectTransform)plug; prt.anchorMin = new Vector2(-0.15f, -0.25f); prt.anchorMax = new Vector2(1.15f, 1.25f);
            prt.offsetMin = prt.offsetMax = Vector2.zero;
        }

        // tomadas e ponto de rede = renders do espelho de parede (módulo 0,07 x 0,09 m)
        foreach (var (obj, sprite) in new[] { ("Traseira/Tomada", "ui_render_tomada"), ("Traseira/Tomada_Monitor", "ui_render_tomada"), ("Traseira/PontoRede", "ui_render_ponto_rede") })
        {
            Transform t = r.Find(obj);
            var img = t.GetComponent<Image>(); img.sprite = Sp(sprite); img.preserveAspect = true;
            float a = img.sprite.rect.width / img.sprite.rect.height;
            foreach (AspectRatioFitter f in t.GetComponentsInChildren<AspectRatioFitter>(true)) f.aspectRatio = a;
        }
        SetAnchor(r.Find("Traseira/Tomada/Area/Porta_Tomada"), 0.20f, 0.30f, 0.80f, 0.70f);
        SetAnchor(r.Find("Traseira/Tomada_Monitor/Area/Porta_TomadaMonitor"), 0.20f, 0.30f, 0.80f, 0.70f);
        SetAnchor(r.Find("Traseira/PontoRede/Area/Porta_Rede"), 0.30f, 0.34f, 0.70f, 0.62f);

        // cabos curvos (UICableCurve no lugar do Image reto) com plugue na ponta solta
        var loose = new Dictionary<PcLink, (string a, string b)>
        {
            { PcLink.Power, ("ui_render_plugue_tomada", "ui_render_plugue_energia") },
            { PcLink.MonitorPower, ("ui_render_plugue_tomada", "ui_render_plugue_energia") },
            { PcLink.Vga, ("ui_render_plugue_vga", "ui_render_plugue_vga") },
            { PcLink.Hdmi, ("ui_render_plugue_hdmi", "ui_render_plugue_hdmi") },
            { PcLink.Network, ("ui_render_plugue_rj45", "ui_render_plugue_rj45") },
        };
        var colors = new Dictionary<PcLink, Color>
        {
            { PcLink.Power, new Color(0.10f, 0.10f, 0.12f) }, { PcLink.MonitorPower, new Color(0.10f, 0.10f, 0.12f) },
            { PcLink.Vga, new Color(0.80f, 0.80f, 0.78f) }, { PcLink.Hdmi, new Color(0.16f, 0.16f, 0.18f) },
            { PcLink.Network, new Color(0.22f, 0.50f, 0.90f) },
        };
        foreach (CableLineView line in r.GetComponentsInChildren<CableLineView>(true))
        {
            GameObject g = line.gameObject;
            var oldImg = g.GetComponent<Image>();
            if (oldImg != null) Object.DestroyImmediate(oldImg);
            var curve = g.GetComponent<UICableCurve>() ?? g.AddComponent<UICableCurve>();
            curve.color = colors[line.Link];
            curve.raycastTarget = false;
            var rt = (RectTransform)g.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            rt.localRotation = Quaternion.identity; rt.pivot = new Vector2(0.5f, 0.5f);
            Transform le = g.transform.Find("PontaSolta");
            if (le == null)
            {
                le = new GameObject("PontaSolta", typeof(RectTransform), typeof(Image)).transform;
                le.SetParent(g.transform, false);
            }
            var leImg = le.GetComponent<Image>(); leImg.preserveAspect = true; leImg.raycastTarget = false;
            ((RectTransform)le).sizeDelta = new Vector2(70f, 46f);
            le.gameObject.SetActive(false);
            var so = new SerializedObject(line);
            so.FindProperty("looseEnd").objectReferenceValue = leImg;
            so.FindProperty("looseSpriteA").objectReferenceValue = Sp(loose[line.Link].a);
            so.FindProperty("looseSpriteB").objectReferenceValue = Sp(loose[line.Link].b);
            so.FindProperty("thickness").floatValue = line.Link == PcLink.Network || line.Link == PcLink.Hdmi ? 8f : 11f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        PrefabUtility.SaveAsPrefabAsset(root, ScreenPath);
        PrefabUtility.UnloadPrefabContents(root);
    }

    private static void SetAnchor(Transform t, float x0, float y0, float x1, float y1)
    {
        var rt = (RectTransform)t;
        rt.anchorMin = new Vector2(x0, y0); rt.anchorMax = new Vector2(x1, y1); rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
