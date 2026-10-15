using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Tablet provisório para testar tickets, com simulação apenas no Editor.</summary>
public class TicketTabletPrototype : MonoBehaviour
{
    [SerializeField] private TicketManager ticketManager;
    [SerializeField] private StageController stageController;
    [SerializeField] private Key toggleKey = Key.T;
    [SerializeField] private InteractionDetector interactionDetector;
    [SerializeField] private Behaviour[] disableWhileTabletOpen;
#if UNITY_EDITOR
    [Header("Somente testes no Editor")]
    [SerializeField] private bool showDebugButtons = true;
#endif

    private bool visible;
    private CursorLockMode previousLockState;
    private bool previousCursorVisible;
    private Vector2 scroll;

    private void Awake()
    {
        if (ticketManager == null) ticketManager = GetComponent<TicketManager>();
        if (stageController == null) stageController = GetComponent<StageController>();
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current[toggleKey].wasPressedThisFrame)
            SetVisible(!visible);
    }

    private void SetVisible(bool show)
    {
        if (visible == show) return;
        visible = show;
        if (show)
        {
            previousLockState = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = previousLockState;
            Cursor.visible = previousCursorVisible;
        }

        if (interactionDetector != null) interactionDetector.enabled = !show;
        if (disableWhileTabletOpen != null)
            foreach (var behaviour in disableWhileTabletOpen)
                if (behaviour != null) behaviour.enabled = !show;
    }

    private void OnDisable()
    {
        if (visible) SetVisible(false);
    }

    private void OnGUI()
    {
        if (ticketManager == null) return;
        int pending = 0;
        foreach (var ticket in ticketManager.Tickets)
            if (ticket.Status == TicketStatus.Pending) pending++;

        if (!visible)
        {
            if (GUI.Button(new Rect(10, 50, 220, 42), $"[T] Tablet  |  Pendentes: {pending}")) SetVisible(true);
            return;
        }

        Color oldColor = GUI.color;
        // Informacao do ritmo da fase (sem abrir o Inspector).
        string nextText = ticketManager.SecondsUntilNextArrival >= 0f
            ? $"Proximo ticket estimado: {Mathf.CeilToInt(ticketManager.SecondsUntilNextArrival)}s"
            : (ticketManager.StageNumber == 1 ? "Fase 1: proximo ticket apos concluir" : "Aguardando inicio do atendimento");
        GUI.Box(new Rect(10, 50, 420, Mathf.Min(Screen.height - 60, 520)), "TABLET - Central de Suporte");
        if (GUI.Button(new Rect(372, 76, 48, 30), "X")) SetVisible(false);
        GUILayout.BeginArea(new Rect(25, 110, 390, Mathf.Max(60, Mathf.Min(Screen.height - 140, 445))));
        GUILayout.Label(nextText);
        scroll = GUILayout.BeginScrollView(scroll);

        foreach (var ticket in ticketManager.Tickets)
        {
            if (ticket.Definition == null) continue;
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label($"#{ticket.Number}  {ticket.Definition.title}");
            GUILayout.Label($"{ticket.Definition.requester} - {ticket.Definition.department}");
            GUILayout.Label(ticket.Definition.description);
            GUILayout.Label($"Status: {StatusText(ticket.Status)} | Progresso: {Mathf.RoundToInt(ticket.Progress * 100f)}%");

            Rect rect = GUILayoutUtility.GetRect(340, 16, GUILayout.ExpandWidth(true));
            GUI.Box(rect, "");
            GUI.color = new Color(0.18f, 0.75f, 0.38f);
            GUI.DrawTexture(new Rect(rect.x + 2, rect.y + 2,
                Mathf.Max(0, rect.width - 4) * ticket.Progress, rect.height - 4), Texture2D.whiteTexture);
            GUI.color = oldColor;

            if (ticket.Status == TicketStatus.Pending && GUILayout.Button("Iniciar atendimento", GUILayout.Height(32)))
                ticketManager.StartTicket(ticket.Number);

#if UNITY_EDITOR
            if (showDebugButtons && ticket.Status == TicketStatus.InProgress)
            {
                GUILayout.Space(6);
                GUILayout.Label("TESTE (somente Editor): simular minigame concluido");
                if (ticket.Definition.steps != null)
                {
                    foreach (var step in ticket.Definition.steps)
                    {
                        if (step == null || string.IsNullOrEmpty(step.id) || ticket.HasCompleted(step.id)) continue;
                        if (GUILayout.Button("Concluir: " + step.description, GUILayout.Height(28)))
                        {
                            ticketManager.CompleteStep(ticket.Number, step.id);
                            Debug.Log($"[TESTE] Etapa simulada: {step.id} | Ticket #{ticket.Number}", this);
                        }
                    }
                }
            }
#endif
            GUILayout.EndVertical();
        }

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private static string StatusText(TicketStatus status)
    {
        switch (status)
        {
            case TicketStatus.Pending: return "Pendente";
            case TicketStatus.InProgress: return "Em atendimento";
            case TicketStatus.Completed: return "Concluído";
            default: return status.ToString();
        }
    }
}
