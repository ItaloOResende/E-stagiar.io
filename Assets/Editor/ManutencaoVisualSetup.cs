using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Etapa 3 (visual): gabinete aberto em 3D com camadas das peças, componentes 3D nos itens do mundo, LED do Power
/// (Canvas e 3D) que só acende com o PC ligado, fundo #DDE5E8 com sombras suaves e proporções naturais
/// (mesma escala px/m) para gabinete e monitor nas vistas Frente e Traseira.
/// Idempotente. Menu: Tools > E-stagiar.io > Ajustar visual da manutenção. Batch: -executeMethod ManutencaoVisualSetup.Run
/// Reverter: Art/_Backup/2026-10-12_etapa3/RESTAURAR.md.
/// </summary>
public static class ManutencaoVisualSetup
{
    private const string Sprites = "Assets/Art/UI/";
    private const string Fbx = "Assets/Art/Models/componentes.fbx";
    private const string Mats = "Assets/Art/Materials/";
    private const string Gameplay = "Assets/Prefabs/Gameplay/";
    private const string ScreenPath = "Assets/Prefabs/UI/UI_Screen_Gabinete.prefab";

    // Áreas visíveis das peças no gabinete aberto (pc_interior_camadas.py: AREA ...), em fração do quadro.
    private static readonly Dictionary<string, Rect> Areas = new Dictionary<string, Rect>
    {
        { "PlacaMae", R(0.2148f, 0.3712f, 0.54f, 0.7775f) },
        { "MemoriaRAM", R(0.3854f, 0.5463f, 0.4047f, 0.7606f) },
        { "Cooler", R(0.2258f, 0.5825f, 0.3749f, 0.7412f) },
        { "Fonte", R(0.1149f, 0.7656f, 0.3827f, 0.8781f) },
        { "HD", R(0.6322f, 0.1294f, 0.7824f, 0.2469f) },
    };
    private static readonly Dictionary<string, string> Layer = new Dictionary<string, string>
    {
        { "PlacaMae", "ui_render_peca_placa" }, { "MemoriaRAM", "ui_render_peca_ram" }, { "Cooler", "ui_render_peca_cooler" },
        { "Fonte", "ui_render_peca_fonte" }, { "HD", "ui_render_peca_hd" },
    };
    private const float InteriorAspect = 1811f / 1600f;
    private static readonly Rect PowerLed = R(0.427f, 0.0881f, 0.573f, 0.1719f);

    private static Rect R(float x0, float y0, float x1, float y1) => Rect.MinMaxRect(x0, y0, x1, y1);

    [MenuItem("Tools/E-stagiar.io/Ajustar visual da manutenção")]
    public static void Run()
    {
        CopySprites();
        ImportComponents();
        foreach (string p in new[] { "PlacaMae", "MemoriaRAM", "HD", "Fonte", "Cooler" }) PartItemVisual(p);
        ConfigureCaseLed();
        ConfigureScreen();
        AssetDatabase.SaveAssets();
        Debug.Log("ManutencaoVisualSetup: concluído.");
    }

    // ------------------------------------------------------------------ arquivos
    private static void CopySprites()
    {
        var files = new List<(string src, string dst)>
        {
            ("Art/Source/Blender/canvas/ui_pc_frente.png", "ui_render_pc_frente.png"),
            ("Art/Source/Blender/canvas/ui_pc_traseira.png", "ui_render_pc_traseira.png"),
            ("Art/Source/Blender/canvas/ui_render_pc_frente_led.png", "ui_render_pc_frente_led.png"),
        };
        foreach (string f in Directory.GetFiles("Art/Source/Blender/interior/camadas", "*.png"))
            files.Add((f, Path.GetFileName(f)));
        string tmp = System.Environment.GetEnvironmentVariable("SPRITES_DIR");
        if (!string.IsNullOrEmpty(tmp)) files.Add((Path.Combine(tmp, "ui_hover_anel.png"), "ui_hover_anel.png"));
        string template = File.ReadAllText("Assets/Art/UI/PH_pc_back.png.meta");
        foreach (var (src, dst) in files)
        {
            string path = Sprites + dst;
            File.Copy(src, path, true);
            if (File.Exists(path + ".meta")) continue;
            string meta = System.Text.RegularExpressions.Regex.Replace(template, @"guid: \w+", "guid: " + System.Guid.NewGuid().ToString("N"));
            meta = System.Text.RegularExpressions.Regex.Replace(meta, @"spriteID: \w+", "spriteID: " + System.Guid.NewGuid().ToString("N"));
            File.WriteAllText(path + ".meta", meta);
        }
        File.Copy("Art/Source/Blender/export/componentes.fbx", Fbx, true);
        AssetDatabase.Refresh();
    }

    private static Material Mat(string folder, string name, Color c, float smooth = 0.3f, float metal = 0f)
    {
        Directory.CreateDirectory(folder);
        string path = folder + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", metal);
        EditorUtility.SetDirty(m);
        return m;
    }

