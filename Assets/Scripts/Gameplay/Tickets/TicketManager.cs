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

    public event Action<TicketInstance> TicketCreated;
    public event Action<TicketInstance> TicketChanged;
    public event Action<TicketInstance> TicketFinished;

    private readonly List<TicketInstance> tickets = new List<TicketInstance>();
    private int nextTicketNumber = 1;
    private int stageNumber;
    private bool running;
    private float nextArrivalAt;
    private float remainingStageTime;
    private TicketDefinition previousDefinition;
    private bool arrivalClockStarted;

    public IReadOnlyList<TicketInstance> Tickets => tickets;
    public int StageNumber => stageNumber;
    public bool Running => running;
    public int OpenCount => tickets.FindAll(t => t.Status != TicketStatus.Completed).Count;
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
        arrivalClockStarted = false;
        maximumOpenTickets = stageNumber <= 1 ? 1 : Mathf.Min(4, stageNumber);
        CreateTicket();
        // O primeiro intervalo so comeca quando o jogador INICIA o primeiro atendimento.
    }

    public void UpdateStageTime(float secondsLeft) => remainingStageTime = Mathf.Max(0f, secondsLeft);

    public void EndStage()
    {
        running = false;
        CancelInvoke();
    }

    private bool CanGenerate() => running && remainingStageTime > stopGeneratingBeforeEndSeconds;

    private void Update()
    {
        if (!CanGenerate() || stageNumber < 2 || !arrivalClockStarted) return;
        if (Time.time < nextArrivalAt || OpenCount >= maximumOpenTickets) return;
        if (CreateTicket()) ScheduleNextArrival(Time.time);
        else ScheduleNextArrival(Time.time); // Evita logs de erro a cada frame se catalogo estiver vazio.
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
        var ticket = new TicketInstance(nextTicketNumber++, selected);
        tickets.Add(ticket);
        previousDefinition = selected;
        TicketCreated?.Invoke(ticket);
        Debug.Log($"Chamado #{ticket.Number} recebido: {selected.title}", this);
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
        if (eligible.Count > 1) eligible.Remove(previousDefinition);
        return eligible[UnityEngine.Random.Range(0, eligible.Count)];
    }

    public bool StartTicket(int number)
    {
        TicketInstance ticket = Find(number);
        if (!running || ticket == null || ticket.Status != TicketStatus.Pending) return false;
        ticket.Status = TicketStatus.InProgress;
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
        if (ticket.Progress >= 0.9999f)
        {
            ticket.Status = TicketStatus.Completed;
            TicketFinished?.Invoke(ticket);
            TicketChanged?.Invoke(ticket);
            if (CanGenerate())
            {
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
        }
        return true;
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
