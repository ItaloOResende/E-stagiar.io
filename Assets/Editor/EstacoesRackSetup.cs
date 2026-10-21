using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Office_Placeholder: todas as estações recebem chamados, o rack atende todos os PCs e o rodízio cobre os minigames.
/// Idempotente (rodar de novo não muda o que já está certo) e só mexe em componentes, nunca no layout:
///  1. <b>Nomes</b>: estação com <see cref="StationEmployee.employeeName"/> vazio e plaquinha com nome posto à mão
///     (frente e verso iguais) passa a ter esse nome como funcionário — a plaquinha, o chamado e o tablet mostram o
///     mesmo nome. Frente ≠ verso, ou placa sem nome, é só relatado (decisão do usuário). NPCs não são criados nem
///     movidos.
///  2. <b>Equipamentos</b>: PC/notebook de estação sem <see cref="TicketDeviceBridge"/> ganha uma, copiada da de outra
///     estação do mesmo tipo (mesmos chamados e ajustes; só o rótulo é o da estação).
///  3. <b>Rack</b>: uma porta por PC de estação + 2 reservas (portas livres no switch: sem elas não há o defeito "porta
///     trocada" nem o laço S ↔ S); cada ponto de rede de PC sem porta recebe a próxima livre (as já vinculadas ficam como estão) e o
///     chamado do rack entra nos PCs com porta.
///  4. <b>Rodízio</b> (<see cref="TicketCategoryRotation"/>): subgrupos de Rede (crimpagem, rack, cabo defeituoso) e de
///     PC (peças, cabos).
///  5. <b>NPCs e setores</b>: liga o NPC sentado (indicado pelo usuário) à estação dele (<c>stationNpc</c>, sem mover) e
///     preenche o setor das estações que estavam sem (<see cref="SetorPadrao"/>). Só onde ainda está vazio.
/// Antes de mudar, copia a cena (arquivo salvo) para <c>Art/_Backup/</c>. Depois: salvar a cena (Ctrl+S).
/// </summary>
public static class EstacoesRackSetup
{
    private const string Cena = "Assets/Scenes/Office_Placeholder.unity";
    private const int PortaReserva = 2;   // portas livres no switch: defeito "porta trocada" e laço S ↔ S

    internal static readonly string[] RedeCrimpagem = { TestadorCabo.ChamadoCrimpagem };
    internal static readonly string[] RedeRack = { TestadorCabo.ChamadoRack };
    internal static readonly string[] RedeCabo = { TestadorCabo.ChamadoCaboEstacao };
    internal static readonly string[] PcPecas = { "pc-sem-ram", "pc-sem-hd", "pc-pecas-ausentes", "pc-montagem-completa", "pc-sem-sistema" };
    internal static readonly string[] PcCabos = { "pc-sem-energia", "pc-sem-rede", "monitor-sem-imagem", "pc-sem-video-e-rede" };

    // NPC sentado (objeto na raiz da cena) de cada estação, como o usuário indicou.
    private static readonly Dictionary<string, string> NpcDaEstacao = new Dictionary<string, string>
    {
        { "Estação 03", "npc 5" }, { "Estação 05", "npc" }, { "Estação 10", "npc4" }, { "Estação 16", "npc3" },
    };

    // Setor das estações que ganharam funcionário pelo nome da plaquinha (escolhidos entre os setores das fichas).
    private static readonly Dictionary<string, string> SetorPadrao = new Dictionary<string, string>
    {
        { "Ana", "Comercial" }, { "Lucas", "Financeiro" }, { "Giovana", "Atendimento" }, { "Débora", "Administrativo" },
        { "Eduardo", "Comercial" }, { "Marcos", "Administrativo" }, { "Lúcia", "Atendimento" },
    };

    private static readonly List<string> L = new List<string>();

