using UnityEngine;

/// <summary>
/// Pontuação atual no ALTO À ESQUERDA da tela, durante a fase. Lê SÓ o <see cref="ScoreKeeper.Score"/> (a mesma fonte
/// do relatório final): não existe contador próprio. A cada lançamento (<see cref="ScoreKeeper.ScoreChanged"/>) mostra
/// por um instante o valor do lançamento (+N verde / −N vermelho) ao lado do total.
/// Criado pelo próprio <see cref="ScoreKeeper"/> no mesmo objeto (campo <i>Show Score Hud</i>); não precisa estar na cena.
/// Some com a fase encerrada, no tutorial (fase 0) e com o relatório final na frente. Mesmo selo do cronômetro
/// (<see cref="StageTimerHUD"/>, alto à direita): cartão arredondado do <see cref="HudEstilo"/>, rótulo "PONTOS" e o
/// total grande, na fonte do jogo (sem <see cref="textFont"/>, a do cronômetro no mesmo objeto).
/// </summary>
[DisallowMultipleComponent]
public class ScoreHUD : MonoBehaviour
{
    [SerializeField] private ScoreKeeper keeper;
    [SerializeField] private StageController stageController;
    [SerializeField] private TicketManager ticketManager;
    [Header("Visual (pixels da referência 1920 x 1080)")]
    [Tooltip("Distância da borda ESQUERDA da tela (a notificação de chamado novo do tablet fica alinhada a ela).")]
    [SerializeField, Min(0f)] private float leftMargin = 24f;
    [Tooltip("Segundos que o valor do último lançamento (+N/−N) fica ao lado do total.")]
    [SerializeField, Min(0.2f)] private float deltaSeconds = 2f;

    [SerializeField] private Color Ganho = new Color(0.40f, 0.90f, 0.50f);
    [SerializeField] private Color Perda = new Color(1f, 0.42f, 0.38f);

    [Header("Texto dos pontos")]
    [SerializeField] private string pointsLabel = "PONTOS";
    [Tooltip("Vazio = a fonte do cronômetro (StageTimerHUD) no mesmo objeto.")]
    [SerializeField] private Font textFont;
    [SerializeField] private Color textColor = new Color(0.96f, 0.98f, 1f, 1f);

    private int lastDelta;
    private float deltaAt = -100f;

    /// <summary>O selo está desenhado neste quadro.</summary>
    public bool Visible { get; private set; }
    /// <summary>Texto do total mostrado (testes).</summary>
    public string Text => keeper != null ? $"{pointsLabel}   {keeper.Score}" : "";

    private void Awake()
    {
        if (keeper == null) keeper = GetComponent<ScoreKeeper>();
        if (stageController == null) stageController = GetComponent<StageController>();
        if (ticketManager == null) ticketManager = GetComponent<TicketManager>();
    }

    private void OnEnable()
    {
        if (keeper == null) keeper = GetComponent<ScoreKeeper>();
        if (keeper != null) keeper.ScoreChanged += OnScoreChanged;
    }

    private void OnDisable()
    {
        if (keeper != null) keeper.ScoreChanged -= OnScoreChanged;
    }

    private Font Fonte
    {
        get
        {
            if (textFont != null) return textFont;
            var timer = GetComponent<StageTimerHUD>();
            return timer != null ? timer.Fonte : null;
        }
    }

    private void OnScoreChanged(ScoreEntry e)
    {
        if (e == null || e.Points == 0) return;
        // lançamentos no mesmo instante (ex.: atraso + finalização) somam no mesmo destaque
        lastDelta = Time.unscaledTime - deltaAt < 0.25f ? lastDelta + e.Points : e.Points;
        deltaAt = Time.unscaledTime;
    }

    private void OnGUI()
    {
        Visible = false;
        if (keeper == null || keeper.StageFinished || StageReportScreen.Showing) return;
        if (stageController != null && !stageController.IsRunning) return;
        if (ticketManager != null && ticketManager.StageNumber == 0) return;

        float e = HudEstilo.Escala;
        Font fonte = Fonte;
        Visible = true;

        string valor = keeper.Score.ToString();
        float w = Mathf.Max(HudEstilo.Largura(pointsLabel, valor, e, fonte), 130f * e);
        float h = HudEstilo.AlturaSelo * e;
        float x = leftMargin * e, y = (HudEstilo.AlturaFaixa + HudEstilo.MargemTopo) * e;
        HudEstilo.Selo(new Rect(x, y, w, h), pointsLabel, valor, e, fonte, false, textColor);

        // o último lançamento sobe e some ao lado do selo (fora da área da notificação, que fica embaixo)
        float t = Time.unscaledTime - deltaAt;
        if (t < deltaSeconds && lastDelta != 0)
        {
            Color c = lastDelta > 0 ? Ganho : Perda;
            c.a = Mathf.Clamp01((deltaSeconds - t) / 0.4f);
            string d = lastDelta > 0 ? $"+{lastDelta}" : $"−{-lastDelta}";
            HudEstilo.Texto(new Rect(x + w + 12f * e, y + 20f * e - t * 6f * e, 200f * e, h - 20f * e), d, c, e, fonte);
        }
    }
}
