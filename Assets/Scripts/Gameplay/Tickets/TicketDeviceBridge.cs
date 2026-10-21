using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Liga UM equipamento do mundo (PC de uma estação, notebook, impressora, caixa do cabo de rede) aos chamados do
/// <see cref="TicketManager"/> da Fernanda, sem criar outro sistema de tickets:
///  1. <b>Chegada:</b> o chamado aparece no tablet e NENHUM equipamento muda.
///  2. <b>Iniciar atendimento</b> (tablet → <see cref="TicketManager.StartTicket"/>): o chamado reserva UM equipamento
///     livre que aceita o seu <i>Ticket Id</i> (<see cref="acceptedTicketIds"/>). Com vários livres, vai para o que
///     atendeu menos chamados (empate: sorteio), para não concentrar tudo no mesmo PC. A reserva escreve o local e o
///     NPC da estação no chamado (<see cref="TicketInstance.Location"/>/<see cref="TicketInstance.AssignedNpc"/>) e, só
///     então, cria o defeito NESTE equipamento (<see cref="PrepareProblem"/>). Os outros equipamentos não mudam.
///  3. <b>Etapas:</b> concluídas pelo estado real do equipamento (<see cref="Condition"/>), na ordem da ficha.
///     "Inspecionar/diagnosticar" = abrir a tela 2D deste equipamento (<see cref="UIScreenManager.AnyScreenOpened"/>).
///  4. <b>Encerramento:</b> com <see cref="TicketManager.RequirePlayerToFinish"/> ligado, as etapas feitas só deixam o
///     chamado "pronto para finalizar"; o jogador finaliza no tablet. Só então o equipamento é liberado.
/// Etapa sem condição conhecida fica parada (aviso no Console); só o botão de teste do tablet a conclui.
/// </summary>
public abstract class TicketDeviceBridge : MonoBehaviour
{
    private static readonly List<TicketDeviceBridge> active = new List<TicketDeviceBridge>();
    private static readonly Dictionary<TicketInstance, TicketDeviceBridge> owners = new Dictionary<TicketInstance, TicketDeviceBridge>();

    [SerializeField] protected TicketManager ticketManager;
    [Tooltip("Nome mostrado no tablet (ex.: \"PC da Estação 2\").")]
    [SerializeField] private string deviceLabel = "Equipamento";
    [Tooltip("NPC da estação (quem pediu o atendimento ali). Opcional.")]
    [SerializeField] private Transform stationNpc;
    [Tooltip("Ticket Ids (TicketDefinition) que este equipamento atende.")]
    [SerializeField] private List<string> acceptedTicketIds = new List<string>();
    [Tooltip("Ao reservar o equipamento para um chamado, cria nele o defeito que o chamado descreve.")]
    [SerializeField] private bool prepareProblemOnClaim = true;
    [Tooltip("Raiz do equipamento: tela 2D aberta a partir dela (ou de um filho) conta como inspeção. Vazio = este objeto.")]
    [SerializeField] private Transform deviceRoot;
    [Tooltip("Depois de atender um chamado, este equipamento fica fora da escolha por este tempo (s), se houver outro livre: " +
             "evita, por exemplo, um \"sem rede\" no mesmo PC que acabou de ser montado.")]
    [Min(0f)] [SerializeField] private float recentCooldownSeconds = 180f;
    [Tooltip("Intervalo (s) entre verificações do estado do equipamento.")]
    [Min(0.05f)] [SerializeField] private float checkInterval = 0.25f;

    private readonly HashSet<string> warned = new HashSet<string>();
    private float nextCheck;
    private bool prepared;   // o defeito do chamado atual já foi aplicado (antes disso nenhuma etapa conta)

    /// <summary>Chamado reservado agora (null = livre).</summary>
    public TicketInstance Current { get; private set; }
    /// <summary>Quantos chamados este equipamento já atendeu nesta sessão (usado na distribuição).</summary>
    public int ClaimCount { get; private set; }
    /// <summary>Time.time da última conclusão de chamado neste equipamento (-inf = nunca).</summary>
    public float LastFinishedAt { get; private set; } = float.NegativeInfinity;
    /// <summary>Atendido há pouco (dentro de <see cref="recentCooldownSeconds"/>).</summary>
    public bool RecentlyServed => Time.time - LastFinishedAt < recentCooldownSeconds;
    /// <summary>O jogador abriu a tela deste equipamento desde que o chamado foi reservado.</summary>
    protected bool Inspected { get; private set; }
    public string DeviceLabel => deviceLabel;
    public Transform StationNpc => stationNpc;
    public IReadOnlyList<string> AcceptedTicketIds => acceptedTicketIds;

