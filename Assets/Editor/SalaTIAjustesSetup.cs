using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Ajustes da sala de TI (etapa 1) na <c>Office_Placeholder</c>, por cima do <see cref="MobiliarioEscritorioSetup"/>:
///  1. Armário e estante saem do Toon Shader (o <c>M_Escritorio_Claro</c> usa "Toon 3D as 2D", que estoura de brilho
///     nesta cena) para materiais URP/Lit próprios com as MESMAS cores (<c>Materials/Mobiliario/M_Mobiliario_*</c>).
///     Os materiais do Matheus não são alterados (mesas, gaveteiros e divisórias continuam com eles).
///  2. Segunda bancada (<c>Props/Bancada_Armarios</c>: armários e gavetas embaixo) na parede norte, com uma cadeira.
///  3. Rack de rede (<c>Gameplay/RackInternet</c>) dentro da sala e peças de parede (<c>Props/Quadro_Seguranca</c>,
///     <c>Quadro_Rede</c>, <c>Calendario_TI</c>, <c>Aviso_Manutencao</c>) + placa externa <c>Props/Placa_SalaTI</c>.
/// Na cena só mexe no grupo <c>Sala_TI_Complementos</c> (recriado a cada execução); o grupo <c>Sala_TI</c> e o
/// resto da cena ficam como estão. Prefabs só são criados se não existirem; os materiais dos dois móveis são
/// trocados sempre (idempotente).
///
/// Menu: Tools > E-stagiar.io > Escritório: ajustes da sala de TI. Batch: -executeMethod SalaTIAjustesSetup.Run
/// Relatório: Logs/SalaTIAjustes.txt.
/// </summary>
public static class SalaTIAjustesSetup
{
    private const string Cena = "Assets/Scenes/Office_Placeholder.unity";
    private const string Models = "Assets/Art/Models/";
    private const string Props = "Assets/Prefabs/Props/";
    private const string Gameplay = "Assets/Prefabs/Gameplay/";
    private const string MatMob = "Assets/Art/Materials/Mobiliario/";
    private const string MatQuadros = "Assets/Art/Materials/Quadros/";
    private const string TexQuadros = "Assets/Art/Textures/Quadros/";
    private const string Grupo = "Sala_TI_Complementos";
    private const string Relatorio = "Logs/SalaTIAjustes.txt";

    // giro extra do rack para a frente (painel) ficar para o centro da sala; ajustado pela captura
    private const float RackGiro = 0f;

    private static readonly List<string> log = new List<string>();

    [MenuItem("Tools/E-stagiar.io/Escritório: ajustes da sala de TI")]
    public static void Menu() => Executar();

    public static void Run()
    {
        Executar();
        EditorApplication.Exit(0);
    }

