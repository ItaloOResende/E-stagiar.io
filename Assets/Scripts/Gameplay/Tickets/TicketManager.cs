using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Gera chamados e controla a fila. Tutorial e fase 1 sao sequenciais;
/// da fase 2 em diante a chegada e adaptativa e pode sobrepor atendimentos.</summary>
public class TicketManager : MonoBehaviour
{
    [Header("Catalogo de chamados")]
    [SerializeField] private List<TicketDefinition> catalog = new List<TicketDefinition>();
    [SerializeField] private TicketDefinition tutorialTicket;

    [Header("Dificuldade (configuravel)")]
    [Min(1)] [SerializeField] private int maximumOpenTickets = 2;
    [Min(5f)] [SerializeField] private float minimumArrivalSeconds = 120f;
    [Min(5f)] [SerializeField] private float maximumArrivalSeconds = 165f;
    [Min(0f)] [SerializeField] private float firstPhaseRestSeconds = 15f;
    [Min(0f)] [SerializeField] private float stopGeneratingBeforeEndSeconds = 90f;

    [Header("Chegada adaptativa")]
    [Tooltip("Tempo medio estimado para um ticket, incluindo leitura e deslocamento.")]
    [Min(30f)] [SerializeField] private float estimatedTicketSeconds = 180f;
    [Tooltip("Fator da chegada: 0.75 = novo chamado em 75% do tempo estimado.")]
    [Range(0.4f, 1.2f)] [SerializeField] private float arrivalFraction = 0.75f;
    [Tooltip("Variacao percentual no intervalo, para nao ficar previsivel.")]
    [Range(0f, 0.3f)] [SerializeField] private float arrivalJitter = 0.12f;
    [Tooltip("Na fase 1 o proximo chamado e imediato quando o anterior acaba.")]
    [SerializeField] private bool instantNextTicketOnFirstStage = true;

    [Header("Prazo de referência (SLA) por complexidade")]
    [Tooltip("Prazo (s) de cada chamado, contado da CHEGADA até a CONCLUSÃO. É uma referência de desempenho: passar do " +
             "prazo não fecha nem bloqueia o chamado (a barra escurece e o ScoreKeeper aplica a penalidade de atraso). " +
             "Um chamado pode ter prazo próprio (TicketDefinition.slaSecondsOverride).")]
    [Min(10f)] [SerializeField] private float simpleSlaSeconds = 150f;
    [Min(10f)] [SerializeField] private float mediumSlaSeconds = 240f;
    [Min(10f)] [SerializeField] private float complexSlaSeconds = 360f;

    [Header("Encerramento")]
    [Tooltip("Ligado: com todas as etapas feitas o chamado continua EM ATENDIMENTO (\"pronto para finalizar\") ate o " +
             "jogador finalizar (FinishTicket, botao do tablet). Desligado: conclui sozinho na ultima etapa (como antes).")]
    [SerializeField] private bool requirePlayerToFinish;

    public event Action<TicketInstance> TicketCreated;
    public event Action<TicketInstance> TicketChanged;
    public event Action<TicketInstance> TicketFinished;
    /// <summary>Um chamado aberto acabou de passar do prazo (uma vez por chamado). Ele continua aberto.</summary>
    public event Action<TicketInstance> TicketOverdue;
    /// <summary>
    /// O jogador tentou finalizar e foi recusado (uma vez por tentativa; nada foi concluído): etapas pendentes
    /// (<see cref="TicketInstance.LastRejectReason"/> vazio) ou equipamento em condição de não entrega (motivo preenchido).
    /// </summary>
    public event Action<TicketInstance> FinishRejected;
    /// <summary>
    /// Não vão chegar mais chamados (fim da geração: faltam menos de <c>stopGeneratingBeforeEndSeconds</c>) e nenhum
    /// está aberto: o expediente pode acabar antes do relógio (o StageController encerra a fase). Uma vez por fase.
    /// </summary>
    public event Action AllWorkDone;
    private bool allWorkDoneRaised;

    private readonly List<TicketInstance> tickets = new List<TicketInstance>();
    private int nextTicketNumber = 1;
    private int stageNumber;
    private bool running;
    private float nextArrivalAt;
    private float remainingStageTime;
    private TicketDefinition previousDefinition;
    private bool arrivalClockStarted;

    public IReadOnlyList<TicketInstance> Tickets => tickets;
    public bool RequirePlayerToFinish { get => requirePlayerToFinish; set => requirePlayerToFinish = value; }

