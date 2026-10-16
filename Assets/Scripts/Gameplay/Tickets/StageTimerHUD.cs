using UnityEngine;

/// <summary>
/// HUD provisoria da fase, no ALTO DA DIREITA. Usa o tempo real do StageController.
/// Mostra SO o nome da fase e o cronometro decrescendo — a barra de progresso da fase NAO existe mais.
/// Nao cria um segundo temporizador nem modifica os minigames.
/// O tempo de CADA chamado é outro: veja <see cref="TicketManager"/> e a barra da lista do celular.
/// <para>
/// O RETANGULO ESCURO E DO TAMANHO DO TEXTO (mais <see cref="paddingX"/>/<see cref="paddingY"/> de respiro):
/// nada de barra atravessando o topo. O selo fica À ESQUERDA do botão de fechar das telas 2D (todos eles ficam
/// nos 88 px do canto superior direito: 24 de margem + 64x64), por isso <see cref="rightMargin"/> começa em 100:
/// o selo para 12 px antes do botão e não atrapalha nem o botão nem o conteúdo do meio das telas.
/// </para>
/// </summary>
[DisallowMultipleComponent]
public class StageTimerHUD : MonoBehaviour
{
    [SerializeField] private StageController stageController;
    [SerializeField] private TicketManager ticketManager;
    [Header("Visual (pixels da referência 1920 x 1080)")]
    [Tooltip("Tamanho da LETRA do selo. O retangulo de fundo cresce junto, sempre colado no texto.")]
    [SerializeField, Min(10f)] private int fontSize = 28;
    [Tooltip("Respiro entre o texto e a borda do retangulo (horizontal).")]
    [SerializeField, Min(0f)] private float paddingX = 14f;
    [Tooltip("Respiro entre o texto e a borda do retangulo (vertical).")]
    [SerializeField, Min(0f)] private float paddingY = 8f;
    [SerializeField, Min(0f)] private float topMargin = 16f;
    [Tooltip("Distancia da borda DIREITA da tela. O padrao (100) deixa o selo FORA do botao de fechar das telas.")]
    [SerializeField, Min(0f)] private float rightMargin = 100f;

    /// <summary>Resolução de referência das telas 2D do jogo (o selo acompanha a mesma escala).</summary>
    private const float RefLargura = 1920f;
    private const float RefAltura = 1080f;

    private static readonly Color Backdrop = new Color(0.08f, 0.10f, 0.14f, 0.78f);

    private GUIStyle labelStyle;

    private void Awake()
    {
        if (stageController == null) stageController = GetComponent<StageController>();
        if (ticketManager == null) ticketManager = GetComponent<TicketManager>();
    }

    private void OnGUI()
    {
        if (stageController == null || ticketManager == null) return;
        int stage = ticketManager.StageNumber;
        if (stage == 0) return; // Tutorial sem cronometro.

        float duration = Mathf.Max(1f, stageController.DurationSeconds);
        float remaining = Mathf.Clamp(stageController.SecondsRemaining, 0f, duration);
        int seconds = Mathf.CeilToInt(remaining);
        string text = $"FASE {stage}   |   {seconds / 60:00}:{seconds % 60:00}";

        // A escala acompanha o CanvasScaler das telas do jogo (referência 1920x1080), então o selo fica do
        // MESMO tamanho relativo em qualquer resolução — como o celular do jogador.
        float e = Escala;
        GarantirEstilo(e);

        // O retangulo e medido PELO TEXTO: o fundo e sempre um selo colado na letra.
        Vector2 medida = labelStyle.CalcSize(new GUIContent(text));
        float w = Mathf.Ceil(medida.x + 2f * paddingX * e);
        float h = Mathf.Ceil(medida.y + 2f * paddingY * e);
        float x = Screen.width - w - rightMargin * e;   // alto da DIREITA, à esquerda do botão de fechar
        float y = topMargin * e;

        DrawRect(new Rect(x, y, w, h), Backdrop);
        GUI.Label(new Rect(x + paddingX * e, y + paddingY * e, w - 2f * paddingX * e, h - 2f * paddingY * e),
            text, labelStyle);
    }

    /// <summary>A mesma escala que o CanvasScaler das telas do jogo usa (referência 1920 x 1080).</summary>
    private static float Escala => Mathf.Sqrt((Screen.width / RefLargura) * (Screen.height / RefAltura));

    /// <summary>Monta o estilo uma vez por escala de tela (mudar o tamanho da fonte a cada quadro é caro).</summary>
    private void GarantirEstilo(float escala)
    {
        int tamanho = Mathf.Max(9, Mathf.RoundToInt(fontSize * escala));
        if (labelStyle != null && labelStyle.fontSize == tamanho) return;

        labelStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = tamanho,
            fontStyle = FontStyle.Bold,
            wordWrap = false,
            clipping = TextClipping.Clip
        };
        labelStyle.normal.textColor = Color.white;
    }

    private static void DrawRect(Rect rect, Color color)
    {
        Color previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previous;
    }
}

/// <summary>
/// A barra de TEMPO dos chamados — usada em cada chamado da lista do celular (o que resta daQUELE chamado):
/// fundo escuro, preenchimento que ENCOLHE conforme o tempo acaba e cor
/// que muda com o que resta (verde → amarelo → laranja → vermelho). Ela toma o lugar da antiga barra
/// de progresso das etapas, que não existe mais. (A fase NAO usa mais barra: so cronometro e nome.)
/// </summary>
public static class TicketTimeBar
{
    private static readonly Color Verde = new Color(0.13f, 0.72f, 0.35f);
    private static readonly Color Amarelo = new Color(0.95f, 0.80f, 0.15f);
    private static readonly Color Laranja = new Color(1f, 0.50f, 0.10f);
    private static readonly Color Vermelho = new Color(0.85f, 0.18f, 0.18f);
    private static readonly Color Fundo = new Color(0.05f, 0.07f, 0.09f, 0.95f);

    /// <summary>Verde no começo, vermelho no fim.</summary>
    public static Color Cor(float fracao) =>
        fracao > 0.65f ? Verde : fracao > 0.40f ? Amarelo : fracao > 0.20f ? Laranja : Vermelho;

    /// <summary>Fundo escuro + o pedaço que ainda resta, na cor da fração.</summary>
    public static void Desenhar(Rect rect, float fracao)
    {
        Color anterior = GUI.color;
        GUI.color = Fundo;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        fracao = Mathf.Clamp01(fracao);
        if (fracao > 0f)
        {
            GUI.color = Cor(fracao);
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * fracao, rect.height), Texture2D.whiteTexture);
        }
        GUI.color = anterior;
    }
}