    private static void Executar()
    {
        log.Clear();
        AssetDatabase.Refresh();
        Directory.CreateDirectory(MatMob);
        Directory.CreateDirectory(MatQuadros);

        // 1) materiais URP/Lit com as cores dos materiais toon do Matheus
        var claro = Lit("M_Mobiliario_Claro", "Assets/Art/Materials/M_Escritorio_Claro.mat", 0.15f);
        var escuro = Lit("M_Mobiliario_Escuro", "Assets/Art/Materials/M_Escritorio_Escuro.mat", 0.10f);
        var metal = Lit("M_Mobiliario_Metal", "Assets/Art/Materials/M_PC_Chapa.mat", 0.35f);
        var troca = new Dictionary<string, Material>
        {
            { "M_Escritorio_Claro", claro }, { "M_Escritorio_Escuro", escuro }, { "M_PC_Chapa", metal },
            { "Claro", claro }, { "Escuro", escuro }, { "Metal", metal },
        };
        foreach (string p in new[] { "Armario_TI", "Estante_Estoque" }) TrocarMateriais(Props + p + ".prefab", troca);

        // 2-3) prefabs novos
        var slots = new Dictionary<string, Material>(troca);
        GameObject bancada2 = Prefab("Bancada_Armarios", "bancada_armarios", slots, null, go =>
        {
            var b = go.AddComponent<BoxCollider>(); b.center = new Vector3(0, 0.45f, 0); b.size = new Vector3(1.8f, 0.9f, 0.6f);
            go.AddComponent<WorkSurface>();
        });
        GameObject qSeg = Prefab("Quadro_Seguranca", "quadro_retrato", slots, Imagem("cartaz_seguranca"), null);
        GameObject qRede = Prefab("Quadro_Rede", "quadro_paisagem", slots, Imagem("quadro_rede"), null);
        GameObject cal = Prefab("Calendario_TI", "calendario", slots, Imagem("calendario"), null);
        GameObject aviso = Prefab("Aviso_Manutencao", "placa", slots, Imagem("aviso_bancada"), null);
        GameObject placa = Prefab("Placa_SalaTI", "placa", slots, Imagem("placa_sala_ti"), null);

        var scene = EditorSceneManager.OpenScene(Cena);
        var old = scene.GetRootGameObjects().FirstOrDefault(r => r.name == Grupo);
        if (old != null) Object.DestroyImmediate(old);
        Physics.SyncTransforms();
        Cena_(scene, bancada2, qSeg, qRede, cal, aviso, placa);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        File.WriteAllText(Relatorio, string.Join("\n", log));
        Debug.Log("[Sala de TI] " + string.Join(" · ", log));
    }

    // ------------------------------------------------------------------ materiais