    private bool Accepts(TicketInstance t) => t != null && t.Definition != null && acceptedTicketIds.Contains(t.Definition.ticketId);
    private bool Free => Current == null || Current.IsClosed || ticketManager == null || !Contains(ticketManager.Tickets, Current);

    /// <summary>
    /// O chamado pode ser iniciado agora: há um equipamento livre que o aceita, ou nenhum equipamento da cena o aceita
    /// (chamado sem integração: segue o fluxo antigo, só pelo tablet).
    /// </summary>
    public static bool CanStart(TicketInstance t)
    {
        return Availability(t, out _, out _) != StartBlock.AllBusy;
    }

    /// <summary>Por que um chamado pendente pode (ou não) ser iniciado agora.</summary>
    public enum StartBlock { Free, NoDevice, AllBusy }

    /// <summary>
    /// Equipamentos da cena que aceitam o chamado (<paramref name="total"/>) e quantos estão livres para RESERVÁ-LO agora
    /// (<paramref name="free"/>), com os mesmos critérios da reserva (<see cref="AssignAll"/>): ligado, do mesmo
    /// TicketManager e sem outro chamado. Assim o tablet nunca deixa iniciar um chamado que não teria equipamento.
    /// </summary>
    public static StartBlock Availability(TicketInstance t, out int total, out int free)
    {
        total = free = 0;
        foreach (TicketDeviceBridge b in active)
        {
            if (b == null || !b.isActiveAndEnabled || !b.Accepts(t)) continue;
            total++;
            if (b.Free && !OwnedByOther(b, t)) free++;
        }
        return total == 0 ? StartBlock.NoDevice : free > 0 ? StartBlock.Free : StartBlock.AllBusy;
    }

    // O equipamento já está reservado (owners) para outro chamado ainda aberto.
    private static bool OwnedByOther(TicketDeviceBridge b, TicketInstance t)
    {
        foreach (KeyValuePair<TicketInstance, TicketDeviceBridge> kv in owners)
            if (kv.Value == b && kv.Key != t && !kv.Key.IsClosed) return true;
        return false;
    }

    /// <summary>
    /// Estação dona deste equipamento, lida no Awake: o gabinete/notebook pode ser carregado (sai da hierarquia da
    /// estação ao ser pego), mas continua sendo o equipamento DAQUELA estação.
    /// </summary>
    protected StationEmployee HomeStation { get; private set; }
    private StationEmployee Station => HomeStation != null ? HomeStation : StationEmployee.Of(this);

    protected virtual void Awake()
    {
        HomeStation = StationEmployee.Of(this);
        if (deviceRoot == null) deviceRoot = transform;
        if (ticketManager == null) ticketManager = FindAnyObjectByType<TicketManager>();
    }

    /// <summary>
    /// Este equipamento passa a ser o da estação de <paramref name="anterior"/> (ex.: estação nova montada com OUTRO
    /// gabinete): herda a configuração, a estação, o NPC e o histórico de atendimentos. A raiz passa a ser este objeto.
    /// </summary>
    protected void InheritStation(TicketDeviceBridge anterior)
    {
        ticketManager = anterior.ticketManager;
        deviceLabel = anterior.deviceLabel;
        stationNpc = anterior.stationNpc;
        acceptedTicketIds = new List<string>(anterior.acceptedTicketIds);
        prepareProblemOnClaim = anterior.prepareProblemOnClaim;
        recentCooldownSeconds = anterior.recentCooldownSeconds;
        checkInterval = anterior.checkInterval;
        deviceRoot = transform;
        HomeStation = anterior.Station;
        ClaimCount = anterior.ClaimCount;
        LastFinishedAt = anterior.LastFinishedAt;
    }

    protected virtual void OnEnable()
    {
        if (!active.Contains(this)) active.Add(this);
        if (ticketManager != null)
        {
            ticketManager.TicketChanged += OnTicketChanged;
            ticketManager.TicketFinished += OnTicketFinished;
        }
        UIScreenManager.AnyScreenOpened += OnScreenOpened;
    }

    protected virtual void OnDisable()
    {
        active.Remove(this);
        if (ticketManager != null)
        {
            ticketManager.TicketChanged -= OnTicketChanged;
            ticketManager.TicketFinished -= OnTicketFinished;
        }
        UIScreenManager.AnyScreenOpened -= OnScreenOpened;
        Release();
    }

