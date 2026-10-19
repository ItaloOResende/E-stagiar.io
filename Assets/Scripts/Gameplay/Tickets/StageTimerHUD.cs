using UnityEngine;

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
    [SerializeField, Min(0f)] private float rightMargin = 100f;

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
        string text = $"{seconds / 60:00}:{seconds % 60:00}";   // só o tempo (sem o nome da fase)

        // A escala acompanha o CanvasScaler das telas do jogo (referência 1920x1080), então o selo fica do
        // MESMO tamanho relativo em qualquer resolução — como o tablet do jogador.
        float e = HudEstilo.Escala;

        // faixa do topo: largura toda, encolhe da direita para a esquerda conforme o tempo acaba
        float fracao = remaining / duration;
        float hb = Mathf.Max(2f, Mathf.Round(HudEstilo.AlturaFaixa * e));
        DrawRect(new Rect(0f, 0f, Screen.width, hb), new Color(0.05f, 0.07f, 0.09f, 0.75f));
        DrawRect(new Rect(0f, 0f, Screen.width * fracao, hb), CorDaFaixa(fracao));

        // último minuto: vermelho; últimos segundos: pulsa (só o tamanho do selo, o texto continua legível)
        bool urgente = remaining <= warningSeconds;
        bool final = remaining <= finalSeconds;
        float pulso = final ? 1f + 0.06f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f * 1.6f)) : 1f;
        Visible = true;

        // largura fixa pelo maior texto possível ("00:00"): o selo não "dança" quando os dígitos mudam
        float w = Mathf.Max(HudEstilo.Largura(rotulo, "00:00", e, fonte), 130f * e);
        float h = HudEstilo.AlturaSelo * e;
        float x = Screen.width - w - rightMargin * e;   // alto da DIREITA, à esquerda do botão de fechar
        float y = hb + HudEstilo.MargemTopo * e;   // abaixo da faixa, alinhado com o selo da pontuação
        Rect r = new Rect(x, y, w, h);
        if (pulso > 1f)
        {
            Vector2 c = r.center;
            r.size *= pulso;
            r.center = c;
        }
        HudEstilo.Selo(r, rotulo, text, e * pulso, fonte, urgente);
    }

    /// <summary>Verde no início, amarelo na metade, vermelho no fim (degradê contínuo).</summary>
    public static Color CorDaFaixa(float fracao)
    {
        Color verde = new Color(0.2f, 0.8f, 0.35f), amarelo = new Color(0.98f, 0.82f, 0.18f), vermelho = new Color(0.9f, 0.18f, 0.15f);
        fracao = Mathf.Clamp01(fracao);
        return fracao > 0.5f ? Color.Lerp(amarelo, verde, (fracao - 0.5f) * 2f) : Color.Lerp(vermelho, amarelo, fracao * 2f);
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