    [MenuItem("Tools/E-stagiar.io/Chamados: estações, rack e rodízio")]
    public static void Rodar()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != Cena)
        {
            EditorUtility.DisplayDialog("Estações e rack", "Abra a cena Office_Placeholder antes.", "OK");
            return;
        }
        if (scene.isDirty && !EditorUtility.DisplayDialog("Estações e rack",
                "A cena tem alterações não salvas. O backup copia o arquivo SALVO. Continuar mesmo assim?", "Continuar", "Cancelar"))
            return;
        Backup();
        L.Clear();
        Aplicar();
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[Estações e rack]\n" + string.Join("\n", L));
        EditorUtility.DisplayDialog("Estações e rack", "Pronto. Confira o Console e salve a cena (Ctrl+S).", "OK");
    }

    /// <summary>Aplica as quatro etapas na cena aberta (sem backup nem diálogo: usado também pelos testes).</summary>
    public static IReadOnlyList<string> Aplicar()
    {
        var estacoes = Object.FindObjectsByType<StationEmployee>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .OrderBy(e => e.stationLabel).ToList();
        foreach (StationEmployee e in estacoes) Nome(e);
        Bridges(estacoes);
        Rack(estacoes);
        Rodizio();
        NpcsESetores(estacoes);
        return L;
    }

    private static void Backup()
    {
        string pasta = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Art", "_Backup",
                                    System.DateTime.Now.ToString("yyyy-MM-dd_HHmm") + "_estacoes_rack");
        Directory.CreateDirectory(pasta);
        File.Copy(Path.Combine(Path.GetDirectoryName(Application.dataPath), Cena), Path.Combine(pasta, "Office_Placeholder.unity"), true);
        Debug.Log($"[Estações e rack] backup da cena em {pasta}");
    }

    // ------------------------------------------------------------------ 1. nomes

    private static void Nome(StationEmployee e)
    {
        StationNamePlate placa = e.GetComponentInChildren<StationNamePlate>(true);
        string[] faces = placa != null ? NomesDaPlaca(placa) : new string[0];
        if (e.Occupied)
        {
            if (faces.Any(f => f != e.employeeName)) L.Add($"{e.stationLabel}: plaquinha ({string.Join(" / ", faces)}) ≠ funcionário {e.employeeName} — ajustada pelo funcionário");
        }
        else
        {
            string nome = faces.Length > 0 ? faces[0] : "";
            bool valido = !string.IsNullOrWhiteSpace(nome) && nome != "Funcionário" && nome != e.stationLabel && !nome.StartsWith("Estação");
            if (faces.Distinct().Count() > 1) { L.Add($"AMBÍGUO {e.stationLabel}: frente e verso da plaquinha diferem ({string.Join(" / ", faces)}); nada mudou"); return; }
            if (!valido) { L.Add($"AMBÍGUO {e.stationLabel}: plaquinha sem nome ('{nome}'); nada mudou"); return; }
            Undo.RecordObject(e, "Nome da estação");
            e.employeeName = nome.Trim();
            e.nomeNaPlaca = "";   // era o nome fictício do setup; o nome real agora é o do funcionário
            EditorUtility.SetDirty(e);
            L.Add($"{e.stationLabel}: funcionário = '{e.employeeName}' (nome da plaquinha)");
        }
        if (placa != null)
        {
            placa.Refresh();   // o texto salvo na cena passa a ser o mesmo do jogo
            foreach (TextMesh t in placa.GetComponentsInChildren<TextMesh>(true)) EditorUtility.SetDirty(t);
        }
    }

    // Textos de nome da plaquinha (frente e verso), pelo campo nameTexts.
    internal static string[] NomesDaPlaca(StationNamePlate placa)
    {
        var so = new SerializedObject(placa);
        SerializedProperty arr = so.FindProperty("nameTexts");
        var nomes = new List<string>();
        for (int i = 0; arr != null && i < arr.arraySize; i++)
            if (arr.GetArrayElementAtIndex(i).objectReferenceValue is TextMesh t) nomes.Add(t.text);
        return nomes.ToArray();
    }

    // ------------------------------------------------------------------ 2. equipamentos

    private static void Bridges(List<StationEmployee> estacoes)
    {
        PcTicketBridge pcModelo = Object.FindObjectsByType<PcTicketBridge>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .OrderByDescending(b => b.AcceptedTicketIds.Count).FirstOrDefault();
        NotebookTicketBridge nbModelo = Object.FindAnyObjectByType<NotebookTicketBridge>(FindObjectsInactive.Include);
        foreach (StationEmployee e in estacoes)
        {
            ComputerCase pc = e.GetComponentInChildren<ComputerCase>(true);
            NotebookEstacao nb = e.GetComponentInChildren<NotebookEstacao>(true);
            if (pc != null && pc.GetComponent<TicketDeviceBridge>() == null)
                Copiar(pcModelo, pc.gameObject, $"PC da {e.stationLabel}", e);
            else if (pc == null && nb != null && nb.GetComponent<TicketDeviceBridge>() == null)
                Copiar(nbModelo, nb.gameObject, $"Notebook da {e.stationLabel}", e);
        }
    }

    private static readonly HashSet<string> PropriosDaEstacao = new HashSet<string> { "deviceLabel", "stationNpc", "deviceRoot", "mesaDaEstacao" };

    private static void Copiar<T>(T modelo, GameObject alvo, string rotulo, StationEmployee e) where T : TicketDeviceBridge
    {
        if (modelo == null) { L.Add($"SEM MODELO de {typeof(T).Name}: {e.stationLabel} ficou sem chamados"); return; }
        T nova = Undo.AddComponent<T>(alvo);
        var de = new SerializedObject(modelo);
        var para = new SerializedObject(nova);
        SerializedProperty p = de.GetIterator();
        for (bool entrar = true; p.NextVisible(entrar); entrar = false)
            if (p.name != "m_Script" && !PropriosDaEstacao.Contains(p.name)) para.CopyFromSerializedProperty(p);
        para.FindProperty("deviceLabel").stringValue = rotulo;
        para.ApplyModifiedPropertiesWithoutUndo();
        L.Add($"{e.stationLabel}: {typeof(T).Name} adicionada ('{rotulo}', {nova.AcceptedTicketIds.Count} chamados)");
    }

    // ------------------------------------------------------------------ 3. rack

    /// <summary>Ponto de rede (parede) de um PC de estação: o que já tem PontoDeRedeRack ou o primeiro da estação.</summary>
    internal static WallConnectionPoint PontoDaEstacao(StationEmployee e)
    {
        var pontos = e.GetComponentsInChildren<WallConnectionPoint>(true);
        return pontos.FirstOrDefault(w => w.GetComponent<PontoDeRedeRack>() != null) ?? pontos.FirstOrDefault();
    }

    private static void Rack(List<StationEmployee> estacoes)
    {
        RedeGame rede = Object.FindAnyObjectByType<RedeGame>(FindObjectsInactive.Include);
        var pcs = estacoes.Where(e => e.GetComponentInChildren<ComputerCase>(true) != null && PontoDaEstacao(e) != null).ToList();
        int portas = pcs.Count + PortaReserva;
        if (rede == null) { L.Add("SEM RedeGame na cena: o rack não foi ampliado"); return; }
        var soRede = new SerializedObject(rede);
        if (soRede.FindProperty("portasDoRack").intValue < portas)
        {
            soRede.FindProperty("portasDoRack").intValue = portas;
            soRede.ApplyModifiedProperties();
            L.Add($"rack: {portas} portas ({pcs.Count} PCs + {PortaReserva} reserva)");
        }
        portas = rede.PortasDoRack;

        var usadas = new HashSet<int>();
        foreach (StationEmployee e in pcs) { int n = PontoDeRedeRack.PortaDe(PontoDaEstacao(e)); if (n > 0) usadas.Add(n); }
        int proxima = 1;
        foreach (StationEmployee e in pcs)
        {
            WallConnectionPoint ponto = PontoDaEstacao(e);
            PontoDeRedeRack link = ponto.GetComponent<PontoDeRedeRack>() ?? Undo.AddComponent<PontoDeRedeRack>(ponto.gameObject);
            if (link.PortaRack == 0)
            {
                while (usadas.Contains(proxima)) proxima++;
                if (proxima > portas) { L.Add($"{e.stationLabel}: sem porta livre no rack"); continue; }
                var so = new SerializedObject(link);
                so.FindProperty("portaRack").intValue = proxima;
                so.ApplyModifiedProperties();
                usadas.Add(proxima);
                L.Add($"{e.stationLabel}: ponto de rede → porta {proxima} do rack");
            }
            PcTicketBridge pc = e.GetComponentInChildren<PcTicketBridge>(true);
            if (pc != null && !pc.AcceptedTicketIds.Contains(TestadorCabo.ChamadoRack))
            {
                var so = new SerializedObject(pc);
                SerializedProperty ids = so.FindProperty("acceptedTicketIds");
                ids.InsertArrayElementAtIndex(ids.arraySize);
                ids.GetArrayElementAtIndex(ids.arraySize - 1).stringValue = TestadorCabo.ChamadoRack;
                so.ApplyModifiedProperties();
                L.Add($"{e.stationLabel}: aceita o chamado do rack");
            }
        }
    }

    // ------------------------------------------------------------------ 5. NPCs e setores

    private static void NpcsESetores(List<StationEmployee> estacoes)
    {
        var raiz = SceneManager.GetActiveScene().GetRootGameObjects();
        foreach (StationEmployee e in estacoes)
        {
            if (string.IsNullOrEmpty(e.department) && SetorPadrao.TryGetValue(e.employeeName, out string setor))
            {
                Undo.RecordObject(e, "Setor da estação");
                e.department = setor;
                EditorUtility.SetDirty(e);
                StationNamePlate placa = e.GetComponentInChildren<StationNamePlate>(true);
                if (placa != null) { placa.Refresh(); foreach (TextMesh t in placa.GetComponentsInChildren<TextMesh>(true)) EditorUtility.SetDirty(t); }
                L.Add($"{e.stationLabel}: setor = {setor}");
            }
            if (!NpcDaEstacao.TryGetValue(e.stationLabel, out string npcNome)) continue;
            GameObject npc = raiz.FirstOrDefault(g => g.name == npcNome);
            TicketDeviceBridge bridge = e.GetComponentInChildren<TicketDeviceBridge>(true);
            if (npc == null || bridge == null) { L.Add($"AMBÍGUO {e.stationLabel}: NPC '{npcNome}' ou equipamento não encontrado; nada mudou"); continue; }
            var so = new SerializedObject(bridge);
            SerializedProperty campo = so.FindProperty("stationNpc");
            if (campo.objectReferenceValue == npc.transform) continue;
            if (campo.objectReferenceValue != null) { L.Add($"AMBÍGUO {e.stationLabel}: já ligada a {campo.objectReferenceValue.name}; nada mudou"); continue; }
            campo.objectReferenceValue = npc.transform;
            so.ApplyModifiedProperties();
            L.Add($"{e.stationLabel}: NPC {npcNome} ({e.employeeName})");
        }
    }

    // ------------------------------------------------------------------ 4. rodízio

    private static void Rodizio()
    {
        TicketCategoryRotation rot = Object.FindAnyObjectByType<TicketCategoryRotation>(FindObjectsInactive.Include);
        if (rot == null) { L.Add("SEM TicketCategoryRotation na cena"); return; }
        var so = new SerializedObject(rot);
        SerializedProperty groups = so.FindProperty("groups");
        for (int i = 0; i < groups.arraySize; i++)
        {
            SerializedProperty g = groups.GetArrayElementAtIndex(i);
            string nome = g.FindPropertyRelative("name").stringValue;
            if (nome == "Rede") Subgrupos(g, ("Crimpagem", RedeCrimpagem), ("Rack", RedeRack), ("Cabo defeituoso", RedeCabo));
            else if (nome == "PC") Subgrupos(g, ("Peças", PcPecas), ("Cabos", PcCabos));
        }
        so.ApplyModifiedProperties();
    }

    private static void Subgrupos(SerializedProperty grupo, params (string nome, string[] ids)[] subs)
    {
        SerializedProperty lista = grupo.FindPropertyRelative("subgroups");
        lista.arraySize = subs.Length;
        for (int i = 0; i < subs.Length; i++)
        {
            SerializedProperty s = lista.GetArrayElementAtIndex(i);
            s.FindPropertyRelative("name").stringValue = subs[i].nome;
            SerializedProperty ids = s.FindPropertyRelative("ticketIds");
            ids.arraySize = subs[i].ids.Length;
            for (int k = 0; k < subs[i].ids.Length; k++) ids.GetArrayElementAtIndex(k).stringValue = subs[i].ids[k];
        }
        L.Add($"rodízio {grupo.FindPropertyRelative("name").stringValue}: {string.Join(" · ", subs.Select(s => s.nome))}");
    }
}
