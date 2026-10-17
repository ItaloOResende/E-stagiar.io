using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Monta os dois gaveteiros da sala (hardware e cabos), com gavetas ocas, puxador do Matheus e plaquinhas,
/// guarda os itens soltos da cena dentro deles e ajusta a altura do personagem.
/// Idempotente. Menu: Tools > E-stagiar.io > Montar gaveteiros. Batch: -executeMethod GaveteirosSetup.Run
///
/// Medidas (m, espaço local do gaveteiro; frente em +Z, origem no chão, no centro):
///   corpo 0,70 x 1,10 x 0,56; 4 gavetas (centros y 0,89 / 0,66 / 0,43 / 0,20), frente 0,658 x 0,21;
///   caixa interna 0,576 x 0,50 (fundo da gaveta em y local -0,073, paredes até +0,065).
/// </summary>
public static class GaveteirosSetup
{
    private const float W = 0.70f, H = 1.10f, D = 0.56f, T = 0.018f;
    private static readonly float[] DrawerY = { 0.89f, 0.66f, 0.43f, 0.20f }; // Gaveta_1 = a de cima
    private const float FrontW = 0.658f, FrontH = 0.21f, FrontT = 0.02f;
    private const float TrayW = 0.60f, TrayD = 0.50f, TrayZ0 = -0.24f, Wall = 0.012f, TrayBottom = -0.085f, WallTop = 0.065f;
    /// <summary>Altura (local da gaveta) do fundo onde os itens apoiam.</summary>
    public const float Floor = TrayBottom + Wall;
    /// <summary>Limites internos da caixa (local da gaveta), usados também pelo teste.</summary>
    public static readonly Bounds Inner = new Bounds(
        new Vector3(0f, (Floor + WallTop) / 2f, TrayZ0 + Wall + (TrayD - Wall) / 2f),
        new Vector3(TrayW - 2 * Wall, WallTop - Floor, TrayD - Wall));

    /// <summary>
    /// Gatilho "AreaGuardar" da gaveta: o espaço interno (fundo até um pouco acima das paredes), onde o jogador guarda
    /// itens de volta (DrawerContents.storageArea). Só gatilho: não colide com nada nem atrapalha a mira.
    /// </summary>
    public static BoxCollider StorageArea(Transform drawer)
    {
        Transform t = drawer.Find("AreaGuardar");
        if (t == null) { t = new GameObject("AreaGuardar").transform; t.SetParent(drawer, false); }
        t.localPosition = Vector3.zero; t.localRotation = Quaternion.identity; t.localScale = Vector3.one;
        BoxCollider box = t.GetComponent<BoxCollider>();
        if (box == null) box = t.gameObject.AddComponent<BoxCollider>();
        box.isTrigger = true;
        float h = WallTop - Floor + 0.10f;
        box.size = new Vector3(Inner.size.x, h, Inner.size.z);
        box.center = new Vector3(Inner.center.x, Floor + h / 2f, Inner.center.z);
        return box;
    }

    // Estoque inicial do protótipo (unidades FÍSICAS, finitas). Posições no espaço local da gaveta, dentro de Inner
    // (0,576 x 0,488): placa-mãe = 2 (cada uma tem 25 x 25 cm; 3 não cabem lado a lado nem empilhadas no fundo).
    private static readonly (string label, Item[] items)[] HardwareDrawers =
    {
        ("ram_cooler", new[] { new Item("Pickup_MemoriaRAM", 3, -0.15f, 0.19f, 0f, -0.07f), new Item("Pickup_Cooler", 2, 0.10f, 0.12f, 0.11f) }),
        ("placa_mae", new[] { new Item("Pickup_PlacaMae", 2, -0.135f, 0.016f, 0.27f) }),
        ("hd_fonte", new[] { new Item("Pickup_HD", 2, -0.22f, 0.10f, 0.12f), new Item("Pickup_Fonte", 2, 0.05f, 0.10f, 0.16f) }),
        ("ferramentas", new[] { new Item("Pickup_Ferramenta", 2, -0.14f, 0.09f, 0.28f), new Item("Pickup_Pendrive", 1, 0f, 0.09f),
                                new Item("Pickup_Alicate", 1, 0f, -0.07f) }),
    };
    private static readonly (string label, Item[] items)[] CableDrawers =
    {
        ("energia", new[] { new Item("Pickup_CaboEnergia", 2, -0.14f, 0.13f, 0.28f) }),
        ("vga", new[] { new Item("Pickup_CaboVideo", 2, -0.14f, 0.12f, 0.28f) }),
        ("hdmi", new[] { new Item("Pickup_CaboHDMI", 2, -0.14f, 0.13f, 0.28f) }),
        ("rede", new[] { new Item("Pickup_CaboRede", 2, -0.14f, 0.13f, 0.28f) }),
    };

