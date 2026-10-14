using UnityEngine;

/// <summary>Ao abrir o gabinete pelo minigame existente, conclui a etapa do ticket correspondente.</summary>
public class ComputerCaseTicketBridge : MonoBehaviour
{
    [SerializeField] private ComputerCase computerCase;
    [SerializeField] private TicketManager ticketManager;
    [SerializeField] private string ticketId = "pc-nao-liga";
    [SerializeField] private string stepId = "abrir-gabinete";

    private void Awake()
    {
        if (computerCase == null) computerCase = GetComponent<ComputerCase>();
    }

    private void OnEnable()
    {
        if (computerCase != null) computerCase.StateChanged += OnStateChanged;
    }

    private void OnDisable()
    {
        if (computerCase != null) computerCase.StateChanged -= OnStateChanged;
    }

    private void OnStateChanged(CaseState state)
    {
        if (state != CaseState.Open || ticketManager == null) return;
        foreach (var ticket in ticketManager.Tickets)
        {
            if (ticket.Status == TicketStatus.InProgress &&
                ticket.Definition != null && ticket.Definition.ticketId == ticketId)
            {
                ticketManager.CompleteStep(ticket.Number, stepId);
                return;
            }
        }
    }
}
