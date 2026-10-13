using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Ajustes do minigame de manutenção (idempotente; Tools > E-stagiar.io > Ajustes da manutenção):
///  - telas do monitor: BIOS sem HD (PH_ui_tela_bios), HD sem sistema (PH_ui_tela_bios_sem_sistema) e instalação em
///    etapas (PH_ui_tela_instalando_0..4), geradas por Tools/screens/gerar_telas_monitor.py, na tela 2D e no monitor 3D;
///  - cursores: mãozinha padrão em todas as telas (UIScreenManager) e chave de fenda 3D (ui_cursor_chave_de_fenda,
///    render de Tools/blender/componentes_build.py) na Traseira, proporcional aos parafusos;
///  - Tomada_Monitor desenhada antes dos cabos (o cabo fica na frente do plugue, como na primeira tomada);
///  - Pickup_Ferramenta com o modelo 3D Item_ChaveDeFenda (componentes.fbx);
///  - gavetas dos gaveteiros com a área de guardar (GaveteirosSetup.StorageArea).
/// </summary>
public static class ManutencaoAjustesSetup
{
    private const string UI = "Assets/Art/UI/";
    private const string ScreenPath = "Assets/Prefabs/UI/UI_Screen_Gabinete.prefab";
    private const string ScenePath = "Assets/Scenes/Office_Placeholder.unity";
    private const string Gameplay = "Assets/Prefabs/Gameplay/";
    private const string Fbx = "Assets/Art/Models/componentes.fbx";
    private const string Cursor = UI + "ui_cursor_chave_de_fenda.png";
    private static readonly Vector2 CursorTip = new Vector2(10, 246);   // HOTSPOT impresso pelo componentes_build.py
    private static readonly Vector2 HandTip = new Vector2(280, 0);      // ponta do dedo em PH_ui_mao (530 x 760)
    private const int InstallStages = 5;

    [MenuItem("Tools/E-stagiar.io/Ajustes da manutenção")]
    public static void Run()
    {
        CopySources();
        ConfigureScreen();
        ConfigureScene();
        ConfigureScrewdriverItem();
        foreach (string cabinet in new[] { "Gaveteiro_Hardware", "Gaveteiro_Cabos" }) ConfigureDrawers(Gameplay + cabinet + ".prefab");
        AssetDatabase.SaveAssets();
        Debug.Log("ManutencaoAjustesSetup: pronto");
    }

    // ------------------------------------------------------------------ arquivos gerados fora do Unity
    private static void CopySources()
    {
        foreach (var (src, dst) in new[] { ("Art/Source/Blender/export/componentes.fbx", Fbx),
                                           ("Art/Source/Blender/canvas/ui_cursor_chave_de_fenda.png", Cursor) })
            if (File.Exists(src)) File.Copy(src, dst, true);
        AssetDatabase.Refresh();

        // telas novas com a mesma importação das telas existentes (pixel art, sem filtro)
        var template = (TextureImporter)AssetImporter.GetAtPath(UI + "PH_ui_tela_bios.png");
        var settings = new TextureImporterSettings();
        template.ReadTextureSettings(settings);
        foreach (string n in new[] { "PH_ui_tela_bios_sem_sistema" }.Concat(Enumerable.Range(0, InstallStages).Select(i => "PH_ui_tela_instalando_" + i)))
        {
            var imp = (TextureImporter)AssetImporter.GetAtPath(UI + n + ".png");
            imp.SetTextureSettings(settings);
            imp.textureCompression = template.textureCompression;
            imp.SaveAndReimport();
        }
        var cur = (TextureImporter)AssetImporter.GetAtPath(Cursor);
        cur.textureType = TextureImporterType.Default;
        cur.alphaIsTransparency = true;
        cur.mipmapEnabled = false;
        cur.textureCompression = TextureImporterCompression.Uncompressed;
        cur.SaveAndReimport();
    }

    private static Sprite Sp(string n) => AssetDatabase.LoadAssetAtPath<Sprite>(UI + n + ".png");
    private static Texture2D Tex(string path) => AssetDatabase.LoadAssetAtPath<Texture2D>(path);

