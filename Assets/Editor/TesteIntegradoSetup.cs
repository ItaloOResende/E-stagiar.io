using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Cena de testes integrada no escritório do Matheus: Tools &gt; E-stagiar.io &gt; Teste integrado (4 estações).
///  - Copia <c>Fase_01</c> (arte do Matheus + sistemas + NavMesh + supervisor) para <c>Teste_Integrado.unity</c>, só
///    se ela ainda não existir; a <c>Fase_01</c> não é alterada.
///  - Completa até 4 estações de PC (<c>Estacao_{Esquerda,Direita}_Funcional</c>) em pontos livres do piso (sem
///    colisão com nada e com o pé no NavMesh), com <see cref="NavMeshObstacle"/> recortando o NavMesh para o
///    supervisor contornar. As 4 começam funcionais (cabos e PC ligados pelo <c>PcEstadoInicial</c>).
///  - 1 NPC por estação, de pé ao lado da mesa, olhando para ela (modelos existentes <c>npc2</c>/<c>character2</c>,
///    altura ajustada a ~1,65 m), ligado ao <see cref="PcTicketBridge"/> da estação.
///  - Chamados: fase 3, catálogo <c>TicketsData/Gerados</c> (sem tutorial), finalização explícita pelo jogador,
///    <see cref="TicketCategoryRotation"/> (PC / notebook / impressora / rede), cronômetro e <see cref="ScoreKeeper"/>.
///  - Pontes do notebook, impressora e caixa do cabo de rede.
/// Idempotente: rodar de novo só reaplica (não cria estações/NPCs a mais). Posições novas: conferir no Editor.
/// </summary>
public static class TesteIntegradoSetup
{
    private const string Source = "Assets/Scenes/Fase_01.unity";
    public const string ScenePath = "Assets/Scenes/Teste_Integrado.unity";
    private const string TicketsFolder = "Assets/TicketsData/Gerados";
    private const string Gameplay = "Assets/Prefabs/Gameplay/";
    private const int Stage = 3;
    private const int Stations = 4;
    private const float NpcHeight = 1.65f;

    private static readonly string[] PcTickets =
    {
        "pc-sem-energia", "pc-sem-ram", "pc-sem-hd", "pc-sem-rede", "pc-sem-sistema", "pc-pecas-ausentes",
        "pc-montagem-completa", "pc-sem-video-e-rede", "monitor-sem-imagem"
    };
    // chamados de rede atendidos no PC da estação (cabo da mesa / ligação no rack)
    // o chamado de crimpagem também é da estação do funcionário (a caixa de cabo e o rack são só onde se trabalha)
    private static readonly string[] RedePcTickets = { TestadorCabo.ChamadoCaboEstacao, TestadorCabo.ChamadoRack, TestadorCabo.ChamadoCrimpagem };
    private const string TesterPrefabPath = Gameplay + "Testador_CaboRede.prefab";
    private const string TesterScreenPath = "Assets/Prefabs/UI/UI_Screen_TestadorCabo.prefab";
    private const int DesktopStations = 2;   // estações 1-2 com desktop; 3-4 com notebook (a 3 também com impressora)
    private static readonly string[] NotebookTickets =
        { NotebookTicketBridge.SemRam, NotebookTicketBridge.SemSsd, NotebookTicketBridge.SemSistema, NotebookTicketBridge.SemBoot };
    private static readonly string[] PrinterTickets =
        { ImpressoraTicketBridge.SemPapel, ImpressoraTicketBridge.SemToner, ImpressoraTicketBridge.SemIp, ImpressoraTicketBridge.SemImpressao };
    private static readonly string[] RestockLoose = { "Pickup_RamSodimm", "Pickup_Ssd", "Pickup_Toner", "Pickup_ResmaPapel" };
    private static readonly string[] NpcModels = { "Assets/Art/Models/npc2.fbx", "Assets/Art/Models/character2.fbx" };

