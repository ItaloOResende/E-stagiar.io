using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Monta os minigames de NOTEBOOK e de IMPRESSORA na cena Office_Placeholder:
///  - as telas 2D (UI_Screen_Notebook e UI_Screen_Impressora) sob o ScreenCanvas;
///  - os objetos do notebook e da impressora na sala, com visual de blocos e interação;
///  - os itens (memória SO-DIMM, SSD, toner, resma de papel) como prefabs em Pickup_* e
///    instâncias perto de cada estação.
/// Idempotente: rodar de novo só atualiza, sem duplicar.
/// Menu: Tools &gt; E-stagiar.io &gt; Configurar minigames de notebook e impressora
/// </summary>
public static class NotebookImpressoraSetup
{
    private const string CenaPath = "Assets/Scenes/Office_Placeholder.unity";
    private const string PastaPrefabs = "Assets/Prefabs/Gameplay/";
    private const string Mats = "Assets/Art/Materials/";

    [MenuItem("Tools/E-stagiar.io/Configurar minigames de notebook e impressora")]
    public static void Run()
    {
        AssetDatabase.Refresh();

        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var cena = EditorSceneManager.OpenScene(CenaPath, OpenSceneMode.Single);

        // ---- 1) telas 2D
        GameObject telaNote = GarantirTela("UI_Screen_Notebook", typeof(NotebookTela), "Notebook");
        GameObject telaImp = GarantirTela("UI_Screen_Impressora", typeof(ImpressoraTela), "Impressora");

        // ---- 2) itens (prefabs de peças)
        GameObject prefRam = GarantirPecaNotebook("Pickup_RamSodimm", NotebookParte.Ram, "memória SO-DIMM",
            new Color(0.16f, 0.5f, 0.28f), new Vector3(0.067f, 0.004f, 0.03f), 0.002f);
        GameObject prefSsd = GarantirPecaNotebook("Pickup_Ssd", NotebookParte.Ssd, "SSD",
            new Color(0.24f, 0.27f, 0.32f), new Vector3(0.08f, 0.008f, 0.055f), 0.004f);
        GameObject prefToner = GarantirPecaImpressora("Pickup_Toner", ImpressoraParte.Toner, "cartucho de toner",
            new Color(0.28f, 0.31f, 0.37f), new Vector3(0.24f, 0.07f, 0.07f), 0.035f);
        GameObject prefPapel = GarantirPecaImpressora("Pickup_ResmaPapel", ImpressoraParte.Papel, "resma de papel",
            new Color(0.86f, 0.87f, 0.83f), new Vector3(0.21f, 0.05f, 0.297f), 0.025f);

        // ---- 3) a mesa dos minigames + os objetos do mundo
        GameObject mesa = GarantirMesaMinigames();

        GameObject note = GarantirNotebook();
        LigarEstacao(note, telaNote, "Usar o notebook");
        GameObject imp = GarantirImpressora();
        LigarEstacao(imp, telaImp, "Usar a impressora");

        // ---- 4) arrumar o notebook, a impressora e os itens em cima da mesa
        GameObject prefChave = AssetDatabase.LoadAssetAtPath<GameObject>(PastaPrefabs + "Pickup_Ferramenta.prefab");
        GameObject prefPendrive = AssetDatabase.LoadAssetAtPath<GameObject>(PastaPrefabs + "Pickup_Pendrive.prefab");
        ArrumarNaMesa(mesa, note, imp, prefRam, prefSsd, prefToner, prefPapel, prefChave, prefPendrive);

        EditorSceneManager.MarkSceneDirty(cena);
        EditorSceneManager.SaveScene(cena);
        AssetDatabase.SaveAssets();
        Debug.Log("NotebookImpressoraSetup: concluído.");
    }

    // ------------------------------------------------------------------ telas

    private static GameObject GarantirTela(string nome, Type tipo, string titulo)
    {
        GameObject canvas = AcharNaCena("ScreenCanvas");
        if (canvas == null) throw new Exception("NotebookImpressoraSetup: 'ScreenCanvas' não encontrado na cena.");

        Transform existente = canvas.transform.Find(nome);
        GameObject go = existente != null ? existente.gameObject : new GameObject(nome, typeof(RectTransform));
        if (existente == null) go.transform.SetParent(canvas.transform, false);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;

        if (go.GetComponent<UIScreen>() == null) go.AddComponent<UIScreen>();
        if (go.GetComponent(tipo) == null) go.AddComponent(tipo);

        var so = new SerializedObject(go.GetComponent<UIScreen>());
        SerializedProperty nomeProp = so.FindProperty("screenName");
        if (nomeProp != null) { nomeProp.stringValue = titulo; so.ApplyModifiedPropertiesWithoutUndo(); }

        go.SetActive(false);
        return go;
    }

