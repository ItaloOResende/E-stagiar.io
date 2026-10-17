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

/// <summary>Complexidade do chamado: define o prazo de referência (SLA) no <see cref="TicketManager"/>. Valores novos no fim.</summary>
public enum TicketComplexity
{
    /// <summary>Deduzida da soma dos pesos das etapas (até 4 = simples, até 6 = média, acima = complexa).</summary>
    Auto,
    /// <summary>Tarefa curta (ex.: papel/toner da impressora, cabo de rede solto).</summary>
    Simple,
    /// <summary>Troca de uma peça, crimpagem, diagnóstico com mais de um passo.</summary>
    Medium,
    /// <summary>Reparo completo (várias peças, instalação de sistema, montagem).</summary>
    Complex
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

    [Header("Prazo de referência (SLA)")]
    [Tooltip("Define o prazo pelo TicketManager (um valor por complexidade). Auto = pela soma dos pesos das etapas.")]
    public TicketComplexity complexity = TicketComplexity.Auto;
    [Tooltip("Prazo (s) só deste chamado, desde a chegada até a conclusão. 0 = usa o da complexidade.")]
    [Min(0f)] public float slaSecondsOverride;

    /// <summary>Complexidade efetiva (resolve <see cref="TicketComplexity.Auto"/> pelos pesos das etapas).</summary>
    public TicketComplexity EffectiveComplexity
    {
        get
        {
            if (complexity != TicketComplexity.Auto) return complexity;
            int weight = 0;
            if (steps != null)
                foreach (TicketStepDefinition s in steps)
                    if (s != null && !string.IsNullOrWhiteSpace(s.id)) weight += Mathf.Max(1, s.weight);
            return weight <= 4 ? TicketComplexity.Simple : weight <= 6 ? TicketComplexity.Medium : TicketComplexity.Complex;
        }
    }
}

/// <summary>Expired não é mais usado (o SLA não expira o chamado: ver <see cref="TicketInstance.IsOverdue"/>); mantido por compatibilidade.</summary>
public enum TicketStatus { Pending, InProgress, Completed, Expired }

public class TicketInstance
{
    /// <summary>Prazo padrão (s) quando ninguém informa outro (o TicketManager usa o prazo por complexidade).</summary>
    public const float DefaultSecondsToResolve = 240f;

    public readonly int Number;
    public readonly TicketDefinition Definition;
    public TicketStatus Status { get; internal set; }

    /// <summary>Prazo de referência (SLA, s), contado da chegada até a conclusão. Não expira o chamado.</summary>
    public float SecondsToResolve { get; internal set; }

    /// <summary>Quanto ainda resta do prazo; NEGATIVO depois do prazo (quanto já passou). É o que a barra mostra.</summary>
    public float SecondsLeft { get; internal set; }

    /// <summary>O prazo já passou (aberto agora, ou concluído depois do prazo).</summary>
    public bool IsOverdue => SecondsLeft < 0f;
    /// <summary>Quanto (s) passou do prazo (0 = dentro do prazo).</summary>
    public float OverdueSeconds => Mathf.Max(0f, -SecondsLeft);
    /// <summary>Concluído depois do prazo.</summary>
    public bool CompletedLate => Status == TicketStatus.Completed && IsOverdue;
    /// <summary>Já avisado de que passou do prazo (TicketManager.TicketOverdue, uma vez por chamado).</summary>
    public bool OverdueNotified { get; internal set; }
    /// <summary>Quantos minutos inteiros de atraso já foram penalizados (ScoreKeeper).</summary>
    public int LateMinutesCharged { get; set; }

    /// <summary>Onde atender (ex.: "PC da Estação 2"), preenchido pelo equipamento que assumiu o chamado. Vazio = nenhum.</summary>
    public string Location { get; set; } = "";
    /// <summary>Funcionário que pediu (padrão: o da ficha; ao reservar a estação, o funcionário DELA — StationEmployee).</summary>
    public string Requester { get; set; }
    public string Department { get; set; }
    /// <summary>Tentativas de finalizar com etapas pendentes (TicketManager.TryFinishTicket).</summary>
    public int RejectedFinishAttempts { get; internal set; }
    /// <summary>
    /// Motivo da última recusa de finalização: vazio = etapas pendentes; preenchido = todas as etapas feitas, mas o
    /// equipamento não está em condição de entrega (ex.: "gabinete aberto"), ver TicketDeviceBridge.DeliveryBlocker.
    /// </summary>
    public string LastRejectReason { get; internal set; } = "";
    /// <summary>Tentativas de entregar com o equipamento aberto/incompleto (todas as etapas feitas).</summary>
    public int RejectedDeliveryAttempts { get; internal set; }
    /// <summary>Nome do NPC da estação que recebeu o atendimento (vazio = nenhum).</summary>
    public string AssignedNpc { get; set; } = "";
    /// <summary>Em atendimento com todas as etapas feitas: falta o jogador finalizar (TicketManager.FinishTicket).</summary>
    public bool ReadyToFinish => Status == TicketStatus.InProgress && Progress >= 0.9999f;
    /// <summary>Time.time ao chegar, ao iniciar o atendimento e ao concluir (-1 = ainda não aconteceu).</summary>
    public float CreatedAt { get; internal set; } = -1f;
    public float StartedAt { get; internal set; } = -1f;
    public float CompletedAt { get; internal set; } = -1f;
    /// <summary>Duração do atendimento (início → conclusão), em segundos; -1 enquanto não concluído.</summary>
    public float ServiceSeconds => StartedAt >= 0f && CompletedAt >= 0f ? CompletedAt - StartedAt : -1f;
    private readonly HashSet<string> finishedSteps = new HashSet<string>();

    public TicketInstance(int number, TicketDefinition definition, float secondsToResolve = DefaultSecondsToResolve)
    {
        Number = number;
        Definition = definition;
        Status = TicketStatus.Pending;
        SecondsToResolve = Mathf.Max(1f, secondsToResolve);
        SecondsLeft = SecondsToResolve;
        CreatedAt = Time.time;
        Requester = definition != null ? definition.requester : "";
        Department = definition != null ? definition.department : "";
    }

    /// <summary>Fracao do prazo que ainda resta (1 = acabou de chegar; 0 = prazo atingido). E a barra do chamado.</summary>
    public float TimeFraction => SecondsToResolve <= 0f ? 0f : Mathf.Clamp01(SecondsLeft / SecondsToResolve);

    /// <summary>Depois do prazo: 0 (acabou de passar) a 1 (atraso igual a metade do prazo ou mais). A barra escurece até o preto.</summary>
    public float OverdueFraction => SecondsToResolve <= 0f ? 1f : Mathf.Clamp01(OverdueSeconds / (SecondsToResolve * 0.5f));

    /// <summary>O chamado ja saiu da fila (concluido)?</summary>
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
