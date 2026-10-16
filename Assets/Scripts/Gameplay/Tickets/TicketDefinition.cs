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

public enum TicketStatus { Pending, InProgress, Completed, Expired }

public class TicketInstance
{
    /// <summary>Tempo padrao (s) que um chamado tem para ser resolvido, contado desde a chegada.</summary>
    public const float DefaultSecondsToResolve = 60f;

    public readonly int Number;
    public readonly TicketDefinition Definition;
    public TicketStatus Status { get; internal set; }

    /// <summary>Quanto tempo (s) este chamado tem para ser resolvido (contado desde a chegada dele).</summary>
    public float SecondsToResolve { get; internal set; }

    /// <summary>Quanto ainda resta desse tempo — e o que a barra do chamado mostra descendo.</summary>
    public float SecondsLeft { get; internal set; }

    private readonly HashSet<string> finishedSteps = new HashSet<string>();

    public TicketInstance(int number, TicketDefinition definition, float secondsToResolve = DefaultSecondsToResolve)
    {
        Number = number;
        Definition = definition;
        Status = TicketStatus.Pending;
        SecondsToResolve = Mathf.Max(1f, secondsToResolve);
        SecondsLeft = SecondsToResolve;
    }

    /// <summary>Fracao do tempo que ainda resta (1 = acabou de chegar; 0 = tempo esgotado). E a barra do chamado.</summary>
    public float TimeFraction => SecondsToResolve <= 0f ? 0f : Mathf.Clamp01(SecondsLeft / SecondsToResolve);

    /// <summary>O chamado ja saiu da fila (resolvido ou perdido por tempo)?</summary>
    public bool IsClosed => Status == TicketStatus.Completed || Status == TicketStatus.Expired;

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