    private static void LigarEstacao(GameObject go, GameObject tela, string mensagem)
    {
        UIScreen screen = tela.GetComponent<UIScreen>();

        var ui = go.GetComponent<UIScreenInteractable>();
        if (ui == null) ui = go.AddComponent<UIScreenInteractable>();
        ui.screen = screen;
        ui.interactionMessage = mensagem;

        var est = go.GetComponent<RedeEstacaoInteractable>();
        if (est == null) est = go.AddComponent<RedeEstacaoInteractable>();
        est.screen = screen;
        est.interactionMessage = mensagem;
    }

    // ------------------------------------------------------------------ itens

    private static GameObject GarantirPecaNotebook(string nome, NotebookParte parte, string rotulo, Color cor, Vector3 escala, float alturaVisual)
        => GarantirPeca(nome, rotulo, cor, escala, alturaVisual, go =>
        {
            var comp = go.AddComponent<NotebookPartItem>();
            var so = new SerializedObject(comp);
            SerializedProperty p = so.FindProperty("parte");
            if (p != null) p.enumValueIndex = (int)parte;
            SerializedProperty d = so.FindProperty("displayName");
            if (d != null) d.stringValue = rotulo;
            so.ApplyModifiedPropertiesWithoutUndo();
        });

    private static GameObject GarantirPecaImpressora(string nome, ImpressoraParte parte, string rotulo, Color cor, Vector3 escala, float alturaVisual)
        => GarantirPeca(nome, rotulo, cor, escala, alturaVisual, go =>
        {
            var comp = go.AddComponent<ImpressoraPartItem>();
            var so = new SerializedObject(comp);
            SerializedProperty p = so.FindProperty("parte");
            if (p != null) p.enumValueIndex = (int)parte;
            SerializedProperty d = so.FindProperty("displayName");
            if (d != null) d.stringValue = rotulo;
            so.ApplyModifiedPropertiesWithoutUndo();
        });

    /// <summary>Cria (se não existir) um prefab de item pegável: raiz lógica + filho "Visual" (um bloco).</summary>
    private static GameObject GarantirPeca(string nome, string rotulo, Color cor, Vector3 escalaVisual, float alturaVisual, Action<GameObject> configurar)
    {
        string caminho = PastaPrefabs + nome + ".prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(caminho);
        if (prefab != null) return prefab;

        GameObject go = new GameObject(nome);
        go.AddComponent<PickupInteractable>();   // o RequireComponent acrescenta o Rigidbody

        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.name = "Visual";
        visual.transform.SetParent(go.transform, false);
        visual.transform.localPosition = new Vector3(0f, alturaVisual, 0f);
        visual.transform.localScale = escalaVisual;
        Collider colVisual = visual.GetComponent<Collider>();
        if (colVisual != null) UnityEngine.Object.DestroyImmediate(colVisual);
        visual.GetComponent<MeshRenderer>().sharedMaterial =
            MaterialCor(Mats + "M_" + nome + ".mat", cor, 0.25f);

        configurar?.Invoke(go);

        prefab = PrefabUtility.SaveAsPrefabAsset(go, caminho);
        UnityEngine.Object.DestroyImmediate(go);
        Debug.Log("NotebookImpressoraSetup: criei o item '" + caminho + "'.");
        return prefab;
    }

    /// <summary>Põe (ou move) uma instância de item em cima da mesa, como filho dela.</summary>
    private static void ColocarNaMesa(GameObject mesa, GameObject prefab, string nome, Vector3 local)
    {
        if (prefab == null || mesa == null) return;

        GameObject item = AcharNaCena(nome);
        if (item == null)
        {
            item = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            item.name = nome;
        }
        Encaixar(mesa, item, local);
    }

    private static void Encaixar(GameObject mesa, GameObject go, Vector3 local)
    {
        if (mesa == null || go == null) return;
        go.transform.SetParent(mesa.transform, false);
        go.transform.localPosition = local;
        go.transform.localRotation = Quaternion.identity;
    }

    // ------------------------------------------------------------------ objetos do mundo