    private static Material Lit(string nome, string origem, float smooth)
    {
        string path = MatMob + nome + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
            log.Add($"{nome}: criado (URP/Lit)");
        }
        var src = AssetDatabase.LoadAssetAtPath<Material>(origem);
        Color c = src != null && src.HasProperty("_BaseColor") ? src.GetColor("_BaseColor") : Color.gray;
        c.a = 1f;
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Smoothness", smooth);
        m.SetFloat("_Metallic", 0f);
        m.SetFloat("_EnvironmentReflections", 0f);    // sem reflexo do céu (o "brilho" que incomodava)
        m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        m.SetFloat("_SpecularHighlights", 1f);
        EditorUtility.SetDirty(m);
        return m;
    }

    private static Material Imagem(string tex)
    {
        string path = MatQuadros + "M_Quadro_" + tex + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
        }
        var imp = (TextureImporter)AssetImporter.GetAtPath(TexQuadros + tex + ".png");
        if (imp != null && (imp.wrapMode != TextureWrapMode.Clamp || imp.maxTextureSize < 2048))
        {
            imp.wrapMode = TextureWrapMode.Clamp; imp.maxTextureSize = 2048; imp.SaveAndReimport();
        }
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TexQuadros + tex + ".png"));
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Smoothness", 0.1f);
        m.SetFloat("_EnvironmentReflections", 0f);
        m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        EditorUtility.SetDirty(m);
        return m;
    }

    private static void TrocarMateriais(string prefabPath, Dictionary<string, Material> troca)
    {
        var root = PrefabUtility.LoadPrefabContents(prefabPath);
        int n = 0;
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            var ms = r.sharedMaterials;
            for (int i = 0; i < ms.Length; i++)
                if (ms[i] != null && troca.TryGetValue(ms[i].name, out var novo) && ms[i] != novo) { ms[i] = novo; n++; }
            r.sharedMaterials = ms;
        }
        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        PrefabUtility.UnloadPrefabContents(root);
        log.Add($"{Path.GetFileNameWithoutExtension(prefabPath)}: {n} material(is) trocado(s) para URP/Lit");
    }

    // ------------------------------------------------------------------ prefabs

    private static GameObject Prefab(string nome, string fbx, Dictionary<string, Material> slots, Material imagem,
                                     System.Action<GameObject> extra)
    {
        string path = Props + nome + ".prefab";
        var existente = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existente != null) { log.Add($"{nome}: prefab já existia (mantido)"); return existente; }
        var modelo = AssetDatabase.LoadAssetAtPath<GameObject>(Models + fbx + ".fbx");
        if (modelo == null) { log.Add($"FALTA {Models}{fbx}.fbx"); return null; }
        var root = new GameObject(nome);
        var vis = (GameObject)PrefabUtility.InstantiatePrefab(modelo);
        vis.name = "Visual";
        vis.transform.SetParent(root.transform, false);
        foreach (var r in vis.GetComponentsInChildren<MeshRenderer>())
        {
            var ms = r.sharedMaterials;
            for (int i = 0; i < ms.Length; i++)
            {
                string s = ms[i] != null ? ms[i].name : "";
                if (s == "Imagem" && imagem != null) ms[i] = imagem;
                else if (slots.TryGetValue(s, out var m)) ms[i] = m;
                else log.Add($"{nome}: slot '{s}' sem material");
            }
            r.sharedMaterials = ms;
        }
        extra?.Invoke(root);
        var pref = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        log.Add($"{nome}: prefab criado");
        return pref;
    }

    // ------------------------------------------------------------------ cena

    private static void Cena_(UnityEngine.SceneManagement.Scene scene, GameObject bancada2, GameObject qSeg, GameObject qRede,
                              GameObject cal, GameObject aviso, GameObject placa)
    {
        // limites da sala pelas paredes (como no MobiliarioEscritorioSetup)
        Vector3 c = new Vector3(35.3f, 1.0f, 14.6f);
        float xW = Raio(c, Vector3.left).point.x, xE = Raio(c, Vector3.right).point.x, zN = Raio(c, Vector3.forward).point.z;
        float zS = Raio(new Vector3(32.0f, 1.0f, 14.6f), Vector3.back).point.z;
        log.Add($"sala: x {xW:0.00}..{xE:0.00}, z {zS:0.00}..{zN:0.00}");

        var g = new GameObject(Grupo);
        var moveis = Filho(g, "Moveis"); var parede = Filho(g, "Parede"); var externo = Filho(g, "Externo");
        Quaternion sul = Quaternion.Euler(0, 180, 0), norte = Quaternion.identity, leste = Quaternion.Euler(0, 90, 0), oeste = Quaternion.Euler(0, -90, 0);

        // parede norte, lado oeste (entre o armário e o gaveteiro de hardware): bancada com armários + cadeira
        var b2 = Inst(bancada2, moveis, "Bancada_Armarios", new Vector3(32.85f, 0f, Raio(new Vector3(32.85f, 1.0f, 14.6f), Vector3.forward).point.z - 0.30f - 0.015f), sul);
        Inst(AssetDatabase.LoadAssetAtPath<GameObject>(Props + "OF_Cadeira.prefab"), moveis, "Cadeira_Bancada", new Vector3(32.55f, 0f, zN - 1.10f), sul * Quaternion.Euler(0, 180, 0) * Quaternion.Euler(0, 12, 0));
        if (b2 != null)
        {
            Transform t = b2.transform;
            Inst(AssetDatabase.LoadAssetAtPath<GameObject>(Props + "OF_Monitor.prefab"), moveis, "Monitor_Testes", t.TransformPoint(0.15f, 0.90f, -0.15f), t.rotation);
            Inst(AssetDatabase.LoadAssetAtPath<GameObject>(Props + "OF_Teclado.prefab"), moveis, "Teclado_Testes", t.TransformPoint(0.15f, 0.90f, 0.15f), t.rotation);
        }

        // rack de rede: parede sul, a leste da porta, de frente para a sala
        var rack = Inst(AssetDatabase.LoadAssetAtPath<GameObject>(Gameplay + "RackInternet.prefab"), moveis, "RackInternet",
            new Vector3(38.3f, 0f, zS + 0.30f), norte * Quaternion.Euler(0, RackGiro, 0));

        // parede (a origem das peças é o fundo: vai no ponto da parede, olhando para dentro da sala)
        NaParede(qSeg, parede, "Quadro_Seguranca", new Vector3(33.40f, 1.62f, zN - 0.5f), Vector3.forward, sul);
        NaParede(aviso, parede, "Aviso_Manutencao", new Vector3(35.9f, 2.05f, zN - 0.5f), Vector3.forward, sul);
        NaParede(qRede, parede, "Quadro_Rede", new Vector3(39.35f, 1.55f, zS + 0.5f), Vector3.back, norte);
        NaParede(cal, parede, "Calendario_TI", new Vector3(xW + 0.5f, 1.50f, 14.2f), Vector3.left, leste);
        // placa externa: face da parede sul voltada para o salão, à direita da porta
        NaParede(placa, externo, "Placa_SalaTI", new Vector3(37.75f, 1.75f, zS - 1.0f), Vector3.forward, sul);

        Physics.SyncTransforms();
        Validar(g, rack);
    }

    private static RaycastHit Raio(Vector3 de, Vector3 dir)
    {
        Physics.SyncTransforms();
        // só as paredes do prédio (grupo MAPA do Matheus): móveis e objetos na frente não contam
        var hits = Physics.RaycastAll(de, dir, 15f, ~0, QueryTriggerInteraction.Ignore).OrderBy(x => x.distance);
        foreach (var hp in hits)
            if (hp.collider.transform.root.name == "MAPA") return hp;
        RaycastHit h = default;
        log.Add($"ATENÇÃO: sem parede a partir de {de} na direção {dir}");
        h.point = de + dir * 3f; h.normal = -dir;
        return h;
    }

    private static void NaParede(GameObject prefab, GameObject pai, string nome, Vector3 perto, Vector3 dirParede, Quaternion rot)
    {
        var h = Raio(perto, dirParede);
        var go = Inst(prefab, pai, nome, new Vector3(h.point.x, perto.y, h.point.z) - dirParede * 0.002f, rot);
        if (go != null) log.Add($"{nome}: na parede {Caminho(h.collider != null ? h.collider.transform : null)} em ({h.point.x:0.00}; {perto.y:0.00}; {h.point.z:0.00})");
    }

    private static GameObject Filho(GameObject root, string nome)
    {
        var go = new GameObject(nome);
        go.transform.SetParent(root.transform, false);
        return go;
    }

    private static GameObject Inst(GameObject prefab, GameObject pai, string nome, Vector3 pos, Quaternion rot)
    {
        if (prefab == null) { log.Add("FALTA prefab para " + nome); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, pai.scene);
        go.name = nome;
        go.transform.SetParent(pai.transform, false);
        go.transform.SetPositionAndRotation(pos, rot);
        LigarTelasDaCena(go);
        return go;
    }

    // mesma regra do MobiliarioEscritorioSetup: na cena, a tela 2D é a da própria cena
    private static void LigarTelasDaCena(GameObject go)
    {
        var telas = Object.FindObjectsByType<UIScreen>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(s => s.gameObject.scene == go.scene).ToList();
        foreach (var comp in go.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (comp == null) continue;
            var f = comp.GetType().GetField("screen");
            if (f == null || f.FieldType != typeof(UIScreen)) continue;
            var atual = (UIScreen)f.GetValue(comp);
            if (atual == null || !EditorUtility.IsPersistent(atual)) continue;
            var daCena = telas.FirstOrDefault(s => PrefabUtility.GetCorrespondingObjectFromSource(s) == atual)
                         ?? telas.FirstOrDefault(s => s.name == atual.name);
            if (daCena == null) { log.Add($"{go.name}: tela {atual.name} não existe na cena"); continue; }
            f.SetValue(comp, daCena);
            PrefabUtility.RecordPrefabInstancePropertyModifications(comp);
            log.Add($"{go.name}: {comp.GetType().Name} -> {daCena.name} (cena)");
        }
    }

    private static string Caminho(Transform t) => t == null ? "-" : t.parent ? Caminho(t.parent) + "/" + t.name : t.name;

    // ------------------------------------------------------------------ validação

    private static void Validar(GameObject g, GameObject rack)
    {
        int problemas = 0;
        var solidos = g.GetComponentsInChildren<Collider>().Where(c => c.enabled && !c.isTrigger).ToList();
        foreach (var c in solidos)
            foreach (var o in Physics.OverlapBox(c.bounds.center, c.bounds.extents * 0.98f, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
            {
                if (o == c || o.transform.IsChildOf(g.transform) && Raiz(o.transform, g) == Raiz(c.transform, g)) continue;
                if (o.attachedRigidbody != null || o.transform.root.name == "Player") continue;
                if (Physics.ComputePenetration(c, c.transform.position, c.transform.rotation, o, o.transform.position, o.transform.rotation, out _, out float d) && d > 0.015f)
                { log.Add($"INVADE: {Caminho(c.transform)} x {Caminho(o.transform)} ({d * 100:0.0} cm)"); problemas++; }
            }
        // apoio dos móveis no chão / sobre a bancada
        foreach (Transform t in g.transform.Find("Moveis"))
        {
            var rs = t.GetComponentsInChildren<Renderer>();
            Bounds b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            float sup = float.NegativeInfinity;
            foreach (var h in Physics.RaycastAll(new Vector3(b.center.x, b.min.y + 0.05f, b.center.z), Vector3.down, 2f, ~0, QueryTriggerInteraction.Ignore))
                if (!h.collider.transform.IsChildOf(t)) sup = Mathf.Max(sup, h.point.y);
            float gap = b.min.y - sup;
            bool ok = gap > -0.02f && gap < 0.02f;
            log.Add($"apoio: {t.name} base {b.min.y:0.000}, apoio {sup:0.000}{(ok ? "" : "  <-- VERIFICAR")}");
            if (!ok) problemas++;
        }
        // peças de parede: o fundo encosta na parede (até 1 cm) e nada fica na frente delas
        foreach (Transform par in new[] { g.transform.Find("Parede"), g.transform.Find("Externo") })
            foreach (Transform t in par)
            {
                Vector3 frente = t.forward;
                bool encosta = Physics.Raycast(t.position + frente * 0.01f, -frente, out RaycastHit h, 0.03f, ~0, QueryTriggerInteraction.Ignore);
                var rs = t.GetComponentsInChildren<Renderer>();
                Bounds b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                var na = Physics.OverlapBox(b.center + frente * 0.06f, b.extents + Vector3.one * 0.01f, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)
                    .Where(o => !o.transform.IsChildOf(t) && o != h.collider).Select(o => Caminho(o.transform)).ToList();
                log.Add($"parede: {t.name} {(encosta ? "encostada" : "SEM PAREDE ATRÁS")}{(na.Count > 0 ? "; na frente: " + string.Join(", ", na) : "")}");
                if (!encosta || na.Count > 0) problemas++;
            }
        // acesso: ponto em frente ao rack e à bancada nova livre para a cápsula do jogador
        foreach (var (nome, p) in new[] { ("rack", rack != null ? rack.transform.position + rack.transform.forward * 0.8f : Vector3.zero),
                                         ("bancada nova", g.transform.Find("Moveis/Bancada_Armarios").position + g.transform.Find("Moveis/Bancada_Armarios").forward * 1.7f) })
        {
            bool livre = !Physics.CheckCapsule(p + Vector3.up * 0.35f, p + Vector3.up * 1.4f, 0.30f, ~0, QueryTriggerInteraction.Ignore);
            log.Add($"acesso: frente do(a) {nome} ({p.x:0.00}; {p.z:0.00}) {(livre ? "livre" : "BLOQUEADA")}");
            if (!livre) problemas++;
        }
        log.Add($"VALIDAÇÃO: {problemas} problema(s)");
    }

    private static Transform Raiz(Transform t, GameObject g)
    {
        while (t.parent != null && t.parent.parent != g.transform && t.parent != g.transform) t = t.parent;
        return t;
    }
}
