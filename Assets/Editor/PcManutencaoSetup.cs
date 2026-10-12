using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Configura a manutenção do PC (fonte, cooler, HDMI, energia do monitor, rede) nos prefabs e na cena.
/// Idempotente: pode rodar de novo sem duplicar nada. Menu: Tools > E-stagiar.io > Configurar manutenção do PC.
/// Batch: Unity -batchmode -quit -projectPath . -executeMethod PcManutencaoSetup.Run
/// </summary>
public static class PcManutencaoSetup
{
    private const string Gameplay = "Assets/Prefabs/Gameplay/";
    private const string ScreenPath = "Assets/Prefabs/UI/UI_Screen_Gabinete.prefab";
    private const string ScenePath = "Assets/Scenes/Office_Placeholder.unity";
    private const string Sprites = "Assets/Art/Official/Sprites/";
    private const string Mats = "Assets/Art/Placeholders/Materials/";

    [MenuItem("Tools/E-stagiar.io/Configurar manutenção do PC")]
    public static void Run()
    {
        AssetDatabase.Refresh();
        GameObject psu = PartPrefab("Pickup_Fonte", PcComponent.Psu, "fonte de alimentação", "ui_fonte", 0.15f, 0.0857f);
        GameObject cooler = PartPrefab("Pickup_Cooler", PcComponent.CpuCooler, "cooler do processador", "ui_cooler", 0.09f, 0.09f);
        GameObject hdmi = HdmiCablePrefab();
        ConfigureCase(psu, cooler);
        ConfigureScreen();
        ConfigureScene(psu, cooler, hdmi);
        AssetDatabase.SaveAssets();
        Debug.Log("PcManutencaoSetup: concluído.");
    }

    // ---------------------------------------------------------------- prefabs de itens
    private static GameObject PartPrefab(string name, PcComponent component, string display, string sprite, float sx, float sy)
    {
        string path = Gameplay + name + ".prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            AssetDatabase.CopyAsset(Gameplay + "Pickup_HD.prefab", path);

        string matPath = Mats + "M_Item2D_" + name.Substring("Pickup_".Length) + ".mat";
        if (AssetDatabase.LoadAssetAtPath<Material>(matPath) == null)
            AssetDatabase.CopyAsset(Mats + "M_Item2D_HD.mat", matPath);
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Sprites + sprite + ".png");
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
        EditorUtility.SetDirty(mat);

