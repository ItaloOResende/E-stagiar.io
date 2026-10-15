using UnityEngine;

/// <summary>
/// Liga eventos reais do ComputerPower a etapas do ticket.
/// Anexe somente ao gabinete que deve atender ao chamado configurado.
/// IDs vazios significam que a regra está desativada.
/// </summary>
[RequireComponent(typeof(ComputerPower))]
public class ComputerPowerTicketBridge : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private ComputerPower computerPower;
    [SerializeField] private TicketManager ticketManager;

    [Header("Chamado deste computador")]
    [SerializeField] private string ticketId = "pc-nao-liga";

    [Header("Etapas concluidas por condicoes reais (deixe vazio para desativar)")]
    [SerializeField] private string powerConnectedStepId = "";
    [SerializeField] private string videoConnectedStepId = "";
    [SerializeField] private string networkConnectedStepId = "";
    [SerializeField] private string biosReachedStepId = "";
    [SerializeField] private string operatingSystemReachedStepId = "resolver-problema";

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

    private void EvaluateConditions()
    {
        if (ticketManager == null || computerPower == null || !ticketManager.Running) return;

        foreach (TicketInstance ticket in ticketManager.Tickets)
        {
            if (ticket.Status != TicketStatus.InProgress || ticket.Definition == null ||
                ticket.Definition.ticketId != ticketId) continue;

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
