using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pendrive de instalação do sistema (idempotente; Tools > E-stagiar.io > Configurar pendrive):
///  - item Pickup_Pendrive (cópia do Pickup_HD com PendriveItem no lugar do ComputerPartItem e o modelo
///    Item_Pendrive de componentes.fbx, gerado por Tools/blender/componentes_build.py);
///  - Gabinete: âncoras Porta3D_Usb0..2 nas 3 USB da traseira (achadas na malha: caixas pretas 9 x 16 mm do I/O,
///    gabinete_v2_build.USB_XN/USB_YN) + PcPendriveView3D com o pendrive encaixado;
///  - tela UI_Screen_Gabinete: Porta_USB_0..2 (UsbPortView) na Traseira, cópias de Porta_RedeGabinete;
///  - Gaveteiro_Hardware: 1 pendrive na gaveta de ferramentas.
/// Antes de rodar: copiar Art/Source/Blender/export/componentes.fbx para Assets/Art/Models e
/// Art/Source/Blender/canvas/ui_render_pendrive.png para Assets/Art/UI (o script copia se existirem).
/// </summary>
public static class PendriveSetup
{
    private const string Gameplay = "Assets/Prefabs/Gameplay/";
    private const string ItemPath = Gameplay + "Pickup_Pendrive.prefab";
    private const string CasePath = Gameplay + "Gabinete.prefab";
    private const string ScreenPath = "Assets/Prefabs/UI/UI_Screen_Gabinete.prefab";
    private const string Fbx = "Assets/Art/Models/componentes.fbx";
    private const string Sprite = "Assets/Art/UI/ui_render_pendrive.png";
    private const string PortMaterial = "M_PC_Porta";

    // Mesmos números do gabinete_v2_build.py (pc_back normalizado: x da esquerda vista de trás, y de cima).
    private static readonly float[] UsbXn = { 0.23f, 0.32f, 0.41f };
    private const float UsbYn = 0.47f, CaseW = 0.2982f, CaseH = 0.5194f;
    private const float BodyW = 0.010f, BodyH = 0.020f;   // corpo do pendrive visto de trás (m)
    private const float PlugDepth = 0.011f;               // quanto do conector entra na porta (m)

    [MenuItem("Tools/E-stagiar.io/Configurar pendrive")]
    public static void Run()
    {
        CopySources();
        Mesh mesh = AssetDatabase.LoadAllAssetsAtPath(Fbx).OfType<Mesh>().First(m => m.name == "Item_Pendrive");
        Material[] mats = MaterialsOf("Item_Pendrive");
        BuildItem(mesh, mats);
        BuildCase(mesh, mats);
        BuildScreen();
        AddToDrawer();
        AssetDatabase.SaveAssets();
        Debug.Log("PendriveSetup: pronto");
    }

    // ------------------------------------------------------------------ arquivos gerados fora do Unity
    private static void CopySources()
    {
        var copies = new[] { ("Art/Source/Blender/export/componentes.fbx", Fbx), ("Art/Source/Blender/canvas/ui_render_pendrive.png", Sprite) };
        foreach (var (src, dst) in copies)
            if (File.Exists(src)) File.Copy(src, dst, true);
        AssetDatabase.Refresh();
        var imp = (TextureImporter)AssetImporter.GetAtPath(Sprite);
        if (imp.textureType != TextureImporterType.Sprite || imp.mipmapEnabled)
        {
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();
        }
    }

