using System;
using UnityEngine;

/// <summary>
/// Fecha um chamado pelo ESTADO FINAL do computador, não etapa por etapa: enquanto o PC não estiver do jeito
/// combinado, nada acontece; quando estiver, o chamado inteiro é dado como resolvido (todas as etapas de uma vez).
/// Ex.: "consertar PC" = todas as peças no lugar, cabos ligados, ligando no sistema e com internet.
/// Anexe somente ao gabinete que deve atender ao chamado configurado.
/// </summary>
[RequireComponent(typeof(ComputerPower))]
public class ComputerPowerTicketBridge : MonoBehaviour
{
    /// <summary>O que o PC precisa ter, tudo junto, para o chamado ser considerado resolvido.</summary>
    [Flags]
    public enum FinalState
    {
        Nothing = 0,
        /// <summary>Todas as peças no lugar: fonte, placa-mãe, memória RAM, cooler e HD.</summary>
        AllParts = 1 << 0,
        /// <summary>Cabo de energia do gabinete E do monitor, cada um com as duas pontas ligadas.</summary>
        PowerCables = 1 << 1,
        /// <summary>Imagem chegando ao monitor (cabo de vídeo ou HDMI nas duas pontas).</summary>
        VideoSignal = 1 << 2,
        /// <summary>Ligado e mostrando o sistema operacional.</summary>
        OperatingSystem = 1 << 3,
        /// <summary>No sistema operacional com o cabo de rede ligado.</summary>
        Internet = 1 << 4,
        /// <summary>Gabinete fechado (todos os parafusos obrigatórios recolocados).</summary>
        CaseClosed = 1 << 5
    }

    [Header("Referencias")]
    [SerializeField] private ComputerPower computerPower;
    [SerializeField] private TicketManager ticketManager;

    [Header("Chamado deste computador")]
    [SerializeField] private string ticketId = "pc-nao-liga";

    [Header("Fechamento pelo ESTADO FINAL")]
    [Tooltip("Ligado: o chamado só é resolvido quando o PC estiver INTEIRO no estado abaixo (as etapas fecham todas de uma vez). " +
             "Desligado: usa as etapas por condição, uma a uma, nos campos seguintes.")]
    [SerializeField] private bool requireFinalState = true;
    [Tooltip("Estado final esperado. Padrão: todas as peças + cabos de energia + ligando no sistema + com internet.")]
    [SerializeField] private FinalState requiredFinalState =
        FinalState.AllParts | FinalState.PowerCables | FinalState.OperatingSystem | FinalState.Internet;
    [Tooltip("Tempo (s) entre as verificações do estado do PC (o jogador pode montar tudo antes de aceitar o chamado).")]
    [Min(0.05f)] [SerializeField] private float verifyIntervalSeconds = 0.25f;

    [Header("Etapas por condicao (so com o estado final DESLIGADO)")]
    [Tooltip("Deixe vazio para desativar a regra.")]
    [SerializeField] private string powerConnectedStepId = "";
    [SerializeField] private string videoConnectedStepId = "";
    [SerializeField] private string networkConnectedStepId = "";
    [SerializeField] private string biosReachedStepId = "";
    [SerializeField] private string operatingSystemReachedStepId = "resolver-problema";

    private float nextVerifyAt;
    private bool closingTicket;

    private void Awake()
    {
        if (computerPower == null) computerPower = GetComponent<ComputerPower>();
    }

    private void OnEnable()
    {
        if (computerPower == null) computerPower = GetComponent<ComputerPower>();
        if (computerPower == null) return;
        computerPower.ConnectionsChanged += OnComputerChanged;
        computerPower.PowerStateChanged += OnPowerStateChanged;
    }

    private void OnDisable()
    {
        if (computerPower == null) return;
        computerPower.ConnectionsChanged -= OnComputerChanged;
        computerPower.PowerStateChanged -= OnPowerStateChanged;
    }

    private void OnComputerChanged() => EvaluateConditions();
    private void OnPowerStateChanged(PcPowerState unused) => EvaluateConditions();