    // ------------------------------------------------------------------ tela 2D do gabinete
    private static void ConfigureScreen()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(ScreenPath);
        var so = new SerializedObject(root.GetComponent<ComputerScreenController>());
        so.FindProperty("noBootableScreen").objectReferenceValue = Sp("PH_ui_tela_bios_sem_sistema");
        SerializedProperty install = so.FindProperty("installScreens");
        install.arraySize = InstallStages;
        for (int i = 0; i < InstallStages; i++) install.GetArrayElementAtIndex(i).objectReferenceValue = Sp("PH_ui_tela_instalando_" + i);
        so.FindProperty("rearCursor").objectReferenceValue = Tex(Cursor);
        so.FindProperty("rearCursorHotspot").vector2Value = CursorTip;
        so.FindProperty("handCursorHotspot").vector2Value = HandTip;
        so.ApplyModifiedPropertiesWithoutUndo();

        // a tomada do monitor vinha DEPOIS do contêiner dos cabos (desenhada por cima do cabo): passa para antes dele
        Transform rear = root.transform.Find("Traseira");
        Transform cables = rear.Find("Cabos"), outlet = rear.Find("Tomada_Monitor");
        if (outlet.GetSiblingIndex() > cables.GetSiblingIndex()) outlet.SetSiblingIndex(cables.GetSiblingIndex());

        PrefabUtility.SaveAsPrefabAsset(root, ScreenPath);
        PrefabUtility.UnloadPrefabContents(root);
    }

    // ------------------------------------------------------------------ cena: monitor 3D e cursor padrão
    private static void ConfigureScene()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        foreach (MonitorDisplay m in Object.FindObjectsByType<MonitorDisplay>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var so = new SerializedObject(m);
            so.FindProperty("noBootableTexture").objectReferenceValue = Tex(UI + "PH_ui_tela_bios_sem_sistema.png");
            SerializedProperty install = so.FindProperty("installTextures");
            install.arraySize = InstallStages;
            for (int i = 0; i < InstallStages; i++) install.GetArrayElementAtIndex(i).objectReferenceValue = Tex(UI + "PH_ui_tela_instalando_" + i + ".png");
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        foreach (UIScreenManager mgr in Object.FindObjectsByType<UIScreenManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var so = new SerializedObject(mgr);
            so.FindProperty("defaultCursor").objectReferenceValue = Tex(UI + "PH_ui_mao.png");
            so.FindProperty("defaultCursorHotspot").vector2Value = HandTip;
            so.FindProperty("defaultCursorHeight").intValue = 48;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        EditorSceneManager.SaveScene(scene);
    }

    // ------------------------------------------------------------------ chave de fenda 3D
    private static void ConfigureScrewdriverItem()
    {
        string path = Gameplay + "Pickup_Ferramenta.prefab";
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
        MeshFilter src = model.GetComponentsInChildren<MeshFilter>(true).First(f => f.sharedMesh.name == "Item_ChaveDeFenda");
        Mesh mesh = src.sharedMesh;

        GameObject root = PrefabUtility.LoadPrefabContents(path);
        Transform visual = root.transform.Find("Visual");
        for (int i = visual.childCount - 1; i >= 0; i--) Object.DestroyImmediate(visual.GetChild(i).gameObject);
        visual.localPosition = Vector3.zero; visual.localRotation = Quaternion.identity; visual.localScale = Vector3.one;
        MeshFilter mf = visual.GetComponent<MeshFilter>();
        if (mf == null) mf = visual.gameObject.AddComponent<MeshFilter>();
        MeshRenderer mr = visual.GetComponent<MeshRenderer>();
        if (mr == null) mr = visual.gameObject.AddComponent<MeshRenderer>();
        mf.sharedMesh = mesh;
        mr.sharedMaterials = src.GetComponent<MeshRenderer>().sharedMaterials;

        // base no chão (não flutua) e alvo de mira com altura mínima para cima
        Bounds b = mesh.bounds;
        var box = root.GetComponent<BoxCollider>();
        float h = Mathf.Max(b.size.y, 0.04f);
        box.size = new Vector3(b.size.x + 0.01f, h, b.size.z + 0.01f);
        box.center = new Vector3(b.center.x, b.min.y + h / 2f, b.center.z);
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
    }

    // ------------------------------------------------------------------ gavetas: área de guardar
    private static void ConfigureDrawers(string path)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        foreach (DrawerContents dc in root.GetComponentsInChildren<DrawerContents>(true))
        {
            var so = new SerializedObject(dc);
            so.FindProperty("storageArea").objectReferenceValue = GaveteirosSetup.StorageArea(dc.transform);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
    }
}