    // Gaveteiro de suprimentos (notebook e impressora): memória SO-DIMM e SSD, pendrives, toner e papel.
    private static readonly (string label, Item[] items)[] SupplyDrawers =
    {
        ("memoria_ssd", new[] { new Item("Pickup_RamSodimm", 3, -0.15f, 0.11f, 0.13f), new Item("Pickup_Ssd", 3, -0.15f, -0.09f, 0.13f) }),
        ("pendrive", new[] { new Item("Pickup_Pendrive", 1, -0.15f, 0f, 0.15f) }),   // 2026-10-17: era 3 (balanceamento)
        ("toner", new[] { new Item("Pickup_Toner", 2, 0f, 0.11f, 0f, -0.2f) }),
        ("papel", new[] { new Item("Pickup_ResmaPapel", 2, -0.13f, 0f, 0.26f) }),
    };

    /// <summary>
    /// Monta o prefab Gaveteiro_Suprimentos (mesmo corpo, gavetas, puxador do Matheus e plaquinhas dos outros dois)
    /// com as peças do notebook e da impressora. Usa as malhas já gravadas pelo "Montar gaveteiros".
    /// </summary>
    public static GameObject BuildSupplies()
    {
        Mesh body = AssetDatabase.LoadAssetAtPath<Mesh>(Meshes + "Gaveteiro_Corpo.asset");
        Mesh drawer = AssetDatabase.LoadAssetAtPath<Mesh>(Meshes + "Gaveteiro_Gaveta.asset");
        Mesh handle = AssetDatabase.LoadAssetAtPath<Mesh>(Meshes + "Gaveteiro_Puxador.asset");
        Mesh plate = AssetDatabase.LoadAssetAtPath<Mesh>(Meshes + "Gaveteiro_Plaquinha.asset");
        if (body == null || drawer == null || handle == null || plate == null) { Debug.LogError("GaveteirosSetup: rode 'Montar gaveteiros' antes."); return null; }
        return BuildCabinet("Gaveteiro_Suprimentos", "suprimentos", body, drawer, handle, plate, SupplyDrawers);
    }

    /// <summary>
    /// Só reescreve o estoque (lista de itens de cada gaveta) dos prefabs já montados, sem remontar malhas, cena
    /// ou gaveteiros. Menu: Tools > E-stagiar.io > Atualizar estoque das gavetas.
    /// </summary>
    [MenuItem("Tools/E-stagiar.io/Atualizar estoque das gavetas")]
    public static void UpdateStock()
    {
        foreach (var (name, drawers) in new[] { ("Gaveteiro_Hardware", HardwareDrawers), ("Gaveteiro_Cabos", CableDrawers) })
        {
            string path = Gameplay + name + ".prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            for (int d = 0; d < drawers.Length; d++)
                WriteEntries(root.transform.Find("Gaveta_" + (d + 1)).GetComponent<DrawerContents>(), drawers[d].items);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }
        AssetDatabase.SaveAssets();
    }

