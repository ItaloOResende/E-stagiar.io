using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Correções de gameplay de 2026-10-16 (ferramentas pela empunhadura, notebook pegável, estações da cena de teste).
/// Cada passo é idempotente e pode ser rodado pelo menu ou em batchmode (-executeMethod).
/// </summary>
public static class CorrecoesGameplaySetup
{
    private const string Cena = "Assets/Scenes/Teste_Integrado.unity";
    private const string Gameplay = "Assets/Prefabs/Gameplay/";
    private const string NavAsset = "Assets/Scenes/Teste_Integrado_NavMesh.asset";

    [MenuItem("Tools/E-stagiar.io/Correções 2026-10-16 (ferramentas, notebook, estações)")]
    public static void Tudo()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Empunhaduras();
        NotebookPegavel();
        EstacoesTesteIntegrado();
    }

    // ------------------------------------------------------------------ 1. ferramentas pela empunhadura

    /// <summary>
    /// Chave de fenda, alicate e pendrive: a mão segura a EMPUNHADURA (cabo / corpo do pendrive), com a ponta
    /// funcional para a frente. O ponto de pega é medido na malha: a extremidade mais GROSSA ao longo do maior eixo
    /// (cabo da chave, cabos do alicate, corpo do pendrive) e o centro dos 35% do comprimento a partir dela.
    /// </summary>
    public static void Empunhaduras()
    {
        Empunhadura("Pickup_Ferramenta", new Vector3(-18f, -8f, 0f));
        Empunhadura("Pickup_Alicate", new Vector3(-18f, -8f, 0f));
        Empunhadura("Pickup_Pendrive", new Vector3(-25f, -6f, 0f));
        AssetDatabase.SaveAssets();
    }

    private static void Empunhadura(string prefab, Vector3 pose)
    {
        string path = Gameplay + prefab + ".prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var pickup = root.GetComponent<PickupInteractable>();
            if (pickup == null) { Debug.LogError($"[Empunhadura] {prefab} sem PickupInteractable"); return; }
            var pts = new System.Collections.Generic.List<Vector3>();
            foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var r = mf.GetComponent<Renderer>();
                if (mf.sharedMesh == null || r == null || !r.enabled || mf.name.Contains("Quebrad")) continue;
                foreach (Vector3 v in mf.sharedMesh.vertices) pts.Add(root.transform.InverseTransformPoint(mf.transform.TransformPoint(v)));
            }
            if (pts.Count == 0) { Debug.LogError($"[Empunhadura] {prefab} sem malha"); return; }

            Bounds b = new Bounds(pts[0], Vector3.zero);
            foreach (Vector3 p in pts) b.Encapsulate(p);
            int ax = b.size.x >= b.size.y && b.size.x >= b.size.z ? 0 : b.size.y >= b.size.z ? 1 : 2;
            float min = b.min[ax], len = b.size[ax];
            float Grossura(float from, float to)
            {
                bool any = false; Bounds s = default;
                foreach (Vector3 p in pts)
                {
                    float t = (p[ax] - min) / len;
                    if (t < from || t > to) continue;
                    if (!any) { s = new Bounds(p, Vector3.zero); any = true; } else s.Encapsulate(p);
                }
                if (!any) return 0f;
                Vector3 sz = s.size; sz[ax] = 0f;
                return sz.x * sz.y + sz.y * sz.z + sz.x * sz.z;   // área da seção (o eixo longo zerado)
            }
            bool cabaNoMin = Grossura(0f, 0.2f) >= Grossura(0.8f, 1f);
            float a0 = cabaNoMin ? 0f : 0.65f, a1 = cabaNoMin ? 0.35f : 1f;
            bool any2 = false; Bounds band = default;
            foreach (Vector3 p in pts)
            {
                float t = (p[ax] - min) / len;
                if (t < a0 || t > a1) continue;
                if (!any2) { band = new Bounds(p, Vector3.zero); any2 = true; } else band.Encapsulate(p);
            }
            Vector3 grip = band.center;
            Vector3 tip = Vector3.zero; tip[ax] = cabaNoMin ? 1f : -1f;   // da empunhadura para a ponta funcional
            Vector3 up = Mathf.Abs(Vector3.Dot(tip, Vector3.up)) > 0.9f ? Vector3.forward : Vector3.up;
            Quaternion euler = Quaternion.Euler(pose) * Quaternion.Inverse(Quaternion.LookRotation(tip, up));

            var so = new SerializedObject(pickup);
            so.FindProperty("useGripPoint").boolValue = true;
            so.FindProperty("gripLocalPoint").vector3Value = grip;
            so.FindProperty("holdEulerOffset").vector3Value = euler.eulerAngles;
            so.FindProperty("holdPositionOffset").vector3Value = Vector3.zero;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Debug.Log($"[Empunhadura] {prefab}: eixo {"XYZ"[ax]}, comprimento {len:0.000} m, pega em {grip:F3} " +
                      $"(lado {(cabaNoMin ? "-" : "+")}{"XYZ"[ax]}), ponta para {tip}, rotação {euler.eulerAngles:F0}");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // ------------------------------------------------------------------ 2. notebook pegável

    /// <summary>
    /// Notebook_Estacao: clique ESQUERDO pega e carrega (PickupInteractable + Rigidbody), clique DIREITO abre a tela
    /// (UIScreenInteractable, como antes). O RedeEstacaoInteractable (que abria a tela no clique esquerdo e impedia
    /// pegar) fica desligado, não removido.
    /// </summary>
    public static void NotebookPegavel()
    {
        string path = Gameplay + "Notebook_Estacao.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var body = root.GetComponent<Rigidbody>(); if (body == null) body = root.AddComponent<Rigidbody>();
            body.mass = 2f;
            body.interpolation = RigidbodyInterpolation.None;
            body.isKinematic = true;
            var pickup = root.GetComponent<PickupInteractable>(); if (pickup == null) pickup = root.AddComponent<PickupInteractable>();
            var so = new SerializedObject(pickup);
            so.FindProperty("interactionMessage").stringValue = "Pegar";
            so.FindProperty("holdPositionOffset").vector3Value = new Vector3(-0.05f, -0.06f, 0.12f);
            so.FindProperty("holdEulerOffset").vector3Value = new Vector3(10f, 0f, 0f);
            so.FindProperty("holdScale").floatValue = 0.8f;
            so.FindProperty("placedYawOffset").floatValue = 180f;   // colocado de frente para o jogador
            so.ApplyModifiedPropertiesWithoutUndo();
            foreach (var c in root.GetComponents<RedeEstacaoInteractable>()) c.enabled = false;
            var screen = root.GetComponent<UIScreenInteractable>();
            if (screen != null) screen.interactionMessage = "Manutenção";
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Debug.Log("[Notebook] Notebook_Estacao: Esq. = pegar, Dir. = manutenção");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // ------------------------------------------------------------------ 3. estações da cena Teste_Integrado

    /// <summary>
    /// 3 desktops + 3 notebooks, cada um numa estação (mesa, cadeira, NPC):
    ///  - Estacao_1: desktop (sem mudança); Estacao_2 (desktop que estava sozinho longe do escritório, z ≈ 21) vai para
    ///    o lado da Estacao_1, de costas para ela pela divisória (como as estações 3 e 4);
    ///  - Estacao_3: volta a ser desktop (o PC é reativado, o notebook da mesa é desativado; a impressora fica);
    ///  - Estacao_4 e Estacao_5: notebooks (sem mudança);
    ///  - Estacao_6 (nova, ao lado da 4): recebe o notebook avulso que flutuava no meio da sala.
    /// Mesas que flutuavam (estações 3 e 4, 11–18 cm acima do piso) voltam ao chão. O NavMesh é assado de novo num asset
    /// próprio da cena (o da Fase_01 não muda). Rodar de novo não cria nada a mais.
    /// </summary>
    public static void EstacoesTesteIntegrado()
    {
        var scene = EditorSceneManager.OpenScene(Cena);
        var log = new System.Collections.Generic.List<string>();
        TicketManager tickets = Object.FindAnyObjectByType<TicketManager>(FindObjectsInactive.Include);
        ScoreKeeper score = tickets != null ? tickets.GetComponent<ScoreKeeper>() : null;
        Transform grupo = GameObject.Find("Estacoes_Teste")?.transform;
        Transform e1 = Estacao("Estacao_1"), e2 = Estacao("Estacao_2"), e3 = Estacao("Estacao_3"), e4 = Estacao("Estacao_4");
        if (tickets == null || grupo == null || e1 == null || e2 == null || e3 == null || e4 == null)
        {
            Debug.LogError("[Estações] cena fora do esperado (falta TicketManager, Estacoes_Teste ou Estacao_1..4)");
            return;
        }

        // mesas no chão
        foreach (Transform e in new[] { e3, e4 }) NoChao(e, log);

        // Estação 2: ao lado da 1, do outro lado da divisória (mesmo afastamento das estações 3 e 4)
        float passo = Mathf.Abs(e3.position.z - e4.position.z);
        Vector3 alvo2 = new Vector3(e1.position.x, 0f, e1.position.z + passo);
        if (Vector3.Distance(e2.position, alvo2) > 0.05f)
        {
            e2.SetPositionAndRotation(alvo2, e1.rotation * Quaternion.Euler(0f, 180f, 0f));
            log.Add($"Estacao_2 movida para {alvo2:F2}");
        }
        NoChao(e2, log);
        Chamar("AddObstacle", e2.gameObject);
        // o NPC da estação 2 acompanha a mesa (se ele tinha ficado no lugar antigo, volta para o lado dela)
        Transform npc2 = (Transform)Chamar("EnsureNpc", e2, 1, scene);
        if (npc2 != null && !npc2.IsChildOf(e2)) { npc2.SetParent(e2, true); log.Add("NPC_Estacao_2 preso à Estacao_2"); }
        if (npc2 != null && Vector3.Distance(npc2.position, e2.position) > 1.8f)
        {
            // mesmo lugar que o TesteIntegradoSetup.EnsureNpc usa: ao lado da mesa, do lado oposto ao gabinete
            ComputerCase c2 = e2.GetComponentInChildren<ComputerCase>(true);
            float lado = Mathf.Sign(e2.InverseTransformPoint(c2.transform.position).x);
            npc2.position = e2.TransformPoint(new Vector3(-lado * 1.45f, 0f, 0.55f));
            Vector3 olhar = e2.position - npc2.position; olhar.y = 0f;
            npc2.rotation = Quaternion.LookRotation(olhar.normalized, Vector3.up);
            log.Add("NPC_Estacao_2 ao lado da mesa");
        }

        // Estação 3: desktop de novo
        Transform pc3 = PcRoot(e3);
        if (pc3 != null && !pc3.gameObject.activeSelf) { pc3.gameObject.SetActive(true); log.Add("PC da Estacao_3 reativado"); }
        NotebookEstacao nb3 = e3.GetComponentInChildren<NotebookEstacao>(true);
        if (nb3 != null && nb3.gameObject.activeSelf) { nb3.gameObject.SetActive(false); log.Add("notebook da Estacao_3 desativado"); }
        ComputerCase case3 = pc3 != null ? pc3.GetComponentInChildren<ComputerCase>(true) : null;
        if (case3 != null)
        {
            Transform npc3 = (Transform)Chamar("EnsureNpc", e3, 2, scene);
            string[] pcIds = Campo<string[]>("PcTickets").Concat(Campo<string[]>("RedePcTickets")).ToArray();
            Ponte<PcTicketBridge>(case3.gameObject, tickets, "PC da Estação 3", pcIds, npc3,
                so => so.FindProperty("scoreKeeper").objectReferenceValue = score);
            Chamar("LinkRackPort", e3, 3, log);
        }

        // Estação 6: nova, ao lado da 4 (mesma rotação), com o notebook avulso
        Transform e6 = Estacao("Estacao_6");
        if (e6 == null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Gameplay + "Estacao_Direita_Funcional.prefab");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            e6 = go.transform;
            e6.SetParent(grupo, false);
            e6.name = "Estacao_6";
            log.Add("Estacao_6 criada");
        }
        // 4 m à direita da 4: o NPC (ao lado da mesa) fica fora da divisória das estações 3/4
        Vector3 alvo6 = new Vector3(e4.position.x + 4f, 0f, e4.position.z);
        if (Vector3.Distance(e6.position, alvo6) > 0.05f) { e6.SetPositionAndRotation(alvo6, e4.rotation); log.Add($"Estacao_6 em {alvo6:F2}"); }
        NoChao(e6, log);
        Physics.SyncTransforms();
        Bounds b6 = WorldBounds(e6.gameObject);
        foreach (Collider c in Physics.OverlapBox(b6.center + Vector3.up * 0.05f, b6.extents * 0.95f, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
            if (!c.transform.IsChildOf(e6) && c.bounds.size.y > 0.15f && c.bounds.max.y > 0.1f)
                log.Add($"ATENÇÃO: Estacao_6 encosta em {c.name}");
        Chamar("AddObstacle", e6.gameObject);
        Transform pc6 = PcRoot(e6);
        Transform npc6 = (Transform)Chamar("EnsureNpc", e6, 5, scene);
        MonitorDisplay mon6 = pc6 != null ? pc6.GetComponentInChildren<MonitorDisplay>(true) : null;
        Bounds area = WorldBounds(mon6 != null ? mon6.gameObject : e6.gameObject);
        if (pc6 != null && pc6.gameObject.activeSelf) pc6.gameObject.SetActive(false);

        NotebookEstacao nbAvulso = null;
        foreach (NotebookEstacao nb in Object.FindObjectsByType<NotebookEstacao>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (nb.transform.parent == null || nb.transform.IsChildOf(e6)) { nbAvulso = nb; break; }
        if (nbAvulso != null)
        {
            if (!nbAvulso.transform.IsChildOf(e6))
            {
                nbAvulso.transform.SetParent(e6, true);
                nbAvulso.transform.SetPositionAndRotation(new Vector3(area.center.x, area.min.y, area.center.z), (pc6 != null ? pc6 : e6).rotation);
                log.Add("notebook avulso levado para a Estacao_6");
            }
            Transform cadeira = e6.Find("Cadeira");
            Chamar("ToonNotebookVisual", nbAvulso.gameObject, cadeira != null ? cadeira : npc6);
            Ponte<NotebookTicketBridge>(nbAvulso.gameObject, tickets, "Notebook da Estação 6", Campo<string[]>("NotebookTickets"), npc6,
                so => Chamar("NotebookExtra", so));
        }
        else log.Add("FALTA notebook avulso (nada levado para a Estacao_6)");

        // contagem final
        int pcs = 0, nbs = 0;
        foreach (var c in Object.FindObjectsByType<ComputerPower>(FindObjectsSortMode.None)) if (c.isActiveAndEnabled) pcs++;
        foreach (var n in Object.FindObjectsByType<NotebookEstacao>(FindObjectsSortMode.None)) if (n.isActiveAndEnabled) nbs++;
        log.Add($"{pcs} desktops e {nbs} notebooks ativos");

        AssarNavMesh(log);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Estações] " + string.Join(" · ", log));
    }

    public static void DiagPiso()
    {
        EditorSceneManager.OpenScene(Cena);
        var chao = GameObject.Find("Chao_Fase01");
        if (chao != null) Debug.Log($"[Piso] Chao_Fase01 bounds {chao.GetComponent<Collider>().bounds}");
        foreach (string n in new[] { "Estacao_1", "Estacao_2", "Estacao_3", "Estacao_4", "Estacao_5", "Estacao_6" })
        {
            Transform e = Estacao(n);
            if (e == null) continue;
            foreach (RaycastHit h in Physics.RaycastAll(new Vector3(e.position.x, 0.6f, e.position.z), Vector3.down, 3f, ~0, QueryTriggerInteraction.Ignore))
                Debug.Log($"[Piso] {n} (renderer {h.collider.GetComponent<Renderer>() != null}) y={e.position.y:0.000}: {Path(h.collider.transform)} em {h.point.y:0.000} (altura {h.collider.bounds.size.y:0.00})");
        }
    }

    // Apoia a estação no piso: maior ponto de chão (collider largo e baixo) embaixo do centro dela.
    private static void NoChao(Transform e, System.Collections.Generic.List<string> log)
    {
        Physics.SyncTransforms();
        float best = float.NegativeInfinity;
        string piso = "";
        foreach (RaycastHit h in Physics.RaycastAll(new Vector3(e.position.x, 0.6f, e.position.z), Vector3.down, 3f, ~0, QueryTriggerInteraction.Ignore))
        {
            // piso VISÍVEL (o colisor invisível Chao_Fase01 fica 15 cm acima do chão desenhado)
            var r = h.collider.GetComponent<Renderer>();
            if (h.collider.transform.IsChildOf(e) || r == null || !r.enabled || h.point.y > 0.4f || h.point.y <= best) continue;
            best = h.point.y; piso = Path(h.collider.transform);
        }
        if (float.IsNegativeInfinity(best)) best = 0f;
        if (Mathf.Abs(e.position.y - best) < 0.005f) return;
        log.Add($"{e.name}: y {e.position.y:0.000} → {best:0.000} (no piso: {piso})");
        e.position = new Vector3(e.position.x, best, e.position.z);
    }

    private static void AssarNavMesh(System.Collections.Generic.List<string> log)
    {
        var navGo = GameObject.Find("NavMesh_Escritorio");
        var surface = navGo != null ? navGo.GetComponent<Unity.AI.Navigation.NavMeshSurface>() : null;
        if (surface == null) { log.Add("FALTA NavMesh_Escritorio"); return; }
        Physics.SyncTransforms();
        surface.BuildNavMesh();
        if (surface.navMeshData == null) { log.Add("NavMesh saiu vazio"); return; }
        if (AssetDatabase.LoadAssetAtPath<UnityEngine.AI.NavMeshData>(NavAsset) != null) AssetDatabase.DeleteAsset(NavAsset);
        AssetDatabase.CreateAsset(surface.navMeshData, NavAsset);
        EditorUtility.SetDirty(surface);
        log.Add("NavMesh assado em " + NavAsset);
    }

    private static Transform Estacao(string nome)
    {
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.name == nome && t.GetComponentInChildren<ComputerCase>(true) != null) return t;
        return null;
    }

    private static Transform PcRoot(Transform station)
    {
        foreach (Transform t in station.GetComponentsInChildren<Transform>(true))
            if (t.name.StartsWith("PC_Escritorio")) return t;
        return null;
    }

    private static Bounds WorldBounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>(true);
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        Bounds b = rs[0].bounds;
        foreach (Renderer r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    // Reaproveita a montagem do TesteIntegradoSetup (NPC, pontes, visual do notebook) sem duplicar código.
    private const System.Reflection.BindingFlags Priv = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;

    private static object Chamar(string metodo, params object[] args) =>
        typeof(TesteIntegradoSetup).GetMethod(metodo, Priv).Invoke(null, args);

    private static T Campo<T>(string nome) => (T)typeof(TesteIntegradoSetup).GetField(nome, Priv).GetValue(null);

    private static void Ponte<T>(GameObject go, TicketManager tickets, string label, string[] ids, Transform npc,
                                 System.Action<SerializedObject> extra) where T : TicketDeviceBridge =>
        typeof(TesteIntegradoSetup).GetMethod("Bridge", Priv).MakeGenericMethod(typeof(T))
            .Invoke(null, new object[] { go, tickets, label, ids, npc, extra });

    /// <summary>Diagnóstico: lista equipamentos (PC, notebook, impressora) da cena de teste com posição e estado.</summary>
    public static void Diagnostico()
    {
        EditorSceneManager.OpenScene(Cena);
        var sb = new StringBuilder("[Diag] equipamentos da cena:\n");
        foreach (var c in Object.FindObjectsByType<ComputerPower>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            sb.AppendLine($"PC {Path(c.transform)} ativo={c.gameObject.activeInHierarchy} pos={c.transform.position}");
        foreach (var n in Object.FindObjectsByType<NotebookEstacao>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            sb.AppendLine($"NB {Path(n.transform)} ativo={n.gameObject.activeInHierarchy} pos={n.transform.position}");
        foreach (var n in Object.FindObjectsByType<ImpressoraEstacao>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            sb.AppendLine($"IMP {Path(n.transform)} ativo={n.gameObject.activeInHierarchy} pos={n.transform.position}");
        foreach (var n in Object.FindObjectsByType<TicketDeviceBridge>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            sb.AppendLine($"BRIDGE {n.GetType().Name} {Path(n.transform)} ativo={n.isActiveAndEnabled}");
        foreach (var n in Object.FindObjectsByType<WorkSurface>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            sb.AppendLine($"MESA {Path(n.transform)} ativo={n.gameObject.activeInHierarchy} pos={n.transform.position}");
        foreach (GameObject r in EditorSceneManager.GetActiveScene().GetRootGameObjects())
        {
            sb.AppendLine($"ROOT {r.name} ativo={r.activeSelf} pos={r.transform.position}");
            foreach (Transform ch in r.transform)
                sb.AppendLine($"   - {ch.name} ativo={ch.gameObject.activeSelf} pos={ch.position}");
        }
        Debug.Log(sb.ToString());
    }

    /// <summary>Diagnóstico visual: capturas da cena de teste (planta e vistas dos gaveteiros) na pasta Temp.</summary>
    public static void Capturas()
    {
        EditorSceneManager.OpenScene(Cena);
        string dir = System.Environment.GetEnvironmentVariable("CAPTURAS_DIR") ?? System.IO.Path.GetFullPath("Logs/Capturas");
        System.IO.Directory.CreateDirectory(dir);
        // teto/paredes altos atrapalham a planta: só a câmera ortográfica com corte abaixo de 2,4 m
        Shot(dir + "/planta.png", new Vector3(22f, 2.3f, 9f), Quaternion.Euler(90f, 0f, 0f), true, 16f, 0.05f);
        Shot(dir + "/planta_sala.png", new Vector3(23f, 2.3f, 1.5f), Quaternion.Euler(90f, 0f, 0f), true, 5f, 0.05f);
        foreach (string g in new[] { "Gaveteiro_Hardware", "Gaveteiro_Cabos", "Gaveteiro_Suprimentos" })
        {
            GameObject o = GameObject.Find(g);
            if (o == null) continue;
            Vector3 front = -o.transform.forward;   // a plaquinha fica do lado -Z local (ver posições)
            Vector3 p = o.transform.position + Vector3.up * 0.7f;
            Shot(dir + "/" + g + ".png", p + front * 1.6f + Vector3.up * 0.2f, Quaternion.LookRotation(-front), false, 0f, 0.05f);
            Shot(dir + "/" + g + "_tras.png", p - front * 1.6f + Vector3.up * 0.2f, Quaternion.LookRotation(front), false, 0f, 0.05f);
        }
        foreach (var c in Object.FindObjectsByType<ComputerPower>(FindObjectsSortMode.None))
            ShotAround(dir, "pc_" + c.transform.parent.parent.name, c.transform.position);
        foreach (var n in Object.FindObjectsByType<NotebookEstacao>(FindObjectsSortMode.None))
            ShotAround(dir, "nb_" + (n.transform.parent != null ? n.transform.parent.name : "raiz"), n.transform.position);
        Debug.Log("[Diag] capturas em " + dir);
    }

    /// <summary>Vista "Aberto" do notebook (mesma direção da NotebookTela), para conferir o interior.</summary>
    public static void CapturaNotebook()
    {
        string dir = System.Environment.GetEnvironmentVariable("CAPTURAS_DIR") ?? System.IO.Path.GetFullPath("Logs/Capturas");
        System.IO.Directory.CreateDirectory(dir);
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects);
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(EquipamentosToonGerador.NotebookPrefabPath);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
        foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
            if (t.name == "TampaInferior") t.gameObject.SetActive(false);
        Transform baseT = null;
        foreach (Transform t in go.GetComponentsInChildren<Transform>(true)) if (t.name == "Base") baseT = t;
        Bounds b = WorldBounds(baseT != null ? baseT.gameObject : go);
        Vector3 olhar = new Vector3(0f, 1f, -0.95f).normalized, cima = Vector3.back;
        foreach (var (nome, o, c) in new[] { ("nb_aberto", new Vector3(0f, -1f, -0.95f).normalized, Vector3.back), ("nb_baixo", Vector3.down, Vector3.back) })
        {
            var camGo = new GameObject("cam");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = Mathf.Max(b.extents.x / 1.6f, b.extents.z) * 1.15f;
            camGo.transform.SetPositionAndRotation(b.center + o * 1f, Quaternion.LookRotation(-o, c));
            cam.nearClipPlane = 0.01f; cam.farClipPlane = 3f;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.86f, 0.9f, 0.92f);
            var rt = new RenderTexture(1600, 1000, 24);
            cam.targetTexture = rt; cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1600, 1000, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0); tex.Apply();
            System.IO.File.WriteAllBytes($"{dir}/{nome}.png", tex.EncodeToPNG());
            RenderTexture.active = null; cam.targetTexture = null;
            Object.DestroyImmediate(rt); Object.DestroyImmediate(camGo);
        }
    }

    private static void ShotAround(string dir, string name, Vector3 target)
    {
        Vector3 eye = target + new Vector3(-1.6f, 1.0f, -1.6f);
        Shot($"{dir}/{name}.png", eye, Quaternion.LookRotation(target - eye), false, 0f, 0.05f);
    }

    private static void Shot(string file, Vector3 pos, Quaternion rot, bool ortho, float size, float near)
    {
        var go = new GameObject("CameraCaptura");
        var cam = go.AddComponent<Camera>();
        go.transform.SetPositionAndRotation(pos, rot);
        cam.orthographic = ortho;
        cam.orthographicSize = size;
        cam.nearClipPlane = near;
        cam.farClipPlane = ortho ? 3f : 80f;
        cam.fieldOfView = 55f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.2f, 0.2f, 0.25f);
        var rt = new RenderTexture(1280, 960, 24);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(1280, 960, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1280, 960), 0, 0);
        tex.Apply();
        System.IO.File.WriteAllBytes(file, tex.EncodeToPNG());
        RenderTexture.active = null;
        cam.targetTexture = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(tex);
        Object.DestroyImmediate(go);
    }

    private static string Path(Transform t)
    {
        string p = t.name;
        for (Transform x = t.parent; x != null; x = x.parent) p = x.name + "/" + p;
        return p;
    }
}
