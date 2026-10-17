using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Relatório final da fase (tela 2D). Aparece quando o <see cref="ScoreKeeper"/> encerra a fase
/// (<see cref="ScoreKeeper.StageScored"/>, disparado pelo fim do <see cref="StageController"/>) e mostra SÓ o que foi
/// registrado na partida:
///  - estrelas (<see cref="ScoreKeeper.FinalStars"/>, pelos limites do próprio ScoreKeeper), pontos e percentual;
///  - tempo da fase (<see cref="ScoreKeeper.StageSeconds"/>);
///  - chamados que chegaram (<see cref="ScoreKeeper.OfferedTickets"/>): concluídos no prazo, concluídos atrasados e não
///    concluídos (abertos no fim da fase);
///  - lançamentos do ScoreKeeper: infrações presenciadas (com a penalidade de cada uma), atrasos, gabinete incompleto,
///    bônus; e as infrações que aconteceram sem ninguém ver (<see cref="InfractionLog"/>), sem penalidade;
///  - o porquê das estrelas: percentual obtido × limites, e onde os pontos foram perdidos.
/// Monta a própria interface por código (formas geradas, fonte padrão da UI), no estilo cartunizado dos avisos do jogo.
/// Não precisa estar na cena: o próprio ScoreKeeper o cria no mesmo objeto (<see cref="ScoreKeeper"/>, campo Show Stage Report).
/// Enquanto aberto, desliga os controles do jogador e libera o cursor.
/// Botões: "Jogar novamente" (recarrega a cena no MESMO modo) e "Ir para o menu inicial" (recarrega a cena do zero e
/// abre o <see cref="MainMenuScreen"/>: nenhum dado da partida sobra). No <see cref="GameMode.SpeedRun"/> o topo mostra o
/// resultado do Speed Run (concluídos, tempo médio, ritmo e recorde) no lugar das estrelas.
/// </summary>
public class StageReportScreen : MonoBehaviour
{
    [SerializeField] private ScoreKeeper keeper;
    [SerializeField] private Font font;
    [Tooltip("Mostrar automaticamente quando a fase terminar.")]
    [SerializeField] private bool showOnStageEnd = true;

    private static readonly Color Ink = new Color(0.13f, 0.11f, 0.16f);
    private static readonly Color Cream = new Color(1f, 0.97f, 0.9f);
    private static readonly Color Paper = new Color(1f, 1f, 1f);
    private static readonly Color Blue = new Color(0.16f, 0.45f, 0.86f);
    private static readonly Color Gold = new Color(1f, 0.79f, 0.16f);
    private static readonly Color StarOff = new Color(0.78f, 0.76f, 0.74f);
    private static readonly Color Good = new Color(0.16f, 0.62f, 0.33f);
    private static readonly Color Warn = new Color(0.93f, 0.55f, 0.1f);
    private static readonly Color Bad = new Color(0.86f, 0.2f, 0.18f);
    private static readonly Color Muted = new Color(0.42f, 0.39f, 0.44f);

    /// <summary>Algum relatório está aberto (o cursor fica livre).</summary>
    public static bool Showing { get; private set; }

    private Canvas canvas;
    private RectTransform panel;
    private readonly List<RectTransform> stars = new List<RectTransform>();
    private readonly List<Behaviour> disabledControls = new List<Behaviour>();
    private float openedAt;
    private int earnedStars;
    private Sprite rounded, roundedThin, starSprite, circleSprite;

    /// <summary>Texto do resumo (\"por que N estrelas\") da última abertura, para testes.</summary>
    public string LastSummary { get; private set; } = "";
    /// <summary>"recebidos|no prazo|com atraso|não concluídos" da última abertura (testes de coerência).</summary>
    public string LastCounts { get; private set; } = "";
    public bool IsOpen => canvas != null && canvas.gameObject.activeSelf;

    private void Awake()
    {
        if (keeper == null) keeper = GetComponent<ScoreKeeper>();
    }

    private void OnEnable()
    {
        if (keeper == null) keeper = GetComponent<ScoreKeeper>();
        if (keeper != null) keeper.StageScored += OnStageScored;
    }

    private void OnDisable()
    {
        if (keeper != null) keeper.StageScored -= OnStageScored;
        if (IsOpen) Close();
    }

    private void OnStageScored(int score, int stars)
    {
        StageController sc = GetComponent<StageController>();
        speedRun = sc != null && sc.IsSpeedRun;
        if (speedRun)
        {
            int done = 0;
            foreach (TicketInstance t in keeper.OfferedTickets) if (t.Status == TicketStatus.Completed) done++;
            newRecord = GameSession.SubmitSpeedRun(done);
        }
        if (showOnStageEnd) Show();
    }

    private bool speedRun, newRecord;
    /// <summary>O último relatório foi de um Speed Run (testes).</summary>
    public bool IsSpeedRunReport => speedRun;
    /// <summary>Texto do resultado do Speed Run da última abertura (testes).</summary>
    public string LastSpeedRun { get; private set; } = "";

    // ================================================================== abrir / fechar

    /// <summary>Monta e mostra o relatório com os dados atuais do ScoreKeeper (normalmente já encerrado).</summary>
    public void Show()
    {
        if (keeper == null) return;
        if (canvas != null) Destroy(canvas.gameObject);
        stars.Clear();
        Build();
        openedAt = Time.unscaledTime;
        Showing = true;
        SetPlayerControls(false);
        UICursor.Reset();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        InteractionSounds.Raise(InteractionSound.DrawerOpened);
    }

    public void Close()
    {
        if (canvas != null) canvas.gameObject.SetActive(false);
        Showing = false;
        SetPlayerControls(true);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // Jogar de novo no mesmo modo: a cena é recarregada do zero (o modo fica em GameSession).
    private void Restart()
    {
        Showing = false;
        GameSession.ReloadActiveScene();
    }

    /// <summary>"Ir para o menu inicial": encerra a partida e recarrega a cena limpa, abrindo o menu inicial.</summary>
    public void GoToMainMenu()
    {
        Showing = false;
        GameSession.GoToMainMenu();
    }

    private void SetPlayerControls(bool on)
    {
        if (!on)
        {
            disabledControls.Clear();
            foreach (var b in FindObjectsByType<PlayerLook>(FindObjectsSortMode.None)) Disable(b);
            foreach (var b in FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None)) Disable(b);
            foreach (var b in FindObjectsByType<InteractionDetector>(FindObjectsSortMode.None)) Disable(b);
            return;
        }
        foreach (Behaviour b in disabledControls) if (b != null) b.enabled = true;
        disabledControls.Clear();
    }