    private static void ImportComponents()
    {
        string f = Mats + "Componentes/";
        var remap = new Dictionary<string, Material>
        {
            { "MAT_Comp_PCB", Mat(f, "M_Comp_PCB", new Color(0.10f, 0.42f, 0.20f)) },
            { "MAT_Comp_PCB_RAM", Mat(f, "M_Comp_PCB_RAM", new Color(0.08f, 0.36f, 0.17f)) },
            { "MAT_Comp_Chip", Mat(f, "M_Comp_Chip", new Color(0.09f, 0.09f, 0.10f), 0.4f) },
            { "MAT_Comp_Slot", Mat(f, "M_Comp_Slot", new Color(0.14f, 0.14f, 0.16f), 0.4f) },
            { "MAT_Comp_Branco", Mat(f, "M_Comp_Branco", new Color(0.90f, 0.90f, 0.86f)) },
            { "MAT_Comp_Dourado", Mat(f, "M_Comp_Dourado", new Color(0.95f, 0.75f, 0.25f), 0.6f, 0.6f) },
            { "MAT_Comp_Aluminio", Mat(f, "M_Comp_Aluminio", new Color(0.76f, 0.78f, 0.82f), 0.6f, 0.5f) },
            { "MAT_Comp_Etiqueta", Mat(f, "M_Comp_Etiqueta", new Color(0.95f, 0.93f, 0.80f)) },
            { "MAT_Comp_Ventoinha", Mat(f, "M_Comp_Ventoinha", new Color(0.30f, 0.58f, 0.86f), 0.45f) },
            { "MAT_Comp_Vermelho", Mat(f, "M_Comp_Vermelho", new Color(0.84f, 0.27f, 0.21f)) },
            { "MAT_Gabinete_Corpo", AssetDatabase.LoadAssetAtPath<Material>(Mats + "M_Escritorio_Escuro.mat") },
        };
        foreach (string n in new[] { "Painel", "Chapa", "Parafuso", "Porta", "VGA", "Pino", "LED", "HDMI" })
            remap["MAT_Gabinete_" + n] = AssetDatabase.LoadAssetAtPath<Material>(Mats + "M_PC_" + n + ".mat");
        var imp = (ModelImporter)AssetImporter.GetAtPath(Fbx);
        imp.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        foreach (var kv in remap)
            if (kv.Value != null) imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), kv.Key), kv.Value);
        imp.SaveAndReimport();
    }

    // ------------------------------------------------------------------ itens do mundo (placa, RAM, HD, fonte, cooler)
    /// <summary>Troca o Visual 2D (dois quads com o sprite) pelo componente 3D; o collider acompanha a malha.</summary>
    private static void PartItemVisual(string name)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
        MeshFilter mf = model.GetComponentsInChildren<MeshFilter>(true).First(m => m.sharedMesh.name == "Item_" + name);
        string path = Gameplay + "Pickup_" + name + ".prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        Transform old = root.transform.Find("Visual");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var vis = new GameObject("Visual");
        vis.transform.SetParent(root.transform, false);
        vis.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
        vis.AddComponent<MeshRenderer>().sharedMaterials = mf.GetComponent<MeshRenderer>().sharedMaterials;
        var box = root.GetComponent<BoxCollider>();
        box.size = mf.sharedMesh.bounds.size; box.center = mf.sharedMesh.bounds.center;
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
    }

    private static void ConfigureCaseLed()
    {
        Material on = AssetDatabase.LoadAssetAtPath<Material>(Mats + "M_PC_LED.mat");
        Material off = Mat(Mats, "M_PC_LED_Off", new Color(0.30f, 0.32f, 0.31f), 0.5f);
        string path = Gameplay + "Gabinete.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        var led = root.GetComponent<PcPowerLed3D>() ?? root.AddComponent<PcPowerLed3D>();
        var so = new SerializedObject(led);
        so.FindProperty("target").objectReferenceValue = root.transform.Find("Visual").GetComponent<MeshRenderer>();
        so.FindProperty("ledOn").objectReferenceValue = on;
        so.FindProperty("ledOff").objectReferenceValue = off;
        so.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
    }

    // ------------------------------------------------------------------ Canvas
    private static Sprite Sp(string n) => AssetDatabase.LoadAssetAtPath<Sprite>(Sprites + n + ".png");

    private static void Anchor(Transform t, Rect r)
    {
        var rt = (RectTransform)t;
        rt.anchorMin = r.min; rt.anchorMax = r.max; rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private static void SoftShadow(Transform t)
    {
        if (t == null || t.GetComponent<Image>() == null) return;
        var s = t.GetComponent<Shadow>() ?? t.gameObject.AddComponent<Shadow>();
        s.effectColor = new Color(0.10f, 0.14f, 0.18f, 0.28f);
        s.effectDistance = new Vector2(7f, -7f);
    }

    private static void ConfigureScreen()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(ScreenPath);
        Transform r = root.transform;

        // fundo azul-acinzentado claro (#DDE5E8): os equipamentos escuros se destacam
        var fundo = r.Find("Fundo").GetComponent<Image>();
        ColorUtility.TryParseHtmlString("#DDE5E8", out Color bg);
        fundo.color = bg;

        // Proporções naturais: mesma escala (~1000 px por metro numa tela 1920x1080) para gabinete (0,30 x 0,52 m)
        // e monitor (0,73 x 0,73 m), apoiados na mesma linha de base. Frente: gabinete à esquerda do monitor
        // (como na mesa); Traseira: espelhado (monitor à esquerda), tomadas na coluna da direita.
        Anchor(r.Find("Frente/Gabinete"), R(0.2005f, 0.1574f, 0.3557f, 0.6380f));
        Anchor(r.Find("Frente/Monitor"), R(0.4182f, 0.1574f, 0.7995f, 0.8343f));
        Anchor(r.Find("Traseira/Monitor"), R(0.1302f, 0.1574f, 0.5115f, 0.8343f));
        Anchor(r.Find("Traseira/ContainerTraseira_Area"), R(0.5300f, 0.1574f, 0.6852f, 0.6380f));   // à esquerda da miniatura da mão (EstacaoInteracaoSetup)
        foreach (string p in new[] { "Frente/Gabinete", "Frente/Monitor", "Traseira/Monitor", "Traseira/Tomada", "Traseira/Tomada_Monitor",
                                     "Traseira/PontoRede", "Traseira/ContainerTraseira_Area/ContainerTraseira/Imagem",
                                     "Interior/ContainerInterior_Area/ContainerInterior/Imagem" })
            SoftShadow(r.Find(p));

        // Botão Power: anel de hover branco sobre o botão desenhado (apagado); LED aceso só com o PC ligado
        Transform power = r.Find("Frente/Gabinete/Area/BotaoPower");
        Anchor(power, PowerLed);
        var img = power.GetComponent<Image>();
        img.sprite = Sp("ui_hover_anel"); img.preserveAspect = true; img.color = Color.white;
        var btn = power.GetComponent<Button>();
        btn.transition = Selectable.Transition.ColorTint;
        btn.targetGraphic = img;
        ColorBlock cb = btn.colors;
        cb.normalColor = new Color(1f, 1f, 1f, 0f);
        cb.highlightedColor = new Color(1f, 1f, 1f, 0.9f);
        cb.pressedColor = new Color(0.85f, 0.9f, 1f, 1f);
        cb.selectedColor = new Color(1f, 1f, 1f, 0f);
        cb.disabledColor = new Color(1f, 1f, 1f, 0f);
        cb.colorMultiplier = 1f; cb.fadeDuration = 0.08f;
        btn.colors = cb;
        Transform led = power.Find("LED");
        if (led == null) { led = new GameObject("LED", typeof(RectTransform), typeof(Image)).transform; led.SetParent(power, false); }
        Anchor(led, R(0f, 0f, 1f, 1f));
        var ledImg = led.GetComponent<Image>();
        ledImg.sprite = Sp("ui_render_pc_frente_led"); ledImg.preserveAspect = true; ledImg.raycastTarget = false;
        led.SetAsFirstSibling();
        led.gameObject.SetActive(false);
        var cso = new SerializedObject(root.GetComponent<ComputerScreenController>());
        cso.FindProperty("powerLed").objectReferenceValue = led.gameObject;
        cso.ApplyModifiedPropertiesWithoutUndo();

        // Interior: gabinete aberto 3D; cada peça = camada do quadro inteiro (mesma câmera), na ordem de montagem
        const string inside = "Interior/ContainerInterior_Area/ContainerInterior/";
        foreach (AspectRatioFitter f in r.Find("Interior").GetComponentsInChildren<AspectRatioFitter>(true)) f.aspectRatio = InteriorAspect;
        Anchor(r.Find("Interior/ContainerInterior_Area"), R(0.05f, 0.11f, 0.78f, 0.96f));
        string[] order = { "Fonte", "PlacaMae", "HD", "MemoriaRAM", "Cooler" };
        for (int i = 0; i < order.Length; i++)
        {
            Transform c = r.Find(inside + "Componentes/" + order[i]);
            Anchor(c, R(0f, 0f, 1f, 1f));
            var ci = c.GetComponent<Image>();
            ci.sprite = Sp(Layer[order[i]]); ci.preserveAspect = false; ci.raycastTarget = false; ci.color = Color.white;
            c.SetSiblingIndex(i);
            Transform s = r.Find(inside + "Encaixes/Encaixe_" + order[i]);
            Rect a = Areas[order[i]];
            float minW = 0.05f, pad = 0.008f;                     // RAM é estreita na imagem: área clicável mínima
            if (a.width < minW) a = R(a.center.x - minW / 2, a.yMin, a.center.x + minW / 2, a.yMax);
            Anchor(s, R(a.xMin - pad, a.yMin - pad, a.xMax + pad, a.yMax + pad));
            s.SetSiblingIndex(i);
        }
        PrefabUtility.SaveAsPrefabAsset(root, ScreenPath);
        PrefabUtility.UnloadPrefabContents(root);
    }
}