    private static void WriteEntries(DrawerContents contents, Item[] items)
    {
        var cso = new SerializedObject(contents);
        SerializedProperty list = cso.FindProperty("entries");
        list.arraySize = items.Length;
        for (int i = 0; i < items.Length; i++)
        {
            SerializedProperty e = list.GetArrayElementAtIndex(i);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Gameplay + items[i].prefab + ".prefab");
            e.FindPropertyRelative("prefab").objectReferenceValue = prefab.GetComponent<PickupInteractable>();
            e.FindPropertyRelative("quantity").intValue = items[i].qty;
            e.FindPropertyRelative("delivery").intValue = (int)DrawerContents.Delivery.PhysicalInDrawer;
            e.FindPropertyRelative("localPosition").vector3Value = items[i].pos;
            e.FindPropertyRelative("localEuler").vector3Value = new Vector3(0, items[i].yaw, 0);
            e.FindPropertyRelative("step").vector3Value = items[i].step;
        }
        cso.ApplyModifiedPropertiesWithoutUndo();
    }

    private const string Meshes = "Assets/Art/Meshes/";
    private const string Mats = "Assets/Art/Materials/";
    private const string Labels = "Assets/Art/Textures/Plaquinhas/";
    private const string Gameplay = "Assets/Prefabs/Gameplay/";
    private const string ScenePath = "Assets/Scenes/Office_Placeholder.unity";
    private const string Claro = Mats + "M_Escritorio_Claro.mat", Escuro = Mats + "M_Escritorio_Escuro.mat", Metal = Mats + "M_PC_Chapa.mat";

    private struct Item
    {
        public string prefab; public int qty; public Vector3 pos; public Vector3 step; public float yaw;
        public Item(string prefab, int qty, float x, float z, float stepX = 0f, float stepZ = 0f, float yaw = 0f)
        { this.prefab = prefab; this.qty = qty; pos = new Vector3(x, Floor + 0.002f, z); step = new Vector3(stepX, 0f, stepZ); this.yaw = yaw; }
    }

    [MenuItem("Tools/E-stagiar.io/Montar gaveteiros")]
    public static void Run()
    {
        AssetDatabase.Refresh();
        System.IO.Directory.CreateDirectory(Meshes);
        Mesh body = SaveMesh(BuildBody(), "Gaveteiro_Corpo");
        Mesh drawer = SaveMesh(BuildDrawer(), "Gaveteiro_Gaveta");
        Mesh handle = SaveMesh(BuildHandleFromMatheus(), "Gaveteiro_Puxador");
        Mesh plate = SaveMesh(BuildPlate(), "Gaveteiro_Plaquinha");
        FitCables();

        var hardware = BuildCabinet("Gaveteiro_Hardware", "hardware", body, drawer, handle, plate, HardwareDrawers);
        var cables = BuildCabinet("Gaveteiro_Cabos", "cabos", body, drawer, handle, plate, CableDrawers);
        ConfigureScene(hardware, cables);
        AssetDatabase.SaveAssets();
        Debug.Log("GaveteirosSetup: concluído.");
    }

    // ------------------------------------------------------------------ malhas
    private class MeshBuilder
    {
        public readonly List<Vector3> v = new List<Vector3>();
        public readonly List<Vector3> n = new List<Vector3>();
        public readonly List<Vector2> uv = new List<Vector2>();
        public readonly List<List<int>> sub = new List<List<int>>();

        public MeshBuilder(int submeshes) { for (int i = 0; i < submeshes; i++) sub.Add(new List<int>()); }

        public void Quad(int s, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
        {
            int i = v.Count;
            v.AddRange(new[] { a, b, c, d });
            n.AddRange(new[] { normal, normal, normal, normal });
            uv.AddRange(new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) });
            sub[s].AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 }); // horário visto de fora (frente no Unity)
        }

        public void Box(int s, Vector3 center, Vector3 size)
        {
            Vector3 h = size / 2f;
            Vector3 P(float x, float y, float z) => center + new Vector3(x * h.x, y * h.y, z * h.z);
            Quad(s, P(-1, -1, 1), P(-1, 1, 1), P(1, 1, 1), P(1, -1, 1), Vector3.forward);      // +Z
            Quad(s, P(1, -1, -1), P(1, 1, -1), P(-1, 1, -1), P(-1, -1, -1), Vector3.back);     // -Z
            Quad(s, P(1, -1, 1), P(1, 1, 1), P(1, 1, -1), P(1, -1, -1), Vector3.right);        // +X
            Quad(s, P(-1, -1, -1), P(-1, 1, -1), P(-1, 1, 1), P(-1, -1, 1), Vector3.left);     // -X
            Quad(s, P(-1, 1, 1), P(-1, 1, -1), P(1, 1, -1), P(1, 1, 1), Vector3.up);           // +Y
            Quad(s, P(-1, -1, -1), P(-1, -1, 1), P(1, -1, 1), P(1, -1, -1), Vector3.down);     // -Y
        }

        public Mesh ToMesh(string name)
        {
            var m = new Mesh { name = name };
            m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv);
            m.subMeshCount = sub.Count;
            for (int i = 0; i < sub.Count; i++) m.SetTriangles(sub[i], i);
            m.RecalculateBounds();
            return m;
        }
    }

    private static Mesh BuildBody()
    {
        var b = new MeshBuilder(1);
        const float plinth = 0.075f;
        b.Box(0, new Vector3(-(W - T) / 2, H / 2, 0), new Vector3(T, H, D));                    // laterais
        b.Box(0, new Vector3((W - T) / 2, H / 2, 0), new Vector3(T, H, D));
        b.Box(0, new Vector3(0, H - T / 2, 0), new Vector3(W, T, D));                           // tampo
        b.Box(0, new Vector3(0, plinth + T / 2, 0), new Vector3(W - 2 * T, T, D));              // fundo
        b.Box(0, new Vector3(0, plinth / 2, -0.015f), new Vector3(W - 2 * T, plinth, D - 0.03f)); // rodapé recuado
        b.Box(0, new Vector3(0, (plinth + H - T) / 2, -D / 2 + T / 2), new Vector3(W - 2 * T, H - T - plinth, T)); // costas
        float topFront = DrawerY[0] + FrontH / 2 + 0.005f;                                      // faixa do título
        b.Box(0, new Vector3(0, (topFront + H - T) / 2, D / 2 - T / 2), new Vector3(W - 2 * T, H - T - topFront, T));
        for (int i = 0; i < DrawerY.Length - 1; i++)                                            // travessas entre gavetas
        {
            float y = (DrawerY[i] + DrawerY[i + 1]) / 2;
            b.Box(0, new Vector3(0, y, D / 2 - 0.02f), new Vector3(W - 2 * T, T, 0.04f));
        }
        return b.ToMesh("Gaveteiro_Corpo");
    }

    private static Mesh BuildDrawer()
    {
        var b = new MeshBuilder(2); // 0 = frente (madeira clara), 1 = caixa interna (escura, contrasta com as peças)
        b.Box(0, new Vector3(0, 0, D / 2 - FrontT / 2), new Vector3(FrontW, FrontH, FrontT));
        float h = WallTop - TrayBottom, yc = (WallTop + TrayBottom) / 2, zc = TrayZ0 + TrayD / 2;
        b.Box(1, new Vector3(0, TrayBottom + Wall / 2, zc), new Vector3(TrayW, Wall, TrayD));   // fundo
        b.Box(1, new Vector3(-(TrayW - Wall) / 2, yc, zc), new Vector3(Wall, h, TrayD));        // laterais
        b.Box(1, new Vector3((TrayW - Wall) / 2, yc, zc), new Vector3(Wall, h, TrayD));
        b.Box(1, new Vector3(0, yc, TrayZ0 + Wall / 2), new Vector3(TrayW, h, Wall));            // fundo de trás
        return b.ToMesh("Gaveteiro_Gaveta");
    }

    private static Mesh BuildPlate()
    {
        var b = new MeshBuilder(1);
        // quadrado unitário voltado para +Z; visto de +Z a direita é -X, por isso o U cresce para -X
        b.Quad(0, new Vector3(-0.5f, -0.5f, 0), new Vector3(-0.5f, 0.5f, 0), new Vector3(0.5f, 0.5f, 0), new Vector3(0.5f, -0.5f, 0), Vector3.forward);
        var m = b.ToMesh("Gaveteiro_Plaquinha");
        m.SetUVs(0, new List<Vector2> { new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1), new Vector2(0, 0) });
        return m;
    }

    /// <summary>
    /// Puxador = a peça solta da malha do gaveta.fbx (Matheus), sem alteração de forma: só reorientada
    /// (frente → +Z) e escalada para 14 cm de comprimento.
    /// </summary>
    private static Mesh BuildHandleFromMatheus()
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Models/gaveta.fbx");
        MeshFilter mf = model.GetComponentInChildren<MeshFilter>();
        Mesh src = mf.sharedMesh;
        Matrix4x4 toModel = model.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
        Vector3[] verts = src.vertices.Select(p => toModel.MultiplyPoint3x4(p)).ToArray();
        int[] tris = src.triangles;

        // ilhas por posição (o FBX duplica vértices nas arestas): união pelos triângulos + posições iguais
        var parent = Enumerable.Range(0, verts.Length).ToArray();
        int Find(int x) { while (parent[x] != x) x = parent[x] = parent[parent[x]]; return x; }
        void Union(int a, int c) { parent[Find(a)] = Find(c); }
        for (int i = 0; i < tris.Length; i += 3) { Union(tris[i], tris[i + 1]); Union(tris[i], tris[i + 2]); }
        var byPos = new Dictionary<Vector3Int, int>();
        for (int i = 0; i < verts.Length; i++)
        {
            var key = Vector3Int.RoundToInt(verts[i] * 10000f);
            if (byPos.TryGetValue(key, out int j)) Union(i, j); else byPos[key] = i;
        }
        var islands = Enumerable.Range(0, verts.Length).GroupBy(Find).OrderBy(g => g.Count()).ToList();
        var handleSet = new HashSet<int>(islands[0]);
        Bounds all = new Bounds(verts[0], Vector3.zero); foreach (var p in verts) all.Encapsulate(p);
        Bounds hb = new Bounds(verts[islands[0].First()], Vector3.zero); foreach (int i in handleSet) hb.Encapsulate(verts[i]);

        Vector3 dir = hb.center - all.center; dir.y = 0;
        Vector3 front = Mathf.Abs(dir.x) > Mathf.Abs(dir.z) ? new Vector3(Mathf.Sign(dir.x), 0, 0) : new Vector3(0, 0, Mathf.Sign(dir.z));
        Quaternion rot = Quaternion.Inverse(Quaternion.LookRotation(front, Vector3.up));

        var map = new Dictionary<int, int>();
        var nv = new List<Vector3>();
        foreach (int i in handleSet) { map[i] = nv.Count; nv.Add(rot * (verts[i] - hb.center)); }
        Bounds nb = new Bounds(nv[0], Vector3.zero); foreach (var p in nv) nb.Encapsulate(p);
        float k = 0.14f / nb.size.x;
        for (int i = 0; i < nv.Count; i++) nv[i] = (nv[i] - new Vector3(nb.center.x, nb.center.y, nb.min.z)) * k;
        var nt = new List<int>();
        for (int i = 0; i < tris.Length; i += 3)
            if (handleSet.Contains(tris[i])) nt.AddRange(new[] { map[tris[i]], map[tris[i + 1]], map[tris[i + 2]] });
        var m = new Mesh { name = "Gaveteiro_Puxador" };
        m.SetVertices(nv); m.SetTriangles(nt, 0);
        m.RecalculateNormals(); m.RecalculateBounds();
        Debug.Log($"GaveteirosSetup: puxador do Matheus com {nv.Count} vértices, frente original {front}, tamanho {m.bounds.size:F3}");
        return m;
    }

    private static Mesh SaveMesh(Mesh m, string name)
    {
        string path = Meshes + name + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null) { AssetDatabase.CreateAsset(m, path); return m; }
        existing.Clear();
        EditorUtility.CopySerialized(m, existing);
        existing.name = name;
        EditorUtility.SetDirty(existing);
        return existing;
    }

    /// <summary>Forro cinza-claro da caixa interna: contrasta com os cabos pretos e com as peças coloridas.</summary>
    private static Material LiningMaterial()
    {
        string path = Mats + "M_Gaveta_Forro.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        m.SetColor("_BaseColor", new Color(0.66f, 0.67f, 0.70f));
        m.SetFloat("_Smoothness", 0.15f);
        EditorUtility.SetDirty(m);
        return m;
    }

    // ------------------------------------------------------------------ materiais das plaquinhas
    private static Material LabelMaterial(string id)
    {
        string tex = Labels + "plaquinha_" + id + ".png";
        var imp = (TextureImporter)AssetImporter.GetAtPath(tex);
        if (imp.filterMode != FilterMode.Point || imp.mipmapEnabled || imp.wrapMode != TextureWrapMode.Clamp || !imp.alphaIsTransparency)
        {
            imp.filterMode = FilterMode.Point; imp.mipmapEnabled = false; imp.wrapMode = TextureWrapMode.Clamp;
            imp.alphaIsTransparency = true; imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();
        }
        System.IO.Directory.CreateDirectory(Mats + "Plaquinhas");
        string path = Mats + "Plaquinhas/M_Plaquinha_" + id + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(tex));
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_Smoothness", 0.25f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // ------------------------------------------------------------------ cabos (cabem na gaveta)
    /// <summary>Os modelos dos cabos (placeholders) tinham ~1,2 m: o Visual é reduzido para um rolo de até ~15 cm (os rolos atuais, de cabos.fbx, já cabem e não mudam).</summary>
    private static void FitCables()
    {
        foreach (string name in new[] { "Pickup_CaboEnergia", "Pickup_CaboVideo", "Pickup_CaboHDMI", "Pickup_CaboRede" })
        {
            string path = Gameplay + name + ".prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            Transform visual = root.transform.Find("Visual");
            Bounds b = LocalBounds(root.transform, visual);
            float k = Mathf.Min(0.15f / Mathf.Max(b.size.x, b.size.z), 0.10f / b.size.y);
            if (k < 0.98f)
            {
                visual.localScale *= k;
                visual.localPosition *= k;
                b = LocalBounds(root.transform, visual);
                var box = root.GetComponent<BoxCollider>();
                box.size = b.size; box.center = b.center;
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"GaveteirosSetup: {name} reduzido x{k:F3} -> {b.size:F3}");
            }
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static Bounds LocalBounds(Transform root, Transform part)
    {
        var mfs = part.GetComponentsInChildren<MeshFilter>(true);
        bool first = true; Bounds b = default;
        foreach (var mf in mfs)
        {
            if (mf.sharedMesh == null) continue;
            Matrix4x4 m = root.worldToLocalMatrix * mf.transform.localToWorldMatrix;
            Bounds mb = mf.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 p = m.MultiplyPoint3x4(c);
                if (first) { b = new Bounds(p, Vector3.zero); first = false; } else b.Encapsulate(p);
            }
        }
        return b;
    }

    // ------------------------------------------------------------------ prefabs dos gaveteiros
    private static GameObject Part(string name, Transform parent, Mesh mesh, Material[] mats, Vector3 pos, Vector3 scale)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterials = mats;
        return go;
    }

    private static GameObject BuildCabinet(string name, string titleId, Mesh body, Mesh drawer, Mesh handle, Mesh plate,
                                          (string label, Item[] items)[] drawers)
    {
        var claro = AssetDatabase.LoadAssetAtPath<Material>(Claro);
        var forro = LiningMaterial();
        var metal = AssetDatabase.LoadAssetAtPath<Material>(Metal);

        var root = new GameObject(name);
        var rootBox = root.AddComponent<BoxCollider>();
        // corpo recuado 2 cm atrás das frentes: a mira nas gavetas nunca bate no corpo (que não é interagível)
        const float front = D / 2 - FrontT;
        rootBox.size = new Vector3(W, H, front + D / 2); rootBox.center = new Vector3(0, H / 2, (front - D / 2) / 2);
        Part("Visual_Corpo", root.transform, body, new[] { claro }, Vector3.zero, Vector3.one);
        float titleY = (DrawerY[0] + FrontH / 2 + H - T) / 2;
        Part("Plaquinha_Titulo", root.transform, plate, new[] { LabelMaterial("titulo_" + titleId) },
             new Vector3(0, titleY, D / 2 + 0.0015f), new Vector3(0.36f, 0.065f, 1f));

        for (int d = 0; d < drawers.Length; d++)
        {
            var g = new GameObject("Gaveta_" + (d + 1));
            g.transform.SetParent(root.transform, false);
            g.transform.localPosition = new Vector3(0, DrawerY[d], 0);
            var box = g.AddComponent<BoxCollider>();                      // só a frente: não cobre os itens dentro
            box.size = new Vector3(FrontW, FrontH, 0.04f); box.center = new Vector3(0, 0, D / 2 + 0.005f);
            var open = g.AddComponent<OpenableInteractable>();
            var so = new SerializedObject(open);
            so.FindProperty("interactionMessage").stringValue = "Abrir/Fechar";
            so.FindProperty("localAxis").vector3Value = Vector3.forward;
            so.FindProperty("openAmount").floatValue = 0.4f;
            so.ApplyModifiedPropertiesWithoutUndo();

            Part("Visual", g.transform, drawer, new[] { claro, forro }, Vector3.zero, Vector3.one);
            Part("Puxador", g.transform, handle, new[] { metal }, new Vector3(0, -0.035f, D / 2), Vector3.one);
            Part("Plaquinha", g.transform, plate, new[] { LabelMaterial(drawers[d].label) },
                 new Vector3(0, 0.05f, D / 2 + 0.0015f), new Vector3(0.30f, 0.075f, 1f));

            var contents = g.AddComponent<DrawerContents>();
            var cso = new SerializedObject(contents);
            cso.ApplyModifiedPropertiesWithoutUndo();
            WriteEntries(contents, drawers[d].items);
            cso.Update();
            cso.FindProperty("hideContentsWhenClosed").boolValue = true;
            cso.FindProperty("storageArea").objectReferenceValue = StorageArea(g.transform);
            cso.ApplyModifiedPropertiesWithoutUndo();
        }

        string path = Gameplay + name + ".prefab";
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return saved;
    }

    // ------------------------------------------------------------------ cena e personagem
    private static readonly string[] LooseItems =
    {
        "CaboEnergia_Pickup", "CaboRede_Pickup", "CaboRede_Pickup (1)", "CaboVideo_Pickup_Longe",
        "Pickup_Fonte", "Pickup_Cooler", "Pickup_CaboHDMI", "Pickup_CaboEnergia_Monitor", "Gaveteiro_Teste",
    };

    private static void ConfigureScene(GameObject hardware, GameObject cables)
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Select(t => t.gameObject).ToList();
        foreach (GameObject g in all)
            if (g != null && g.transform.parent == null && (LooseItems.Contains(g.name) || g.name == "Gaveteiro_Hardware" || g.name == "Gaveteiro_Cabos"))
                Object.DestroyImmediate(g);

        // Lado a lado no vão entre as duas mesas, de frente para a sala (mesmo lugar do Gaveteiro_Teste).
        Place(hardware, new Vector3(0.45f, 0f, 2.68f));
        Place(cables, new Vector3(-0.45f, 0f, 2.68f));

        // Personagem: escala 0,514 -> 0,85 (cápsula de 2 m x 0,85 = 1,70 m; modelo ~1,65 m; olhos ~1,53 m).
        GameObject player = GameObject.Find("Player");
        const float scale = 0.85f;
        player.transform.localScale = Vector3.one * scale;
        var cc = player.GetComponent<CharacterController>();
        cc.radius = 0.35f; // raio no mundo 0,30 m (com 0,5 seria 0,43 m: largo demais para os corredores)
        Vector3 p = player.transform.position;
        player.transform.position = new Vector3(p.x, cc.height / 2f * scale + cc.skinWidth, p.z); // começa apoiado no chão

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void Place(GameObject prefab, Vector3 pos)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, 180f, 0f));
    }
}