        GameObject root = PrefabUtility.LoadPrefabContents(path);
        var part = root.GetComponent<ComputerPartItem>();
        var so = new SerializedObject(part);
        so.FindProperty("component").intValue = (int)component;
        so.FindProperty("displayName").stringValue = display;
        so.ApplyModifiedPropertiesWithoutUndo();
        foreach (string face in new[] { "Frente", "Verso" })
        {
            Transform t = root.GetComponentsInChildren<Transform>(true).First(x => x.name == face);
            t.localScale = new Vector3(sx, sy, 1f);
            t.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }
        var box = root.GetComponent<BoxCollider>();
        if (box != null) box.size = new Vector3(sx, box.size.y, sy);
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    private static GameObject HdmiCablePrefab()
    {
        string path = Gameplay + "Pickup_CaboHDMI.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            AssetDatabase.CopyAsset(Gameplay + "Pickup_CaboVideo.prefab", path);
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        var so = new SerializedObject(root.GetComponent<CableItem>());
        so.FindProperty("cableType").intValue = (int)CableType.Hdmi;
        so.FindProperty("displayName").stringValue = "cabo HDMI";
        so.ApplyModifiedPropertiesWithoutUndo();

        // Cor própria (preto com ponta prateada não aparece de longe: cinza-escuro azulado), para não confundir com o VGA.
        const string matPath = "Assets/Art/Placeholders/Materials/M_CaboHDMI.mat";
        Renderer r = root.GetComponentInChildren<Renderer>(true);
        if (r != null && r.sharedMaterial != null)
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(matPath) == null)
                AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(r.sharedMaterial), matPath);
            var m = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (m != null)
            {
                Color c = new Color(0.16f, 0.16f, 0.2f);
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
                if (m.HasProperty("_Color")) m.SetColor("_Color", c);
                EditorUtility.SetDirty(m);
                foreach (Renderer rr in root.GetComponentsInChildren<Renderer>(true)) rr.sharedMaterial = m;
            }
        }
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    private static void ConfigureCase(GameObject psu, GameObject cooler)
    {
        string path = Gameplay + "Gabinete.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        var so = new SerializedObject(root.GetComponent<ComputerCase>());
        so.FindProperty("psuItemPrefab").objectReferenceValue = psu;
        so.FindProperty("coolerItemPrefab").objectReferenceValue = cooler;
        so.FindProperty("fixedComponents").intValue = (int)ComputerCase.AllComponents;
        so.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
    }

    // ---------------------------------------------------------------- tela de manutenção
    private static Sprite S(string name) =>
        AssetDatabase.LoadAssetAtPath<Sprite>(Sprites + name + ".png") ??
        AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Placeholders/Sprites/" + name + ".png");

    private static RectTransform Find(Transform root, string path) => (RectTransform)root.Find(path);

    private static void Anchor(RectTransform rt, float x0, float y0, float x1, float y1)
    {
        rt.anchorMin = new Vector2(x0, y0);
        rt.anchorMax = new Vector2(x1, y1);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    /// <summary>Filho com esse nome (reaproveita) ou cópia de <paramref name="template"/>.</summary>
    private static RectTransform Child(RectTransform template, string name)
    {
        Transform existing = template.parent.Find(name);
        if (existing != null) return (RectTransform)existing;
        var copy = (RectTransform)Object.Instantiate(template.gameObject, template.parent).transform;
        copy.name = name;
        return copy;
    }

    private static void SetImage(Transform t, string sprite, bool preserve = true)
    {
        var img = t.GetComponent<Image>();
        img.sprite = S(sprite);
        img.preserveAspect = preserve;
        img.color = Color.white;
    }

    private static void SetPort(RectTransform view, PcPort port, string plugSprite)
    {
        var so = new SerializedObject(view.GetComponent<ComputerPortView>());
        so.FindProperty("port").intValue = (int)port;
        so.ApplyModifiedPropertiesWithoutUndo();
        Transform plug = view.Find("Plugue");
        if (plug != null) SetImage(plug, plugSprite);
    }

    private static void FitAspect(Transform imageObject)
    {
        var img = imageObject.GetComponent<Image>();
        if (img == null || img.sprite == null) return;
        float a = img.sprite.rect.width / img.sprite.rect.height;
        foreach (AspectRatioFitter f in imageObject.GetComponentsInChildren<AspectRatioFitter>(true)) f.aspectRatio = a;
        foreach (AspectRatioFitter f in imageObject.GetComponents<AspectRatioFitter>()) f.aspectRatio = a;
    }

    private static void ConfigureScreen()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(ScreenPath);
        Transform r = root.transform;
        var ctrl = root.GetComponent<ComputerScreenController>();
        var cso = new SerializedObject(ctrl);

        // ---------- interior ----------
        const string inside = "Interior/ContainerInterior_Area/ContainerInterior/";
        RectTransform comps = Find(r, inside + "Componentes"), slots = Find(r, inside + "Encaixes");
        RectTransform mb = Find(comps, "PlacaMae"), ram = Find(comps, "MemoriaRAM"), hd = Find(comps, "HD");
        RectTransform fonte = Child(hd, "Fonte"), cooler = Child(hd, "Cooler");
        RectTransform sMb = Find(slots, "Encaixe_PlacaMae"), sRam = Find(slots, "Encaixe_MemoriaRAM"), sHd = Find(slots, "Encaixe_HD");
        RectTransform sFonte = Child(sHd, "Encaixe_Fonte"), sCooler = Child(sHd, "Encaixe_Cooler");

        var layout = new (RectTransform img, RectTransform slot, float x0, float y0, float x1, float y1)[]
        {
            (fonte, sFonte, 0.03f, 0.82f, 0.27f, 0.985f),
            (mb, sMb, 0.08f, 0.25f, 0.54f, 0.80f),
            (hd, sHd, 0.64f, 0.05f, 0.92f, 0.27f),
            (ram, sRam, 0.328f, 0.545f, 0.378f, 0.75f),     // slot vertical da placa-mãe (à direita do processador)
            (cooler, sCooler, 0.13f, 0.54f, 0.296f, 0.76f), // sobre o soquete do processador
        };
        int i = 0;
        foreach (var l in layout)
        {
            Anchor(l.img, l.x0, l.y0, l.x1, l.y1);
            Anchor(l.slot, l.x0, l.y0, l.x1, l.y1);
            l.img.SetSiblingIndex(i);   // RAM e cooler depois (por cima) da placa-mãe
            l.slot.SetSiblingIndex(i);
            i++;
        }
        SetImage(fonte, "ui_fonte"); SetImage(cooler, "ui_cooler");
        SetImage(hd, "ui_hd_deitado"); SetImage(ram, "ui_memoria_ram_vertical");
        foreach (var (slot, comp) in new[] { (sFonte, PcComponent.Psu), (sCooler, PcComponent.CpuCooler) })
        {
            var so = new SerializedObject(slot.GetComponent<ComputerSlotView>());
            so.FindProperty("slot").intValue = (int)comp;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        cso.FindProperty("psuView").objectReferenceValue = fonte.gameObject;
        cso.FindProperty("coolerView").objectReferenceValue = cooler.gameObject;
        SetList(cso.FindProperty("slots"), slots.GetComponentsInChildren<ComputerSlotView>(true).Cast<Object>());

        // ---------- traseira: portas do gabinete ----------
        const string back = "Traseira/ContainerTraseira_Area/ContainerTraseira/Portas/";
        RectTransform pVga = Find(r, back + "Porta_VGAGabinete"), pPow = Find(r, back + "Porta_EnergiaGabinete");
        RectTransform pHdmi = Child(pVga, "Porta_HDMIGabinete"), pNet = Child(pVga, "Porta_RedeGabinete");
        Anchor(pHdmi, 0.49f, 0.205f, 0.61f, 0.242f);
        Anchor(pNet, 0.70f, 0.150f, 0.81f, 0.188f);
        SetPort(pPow, PcPort.CasePower, "ui_plugue_energia");
        SetPort(pVga, PcPort.CaseVideo, "ui_plugue_vga");
        SetPort(pHdmi, PcPort.CaseHdmi, "ui_plugue_hdmi");
        SetPort(pNet, PcPort.CaseNetwork, "ui_plugue_rj45");

        // ---------- traseira: monitor ----------
        RectTransform mVga = Find(r, "Traseira/Monitor/Area/Porta_VGAMonitor");
        RectTransform mHdmi = Child(mVga, "Porta_HDMIMonitor"), mPow = Child(mVga, "Porta_EnergiaMonitor");
        Anchor(mVga, 0.445f, 0.405f, 0.555f, 0.495f);
        Anchor(mHdmi, 0.585f, 0.425f, 0.65f, 0.475f);
        Anchor(mPow, 0.32f, 0.415f, 0.395f, 0.485f);
        SetPort(mVga, PcPort.MonitorVideo, "ui_plugue_vga");
        SetPort(mHdmi, PcPort.MonitorHdmi, "ui_plugue_hdmi");
        SetPort(mPow, PcPort.MonitorPower, "ui_plugue_energia");

        // ---------- traseira: tomadas e ponto de rede (coluna da direita) ----------
        RectTransform tPc = Find(r, "Traseira/Tomada");
        RectTransform tMon = Child(tPc, "Tomada_Monitor"), rede = Child(tPc, "PontoRede");
        Anchor(tPc, 0.85f, 0.65f, 0.97f, 0.88f);   // acima dos botões de navegação (canto inferior direito)
        Anchor(tMon, 0.85f, 0.405f, 0.97f, 0.635f);
        Anchor(rede, 0.85f, 0.16f, 0.97f, 0.39f);
        SetImage(tPc, "ui_tomada_energia"); SetImage(tMon, "ui_tomada_energia"); SetImage(rede, "ui_ponto_rede");
        foreach (RectTransform t in new[] { tPc, tMon, rede }) FitAspect(t);
        RectTransform portPc = Find(tPc, "Area/Porta_Tomada");
        Anchor(portPc, 0.13f, 0.39f, 0.79f, 0.71f);
        SetPort(portPc, PcPort.Outlet, "ui_plugue_tomada");
        RectTransform portMon = (RectTransform)(tMon.Find("Area/Porta_Tomada") ?? tMon.Find("Area/Porta_TomadaMonitor"));
        portMon.name = "Porta_TomadaMonitor";
        Anchor(portMon, 0.13f, 0.39f, 0.79f, 0.71f);
        SetPort(portMon, PcPort.MonitorOutlet, "ui_plugue_tomada");
        RectTransform portNet = (RectTransform)(rede.Find("Area/Porta_Tomada") ?? rede.Find("Area/Porta_Rede"));
        portNet.name = "Porta_Rede";
        Anchor(portNet, 0.28f, 0.34f, 0.66f, 0.63f);
        SetPort(portNet, PcPort.NetworkJack, "ui_plugue_rj45");
        SetList(cso.FindProperty("ports"), r.GetComponentsInChildren<ComputerPortView>(true).Cast<Object>());

        // ---------- cabos (faixas) ----------
        RectTransform cPow = Find(r, "Traseira/Cabos/CaboEnergia"), cVid = Find(r, "Traseira/Cabos/CaboVideo");
        var cables = new (RectTransform line, PcLink link, RectTransform a, RectTransform b, Color color)[]
        {
            (cPow, PcLink.Power, portPc, pPow, new Color(0.1f, 0.1f, 0.12f, 0.95f)),
            (Child(cPow, "CaboEnergiaMonitor"), PcLink.MonitorPower, portMon, mPow, new Color(0.1f, 0.1f, 0.12f, 0.95f)),
            (cVid, PcLink.Vga, pVga, mVga, new Color(0.15f, 0.35f, 0.9f, 0.95f)),
            (Child(cVid, "CaboHDMI"), PcLink.Hdmi, pHdmi, mHdmi, new Color(0.3f, 0.3f, 0.36f, 0.95f)),
            (Child(cVid, "CaboRede"), PcLink.Network, portNet, pNet, new Color(0.25f, 0.6f, 0.95f, 0.95f)),
        };
        foreach (var c in cables)
        {
            var so = new SerializedObject(c.line.GetComponent<CableLineView>());
            so.FindProperty("link").intValue = (int)c.link;
            so.FindProperty("from").objectReferenceValue = c.a;
            so.FindProperty("to").objectReferenceValue = c.b;
            so.ApplyModifiedPropertiesWithoutUndo();
            c.line.GetComponent<Image>().color = c.color;
        }
        SetList(cso.FindProperty("cableLines"), cables.Select(c => (Object)c.line.GetComponent<CableLineView>()));

        cso.FindProperty("onlineScreen").objectReferenceValue = S("ui_tela_sistema_rede");
        cso.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(root, ScreenPath);
        PrefabUtility.UnloadPrefabContents(root);
    }

    private static void SetList(SerializedProperty list, IEnumerable<Object> items)
    {
        Object[] arr = items.ToArray();
        list.arraySize = arr.Length;
        for (int i = 0; i < arr.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = arr[i];
    }

    // ---------------------------------------------------------------- cena
    private static void ConfigureScene(GameObject psu, GameObject cooler, GameObject hdmi)
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject hdInScene = FindInstance(Gameplay + "Pickup_HD.prefab");
        Vector3 basePos = hdInScene != null ? hdInScene.transform.position : Vector3.zero;
        Quaternion baseRot = hdInScene != null ? hdInScene.transform.rotation : Quaternion.identity;
        Transform parent = hdInScene != null ? hdInScene.transform.parent : null;

        Place(psu, "Pickup_Fonte", basePos + new Vector3(0.25f, 0f, 0f), baseRot, parent);
        Place(cooler, "Pickup_Cooler", basePos + new Vector3(0.45f, 0f, 0f), baseRot, parent);
        Place(hdmi, "Pickup_CaboHDMI", basePos + new Vector3(0f, 0f, 0.3f), baseRot, parent);
        var powerCable = AssetDatabase.LoadAssetAtPath<GameObject>(Gameplay + "Pickup_CaboEnergia.prefab");
        Place(powerCable, "Pickup_CaboEnergia_Monitor", basePos + new Vector3(0.25f, 0f, 0.3f), baseRot, parent);

        foreach (MonitorDisplay d in Object.FindObjectsByType<MonitorDisplay>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var so = new SerializedObject(d);
            so.FindProperty("onlineTexture").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Placeholders/Sprites/ui_tela_sistema_rede.png");
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static GameObject FindInstance(string prefabPath)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        return Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Select(t => t.gameObject)
            .FirstOrDefault(g => PrefabUtility.IsAnyPrefabInstanceRoot(g) &&
                                 PrefabUtility.GetCorrespondingObjectFromSource(g) == prefab);
    }

    private static void Place(GameObject prefab, string name, Vector3 pos, Quaternion rot, Transform parent)
    {
        if (prefab == null) return;
        GameObject existing = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Select(t => t.gameObject).FirstOrDefault(g => g.name == name);
        if (existing != null) return;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.name = name;
        go.transform.SetPositionAndRotation(pos, rot);
    }
}