    /// <summary>
    /// Opcional (ex.: rodizio de tipos na cena de teste, <see cref="TicketCategoryRotation"/>): escolhe entre os chamados
    /// JA FILTRADOS pela fase (sem tutorial, minimumStage, com etapas). Null ou retorno null = sorteio padrao.
    /// </summary>
    public Func<List<TicketDefinition>, TicketDefinition, TicketDefinition> SelectionOverride { get; set; }
    public int StageNumber => stageNumber;
    public bool Running => running;
    public int OpenCount => tickets.FindAll(t => !t.IsClosed).Count;
    public float RemainingStageTime => remainingStageTime;
    public float SecondsUntilNextArrival => running && stageNumber >= 2 && arrivalClockStarted
        ? Mathf.Max(0f, nextArrivalAt - Time.time) : -1f;

    public void BeginStage(int stage, float durationSeconds)
    {
        CancelInvoke();
        stageNumber = Mathf.Max(0, stage);
        remainingStageTime = Mathf.Max(0f, durationSeconds);
        tickets.Clear();
        previousDefinition = null;
        nextTicketNumber = 1;
        running = true;
        allWorkDoneRaised = false;
        arrivalClockStarted = false;
        maximumOpenTickets = stageNumber <= 1 ? 1 : Mathf.Min(4, stageNumber);
        CreateTicket();
        // O relogio de chegada corre desde o inicio da fase: nao depende de o jogador iniciar um atendimento
        // (antes so comecava no primeiro StartTicket, e sem iniciar nenhum nao chegava chamado novo).
        if (stageNumber >= 2) ScheduleNextArrival(Time.time);
    }

    public void UpdateStageTime(float secondsLeft) => remainingStageTime = Mathf.Max(0f, secondsLeft);

    public void EndStage()
    {
        if (running) LogQueue("fim da fase");
        running = false;
        CancelInvoke();
    }

    /// <summary>
    /// Resumo da fila no Console: chamados gerados, concluídos, em atendimento (com/sem equipamento), aguardando início
    /// (com equipamento livre ou com todos ocupados), atrasados, e se a geração parou por limite de abertos ou pelo fim.
    /// </summary>
    [ContextMenu("Diagnóstico da fila de chamados")]
    public void LogQueue() => LogQueue("agora");

    private void LogQueue(string quando)
    {
        int concluidos = 0, atendendo = 0, aguardando = 0, ocupados = 0, atrasados = 0;
        foreach (TicketInstance t in tickets)
        {
            if (t.Status == TicketStatus.Completed) { concluidos++; continue; }
            if (t.IsClosed) continue;
            if (t.SecondsLeft < 0f) atrasados++;
            if (t.Status == TicketStatus.InProgress) atendendo++;
            else if (TicketDeviceBridge.Availability(t, out _, out _) == TicketDeviceBridge.StartBlock.AllBusy) ocupados++;
            else aguardando++;
        }
        Debug.Log($"[Fila] {quando}: fase {stageNumber}, {tickets.Count} gerados, {concluidos} concluídos, {atendendo} em atendimento, " +
                  $"{aguardando} aguardando início, {ocupados} sem equipamento livre, {atrasados} atrasados (abertos). " +
                  $"Limite de abertos {maximumOpenTickets}; " +
                  (CanGenerate() ? (arrivalClockStarted ? $"próximo em {Mathf.Max(0f, nextArrivalAt - Time.time):0}s" +
                                   (OpenCount >= maximumOpenTickets ? " (espera vaga)" : "") : "relógio de chegada parado até iniciar um chamado")
                                 : "geração encerrada"), this);
    }

    private bool CanGenerate() => running && remainingStageTime > stopGeneratingBeforeEndSeconds;

    private void Update()
    {
        TickTickets(); // O relogio de cada chamado corre independente da geracao de novos chamados.

        if (running && !allWorkDoneRaised && stageNumber >= 1 && tickets.Count > 0 && !CanGenerate() && OpenCount == 0)
        {
            allWorkDoneRaised = true;
            Debug.Log("Sem chamados pendentes e sem novos chamados até o fim: expediente concluído.", this);
            AllWorkDone?.Invoke();
        }

        if (!CanGenerate() || stageNumber < 2 || !arrivalClockStarted) return;
        if (Time.time < nextArrivalAt || OpenCount >= maximumOpenTickets) return;
        if (CreateTicket()) ScheduleNextArrival(Time.time);
        else ScheduleNextArrival(Time.time); // Evita logs de erro a cada frame se catalogo estiver vazio.
    }

