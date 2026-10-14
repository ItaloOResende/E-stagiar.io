using System;
using System.Collections.Generic;
using UnityEngine;

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

    public IReadOnlyList<TicketInstance> Tickets => tickets;
    public int StageNumber => stageNumber;
    public bool Running => running;
    public int OpenCount => tickets.FindAll(t => t.Status != TicketStatus.Completed).Count;

    public void BeginStage(int stage, float durationSeconds)
    {
        stageNumber = Mathf.Max(0, stage);
        remainingStageTime = Mathf.Max(0, durationSeconds);
        tickets.Clear();
        previousDefinition = null;
        nextTicketNumber = 1;
        running = true;
        // No tutorial e na fase 1: sempre um de cada vez.
        maximumOpenTickets = stageNumber <= 1 ? 1 : Mathf.Min(4, stageNumber);
        // Fases mais altas: chegadas mais frequentes, com um piso para nao inviabilizar.
        if (stageNumber >= 2)
        {
            minimumArrivalSeconds = Mathf.Max(65f, 120f - (stageNumber - 2) * 12f);
            maximumArrivalSeconds = Mathf.Max(minimumArrivalSeconds + 20f, 165f - (stageNumber - 2) * 12f);
        }
        CreateTicket();
        ScheduleNextArrival(Time.time);
    }

    public void UpdateStageTime(float secondsLeft)
    {
        remainingStageTime = Mathf.Max(0f, secondsLeft);
    }

    public void EndStage() => running = false;

    private void Update()
    {
        if (!running || stageNumber <= 1) return;
        if (remainingStageTime <= stopGeneratingBeforeEndSeconds) return;
        if (Time.time < nextArrivalAt) return;
        if (OpenCount >= maximumOpenTickets) return;
        CreateTicket();
        ScheduleNextArrival(Time.time);
    }

    private void ScheduleNextArrival(float now)
    {
        nextArrivalAt = now + UnityEngine.Random.Range(minimumArrivalSeconds, maximumArrivalSeconds);
    }

    private void CreateTicket()
    {
        TicketDefinition selected = ChooseDefinition();
        if (selected == null)
        {
            Debug.LogWarning("TicketManager: adicione Ticket Definitions ao Catalog (ou Tutorial Ticket).", this);
            return;
        }
        var ticket = new TicketInstance(nextTicketNumber++, selected);
        tickets.Add(ticket);
        previousDefinition = selected;
        TicketCreated?.Invoke(ticket);
        Debug.Log($"Chamado #{ticket.Number} recebido: {selected.title}", this);
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
            if (stageNumber == 1 && remainingStageTime > stopGeneratingBeforeEndSeconds)
                Invoke(nameof(CreateSequentialTicket), firstPhaseRestSeconds);
        }
        return true;
    }

    private void CreateSequentialTicket()
    {
        if (running && stageNumber == 1 && remainingStageTime > stopGeneratingBeforeEndSeconds && OpenCount == 0)
            CreateTicket();
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
                Debug.Log($"Chamado #{active.Number}: {step.description} — {active.Progress:P0}", this);
                return;
            }
        }
    }

    public TicketInstance Find(int number) => tickets.Find(t => t.Number == number);
}
