using System;
using UnityEngine;

public class StageController : MonoBehaviour
{
    [SerializeField] private TicketManager ticketManager;
    [Tooltip("0 = tutorial; 1 = primeira fase; 2+ = fases com fila")]
    [Min(0)] [SerializeField] private int stageNumber = 1;
    [Min(1f)] [SerializeField] private float durationMinutes = 15f;
    [SerializeField] private bool startAutomatically = true;
    [Tooltip("Abrir o MENU INICIAL (escolha do modo) antes de começar, em vez de começar direto. Voltar ao menu pelo " +
             "relatório final sempre abre o menu, com este campo ligado ou não.")]
    [SerializeField] private bool showStartMenu;

    public event Action<float> TimeChanged;
    /// <summary>A fase começou (<see cref="StartStage"/>): a pontuação zera aqui.</summary>
    public event Action StageStarted;
    public event Action StageEnded;

    public float SecondsRemaining { get; private set; }
    public float DurationSeconds => durationMinutes * 60f;
    public bool IsRunning { get; private set; }

    private void Awake()
    {
        if (ticketManager == null) ticketManager = GetComponent<TicketManager>();
    }

    private void OnEnable()
    {
        if (ticketManager == null) ticketManager = GetComponent<TicketManager>();
        if (ticketManager != null)
        {
            ticketManager.TicketFinished += OnTicketFinished;
            ticketManager.AllWorkDone += EndStageNow;   // nada pendente e nada mais por vir: encerra (relatório)
        }
    }

    private void OnDisable()
    {
        if (ticketManager != null)
        {
            ticketManager.TicketFinished -= OnTicketFinished;
            ticketManager.AllWorkDone -= EndStageNow;
        }
    }

    private void OnTicketFinished(TicketInstance ticket)
    {
        if (stageNumber != 0 || !IsRunning) return;
        IsRunning = false;
        ticketManager.EndStage();
        StageEnded?.Invoke();
        Debug.Log("Tutorial concluido!", this);
    }

    private void Start()
    {
        // "Começar o expediente" (fim do tutorial): começa direto, sem o menu inicial
        if (GameSession.ConsumeStartRequest()) { GameSession.ConsumeMenuRequest(); StartStageWithMode(GameMode.Normal); return; }
        // pedido do relatório ("Ir para o menu inicial") ou cena configurada para abrir no menu
        if (GameSession.ConsumeMenuRequest() || showStartMenu) { MainMenuScreen.Open(this); return; }
        if (startAutomatically) StartStage();
    }

    /// <summary>Escolha do menu inicial: define o modo e começa a fase.</summary>
    public void StartStageWithMode(GameMode mode)
    {
        GameSession.Mode = mode;
        StartStage();
    }

    public void StartStage()
    {
        if (ticketManager == null)
        {
            Debug.LogError("StageController: atribua TicketManager no Inspector.", this);
            return;
        }
        SecondsRemaining = DurationSeconds;
        IsRunning = true;
        StageStarted?.Invoke();
        ticketManager.BeginStage(stageNumber, SecondsRemaining);
        TimeChanged?.Invoke(SecondsRemaining);
    }

    /// <summary>
    /// TESTE (ver o relatório final de uma vez): conclui TODOS os chamados que já chegaram — aceita, cumpre as etapas e
    /// entrega pelo fluxo normal (TryFinishTicket: a pontuação é a do próprio jogo) — e encerra a fase. Chamado cuja
    /// entrega o jogo recusar (equipamento aberto etc.) fica aberto e aparece como não concluído. Só pelo menu de contexto.
    /// </summary>
    [ContextMenu("TESTE: Concluir chamados e encerrar (ver relatório)")]
    public void TesteConcluirTudoEEncerrar()
    {
        if (!IsRunning || ticketManager == null) return;
        int concluidos = 0;
        foreach (TicketInstance t in new System.Collections.Generic.List<TicketInstance>(ticketManager.Tickets))
        {
            if (t == null || t.IsClosed || t.Definition == null) continue;
            if (t.Status == TicketStatus.Pending) ticketManager.StartTicket(t.Number);
            foreach (TicketStepDefinition step in t.Definition.steps)
                if (step != null && !t.HasCompleted(step.id)) ticketManager.CompleteStep(t.Number, step.id);
            if (!t.IsClosed && ticketManager.TryFinishTicket(t.Number)) concluidos++;
            else if (t.IsClosed) concluidos++;
        }
        Debug.Log($"TESTE: {concluidos} chamado(s) concluído(s); encerrando a fase.", this);
        EndStageNow();
    }

    /// <summary>Encerra a fase agora (como se o tempo tivesse acabado): pontuação final e relatório.</summary>
    [ContextMenu("TESTE: Encerrar a fase agora")]
    public void EndStageNow()
    {
        if (!IsRunning) return;
        IsRunning = false;
        ticketManager.EndStage();
        StageEnded?.Invoke();
        Debug.Log("Fase encerrada manualmente.", this);
    }

    private void Update()
    {
        if (!IsRunning) return;
        if (stageNumber == 0) return; // Tutorial sem relogio.
        SecondsRemaining = Mathf.Max(0f, SecondsRemaining - Time.deltaTime);
        ticketManager.UpdateStageTime(SecondsRemaining);
        TimeChanged?.Invoke(SecondsRemaining);
        if (SecondsRemaining > 0f) return;
        IsRunning = false;
        ticketManager.EndStage();
        StageEnded?.Invoke();
        Debug.Log("Tempo da fase encerrado.", this);
    }
}
