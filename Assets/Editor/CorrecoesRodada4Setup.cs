using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Rodada 4 (2026-10-17): valores de cena (volume do suspense do relógio, limite de 1 estrela em 20%), ponto de retirada
/// da estação nova sobre a bancada da sala de TI (<see cref="AssemblyPickupPoint"/>) e a cor própria do cabo do alicate de
/// crimpar (diferente da chave de fenda). Idempotente; não mexe em shader nem nas configurações de render.
/// </summary>
public static class CorrecoesRodada4Setup
{
    private const string Cena = "Assets/Scenes/Teste_Integrado.unity";
    private const string MatVermelho = "Assets/Art/Materials/Componentes/M_Comp_Vermelho.mat";
    private const string MatAlicate = "Assets/Art/Materials/Componentes/M_Comp_CaboAlicate.mat";
    private const string Alicate = "Assets/Prefabs/Gameplay/Pickup_Alicate.prefab";

    [MenuItem("Tools/E-stagiar.io/Correções 2026-10-17 (4): estrelas, suspense, retirada da estação nova, cabo do alicate")]
    public static void Tudo()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var log = new List<string>();
        CaboDoAlicate(log);
        var scene = EditorSceneManager.OpenScene(Cena);
        ValoresDeCena(log);
        PontoDeRetirada(log);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[Rodada4] " + string.Join(" · ", log));
    }

    // ------------------------------------------------------------------ alicate

    // O alicate e a chave de fenda usavam o mesmo cabo vermelho (M_Comp_Vermelho). O alicate passa a ter cabo AZUL
    // (cópia do material, mesmo shader, só a cor muda); a chave continua vermelha.
    private static void CaboDoAlicate(List<string> log)
    {
        var vermelho = AssetDatabase.LoadAssetAtPath<Material>(MatVermelho);
        var azul = AssetDatabase.LoadAssetAtPath<Material>(MatAlicate);
        if (azul == null)
        {
            azul = new Material(vermelho) { name = "M_Comp_CaboAlicate" };
            AssetDatabase.CreateAsset(azul, MatAlicate);
        }
        var cor = new Color(0.16f, 0.42f, 0.86f, 1f);
        if (azul.HasProperty("_BaseColor")) azul.SetColor("_BaseColor", cor);
        if (azul.HasProperty("_Color")) azul.SetColor("_Color", cor);
        EditorUtility.SetDirty(azul);

        GameObject root = PrefabUtility.LoadPrefabContents(Alicate);
        int trocados = 0;
        try
        {
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                Material[] mats = r.sharedMaterials;
                bool mudou = false;
                for (int i = 0; i < mats.Length; i++)
                    if (mats[i] == vermelho) { mats[i] = azul; mudou = true; trocados++; }
                if (mudou) r.sharedMaterials = mats;
            }
            PrefabUtility.SaveAsPrefabAsset(root, Alicate);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        log.Add($"alicate: {trocados} material(is) do cabo trocados para azul (chave de fenda segue vermelha)");
    }

    // ------------------------------------------------------------------ valores

    private static void ValoresDeCena(List<string> log)
    {
        foreach (ScoreKeeper k in Object.FindObjectsByType<ScoreKeeper>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var so = new SerializedObject(k);
            SerializedProperty t = so.FindProperty("starPercentThresholds");
            if (t.arraySize > 0) t.GetArrayElementAtIndex(0).floatValue = 20f;
            so.ApplyModifiedPropertiesWithoutUndo();
            log.Add($"estrelas: 1ª a partir de 20% em {k.name}");
        }
        foreach (StageTimerHUD h in Object.FindObjectsByType<StageTimerHUD>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var so = new SerializedObject(h);
            so.FindProperty("warningVolume").floatValue = 0.4f;
            so.FindProperty("finalVolume").floatValue = 0.8f;
            so.ApplyModifiedPropertiesWithoutUndo();
            log.Add("suspense do relógio: 0,40 / 0,80");
        }
    }

    // ------------------------------------------------------------------ retirada da estação nova

    // A bancada da sala de TI = a superfície de trabalho FORA das estações mais perto dos gaveteiros. O ponto fica no
    // maior trecho livre do tampo, perto da borda da frente; os itens se alinham ao longo dele.
    private static void PontoDeRetirada(List<string> log)
    {
        AssemblyPickupPoint existente = Object.FindAnyObjectByType<AssemblyPickupPoint>(FindObjectsInactive.Include);
        DrawerContents gaveta = Object.FindAnyObjectByType<DrawerContents>();
        WorkSurface bancada = null;
        float melhor = float.MaxValue;
        foreach (WorkSurface w in Object.FindObjectsByType<WorkSurface>(FindObjectsSortMode.None))
        {
            if (w.GetComponentInParent<StationEmployee>() != null) continue;
            float d = gaveta != null ? (w.transform.position - gaveta.transform.position).sqrMagnitude : 0f;
            if (d < melhor) { melhor = d; bancada = w; }
        }
        if (bancada == null) { log.Add("FALTA bancada (WorkSurface fora das estações)"); return; }

        Bounds b = Limites(bancada.gameObject);
        bool eixoX = b.extents.x >= b.extents.z;
        Vector3 ao = eixoX ? Vector3.right : Vector3.forward;   // comprimento
        Vector3 fundo = eixoX ? Vector3.forward : Vector3.right;
        float comp = eixoX ? b.size.x : b.size.z, prof = eixoX ? b.size.z : b.size.x;
        // frente = o lado da profundidade mais longe da parede (o mais perto do centro da sala de teste)
        Vector3 centroSala = Vector3.zero;
        foreach (StationEmployee e in Object.FindObjectsByType<StationEmployee>(FindObjectsSortMode.None)) centroSala += e.transform.position;
        int ne = Object.FindObjectsByType<StationEmployee>(FindObjectsSortMode.None).Length;
        if (ne > 0) centroSala /= ne;
        if (Vector3.Dot(centroSala - b.center, fundo) < 0f) fundo = -fundo;   // "fundo" agora aponta para a FRENTE

        Physics.SyncTransforms();
        float topo = Topo(bancada, b.center);
        // varre o comprimento procurando o maior trecho livre (nada em cima do tampo)
        const float passo = 0.05f;
        int n = Mathf.FloorToInt((comp - 0.1f) / passo);
        int ini = 0, melhorIni = 0, melhorLen = 0;
        for (int i = 0; i <= n; i++)
        {
            Vector3 p = b.center + ao * (-comp * 0.5f + 0.05f + i * passo);
            bool livre = i < n && Livre(bancada, new Vector3(p.x, topo + 0.16f, p.z), ao, fundo, prof);
            if (!livre)
            {
                if (i - ini > melhorLen) { melhorLen = i - ini; melhorIni = ini; }
                ini = i + 1;
            }
        }
        float largura = melhorLen * passo;
        Vector3 centro = b.center + ao * (-comp * 0.5f + 0.05f + (melhorIni + melhorLen * 0.5f) * passo) + fundo * (prof * 0.18f);
        centro.y = topo;

        GameObject go = existente != null ? existente.gameObject : new GameObject("Retirada_EstacaoNova");
        if (existente == null) go.AddComponent<AssemblyPickupPoint>();
        go.transform.SetParent(bancada.transform.parent, true);
        // eixo X do ponto = comprimento livre; Z = para a frente (as fileiras extras vão para o fundo, -Z)
        go.transform.SetPositionAndRotation(centro, Quaternion.LookRotation(fundo, Vector3.up));
        var so = new SerializedObject(go.GetComponent<AssemblyPickupPoint>());
        so.FindProperty("usableWidth").floatValue = Mathf.Max(0.6f, largura - 0.1f);
        so.FindProperty("rowSpacing").floatValue = Mathf.Clamp(prof * 0.36f, 0.22f, 0.4f);
        so.ApplyModifiedPropertiesWithoutUndo();
        log.Add($"retirada da estação nova sobre {bancada.name} (tampo y={topo:0.000}, trecho livre {largura:0.00} m de {comp:0.00} m, prof. {prof:0.00} m)");
    }

    private static bool Livre(WorkSurface mesa, Vector3 c, Vector3 ao, Vector3 fundo, float prof)
    {
        Quaternion r = Quaternion.LookRotation(fundo, Vector3.up);
        foreach (Collider col in Physics.OverlapBox(c, new Vector3(0.025f, 0.13f, prof * 0.4f), r, ~0, QueryTriggerInteraction.Ignore))
            if (!col.transform.IsChildOf(mesa.transform)) return false;
        return true;
    }

    private static float Topo(WorkSurface mesa, Vector3 p)
    {
        float best = float.NegativeInfinity;
        foreach (RaycastHit h in Physics.RaycastAll(new Vector3(p.x, p.y + 2f, p.z), Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore))
            if (h.collider.transform.IsChildOf(mesa.transform) && h.point.y > best) best = h.point.y;
        return float.IsNegativeInfinity(best) ? Limites(mesa.gameObject).max.y : best;
    }

    private static Bounds Limites(GameObject go)
    {
        bool tem = false; Bounds b = default;
        foreach (Collider c in go.GetComponentsInChildren<Collider>())
        {
            if (!tem) { b = c.bounds; tem = true; } else b.Encapsulate(c.bounds);
        }
        return b;
    }

    // ------------------------------------------------------------------ capturas (diagnóstico; CAPTURAS_DIR)

    public static void Captura()
    {
        EditorSceneManager.OpenScene(Cena);
        string dir = System.Environment.GetEnvironmentVariable("CAPTURAS_DIR");
        System.IO.Directory.CreateDirectory(dir);
        AssemblyPickupPoint p = Object.FindAnyObjectByType<AssemblyPickupPoint>();
        if (p == null) { Debug.LogError("[Rodada4] sem ponto de retirada"); return; }
        Vector3 c = p.transform.position;
        Shot($"{dir}/retirada_topo.png", c + Vector3.up * 1.8f, Quaternion.Euler(90f, p.transform.eulerAngles.y, 0f), true);
        Vector3 olho = c + p.transform.forward * 1.6f + Vector3.up * 0.9f;
        Shot($"{dir}/retirada_frente.png", olho, Quaternion.LookRotation(c - olho), false);
    }

    private static void Shot(string file, Vector3 pos, Quaternion rot, bool orto)
    {
        var go = new GameObject("cam"); var cam = go.AddComponent<Camera>();
        go.transform.SetPositionAndRotation(pos, rot);
        cam.orthographic = orto; cam.orthographicSize = 1.1f; cam.nearClipPlane = 0.02f; cam.fieldOfView = 55f;
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.4f, 0.45f, 0.5f);
        var rt = new RenderTexture(960, 640, 24); cam.targetTexture = rt; cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(960, 640, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 960, 640), 0, 0); tex.Apply();
        System.IO.File.WriteAllBytes(file, tex.EncodeToPNG());
        RenderTexture.active = null; cam.targetTexture = null; Object.DestroyImmediate(rt); Object.DestroyImmediate(go);
    }
}
