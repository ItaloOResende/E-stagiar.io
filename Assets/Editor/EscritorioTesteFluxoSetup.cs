using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Prepara a <c>Escritorio_Teste</c> para jogar o fluxo integrado (chamados da Fernanda + equipamentos + minigames +
/// supervisor + pontuação). Menu: Tools &gt; E-stagiar.io &gt; Escritório de teste: fluxo integrado de chamados.
///  - <c>Chamados</c>: <see cref="StageController"/> na fase 3 (fila com até 3 chamados abertos e chegada
///    adaptativa; todos os chamados do catálogo valem a partir da 3) e catálogo = todos os chamados de
///    <c>TicketsData/Gerados</c> que não são de tutorial. Cronômetro (<see cref="StageTimerHUD"/>) e
///    <see cref="ScoreKeeper"/> ligados (valores de pontuação NÃO são tocados).
///  - Cada PC das estações: começa funcional (sem sorteio de peças nem cabo solto; o <c>PcEstadoInicial</c> liga tudo)
///    e recebe um <see cref="PcTicketBridge"/> (o defeito vem do chamado).
///  - <c>Notebook_Estacao</c>, <c>Impressora_Estacao</c> e <c>Caixa_CaboRede</c> recebem as pontes dos minigames.
///  - <c>NPC_Supervisor</c> ligado (vinha desligado) com rota e jogador preenchidos se estiverem vazios.
/// Idempotente: rodar de novo só reaplica. Não mexe em posições, arte, outras cenas nem configurações do projeto.
/// </summary>
public static class EscritorioTesteFluxoSetup
{
    private const string ScenePath = "Assets/Scenes/Escritorio_Teste.unity";
    private const string TicketsFolder = "Assets/TicketsData/Gerados";
    private const int Stage = 3;

    private static readonly string[] PcTickets =
    {
        "pc-sem-energia", "pc-sem-ram", "pc-sem-hd", "pc-sem-rede", "pc-sem-sistema", "pc-pecas-ausentes",
        "pc-montagem-completa", "pc-sem-video-e-rede", "monitor-sem-imagem"
    };

    [MenuItem("Tools/E-stagiar.io/Escritório de teste: fluxo integrado de chamados")]
    public static void Run()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var log = new List<string>();

        // ---- chamados, fase, cronômetro, pontuação
        TicketManager tickets = Object.FindAnyObjectByType<TicketManager>(FindObjectsInactive.Include);
        StageController stage = tickets != null ? tickets.GetComponent<StageController>() : null;
        if (tickets == null || stage == null)
        {
            Debug.LogError("EscritorioTesteFluxoSetup: falta o objeto Chamados (TicketManager + StageController) na cena.");
            return;
        }
        var soStage = new SerializedObject(stage);
        soStage.FindProperty("ticketManager").objectReferenceValue = tickets;
        soStage.FindProperty("stageNumber").intValue = Stage;
        soStage.ApplyModifiedPropertiesWithoutUndo();

        var catalog = AssetDatabase.FindAssets("t:TicketDefinition", new[] { TicketsFolder })
            .Select(g => AssetDatabase.LoadAssetAtPath<TicketDefinition>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(d => d != null && !d.tutorialOnly).OrderBy(d => d.ticketId).ToList();
        var soTickets = new SerializedObject(tickets);
        SerializedProperty list = soTickets.FindProperty("catalog");
        list.arraySize = catalog.Count;
        for (int i = 0; i < catalog.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = catalog[i];
        soTickets.ApplyModifiedPropertiesWithoutUndo();
        log.Add($"fase {Stage}, catálogo com {catalog.Count} chamados");

        var hud = tickets.GetComponent<StageTimerHUD>();
        if (hud == null) hud = tickets.gameObject.AddComponent<StageTimerHUD>();
        var soHud = new SerializedObject(hud);
        soHud.FindProperty("stageController").objectReferenceValue = stage;
        soHud.FindProperty("ticketManager").objectReferenceValue = tickets;
        soHud.ApplyModifiedPropertiesWithoutUndo();

        ScoreKeeper score = tickets.GetComponent<ScoreKeeper>();
        if (score == null) score = tickets.gameObject.AddComponent<ScoreKeeper>();
        var soScore = new SerializedObject(score);
        soScore.FindProperty("ticketManager").objectReferenceValue = tickets;
        soScore.FindProperty("stageController").objectReferenceValue = stage;
        soScore.ApplyModifiedPropertiesWithoutUndo();

        // ---- PCs das estações
        foreach (ComputerCase pcCase in Object.FindObjectsByType<ComputerCase>(FindObjectsInactive.Include))
        {
            Transform station = pcCase.transform;
            while (station.parent != null && !station.name.StartsWith("Estacao")) station = station.parent;
            string label = "PC da " + station.name.Replace("Estacao_", "Estação ");

            var soCase = new SerializedObject(pcCase);
            soCase.FindProperty("randomizeComponents").boolValue = false;
            soCase.FindProperty("fixedComponents").intValue = (int)ComputerCase.AllComponents;
            soCase.FindProperty("startingHddHasOperatingSystem").boolValue = true;
            soCase.ApplyModifiedPropertiesWithoutUndo();
            var estado = pcCase.GetComponent<PcEstadoInicial>();
            if (estado != null)
            {
                var soEstado = new SerializedObject(estado);
                soEstado.FindProperty("chanceCaboSolto").floatValue = 0f;
                soEstado.FindProperty("ligarAoIniciar").boolValue = true;
                soEstado.ApplyModifiedPropertiesWithoutUndo();
            }
            Bridge<PcTicketBridge>(pcCase.gameObject, tickets, label, PcTickets, b => b.FindProperty("scoreKeeper").objectReferenceValue = score);
            log.Add(label + (estado == null ? " (sem PcEstadoInicial: começa com cabos soltos)" : ""));
        }

        // ---- minigames
        Minigame<NotebookEstacao, NotebookTicketBridge>(tickets, "Notebook (mesa dos minigames)", "notebook-sem-boot", log);
        Minigame<ImpressoraEstacao, ImpressoraTicketBridge>(tickets, "Impressora", "impressora-sem-impressao", log);
        GameObject caixa = Find("Caixa_CaboRede");
        if (caixa != null)
        {
            Bridge<RedeTicketBridge>(caixa, tickets, "Caixa do cabo de rede (teste no rack)", new[] { "crimpar-cabo" }, null);
            log.Add("Caixa_CaboRede");
        }
        else log.Add("FALTA Caixa_CaboRede");

        // ---- supervisor
        GameObject npc = Find("NPC_Supervisor");
        if (npc != null)
        {
            npc.SetActive(true);
            var patrol = npc.GetComponent<SupervisorPatrol>();
            GameObject player = Find("Player");
            GameObject route = Find("Ronda_Supervisor");
            if (patrol != null)
            {
                var so = new SerializedObject(patrol);
                FillIfEmpty(so, "route", route != null ? route.transform : null);
                FillIfEmpty(so, "player", player != null ? player.transform : null);
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            var perception = npc.GetComponent<SupervisorPerception>();
            if (perception != null)
            {
                var so = new SerializedObject(perception);
                FillIfEmpty(so, "player", player != null ? player.transform : null);
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            log.Add("NPC_Supervisor ligado");
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("EscritorioTesteFluxoSetup: " + string.Join(" · ", log));
    }

    private static void Minigame<TStation, TBridge>(TicketManager tickets, string label, string ticketId, List<string> log)
        where TStation : Component where TBridge : TicketDeviceBridge
    {
        TStation station = Object.FindAnyObjectByType<TStation>(FindObjectsInactive.Include);
        if (station == null) { log.Add("FALTA " + typeof(TStation).Name); return; }
        Bridge<TBridge>(station.gameObject, tickets, label, new[] { ticketId }, null);
        log.Add(station.name);
    }

    private static void Bridge<T>(GameObject go, TicketManager tickets, string label, string[] ids,
                                  System.Action<SerializedObject> extra) where T : TicketDeviceBridge
    {
        T bridge = go.GetComponent<T>();
        if (bridge == null) bridge = go.AddComponent<T>();
        var so = new SerializedObject(bridge);
        so.FindProperty("ticketManager").objectReferenceValue = tickets;
        so.FindProperty("deviceLabel").stringValue = label;
        SerializedProperty acc = so.FindProperty("acceptedTicketIds");
        acc.arraySize = ids.Length;
        for (int i = 0; i < ids.Length; i++) acc.GetArrayElementAtIndex(i).stringValue = ids[i];
        extra?.Invoke(so);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void FillIfEmpty(SerializedObject so, string path, Object value)
    {
        SerializedProperty p = so.FindProperty(path);
        if (p != null && value != null && p.objectReferenceValue == null) p.objectReferenceValue = value;
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
