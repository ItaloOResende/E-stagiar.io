using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Correções dos testes na Unity (2026-10-14, 2ª etapa dos minigames). Pontual e idempotente; não reconstrói a cena.
///  1. Peças dos minigames quebráveis: Pickup_RamSodimm (modelo novo Item_MemoriaSodimm) e Pickup_Ssd quebram na 1ª
///     queda (Visual_Quebrado = Item_*_Quebrad*), Pickup_Toner na 2ª (camada de rachaduras). A lógica (NotebookPartItem /
///     ImpressoraPartItem = IBreakable) recusa peça quebrada nos encaixes.
///  2. Impressora: material ORIGINAL do Matheus (remapeamento do impressora.fbx para M_Objetos_Parede; desfaz a troca de
///     material na instância) e painel reposicionado na face da frente (aplicado ao prefab Impressora_Estacao).
///  3. Tela do notebook (prefab UI_Screen_Notebook): mesmos parafusos (ScrewView), cursor de chave de fenda e telas do
///     monitor (BIOS, sem sistema, instalando, sistema) da tela do gabinete.
///  4. Alcance único das interações: InteractionDetector do Player = 2 m (o ItemHolder usa o mesmo valor).
/// Menu: Tools &gt; E-stagiar.io &gt; Correções dos testes (minigames, impressora, alcance)
/// </summary>
public static class CorrecoesTestesSetup
{
    private const string Cena = "Assets/Scenes/Office_Placeholder.unity";
    private const string Gameplay = "Assets/Prefabs/Gameplay/";
    private const string PrefabsUI = "Assets/Prefabs/UI/";
    private const float Alcance = 2f;

    [MenuItem("Tools/E-stagiar.io/Correções dos testes (minigames, impressora, alcance)")]
    public static void Run()
    {
        AssetDatabase.Refresh();
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        Material rachadura = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/M_Rachadura.mat");
        PecaQuebravel("Pickup_RamSodimm", "Item_MemoriaSodimm", "Item_MemoriaSodimm_Quebrada", 1, trocarVisual: true);
        PecaQuebravel("Pickup_Ssd", "Item_Ssd", "Item_Ssd_Quebrado", 1, trocarVisual: false);
        TonerQuebravel(rachadura);
        TelaNotebook();

        MinigamesEstacoesSetup.GarantirMaterialOriginalImpressora();
        ImpressoraSemTrocaDeMaterialNoPrefab();

        var cena = EditorSceneManager.OpenScene(Cena, OpenSceneMode.Single);
        ImpressoraNaCena();
        AlcanceDoJogador();
        EditorSceneManager.MarkSceneDirty(cena);
        EditorSceneManager.SaveScene(cena);
        AssetDatabase.SaveAssets();
        Validar();
        Debug.Log("CorrecoesTestesSetup: concluído.");
    }

    // ================================================================== peças quebráveis

    private static void PecaQuebravel(string prefab, string inteira, string quebrada, int quedas, bool trocarVisual)
    {
        string path = Gameplay + prefab + ".prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        if (trocarVisual) MinigamesEstacoesSetup.VisualDoItem(root, inteira, 0.02f);

        GameObject camada = MinigamesEstacoesSetup.NovaCamada(root.transform);
        var (mesh, mats) = MinigamesEstacoesSetup.ItemFbx(quebrada);
        camada.AddComponent<MeshFilter>().sharedMesh = mesh;
        camada.AddComponent<MeshRenderer>().sharedMaterials = mats;

        var dano = MinigamesEstacoesSetup.Garantir<PartFallDamage>(root);
        var so = new SerializedObject(dano);
        so.FindProperty("dropsToBreak").intValue = quedas;
        so.FindProperty("intactVisual").objectReferenceValue = root.transform.Find("Visual").gameObject;   // troca de malha, como as peças do PC
        so.FindProperty("brokenVisual").objectReferenceValue = camada;
        so.ApplyModifiedPropertiesWithoutUndo();
        camada.SetActive(false);
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
    }