    [MenuItem("Tools/E-stagiar.io/Teste integrado (4 estações)")]
    public static void Run()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null && !AssetDatabase.CopyAsset(Source, ScenePath))
        {
            Debug.LogError("TesteIntegradoSetup: não consegui copiar " + Source);
            return;
        }
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var log = new List<string>();

        TicketManager tickets = Object.FindAnyObjectByType<TicketManager>(FindObjectsInactive.Include);
        StageController stage = tickets != null ? tickets.GetComponent<StageController>() : null;
        if (tickets == null || stage == null)
        {
            Debug.LogError("TesteIntegradoSetup: a cena não tem o objeto Chamados (TicketManager + StageController).");
            return;
        }
        ConfigureTickets(tickets, stage, log);
        ScoreKeeper score = tickets.GetComponent<ScoreKeeper>();

        EquipamentosToonGerador.GerarNotebook();   // modelo toon do notebook (atualiza o mesmo prefab)

        // ---- estações
        List<Transform> stations = StationRoots();
        Transform group = Find("Estacoes_Teste")?.transform;
        if (stations.Count < Stations)
        {
            if (group == null) group = new GameObject("Estacoes_Teste").transform;
            Quaternion rot = stations.Count > 0 ? stations[0].rotation : Quaternion.identity;
            foreach (Vector3 p in FreeSpots(stations, rot, Stations - stations.Count))
            {
                bool direita = stations.Count % 2 == 1;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Gameplay + $"Estacao_{(direita ? "Direita" : "Esquerda")}_Funcional.prefab");
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                go.transform.SetParent(group, false);
                go.transform.SetPositionAndRotation(p, rot);
                stations.Add(go.transform);
                AddObstacle(go);
                log.Add($"nova estação em {p:F1}");
            }
            if (stations.Count < Stations) log.Add($"SÓ {stations.Count} estações: não achei espaço livre para mais");
        }

        for (int i = 0; i < stations.Count; i++)
        {
            Transform st = stations[i];
            st.name = "Estacao_" + (i + 1);
            Transform npc = EnsureNpc(st, i, scene);
            if (i >= DesktopStations) { NotebookStation(st, i, tickets, npc, scene, log); continue; }
            ComputerCase pcCase = st.GetComponentInChildren<ComputerCase>(true);
            MakeFunctional(pcCase);
            Bridge<PcTicketBridge>(pcCase.gameObject, tickets, "PC da Estação " + (i + 1), PcTickets.Concat(RedePcTickets).ToArray(), npc,
                b => b.FindProperty("scoreKeeper").objectReferenceValue = score);
            LinkRackPort(st, i + 1, log);
        }
        log.Add($"{stations.Count} estações com NPC ({Mathf.Min(DesktopStations, stations.Count)} com desktop, o resto com notebook)");

        // ---- minigames
        foreach (NotebookEstacao nb in Object.FindObjectsByType<NotebookEstacao>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (StationOf(nb.transform) == null)
            {
                ToonNotebookVisual(nb.gameObject);
                Bridge<NotebookTicketBridge>(nb.gameObject, tickets, "Notebook da bancada de manutenção", NotebookTickets, null, NotebookExtra);
            }
        foreach (ImpressoraEstacao pr in Object.FindObjectsByType<ImpressoraEstacao>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (StationOf(pr.transform) == null)
                Bridge<ImpressoraTicketBridge>(pr.gameObject, tickets, "Impressora compartilhada", PrinterTickets, null, PrinterExtra);
        ConfigureRestock(tickets.gameObject, log);
        GameObject caixa = Find("Caixa_CaboRede");
        if (caixa != null) Bridge<RedeTicketBridge>(caixa, tickets, "Caixa do cabo de rede", new string[0], null, null);   // não é local de chamado
        else log.Add("FALTA Caixa_CaboRede");
        EnsureTester(caixa, scene, log);

        GameObject boss = Find("NPC_Supervisor");
        if (boss != null && !boss.activeSelf) { boss.SetActive(true); log.Add("supervisor ligado"); }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("TesteIntegradoSetup: " + string.Join(" · ", log));
    }

    // ------------------------------------------------------------------ rede

    // Ponto de rede da estação N <-> par N do rack (patch panel N <-> switch N).
    private static void LinkRackPort(Transform station, int port, List<string> log)
    {
        var wall = station.GetComponentInChildren<WallConnectionPoint>(true);
        if (wall == null) { log.Add($"FALTA ponto de rede em {station.name}"); return; }
        var link = wall.GetComponent<PontoDeRedeRack>(); if (link == null) link = wall.gameObject.AddComponent<PontoDeRedeRack>();
        var so = new SerializedObject(link);
        so.FindProperty("portaRack").intValue = port;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Testador de cabos: tela 2D (prefab), objeto 3D (prefab, visual provisório com primitivas) e uma unidade na cena,
    // ao lado da caixa de cabo de rede. Não recria o que já existe.
    private static void EnsureTester(GameObject caixa, UnityEngine.SceneManagement.Scene scene, List<string> log)
    {
        var screen = AssetDatabase.LoadAssetAtPath<UIScreen>(TesterScreenPath);
        if (screen == null)
        {
            var go = new GameObject("UI_Screen_TestadorCabo", typeof(RectTransform));
            var ui = go.AddComponent<UIScreen>();
            var so = new SerializedObject(ui);
            so.FindProperty("screenName").stringValue = "Testador de cabo";
            so.ApplyModifiedPropertiesWithoutUndo();
            go.AddComponent<TestadorCaboScreen>();
            go.SetActive(false);
            screen = PrefabUtility.SaveAsPrefabAsset(go, TesterScreenPath).GetComponent<UIScreen>();
            Object.DestroyImmediate(go);
            log.Add("tela do testador criada");
        }

        var telaTestador = screen.GetComponent<TestadorCaboScreen>();
        if (telaTestador != null)
        {
            var soTela = new SerializedObject(telaTestador);
            soTela.FindProperty("plugueRj45").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/ui_render_plugue_rj45.png");
            soTela.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SavePrefabAsset(screen.gameObject);
        }
        GameObject prefab = EquipamentosToonGerador.GerarTestador(screen);   // modelo toon (atualiza o mesmo prefab)
        log.Add("testador (modelo toon) atualizado");

        if (Object.FindAnyObjectByType<TestadorCaboInteractable>(FindObjectsInactive.Include) != null) return;
        var tester = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        if (caixa != null)
        {
            tester.transform.SetParent(caixa.transform.parent, true);
            tester.transform.position = caixa.transform.position + caixa.transform.right * 0.35f;
            tester.transform.rotation = caixa.transform.rotation;
        }
        log.Add("testador colocado " + (caixa != null ? "ao lado da caixa de cabo" : "na origem (sem Caixa_CaboRede)"));
    }

    // ------------------------------------------------------------------ notebooks e impressoras

    // Estação com notebook: o PC_Escritorio da mesa (gabinete, monitor, teclado, mouse, ponto elétrico) é DESATIVADO
    // (não apagado) e um Notebook_Estacao fica no lugar do monitor. A estação logo depois das de desktop também ganha
    // uma impressora própria. Rodar de novo reaproveita o que já existe.
    private static void NotebookStation(Transform st, int i, TicketManager tickets, Transform npc,
                                        UnityEngine.SceneManagement.Scene scene, List<string> log)
    {
        Transform pc = PcRoot(st);
        Transform anchor = pc != null ? pc : st;
        MonitorDisplay monitor = pc != null ? pc.GetComponentInChildren<MonitorDisplay>(true) : null;
        Bounds area = WorldBounds(monitor != null ? monitor.gameObject : anchor.gameObject);
        if (pc != null && pc.gameObject.activeSelf) pc.gameObject.SetActive(false);

        NotebookEstacao nb = st.GetComponentInChildren<NotebookEstacao>(true);
        if (nb == null)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Gameplay + "Notebook_Estacao.prefab"), scene);
            go.transform.SetParent(st, true);
            go.transform.SetPositionAndRotation(new Vector3(area.center.x, area.min.y, area.center.z), anchor.rotation);
            nb = go.GetComponent<NotebookEstacao>();
            log.Add($"notebook na {st.name}");
        }
        ToonNotebookVisual(nb.gameObject, st.Find("Cadeira") != null ? st.Find("Cadeira") : npc);   // de frente para quem senta
        Bridge<NotebookTicketBridge>(nb.gameObject, tickets, $"Notebook da Estação {i + 1}", NotebookTickets, npc, NotebookExtra);

        if (i != DesktopStations) return;
        ImpressoraEstacao pr = st.GetComponentInChildren<ImpressoraEstacao>(true);
        if (pr == null)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Gameplay + "Impressora_Estacao.prefab"), scene);
            go.transform.SetParent(st, true);
            Vector3 side = anchor.rotation * Vector3.right * (area.extents.x + 0.3f);
            go.transform.SetPositionAndRotation(nb.transform.position + side, anchor.rotation);
            pr = go.GetComponent<ImpressoraEstacao>();
            log.Add($"impressora na {st.name}");
        }
        SnapToSurface(pr.gameObject);
        Bridge<ImpressoraTicketBridge>(pr.gameObject, tickets, $"Impressora da Estação {i + 1}", PrinterTickets, npc, PrinterExtra);
    }

    private static Transform PcRoot(Transform station)
    {
        foreach (Transform t in station.GetComponentsInChildren<Transform>(true))
            if (t.name.StartsWith("PC_Escritorio")) return t;
        return null;
    }

    // Estação (Estacao_N) que contém o objeto; null = fora das estações (bancada, outro cômodo).
    private static Transform StationOf(Transform t)
    {
        for (Transform p = t; p != null; p = p.parent)
            if (p.name.StartsWith("Estacao_")) return p;
        return null;
    }

    // Troca o visual do Notebook_Estacao NESTA cena pelo modelo toon (Prefabs/Props/Notebook_Toon) em TAMANHO REAL,
    // apoiado na superfície embaixo dele (raycast), com a dobradiça do lado onde o visual antigo tinha a tela. O visual
    // antigo fica com renderers e colisores desligados (a mira usava esses colisores, deslocados do modelo novo) e o
    // root ganha um BoxCollider do tamanho do modelo. O prefab Notebook_Estacao não é alterado. Rodar de novo reajusta.
    private static void ToonNotebookVisual(GameObject nb, Transform olharPara = null)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(EquipamentosToonGerador.NotebookPrefabPath);
        if (model == null) model = EquipamentosToonGerador.GerarNotebook();
        Transform old = nb.transform.Find("Visual");
        Transform inst = nb.transform.Find("Visual_Toon");
        if (inst == null)
        {
            inst = ((GameObject)PrefabUtility.InstantiatePrefab(model, nb.scene)).transform;
            inst.name = "Visual_Toon";
            inst.SetParent(nb.transform, false);
        }
        inst.localScale = Vector3.one;

        // de que lado ficava a tela no visual antigo (a dobradiça do modelo novo fica em -Z)
        float giro = 0f;
        Transform tela = old != null ? (old.Find("Display") ?? old.Find("Tampa")) : null;
        Transform baseOld = old != null ? old.Find("Base") : null;
        if (tela != null && baseOld != null)
        {
            Vector3 dir = nb.transform.InverseTransformPoint(tela.position) - nb.transform.InverseTransformPoint(baseOld.position);
            if (new Vector2(dir.x, dir.z).sqrMagnitude > 1e-6f) giro = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg - 180f;
        }
        inst.localRotation = Quaternion.Euler(0f, giro, 0f);

        if (old != null)
        {
            foreach (Renderer r in old.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            foreach (Collider c in old.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        }

        // estação: o notebook vira de frente para o NPC (quem usa a mesa)
        if (olharPara != null)
        {
            Vector3 alvo = olharPara.position - nb.transform.position; alvo.y = 0f;
            Vector3 frente = inst.forward; frente.y = 0f;
            if (alvo.sqrMagnitude > 0.01f && frente.sqrMagnitude > 0.01f)
                nb.transform.rotation = Quaternion.FromToRotation(frente.normalized, alvo.normalized) * nb.transform.rotation;
        }

        // apoia na superfície embaixo (mesa/bancada): a base do modelo (pés) fica na altura do ponto atingido
        float y = SurfaceBelow(nb.transform, nb.transform.position);
        inst.position = new Vector3(nb.transform.position.x, y, nb.transform.position.z);

        // colisores JUSTOS (a mira só pega o notebook visível): um caixa na base, no root (onde ficam os Interactable),
        // e outra na tampa da tela, no próprio objeto da tampa (girada com ela). Uma caixa única cobrindo base + tela
        // aberta incluía todo o ar em cima do teclado e atrás da tela.
        Transform hinge = inst.Find("Dobradica");
        var box = nb.GetComponent<BoxCollider>(); if (box == null) box = nb.AddComponent<BoxCollider>();
        Bounds lb = LocalMeshBounds(nb.transform, inst, hinge);
        box.center = lb.center;
        box.size = lb.size;
        Transform lid = hinge != null ? hinge.Find("Tampa") : null;
        if (lid != null)
        {
            var lidBox = lid.GetComponent<BoxCollider>(); if (lidBox == null) lidBox = lid.gameObject.AddComponent<BoxCollider>();
            Bounds tb = LocalMeshBounds(lid, lid);
            lidBox.center = tb.center;
            lidBox.size = tb.size;
        }
    }

    // Altura da primeira superfície (colisor sólido) embaixo do ponto, ignorando o próprio objeto. Sem nada: a do ponto.
    private static float SurfaceBelow(Transform self, Vector3 point)
    {
        Physics.SyncTransforms();
        RaycastHit[] hits = Physics.RaycastAll(point + Vector3.up * 0.6f, Vector3.down, 2.5f, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit h in hits)
            if (!h.collider.transform.IsChildOf(self)) return h.point.y;
        return point.y;
    }

    // Bounds das malhas de 'what' no espaço local de 'space' (vale para objetos girados).
    private static Bounds LocalMeshBounds(Transform space, Transform what, Transform except = null)
    {
        var b = new Bounds();
        bool any = false;
        foreach (MeshFilter mf in what.GetComponentsInChildren<MeshFilter>(false))
        {
            if (mf.sharedMesh == null || (except != null && mf.transform.IsChildOf(except))) continue;
            Bounds mb = mf.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) * 2 - 1, (i & 2) - 1, (i & 4) / 2 - 1));
                Vector3 l = space.InverseTransformPoint(mf.transform.TransformPoint(c));
                if (!any) { b = new Bounds(l, Vector3.zero); any = true; } else b.Encapsulate(l);
            }
        }
        return b;
    }

    // Apoia um equipamento (ex.: impressora) na superfície embaixo dele, pelo ponto mais baixo dos renderers.
    private static void SnapToSurface(GameObject go)
    {
        Bounds wb = WorldBounds(go);
        float y = SurfaceBelow(go.transform, new Vector3(wb.center.x, wb.max.y, wb.center.z));
        go.transform.position += Vector3.up * (y - wb.min.y);
    }

    private static void NotebookExtra(SerializedObject so)
    {
        so.FindProperty("comecarFuncional").boolValue = true;
        so.FindProperty("ramPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(Gameplay + "Pickup_RamSodimm.prefab");
        so.FindProperty("ssdPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(Gameplay + "Pickup_Ssd.prefab");
    }

    private static void PrinterExtra(SerializedObject so)
    {
        so.FindProperty("comecarFuncional").boolValue = true;
        so.FindProperty("tonerPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(Gameplay + "Pickup_Toner.prefab");
        so.FindProperty("papelPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(Gameplay + "Pickup_ResmaPapel.prefab");
    }

    // Reposição temporária: as gavetas entram sozinhas; as peças soltas de notebook/impressora (fora de gavetas e de
    // equipamentos) viram pontos de reposição no lugar onde estão.
    private static void ConfigureRestock(GameObject host, List<string> log)
    {
        var restock = host.GetComponent<ReposicaoEstoque>(); if (restock == null) restock = host.AddComponent<ReposicaoEstoque>();
        var so = new SerializedObject(restock);
        SerializedProperty list = so.FindProperty("pontosSoltos");
        list.arraySize = 0;
        foreach (PickupInteractable item in Object.FindObjectsByType<PickupInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(item);
            if (source == null || !RestockLoose.Contains(source.gameObject.name)) continue;
            if (item.GetComponentInParent<DrawerContents>(true) != null || item.GetComponentInParent<NotebookEstacao>(true) != null ||
                item.GetComponentInParent<ImpressoraEstacao>(true) != null) continue;
            int k = list.arraySize++;
            SerializedProperty e = list.GetArrayElementAtIndex(k);
            Transform t = item.transform;
            e.FindPropertyRelative("prefab").objectReferenceValue = source;
            e.FindPropertyRelative("pai").objectReferenceValue = t.parent;
            e.FindPropertyRelative("posicao").vector3Value = t.parent != null ? t.localPosition : t.position;
            e.FindPropertyRelative("rotacao").vector3Value = t.parent != null ? t.localEulerAngles : t.eulerAngles;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        log.Add($"reposição de estoque: gavetas + {list.arraySize} peça(s) solta(s)");
    }

    // ------------------------------------------------------------------ chamados
    private static void ConfigureTickets(TicketManager tickets, StageController stage, List<string> log)
    {
        var soStage = new SerializedObject(stage);
        soStage.FindProperty("ticketManager").objectReferenceValue = tickets;
        soStage.FindProperty("stageNumber").intValue = Stage;
        soStage.ApplyModifiedPropertiesWithoutUndo();

        var catalog = AssetDatabase.FindAssets("t:TicketDefinition", new[] { TicketsFolder })
            .Select(g => AssetDatabase.LoadAssetAtPath<TicketDefinition>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(d => d != null && !d.tutorialOnly).OrderBy(d => d.ticketId).ToList();
        var so = new SerializedObject(tickets);
        SerializedProperty list = so.FindProperty("catalog");
        list.arraySize = catalog.Count;
        for (int i = 0; i < catalog.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = catalog[i];
        so.FindProperty("requirePlayerToFinish").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();

        var rotation = tickets.GetComponent<TicketCategoryRotation>(); if (rotation == null) rotation = tickets.gameObject.AddComponent<TicketCategoryRotation>();
        var soRot = new SerializedObject(rotation);
        SerializedProperty groups = soRot.FindProperty("groups");
        var defs = new (string name, string[] ids)[]
        {
            ("PC", PcTickets), ("Notebook", NotebookTickets),
            ("Impressora", PrinterTickets), ("Rede", RedePcTickets)
        };
        groups.arraySize = defs.Length;
        for (int i = 0; i < defs.Length; i++)
        {
            SerializedProperty g = groups.GetArrayElementAtIndex(i);
            g.FindPropertyRelative("name").stringValue = defs[i].name;
            SerializedProperty ids = g.FindPropertyRelative("ticketIds");
            ids.arraySize = defs[i].ids.Length;
            for (int j = 0; j < defs[i].ids.Length; j++) ids.GetArrayElementAtIndex(j).stringValue = defs[i].ids[j];
        }
        soRot.ApplyModifiedPropertiesWithoutUndo();

        var hud = tickets.GetComponent<StageTimerHUD>(); if (hud == null) hud = tickets.gameObject.AddComponent<StageTimerHUD>();
        var soHud = new SerializedObject(hud);
        soHud.FindProperty("stageController").objectReferenceValue = stage;
        soHud.FindProperty("ticketManager").objectReferenceValue = tickets;
        soHud.ApplyModifiedPropertiesWithoutUndo();

        var score = tickets.GetComponent<ScoreKeeper>(); if (score == null) score = tickets.gameObject.AddComponent<ScoreKeeper>();
        var soScore = new SerializedObject(score);
        soScore.FindProperty("ticketManager").objectReferenceValue = tickets;
        soScore.FindProperty("stageController").objectReferenceValue = stage;
        soScore.ApplyModifiedPropertiesWithoutUndo();
        log.Add($"fase {Stage}, {catalog.Count} chamados, rodízio de 4 tipos, finalização pelo jogador");
    }

    // ------------------------------------------------------------------ estações
    private static List<Transform> StationRoots()
    {
        var roots = new List<Transform>();
        // inativos também: nas estações de notebook o PC fica desativado e a estação continua existindo
        foreach (ComputerCase c in Object.FindObjectsByType<ComputerCase>(FindObjectsInactive.Include))
        {
            Transform t = c.transform;
            while (t.parent != null && !t.name.StartsWith("Estacao")) t = t.parent;
            if (t.name.StartsWith("Estacao") && !roots.Contains(t)) roots.Add(t);
        }
        return roots.OrderBy(t => t.position.x).ThenBy(t => t.position.z).ToList();
    }

    private static void MakeFunctional(ComputerCase pcCase)
    {
        var soCase = new SerializedObject(pcCase);
        soCase.FindProperty("randomizeComponents").boolValue = false;
        soCase.FindProperty("fixedComponents").intValue = (int)ComputerCase.AllComponents;
        soCase.FindProperty("startingHddHasOperatingSystem").boolValue = true;
        soCase.ApplyModifiedPropertiesWithoutUndo();
        var estado = pcCase.GetComponent<PcEstadoInicial>();
        if (estado == null) return;
        var so = new SerializedObject(estado);
        so.FindProperty("chanceCaboSolto").floatValue = 0f;
        so.FindProperty("ligarAoIniciar").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AddObstacle(GameObject station)
    {
        Bounds b = WorldBounds(station);
        var obs = station.GetComponent<NavMeshObstacle>(); if (obs == null) obs = station.AddComponent<NavMeshObstacle>();
        obs.shape = NavMeshObstacleShape.Box;
        obs.carving = true;
        obs.center = station.transform.InverseTransformPoint(b.center);
        Vector3 s = station.transform.InverseTransformVector(b.size);
        obs.size = new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
    }

    /// <summary>
    /// Pontos livres para novas estações: grade sobre o NavMesh, perto das estações existentes. Livre = a caixa da
    /// estação (+ folga para andar) não toca nenhum collider (exceto piso) e os 4 cantos estão no NavMesh.
    /// </summary>
    private static List<Vector3> FreeSpots(List<Transform> existing, Quaternion rot, int count)
    {
        var result = new List<Vector3>();
        var probe = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Gameplay + "Estacao_Esquerda_Funcional.prefab"));
        probe.transform.SetPositionAndRotation(Vector3.zero, rot);
        Bounds local = WorldBounds(probe);
        Object.DestroyImmediate(probe);
        Vector3 half = local.extents + new Vector3(0.6f, 0f, 0.6f);   // folga para o jogador e o supervisor passarem
        half.y = 0.7f;

        NavMeshTriangulation tri = NavMesh.CalculateTriangulation();
        if (tri.vertices.Length == 0) return result;
        Bounds nav = new Bounds(tri.vertices[0], Vector3.zero);
        foreach (Vector3 v in tri.vertices) nav.Encapsulate(v);
        Vector3 centre = existing.Count > 0 ? existing.Aggregate(Vector3.zero, (a, t) => a + t.position) / existing.Count : nav.center;
        var route = Find("Ronda_Supervisor");
        var avoid = new List<Vector3>();
        if (route != null) foreach (Transform p in route.transform) avoid.Add(p.position);
        foreach (Transform t in existing) avoid.Add(t.position);
        var player = Find("Player");
        if (player != null) avoid.Add(player.transform.position);

        Physics.SyncTransforms();
        var candidates = new List<Vector3>();
        for (float x = nav.min.x; x <= nav.max.x; x += 0.5f)
            for (float z = nav.min.z; z <= nav.max.z; z += 0.5f)
                candidates.Add(new Vector3(x, nav.min.y, z));
        foreach (Vector3 c0 in candidates.OrderBy(c => (c - centre).sqrMagnitude))
        {
            if (result.Count >= count) break;
            if (!NavMesh.SamplePosition(c0, out NavMeshHit hit, 0.3f, NavMesh.AllAreas)) continue;
            Vector3 c = hit.position;
            Vector3 centerW = c + rot * local.center; centerW.y = c.y + 0.75f;
            if (avoid.Any(a => Vector3.Distance(new Vector3(a.x, 0, a.z), new Vector3(centerW.x, 0, centerW.z)) < 2.2f)) continue;
            if (result.Any(r => Vector3.Distance(r, c) < local.size.magnitude + 1f)) continue;
            bool blocked = Physics.OverlapBox(centerW, half, rot, ~0, QueryTriggerInteraction.Ignore)
                .Any(col => col.bounds.size.y > 0.15f && col.bounds.max.y > c.y + 0.1f);
            if (blocked) continue;
            bool onNav = true;
            foreach (Vector3 corner in new[] { new Vector3(-1, 0, -1), new Vector3(1, 0, -1), new Vector3(-1, 0, 1), new Vector3(1, 0, 1) })
            {
                Vector3 w = centerW + rot * Vector3.Scale(corner, new Vector3(half.x, 0, half.z));
                if (!NavMesh.SamplePosition(new Vector3(w.x, c.y, w.z), out _, 0.25f, NavMesh.AllAreas)) { onNav = false; break; }
            }
            if (onNav) { result.Add(c); avoid.Add(c); }
        }
        return result;
    }

    // ------------------------------------------------------------------ NPCs
    private static Transform EnsureNpc(Transform station, int index, UnityEngine.SceneManagement.Scene scene)
    {
        string name = "NPC_Estacao_" + (index + 1);
        GameObject npc = Find(name);
        if (npc != null) return npc.transform;
        npc = new GameObject(name);
        SceneManager_Move(npc, scene);
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(NpcModels[index % NpcModels.Length]);
        var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
        visual.name = "Visual";
        visual.transform.SetParent(npc.transform, false);
        Bounds vb = WorldBounds(visual);
        if (vb.size.y > 0.01f) visual.transform.localScale *= NpcHeight / vb.size.y;
        vb = WorldBounds(visual);
        visual.transform.position += new Vector3(-vb.center.x, -vb.min.y, -vb.center.z);   // pé no chão, centrado na raiz

        // de pé ao lado da mesa, do lado oposto ao gabinete, à altura da cadeira; olhando para a mesa
        ComputerCase pcCase = station.GetComponentInChildren<ComputerCase>(true);
        float caseSide = Mathf.Sign(station.InverseTransformPoint(pcCase.transform.position).x);
        Vector3 local = new Vector3(-caseSide * 1.45f, 0f, 0.55f);
        npc.transform.position = station.TransformPoint(local);
        Vector3 look = station.TransformPoint(new Vector3(0f, 0f, 0f)) - npc.transform.position;
        look.y = 0f;
        npc.transform.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
        npc.transform.SetParent(station, true);

        var obs = npc.AddComponent<NavMeshObstacle>();
        obs.shape = NavMeshObstacleShape.Capsule;
        obs.radius = 0.3f;
        obs.height = NpcHeight;
        obs.center = new Vector3(0f, NpcHeight / 2f, 0f);
        obs.carving = true;
        return npc.transform;
    }

    private static void SceneManager_Move(GameObject go, UnityEngine.SceneManagement.Scene scene) =>
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);

    // ------------------------------------------------------------------ pontes
    private static void Minigame<TStation, TBridge>(TicketManager tickets, string label, string ticketId, List<string> log)
        where TStation : Component where TBridge : TicketDeviceBridge
    {
        TStation station = Object.FindAnyObjectByType<TStation>(FindObjectsInactive.Include);
        if (station == null) { log.Add("FALTA " + typeof(TStation).Name); return; }
        Bridge<TBridge>(station.gameObject, tickets, label, new[] { ticketId }, null, null);
    }

    private static void Bridge<T>(GameObject go, TicketManager tickets, string label, string[] ids, Transform npc,
                                  System.Action<SerializedObject> extra) where T : TicketDeviceBridge
    {
        T bridge = go.GetComponent<T>(); if (bridge == null) bridge = go.AddComponent<T>();
        var so = new SerializedObject(bridge);
        so.FindProperty("ticketManager").objectReferenceValue = tickets;
        so.FindProperty("deviceLabel").stringValue = label;
        so.FindProperty("stationNpc").objectReferenceValue = npc;
        SerializedProperty acc = so.FindProperty("acceptedTicketIds");
        acc.arraySize = ids.Length;
        for (int i = 0; i < ids.Length; i++) acc.GetArrayElementAtIndex(i).stringValue = ids[i];
        extra?.Invoke(so);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ------------------------------------------------------------------ util
    private static Bounds WorldBounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>(true);
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        Bounds b = rs[0].bounds;
        foreach (Renderer r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    private static GameObject Find(string name)
    {
        foreach (GameObject root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name == name) return root;
            Transform t = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == name);
            if (t != null) return t.gameObject;
        }
        return null;
    }
}
