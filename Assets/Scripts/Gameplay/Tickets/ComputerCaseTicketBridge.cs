using UnityEngine;

/// <summary>
/// Ao abrir o gabinete pelo minigame existente, conclui a etapa do ticket correspondente.
/// Ao concluir esse chamado com o gabinete incompleto (<see cref="ComputerCase.IsIncomplete"/>: tampa aberta ou
/// parafusos obrigatórios fora), avisa o <see cref="ScoreKeeper"/> (penalidade configurável lá).
/// </summary>
public class ComputerCaseTicketBridge : MonoBehaviour
{
    [SerializeField] private ComputerCase computerCase;
    [SerializeField] private TicketManager ticketManager;
    [SerializeField] private string ticketId = "pc-nao-liga";
    [SerializeField] private string stepId = "abrir-gabinete";
    [Tooltip("Opcional: penaliza concluir o chamado com o gabinete incompleto. Vazio = sem penalidade.")]
    [SerializeField] private ScoreKeeper scoreKeeper;

    private void Awake()
    {
        if (computerCase == null) computerCase = GetComponent<ComputerCase>();
    }

    private void OnEnable()
    {
        if (computerCase != null) computerCase.StateChanged += OnStateChanged;
        if (ticketManager != null) ticketManager.TicketFinished += OnTicketFinished;
    }

    private void OnDisable()
    {
        if (computerCase != null) computerCase.StateChanged -= OnStateChanged;
        if (ticketManager != null) ticketManager.TicketFinished -= OnTicketFinished;
    }

    private void OnTicketFinished(TicketInstance ticket)
    {
        if (scoreKeeper == null || computerCase == null || ticket.Definition == null || ticket.Definition.ticketId != ticketId) return;
        if (computerCase.IsIncomplete) scoreKeeper.ApplyIncompleteCasePenalty(ticket);
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
