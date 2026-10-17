using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Rodada 2 das correções (2026-10-16): plaquinhas de mesa com o funcionário de cada estação, seta de localização do
/// chamado, chamado de crimpagem na estação do funcionário (não na caixa de cabo). Idempotente.
/// </summary>
public static class CorrecoesRodada2Setup
{
    private const string Cena = "Assets/Scenes/Teste_Integrado.unity";
    private const string PlacaPath = "Assets/Prefabs/Props/Placa_Mesa.prefab";
    private const string SetaPath = "Assets/Resources/Indicador_Chamado.prefab";
    private const string Mats = "Assets/Art/Materials/Placas";

    // Funcionários das estações = solicitantes reais das fichas (TicketsData/Gerados). Desktops: quem abre chamados de
    // PC/rede; notebooks: quem abre chamados de notebook (Paula, Laura) e Joana (impressora), que trabalha num notebook.
    private static readonly (string estacao, string nome, string setor)[] Funcionarios =
    {
        ("Estacao_1", "Francisco", "Comercial"), ("Estacao_2", "Marcia", "Financeiro"), ("Estacao_3", "Carmen", "RH"),
        ("Estacao_4", "Paula", "Marketing"), ("Estacao_5", "Laura", "Diretoria"), ("Estacao_6", "Joana", "Administrativo"),
    };

    [MenuItem("Tools/E-stagiar.io/Correções 2026-10-16 (2): placas, seta, chamados de rede")]
    public static void Tudo()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        GameObject placa = CriarPlaca();
        CriarSeta();
        var scene = EditorSceneManager.OpenScene(Cena);
        var log = new List<string>();
        TicketManager tickets = Object.FindAnyObjectByType<TicketManager>(FindObjectsInactive.Include);

        // departamentos reais das fichas, quando a ficha existe
        var setores = new Dictionary<string, string>();
        foreach (string g in AssetDatabase.FindAssets("t:TicketDefinition", new[] { "Assets/TicketsData/Gerados" }))
        {
            var def = AssetDatabase.LoadAssetAtPath<TicketDefinition>(AssetDatabase.GUIDToAssetPath(g));
            if (def != null && !string.IsNullOrEmpty(def.requester)) setores[def.requester] = def.department;
        }

        foreach (var (nomeEst, nome, setor) in Funcionarios)
        {
            Transform est = Estacao(nomeEst);
            if (est == null) { log.Add("FALTA " + nomeEst); continue; }
            var emp = est.GetComponent<StationEmployee>(); if (emp == null) emp = est.gameObject.AddComponent<StationEmployee>();
            emp.employeeName = nome;
            emp.department = setores.TryGetValue(nome, out string s) && !string.IsNullOrEmpty(s) ? s : setor;
            EditorUtility.SetDirty(emp);
            ColocarPlaca(est, placa, scene, log);
        }

        // crimpagem: chamado da ESTAÇÃO (PcTicketBridge), não da caixa de cabo
        string crimp = TestadorCabo.ChamadoCrimpagem;
        foreach (PcTicketBridge pc in Object.FindObjectsByType<PcTicketBridge>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            AjustarIds(pc, ids => { if (!ids.Contains(crimp)) ids.Add(crimp); }, log);
        foreach (RedeTicketBridge r in Object.FindObjectsByType<RedeTicketBridge>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            AjustarIds(r, ids => ids.Remove(crimp), log);
            var so = new SerializedObject(r);
            so.FindProperty("deviceLabel").stringValue = "Caixa do cabo de rede";
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        log.Add("crimpar-cabo agora é atendido no PC da estação");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Rodada2] " + string.Join(" · ", log));
    }