    /// <summary>
    /// O relogio de CADA chamado (nao e o da fase), desde a chegada: desce ate o prazo e continua contando o atraso
    /// (SecondsLeft negativo). Passar do prazo so avisa (<see cref="TicketOverdue"/>); o chamado segue aberto e pode
    /// ser iniciado e concluido normalmente.
    /// </summary>
    private void TickTickets()
    {
        if (!running) return;
        float dt = Time.deltaTime;
        for (int i = tickets.Count - 1; i >= 0; i--)
        {
            TicketInstance ticket = tickets[i];
            if (ticket.IsClosed) continue;

            ticket.SecondsLeft -= dt;
            if (ticket.SecondsLeft >= 0f || ticket.OverdueNotified) continue;

            ticket.OverdueNotified = true;
            Debug.Log($"Chamado #{ticket.Number} passou do prazo de {ticket.SecondsToResolve:0}s (continua aberto).", this);
            TicketOverdue?.Invoke(ticket);
            TicketChanged?.Invoke(ticket);
        }
    }

    /// <summary>Prazo de referência (s) de um chamado: o próprio dele, ou o da complexidade.</summary>
    public float SlaSecondsFor(TicketDefinition d)
    {
        if (d == null) return TicketInstance.DefaultSecondsToResolve;
        if (d.slaSecondsOverride > 0f) return d.slaSecondsOverride;
        switch (d.EffectiveComplexity)
        {
            case TicketComplexity.Simple: return simpleSlaSeconds;
            case TicketComplexity.Complex: return complexSlaSeconds;
            default: return mediumSlaSeconds;
        }
    }

    /// <summary>Um chamado saiu da fila (concluido): trata a chegada do proximo.</summary>
    private void OnTicketClosed()
    {
        if (!CanGenerate()) return;
        if (stageNumber == 1 && OpenCount == 0)
        {
            if (instantNextTicketOnFirstStage) CreateTicket();
            else Invoke(nameof(CreateSequentialTicket), firstPhaseRestSeconds);
        }
        else if (stageNumber >= 2 && OpenCount == 0)
        {
            // Jogador foi mais rapido que a estimativa: nao o deixa ocioso.
            if (CreateTicket()) ScheduleNextArrival(Time.time);
        }
    }

    private void ScheduleNextArrival(float now)
    {
        // Fase 2: 180 * 0.75 = 135 segundos em media.
        // Fases avancadas: acelera aos poucos, sem intervalos impossiveis.
        float stageMultiplier = Mathf.Pow(0.88f, Mathf.Max(0, stageNumber - 2));
        float baseInterval = estimatedTicketSeconds * arrivalFraction * stageMultiplier;
        float variation = UnityEngine.Random.Range(-arrivalJitter, arrivalJitter);
        float interval = Mathf.Max(65f, baseInterval * (1f + variation));
        // Respeita os valores min/max antigos no Inspector apenas como limites de seguranca amplos.
        nextArrivalAt = now + interval;
        arrivalClockStarted = true;
    }

    private bool CreateTicket()
    {
        TicketDefinition selected = ChooseDefinition();
        if (selected == null)
        {
            Debug.LogWarning("TicketManager: cadastre pelo menos um Ticket Definition valido no Catalog (ou Tutorial Ticket).", this);
            return false;
        }
        var ticket = new TicketInstance(nextTicketNumber++, selected, SlaSecondsFor(selected));
        tickets.Add(ticket);
        previousDefinition = selected;
        TicketCreated?.Invoke(ticket);
        Debug.Log($"Chamado #{ticket.Number} recebido: {selected.title} (prazo {ticket.SecondsToResolve:0}s)", this);
        return true;
    }

    private TicketDefinition ChooseDefinition()
    {
        if (stageNumber == 0) return tutorialTicket;
        var eligible = new List<TicketDefinition>();
        foreach (TicketDefinition d in catalog)
            if (d != null && !d.tutorialOnly && d.minimumStage <= stageNumber && d.steps != null && d.steps.Count > 0)
                eligible.Add(d);
        if (eligible.Count == 0) return null;
        if (SelectionOverride != null)
        {
            TicketDefinition chosen = SelectionOverride(eligible, previousDefinition);
            if (chosen != null) return chosen;
        }
        if (eligible.Count > 1) eligible.Remove(previousDefinition);
        return eligible[UnityEngine.Random.Range(0, eligible.Count)];
    }

