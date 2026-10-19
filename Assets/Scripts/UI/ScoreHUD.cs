using UnityEngine;

/// <summary>
/// Pontuação atual no ALTO À ESQUERDA da tela, durante a fase. Lê SÓ o <see cref="ScoreKeeper.Score"/> (a mesma fonte
/// do relatório final): não existe contador próprio. A cada lançamento (<see cref="ScoreKeeper.ScoreChanged"/>) mostra
/// por um instante o valor do lançamento (+N verde / −N vermelho) ao lado do total.
/// Criado pelo próprio <see cref="ScoreKeeper"/> no mesmo objeto (campo <i>Show Score Hud</i>); não precisa estar na cena.
/// Some com a fase encerrada, no tutorial (fase 0) e com o relatório final na frente. Mesmo estilo e escala do selo do
/// cronômetro (<see cref="StageTimerHUD"/>, alto à direita) — os dois não se encostam.
/// </summary>
[DisallowMultipleComponent]
public class ScoreHUD : MonoBehaviour
{
    [SerializeField] private ScoreKeeper keeper;
    [SerializeField] private StageController stageController;
    [SerializeField] private TicketManager ticketManager;
    [Header("Visual (pixels da referência 1920 x 1080)")]
    [SerializeField, Min(10)] private int fontSize = 28;
    [SerializeField, Min(0f)] private float paddingX = 14f;
    [SerializeField, Min(0f)] private float paddingY = 8f;
    [SerializeField, Min(0f)] private float topMargin = 16f;
    [SerializeField, Min(0f)] private float leftMargin = 24f;
    [Tooltip("Segundos que o valor do último lançamento (+N/−N) fica ao lado do total.")]
    [SerializeField, Min(0.2f)] private float deltaSeconds = 2f;

    [SerializeField] private Color Backdrop = new Color(0.08f, 0.10f, 0.14f, 0.78f);
    [SerializeField] private Color Ganho = new Color(0.40f, 0.90f, 0.50f);
    [SerializeField] private Color Perda = new Color(1f, 0.42f, 0.38f);

    [Header("Texto dos pontos")]
    [SerializeField] private string pointsLabel = "PONTOS";
    [SerializeField] private Font textFont;
    [SerializeField] private FontStyle textFontStyle = FontStyle.Bold;
    [SerializeField] private Color textColor = Color.white;

    private GUIStyle style;
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

        float e = Mathf.Sqrt((Screen.width / 1920f) * (Screen.height / 1080f));
        int size = Mathf.Max(9, Mathf.RoundToInt(fontSize * e));
        if (style == null || style.fontSize != size)
        {
            style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft, fontSize = size, fontStyle = FontStyle.Bold, wordWrap = false,
                clipping = TextClipping.Overflow
            };
        }
        style.font = textFont;
        style.fontStyle = textFontStyle;
        Visible = true;

        string total = Text;
        Vector2 m = style.CalcSize(new GUIContent(total));
        float w = Mathf.Ceil(m.x + 2f * paddingX * e), h = Mathf.Ceil(m.y + 2f * paddingY * e);
        float x = leftMargin * e, y = topMargin * e;
        Color prev = GUI.color;
        GUI.color = Backdrop;
        GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
        GUI.color = prev;
        style.normal.textColor = textColor;
        GUI.Label(new Rect(x + paddingX * e, y, w, h), total, style);

        float t = Time.unscaledTime - deltaAt;
        if (t < deltaSeconds && lastDelta != 0)
        {
            Color c = lastDelta > 0 ? Ganho : Perda;
            c.a = Mathf.Clamp01((deltaSeconds - t) / 0.4f);
            style.normal.textColor = c;
            string d = lastDelta > 0 ? $"+{lastDelta}" : $"−{-lastDelta}";
            GUI.Label(new Rect(x + w + 10f * e, y - t * 6f * e, 200f * e, h), d, style);
            style.normal.textColor = textColor;
        }
    }
}
