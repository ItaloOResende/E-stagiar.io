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

    [MenuItem("Tools/E-stagiar.io/Configurar minigame de rede")]
    public static void Run()
    {
        AssetDatabase.Refresh();

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
    /// Aponte as MESMAS artes da conexão de rede do PC na tela do rack: a tomada (ponto de rede) e o
    /// plugue RJ45. Sem elas a tela ainda funciona, mas desenha tomada/plugue simples de reserva.
    /// </summary>
    private static void LigarArtesNoRack(GameObject tela)
    {
        var comp = tela.GetComponent<RedeRackScreen>();
        if (comp == null) return;

        var so = new SerializedObject(comp);
        bool mudou = false;

        Sprite tomada = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteTomada);
        if (tomada == null)
            Debug.LogWarning("RedeMinigameSetup: não achei '" + SpriteTomada + "' — a tomada ficará no desenho de reserva.");
        else mudou |= Atribuir(so, "spriteTomada", tomada);

        Sprite plug = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePlug);
        if (plug == null)
            Debug.LogWarning("RedeMinigameSetup: não achei '" + SpritePlug + "' — o plugue ficará no desenho de reserva.");
        else mudou |= Atribuir(so, "spritePlug", plug);

        if (mudou) so.ApplyModifiedPropertiesWithoutUndo();
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