    public bool StartTicket(int number)
    {
        TicketInstance ticket = Find(number);
        if (!running || ticket == null || ticket.Status != TicketStatus.Pending) return false;
        ticket.Status = TicketStatus.InProgress;
        ticket.StartedAt = Time.time;
        TicketChanged?.Invoke(ticket);
        if (stageNumber >= 2 && !arrivalClockStarted)
            ScheduleNextArrival(Time.time);
        return true;
    }

    public bool CompleteStep(int number, string stepId)
    {
        TicketInstance ticket = Find(number);
        if (!running || ticket == null || !ticket.TryCompleteStep(stepId)) return false;
        TicketChanged?.Invoke(ticket);
        if (ticket.Progress >= 0.9999f && !requirePlayerToFinish) Finish(ticket);
        return true;
    }

    /// <summary>
    /// Finalizacao pedida pelo jogador (tablet). So vale para chamado em atendimento com todas as etapas feitas
    /// (<see cref="TicketInstance.ReadyToFinish"/>).
    /// </summary>
    public bool FinishTicket(int number)
    {
        TicketInstance ticket = Find(number);
        if (!running || ticket == null || !ticket.ReadyToFinish) return false;
        Finish(ticket);
        return true;
    }

    /// <summary>
    /// Tentativa de finalização pelo jogador (botão do tablet). Com todas as etapas feitas, finaliza; com etapas
    /// pendentes, NÃO finaliza e avisa <see cref="FinishRejected"/> (o ScoreKeeper penaliza a tentativa).
    /// </summary>
    public bool TryFinishTicket(int number)
    {
        TicketInstance ticket = Find(number);
        if (!running || ticket == null || ticket.Status != TicketStatus.InProgress) return false;
        if (ticket.ReadyToFinish)
        {
            // etapas feitas, mas o equipamento não pode ser entregue assim (ex.: gabinete/notebook aberto): recusa e avisa
            string blocker = TicketDeviceBridge.DeliveryBlockerFor(ticket);
            if (string.IsNullOrEmpty(blocker)) { ticket.LastRejectReason = ""; Finish(ticket); return true; }
            ticket.RejectedDeliveryAttempts++;
            ticket.LastRejectReason = blocker;
            Debug.Log($"Chamado #{ticket.Number}: entrega recusada ({blocker}).", this);
            FinishRejected?.Invoke(ticket);
            TicketChanged?.Invoke(ticket);
            return false;
        }
        ticket.LastRejectReason = "";
        ticket.RejectedFinishAttempts++;
        Debug.Log($"Chamado #{ticket.Number}: tentativa de finalizar com etapas pendentes ({ticket.Progress:P0}).", this);
        FinishRejected?.Invoke(ticket);
        TicketChanged?.Invoke(ticket);
        return false;
    }

    private void Finish(TicketInstance ticket)
    {
        ticket.Status = TicketStatus.Completed;
        ticket.CompletedAt = Time.time;
        TicketFinished?.Invoke(ticket);
        TicketChanged?.Invoke(ticket);
        OnTicketClosed();   // vaga aberta: chegada do proximo (resolvido ou perdido)
    }

    private void CreateSequentialTicket()
    {
        if (stageNumber == 1 && CanGenerate() && OpenCount == 0) CreateTicket();
    }

    [ContextMenu("TESTE: Aceitar primeiro chamado pendente")]
    private void DebugAccept()
    {
        TicketInstance pending = tickets.Find(t => t.Status == TicketStatus.Pending);
        if (pending != null) StartTicket(pending.Number);
    }

    [ContextMenu("TESTE: Concluir proxima etapa")]
    private void DebugNextStep()
    {
        TicketInstance active = tickets.Find(t => t.Status == TicketStatus.InProgress);
        if (active == null || active.Definition == null) return;
        foreach (TicketStepDefinition step in active.Definition.steps)
        {
            if (step != null && !active.HasCompleted(step.id))
            {
                CompleteStep(active.Number, step.id);
                Debug.Log($"Chamado #{active.Number}: {step.description} - {active.Progress:P0}", this);
                return;
            }
        }
    }

    public TicketInstance Find(int number) => tickets.Find(t => t.Number == number);
}
