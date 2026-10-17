using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tranca a gaveta (<see cref="OpenableInteractable"/> do mesmo objeto) enquanto houver um chamado EM ATENDIMENTO
/// cujo Ticket Id está em <see cref="lockedDuringTickets"/>. Uso atual: a gaveta de cabos de rede prontos
/// (Gaveteiro_Cabos) fica trancada no chamado <c>crimpar-cabo</c>, que pede um cabo NOVO crimpado pelo jogador.
/// Se a gaveta já estava aberta, ela fecha quando a trava começa a valer (só pode reabrir no fim do chamado).
/// O HUD mostra o motivo com o ícone de bloqueio; clicar só dá o tranco e o som de recusa.
/// </summary>
[RequireComponent(typeof(OpenableInteractable))]
public class DrawerTicketLock : MonoBehaviour, IOpenLock
{
    [SerializeField] private TicketManager ticketManager;
    [Tooltip("Ticket Ids que trancam esta gaveta enquanto estão em atendimento.")]
    [SerializeField] private List<string> lockedDuringTickets = new List<string> { "crimpar-cabo" };
    [SerializeField] private string reason = "Gaveta trancada durante este atendimento";

    private OpenableInteractable openable;
    private bool wasLocked;

    public string LockReason => reason;

    public bool IsLocked
    {
        get
        {
            if (ticketManager == null) ticketManager = FindAnyObjectByType<TicketManager>();
            if (ticketManager == null) return false;
            foreach (TicketInstance t in ticketManager.Tickets)
                if (t.Status == TicketStatus.InProgress && t.Definition != null && lockedDuringTickets.Contains(t.Definition.ticketId))
                    return true;
            return false;
        }
    }

    private void Awake() => openable = GetComponent<OpenableInteractable>();

    private void Update()
    {
        bool locked = IsLocked;
        if (locked && !wasLocked && openable.IsOpen) openable.SetOpen(false);   // começou a valer com ela aberta
        wasLocked = locked;
    }
}