    private static GameObject GarantirNotebook()
    {
        GameObject note = AcharNaCena("Notebook_Estacao");
        if (note == null) note = new GameObject("Notebook_Estacao");
        AdicionarSeFaltar<NotebookEstacao>(note);

        Transform visual = note.transform.Find("Visual");
        if (visual == null)
        {
            GameObject v = new GameObject("Visual");
            v.transform.SetParent(note.transform, false);
            visual = v.transform;
        }

        Material corpo = MaterialCor(Mats + "M_Notebook_Corpo.mat", new Color(0.22f, 0.24f, 0.28f), 0.35f);
        Material tela = MaterialCor(Mats + "M_Notebook_Tela.mat", new Color(0.05f, 0.06f, 0.09f), 0.2f);

        Transform baseNote = FilhoBloco(visual, "Base", corpo);
        baseNote.localPosition = new Vector3(0f, 0.012f, 0f);
        baseNote.localScale = new Vector3(0.34f, 0.024f, 0.25f);

        Transform dobradica = visual.Find("Dobradica");
        if (dobradica == null)
        {
            GameObject d = new GameObject("Dobradica");
            d.transform.SetParent(visual, false);
            dobradica = d.transform;
        }
        dobradica.localPosition = new Vector3(0f, 0.022f, -0.125f);
        dobradica.localRotation = Quaternion.Euler(-104f, 0f, 0f);

        Transform tampa = FilhoBloco(dobradica, "Tampa", corpo);
        tampa.localPosition = new Vector3(0f, 0.01f, 0.125f);
        tampa.localScale = new Vector3(0.34f, 0.02f, 0.25f);

        Transform display = FilhoBloco(tampa, "Display", tela);
        display.localPosition = new Vector3(0f, 0.011f, 0f);
        display.localScale = new Vector3(0.31f, 0.004f, 0.22f);

        return note;
    }

    private static GameObject GarantirImpressora()
    {
        GameObject imp = AcharNaCena("Impressora_Estacao");
        if (imp == null) imp = new GameObject("Impressora_Estacao");
        AdicionarSeFaltar<ImpressoraEstacao>(imp);

        Transform visual = imp.transform.Find("Visual");
        if (visual == null)
        {
            GameObject v = new GameObject("Visual");
            v.transform.SetParent(imp.transform, false);
            visual = v.transform;
        }

        Material corpo = MaterialCor(Mats + "M_Impressora_Corpo.mat", new Color(0.78f, 0.79f, 0.78f), 0.15f);
        Material escuro = MaterialCor(Mats + "M_Impressora_Escuro.mat", new Color(0.12f, 0.13f, 0.16f), 0.4f);

        Transform corpoImp = FilhoBloco(visual, "Corpo", corpo);
        corpoImp.localPosition = new Vector3(0f, 0.15f, 0f);
        corpoImp.localScale = new Vector3(0.42f, 0.3f, 0.38f);

        Transform saida = FilhoBloco(visual, "SaidaPapel", corpo);
        saida.localPosition = new Vector3(0f, 0.315f, 0.06f);
        saida.localScale = new Vector3(0.34f, 0.05f, 0.26f);

        Transform bandeja = FilhoBloco(visual, "Bandeja", corpo);
        bandeja.localPosition = new Vector3(0f, 0.1f, 0.2f);
        bandeja.localScale = new Vector3(0.3f, 0.14f, 0.06f);

        Transform painel = FilhoBloco(visual, "Painel", escuro);
        painel.localPosition = new Vector3(0f, 0.24f, 0.19f);
        painel.localScale = new Vector3(0.2f, 0.08f, 0.012f);

        return imp;
    }

    private static Transform FilhoBloco(Transform pai, string nome, Material material)
    {
        Transform existente = pai.Find(nome);
        GameObject go = existente != null ? existente.gameObject : GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = nome;
        if (existente == null) go.transform.SetParent(pai, false);
        go.GetComponent<MeshRenderer>().sharedMaterial = material;
        return go.transform;
    }

    // ------------------------------------------------------------------ posicionamento

    // ------------------------------------------------------------------ mesa dos minigames

    /// <summary>Altura do tampo da mesa (superfície onde as coisas ficam).</summary>
    private const float AlturaTampo = 0.78f;
    /// <summary>Onde a mesa nova nasce (canto livre da sala, à frente/esquerda). Arraste depois se quiser.</summary>
    private static readonly Vector3 PostoMesa = new Vector3(-1.9f, 0f, -2.3f);

