using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Monta o minigame de cabo de rede na cena Office_Placeholder:
///  - o estado compartilhado (RedeGame);
///  - a tela 2D de crimpagem (UI_Screen_Crimpagem) e a tela do rack (UI_Screen_Rack) sob o ScreenCanvas;
///  - a caixa de cabo de rede na sala, perto do rack;
///  - o componente de interação no rack (objeto "RackInternet").
/// Idempotente: rodar de novo só atualiza, sem duplicar.
/// Menu: Tools &gt; E-stagiar.io &gt; Configurar minigame de rede
/// </summary>
public static class RedeMinigameSetup
{
    private const string CenaPath = "Assets/Scenes/Office_Placeholder.unity";
    private const string Mats = "Assets/Art/Materials/";
    private const string CaboAzul = "Assets/Art/Materials/Cabos/M_Cabo_Azul.mat";
    private const string CaboRedePrefab = "Assets/Prefabs/Gameplay/Pickup_CaboRede.prefab";
    // As MESMAS artes da conexão de rede do PC: a tomada (ponto de rede) e o plugue RJ45.
    private const string SpriteTomada = "Assets/Art/UI/ui_render_ponto_rede.png";
    private const string SpritePlug = "Assets/Art/UI/ui_render_plugue_rj45.png";

    // Os PREFABS das duas telas: como as cenas usam esses prefabs por instância, preencher as artes aqui vale
    // para todas elas de uma vez (Office_Placeholder, Escritorio_Teste e Fase_01), sem mexer em cena nenhuma.
    private const string PrefabCrimp = "Assets/Prefabs/UI/UI_Screen_Crimpagem.prefab";
    private const string PrefabRack = "Assets/Prefabs/UI/UI_Screen_Rack.prefab";

    // As artes e os sons do PROTÓTIPO da cena MinigameTest (o mesmo desenho que o RedeCrimpMinigame usava).
    private const string ArteFundo = "Assets/Art/Textures/Sprites/bg1.png";
    private const string ArteCaixa = "Assets/Art/Textures/Sprites/caixa.png";
    private const string ArteCapa = "Assets/Art/Textures/Sprites/capa.png";
    private const string ArteRj45 = "Assets/Art/Textures/Sprites/conector.png";
    private const string ArteAlicate = "Assets/Art/Textures/Sprites/alicate1.png";
    private const string ArteAlicateFechado = "Assets/Art/Textures/Sprites/alicate2.png";
    private const string SomCorte = "Assets/Art/Sounds/corte.mp3";
    private const string SomDescascar = "Assets/Art/Sounds/bip.mp3";
    private const string SomErro = "Assets/Art/Sounds/erro.mp3";

    // Marca (por usuário) de que as artes já foram aplicadas nos prefabs, para não repetir a cada reload.
    private const string VersaoArtes = "rede-artes-v1";
    private const string ChaveArtes = "E-stagiar.io.Rede.ArtesAplicadas";

    [MenuItem("Tools/E-stagiar.io/Configurar minigame de rede")]
    public static void Run()
    {
        AssetDatabase.Refresh();

        // As artes/efeitos/sons do protótipo entram pelo PREFAB das telas (vale para todas as cenas de uma vez).
        AplicarArtesNosPrefabs();

        // Em batch (linha de comando) não existe diálogo: não pergunta nada e segue direto.
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var cena = EditorSceneManager.OpenScene(CenaPath, OpenSceneMode.Single);

        // 1) estado
        GameObject redeGame = AcharNaCena("RedeGame");
        if (redeGame == null) redeGame = new GameObject("RedeGame");
        AdicionarSeFaltar<RedeGame>(redeGame);

        // 2) telas 2D
        GameObject telaCrimp = GarantirTela("UI_Screen_Crimpagem", typeof(RedeCrimpScreen), "Crimpagem de cabo de rede");
        GameObject telaRack = GarantirTela("UI_Screen_Rack", typeof(RedeRackScreen), "Rack de rede");
        LigarCaboNaCrimpagem(telaCrimp);
        LigarArtesNoRack(telaRack);

        // 3) caixa de cabo de rede
        GameObject caixa = GarantirCaixa();
        LigarEstacao(caixa, telaCrimp, "Usar a caixa de cabo de rede");

        // 4) rack
        GameObject rack = AcharNaCena("RackInternet");
        if (rack != null) LigarEstacao(rack, telaRack, "Ligar o cabo no rack");
        else Debug.LogWarning("RedeMinigameSetup: não achei o objeto 'RackInternet' na cena — o rack não foi ligado. " +
                              "Coloque o modelo do rack na sala e rode de novo (ou arraste a tela UI_Screen_Rack no componente).");

        EditorSceneManager.MarkSceneDirty(cena);
        EditorSceneManager.SaveScene(cena);
        AssetDatabase.SaveAssets();
        Debug.Log("RedeMinigameSetup: concluído.");
    }

