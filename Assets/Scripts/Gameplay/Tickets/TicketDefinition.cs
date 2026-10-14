using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class TicketStepDefinition
{
    public string id = "step-id";
    public string description = "Concluir etapa";
    [Min(1)] public int weight = 1;
}

[CreateAssetMenu(fileName = "Ticket", menuName = "E-stagiar/Tickets/Ticket Definition")]
public class TicketDefinition : ScriptableObject
{
    public string ticketId = "ticket-id";
    public string title = "Novo chamado";
    [TextArea(2, 5)] public string description;
    public string requester = "Funcionário";
    public string department = "Escritório";
    [Min(1)] public int minimumStage = 1;
    public bool tutorialOnly;
    public List<TicketStepDefinition> steps = new List<TicketStepDefinition>();
}

public enum TicketStatus { Pending, InProgress, Completed }

public class TicketInstance
{
    public readonly int Number;
    public readonly TicketDefinition Definition;
    public TicketStatus Status { get; internal set; }
    private readonly HashSet<string> finishedSteps = new HashSet<string>();

    public TicketInstance(int number, TicketDefinition definition)
    {
        Number = number;
        Definition = definition;
        Status = TicketStatus.Pending;
    }

    public float Progress
    {
        get
        {
            if (Definition == null || Definition.steps == null || Definition.steps.Count == 0) return 0f;
            int total = 0, finished = 0;
            foreach (TicketStepDefinition step in Definition.steps)
            {
                if (step == null || string.IsNullOrWhiteSpace(step.id)) continue;
                int w = Mathf.Max(1, step.weight);
                total += w;
                if (finishedSteps.Contains(step.id)) finished += w;
            }
            return total == 0 ? 0f : (float)finished / total;
        }
    }

    public bool TryCompleteStep(string stepId)
    {
        if (Status != TicketStatus.InProgress || string.IsNullOrWhiteSpace(stepId)) return false;
        if (Definition == null || Definition.steps == null) return false;
        foreach (TicketStepDefinition step in Definition.steps)
        {
            if (step != null && step.id == stepId) return finishedSteps.Add(stepId);
        }
        return false;
    }

    public bool HasCompleted(string stepId) => finishedSteps.Contains(stepId);
}