    private static Material[] MaterialsOf(string meshName)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
        return model.GetComponentsInChildren<MeshFilter>(true).First(f => f.sharedMesh.name == meshName)
            .GetComponent<MeshRenderer>().sharedMaterials;
    }

    // ------------------------------------------------------------------ item do mundo
    private static void BuildItem(Mesh mesh, Material[] mats)
    {
        // Parte do Pickup_HD (mesmos PickupInteractable/Rigidbody/prompt) e troca só o que é do pendrive.
        GameObject root = PrefabUtility.LoadPrefabContents(File.Exists(ItemPath) ? ItemPath : Gameplay + "Pickup_HD.prefab");
        root.name = "Pickup_Pendrive";
        foreach (var part in root.GetComponents<ComputerPartItem>()) Object.DestroyImmediate(part);
        if (root.GetComponent<PendriveItem>() == null) root.AddComponent<PendriveItem>();

        Transform visual = root.transform.Find("Visual");
        for (int i = visual.childCount - 1; i >= 0; i--) Object.DestroyImmediate(visual.GetChild(i).gameObject);
        visual.localPosition = Vector3.zero; visual.localRotation = Quaternion.identity; visual.localScale = Vector3.one;
        var mf = Get<MeshFilter>(visual);
        var mr = Get<MeshRenderer>(visual);
        mf.sharedMesh = mesh; mr.sharedMaterials = mats;

        // Mesmo critério dos cabos: base no chão (não flutua) e alvo de mira com altura mínima para cima.
        Bounds b = mesh.bounds;
        var box = root.GetComponent<BoxCollider>();
        float h = Mathf.Max(b.size.y, 0.04f);
        box.size = new Vector3(b.size.x + 0.01f, h, b.size.z + 0.01f);
        box.center = new Vector3(b.center.x, b.min.y + h / 2f, b.center.z);

        var so = new SerializedObject(root.GetComponent<PickupInteractable>());
        so.FindProperty("holdPositionOffset").vector3Value = Vector3.zero;
        so.FindProperty("holdEulerOffset").vector3Value = new Vector3(-60f, 0f, 0f);
        so.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(root, ItemPath);
        PrefabUtility.UnloadPrefabContents(root);
    }

    // ------------------------------------------------------------------ gabinete 3D
    private static void BuildCase(Mesh pendrive, Material[] mats)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(CasePath);
        Transform rt = root.transform;
        Transform net = rt.GetComponentsInChildren<Transform>(true).First(t => t.name == "Porta3D_CaseNetwork");
        Vector3 back = rt.InverseTransformDirection(net.forward);   // para fora da traseira (espaço do root)

        List<Bounds> usb = FindUsbPorts(rt, back);
        Vector3 right = Vector3.Cross(Vector3.up, -back);           // direita de quem olha a traseira
        usb = usb.OrderBy(u => Vector3.Dot(u.center, right)).ToList(); // 0 = esquerda vista de trás (USB_XN[0])

        var anchors = new Transform[ComputerPower.UsbPortCount];
        for (int i = 0; i < anchors.Length; i++)
        {
            string n = "Porta3D_Usb" + i;
            Transform a = rt.Find(n) ?? new GameObject(n).transform;
            a.SetParent(rt, false);
            Vector3 face = usb[i].center + back * Vector3.Dot(usb[i].extents, Abs(back));
            a.localPosition = face;
            a.localRotation = Quaternion.LookRotation(back, Vector3.up);
            anchors[i] = a;
        }

        // Pendrive encaixado: conector (lado do alumínio) para -Z da âncora, espessura na horizontal (X).
        Transform plugged = rt.Find("Pendrive3D") ?? new GameObject("Pendrive3D").transform;
        plugged.SetParent(rt, false);
        Transform model = plugged.Find("Modelo") ?? new GameObject("Modelo").transform;
        model.SetParent(plugged, false);
        var mf = Get<MeshFilter>(model);
        var mr = Get<MeshRenderer>(model);
        mf.sharedMesh = pendrive; mr.sharedMaterials = mats;

        Vector3 plugDir = PlugDirection(pendrive, mats);
        Quaternion rot = Quaternion.LookRotation(Vector3.back, Vector3.right) * Quaternion.Inverse(Quaternion.LookRotation(plugDir, Vector3.up));
        float tip = pendrive.vertices.Max(v => Vector3.Dot(v, plugDir));
        // ponta do conector no eixo do pendrive (o item deitado tem a base em y = 0: o eixo passa pelo centro)
        Vector3 c = pendrive.bounds.center;
        Vector3 tipPoint = c - plugDir * Vector3.Dot(c, plugDir) + plugDir * tip;
        model.localRotation = rot;
        // ponta do conector PlugDepth para dentro da boca da porta (z = 0 da âncora)
        model.localPosition = Vector3.back * PlugDepth - rot * tipPoint;
        plugged.gameObject.SetActive(false);

        var view = Get<PcPendriveView3D>(root.transform);
        var so = new SerializedObject(view);
        SerializedProperty list = so.FindProperty("usbAnchors");
        list.arraySize = anchors.Length;
        for (int i = 0; i < anchors.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = anchors[i];
        so.FindProperty("plugged").objectReferenceValue = plugged;
        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, CasePath);
        PrefabUtility.UnloadPrefabContents(root);
        Debug.Log("PendriveSetup: USB " + string.Join(" | ", usb.Select(u => u.center.ToString("F4") + " " + u.size.ToString("F4"))));
    }

    private static T Get<T>(Component c) where T : Component => c.TryGetComponent(out T t) ? t : c.gameObject.AddComponent<T>();

    private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

    /// <summary>Caixas pretas do I/O de ~9 x 16 mm (largura x altura) com a face para trás = as 3 USB.</summary>
    private static List<Bounds> FindUsbPorts(Transform root, Vector3 back)
    {
        MeshRenderer r = root.GetComponentsInChildren<MeshRenderer>(true)
            .First(x => x.sharedMaterials.Any(m => m != null && m.name == PortMaterial) && x.transform.name != "Modelo");
        Mesh mesh = r.GetComponent<MeshFilter>().sharedMesh;
        int sub = System.Array.FindIndex(r.sharedMaterials, m => m != null && m.name == PortMaterial);
        Vector3[] v = mesh.vertices;
        int[] tri = mesh.GetTriangles(sub);

        // componentes conexos (cada caixa tem seus vértices)
        var parent = new Dictionary<int, int>();
        int F(int x) { while (parent[x] != x) x = parent[x] = parent[parent[x]]; return x; }
        foreach (int i in tri) if (!parent.ContainsKey(i)) parent[i] = i;
        for (int t = 0; t < tri.Length; t += 3) { parent[F(tri[t])] = F(tri[t + 1]); parent[F(tri[t + 1])] = F(tri[t + 2]); }
        // vértices em posições iguais (bevel) também unem
        var byPos = new Dictionary<Vector3Int, int>();
        foreach (int i in parent.Keys.ToList())
        {
            var k = Vector3Int.RoundToInt(v[i] * 100000f);
            if (byPos.TryGetValue(k, out int j)) parent[F(i)] = F(j); else byPos[k] = i;
        }

        Vector3 side = Vector3.Cross(Vector3.up, back).normalized;
        var found = new List<Bounds>();
        foreach (var g in parent.Keys.ToList().GroupBy(F).ToList())
        {
            var pts = g.Select(i => root.InverseTransformPoint(r.transform.TransformPoint(v[i]))).ToList();
            var b = new Bounds(pts[0], Vector3.zero);
            foreach (var p in pts) b.Encapsulate(p);
            float w = Mathf.Abs(Vector3.Dot(b.size, Abs(side))), h = b.size.y;
            if (Mathf.Abs(w - 0.009f) < 0.003f && Mathf.Abs(h - 0.016f) < 0.004f) found.Add(b);
        }
        if (found.Count != ComputerPower.UsbPortCount)
            throw new System.Exception($"PendriveSetup: esperava {ComputerPower.UsbPortCount} USB na traseira, achei {found.Count}");
        return found;
    }

    /// <summary>Direção (no espaço da malha) em que o conector metálico aponta: do centro do corpo para o alumínio.</summary>
    private static Vector3 PlugDirection(Mesh mesh, Material[] mats)
    {
        int sub = System.Array.FindIndex(mats, m => m != null && m.name == "M_Comp_Aluminio");
        Vector3[] v = mesh.vertices;
        var idx = mesh.GetTriangles(sub).Distinct().ToList();
        Vector3 c = Vector3.zero;
        foreach (int i in idx) c += v[i];
        c /= idx.Count;
        Vector3 d = c - mesh.bounds.center;
        // eixo longo do item deitado (X ou Z)
        return Mathf.Abs(d.x) > Mathf.Abs(d.z) ? new Vector3(Mathf.Sign(d.x), 0, 0) : new Vector3(0, 0, Mathf.Sign(d.z));
    }

    // ------------------------------------------------------------------ tela 2D (Traseira)
    private static void BuildScreen()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(ScreenPath);
        Transform template = root.GetComponentsInChildren<Transform>(true).First(t => t.name == "Porta_RedeGabinete");
        Transform parent = template.parent;
        var views = new List<UsbPortView>();
        for (int i = 0; i < ComputerPower.UsbPortCount; i++)
        {
            string n = "Porta_USB_" + i;
            Transform t = parent.Find(n);
            if (t == null)
            {
                t = Object.Instantiate(template.gameObject, parent).transform;
                t.name = n;
            }
            foreach (var old in t.GetComponents<ComputerPortView>()) Object.DestroyImmediate(old);
            var view = Get<UsbPortView>(t);

            // área clicável em volta da porta; o pendrive (Plugue) no tamanho real do corpo visto de trás
            float cx = UsbXn[i], cy = 1f - UsbYn, aw = 0.05f, ah = 0.05f;
            var rt = (RectTransform)t;
            rt.anchorMin = new Vector2(cx - aw / 2, cy - ah / 2); rt.anchorMax = new Vector2(cx + aw / 2, cy + ah / 2);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var plug = (RectTransform)t.Find("Plugue");
            float pw = BodyW / CaseW / aw, ph = BodyH / CaseH / ah;
            plug.anchorMin = new Vector2(0.5f - pw / 2, 0.5f - ph / 2); plug.anchorMax = new Vector2(0.5f + pw / 2, 0.5f + ph / 2);
            plug.offsetMin = plug.offsetMax = Vector2.zero;
            var img = plug.GetComponent<Image>();
            img.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Sprite);
            img.preserveAspect = true;
            plug.gameObject.SetActive(false);

            var so = new SerializedObject(view);
            so.FindProperty("index").intValue = i;
            so.FindProperty("button").objectReferenceValue = t.GetComponent<Button>();
            so.FindProperty("pluggedIndicator").objectReferenceValue = img;
            so.ApplyModifiedPropertiesWithoutUndo();
            views.Add(view);
        }
        var cso = new SerializedObject(root.GetComponent<ComputerScreenController>());
        SerializedProperty list = cso.FindProperty("usbPorts");
        list.arraySize = views.Count;
        for (int i = 0; i < views.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = views[i];
        cso.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(root, ScreenPath);
        PrefabUtility.UnloadPrefabContents(root);
    }

    // ------------------------------------------------------------------ gaveta de ferramentas
    private static void AddToDrawer()
    {
        const string path = Gameplay + "Gaveteiro_Hardware.prefab";
        var item = AssetDatabase.LoadAssetAtPath<GameObject>(ItemPath).GetComponent<PickupInteractable>();
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        foreach (DrawerContents dc in root.GetComponentsInChildren<DrawerContents>(true))
        {
            var so = new SerializedObject(dc);
            SerializedProperty entries = so.FindProperty("entries");
            bool tools = false, has = false;
            for (int i = 0; i < entries.arraySize; i++)
            {
                Object p = entries.GetArrayElementAtIndex(i).FindPropertyRelative("prefab").objectReferenceValue;
                if (p != null && p.name == "Pickup_Ferramenta") tools = true;
                if (p == item) has = true;
            }
            if (!tools || has) continue;
            SerializedProperty first = entries.GetArrayElementAtIndex(0);
            Vector3 pos = first.FindPropertyRelative("localPosition").vector3Value;
            entries.arraySize++;
            SerializedProperty e = entries.GetArrayElementAtIndex(entries.arraySize - 1);
            e.FindPropertyRelative("prefab").objectReferenceValue = item;
            e.FindPropertyRelative("quantity").intValue = 1;
            e.FindPropertyRelative("delivery").intValue = (int)DrawerContents.Delivery.PhysicalInDrawer;
            e.FindPropertyRelative("localPosition").vector3Value = new Vector3(0f, pos.y, pos.z);  // entre as 2 chaves de fenda
            e.FindPropertyRelative("localEuler").vector3Value = Vector3.zero;
            e.FindPropertyRelative("step").vector3Value = Vector3.zero;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
    }
}