    // Um chamado entrou em atendimento: reserva um equipamento para ele (uma única vez).
    private void OnTicketChanged(TicketInstance t)
    {
        // chamado perdido por tempo (TicketStatus.Expired): o equipamento fica livre para outro (o defeito continua nele)
        if (t == Current && t.Status == TicketStatus.Expired)
        {
            OnTicketExpired(t);
            Release();
            AssignAll(ticketManager);
            return;
        }
        if (t.Status == TicketStatus.InProgress && !owners.ContainsKey(t)) AssignAll(ticketManager);
    }

    private void OnTicketFinished(TicketInstance ticket)
    {
        if (ticket != Current) return;
        LastFinishedAt = Time.time;
        OnTicketCompleted(ticket);
        Release();
        AssignAll(ticketManager);   // chamados iniciados que estavam sem equipamento livre
    }

    /// <summary>Reserva equipamento para todo chamado EM ATENDIMENTO que ainda não tem um.</summary>
    private static void AssignAll(TicketManager manager)
    {
        if (manager == null) return;
        foreach (TicketInstance t in manager.Tickets)
        {
            if (t.Status != TicketStatus.InProgress || owners.ContainsKey(t)) continue;
            TicketDeviceBridge best = null;
            int ties = 0;
            // equipamento recém-atendido só entra se não houver outro livre (nada de chamado contraditório em seguida)
            bool anyFresh = false;
            foreach (TicketDeviceBridge b in active)
                if (b != null && b.isActiveAndEnabled && b.ticketManager == manager && b.Accepts(t) && b.Free && !b.RecentlyServed) anyFresh = true;
            foreach (TicketDeviceBridge b in active)
            {
                if (b == null || !b.isActiveAndEnabled || b.ticketManager != manager || !b.Accepts(t) || !b.Free) continue;
                if (anyFresh && b.RecentlyServed) continue;
                // a estação do PRÓPRIO solicitante da ficha vem primeiro, MAS só se ainda não atendeu nenhum chamado na
                // partida: fichas repetidas (ou duas do mesmo solicitante) mandavam o MESMO computador de novo com outros
                // ainda sem chamado. Depois, a que atendeu menos (sorteio no empate): só repete com todos já usados.
                bool dono = b.IsRequesterStation(t) && b.ClaimCount == 0, bestDono = best != null && best.IsRequesterStation(t) && best.ClaimCount == 0;
                if (best != null && bestDono && !dono) continue;
                if (best == null || (dono && !bestDono) || b.ClaimCount < best.ClaimCount) { best = b; ties = 1; }
                else if (b.ClaimCount == best.ClaimCount && Random.Range(0, ++ties) == 0) best = b;   // sorteio justo no empate
            }
            if (best != null) best.Claim(t);
        }
    }

    private bool IsRequesterStation(TicketInstance t)
    {
        StationEmployee e = Station;
        return e != null && e.Occupied && t.Definition != null && e.employeeName == t.Definition.requester;
    }

    private void Claim(TicketInstance t)
    {
        Release();
        owners[t] = this;
        Current = t;
        ClaimCount++;
        Inspected = false;
        prepared = !prepareProblemOnClaim;
        t.Location = deviceLabel;
        t.AssignedNpc = stationNpc != null ? stationNpc.name : "";
        // o chamado é do funcionário da estação (placa da mesa); o local é a estação, nunca o rack ou o testador
        StationEmployee dono = Station;
        if (dono != null && !dono.Occupied)   // estação sem funcionário: o solicitante é o da ficha, o local é a estação
        {
            if (!string.IsNullOrEmpty(dono.stationLabel)) t.Location = $"{dono.stationLabel} ({deviceLabel})";
        }
        else if (dono != null)
        {
            t.Requester = dono.employeeName;
            if (!string.IsNullOrEmpty(dono.department)) t.Department = dono.department;
            t.AssignedNpc = "";
            t.Location = $"Estação de {dono.employeeName} ({deviceLabel})";
        }
        StationLocator.Show(this, dono != null ? dono.transform : deviceRoot);
        if (prepareProblemOnClaim) StartCoroutine(PrepareLater(t));
        OnClaimed(t);
        Debug.Log($"Chamado #{t.Number} ({t.Definition.ticketId}) reservado: {deviceLabel}" +
                  (stationNpc != null ? $" (NPC {stationNpc.name})" : "") + ".", this);
    }

    // Um frame depois do clique: o estado inicial do equipamento (ex.: PcEstadoInicial, no Start) já foi aplicado.
    private IEnumerator PrepareLater(TicketInstance t)
    {
        yield return null;
        if (Current != t) yield break;
        PrepareProblem(t.Definition.ticketId);
        prepared = true;
    }

    private static bool Contains(IReadOnlyList<TicketInstance> list, TicketInstance t)
    {
        foreach (TicketInstance x in list) if (x == t) return true;
        return false;
    }