    /// <summary>Cartucho de toner: quebra na 2ª queda; rachaduras por cima do visual atual (topo e laterais).</summary>
    private static void TonerQuebravel(Material mat)
    {
        string path = Gameplay + "Pickup_Toner.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        Transform visual = root.transform.Find("Visual");
        Bounds b = new Bounds(visual.localPosition, visual.localScale);   // o Visual do toner é um cubo unitário escalado
        GameObject camada = MinigamesEstacoesSetup.NovaCamada(root.transform);
        Mesh quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        Transform t = root.transform;
        MinigamesEstacoesSetup.Placa(camada.transform, quad, mat, t.TransformPoint(b.center + Vector3.up * (b.extents.y + 0.002f)),
            Quaternion.LookRotation(-t.up, t.forward), new Vector3(b.size.x * 0.9f, b.size.z * 0.9f, 1f));
        foreach (float s in new[] { 1f, -1f })
            MinigamesEstacoesSetup.Placa(camada.transform, quad, mat, t.TransformPoint(b.center + Vector3.forward * s * (b.extents.z + 0.002f)),
                Quaternion.LookRotation(-t.forward * s, t.up), new Vector3(b.size.x * 0.9f, b.size.y * 0.9f, 1f));

        var dano = MinigamesEstacoesSetup.Garantir<PartFallDamage>(root);
        var so = new SerializedObject(dano);
        so.FindProperty("dropsToBreak").intValue = 2;
        so.FindProperty("intactVisual").objectReferenceValue = null;
        so.FindProperty("brokenVisual").objectReferenceValue = camada;
        so.ApplyModifiedPropertiesWithoutUndo();
        camada.SetActive(false);
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
    }

    // ================================================================== tela do notebook

    private static void TelaNotebook()
    {
        var gabinete = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabsUI + "UI_Screen_Gabinete.prefab")
            .GetComponentInChildren<ComputerScreenController>(true);
        var origem = new SerializedObject(gabinete);
        string path = PrefabsUI + "UI_Screen_Notebook.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        var so = new SerializedObject(root.GetComponent<NotebookTela>());
        so.FindProperty("parafusoPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ScrewView>(PrefabsUI + "ScrewView.prefab");
        so.FindProperty("cursorChave").objectReferenceValue = origem.FindProperty("rearCursor").objectReferenceValue;
        so.FindProperty("cursorChaveHotspot").vector2Value = origem.FindProperty("rearCursorHotspot").vector2Value;
        so.FindProperty("chaveParafusoProporcao").floatValue = origem.FindProperty("screwdriverToScrewRatio").floatValue;
        so.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
        TelasDoNotebook();
    }

    /// <summary>
    /// Telas do notebook: as mesmas do monitor do PC, geradas com a terminologia do notebook (SSD, SO-DIMM, tela
    /// integrada, Wi-Fi neutro) por Tools/screens/gerar_telas_monitor.py (PH_ui_tela_notebook_*), com a mesma importação
    /// das telas do PC. Pode rodar sozinho (menu ou -executeMethod).
    /// </summary>
    [MenuItem("Tools/E-stagiar.io/Telas do notebook (SSD)")]
    public static void TelasDoNotebook()
    {
        const string ui = "Assets/Art/UI/", pre = ui + "PH_ui_tela_notebook_";
        AssetDatabase.Refresh();
        var modelo = (TextureImporter)AssetImporter.GetAtPath(ui + "PH_ui_tela_bios.png");
        var ajustes = new TextureImporterSettings();
        modelo.ReadTextureSettings(ajustes);
        string[] nomes = { "bios", "bios_sem_sistema", "instalando_0", "instalando_1", "instalando_2", "instalando_3", "instalando_4", "sistema" };
        foreach (string n in nomes)
        {
            var imp = (TextureImporter)AssetImporter.GetAtPath(pre + n + ".png");
            if (imp == null) throw new System.Exception("CorrecoesTestesSetup: falta " + pre + n + ".png (rode gerar_telas_monitor.py).");
            imp.SetTextureSettings(ajustes);
            imp.textureCompression = modelo.textureCompression;
            imp.SaveAndReimport();
        }
        Sprite S(string n) => AssetDatabase.LoadAssetAtPath<Sprite>(pre + n + ".png");

        string path = PrefabsUI + "UI_Screen_Notebook.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        var so = new SerializedObject(root.GetComponent<NotebookTela>());
        so.FindProperty("telaSemArmazenamento").objectReferenceValue = S("bios");
        so.FindProperty("telaSemSistema").objectReferenceValue = S("bios_sem_sistema");
        so.FindProperty("telaSistema").objectReferenceValue = S("sistema");
        SerializedProperty inst = so.FindProperty("telaInstalando");
        inst.arraySize = 5;
        for (int i = 0; i < 5; i++) inst.GetArrayElementAtIndex(i).objectReferenceValue = S("instalando_" + i);
        so.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);

