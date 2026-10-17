using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Tipo de lançamento na pontuação. Valores novos no fim.</summary>
public enum ScoreEntryKind { TicketReward, Penalty, Bonus, LatePenalty }

/// <summary>Um lançamento na pontuação (positivo = ganho, negativo = penalidade).</summary>
public class ScoreEntry
{
    public ScoreEntryKind Kind { get; internal set; }
    public string Reason { get; internal set; }
    public int Points { get; internal set; }
    /// <summary>Número do chamado envolvido (0 = nenhum, ex.: infração).</summary>
    public int TicketNumber { get; internal set; }
    /// <summary>Ticket Id do chamado (vazio = nenhum).</summary>
    public string TicketId { get; internal set; } = "";
    /// <summary>Tipo da infração (null = o lançamento não é de infração).</summary>
    public InfractionKind? Infraction { get; internal set; }
    /// <summary>Ocorrência daquele tipo de infração na fase (1 = primeira; 0 = não se aplica).</summary>
    public int Occurrence { get; internal set; }
    public float Time { get; internal set; }
}

/// <summary>
/// Pontuação da fase: só ESCUTA os sistemas que já existem, sem detectar nada de novo.
///  - <b>Chamado concluído</b> (<see cref="TicketManager.TicketFinished"/>; com finalização explícita, só depois do
///    "Finalizar atendimento"): ganha o valor-base do chamado = soma dos pesos das etapas da ficha
///    (<see cref="TicketStepDefinition.weight"/>) × <see cref="pointsPerStepWeight"/>. Ver <see cref="BaseValue"/>.
///  - <b>Infração PRESENCIADA</b> pelo supervisor (<see cref="InfractionLog.Witnessed"/>, disparado uma única vez por
///    infração): penalidade progressiva por tipo, contada na fase (<see cref="repeatPenalties"/>: 1ª, 2ª, 3ª+).
///  - Gabinete incompleto ao concluir o chamado: <see cref="ApplyIncompleteCasePenalty"/> (chamado pelo PcTicketBridge).
///  - Entrega recusada com o equipamento aberto (todas as etapas feitas, <see cref="TicketDeviceBridge.DeliveryBlockerFor"/>):
///    <see cref="openEquipmentDeliveryPenalty"/> por tentativa.
/// <b>Pontuação possível</b> (<see cref="PossibleScore"/>): soma dos valores-base de todos os chamados que CHEGARAM na
/// fase (<see cref="TicketManager.TicketCreated"/>). Não diminui com erros; chamado não concluído conta contra.
/// <b>Estrelas</b>: percentual Score/Possível (0–100%) comparado a <see cref="starPercentThresholds"/>.
///  - <b>Atraso</b> (<see cref="TicketManager.TicketOverdue"/>): quando um chamado passa do prazo de referência (SLA)
///    perde <see cref="latePenalty"/> uma vez e mais <see cref="latePenaltyPerMinute"/> a cada minuto inteiro de atraso
///    enquanto continua aberto. O chamado nunca é bloqueado: concluído atrasado ainda ganha o valor-base.
/// Bônus de tempo: não implementado.
/// Não substitui nem conversa com as vidas do GDD (integração pendente).
/// </summary>
public class ScoreKeeper : MonoBehaviour
{
    [SerializeField] private TicketManager ticketManager;
    [Tooltip("Fase: zera a pontuação no início e calcula as estrelas no fim. Vazio = procura no mesmo objeto.")]
    [SerializeField] private StageController stageController;

    [Header("Chamados")]
    [Tooltip("Pontos por unidade de peso de etapa. Valor-base do chamado = soma dos pesos das etapas × este valor.")]
    [Min(0)] [SerializeField] private int pointsPerStepWeight = 10;
    [Tooltip("Pontos subtraídos se o chamado for concluído com o gabinete aberto ou sem parafusos obrigatórios. [A DEFINIR]")]
    [Min(0)] [SerializeField] private int incompleteCasePenalty;

    [Header("Finalização incompleta")]
    [Tooltip("Pontos perdidos a cada tentativa de finalizar um chamado com etapas pendentes (o chamado não é concluído).")]
    [Min(0)] [SerializeField] private int incompleteFinishPenalty = 5;
    [Tooltip("Pontos perdidos a cada tentativa de ENTREGAR um chamado com todas as etapas feitas, mas com o equipamento " +
             "ainda aberto (gabinete sem tampa/parafusos, notebook sem os parafusos). O chamado não é concluído.")]
    [Min(0)] [SerializeField] private int openEquipmentDeliveryPenalty = 5;