    private void Release()
    {
        if (Current != null && owners.TryGetValue(Current, out TicketDeviceBridge o) && o == this) owners.Remove(Current);
        Current = null;
        StationLocator.Hide(this);
    }

    private void OnScreenOpened(UIScreen screen, GameObject source)
    {
        if (Current == null || source == null) return;
        if (source.transform == deviceRoot || source.transform.IsChildOf(deviceRoot) || deviceRoot.IsChildOf(source.transform))
        {
            Inspected = true;
            Evaluate();
        }
    }

    protected virtual void Update()
    {
        if (Current == null || Current.Status != TicketStatus.InProgress || Time.time < nextCheck) return;
        nextCheck = Time.time + checkInterval;
        Evaluate();
    }

    /// <summary>
    /// Conclui, na ordem da ficha, as etapas cujas condições valem agora (para na primeira que não vale). Não finaliza
    /// o chamado: com todas as etapas feitas ele fica "pronto para finalizar" (se o TicketManager exigir finalização).
    /// </summary>
    public void Evaluate()
    {
        TicketInstance t = Current;
        if (t == null || !prepared || t.Status != TicketStatus.InProgress || t.ReadyToFinish || t.Definition == null || t.Definition.steps == null) return;
        foreach (TicketStepDefinition step in t.Definition.steps)
        {
            if (step == null || string.IsNullOrWhiteSpace(step.id) || t.HasCompleted(step.id)) continue;
            if (!Knows(step.id))
            {
                if (warned.Add(step.id))
                    Debug.LogWarning($"{GetType().Name} ({deviceLabel}): a etapa '{step.id}' não tem condição no equipamento; conclua pelo tablet.", this);
                return;
            }
            if (!Condition(step.id)) return;
            ticketManager.CompleteStep(t.Number, step.id);
            if (Current != t) return;   // concluiu o chamado (TicketManager sem finalização explícita)
        }
    }

    /// <summary>
    /// Motivo para o equipamento do chamado NÃO poder ser entregue agora, mesmo com todas as etapas feitas (null = pode).
    /// Só vale para chamados com equipamento reservado; os outros nunca são bloqueados.
    /// </summary>
    public static string DeliveryBlockerFor(TicketInstance t)
    {
        if (t == null || !owners.TryGetValue(t, out TicketDeviceBridge b) || b == null) return null;
        string estado = b.DeliveryBlocker();   // primeiro o estado do equipamento (ex.: gabinete aberto)
        if (!string.IsNullOrEmpty(estado)) return estado;
        // coerência: uma etapa já concluída cujo estado real voltou atrás (ex.: tirou a RAM depois de instalar) impede a
        // entrega; o tablet mostra qual
        if (t.Definition != null && t.Definition.steps != null)
            foreach (TicketStepDefinition step in t.Definition.steps)
                if (step != null && !string.IsNullOrWhiteSpace(step.id) && t.HasCompleted(step.id) && b.Knows(step.id) && b.MustHoldOnDelivery(step.id) && !b.Condition(step.id))
                    return $"a etapa \"{step.description}\" deixou de valer";
        return null;
    }

    /// <summary>
    /// Condição de entrega do equipamento (padrão: sempre pode). Ex.: o PC não é entregue com o gabinete aberto. Só é
    /// consultada com todas as etapas feitas: enquanto há etapa pendente, um equipamento aberto é parte do trabalho.
    /// </summary>
    protected virtual string DeliveryBlocker() => null;

    /// <summary>
    /// A etapa concluída precisa continuar valendo na entrega (padrão: sim). Etapas de passagem, desfeitas pelo próprio
    /// procedimento (ex.: abrir-gabinete, que precisa estar FECHADO para entregar), devolvem false.
    /// </summary>
    protected virtual bool MustHoldOnDelivery(string stepId) => true;

    /// <summary>Este equipamento sabe avaliar a etapa?</summary>
    protected abstract bool Knows(string stepId);
    /// <summary>A etapa está cumprida no estado atual do equipamento?</summary>
    protected abstract bool Condition(string stepId);
    /// <summary>Cria o defeito do chamado no equipamento (padrão: nada).</summary>
    protected virtual void PrepareProblem(string ticketId) { }
    protected virtual void OnClaimed(TicketInstance ticket) { }
    protected virtual void OnTicketCompleted(TicketInstance ticket) { }
    /// <summary>O chamado reservado aqui foi perdido por tempo (antes de liberar o equipamento).</summary>
    protected virtual void OnTicketExpired(TicketInstance ticket) { }
}