    private static void AjustarIds(TicketDeviceBridge b, System.Action<List<string>> mudar, List<string> log)
    {
        var so = new SerializedObject(b);
        SerializedProperty p = so.FindProperty("acceptedTicketIds");
        var ids = new List<string>();
        for (int i = 0; i < p.arraySize; i++) ids.Add(p.GetArrayElementAtIndex(i).stringValue);
        mudar(ids);
        p.arraySize = ids.Count;
        for (int i = 0; i < ids.Count; i++) p.GetArrayElementAtIndex(i).stringValue = ids[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ------------------------------------------------------------------ placa da mesa

    // Placa de mesa em "V" invertido (prisma): base escura, duas faces claras inclinadas com o nome dos dois lados
    // (lida por quem senta e por quem chega). Toon como o resto do escritório.
    private static GameObject CriarPlaca()
    {
        Material baseMat = Toon("M_Placa_Base", new Color(0.16f, 0.17f, 0.22f), 0.6f);
        Material face = Toon("M_Placa_Face", new Color(0.96f, 0.94f, 0.88f), 0.6f);
        Material faixa = Toon("M_Placa_Faixa", new Color(0.16f, 0.45f, 0.86f), 0f);
        Font fonte = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var root = new GameObject("Placa_Mesa");
        var plate = root.AddComponent<StationNamePlate>();
        Peca(root.transform, "Base", EquipamentosToonGerador.RoundedBox("Placa_Base", new Vector3(0.24f, 0.01f, 0.075f), 0.003f), baseMat, new Vector3(0f, 0.005f, 0f), Quaternion.identity);
        var nomes = new List<TextMesh>(); var setores = new List<TextMesh>();
        foreach (float lado in new[] { 1f, -1f })
        {
            var pivo = new GameObject(lado > 0 ? "Frente" : "Verso").transform;
            pivo.SetParent(root.transform, false);
            // cada face desce da cumeeira (no alto, no meio) para fora, a 62° do tampo; +Y local = lado de fora
            pivo.localPosition = new Vector3(0f, 0.065f, 0f);
            pivo.localRotation = Quaternion.Euler(62f, lado > 0 ? 0f : 180f, 0f);
            Peca(pivo, "Face", EquipamentosToonGerador.RoundedBox("Placa_Face", new Vector3(0.23f, 0.004f, 0.062f), 0.002f), face, new Vector3(0f, 0f, 0.031f), Quaternion.identity);
            Peca(pivo, "Faixa", EquipamentosToonGerador.RoundedBox("Placa_Faixa", new Vector3(0.23f, 0.0045f, 0.01f), 0.001f), faixa, new Vector3(0f, 0f, 0.057f), Quaternion.identity);
            TextMesh n = Texto(pivo, "Nome", fonte, 0.0038f, new Color(0.13f, 0.11f, 0.16f), new Vector3(0f, 0.0028f, 0.032f), FontStyle.Bold);
            TextMesh d = Texto(pivo, "Setor", fonte, 0.0022f, new Color(0.35f, 0.36f, 0.42f), new Vector3(0f, 0.0028f, 0.013f), FontStyle.Normal);
            nomes.Add(n); setores.Add(d);
        }
        var so = new SerializedObject(plate);
        SetArray(so.FindProperty("nameTexts"), nomes);
        SetArray(so.FindProperty("departmentTexts"), setores);
        so.ApplyModifiedPropertiesWithoutUndo();
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PlacaPath);
        Object.DestroyImmediate(root);
        return saved;
    }

    private static void SetArray(SerializedProperty p, List<TextMesh> v)
    {
        p.arraySize = v.Count;
        for (int i = 0; i < v.Count; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = v[i];
    }

    private static TextMesh Texto(Transform pai, string nome, Font fonte, float tamanho, Color cor, Vector3 pos, FontStyle estilo)
    {
        var go = new GameObject(nome);
        go.transform.SetParent(pai, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(90f, 180f, 0f);   // deitado na face, para fora (+Y), em pé para a cumeeira (-Z)
        var t = go.AddComponent<TextMesh>();
        t.font = fonte;
        t.fontSize = 64;
        t.characterSize = tamanho;
        t.anchor = TextAnchor.MiddleCenter;
        t.alignment = TextAlignment.Center;
        t.fontStyle = estilo;
        t.color = cor;
        t.text = nome == "Nome" ? "Funcionário" : "SETOR";
        go.GetComponent<MeshRenderer>().sharedMaterial = TextoMat(fonte);
        return t;
    }

    // Material do texto com teste de profundidade (o da fonte, GUI/Text, desenha por cima de tudo: o nome do verso vazava).
    private static Material TextoMat(Font fonte)
    {
        string path = Mats + "/M_Placa_Texto.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader sh = Shader.Find("Unlit/Transparent");   // com ZTest normal; a textura da fonte (Alpha8) sai em tinta escura
        if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
        m.shader = sh;
        m.mainTexture = fonte.material.mainTexture;
        EditorUtility.SetDirty(m);
        return m;
    }

    private static void ColocarPlaca(Transform est, GameObject placa, UnityEngine.SceneManagement.Scene scene, List<string> log)
    {
        Transform existente = est.Find("Placa_Mesa");
        WorkSurface mesa = est.GetComponentInChildren<WorkSurface>(true);
        if (mesa == null) { log.Add($"{est.name}: sem mesa (WorkSurface)"); return; }
        Bounds b = Bounds(mesa.gameObject);
        GameObject go = existente != null ? existente.gameObject : (GameObject)PrefabUtility.InstantiatePrefab(placa, scene);
        go.name = "Placa_Mesa";
        go.transform.SetParent(est, true);

        // na beira da mesa do lado da cadeira, no canto oposto ao computador; virada para a cadeira (e o verso, para fora)
        Transform cadeira = est.Find("Cadeira");
        Vector3 paraCadeira = cadeira != null ? cadeira.position - b.center : -est.forward;
        paraCadeira.y = 0f;
        paraCadeira = paraCadeira.sqrMagnitude > 1e-4f ? paraCadeira.normalized : -est.forward;
        Vector3 lado = Vector3.Cross(Vector3.up, paraCadeira);
        Transform pc = null;
        foreach (Transform t in est.GetComponentsInChildren<Transform>(true))
            if (t.GetComponent<ComputerCase>() != null || t.GetComponent<NotebookEstacao>() != null) { if (t.gameObject.activeInHierarchy) pc = t; }
        float sinal = pc != null && Vector3.Dot(pc.position - b.center, lado) > 0f ? -1f : 1f;
        float meiaFundura = Mathf.Abs(Vector3.Dot(b.extents, new Vector3(Mathf.Abs(paraCadeira.x), 0f, Mathf.Abs(paraCadeira.z))));
        float meiaLargura = Mathf.Abs(Vector3.Dot(b.extents, new Vector3(Mathf.Abs(lado.x), 0f, Mathf.Abs(lado.z))));
        Vector3 pos = b.center + paraCadeira * (meiaFundura - 0.16f) + lado * sinal * (meiaLargura - 0.32f);
        pos.y = TopoMesa(mesa, pos, b.max.y);
        go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(paraCadeira, Vector3.up));
        go.transform.localScale = Vector3.one * 1.3f;   // legível de onde o jogador passa
        go.GetComponent<StationNamePlate>().Refresh();   // o texto já fica salvo na cena (sem esperar o Play)
        foreach (TextMesh t in go.GetComponentsInChildren<TextMesh>(true)) EditorUtility.SetDirty(t);
        log.Add($"placa em {est.name} ({StationEmployee.Of(go.transform)?.employeeName})");
    }

    // Altura do tampo no ponto (raio de cima na mesa); sem acerto, o topo dos renderers.
    private static float TopoMesa(WorkSurface mesa, Vector3 p, float fallback)
    {
        Physics.SyncTransforms();
        float best = float.NegativeInfinity;
        foreach (RaycastHit h in Physics.RaycastAll(new Vector3(p.x, fallback + 1f, p.z), Vector3.down, 3f, ~0, QueryTriggerInteraction.Ignore))
            if (h.collider.transform.IsChildOf(mesa.transform) && h.point.y > best) best = h.point.y;
        return float.IsNegativeInfinity(best) ? fallback : best;
    }

    // ------------------------------------------------------------------ seta

    // Seta toon amarela apontando para baixo (cabeça piramidal + haste), em Resources para o StationLocator achar.
    private static void CriarSeta()
    {
        Material mat = Toon("M_Indicador_Chamado", new Color(1f, 0.74f, 0.12f), 1.2f);
        var root = new GameObject("Indicador_Chamado");
        Peca(root.transform, "Haste", EquipamentosToonGerador.RoundedBox("Indicador_Haste", new Vector3(0.05f, 0.13f, 0.05f), 0.012f), mat, new Vector3(0f, 0.15f, 0f), Quaternion.identity);
        Peca(root.transform, "Ponta", Piramide(), mat, Vector3.zero, Quaternion.identity);
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        PrefabUtility.SaveAsPrefabAsset(root, SetaPath);
        Object.DestroyImmediate(root);
    }

    private static Mesh Piramide()
    {
        const string path = "Assets/Art/Meshes/Indicador_Ponta.asset";
        var m = new Mesh { name = "Indicador_Ponta" };
        float w = 0.075f, h = 0.11f;
        Vector3 tip = Vector3.zero;
        Vector3[] b = { new Vector3(-w, h, -w), new Vector3(w, h, -w), new Vector3(w, h, w), new Vector3(-w, h, w) };
        var v = new List<Vector3>(); var tris = new List<int>();
        for (int i = 0; i < 4; i++) { int s = v.Count; v.Add(tip); v.Add(b[i]); v.Add(b[(i + 1) % 4]); tris.AddRange(new[] { s, s + 1, s + 2 }); }
        int t0 = v.Count; v.AddRange(b); tris.AddRange(new[] { t0, t0 + 2, t0 + 1, t0, t0 + 3, t0 + 2 });
        m.SetVertices(v); m.SetTriangles(tris, 0); m.RecalculateNormals(); m.RecalculateBounds();
        var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (old == null) { AssetDatabase.CreateAsset(m, path); return m; }
        EditorUtility.CopySerialized(m, old);
        return old;
    }

    public static void Captura()
    {
        EditorSceneManager.OpenScene(Cena);
        string dir = System.Environment.GetEnvironmentVariable("CAPTURAS_DIR");
        System.IO.Directory.CreateDirectory(dir);
        int i = 0;
        foreach (StationNamePlate p in Object.FindObjectsByType<StationNamePlate>(FindObjectsSortMode.None))
        {
            if (i++ > 2) break;
            Vector3 alvo = p.transform.position + Vector3.up * 0.03f;
            Vector3 olho = alvo + p.transform.forward * 0.45f + Vector3.up * 0.25f + p.transform.right * 0.15f;
            Shot($"{dir}/placa_{p.transform.parent.name}.png", olho, Quaternion.LookRotation(alvo - olho));
            Vector3 longe = alvo + p.transform.forward * 1.8f + Vector3.up * 0.8f;
            Shot($"{dir}/placa_longe_{p.transform.parent.name}.png", longe, Quaternion.LookRotation(alvo - longe));
        }
        var seta = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(SetaPath));
        seta.transform.position = new Vector3(0f, -50f, 0f);
        Shot($"{dir}/seta.png", new Vector3(0.5f, -49.8f, -0.5f), Quaternion.LookRotation(new Vector3(-0.5f, -0.05f, 0.5f)));
    }

    private static void Shot(string file, Vector3 pos, Quaternion rot)
    {
        var go = new GameObject("cam"); var cam = go.AddComponent<Camera>();
        go.transform.SetPositionAndRotation(pos, rot);
        cam.nearClipPlane = 0.02f; cam.fieldOfView = 50f;
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.4f, 0.45f, 0.5f);
        var rt = new RenderTexture(960, 640, 24); cam.targetTexture = rt; cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(960, 640, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 960, 640), 0, 0); tex.Apply();
        System.IO.File.WriteAllBytes(file, tex.EncodeToPNG());
        RenderTexture.active = null; cam.targetTexture = null; Object.DestroyImmediate(rt); Object.DestroyImmediate(go);
    }

    // ------------------------------------------------------------------ util

    private static Material Toon(string nome, Color cor, float contorno) =>
        (Material)typeof(EquipamentosToonGerador).GetMethod("Toon", BindingFlags.NonPublic | BindingFlags.Static)
            .Invoke(null, new object[] { Mats, nome, cor, contorno });

    private static void Peca(Transform pai, string nome, Mesh mesh, Material mat, Vector3 pos, Quaternion rot)
    {
        var go = new GameObject(nome);
        go.transform.SetParent(pai, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = rot;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
    }

    private static Transform Estacao(string nome)
    {
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.name == nome && t.GetComponentInChildren<ComputerCase>(true) != null) return t;
        return null;
    }

    private static Bounds Bounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>(true);
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        Bounds b = rs[0].bounds;
        foreach (Renderer r in rs) b.Encapsulate(r.bounds);
        return b;
    }
}