    [Header("Atraso (SLA)")]
    [Tooltip("Pontos perdidos quando um chamado passa do prazo de referência (uma vez por chamado).")]
    [Min(0)] [SerializeField] private int latePenalty = 10;
    [Tooltip("Pontos perdidos a cada minuto inteiro de atraso, enquanto o chamado continua aberto.")]
    [Min(0)] [SerializeField] private int latePenaltyPerMinute = 2;

    [Header("Bônus")]
    [Tooltip("Pontos por uso correto do testador de cabos (diagnosticar o cabo defeituoso do chamado ou verificar um cabo " +
             "crimpado). No máximo uma vez por chamado e por tipo; entra na pontuação possível dos chamados de rede.")]
    [Min(0)] [SerializeField] private int cableTestBonus = 5;

    [Header("Infrações presenciadas (reincidência por tipo, na fase)")]
    [Tooltip("Penalidade da 1ª, 2ª, 3ª... ocorrência do MESMO tipo. Além da lista, repete o último valor.")]
    [SerializeField] private List<int> repeatPenalties = new List<int> { 5, 10, 15 };

    [Header("Relatório")]
    [Tooltip("Cria o relatório final da fase (StageReportScreen) neste objeto, mostrado quando a fase termina.")]
    [SerializeField] private bool showStageReport = true;
    [Tooltip("Cria o selo da pontuação atual (ScoreHUD, alto à esquerda) neste objeto.")]
    [SerializeField] private bool showScoreHud = true;

    [Header("Estrelas (% da pontuação possível)")]
    [Tooltip("Percentual mínimo de cada estrela, em ordem crescente (padrão: 20, 70, 90; 1 estrela a partir de 20%).")]
    [SerializeField] private List<float> starPercentThresholds = new List<float> { 20f, 70f, 90f };

    public event Action<ScoreEntry> ScoreChanged;
    /// <summary>Fim da fase: pontuação final e estrelas (valores congelados em <see cref="FinalScore"/>/<see cref="FinalStars"/>).</summary>
    public event Action<int, int> StageScored;
    /// <summary>Uma infração presenciada acabou de ser penalizada (o aviso do HUD mostra os pontos perdidos).</summary>
    public static event Action<Infraction, ScoreEntry> InfractionPenalized;

    private readonly List<ScoreEntry> entries = new List<ScoreEntry>();
    private readonly Dictionary<InfractionKind, int> occurrences = new Dictionary<InfractionKind, int>();
    private readonly HashSet<Infraction> penalized = new HashSet<Infraction>();
    private readonly HashSet<TicketInstance> offered = new HashSet<TicketInstance>();
    private readonly HashSet<TicketInstance> rewarded = new HashSet<TicketInstance>();
    private readonly HashSet<string> bonusGiven = new HashSet<string>();
    private readonly Dictionary<Infraction, ScoreEntry> infractionEntries = new Dictionary<Infraction, ScoreEntry>();
    private readonly List<TicketInstance> offeredList = new List<TicketInstance>();
    private float stageStartedAt = -1f, stageEndedAt = -1f;

    public int Score { get; private set; }
    /// <summary>Soma dos valores-base dos chamados que chegaram nesta fase (antes de penalidades).</summary>
    public int PossibleScore { get; private set; }
    public IReadOnlyList<ScoreEntry> Entries => entries;
    public int MaxStars => starPercentThresholds.Count;
    public bool StageFinished { get; private set; }
    public int FinalScore { get; private set; }
    public int FinalStars { get; private set; }
    public float FinalPercent { get; private set; }
    /// <summary>Duração real da fase (s), do início ao fim (congelada no fim).</summary>
    public float StageSeconds => stageStartedAt < 0f ? 0f : (StageFinished ? stageEndedAt : UnityEngine.Time.time) - stageStartedAt;
    /// <summary>Time.time do início da fase (-1 = não começou): base do "momento" de cada lançamento no relatório.</summary>
    public float StageStartTime => stageStartedAt;
    /// <summary>Chamados que chegaram na fase, na ordem.</summary>
    public IReadOnlyList<TicketInstance> OfferedTickets => offeredList;
    /// <summary>Limites (%) das estrelas, em ordem crescente.</summary>
    public IReadOnlyList<float> StarThresholds => starPercentThresholds;
    /// <summary>Penalidade lançada para uma infração presenciada (null se não houve).</summary>
    public ScoreEntry EntryFor(Infraction inf) => inf != null && infractionEntries.TryGetValue(inf, out ScoreEntry e) ? e : null;