    private void Update()
    {
        if (!requireFinalState) return;
        if (Time.unscaledTime < nextVerifyAt) return;
        nextVerifyAt = Time.unscaledTime + verifyIntervalSeconds;
        EvaluateConditions();
    }

    /// <summary>
    /// O PC está INTEIRO no estado final configurado? Só o resultado final conta: peças, cabos, sistema e rede,
    /// sem olhar a ordem em que o jogador fez as coisas.
    /// </summary>
    public bool FinalStateReached()
    {
        if (computerPower == null) return false;
        ComputerCase pcCase = computerPower.Case;
        if ((requiredFinalState & FinalState.AllParts) != 0 && pcCase.Components != ComputerCase.AllComponents) return false;
        if ((requiredFinalState & FinalState.PowerCables) != 0 &&
            !(computerPower.PowerConnected && computerPower.MonitorPowerConnected)) return false;
        if ((requiredFinalState & FinalState.VideoSignal) != 0 && !computerPower.VideoConnected) return false;
        if ((requiredFinalState & FinalState.OperatingSystem) != 0 &&
            computerPower.State != PcPowerState.OperatingSystem) return false;
        if ((requiredFinalState & FinalState.Internet) != 0 && !computerPower.Online) return false;
        if ((requiredFinalState & FinalState.CaseClosed) != 0 && pcCase.State != CaseState.Closed) return false;
        return true;
    }

    private void EvaluateConditions()
    {
        if (ticketManager == null || computerPower == null || !ticketManager.Running) return;

        foreach (TicketInstance ticket in ticketManager.Tickets)
        {
            if (ticket.Status != TicketStatus.InProgress || ticket.Definition == null ||
                ticket.Definition.ticketId != ticketId) continue;

            if (requireFinalState)
            {
                if (FinalStateReached()) CloseTicket(ticket);
                break;
            }

            // Condicoes observadas SOMENTE depois de uma mudanca real neste computador.
            CompleteIf(ticket, powerConnectedStepId, computerPower.PowerConnected);
            CompleteIf(ticket, videoConnectedStepId, computerPower.VideoConnected);
            CompleteIf(ticket, networkConnectedStepId, computerPower.NetworkConnected);
            CompleteIf(ticket, biosReachedStepId, computerPower.State == PcPowerState.Bios);
            CompleteIf(ticket, operatingSystemReachedStepId,
                computerPower.State == PcPowerState.OperatingSystem);
            break;
        }
    }

    /// <summary>
    /// "Só no final": conclui TODAS as etapas pendentes do chamado de uma vez. Enquanto o chamado não estiver em
    /// atendimento nada conta, então o jogador pode até montar o PC antes de aceitar — o fechamento vem depois.
    /// </summary>
    private void CloseTicket(TicketInstance ticket)
    {
        if (closingTicket) return;
        if (ticket.Definition.steps == null || ticket.Definition.steps.Count == 0)
        {
            Debug.LogWarning($"Chamado #{ticket.Number}: a definição não tem etapas, então não há como ser concluído.", this);
            return;
        }

        closingTicket = true;
        try
        {
            foreach (TicketStepDefinition step in ticket.Definition.steps)
            {
                if (step == null || string.IsNullOrWhiteSpace(step.id) || ticket.HasCompleted(step.id)) continue;
                ticketManager.CompleteStep(ticket.Number, step.id);
            }
            if (ticket.Status == TicketStatus.Completed)
                Debug.Log($"Chamado #{ticket.Number} resolvido: o PC está no estado final esperado.", this);
        }
        finally
        {
            closingTicket = false;
        }
    }

    private void CompleteIf(TicketInstance ticket, string stepId, bool condition)
    {
        if (condition && !string.IsNullOrWhiteSpace(stepId) &&
            ticket.Status == TicketStatus.InProgress && !ticket.HasCompleted(stepId))
        {
            if (ticketManager.CompleteStep(ticket.Number, stepId))
                Debug.Log($"Ticket #{ticket.Number}: etapa '{stepId}' concluida automaticamente.", this);
        }
    }
}
