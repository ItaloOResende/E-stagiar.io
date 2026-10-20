using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD do tempo da fase. Usa o tempo real do StageController:
///  - uma FAIXA horizontal no topo, da largura toda da tela, que encolhe com o tempo restante (verde → amarelo →
///    vermelho, em degradê contínuo), fixa durante a partida e proporcional à resolução;
///  - o cronômetro (mm:ss) num selo no alto da DIREITA, logo abaixo da faixa (fora dela). Sem o nome da fase.
///    Selo = cartão do <see cref="HudEstilo"/> (o mesmo da pontuação): rótulo "TEMPO" + mm:ss grande na fonte do jogo
///    (<see cref="Fonte"/>, Varela Round); no último minuto fica vermelho e nos últimos segundos pulsa.
/// Nao cria um segundo temporizador nem modifica os minigames.
/// O tempo de CADA chamado é outro: veja <see cref="TicketManager"/> e a barra da lista do celular.
/// <para>
/// O selo tem o tamanho do texto (nada de barra atravessando o topo). O selo fica À ESQUERDA do botão de fechar das telas 2D (todos eles ficam
/// nos 88 px do canto superior direito: 24 de margem + 64x64), por isso <see cref="rightMargin"/> começa em 100:
/// o selo para 12 px antes do botão e não atrapalha nem o botão nem o conteúdo do meio das telas.
/// </para>
/// <para>
/// Desenhado em uGUI num Canvas próprio de <see cref="sortingOrder"/> BAIXO (fica abaixo das telas 2D e do tablet, e
/// acima do prompt de interação): com o cursor por software, o cursor do mouse é composto por CIMA dos canvases, mas
/// ficava sob o selo enquanto ele era IMGUI (o OnGUI desenha depois do cursor). Sendo uGUI, o cursor passa na frente.
/// </para>
/// </summary>
[DisallowMultipleComponent]
public class StageTimerHUD : MonoBehaviour
{
    [SerializeField] private StageController stageController;
    [SerializeField] private TicketManager ticketManager;
    [Header("Visual (pixels da referência 1920 x 1080)")]
    [Tooltip("Fonte do selo e da pontuação (ScoreHUD). Vazio = fonte padrão.")]
    [SerializeField] private Font fonte;
    [SerializeField] private string rotulo = "TEMPO";
    [Tooltip("Distancia da borda DIREITA da tela. O padrao (100) deixa o selo FORA do botao de fechar das telas.")]
    [SerializeField, Min(0f)] private float rightMargin = 800f;

    /// <summary>Fonte do HUD de jogo (também usada pelo <see cref="ScoreHUD"/> no mesmo objeto).</summary>
    public Font Fonte => fonte;

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

    private void Awake()
    {
        if (stageController == null) stageController = GetComponent<StageController>();
        if (ticketManager == null) ticketManager = GetComponent<TicketManager>();
    }