    /// <summary>Desempenho de 0 a 100. Sem pontuação possível (fase sem chamados) = 0.</summary>
    public float Percent => PossibleScore <= 0 ? 0f : Mathf.Clamp(100f * Score / PossibleScore, 0f, 100f);

    public int Stars
    {
        get
        {
            float p = Percent;
            int stars = 0;
            foreach (float t in starPercentThresholds) if (p >= t) stars++;
            return stars;
        }
    }

    /// <summary>Valor-base do chamado: soma dos pesos das etapas da ficha × <see cref="pointsPerStepWeight"/>. Determinístico.</summary>
    public int BaseValue(TicketDefinition d)
    {
        if (d == null || d.steps == null) return 0;
        int weight = 0;
        foreach (TicketStepDefinition s in d.steps)
            if (s != null && !string.IsNullOrWhiteSpace(s.id)) weight += Mathf.Max(1, s.weight);   // mesma regra do TicketInstance.Progress
        return weight * pointsPerStepWeight;
    }

    /// <summary>Penalidade da N-ésima ocorrência (1 = primeira) de um tipo de infração.</summary>
    public int PenaltyFor(int occurrence)
    {
        if (repeatPenalties.Count == 0 || occurrence < 1) return 0;
        return Mathf.Max(0, repeatPenalties[Mathf.Min(occurrence, repeatPenalties.Count) - 1]);
    }

    public int OccurrencesOf(InfractionKind kind) => occurrences.TryGetValue(kind, out int n) ? n : 0;

    private void Awake()
    {
        if (ticketManager == null) ticketManager = GetComponent<TicketManager>();
        if (stageController == null) stageController = GetComponent<StageController>();
        if (showStageReport && GetComponent<StageReportScreen>() == null) gameObject.AddComponent<StageReportScreen>();
        if (showScoreHud && GetComponent<ScoreHUD>() == null) gameObject.AddComponent<ScoreHUD>();
    }

    private void OnEnable()
    {
        if (ticketManager != null)
        {
            ticketManager.TicketCreated += OnTicketCreated;
            ticketManager.TicketFinished += OnTicketFinished;
            ticketManager.TicketOverdue += OnTicketOverdue;
            ticketManager.FinishRejected += OnFinishRejected;
        }
        if (stageController != null)
        {
            stageController.StageStarted += ResetScore;
            stageController.StageEnded += FinishStage;
        }
        InfractionLog.Witnessed += OnInfractionWitnessed;
    }

    private void OnDisable()
    {
        if (ticketManager != null)
        {
            ticketManager.TicketCreated -= OnTicketCreated;
            ticketManager.TicketFinished -= OnTicketFinished;
            ticketManager.TicketOverdue -= OnTicketOverdue;
            ticketManager.FinishRejected -= OnFinishRejected;
        }
        if (stageController != null)
        {
            stageController.StageStarted -= ResetScore;
            stageController.StageEnded -= FinishStage;
        }
        InfractionLog.Witnessed -= OnInfractionWitnessed;
    }

    /// <summary>Zera a pontuação, a pontuação possível e a contagem de reincidência (início de fase, testes).</summary>
    public void ResetScore()
    {
        Score = 0;
        PossibleScore = 0;
        entries.Clear();
        occurrences.Clear();
        penalized.Clear();
        offered.Clear();
        rewarded.Clear();
        bonusGiven.Clear();
        infractionEntries.Clear();
        offeredList.Clear();
        stageStartedAt = UnityEngine.Time.time;
        stageEndedAt = -1f;
        StageFinished = false;
        FinalScore = FinalStars = 0;
        FinalPercent = 0f;
    }