        var check = new SerializedObject(AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<NotebookTela>());
        bool ok = check.FindProperty("telaSemArmazenamento").objectReferenceValue != null && check.FindProperty("telaSistema").objectReferenceValue != null;
        for (int i = 0; i < 5; i++) ok &= check.FindProperty("telaInstalando").GetArrayElementAtIndex(i).objectReferenceValue != null;
        if (ok) Debug.Log("CorrecoesTestesSetup: telas do notebook (SSD) ligadas."); else Debug.LogError("CorrecoesTestesSetup: telas do notebook incompletas.");
    }

    // ================================================================== impressora

    private static void ImpressoraSemTrocaDeMaterialNoPrefab()
    {
        string path = Gameplay + "Impressora_Estacao.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        Transform modelo = root.transform.Find("Visual/Modelo");
        if (modelo != null)
            foreach (Renderer r in modelo.GetComponentsInChildren<Renderer>(true)) MinigamesEstacoesSetup.RevertMateriais(r);
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
    }

    private static void ImpressoraNaCena()
    {
        GameObject imp = MinigamesEstacoesSetup.Achar("Impressora_Estacao");
        if (imp == null) { Debug.LogWarning("CorrecoesTestesSetup: 'Impressora_Estacao' não está na cena."); return; }
        Transform modelo = imp.transform.Find("Visual/Modelo");
        Transform painel = imp.transform.Find("PainelControle");
        foreach (Renderer r in modelo.GetComponentsInChildren<Renderer>(true)) MinigamesEstacoesSetup.RevertMateriais(r);

        var mc = modelo.GetComponentInChildren<MeshCollider>(true);
        Bounds pb = MinigamesEstacoesSetup.ItemFbx("Item_PainelImpressora").mesh.bounds;
        float lado = MinigamesEstacoesSetup.LadoDoPainel(imp.transform, modelo, out Bounds b);
        MinigamesEstacoesSetup.PosicionarPainelNaFrente(imp.transform, mc, painel, pb, lado, b);
        // só a posição do painel vai para o prefab (as telas da cena continuam como override da instância)
        Vector3 pos = painel.localPosition;
        Quaternion rot = painel.localRotation;
        string path = Gameplay + "Impressora_Estacao.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        Transform p = root.transform.Find("PainelControle");
        p.localPosition = pos;
        p.localRotation = rot;
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
        PrefabUtility.RevertObjectOverride(painel, InteractionMode.AutomatedAction);   // a cena passa a seguir o prefab
    }

    // ================================================================== alcance

    private static void AlcanceDoJogador()
    {
        foreach (InteractionDetector d in Object.FindObjectsByType<InteractionDetector>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var so = new SerializedObject(d);
            so.FindProperty("interactionDistance").floatValue = Alcance;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    // ================================================================== validação

    private static void Validar()
    {
        var problemas = new System.Collections.Generic.List<string>();
        foreach (string p in new[] { "Pickup_RamSodimm", "Pickup_Ssd", "Pickup_Toner" })
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(Gameplay + p + ".prefab");
            var dano = go.GetComponent<PartFallDamage>();
            if (dano == null || go.GetComponent<IBreakable>() == null) problemas.Add(p + " sem dano");
        }
        var nb = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabsUI + "UI_Screen_Notebook.prefab").GetComponent<NotebookTela>();
        var nso = new SerializedObject(nb);
        foreach (string f in new[] { "parafusoPrefab", "cursorChave", "telaSemArmazenamento", "telaSistema" })
            if (nso.FindProperty(f).objectReferenceValue == null) problemas.Add("UI_Screen_Notebook." + f + " vazio");
        if (nso.FindProperty("telaInstalando").arraySize == 0) problemas.Add("UI_Screen_Notebook.telaInstalando vazio");

        GameObject imp = MinigamesEstacoesSetup.Achar("Impressora_Estacao");
        if (imp != null)
        {
            Renderer r = imp.transform.Find("Visual/Modelo").GetComponentInChildren<Renderer>();
            string mats = string.Join(",", r.sharedMaterials.Select(m => m != null ? m.name : "null"));
            if (!mats.Contains("M_Objetos_Parede")) problemas.Add("impressora com material " + mats);
            Debug.Log($"CorrecoesTestesSetup: impressora material={mats}; painel em {imp.transform.Find("PainelControle").localPosition:F3}.");
        }
        foreach (InteractionDetector d in Object.FindObjectsByType<InteractionDetector>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (Mathf.Abs(d.InteractionDistance - Alcance) > 0.001f) problemas.Add("alcance " + d.InteractionDistance);
        if (problemas.Count > 0) Debug.LogError("CorrecoesTestesSetup: " + string.Join("; ", problemas));
        else Debug.Log("CorrecoesTestesSetup: validação OK.");
    }
}
