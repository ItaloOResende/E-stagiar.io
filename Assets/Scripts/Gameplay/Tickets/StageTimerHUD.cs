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

    [Header("Cores do HUD")]
    [SerializeField] private Color Backdrop = new Color(0.08f, 0.10f, 0.14f, 0.78f);
    [SerializeField] private Color Urgente = new Color(0.86f, 0.16f, 0.14f, 0.92f);

    [Header("Ícone e textos da fase")]
    [Tooltip("Imagem do ícone da fase. Aceita textura PNG com transparência.")]
    [SerializeField] private Texture2D stageIcon;
    [SerializeField, Min(1f)] private float iconSize = 40f;
    [SerializeField, Min(0f)] private float iconSpacing = 10f;
    [SerializeField] private Color iconColor = Color.white;
    [SerializeField] private string stageLabel = "FASE";
    [SerializeField] private string speedRunLabel = "SPEED RUN";
    [Tooltip("Altera somente o título do HUD para Speed Run; não altera o modo nem a duração da partida.")]
    [SerializeField] private bool showSpeedRunLabel;
    [SerializeField] private string timeLabel = "TEMPO";
    [SerializeField] private bool showStageName = true;
    [SerializeField] private bool showTime = true;
    [SerializeField] private Font textFont;
    [SerializeField] private FontStyle textFontStyle = FontStyle.Bold;
    [SerializeField] private Color textColor = Color.white;

    [Header("Tensão no fim da fase")]
    [Tooltip("Com este tempo restante (s) o selo fica vermelho e começa o som de suspense.")]
    [SerializeField, Min(1f)] private float warningSeconds = 60f;
    [Tooltip("Nos últimos segundos o som fica mais intenso e o selo pulsa.")]
    [SerializeField, Min(1f)] private float finalSeconds = 10f;
    [Tooltip("Volume do suspense (aviso / últimos segundos).")]
    [SerializeField, Range(0f, 1f)] private float warningVolume = 0.4f;
    [SerializeField, Range(0f, 1f)] private float finalVolume = 0.8f;

    /// <summary>Estado da tensão (para o som não reiniciar a cada atualização do contador).</summary>
    private enum Tensao { Normal, Aviso, Final, Fim }
    private Tensao tensao = Tensao.Normal;
    private AudioSource suspense;

    /// <summary>O selo está desenhado (some no fim da fase e com o relatório final na frente).</summary>
    public bool Visible { get; private set; }

    private GUIStyle labelStyle;

    private void Awake()
    {
        if (stageController == null) stageController = GetComponent<StageController>();
        if (ticketManager == null) ticketManager = GetComponent<TicketManager>();
    }

    private void Update()
    {
        if (stageController == null || ticketManager == null) return;
        Tensao nova;
        float s = stageController.SecondsRemaining;
        if (!stageController.IsRunning || ticketManager.StageNumber == 0 || StageReportScreen.Showing) nova = stageController.IsRunning ? Tensao.Normal : Tensao.Fim;
        else if (s <= finalSeconds) nova = Tensao.Final;
        else if (s <= warningSeconds) nova = Tensao.Aviso;
        else nova = Tensao.Normal;
        if (nova == tensao) return;
        tensao = nova;
        // só nas TROCAS de estado: o som segue tocando entre um segundo e outro do contador
        if (tensao == Tensao.Aviso || tensao == Tensao.Final)
        {
            if (suspense == null) suspense = CriarSom();
            suspense.volume = tensao == Tensao.Final ? finalVolume : warningVolume;
            suspense.pitch = tensao == Tensao.Final ? 1.35f : 1f;
            if (!suspense.isPlaying) suspense.Play();
        }
        else if (suspense != null) suspense.Stop();
    }

    private void OnDisable()
    {
        if (suspense != null) suspense.Stop();
        tensao = Tensao.Normal;
    }

    // Suspense discreto gerado por código (sem asset): pulso grave em batida dupla, em loop de 1 s.
    private AudioSource CriarSom()
    {
        const int rate = 22050;
        var dados = new float[rate];
        for (int i = 0; i < dados.Length; i++)
        {
            float t = i / (float)rate;
            float env = Pulso(t, 0f) + 0.7f * Pulso(t, 0.22f);
            dados[i] = env * (Mathf.Sin(2f * Mathf.PI * 55f * t) * 0.8f + Mathf.Sin(2f * Mathf.PI * 110f * t) * 0.2f);
        }
        AudioClip clip = AudioClip.Create("Suspense_Fase", dados.Length, 1, rate, false);
        clip.SetData(dados, 0);
        var src = gameObject.AddComponent<AudioSource>();
        src.clip = clip;
        src.loop = true;
        src.playOnAwake = false;
        src.spatialBlend = 0f;
        return src;
    }

    private static float Pulso(float t, float inicio)
    {
        float x = t - inicio;
        return x < 0f ? 0f : Mathf.Exp(-x * 18f) * Mathf.Clamp01(x * 200f);
    }

    private void OnGUI()
    {
        Visible = false;
        if (stageController == null || ticketManager == null) return;
        int stage = ticketManager.StageNumber;
        if (stage == 0) return; // Tutorial sem cronometro.
        // fase encerrada: o contador zerado e o nome da fase saem; o relatório final é o foco
        if (!stageController.IsRunning || StageReportScreen.Showing) return;

        float duration = Mathf.Max(1f, stageController.DurationSeconds);
        float remaining = Mathf.Clamp(stageController.SecondsRemaining, 0f, duration);
        int seconds = Mathf.CeilToInt(remaining);
        string text = showStageName ? (showSpeedRunLabel ? speedRunLabel : $"{stageLabel} {stage}") : "";
        if (showTime)
            text += (string.IsNullOrEmpty(text) ? "" : "   |   ") + $"{timeLabel}  {seconds / 60:00}:{seconds % 60:00}";
        if (string.IsNullOrEmpty(text) && stageIcon == null) return;

        // A escala acompanha o CanvasScaler das telas do jogo (referência 1920x1080), então o selo fica do
        // MESMO tamanho relativo em qualquer resolução — como o celular do jogador.
        float e = Escala;
        // último minuto: vermelho; últimos segundos: pulsa (só o tamanho do selo, o texto continua legível)
        bool urgente = remaining <= warningSeconds;
        bool final = remaining <= finalSeconds;
        if (final) e *= 1f + 0.08f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f * 1.6f));
        GarantirEstilo(e);
        Visible = true;

        // O retangulo e medido PELO TEXTO: o fundo e sempre um selo colado na letra.
        Vector2 medida = labelStyle.CalcSize(new GUIContent(text));
        float iconWidth = stageIcon != null ? (iconSize + (string.IsNullOrEmpty(text) ? 0f : iconSpacing)) * e : 0f;
        float w = Mathf.Ceil(medida.x + iconWidth + 2f * paddingX * e);
        float h = Mathf.Ceil(Mathf.Max(medida.y, stageIcon != null ? iconSize * e : 0f) + 2f * paddingY * e);
        float x = Screen.width - w - rightMargin * e;   // alto da DIREITA, à esquerda do botão de fechar
        float y = topMargin * e;

        DrawRect(new Rect(x, y, w, h), urgente ? Urgente : Backdrop);
        if (stageIcon != null)
        {
            Color previous = GUI.color;
            GUI.color = iconColor;
            GUI.DrawTexture(new Rect(x + paddingX * e, y + (h - iconSize * e) * 0.5f, iconSize * e, iconSize * e), stageIcon, ScaleMode.ScaleToFit, true);
            GUI.color = previous;
        }
        GUI.Label(new Rect(x + paddingX * e + iconWidth, y + paddingY * e, w - 2f * paddingX * e - iconWidth, h - 2f * paddingY * e),
            text, labelStyle);
    }

    /// <summary>A mesma escala que o CanvasScaler das telas do jogo usa (referência 1920 x 1080).</summary>
    private static float Escala => Mathf.Sqrt((Screen.width / RefLargura) * (Screen.height / RefAltura));

    /// <summary>Monta o estilo uma vez por escala de tela (mudar o tamanho da fonte a cada quadro é caro).</summary>
    private void GarantirEstilo(float escala)
    {
        int tamanho = Mathf.Max(9, Mathf.RoundToInt(fontSize * escala));
        if (labelStyle != null && labelStyle.fontSize == tamanho)
        {
            labelStyle.font = textFont;
            labelStyle.fontStyle = textFontStyle;
            labelStyle.normal.textColor = textColor;
            return;
        }

        labelStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = tamanho,
            font = textFont,
            fontStyle = textFontStyle,
            wordWrap = false,
            clipping = TextClipping.Clip
        };
        labelStyle.normal.textColor = textColor;
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

    private static readonly Color Preto = new Color(0.02f, 0.02f, 0.02f);

    /// <summary>
    /// Barra de um chamado: dentro do prazo encolhe (verde → amarelo → laranja → vermelho); depois do prazo fica
    /// cheia e escurece do vermelho ao preto (<see cref="TicketInstance.OverdueFraction"/>). Nunca bloqueia nada.
    /// </summary>
    public static void Desenhar(Rect rect, TicketInstance chamado)
    {
        if (chamado == null) return;
        if (!chamado.IsOverdue) { Desenhar(rect, chamado.TimeFraction); return; }
        Color anterior = GUI.color;
        GUI.color = Color.Lerp(Vermelho, Preto, chamado.OverdueFraction);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = anterior;
    }

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