    /// <summary>Fim da fase: congela pontuação e estrelas. Depois disso, nada mais é somado até a próxima fase.</summary>
    public void FinishStage()
    {
        if (StageFinished) return;
        ChargeLateMinutes();
        if (stageStartedAt < 0f) stageStartedAt = 0f;
        stageEndedAt = UnityEngine.Time.time;
        StageFinished = true;
        FinalScore = Score;
        FinalPercent = Percent;
        FinalStars = Stars;
        Debug.Log($"[Pontuação] Fase encerrada: {FinalScore}/{PossibleScore} pontos ({FinalPercent:0.#}%), " +
                  $"{FinalStars}/{MaxStars} estrelas, {entries.Count} lançamentos.", this);
        StageScored?.Invoke(FinalScore, FinalStars);
    }

    private void OnTicketCreated(TicketInstance ticket)
    {
        if (StageFinished || ticket == null || !offered.Add(ticket)) return;
        offeredList.Add(ticket);
        int value = BaseValue(ticket.Definition);
        int bonus = ticket.Definition != null ? TestadorCabo.BonusPossiveis(ticket.Definition.ticketId) * cableTestBonus : 0;
        PossibleScore += value + bonus;
        Debug.Log($"[Pontuação] Chamado #{ticket.Number} ({ticket.Definition?.ticketId}) chegou: vale {value}" +
                  (bonus > 0 ? $" (+ até {bonus} de bônus do testador)" : "") + ". " +
                  $"Possível na fase: {PossibleScore}", this);
    }

    private void OnTicketFinished(TicketInstance ticket)
    {
        if (ticket == null || ticket.Status != TicketStatus.Completed || !rewarded.Add(ticket)) return;
        string title = ticket.Definition != null ? ticket.Definition.title : "chamado";
        Add(new ScoreEntry
        {
            Kind = ScoreEntryKind.TicketReward, Reason = $"Chamado concluído: {title}", Points = BaseValue(ticket.Definition),
            TicketNumber = ticket.Number, TicketId = ticket.Definition != null ? ticket.Definition.ticketId : ""
        });
    }

    // InfractionLog.Witnessed já dispara uma vez por infração (MarkWitnessed ignora as resolvidas); o HashSet é uma
    // segunda garantia, por referência (os Ids reiniciam no InfractionLog.ResetAll).
    private void OnInfractionWitnessed(Infraction infraction, Transform witness)
    {
        if (infraction == null || StageFinished || !penalized.Add(infraction)) return;
        int n = OccurrencesOf(infraction.Kind) + 1;
        occurrences[infraction.Kind] = n;
        var entry = new ScoreEntry
        {
            Kind = ScoreEntryKind.Penalty, Reason = $"{InfractionText(infraction.Kind)} ({infraction.ItemName})",
            Points = -PenaltyFor(n), Infraction = infraction.Kind, Occurrence = n
        };
        Add(entry);
        infractionEntries[infraction] = entry;
        InfractionPenalized?.Invoke(infraction, entry);
    }

    /// <summary>Descrição curta da infração para o jogador (aviso do supervisor, relatório).</summary>
    public static string InfractionText(InfractionKind kind)
    {
        switch (kind)
        {
            case InfractionKind.ComponentDropped: return "Objeto caiu";
            case InfractionKind.ComponentBroken: return "Equipamento quebrado na queda";
            case InfractionKind.ItemLeftOnFloor: return "Item largado no chão";
            default: return kind.ToString();
        }
    }

    // O chamado passou do prazo: penalidade única do atraso (os minutos seguintes são cobrados no Update).
    private void OnTicketOverdue(TicketInstance ticket)
    {
        if (ticket == null || StageFinished || ticket.IsClosed) return;
        AddLate(ticket, latePenalty, $"Chamado atrasado: {TitleOf(ticket)}");
    }

    // Uma tentativa recusada = um lançamento (o TicketManager dispara uma vez por clique).
    private void OnFinishRejected(TicketInstance ticket)
    {
        if (ticket == null || StageFinished) return;
        if (!string.IsNullOrEmpty(ticket.LastRejectReason))
        {
            if (openEquipmentDeliveryPenalty <= 0) return;
            Add(new ScoreEntry
            {
                Kind = ScoreEntryKind.Penalty, Reason = $"Entrega com o equipamento aberto: {TitleOf(ticket)}",
                Points = -openEquipmentDeliveryPenalty, Occurrence = ticket.RejectedDeliveryAttempts,
                TicketNumber = ticket.Number, TicketId = ticket.Definition != null ? ticket.Definition.ticketId : ""
            });
            return;
        }
        if (incompleteFinishPenalty <= 0) return;
        Add(new ScoreEntry
        {
            Kind = ScoreEntryKind.Penalty, Reason = $"Tentativa de finalizar com etapas pendentes: {TitleOf(ticket)}",
            Points = -incompleteFinishPenalty, Occurrence = ticket.RejectedFinishAttempts,
            TicketNumber = ticket.Number, TicketId = ticket.Definition != null ? ticket.Definition.ticketId : ""
        });
    }