    // ------------------------------------------------------------------ telas

    /// <summary>Aponte o prefab do cabo de rede no campo "caboPrefab" da tela de crimpagem.</summary>
    private static void LigarCaboNaCrimpagem(GameObject tela)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CaboRedePrefab);
        if (prefab == null)
        {
            Debug.LogWarning("RedeMinigameSetup: não achei '" + CaboRedePrefab + "' — o cabo não será entregue na mão.");
            return;
        }

        var comp = tela.GetComponent<RedeCrimpScreen>();
        if (comp == null) return;

        var so = new SerializedObject(comp);
        SerializedProperty prop = so.FindProperty("caboPrefab");
        if (prop != null) { prop.objectReferenceValue = prefab; so.ApplyModifiedPropertiesWithoutUndo(); }
    }

    /// <summary>
    /// Aponta na crimpagem as MESMAS artes, efeitos e sons do protótipo da cena MinigameTest. Sem eles a tela
    /// continua funcionando com o desenho e os sons de reserva (cada campo é opcional).
    /// </summary>
    private static void LigarArtesDaCrimpagem(RedeCrimpScreen comp)
    {
        if (comp == null) return;

        var so = new SerializedObject(comp);
        bool mudou = false;

        mudou |= Arte(so, "spriteFundo", ArteFundo);
        mudou |= Arte(so, "spriteCaixa", ArteCaixa);
        mudou |= Arte(so, "spriteCapa", ArteCapa);
        mudou |= Arte(so, "spriteRj45", ArteRj45);
        mudou |= Arte(so, "spriteAlicateAberto", ArteAlicate);
        mudou |= Arte(so, "spriteAlicateFechado", ArteAlicateFechado);

        mudou |= Som(so, "somCorte", SomCorte);
        mudou |= Som(so, "somDescascar", SomDescascar);
        mudou |= Som(so, "somErro", SomErro);

        if (mudou) so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void LigarArtesNoRack(GameObject tela)
    {
        if (tela == null) return;
        LigarArtesNoRack(tela.GetComponent<RedeRackScreen>());
    }

    /// <summary>
    /// Aponte as MESMAS artes da conexão de rede do PC na tela do rack: a tomada (ponto de rede) e o
    /// plugue RJ45. Sem elas a tela ainda funciona, mas desenha tomada/plugue simples de reserva.
    /// O fundo é o mesmo da crimpagem (bg1), para as duas telas do minigame combinarem.
    /// </summary>
    private static void LigarArtesNoRack(RedeRackScreen comp)
    {
        if (comp == null) return;

        var so = new SerializedObject(comp);
        bool mudou = false;

        mudou |= Arte(so, "spriteTomada", SpriteTomada);
        mudou |= Arte(so, "spritePlug", SpritePlug);
        mudou |= Arte(so, "spriteFundo", ArteFundo);

        if (mudou) so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>Preenche um campo de sprite da tela (avisa no Console se a arte não existir no projeto).</summary>
    private static bool Arte(SerializedObject so, string campo, string caminho)
    {
        var arte = AssetDatabase.LoadAssetAtPath<Sprite>(caminho);
        if (arte == null)
        {
            Debug.LogWarning("RedeMinigameSetup: não achei a arte '" + caminho + "' — '" + campo + "' fica no desenho de reserva.");
            return false;
        }
        return Atribuir(so, campo, arte);
    }

    /// <summary>Preenche um campo de som da tela (avisa no Console se o som não existir no projeto).</summary>
    private static bool Som(SerializedObject so, string campo, string caminho)
    {
        var clipe = AssetDatabase.LoadAssetAtPath<AudioClip>(caminho);
        if (clipe == null)
        {
            Debug.LogWarning("RedeMinigameSetup: não achei o som '" + caminho + "' — '" + campo + "' fica no som genérico.");
            return false;
        }
        return Atribuir(so, campo, clipe);
    }

    // ------------------------------------------------------------------ artes do protótipo nos prefabs

    /// <summary>
    /// Aponta as artes/efeitos/sons do protótipo nos PREFABS das duas telas. Como as cenas usam esses prefabs por
    /// instância, isto vale de uma vez para a Office_Placeholder, a Escritorio_Teste e a Fase_01, sem tocar em
    /// nenhuma cena. Devolve quantos prefabs foram preenchidos (0 a 2).
    /// </summary>
    public static int AplicarArtesNosPrefabs()
    {
        int ok = 0;
        if (AplicarArtesNoPrefab(PrefabCrimp)) ok++;
        if (AplicarArtesNoPrefab(PrefabRack)) ok++;
        if (ok > 0) AssetDatabase.SaveAssets();
        return ok;
    }

    private static bool AplicarArtesNoPrefab(string caminho)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(caminho) == null)
        {
            Debug.LogWarning("RedeMinigameSetup: não achei o prefab '" + caminho + "'.");
            return false;
        }

        // Não mexer num prefab que está aberto para edição (Prefab Mode): o Unity não gosta de salvar por baixo.
        PrefabStage aberto = PrefabStageUtility.GetCurrentPrefabStage();
        if (aberto != null && aberto.prefabAssetPath == caminho)
        {
            Debug.LogWarning("RedeMinigameSetup: o prefab '" + caminho + "' está aberto no modo Prefab — " +
                             "feche essa aba e rode Tools > E-stagiar.io > Rede: aplicar as artes do protótipo.");
            return false;
        }

        GameObject raiz = PrefabUtility.LoadPrefabContents(caminho);
        try
        {
            var crimp = raiz.GetComponent<RedeCrimpScreen>();
            var rack = raiz.GetComponent<RedeRackScreen>();
            if (crimp == null && rack == null)
            {
                Debug.LogWarning("RedeMinigameSetup: o prefab '" + caminho + "' não tem as telas de rede — não mexi nele.");
                return false;
            }

            LigarArtesDaCrimpagem(crimp);
            LigarArtesNoRack(rack);
            PrefabUtility.SaveAsPrefabAsset(raiz, caminho);
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(raiz);
        }
    }

    [MenuItem("Tools/E-stagiar.io/Rede: aplicar as artes do protótipo")]
    public static void AplicarArtesPeloMenu()
    {
        AssetDatabase.Refresh();
        int n = AplicarArtesNosPrefabs();
        EditorPrefs.SetString(ChaveArtes, VersaoArtes);
        Debug.Log($"RedeMinigameSetup: artes do protótipo aplicadas em {n} de 2 prefab(s) de tela.");
    }

    /// <summary>
    /// Ao abrir o editor (depois de compilar), aplica as artes do protótipo UMA vez nos prefabs das telas: assim o
    /// minigame já abre com a arte do MinigameTest sem precisar rodar menu nenhum. Se não der certo, NÃO marca como
    /// feito (tenta de novo no próximo reload) e o menu acima continua disponível para rodar à mão.
    /// </summary>
    [InitializeOnLoadMethod]
    private static void AplicarArtesAoAbrirOEditor()
    {
        if (Application.isBatchMode) return;
        if (EditorPrefs.GetString(ChaveArtes, string.Empty) == VersaoArtes) return;

        EditorApplication.delayCall += () =>
        {
            if (EditorPrefs.GetString(ChaveArtes, string.Empty) == VersaoArtes) return;
            // Em Play não se mexe em asset (fica para o próximo reload, ou para o menu).
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            try
            {
                if (AplicarArtesNosPrefabs() == 2) EditorPrefs.SetString(ChaveArtes, VersaoArtes);
                else Debug.LogWarning("RedeMinigameSetup: nem todos os prefabs das telas receberam as artes do " +
                                      "protótipo. Rode Tools > E-stagiar.io > Rede: aplicar as artes do protótipo.");
            }
            catch (Exception e)
            {
                Debug.LogWarning("RedeMinigameSetup: não deu para aplicar as artes do protótipo automaticamente (" +
                                 e.Message + "). Rode Tools > E-stagiar.io > Rede: aplicar as artes do protótipo.");
            }
        };
    }

    private static bool Atribuir(SerializedObject so, string campo, UnityEngine.Object valor)
    {
        SerializedProperty prop = so.FindProperty(campo);
        if (prop == null)
        {
            Debug.LogWarning("RedeMinigameSetup: o campo '" + campo + "' não existe em " + so.targetObject.GetType().Name + ".");
            return false;
        }
        if (prop.objectReferenceValue == valor) return false;
        prop.objectReferenceValue = valor;
        return true;
    }

    private static GameObject GarantirTela(string nome, Type tipo, string titulo)
    {
        GameObject canvas = AcharNaCena("ScreenCanvas");
        if (canvas == null) throw new Exception("RedeMinigameSetup: 'ScreenCanvas' não encontrado na cena.");

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

        go.SetActive(false);   // começa fechada; o UIScreenManager liga ao abrir
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

    // ------------------------------------------------------------------ caixa

    private static GameObject GarantirCaixa()
    {
        GameObject caixa = AcharNaCena("Caixa_CaboRede");
        if (caixa == null) caixa = new GameObject("Caixa_CaboRede");

        Transform visual = caixa.transform.Find("Visual");
        if (visual == null)
        {
            GameObject cubo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cubo.name = "Visual";
            cubo.transform.SetParent(caixa.transform, false);
            visual = cubo.transform;
        }
        visual.localPosition = new Vector3(0f, 0.12f, 0f);
        visual.localRotation = Quaternion.identity;
        visual.localScale = new Vector3(0.36f, 0.24f, 0.26f);
        visual.GetComponent<MeshRenderer>().sharedMaterial =
            MaterialCor(Mats + "M_Caixa_Cabo.mat", new Color(0.60f, 0.44f, 0.27f), 0.15f);

        Transform cabo = caixa.transform.Find("Cabo");
        if (cabo == null)
        {
            GameObject c = GameObject.CreatePrimitive(PrimitiveType.Cube);
            c.name = "Cabo";
            c.transform.SetParent(caixa.transform, false);
            cabo = c.transform;
        }
        cabo.localPosition = new Vector3(0.10f, 0.245f, 0f);
        cabo.localRotation = Quaternion.Euler(0f, 0f, 16f);
        cabo.localScale = new Vector3(0.52f, 0.035f, 0.035f);
        Material matCabo = AssetDatabase.LoadAssetAtPath<Material>(CaboAzul);
        if (matCabo == null) matCabo = MaterialCor(Mats + "M_Cabo_Rede_Azul.mat", new Color(0.18f, 0.45f, 0.85f), 0.4f);
        cabo.GetComponent<MeshRenderer>().sharedMaterial = matCabo;
        Collider colCabo = cabo.GetComponent<Collider>();
        if (colCabo != null) UnityEngine.Object.DestroyImmediate(colCabo);   // não atrapalha a mira com o cabo

        PosicionarCaixa(caixa);
        return caixa;
    }

    private static void PosicionarCaixa(GameObject caixa)
    {
        GameObject rack = AcharNaCena("RackInternet");
        Vector3 basePos;
        if (rack != null)
        {
            Vector3 dir = Vector3.zero - rack.transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.back;
            basePos = rack.transform.position + dir.normalized * 1.15f;
        }
        else basePos = new Vector3(1.6f, 0f, -0.6f);

        Vector3 de = basePos + Vector3.up * 1.6f;
        float y = basePos.y;
        foreach (RaycastHit h in Physics.RaycastAll(de, Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.transform.IsChildOf(caixa.transform)) continue;   // ignora a própria caixa
            if (h.normal.y < 0.7f) continue;                                  // só superfície de chão
            y = h.point.y;
            break;
        }
        caixa.transform.position = new Vector3(basePos.x, y, basePos.z);
        caixa.transform.rotation = Quaternion.identity;
        Debug.Log($"RedeMinigameSetup: caixa de cabo de rede em {caixa.transform.position:F2}.");
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