    /// <summary>
    /// Cria (ou reaproveita) a mesa dos minigames: um tampo e quatro pés, com collider próprio, no canto
    /// livre da sala. Se ela já existe, a posição atual é respeitada — é só arrastar para onde quiser.
    /// </summary>
    private static GameObject GarantirMesaMinigames()
    {
        GameObject mesa = AcharNaCena("Mesa_Minigames");
        bool nova = mesa == null;
        if (nova) mesa = new GameObject("Mesa_Minigames");

        Material tampo = MaterialCor(Mats + "M_Mesa_Minigames_Tampo.mat", new Color(0.55f, 0.40f, 0.25f), 0.3f);
        Material pernas = MaterialCor(Mats + "M_Mesa_Minigames_Pernas.mat", new Color(0.33f, 0.35f, 0.39f), 0.4f);

        Transform topo = FilhoBloco(mesa.transform, "Tampo", tampo);
        topo.localPosition = new Vector3(0f, AlturaTampo - 0.03f, 0f);
        topo.localScale = new Vector3(1.7f, 0.06f, 0.95f);

        Vector2[] cantos =
        {
            new Vector2(-0.76f, -0.39f), new Vector2(0.76f, -0.39f),
            new Vector2(-0.76f, 0.39f), new Vector2(0.76f, 0.39f)
        };
        for (int i = 0; i < cantos.Length; i++)
        {
            Transform pe = FilhoBloco(mesa.transform, "Pe_" + (i + 1), pernas);
            pe.localPosition = new Vector3(cantos[i].x, (AlturaTampo - 0.06f) * 0.5f, cantos[i].y);
            pe.localScale = new Vector3(0.06f, AlturaTampo - 0.06f, 0.06f);
        }

        if (nova)
        {
            Vector3 posto = SobreSuperficie(PostoMesa, mesa);
            mesa.transform.SetPositionAndRotation(posto, Quaternion.identity);
            Debug.Log($"NotebookImpressoraSetup: mesa dos minigames em {posto:F2} " +
                      "(arraste 'Mesa_Minigames' se quiser noutro lugar).");
        }

        return mesa;
    }

    /// <summary>Coloca o notebook, a impressora e os itens EM CIMA da mesa (e deixa tudo como filho dela).</summary>
    private static void ArrumarNaMesa(GameObject mesa, GameObject note, GameObject imp,
        GameObject prefRam, GameObject prefSsd, GameObject prefToner, GameObject prefPapel,
        GameObject prefChave, GameObject prefPendrive)
    {
        // Estações: um pouco para trás na mesa (a tela do notebook e a bandeja da impressora olham para o jogador).
        Encaixar(mesa, note, new Vector3(-0.44f, AlturaTampo, -0.10f));
        Encaixar(mesa, imp, new Vector3(0.46f, AlturaTampo, -0.12f));

        // Itens na borda da frente, ao alcance da mão.
        ColocarNaMesa(mesa, prefRam, "RamSodimm_Pickup", new Vector3(-0.70f, AlturaTampo, 0.30f));
        ColocarNaMesa(mesa, prefSsd, "Ssd_Pickup", new Vector3(-0.42f, AlturaTampo, 0.30f));
        ColocarNaMesa(mesa, prefChave, "ChaveFenda_Pickup", new Vector3(-0.14f, AlturaTampo, 0.31f));
        ColocarNaMesa(mesa, prefPendrive, "Pendrive_Pickup", new Vector3(0.12f, AlturaTampo, 0.31f));
        ColocarNaMesa(mesa, prefToner, "Toner_Pickup", new Vector3(0.48f, AlturaTampo, 0.30f));
        ColocarNaMesa(mesa, prefPapel, "ResmaPapel_Pickup", new Vector3(0.70f, AlturaTampo, 0.28f));
    }

    /// <summary>Projeta um ponto no chão/superfície abaixo dele (a primeira superfície "de cima").</summary>
    private static Vector3 SobreSuperficie(Vector3 alvo, GameObject ignorar, float alturaExtra = 0f)
    {
        Vector3 de = alvo + Vector3.up * 1.8f;
        float y = alvo.y;
        RaycastHit[] hits = Physics.RaycastAll(de, Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit h in hits)
        {
            if (ignorar != null && h.collider.transform.IsChildOf(ignorar.transform)) continue;
            if (h.normal.y < 0.6f) continue;
            y = h.point.y;
            break;
        }
        return new Vector3(alvo.x, y + alturaExtra, alvo.z);
    }

    // ------------------------------------------------------------------ utilidades

    private static void AdicionarSeFaltar<T>(GameObject go) where T : Component
    {
        if (go.GetComponent<T>() == null) go.AddComponent<T>();
    }

    private static Material MaterialCor(string path, Color cor, float suave)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_BaseColor", cor);
        m.SetFloat("_Smoothness", suave);
        EditorUtility.SetDirty(m);
        return m;
    }

    private static GameObject AcharNaCena(string nome)
    {
        foreach (GameObject raiz in EditorSceneManager.GetActiveScene().GetRootGameObjects())
        {
            Transform t = AcharRec(raiz.transform, nome);
            if (t != null) return t.gameObject;
        }
        return null;
    }

    private static Transform AcharRec(Transform t, string nome)
    {
        if (t.name == nome) return t;
        foreach (Transform filho in t)
        {
            Transform r = AcharRec(filho, nome);
            if (r != null) return r;
        }
        return null;
    }
}