    private void Update()
    {
        if (!StageFinished) ChargeLateMinutes();
    }

    // Cada minuto INTEIRO de atraso de um chamado aberto custa latePenaltyPerMinute (cobrado uma vez por minuto).
    private void ChargeLateMinutes()
    {
        if (latePenaltyPerMinute <= 0) return;
        foreach (TicketInstance t in offeredList)
        {
            if (t.IsClosed || !t.IsOverdue) continue;
            int minutes = Mathf.FloorToInt(t.OverdueSeconds / 60f);
            while (t.LateMinutesCharged < minutes)
            {
                t.LateMinutesCharged++;
                AddLate(t, latePenaltyPerMinute, $"Atraso de {t.LateMinutesCharged} min: {TitleOf(t)}");
            }
        }
    }

    private void AddLate(TicketInstance ticket, int points, string reason)
    {
        if (points <= 0) return;
        Add(new ScoreEntry
        {
            Kind = ScoreEntryKind.LatePenalty, Reason = reason, Points = -points,
            TicketNumber = ticket.Number, TicketId = ticket.Definition != null ? ticket.Definition.ticketId : ""
        });
    }

    private static string TitleOf(TicketInstance t) => t.Definition != null ? t.Definition.title : "chamado";

    /// <summary>
    /// Bônus ligado a um chamado (ex.: uso correto do testador de cabos). Concede uma vez só por chamado e por
    /// <paramref name="key"/>; retorna false se já tinha sido concedido, o chamado não existe ou a fase acabou.
    /// </summary>
    public bool AwardTicketBonus(TicketInstance ticket, string key, string reason)
    {
        if (ticket == null || StageFinished || string.IsNullOrEmpty(key) || !bonusGiven.Add(ticket.Number + ":" + key)) return false;
        Add(new ScoreEntry
        {
            Kind = ScoreEntryKind.Bonus, Reason = reason, Points = cableTestBonus,
            TicketNumber = ticket.Number, TicketId = ticket.Definition != null ? ticket.Definition.ticketId : ""
        });
        return true;
    }

    /// <summary>O chamado foi concluído com o computador incompleto (gabinete aberto ou sem parafusos obrigatórios).</summary>
    public void ApplyIncompleteCasePenalty(TicketInstance ticket)
    {
        if (ticket == null) return;
        Add(new ScoreEntry
        {
            Kind = ScoreEntryKind.Penalty, Reason = "Chamado concluído com o gabinete incompleto", Points = -incompleteCasePenalty,
            TicketNumber = ticket.Number, TicketId = ticket.Definition != null ? ticket.Definition.ticketId : ""
        });
    }

    private void Add(ScoreEntry entry)
    {
        if (StageFinished) return;   // depois do fim da fase o resultado não muda
        entry.Time = UnityEngine.Time.time;
        entries.Add(entry);
        Score += entry.Points;
        string kind = entry.Kind == ScoreEntryKind.TicketReward ? "Recompensa" : entry.Kind == ScoreEntryKind.Bonus ? "Bônus"
                    : entry.Kind == ScoreEntryKind.LatePenalty ? "Atraso" : "Penalidade";
        Debug.Log($"[Pontuação] {kind} — {entry.Reason}: {(entry.Points >= 0 ? "+" : "")}{entry.Points}" +
                  (entry.TicketNumber > 0 ? $" (chamado #{entry.TicketNumber} {entry.TicketId})" : "") +
                  (entry.Infraction.HasValue ? $" ({entry.Occurrence}ª ocorrência de {entry.Infraction.Value})" : "") +
                  $". Placar: {Score}/{PossibleScore} ({Percent:0.#}%, {Stars}/{MaxStars} estrelas)", this);
        ScoreChanged?.Invoke(entry);
    }
}