    private void Disable(Behaviour b)
    {
        if (b == null || !b.enabled) return;
        b.enabled = false;
        disabledControls.Add(b);
    }

    // ================================================================== detalhes expansíveis (extrato completo / estrelas)

    private RectTransform detail;
    private CanvasGroup detailGroup;
    private float detailAt = -10f;
    private bool detailClosing;

    private void Clicavel(Image img, UnityEngine.Events.UnityAction action)
    {
        img.raycastTarget = true;
        if (img.sprite == null) img.color = new Color(1f, 1f, 1f, 0f);
        var b = img.gameObject.AddComponent<UnityEngine.UI.Button>();
        b.targetGraphic = img;
        ColorBlock cb = b.colors;
        cb.highlightedColor = new Color(0.92f, 0.92f, 0.92f); cb.pressedColor = new Color(0.8f, 0.8f, 0.8f);
        b.colors = cb;
        b.onClick.AddListener(action);
    }

    /// <summary>Abre o detalhe por cima do relatório (sem sair dele): extrato completo ou explicação das estrelas.</summary>
    private void OpenDetail(Data d, bool estrelas)
    {
        if (detail != null) Destroy(detail.gameObject);
        detail = New(estrelas ? "DetalheEstrelas" : "DetalheExtrato", panel);
        Stretch(detail);
        detailGroup = detail.gameObject.AddComponent<CanvasGroup>();
        detailGroup.alpha = 0f;
        Image dimmer = Box(detail, "Fundo", new Color(0.05f, 0.06f, 0.1f, 0.55f), rounded);
        Stretch(dimmer.rectTransform);
        Clicavel(dimmer, CloseDetail);   // clicar fora recolhe
        dimmer.color = new Color(0.05f, 0.06f, 0.1f, 0.55f);
        Image card = Box(detail, "Cartao", Cream, rounded);
        Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(1340f, 780f), Vector2.zero);
        card.raycastTarget = true;   // cliques no cartão não recolhem
        Label(Area(card.rectTransform, new Vector2(0.5f, 1f), new Vector2(1240f, 50f), new Vector2(0f, -46f)),
            estrelas ? $"POR QUE {d.stars} ESTRELA{(d.stars == 1 ? "" : "S")}?" : $"EXTRATO COMPLETO \u2022 {d.entries.Count} LANÇAMENTOS",
            32, FontStyle.Bold, Ink, TextAnchor.MiddleLeft);
        Button(card.rectTransform, "Recolher \u25B4", new Vector2(0f, 46f), Muted, CloseDetail);
        if (estrelas) BuildStarsDetail(card.rectTransform, d); else BuildFullLedger(card.rectTransform, d);
        detailAt = Time.unscaledTime;
        detailClosing = false;
    }

    private void CloseDetail()
    {
        if (detail == null || detailClosing) return;
        detailClosing = true;
        detailAt = Time.unscaledTime;
    }

    private void AnimateDetail()
    {
        if (detail == null) return;
        float k = Mathf.Clamp01((Time.unscaledTime - detailAt) / 0.22f);
        float a = detailClosing ? 1f - k : k;
        detailGroup.alpha = a;
        detail.localScale = Vector3.one * Mathf.Lerp(0.96f, 1f, a);
        if (detailClosing && k >= 1f) { Destroy(detail.gameObject); detail = null; }
    }

    // Todos os lançamentos, na ordem em que aconteceram, sem agrupar: ordem, momento, tipo, descrição e pontos.
    private void BuildFullLedger(RectTransform card, Data d)
    {
        RectTransform head = Area(card, new Vector2(0.5f, 1f), new Vector2(1240f, 30f), new Vector2(0f, -96f));
        Label(head, "  #      MOMENTO     TIPO                         DESCRIÇÃO", 18, FontStyle.Bold, Muted, TextAnchor.MiddleLeft);
        Label(head, "PONTOS  ", 18, FontStyle.Bold, Muted, TextAnchor.MiddleRight);

        RectTransform view = New("Lista", card);
        view.anchorMin = Vector2.zero; view.anchorMax = Vector2.one;
        view.offsetMin = new Vector2(50f, 100f); view.offsetMax = new Vector2(-50f, -116f);
        view.gameObject.AddComponent<RectMask2D>();
        Image hit = view.gameObject.AddComponent<Image>();
        hit.color = new Color(1f, 1f, 1f, 0f);   // recebe a rolagem do mouse
        RectTransform content = New("Conteudo", view);
        content.anchorMin = new Vector2(0f, 1f); content.anchorMax = new Vector2(1f, 1f); content.pivot = new Vector2(0.5f, 1f);
        const float h = 34f;
        content.sizeDelta = new Vector2(0f, Mathf.Max(1, d.entries.Count) * h);
        content.anchoredPosition = Vector2.zero;
        var scroll = view.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = view; scroll.content = content; scroll.horizontal = false; scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 34f; scroll.inertia = false;

        if (d.entries.Count == 0)
        {
            RectTransform vazio = RowAt(content, 0, h);
            Label(vazio, "Nenhum lançamento nesta fase.", 20, FontStyle.Italic, Muted, TextAnchor.MiddleLeft);
            return;
        }
        float t0 = keeper.StageStartTime < 0f ? 0f : keeper.StageStartTime;
        for (int i = 0; i < d.entries.Count; i++)
        {
            ScoreEntry e = d.entries[i];
            RectTransform line = RowAt(content, i, h);
            if (i % 2 == 0) Stretch(Box(line, "Zebra", new Color(Ink.r, Ink.g, Ink.b, 0.05f), null).rectTransform);
            string tipo = e.Kind == ScoreEntryKind.TicketReward ? "Chamado concluído" : e.Kind == ScoreEntryKind.Bonus ? "Bônus"
                        : e.Kind == ScoreEntryKind.LatePenalty ? "Atraso (SLA)" : e.Infraction.HasValue ? "Infração vista" : "Penalidade";
            string desc = e.Reason;
            if (e.Infraction.HasValue && e.Occurrence > 0) desc += $"  \u2022  {e.Occurrence}ª ocorrência do tipo";
            else if (!e.Infraction.HasValue && e.Kind == ScoreEntryKind.Penalty && e.Occurrence > 0) desc += $"  \u2022  {e.Occurrence}ª tentativa";
            if (e.TicketNumber > 0 && !desc.Contains("#")) desc = $"#{e.TicketNumber} " + desc;
            Color c = e.Points > 0 ? Good : e.Kind == ScoreEntryKind.LatePenalty ? Warn : e.Points < 0 ? Bad : Muted;
            Cell(line, $"{i + 1}", 0f, 50f, Muted, FontStyle.Bold);
            Cell(line, FormatTime(e.Time - t0), 60f, 100f, Ink, FontStyle.Normal);
            Cell(line, tipo, 175f, 230f, c, FontStyle.Bold);
            Text dt = Cell(line, desc, 410f, 690f, Ink, FontStyle.Normal);
            dt.resizeTextForBestFit = true; dt.resizeTextMinSize = 12; dt.resizeTextMaxSize = 19;
            string p = e.Points > 0 ? $"+{e.Points}" : e.Points < 0 ? $"\u2212{-e.Points}" : "0";
            Label(line, p + " pts  ", 20, FontStyle.Bold, c, TextAnchor.MiddleRight);
        }
    }

    private RectTransform RowAt(RectTransform content, int i, float h)
    {
        RectTransform rt = New("Lancamento" + (i + 1), content);
        rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(0f, -(i + 1) * h); rt.offsetMax = new Vector2(0f, -i * h);
        return rt;
    }

    private Text Cell(RectTransform line, string text, float x, float w, Color color, FontStyle style)
    {
        RectTransform a = New("Celula", line);
        a.anchorMin = new Vector2(0f, 0f); a.anchorMax = new Vector2(0f, 1f); a.pivot = new Vector2(0f, 0.5f);
        a.offsetMin = new Vector2(x + 8f, 0f); a.offsetMax = new Vector2(x + 8f + w, 0f);
        return Label(a, text, 19, style, color, TextAnchor.MiddleLeft);
    }

    // Explicação das estrelas: pontuação × máximo numa régua com as faixas, e o que somou/tirou (dados do ScoreKeeper).
    private void BuildStarsDetail(RectTransform card, Data d)
    {
        IReadOnlyList<float> limits = keeper.StarThresholds;
        Text big = Label(Area(card, new Vector2(0.5f, 1f), new Vector2(1240f, 64f), new Vector2(0f, -120f)),
            $"{d.score} de {d.possible} pts possíveis  \u2022  {d.percent:0.#}%", 40, FontStyle.Bold, Blue, TextAnchor.MiddleCenter);
        Outline(big, new Color(0f, 0f, 0f, 0.15f), 2f);

        // régua 0–100% com o preenchimento até o percentual e uma marca por estrela
        const float W = 1100f;
        Image track = Box(card, "Regua", new Color(Ink.r, Ink.g, Ink.b, 0.12f), roundedThin);
        Place(track.rectTransform, new Vector2(0.5f, 1f), new Vector2(W, 34f), new Vector2(0f, -200f));
        Image fill = Box(track.rectTransform, "Preenchido", Gold, roundedThin);
        fill.rectTransform.anchorMin = new Vector2(0f, 0f); fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(d.percent / 100f), 1f);
        fill.rectTransform.offsetMin = Vector2.zero; fill.rectTransform.offsetMax = Vector2.zero;
        for (int i = 0; i < limits.Count; i++)
        {
            float x = Mathf.Clamp01(limits[i] / 100f);
            Image tick = Box(track.rectTransform, "Faixa" + (i + 1), Ink, null);
            tick.rectTransform.anchorMin = tick.rectTransform.anchorMax = new Vector2(x, 0.5f);
            tick.rectTransform.sizeDelta = new Vector2(4f, 52f);
            bool got = d.percent >= limits[i];
            RectTransform lab = New("Rotulo", track.rectTransform);
            lab.anchorMin = lab.anchorMax = new Vector2(x, 0f); lab.pivot = new Vector2(0.5f, 1f);
            lab.sizeDelta = new Vector2(160f, 60f); lab.anchoredPosition = new Vector2(0f, -10f);
            Text faixaTxt = Label(Area(lab, new Vector2(0.5f, 1f), new Vector2(200f, 34f), new Vector2(0f, -18f)),
                $"{new string('\u2605', i + 1)}  \u2265 {limits[i]:0}%", 22, FontStyle.Bold, got ? Ink : Muted, TextAnchor.MiddleCenter);
            if (got) Outline(faixaTxt, Gold, 1f);
        }

        // o que somou e o que tirou
        Image plus = Box(card, "Somou", Paper, roundedThin);
        Place(plus.rectTransform, new Vector2(0.5f, 1f), new Vector2(560f, 300f), new Vector2(-300f, -470f));
        Image minus = Box(card, "Tirou", Paper, roundedThin);
        Place(minus.rectTransform, new Vector2(0.5f, 1f), new Vector2(560f, 300f), new Vector2(300f, -470f));
        Factors(plus.rectTransform, "AUMENTOU A NOTA", Good, new List<(string, string)>
        {
            ($"{d.completed} chamado(s) concluído(s)", $"+{d.rewards} pts"),
            ($"Bônus (testador de cabos)", $"+{d.bonus} pts"),
            ($"{d.onTime} no prazo \u2022 {d.late} com atraso", "")
        });
        int unfinished = d.unfinishedValue;
        Factors(minus.rectTransform, "REDUZIU A NOTA", Bad, new List<(string, string)>
        {
            ($"Infrações vistas ({d.infractions.Count})", Pts(d.infractionPts)),
            ($"Atrasos ({d.lateEntries.Count} lançamento(s))", Pts(d.latePts)),
            ($"Outras penalidades ({d.otherEntries.Count})", Pts(d.otherPts)),
            ($"{d.open} não concluído(s): pontos não ganhos", unfinished > 0 ? $"{unfinished} pts" : "0 pts")
        });
        Text rule = Label(Area(card, new Vector2(0.5f, 0f), new Vector2(1240f, 50f), new Vector2(0f, 118f)),
            "As estrelas comparam a pontuação final com a pontuação possível (o valor de todos os chamados recebidos, " +
            "mais os bônus possíveis). Chamado não concluído não tira pontos, mas o valor dele fica de fora.",
            18, FontStyle.Italic, Muted, TextAnchor.MiddleCenter);
        rule.resizeTextForBestFit = true; rule.resizeTextMinSize = 13; rule.resizeTextMaxSize = 18;
    }

    private static string Pts(int p) => p < 0 ? $"\u2212{-p} pts" : $"{p} pts";

    private void Factors(RectTransform box, string title, Color color, List<(string, string)> items)
    {
        Label(Area(box, new Vector2(0.5f, 1f), new Vector2(500f, 40f), new Vector2(0f, -32f)), title, 22, FontStyle.Bold, color, TextAnchor.MiddleLeft);
        for (int i = 0; i < items.Count; i++)
        {
            RectTransform line = Area(box, new Vector2(0.5f, 1f), new Vector2(500f, 44f), new Vector2(0f, -86f - i * 50f));
            Text a = Label(line, items[i].Item1, 20, FontStyle.Normal, Ink, TextAnchor.MiddleLeft);
            a.resizeTextForBestFit = true; a.resizeTextMinSize = 13; a.resizeTextMaxSize = 20;
            a.rectTransform.offsetMax = new Vector2(-120f, 0f);
            Label(line, items[i].Item2, 21, FontStyle.Bold, color, TextAnchor.MiddleRight);
        }
    }

    private string ShortSummary(Data d)
    {
        if (d.possible <= 0) return "Nenhum chamado chegou nesta fase.";
        var losses = new List<(int pts, string text)>();
        if (d.unfinishedValue > 0) losses.Add((d.unfinishedValue, $"{d.open} chamado(s) não concluído(s)"));
        if (d.latePts < 0) losses.Add((-d.latePts, "atrasos"));
        if (d.infractionPts < 0) losses.Add((-d.infractionPts, "infrações vistas"));
        if (d.otherPts < 0) losses.Add((-d.otherPts, "outras penalidades"));
        losses.Sort((a, b) => b.pts.CompareTo(a.pts));
        string motivo = losses.Count > 0 ? $"maior perda: {losses[0].text}" : "nenhum ponto perdido";
        return $"{d.score} de {d.possible} pts ({d.percent:0.#}%) \u2022 {motivo}";
    }

    // ================================================================== animação

    private void Update()
    {
        if (!IsOpen) return;
        AnimateDetail();
        float t = Time.unscaledTime - openedAt;
        float k = Mathf.Clamp01(t / 0.35f);
        panel.localScale = Vector3.one * Mathf.LerpUnclamped(0.85f, 1f, BackOut(k));
        for (int i = 0; i < stars.Count; i++)
        {
            // as estrelas ganhas "pulam" uma depois da outra
            float s = Mathf.Clamp01((t - 0.35f - i * 0.22f) / 0.3f);
            stars[i].localScale = Vector3.one * (i < earnedStars ? Mathf.LerpUnclamped(0f, 1f, BackOut(s)) : Mathf.Max(s, 0.001f));
            stars[i].localRotation = Quaternion.Euler(0f, 0f, (i - (stars.Count - 1) * 0.5f) * -8f);
        }
    }

    private static float BackOut(float x)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }

    // ================================================================== dados

    private struct Data
    {
        public int score, possible, stars, maxStars, offered, onTime, late, open, openLate, completed;
        public List<ScoreEntry> entries;
        public float percent, seconds;
        public int rewards, bonus, infractionPts, latePts, otherPts, unfinishedValue, missedInfractions;
        public List<ScoreEntry> infractions, lateEntries, otherEntries;
    }

    private Data Collect()
    {
        var d = new Data
        {
            score = keeper.StageFinished ? keeper.FinalScore : keeper.Score,
            possible = keeper.PossibleScore,
            percent = keeper.StageFinished ? keeper.FinalPercent : keeper.Percent,
            stars = keeper.StageFinished ? keeper.FinalStars : keeper.Stars,
            maxStars = keeper.MaxStars,
            seconds = keeper.StageSeconds,
            infractions = new List<ScoreEntry>(), lateEntries = new List<ScoreEntry>(), otherEntries = new List<ScoreEntry>()
        };
        foreach (TicketInstance t in keeper.OfferedTickets)
        {
            d.offered++;
            if (t.Status == TicketStatus.Completed)
            {
                if (t.CompletedLate) d.late++; else d.onTime++;
            }
            else
            {
                d.open++;
                if (t.IsOverdue) d.openLate++;
                d.unfinishedValue += keeper.BaseValue(t.Definition);
            }
        }
        foreach (ScoreEntry e in keeper.Entries)
        {
            switch (e.Kind)
            {
                case ScoreEntryKind.TicketReward: d.rewards += e.Points; break;
                case ScoreEntryKind.Bonus: d.bonus += e.Points; break;
                case ScoreEntryKind.LatePenalty: d.latePts += e.Points; d.lateEntries.Add(e); break;
                default:
                    if (e.Infraction.HasValue) { d.infractionPts += e.Points; d.infractions.Add(e); }
                    else { d.otherPts += e.Points; d.otherEntries.Add(e); }
                    break;
            }
        }
        d.missedInfractions = InfractionLog.TotalMissed;
        // cada chamado cai em UMA categoria: concluído no prazo, concluído com atraso ou não concluído
        d.completed = d.onTime + d.late;
        d.entries = new List<ScoreEntry>(keeper.Entries);
        return d;
    }

    private string Explain(Data d)
    {
        var sb = new StringBuilder();
        IReadOnlyList<float> limits = keeper.StarThresholds;
        if (d.possible <= 0)
            return "Nenhum chamado chegou nesta fase, então não houve pontuação possível para avaliar.";
        sb.Append($"Você fez {d.score} de {d.possible} pontos possíveis ({d.percent:0.#}%). ");
        if (limits.Count > 0)
        {
            sb.Append("Estrelas: ");
            for (int i = 0; i < limits.Count; i++)
                sb.Append($"{i + 1}★ a partir de {limits[i]:0}%").Append(i < limits.Count - 1 ? ", " : ". ");
            if (d.stars < limits.Count)
            {
                float need = limits[d.stars] - d.percent;
                sb.Append($"Faltaram {need:0.#} pontos percentuais para {d.stars + 1}★. ");
            }
            else sb.Append("Avaliação máxima! ");
        }
        // onde os pontos foram embora (só o que aconteceu de verdade)
        var losses = new List<(int pts, string text)>();
        if (d.unfinishedValue > 0) losses.Add((d.unfinishedValue, $"{d.open} chamado(s) não concluído(s) deixaram de render {d.unfinishedValue} pts"));
        if (d.latePts < 0) losses.Add((-d.latePts, $"atrasos custaram {-d.latePts} pts"));
        if (d.infractionPts < 0) losses.Add((-d.infractionPts, $"infrações vistas pelo supervisor custaram {-d.infractionPts} pts"));
        if (d.otherPts < 0) losses.Add((-d.otherPts, $"outras penalidades custaram {-d.otherPts} pts"));
        losses.Sort((a, b) => b.pts.CompareTo(a.pts));
        if (losses.Count > 0)
        {
            sb.Append("Maior perda: ").Append(losses[0].text).Append('.');
            for (int i = 1; i < losses.Count; i++) sb.Append(i == 1 ? " Também: " : "; ").Append(losses[i].text);
            if (losses.Count > 1) sb.Append('.');
        }
        else sb.Append("Nenhum ponto perdido: todos os chamados no prazo e sem infrações vistas.");
        return sb.ToString();
    }

    // ================================================================== montagem

    private void Build()
    {
        if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        rounded = RoundedSprite(22, 6);
        roundedThin = RoundedSprite(16, 3);
        starSprite = Sprite.Create(StarTexture(), new Rect(0, 0, 128, 128), new Vector2(0.5f, 0.5f));
        circleSprite = Sprite.Create(CircleTexture(), new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f));
        Data d = Collect();
        earnedStars = d.stars;
        LastSummary = Explain(d);
        LastCounts = $"{d.offered}|{d.onTime}|{d.late}|{d.open}";

        var go = new GameObject("RelatorioFase", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.transform.SetParent(transform, false);
        canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32500;   // acima do HUD e do celular
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        RectTransform root = (RectTransform)go.transform;

        Image dim = Box(root, "Fundo", new Color(0.05f, 0.06f, 0.1f, 0.78f), null);
        Stretch(dim.rectTransform);

        // cartão principal com borda grossa (estilo cartunizado) e sombra deslocada
        panel = New("Painel", root);
        Center(panel, new Vector2(1500f, 920f), Vector2.zero);
        Image shadow = Box(panel, "Sombra", new Color(0f, 0f, 0f, 0.45f), rounded);
        Stretch(shadow.rectTransform);
        shadow.rectTransform.anchoredPosition = new Vector2(12f, -14f);
        Image card = Box(panel, "Cartao", Cream, rounded);
        Stretch(card.rectTransform);

        // faixa do título
        Image ribbon = Box(panel, "Faixa", Blue, rounded);
        Place(ribbon.rectTransform, new Vector2(0.5f, 1f), new Vector2(760f, 96f), new Vector2(0f, 0f));
        Text title = Label(ribbon.rectTransform, speedRun ? "FIM DO SPEED RUN" : "FIM DO EXPEDIENTE", 50, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        Outline(title, Ink, 3f);

        if (speedRun) BuildSpeedRunHeader(d);
        // estrelas
        RectTransform starRow = New("Estrelas", panel);
        starRow.gameObject.SetActive(!speedRun);   // no Speed Run o critério é o número de concluídos
        Place(starRow, new Vector2(0.5f, 1f), new Vector2(600f, 170f), new Vector2(0f, -140f));
        int max = Mathf.Max(1, d.maxStars);
        for (int i = 0; i < max; i++)
        {
            float size = i == (max - 1) / 2 && max % 2 == 1 ? 150f : 120f;
            RectTransform holder = New("Estrela" + (i + 1), starRow);
            holder.anchorMin = holder.anchorMax = new Vector2((i + 0.5f) / max, 0.5f);
            holder.sizeDelta = new Vector2(size, size);
            holder.anchoredPosition = new Vector2(0f, size > 130f ? 10f : -6f);
            Image outline = Box(holder, "Contorno", Ink, starSprite);
            Stretch(outline.rectTransform);
            Image fill = Box(holder, "Preenchimento", i < d.stars ? Gold : StarOff, starSprite);
            Stretch(fill.rectTransform, 9f);
            if (i < d.stars)
            {
                Image shine = Box(fill.rectTransform, "Brilho", new Color(1f, 1f, 1f, 0.35f), circleSprite);
                Place(shine.rectTransform, new Vector2(0.38f, 0.62f), new Vector2(size * 0.16f, size * 0.16f), Vector2.zero);
            }
            stars.Add(holder);
        }
        string grade = d.stars >= max ? "Excelente atendimento!" : d.stars == max - 1 ? "Bom trabalho!" : d.stars >= 1 ? "Dá para melhorar" : "Expediente difícil";
        Text gradeText = Label(Area(panel, new Vector2(0.5f, 1f), new Vector2(900f, 46f), new Vector2(0f, -262f)),
            grade, 36, FontStyle.Bold, Ink, TextAnchor.MiddleCenter);
        gradeText.name = "Conceito";
        if (speedRun) gradeText.transform.parent.gameObject.SetActive(false);

        // coluna esquerda: números principais
        RectTransform left = New("Resumo", panel);
        Place(left, new Vector2(0f, 1f), new Vector2(640f, 320f), new Vector2(360f, -460f));
        Tile(left, 0, "PONTUAÇÃO", $"{d.score}", $"de {d.possible} possíveis • {d.percent:0.#}%", Blue);
        Tile(left, 1, "TEMPO DA FASE", FormatTime(d.seconds), $"{d.offered} recebido(s): {d.completed} concluído(s), {d.open} não", Muted);
        RectTransform tk = New("Chamados", left);
        Place(tk, new Vector2(0.5f, 0f), new Vector2(640f, 100f), new Vector2(0f, 50f));
        Counter(tk, 0, 3, d.onTime.ToString(), "concluídos no prazo", Good);
        Counter(tk, 1, 3, d.late.ToString(), "concluídos com atraso", Warn);
        Counter(tk, 2, 3, d.open.ToString(), d.openLate > 0 ? $"não concluídos · {d.openLate} fora do prazo" : "não concluídos", Bad);

        // coluna direita: extrato de pontos e penalidades
        Image ledgerBg = Box(panel, "Extrato", Paper, roundedThin);
        Place(ledgerBg.rectTransform, new Vector2(1f, 1f), new Vector2(720f, 320f), new Vector2(-400f, -460f));
        BuildLedger(ledgerBg.rectTransform, d);

        // por que estas estrelas
        // cartão-botão: resumo curto; o clique abre a explicação completa (DetalheEstrelas)
        Image why = Box(panel, "PorQue", new Color(1f, 0.91f, 0.68f), roundedThin);
        Place(why.rectTransform, new Vector2(0.5f, 0f), new Vector2(1400f, 120f), new Vector2(0f, 172f));
        Clicavel(why, () => OpenDetail(d, !speedRun));
        Text whyTitle = Label(Area(why.rectTransform, new Vector2(0f, 1f), new Vector2(520f, 34f), new Vector2(286f, -24f)),
            speedRun ? "COMO O SPEED RUN É AVALIADO" : $"POR QUE {d.stars} ESTRELA{(d.stars == 1 ? "" : "S")}?", 24, FontStyle.Bold, Ink, TextAnchor.MiddleLeft);
        Image ver = Box(why.rectTransform, "VerDetalhes", Blue, rounded);
        Place(ver.rectTransform, new Vector2(1f, 0.5f), new Vector2(250f, 56f), new Vector2(-150f, 0f));
        Outline(Label(ver.rectTransform, "Ver detalhes \u25B8", 22, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter), Ink, 1.5f);
        whyTitle.name = "PorQueTitulo";
        Text whyText = Label(Area(why.rectTransform, new Vector2(0f, 0.5f), new Vector2(1080f, 56f), new Vector2(570f, -18f)),
            speedRun ? "Vence quem CONCLUIR MAIS chamados antes do relógio zerar; o tempo médio por chamado desempata. " +
                       "Pontos e penalidades seguem as regras do expediente (extrato ao lado)." : ShortSummary(d),
            22, FontStyle.Normal, Ink, TextAnchor.MiddleLeft);
        whyText.resizeTextForBestFit = true; whyText.resizeTextMinSize = 14; whyText.resizeTextMaxSize = 21;

        // botões
        Button(panel, "Jogar novamente", new Vector2(-190f, 50f), Blue, Restart);
        Button(panel, "Ir para o menu inicial", new Vector2(190f, 50f), Muted, GoToMainMenu);
    }

    // Topo do relatório no Speed Run: concluídos (critério), tempo médio por atendimento, ritmo e recorde.
    private void BuildSpeedRunHeader(Data d)
    {
        float soma = 0f; int n = 0;
        foreach (TicketInstance t in keeper.OfferedTickets)
            if (t.Status == TicketStatus.Completed && t.ServiceSeconds >= 0f) { soma += t.ServiceSeconds; n++; }
        string media = n > 0 ? FormatTime(soma / n) : "--:--";
        float ritmo = d.seconds > 1f ? d.completed / (d.seconds / 60f) : 0f;
        int rec = GameSession.SpeedRunRecord;
        LastSpeedRun = $"{d.completed}|{media}|{ritmo:0.0}|{rec}";
        Text big = Label(Area(panel, new Vector2(0.5f, 1f), new Vector2(1200f, 110f), new Vector2(0f, -150f)),
            $"{d.completed} CHAMADO{(d.completed == 1 ? "" : "S")} CONCLUÍDO{(d.completed == 1 ? "" : "S")}", 72, FontStyle.Bold, Blue, TextAnchor.MiddleCenter);
        big.name = "SpeedRunConcluidos";
        Outline(big, Ink, 3f);
        Label(Area(panel, new Vector2(0.5f, 1f), new Vector2(1300f, 46f), new Vector2(0f, -232f)),
            $"tempo médio por chamado {media}  •  ritmo {ritmo:0.0} por minuto  •  " +
            (newRecord ? "NOVO RECORDE!" : $"recorde: {rec}"), 30, FontStyle.Bold, newRecord ? Warn : Ink, TextAnchor.MiddleCenter);
    }

    private void BuildLedger(RectTransform parent, Data d)
    {
        Label(Area(parent, new Vector2(0.5f, 1f), new Vector2(660f, 40f), new Vector2(0f, -28f)),
            "EXTRATO DA FASE", 26, FontStyle.Bold, Ink, TextAnchor.MiddleLeft);
        Image todos = Box(parent, "VerExtrato", Blue, roundedThin);
        Place(todos.rectTransform, new Vector2(1f, 1f), new Vector2(230f, 40f), new Vector2(-140f, -30f));
        Label(todos.rectTransform, $"Ver todos ({d.entries.Count}) \u25B8", 19, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        Clicavel(todos, () => OpenDetail(d, false));

        // lista empilhada de cima para baixo; o que não couber vira uma linha de resumo (com o total desses lançamentos)
        list = New("Lista", parent);
        list.anchorMin = Vector2.zero; list.anchorMax = Vector2.one;
        list.offsetMin = new Vector2(28f, 70f); list.offsetMax = new Vector2(-28f, -58f);
        listY = 0f;
        listLimit = 320f - 58f - 70f - 30f;   // reserva a linha de resumo
        hiddenCount = 0; hiddenPoints = 0;

        Row($"{d.completed} chamado(s) concluído(s)", d.rewards, Good, true);
        if (d.bonus != 0) Row("Bônus (testador de cabos)", d.bonus, Good, true);

        Header($"Infrações vistas pelo supervisor ({d.infractions.Count})");
        if (d.infractions.Count == 0) Note("Nenhuma infração vista.");
        foreach (ScoreEntry e in d.infractions)
            Row(e.Occurrence > 1 ? $"{e.Reason} • {e.Occurrence}ª vez" : e.Reason, e.Points, Bad, false);
        if (d.missedInfractions > 0) Note($"{d.missedInfractions} infração(ões) passaram despercebidas (sem penalidade).");

        Header("Atrasos (SLA)");
        if (d.lateEntries.Count == 0) Note("Nenhum chamado passou do prazo.");
        foreach (ScoreEntry e in d.lateEntries) Row($"#{e.TicketNumber} {e.Reason}", e.Points, Warn, false);

        if (d.otherEntries.Count > 0)
        {
            Header("Outras penalidades");
            foreach (ScoreEntry e in d.otherEntries) Row($"#{e.TicketNumber} {e.Reason}", e.Points, Bad, false);
        }
        if (hiddenCount > 0)
        {
            listLimit += 30f;   // a linha de resumo sempre cabe
            RectTransform mais = Row($"... e mais {hiddenCount} lançamento(s) \u25B8 ver todos", hiddenPoints, Bad, false);
            if (mais != null) Clicavel(mais.gameObject.AddComponent<Image>(), () => OpenDetail(d, false));
        }

        // total fixo no rodapé do extrato
        Image line = Box(parent, "Linha", new Color(Ink.r, Ink.g, Ink.b, 0.25f), null);
        Place(line.rectTransform, new Vector2(0.5f, 0f), new Vector2(660f, 3f), new Vector2(0f, 58f));
        Label(Area(parent, new Vector2(0.5f, 0f), new Vector2(660f, 44f), new Vector2(0f, 32f)),
            "TOTAL", 26, FontStyle.Bold, Ink, TextAnchor.MiddleLeft);
        Label(Area(parent, new Vector2(0.5f, 0f), new Vector2(660f, 44f), new Vector2(0f, 32f)),
            $"{d.score} pts", 26, FontStyle.Bold, d.score >= 0 ? Ink : Bad, TextAnchor.MiddleRight);
    }

    // ------------------------------------------------------------------ peças da interface

    private void Tile(RectTransform parent, int index, string caption, string value, string sub, Color accent)
    {
        Image bg = Box(parent, "Bloco_" + caption, Paper, roundedThin);
        Place(bg.rectTransform, new Vector2(0.5f, 1f), new Vector2(640f, 100f), new Vector2(0f, -50f - index * 110f));
        Image bar = Box(bg.rectTransform, "Cor", accent, roundedThin);
        bar.rectTransform.anchorMin = new Vector2(0f, 0f); bar.rectTransform.anchorMax = new Vector2(0f, 1f);
        bar.rectTransform.pivot = new Vector2(0f, 0.5f);
        bar.rectTransform.offsetMin = new Vector2(10f, 12f); bar.rectTransform.offsetMax = new Vector2(26f, -12f);
        Label(Area(bg.rectTransform, new Vector2(0f, 1f), new Vector2(300f, 26f), new Vector2(194f, -20f)),
            caption, 18, FontStyle.Bold, Muted, TextAnchor.MiddleLeft);
        Text v = Label(Area(bg.rectTransform, new Vector2(0f, 0.5f), new Vector2(300f, 56f), new Vector2(194f, -10f)),
            value, 44, FontStyle.Bold, accent, TextAnchor.MiddleLeft);
        Outline(v, new Color(0f, 0f, 0f, 0.15f), 2f);
        Text subT = Label(Area(bg.rectTransform, new Vector2(1f, 0.5f), new Vector2(380f, 56f), new Vector2(-210f, -8f)),
            sub, 21, FontStyle.Normal, Ink, TextAnchor.MiddleRight);
        subT.resizeTextForBestFit = true; subT.resizeTextMinSize = 14; subT.resizeTextMaxSize = 21;
    }

    private void Counter(RectTransform parent, int i, int n, string value, string caption, Color color)
    {
        RectTransform cell = New("Contador_" + caption, parent);
        cell.anchorMin = new Vector2((float)i / n, 0f); cell.anchorMax = new Vector2((float)(i + 1) / n, 1f);
        cell.offsetMin = new Vector2(i == 0 ? 0f : 6f, 0f); cell.offsetMax = new Vector2(i == n - 1 ? 0f : -6f, 0f);
        Image bg = Box(cell, "Fundo", color, roundedThin);
        Stretch(bg.rectTransform);
        Text v = Label(Area(cell, new Vector2(0.5f, 1f), new Vector2(200f, 56f), new Vector2(0f, -32f)),
            value, 46, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        Outline(v, Ink, 2.5f);
        Text c = Label(Area(cell, new Vector2(0.5f, 0f), new Vector2(200f, 36f), new Vector2(0f, 20f)),
            caption, 18, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        c.resizeTextForBestFit = true; c.resizeTextMinSize = 12; c.resizeTextMaxSize = 18;
    }

    private RectTransform list;
    private float listY, listLimit;
    private int hiddenCount, hiddenPoints;

    private void Header(string text)
    {
        RectTransform line = Line(36f);
        if (line != null) Label(line, text, 21, FontStyle.Bold, Blue, TextAnchor.LowerLeft).name = "Secao";
    }

    private void Note(string text)
    {
        RectTransform line = Line(26f);
        if (line != null) Label(line, text, 17, FontStyle.Italic, Muted, TextAnchor.MiddleLeft);
    }

    private RectTransform Row(string text, int points, Color color, bool positive)
    {
        RectTransform line = Line(30f);
        if (line == null) { hiddenCount++; hiddenPoints += points; return null; }
        Text a = Label(line, text, 19, FontStyle.Normal, Ink, TextAnchor.MiddleLeft);
        a.rectTransform.offsetMax = new Vector2(-120f, 0f);
        a.resizeTextForBestFit = true; a.resizeTextMinSize = 13; a.resizeTextMaxSize = 19;
        string p = points > 0 ? $"+{points}" : points < 0 ? $"−{-points}" : "0";
        Label(line, p + " pts", 20, FontStyle.Bold, points < 0 ? color : positive ? Good : Muted, TextAnchor.MiddleRight);
        return line;
    }

    // Próxima linha da lista (faixa de 'height' px abaixo da anterior); null se não cabe mais.
    private RectTransform Line(float height)
    {
        if (listY + height > listLimit) return null;
        RectTransform rt = New("Linha", list);
        rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(0f, -listY - height); rt.offsetMax = new Vector2(0f, -listY);
        listY += height;
        return rt;
    }

    private void Button(RectTransform parent, string text, Vector2 pos, Color color, UnityEngine.Events.UnityAction action)
    {
        Image bg = Box(parent, "Botao_" + text, color, rounded);
        Place(bg.rectTransform, new Vector2(0.5f, 0f), new Vector2(300f, 72f), pos);
        bg.raycastTarget = true;
        var b = bg.gameObject.AddComponent<UnityEngine.UI.Button>();
        b.targetGraphic = bg;
        ColorBlock cb = b.colors;
        cb.highlightedColor = new Color(1.1f, 1.1f, 1.1f); cb.pressedColor = new Color(0.8f, 0.8f, 0.8f);
        b.colors = cb;
        b.onClick.AddListener(action);
        Text t = Label(bg.rectTransform, text, 28, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        Outline(t, Ink, 2f);
    }

    private static string FormatTime(float s)
    {
        int total = Mathf.Max(0, Mathf.RoundToInt(s));
        return $"{total / 60}:{total % 60:00}";
    }

    // ------------------------------------------------------------------ utilitários de RectTransform

    private static RectTransform New(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static void Stretch(RectTransform rt, float inset = 0f)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset); rt.offsetMax = new Vector2(-inset, -inset);
    }

    private static void Center(RectTransform rt, Vector2 size, Vector2 offset)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size; rt.anchoredPosition = offset;
    }

    private static void Place(RectTransform rt, Vector2 anchor, Vector2 size, Vector2 pos)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size; rt.anchoredPosition = pos;
    }

    private static RectTransform Area(RectTransform parent, Vector2 anchor, Vector2 size, Vector2 pos)
    {
        RectTransform rt = New("Area", parent);
        Place(rt, anchor, size, pos);
        return rt;
    }

    private Image Box(Transform parent, string name, Color color, Sprite sprite)
    {
        RectTransform rt = New(name, parent);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.type = sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        img.color = color;
        img.raycastTarget = name == "Fundo";   // o fundo segura os cliques do mundo
        return img;
    }

    private Text Label(RectTransform parent, string text, int size, FontStyle style, Color color, TextAnchor anchor)
    {
        RectTransform rt = New("Texto", parent);
        Stretch(rt);
        var t = rt.gameObject.AddComponent<Text>();
        t.font = font; t.text = text; t.fontSize = size; t.fontStyle = style; t.color = color; t.alignment = anchor;
        t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Truncate;
        t.raycastTarget = false;
        return t;
    }

    private static void Outline(Text t, Color c, float d)
    {
        var o = t.gameObject.AddComponent<UnityEngine.UI.Outline>();
        o.effectColor = c; o.effectDistance = new Vector2(d, -d);
    }

    // ------------------------------------------------------------------ formas geradas (sem assets)

    // Retângulo arredondado branco com borda escura (9-slice). A cor do Image multiplica tudo: o miolo fica na cor
    // pedida e a borda continua escura (contorno cartunizado) em qualquer cor.
    private static Sprite RoundedSprite(int r, int bw)
    {
        int n = r * 2 + 8;
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float cx = Mathf.Clamp(x, r, n - 1 - r), cy = Mathf.Clamp(y, r, n - 1 - r);
                float dd = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
                float a = Mathf.Clamp01(r - dd + 0.5f);
                float edge = Mathf.Clamp01(dd - (r - bw) + 0.5f);
                Color c = Color.Lerp(Color.white, new Color(0.18f, 0.16f, 0.22f), edge);
                c.a = a;
                t.SetPixel(x, y, c);
            }
        t.Apply();
        return Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
    }

    private static Texture2D StarTexture()
    {
        const int n = 128;
        var pts = new Vector2[10];
        for (int i = 0; i < 10; i++)
        {
            float ang = Mathf.PI / 2f + i * Mathf.PI / 5f;
            float rad = (i % 2 == 0 ? 0.5f : 0.24f) * (n - 4);
            pts[i] = new Vector2(n / 2f + Mathf.Cos(ang) * rad, n / 2f - 4f + Mathf.Sin(ang) * rad);
        }
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                // 4 amostras por pixel: borda suave
                int inside = 0;
                for (int s = 0; s < 4; s++)
                    if (InPolygon(new Vector2(x + 0.25f + (s % 2) * 0.5f, y + 0.25f + (s / 2) * 0.5f), pts)) inside++;
                t.SetPixel(x, y, new Color(1f, 1f, 1f, inside / 4f));
            }
        t.Apply();
        return t;
    }

    private static bool InPolygon(Vector2 p, Vector2[] poly)
    {
        bool c = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x) c = !c;
        return c;
    }

    private static Texture2D CircleTexture()
    {
        const int n = 64;
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dd = Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f));
                t.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(31.5f - dd)));
            }
        t.Apply();
        return t;
    }
}
