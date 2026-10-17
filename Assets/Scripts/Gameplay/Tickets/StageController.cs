using System;
using UnityEngine;

public class StageController : MonoBehaviour
{
    [SerializeField] private TicketManager ticketManager;
    [Tooltip("0 = tutorial; 1 = primeira fase; 2+ = fases com fila")]
    [Min(0)] [SerializeField] private int stageNumber = 1;
    [Min(1f)] [SerializeField] private float durationMinutes = 15f;
    [SerializeField] private bool startAutomatically = true;

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
        if (ticketManager != null) ticketManager.TicketFinished += OnTicketFinished;
    }

    private void OnDisable()
    {
        if (ticketManager != null) ticketManager.TicketFinished -= OnTicketFinished;
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
        if (startAutomatically) StartStage();
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