    private void Update()
    {
        Desenhar();   // o selo/faixa mudam a cada quadro (contador, pulso, urgência)
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

    // ============================================================== desenho (uGUI)
    // Canvas próprio, com sortingOrder MÁXIMO: fica POR CIMA de TODOS os canvases dos minigames/telas para que o
    // cronômetro fique VISÍVEL mesmo com o minigame aberto. O cursor do jogo usa Cursor.SetCursor com ForceSoftware
    // (composicionado pelo SO por cima de TODO o render da Unity), por isso o ponteiro passa NA FRENTE do timer.
    private const int OrdemDoCanvas = 32767;

    private Canvas canvas;
    private RectTransform raiz;
    private RectTransform faixa, faixaPreenchida, selo;
    private Image fundoFaixa, preenchimentoFaixa, seloFundo;
    private Outline bordaSelo;
    private Text rotuloTexto, valorTexto;
    private Sprite spriteArredondado;
    private float raioGerado = -1f;

    private void GarantirCanvas()
    {
        if (canvas != null) return;
        var go = new GameObject("StageTimerHUD_Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        go.transform.SetParent(transform, false);
        canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = OrdemDoCanvas;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        raiz = (RectTransform)go.transform;

        // faixa do topo: fundo escuro fixo + preenchimento que encolhe da direita para a esquerda
        fundoFaixa = NovaImagem("FundoFaixa", new Color(0.05f, 0.07f, 0.09f, 0.75f), out faixa, Image.Type.Simple);
        faixa.anchorMin = new Vector2(0f, 1f); faixa.anchorMax = new Vector2(1f, 1f); faixa.pivot = new Vector2(0f, 1f);
        preenchimentoFaixa = NovaImagem("Preenchimento", Color.green, out faixaPreenchida, Image.Type.Simple);
        faixaPreenchida.anchorMin = new Vector2(0f, 1f); faixaPreenchida.anchorMax = new Vector2(0f, 1f); faixaPreenchida.pivot = new Vector2(0f, 1f);

        // selo: cartão arredondado (fundo + borda via Outline) com rótulo e valor
        seloFundo = NovaImagem("SeloFundo", HudEstilo.Fundo, out selo);
        bordaSelo = seloFundo.gameObject.AddComponent<Outline>();
        bordaSelo.effectColor = HudEstilo.Borda; bordaSelo.effectDistance = new Vector2(2f, -2f); bordaSelo.useGraphicAlpha = false;
        rotuloTexto = NovaTexto("Rotulo", rotulo, 15, TextAnchor.UpperLeft); rotuloTexto.color = HudEstilo.Rotulo;
        valorTexto = NovaTexto("Valor", "", 32, TextAnchor.MiddleLeft); valorTexto.color = HudEstilo.Valor;
    }

    private Image NovaImagem(string nome, Color cor, out RectTransform rt, Image.Type tipo = Image.Type.Sliced)
    {
        var go = new GameObject(nome, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(raiz, false);
        rt = (RectTransform)go.transform;
        var img = go.GetComponent<Image>();
        img.color = cor; img.raycastTarget = false; img.type = tipo; img.sprite = SpriteArredondado();
        return img;
    }

    private Text NovaTexto(string nome, string valor, int tamanho, TextAnchor alinhamento)
    {
        var go = new GameObject(nome, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(selo, false);
        var t = go.GetComponent<Text>();
        t.font = fonte != null ? fonte : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = tamanho; t.fontStyle = FontStyle.Bold; t.alignment = alinhamento;
        t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
        t.text = valor; t.raycastTarget = false;
        return t;
    }

    /// <summary>Sprite de retângulo arredondado (9-slice), no raio do HudEstilo; gerado uma vez e reaproveitado.</summary>
    private Sprite SpriteArredondado()
    {
        const float raio = 14f;
        if (spriteArredondado != null && Mathf.Approximately(raioGerado, raio)) return spriteArredondado;
        raioGerado = raio;
        int lado = Mathf.CeilToInt(raio) * 2 + 4;
        var tex = new Texture2D(lado, lado, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var pixels = new Color[lado * lado];
        for (int y = 0; y < lado; y++) for (int x = 0; x < lado; x++)
        {
            float dx = Mathf.Max(raio - (x + .5f), (x + .5f) - (lado - raio));
            float dy = Mathf.Max(raio - (y + .5f), (y + .5f) - (lado - raio));
            float d = new Vector2(Mathf.Max(0f, dx), Mathf.Max(0f, dy)).magnitude;
            pixels[y * lado + x] = new Color(1, 1, 1, Mathf.Clamp01(raio + .5f - d));
        }
        tex.SetPixels(pixels); tex.Apply(false, true);
        spriteArredondado = Sprite.Create(tex, new Rect(0, 0, lado, lado), Vector2.one * .5f, 100f, 0, SpriteMeshType.FullRect, Vector4.one * raio);
        spriteArredondado.hideFlags = HideFlags.HideAndDontSave;
        return spriteArredondado;
    }

    private void OnDestroy()
    {
        if (spriteArredondado != null) { Destroy(spriteArredondado.texture); Destroy(spriteArredondado); }
    }

    private void Desenhar()
    {
        Visible = false;
        if (stageController == null || ticketManager == null) return;
        int stage = ticketManager.StageNumber;
        if (stage == 0) { if (canvas != null) canvas.enabled = false; return; } // Tutorial sem cronometro.
        // fase encerrada: o contador zerado e o nome da fase saem; o relatório final é o foco
        if (!stageController.IsRunning || StageReportScreen.Showing) { if (canvas != null) canvas.enabled = false; return; }

        GarantirCanvas();
        canvas.enabled = true;

        float duration = Mathf.Max(1f, stageController.DurationSeconds);
        float remaining = Mathf.Clamp(stageController.SecondsRemaining, 0f, duration);
        int seconds = Mathf.CeilToInt(remaining);
        valorTexto.text = $"{seconds / 60:00}:{seconds % 60:00}";   // só o tempo (sem o nome da fase)
        rotuloTexto.text = rotulo;

        float e = HudEstilo.Escala;

        // faixa do topo: largura toda, encolhe da direita para a esquerda conforme o tempo acaba
        float fracao = remaining / duration;
        float hb = Mathf.Max(2f, Mathf.Round(HudEstilo.AlturaFaixa * e));
        fundoFaixa.rectTransform.sizeDelta = new Vector2(0f, hb);
        preenchimentoFaixa.rectTransform.sizeDelta = new Vector2(Screen.width, hb);
        preenchimentoFaixa.rectTransform.pivot = new Vector2(0f, 1f);
        preenchimentoFaixa.rectTransform.anchoredPosition = Vector2.zero;
        preenchimentoFaixa.rectTransform.localScale = new Vector3(Mathf.Clamp01(fracao), 1f, 1f);
        preenchimentoFaixa.color = CorDaFaixa(fracao);

        // último minuto: vermelho; últimos segundos: pulsa (só o tamanho do selo, o texto continua legível)
        bool urgente = remaining <= warningSeconds;
        bool final = remaining <= finalSeconds;
        float pulso = final ? 1f + 0.06f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f * 1.6f)) : 1f;
        Visible = true;

        // largura fixa pelo maior texto possível ("00:00"): o selo não "dança" quando os dígitos mudam
        float w = Mathf.Max(HudEstilo.Largura(rotulo, "00:00", e, fonte), 130f * e);
        float h = HudEstilo.AlturaSelo * e;
        float x = Screen.width - w - rightMargin * e;   // alto da DIREITA, mais à direita para não cobrir o botão de fechar
        float y = -hb - HudEstilo.MargemTopo * e;       // abaixo da faixa (eixo Y do uGUI cresce para cima)
        selo.anchorMin = selo.anchorMax = new Vector2(1f, 1f); selo.pivot = new Vector2(1f, 1f);
        selo.sizeDelta = new Vector2(w, h);
        selo.anchoredPosition = new Vector2(-(Screen.width - x), y);
        selo.localScale = Vector3.one * pulso;

        seloFundo.color = urgente ? HudEstilo.FundoUrgente : HudEstilo.Fundo;
        if (bordaSelo != null) bordaSelo.effectColor = urgente ? HudEstilo.BordaUrgente : HudEstilo.Borda;
        rotuloTexto.color = urgente ? HudEstilo.BordaUrgente : HudEstilo.Rotulo;
        valorTexto.color = HudEstilo.Valor;

        // posiciona rótulo e valor dentro do selo (mesmas folgas do HudEstilo.Selo)
        float px = HudEstilo.PaddingX * e, pw = w - 2f * px;
        DefinirFilho(rotuloTexto.rectTransform, px, -8f * e, pw, 18f * e);
        DefinirFilho(valorTexto.rectTransform, px, -24f * e, pw, h - 28f * e);
        rotuloTexto.fontSize = Mathf.Max(8, Mathf.RoundToInt(HudEstilo.TamanhoRotulo * e));
        valorTexto.fontSize = Mathf.Max(10, Mathf.RoundToInt(HudEstilo.TamanhoValor * e));
    }

    private static void DefinirFilho(RectTransform r, float x, float y, float w, float h)
    {
        r.anchorMin = r.anchorMax = new Vector2(0f, 1f); r.pivot = new Vector2(0f, 1f);
        r.anchoredPosition = new Vector2(x, y); r.sizeDelta = new Vector2(w, h);
    }

    private void OnDisable()
    {
        if (canvas != null) canvas.enabled = false;
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

    /// <summary>Verde no início, amarelo na metade, vermelho no fim (degradê contínuo).</summary>
    public static Color CorDaFaixa(float fracao)
    {
        Color verde = new Color(0.2f, 0.8f, 0.35f), amarelo = new Color(0.98f, 0.82f, 0.18f), vermelho = new Color(0.9f, 0.18f, 0.15f);
        fracao = Mathf.Clamp01(fracao);
        return fracao > 0.5f ? Color.Lerp(amarelo, verde, (fracao - 0.5f) * 2f) : Color.Lerp(vermelho, amarelo, fracao * 2f);
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
