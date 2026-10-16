using System.Collections.Generic;
using EstagiarIo.Rede;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// O CELULAR do estagiário — no canto inferior DIREITO e em qualquer lugar do jogo.
/// Fechado ele é só o desenho do aparelho, com um selo vermelho quando há chamados em aberto.
/// O jogador abre com a tecla <b>C</b> (ou clicando no aparelho) e fecha com <b>C</b> de novo, com o "X"
/// no alto da "tela" ou <b>clicando em cima do aparelho aberto</b> — o celular volta a ser miniatura no primeiro
/// clique que cair DENTRO dele e não seja num dos botões (X, CHAMADOS, CORES, "Iniciar atendimento"), que aí
/// cumprem o papel deles. Clicar fora do celular não fecha (no minigame de rede o jogador clica nos cabos da
/// bancada com o app CORES aberto e não pode perder a ordem).
/// Dentro dele vivem dois "apps":
/// <list type="bullet">
///   <item><b>CHAMADOS</b> — a lista dos chamados: o chamado chega recolhido (só "#n título"), o jogador
///   clica em "Iniciar atendimento" e aí aparecem solicitante, setor e descrição; embaixo de cada chamado aberto
///   vem a barra do tempo DELE (que começa a descer na chegada e mostra quando o chamado foi perdido).</item>
///   <item><b>CORES</b> — a ordem do padrão T568B. Ele só existe quando o jogador está no minigame de rede (é a
///   ajuda da crimpagem), e nesse caso é o app que abre por padrão.</item>
/// </list>
/// <para>
/// É desenhado em OnGUI de propósito: o repaint do OnGUI é a última coisa desenhada no quadro, então o celular
/// fica por cima até das telas 2D (que são uGUI). Como o uGUI e o OnGUI recebem o clique cada um por si, um painel
/// uGUI invisível (canvas próprio, ordem 32000) segura o clique enquanto o celular está aberto — e é ele também
/// que desenha e clica no aparelho fechado, para o clique não "vazar" para a tela de trás. Com uma tela 2D aberta
/// esse painel encolhe para o retângulo do aparelho (em vez de cobrir a tela toda) para o "X" da tela continuar
/// respondendo — veja <see cref="AtualizarBloqueio"/>.
/// </para>
/// </summary>
public class TicketTabletPrototype : MonoBehaviour
{
    [SerializeField] private TicketManager ticketManager;
    [SerializeField] private StageController stageController;
    [Tooltip("Tecla que abre e fecha o celular (padrão: C).")]
    [SerializeField] private Key toggleKey = Key.C;
    [SerializeField] private InteractionDetector interactionDetector;
    [Tooltip("Componentes desligados enquanto o celular está aberto: com ele aberto o jogador mexe só no celular.")]
    [SerializeField] private Behaviour[] disableWhileTabletOpen;

    [Header("Celular (pixels da referência 1920 x 1080)")]
    // Largura/altura/margem vêm do RedeUi: é a MESMA geometria que as telas usam para posicionar a seta "►"
    // fora do celular (RedeUi.BordaSetaProxima). Mexeu num, mexeu no outro.
    [SerializeField, Min(50f)] private float larguraIcone = RedeUi.CelularLargura;
    [SerializeField, Min(80f)] private float alturaIcone = RedeUi.CelularAltura;
    [SerializeField, Min(220f)] private float larguraCelular = 430f;
    [SerializeField, Min(320f)] private float alturaCelular = 640f;
    [Tooltip("Distância do aparelho até as bordas direita e de baixo.")]
    [SerializeField, Min(0f)] private float margem = RedeUi.CelularMargem;

    [Header("Aviso de chamado novo (acima do celular)")]
    [Tooltip("Ligado: quando um chamado novo cai, sobe um aviso EM CIMA do celular. Ele mostra so o numero e o " +
             "titulo — solicitante, setor e descricao continuam aparecendo apenas com o celular aberto.")]
    [SerializeField] private bool avisarChamadoNovo = true;
    [Tooltip("Segundos que o aviso fica na tela antes de desaparecer.")]
    [SerializeField, Min(0.5f)] private float avisoSegundos = 6f;
    [Tooltip("Largura do cartao do aviso (pixels da referência 1920 x 1080).")]
    [SerializeField, Min(160f)] private float larguraAviso = 330f;
    [Tooltip("Distância entre o aviso e o topo do celular.")]
    [SerializeField, Min(0f)] private float folgaAviso = 12f;

    /// <summary>Resolução de referência das telas 2D do jogo (o celular acompanha a mesma escala).</summary>
    private const float RefLargura = 1920f;
    private const float RefAltura = 1080f;

    private static readonly Color CorCorpo = new Color(0.16f, 0.18f, 0.23f, 0.99f);
    private static readonly Color CorTela = new Color(0.11f, 0.13f, 0.17f, 1f);
    private static readonly Color CorTexto = new Color(0.93f, 0.95f, 0.97f);
    private static readonly Color CorFraco = new Color(0.62f, 0.68f, 0.76f);
    private static readonly Color CorCard = new Color(0.17f, 0.20f, 0.25f, 1f);
    private static readonly Color CorBotao = new Color(0.27f, 0.31f, 0.38f, 1f);
    private static readonly Color CorBotaoForte = new Color(0.34f, 0.39f, 0.47f, 1f);
    private static readonly Color CorBotaoApertado = new Color(0.21f, 0.24f, 0.29f, 1f);
    private static readonly Color CorAba = new Color(0.16f, 0.19f, 0.24f, 1f);
    private static readonly Color CorAbaAtiva = new Color(0.29f, 0.34f, 0.42f, 1f);
    private static readonly Color CorVermelho = new Color(0.85f, 0.18f, 0.18f, 1f);
    private static readonly Color CorVerde = new Color(0.35f, 0.85f, 0.45f);
    private static readonly Color CorBrancoListra = new Color(0.97f, 0.97f, 0.95f);
    private static readonly Color CorAviso = new Color(1f, 0.78f, 0.25f);   // "tem coisa nova" (o selo usa o vermelho)

    /// <summary>
    /// O ponteiro está EM CIMA do aparelho — o corpo inteiro quando ele está aberto, a miniatura do canto quando
    /// está fechado (aquela também é clicável: botão "Abrir"). As telas 2D leem isto para tirar a ferramenta
    /// (alicate, chave de fenda) da frente do celular: fora dele o cursor continua sendo a ferramenta.
    /// </summary>
    public static bool PonteiroSobreOAparelho { get; private set; }

    private bool aberto;
    private bool mostrarCores;
    private bool cursorDeMaozinha;   // a mãozinha do celular está posta por cima do cursor da tela?
    private bool cursorEraVisivel;
    private CursorLockMode travaDoCursor;
    private Vector2 rolagem;

    // As ações pedidas pelos botões do celular NÃO são feitas no meio do OnGUI: o IMGUI desenha duas vezes por
    // quadro (Layout e Repaint) e mudar o conteúdo num evento só faz a conta de controles de um não bater com a
    // do outro ("ArgumentException: Getting control N's position in a group with only N controls"). As duas
    // ações ficam guardadas aqui e são aplicadas no Update, fora do OnGUI.
    private int iniciarChamadoPendente = -1;
    private bool fecharPendente;

    // Aviso de chamado novo (o "toast" que sobe acima do celular).
    private int ultimoChamadoVisto;      // maior numero de chamado ja anunciado nesta fase
    private int avisoNumero = -1;        // chamado do aviso na tela (-1 = nenhum)
    private string avisoTitulo = "";
    private float avisoNasceuEm;

    // Painel uGUI invisível: desenha/clica o aparelho fechado e segura os cliques com o celular aberto.
    private GameObject painelCelular;
    private GameObject bloqueioTela;
    private bool bloqueioEncolhido;
    private GameObject aparelhoFechado;
    private GameObject selo;
    private Text seloNumero;

    // Estilos do OnGUI (GUIStyle é caro: monta uma vez por escala de tela).
    private float escalaDosEstilos = -1f;
    private GUIStyle sTitulo, sFraco, sTexto, sBom, sRuim, sCard, sBotao, sAba, sAbaAtiva;
    private GUIStyle sAvisoEtiqueta, sAvisoChamado, sAvisoDica;
    private Texture2D tCard, tBotao, tBotaoForte, tBotaoApertado, tAba, tAbaAtiva;

    /// <summary>Celular aberto agora?</summary>
    public bool IsOpen => aberto;

    /// <summary>Abre o celular (também é o clique no aparelho fechado).</summary>
    public void Abrir() => SetAberto(true);

    /// <summary>Fecha o celular (tecla C ou o "X").</summary>
    public void Fechar() => SetAberto(false);

    private void Awake()
    {
        if (ticketManager == null) ticketManager = GetComponent<TicketManager>();
        if (stageController == null) stageController = GetComponent<StageController>();
        ConstruirAparelhoFechado();
    }

    private void Update()
    {
        // Ações pedidas pelos botões do celular (aplicadas fora do OnGUI — veja os campos acima).
        if (fecharPendente)
        {
            fecharPendente = false;
            SetAberto(false);
        }
        if (iniciarChamadoPendente >= 0 && ticketManager != null)
        {
            ticketManager.StartTicket(iniciarChamadoPendente);
            iniciarChamadoPendente = -1;
        }

        if (Keyboard.current != null && Keyboard.current[toggleKey].wasPressedThisFrame) SetAberto(!aberto);
        AtualizarBloqueio();
        VerificarChamadoNovo();
        AtualizarSelo();
        PonteiroSobreOAparelho = CalcularSobreOAparelho();
    }

    private void OnDisable()
    {
        if (aberto) SetAberto(false);
    }

    /// <summary>
    /// Viu um chamado NOVO na lista? Então levanta o aviso. Ele carrega só o número e o título: os detalhes
    /// (solicitante, setor, descrição) só existem dentro do celular, de propósito.
    /// </summary>
    private void VerificarChamadoNovo()
    {
        if (!avisarChamadoNovo) return;

        IReadOnlyList<TicketInstance> lista = ticketManager != null ? ticketManager.Tickets : null;
        if (lista == null || lista.Count == 0)
        {
            // Lista vazia = começo de fase (BeginStage limpa tudo): rearma o aviso para a próxima leva.
            ultimoChamadoVisto = 0;
            return;
        }

        for (int i = lista.Count - 1; i >= 0; i--)   // os novos estão no fim da lista
        {
            TicketInstance chamado = lista[i];
            if (chamado == null || chamado.Number <= ultimoChamadoVisto) continue;

            ultimoChamadoVisto = chamado.Number;
            if (aberto) continue;   // com o celular aberto a própria lista está na frente: nem precisa avisar

            avisoNumero = chamado.Number;
            avisoTitulo = chamado.Definition != null ? chamado.Definition.title : "";
            avisoNasceuEm = Time.unscaledTime;
        }
    }

    /// <summary>Há uma tela do minigame de rede aberta? Então o app do padrão de cores existe.</summary>
    private static bool TemOrdemDeCores => RedeCrimpScreen.Aberta;

    private void SetAberto(bool abrir)
    {
        if (aberto == abrir) return;
        aberto = abrir;

        if (abrir)
        {
            travaDoCursor = Cursor.lockState;
            cursorEraVisivel = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // No minigame de rede o celular abre no padrão de cores; fora dele, nos chamados.
            mostrarCores = TemOrdemDeCores;
            rolagem = Vector2.zero;
            avisoNumero = -1;   // abriu o celular: o aviso já cumpriu o papel
            InteractionSounds.Raise(InteractionSound.DrawerOpened);
        }
        else
        {
            Cursor.lockState = travaDoCursor;
            Cursor.visible = cursorEraVisivel;
            AtualizarCursorDeMao(false);   // fechou: a mãozinha sai e o cursor de baixo volta
            InteractionSounds.Raise(InteractionSound.DrawerClosed);
        }

        if (bloqueioTela != null && bloqueioTela.activeSelf != aberto) bloqueioTela.SetActive(aberto);
        if (aparelhoFechado != null && aparelhoFechado.activeSelf == aberto) aparelhoFechado.SetActive(!aberto);

        if (interactionDetector != null) interactionDetector.enabled = !aberto;
        if (disableWhileTabletOpen != null)
            foreach (Behaviour comportamento in disableWhileTabletOpen)
                if (comportamento != null) comportamento.enabled = !aberto;

        AtualizarBloqueio();
    }

    /// <summary>
    /// O painel invisível do celular existe para o clique não vazar para o que está atrás dele. Com uma <b>tela 2D
    /// aberta</b>, porém, ele encolhe para o retângulo do próprio aparelho em vez de cobrir a tela toda: assim o "X" da
    /// tela (alto à direita) volta a responder com o celular aberto — era o que já valia na crimpagem, onde o celular é
    /// ferramenta de trabalho (a ordem dos fios) — e o que fica embaixo do aparelho continua bloqueado, isto é, nenhum
    /// clique dispara junto com um botão do app.
    /// <para>
    /// Medidas da referência 1920 x 1080: a escala vem do <c>CanvasScaler</c> deste canvas, como no aparelho fechado,
    /// e são as MESMAS de <see cref="RetanguloAberto"/> (largura/altura/margem do celular).
    /// </para>
    /// </summary>
    private void AtualizarBloqueio()
    {
        if (bloqueioTela == null) return;

        // No escritório em 3D (nenhuma tela 2D aberta) o painel segue cobrindo a tela toda.
        bool encolher = !NoMundo3D();
        if (encolher == bloqueioEncolhido) return;
        bloqueioEncolhido = encolher;

        var rt = (RectTransform)bloqueioTela.transform;
        if (encolher)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-margem, margem);
            rt.sizeDelta = new Vector2(larguraCelular, alturaCelular);
        }
        else
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }

    /// <summary>Selo vermelho com quantos chamados estão em aberto (só com o celular fechado).</summary>
    private void AtualizarSelo()
    {
        if (selo == null || ticketManager == null) return;

        int emAberto = ticketManager.OpenCount;
        bool mostrar = !aberto && emAberto > 0;
        if (selo.activeSelf != mostrar) selo.SetActive(mostrar);
        if (mostrar && seloNumero != null) seloNumero.text = emAberto > 9 ? "9+" : emAberto.ToString();
    }

    // ================================================================== aparelho fechado (uGUI)

    /// <summary>
    /// O canvas invisível do celular: um botão do tamanho do aparelho (é ele que desenha e clica no aparelho
    /// fechado, sem deixar o clique vazar para a tela 2D de trás) e um painel que segura o clique enquanto o
    /// celular está aberto (a área dele depende da tela que está atrás: veja <see cref="AtualizarBloqueio"/>).
    /// </summary>
    private void ConstruirAparelhoFechado()
    {
        var go = new GameObject("Celular", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.transform.SetParent(transform, false);
        painelCelular = go;

        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;   // acima de qualquer tela do jogo

        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(RefLargura, RefAltura);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        // Invisível, mas com raycast: é ele que segura o clique que vazaria para o que está atrás do celular aberto.
        // O tamanho muda com o jogo: veja AtualizarBloqueio (tela toda no 3D, só o aparelho com uma tela 2D aberta).
        bloqueioTela = Retangulo("BloqueioTela", go.transform, new Color(0f, 0f, 0f, 0f));
        ((RectTransform)bloqueioTela.transform).anchorMin = Vector2.zero;
        ((RectTransform)bloqueioTela.transform).anchorMax = Vector2.one;
        ((RectTransform)bloqueioTela.transform).offsetMin = Vector2.zero;
        ((RectTransform)bloqueioTela.transform).offsetMax = Vector2.zero;
        bloqueioTela.GetComponent<Image>().raycastTarget = true;
        bloqueioTela.transform.SetAsFirstSibling();
        bloqueioTela.SetActive(false);

        // O aparelho fechado: corpo, tela, alto-falante e o selo dos chamados.
        aparelhoFechado = Retangulo("AparelhoFechado", go.transform, CorCorpo);
        var corpo = (RectTransform)aparelhoFechado.transform;
        corpo.anchorMin = corpo.anchorMax = new Vector2(1f, 0f);
        corpo.pivot = new Vector2(1f, 0f);
        corpo.anchoredPosition = new Vector2(-margem, margem);
        corpo.sizeDelta = new Vector2(larguraIcone, alturaIcone);

        var corpoImg = aparelhoFechado.GetComponent<Image>();
        corpoImg.raycastTarget = true;
        var botao = aparelhoFechado.AddComponent<Button>();
        botao.targetGraphic = corpoImg;
        var cores = botao.colors;
        cores.normalColor = Color.white;
        cores.highlightedColor = new Color(1.25f, 1.25f, 1.25f);
        cores.pressedColor = new Color(0.8f, 0.8f, 0.8f);
        cores.selectedColor = Color.white;
        botao.colors = cores;
        botao.onClick.AddListener(Abrir);

        GameObject tela = Retangulo("Tela", aparelhoFechado.transform, CorTela);
        Ancorar((RectTransform)tela.transform, new Vector2(0.5f, 0.5f),
            new Vector2(larguraIcone - 18f, alturaIcone - 30f), new Vector2(0.5f, 0.5f));
        ((RectTransform)tela.transform).anchoredPosition = new Vector2(0f, -5f);

        GameObject altoFalante = Retangulo("AltoFalante", aparelhoFechado.transform, CorFraco);
        Ancorar((RectTransform)altoFalante.transform, new Vector2(0.5f, 1f), new Vector2(34f, 7f), new Vector2(0.5f, 1f));
        ((RectTransform)altoFalante.transform).anchoredPosition = new Vector2(0f, -12f);

        selo = Retangulo("Selo", aparelhoFechado.transform, CorVermelho);
        Ancorar((RectTransform)selo.transform, new Vector2(1f, 1f), new Vector2(36f, 36f), new Vector2(0.5f, 0.5f));
        ((RectTransform)selo.transform).anchoredPosition = new Vector2(4f, 4f);
        seloNumero = Texto(selo.transform, "Numero", "0", 22, TextAnchor.MiddleCenter, Color.white, true);
        Esticar(seloNumero.rectTransform);
        selo.SetActive(false);
    }

    // ================================================================== celular aberto (OnGUI)

    private void OnGUI()
    {
        // O GUI.color é global do IMGUI. Como DesenharRetangulo agora multiplica por ele (para o aviso poder
        // aparecer e sumir de uma vez), garanto que ninguém entrou aqui com um tint pendurado de outro OnGUI.
        GUI.color = Color.white;

        if (aberto)
        {
            float e = Escala;
            bool cores = mostrarCores && TemOrdemDeCores;

            Rect corpo = RetanguloAberto();

            DesenharRetangulo(corpo, CorCorpo);
            Rect tela = new Rect(corpo.x + 14f * e, corpo.y + 26f * e, corpo.width - 28f * e, corpo.height - 66f * e);
            DesenharRetangulo(tela, CorTela);
            DesenharRetangulo(new Rect(corpo.center.x - 23f * e, corpo.y + 11f * e, 46f * e, 6f * e), CorFraco);
            DesenharRetangulo(new Rect(corpo.center.x - 55f * e, corpo.yMax - 22f * e, 110f * e, 6f * e), CorFraco);

            GarantirEstilos(e);
            DesenharAbas(tela, e, cores);
            DesenharConteudo(tela, e, cores);

            // Por último, DEPOIS de desenhar os botões (é o que faz o GUIUtility.hotControl estar atualizado).
            VerificarCliqueNoCelular(corpo);
        }

        // O cursor: a mãozinha vale EM CIMA do aparelho (aberto OU fechado) e, com o celular aberto, em qualquer
        // lugar do mundo 3D. Chamado do OnGUI (a última coisa do quadro) para sobrepor o que a tela pôs no Update,
        // mas SÓ quando a mãozinha manda: fora do aparelho quem manda é a ferramenta (alicate, chave de fenda) e a
        // tela continua escondendo o cursor como sempre.
        bool mao = CalcularSobreOAparelho() || (aberto && NoMundo3D());
        if (mao)
        {
            // Com a mãozinha na frente, o ponteiro precisa estar livre e visível (fechar uma tela 2D com o celular
            // aberto trava o cursor de novo e ele sumiria de cima do aparelho).
            if (Cursor.lockState != CursorLockMode.None) Cursor.lockState = CursorLockMode.None;
            if (!Cursor.visible) Cursor.visible = true;
        }
        AtualizarCursorDeMao(mao);

        // Por último e por cima de tudo (o OnGUI é a última coisa do quadro): o aviso de chamado novo
        // aparece mesmo com o celular fechado, logo ACIMA dele.
        DesenharAvisoDeChamadoNovo();
    }

    // ================================================================== celular aberto: clique que fecha

    /// <summary>
    /// Com o celular aberto, um clique do mouse com o botão esquerdo <b>dentro do aparelho</b> o devolve a
    /// miniatura — MENOS se o clique caiu em um dos botões dele (X, CHAMADOS, CORES ou "Iniciar atendimento"): aí
    /// o próprio botão já tomou o clique para si e o <see cref="GUIUtility.hotControl"/> deixa de ser zero, então
    /// este toque é ignorado e o botão cumpre o papel dele normalmente (a ação dele só dispara no soltar do mouse).
    /// <para>
    /// Clicar FORA do celular não fecha de propósito: no minigame de rede o aparelho fica aberto no app CORES e o
    /// jogador clica nos cabos da bancada o tempo todo — se o clique de fora fechasse, ele perderia a ordem.
    /// Para minimizar, é no aparelho (ou na tecla C, ou no "X").
    /// </para>
    /// <para>
    /// A ordem importa: este método só pode rodar DEPOIS de os botões serem desenhados, senão o hotControl ainda
    /// não está marcado. E o fechamento é adiado para o Update (veja <c>fecharPendente</c>): mudar o conteúdo no
    /// meio de um evento do OnGUI quebra o IMGUI ("Getting control N's position in a group with only N controls").
    /// </para>
    /// </summary>
    private void VerificarCliqueNoCelular(Rect corpo)
    {
        Event atual = Event.current;
        if (atual == null || atual.type != EventType.MouseDown || atual.button != 0) return;
        if (!corpo.Contains(atual.mousePosition)) return;   // clicou fora do celular: não acontece nada
        if (GUIUtility.hotControl != 0) return;             // o clique foi em um botão do celular: não fecha
        fecharPendente = true;
    }

    // ================================================================== a mãozinha do ponteiro

    /// <summary>Retângulo do aparelho ABERTO, em pixels da tela, na convenção do OnGUI (origem no alto à esquerda).</summary>
    private Rect RetanguloAberto()
    {
        float e = Escala;
        return new Rect(Screen.width - larguraCelular * e - margem * e,
                        Screen.height - alturaCelular * e - margem * e,
                        larguraCelular * e, alturaCelular * e);
    }

    /// <summary>O ponteiro em pixels da tela na convenção do OnGUI (o mouse vem com a origem embaixo).</summary>
    private static Vector2 PonteiroNaTela()
    {
        if (Mouse.current == null) return new Vector2(-1f, -1f);
        Vector2 ponteiro = Mouse.current.position.ReadValue();
        return new Vector2(ponteiro.x, Screen.height - ponteiro.y);
    }

    /// <summary>Nenhuma tela 2D aberta: o jogador está no escritório em 3D (é o <c>UIScreenManager</c> que sabe).</summary>
    private static bool NoMundo3D()
    {
        UIScreenManager telas = UIScreenManager.Active;
        return telas == null || !telas.IsOpen;
    }

    /// <summary>Retângulo da miniatura do aparelho FECHADO, em pixels da tela, na convenção do OnGUI. É a MESMA
    /// geometria do uGUI (ancorado em (1,0), pivô (1,0), margem até as bordas direita e de baixo).</summary>
    private Rect RetanguloFechado()
    {
        float e = Escala;
        return new Rect(Screen.width - (margem + larguraIcone) * e,
                        Screen.height - (margem + alturaIcone) * e,
                        larguraIcone * e, alturaIcone * e);
    }

    /// <summary>
    /// O ponteiro está em cima do aparelho agora? — o corpo inteiro quando ele está aberto, a miniatura do canto
    /// quando está fechado. No mundo 3D com o celular fechado o ponteiro está preso na câmera (não há ponteiro
    /// para apontar), então ali é sempre <c>false</c>.
    /// </summary>
    private bool CalcularSobreOAparelho()
    {
        if (aberto) return RetanguloAberto().Contains(PonteiroNaTela());
        if (NoMundo3D()) return false;
        return RetanguloFechado().Contains(PonteiroNaTela());
    }

    /// <summary>
    /// A mãozinha no ponteiro. Ela vale em dois casos:
    /// <list type="bullet">
    ///   <item>EM CIMA do aparelho — aberto (a tela inteira) <b>ou</b> fechado (a miniatura do canto) — em qualquer
    ///   tela, inclusive nas telas 2D, onde o jogador pode estar com o crimpador ou a chave de fenda "na mão": o
    ///   cursor da ferramenta dá lugar à mãozinha SÓ ali (fora dele a ferramenta volta);</item>
    ///   <item>em qualquer lugar do MUNDO 3D (nenhuma tela 2D aberta), enquanto o celular está aberto: fora das
    ///   telas não existe ferramenta na mão para disputar o cursor.</item>
    /// </list>
    /// A mãozinha é a MESMA textura do cursor padrão das telas (<see cref="UIScreenManager.CursorMao"/>) e entra como
    /// sobreposição (<see cref="UICursor.Sobrepor"/>): ao sair de cima do celular o cursor de baixo volta sozinho.
    /// Chamado do OnGUI (a última coisa do quadro), então sobrepõe o que a tela tiver posto no Update.
    /// </summary>
    private void AtualizarCursorDeMao(bool mao)
    {
        if (mao == cursorDeMaozinha) return;
        cursorDeMaozinha = mao;

        if (!mao)
        {
            UICursor.LimparSobreposicao();
            return;
        }

        UIScreenManager telas = UIScreenManager.Active;
        if (telas == null || telas.CursorMao == null) return;   // sem mãozinha configurada: fica o cursor do sistema
        UICursor.Sobrepor(telas.CursorMao, telas.CursorMaoPonta, telas.CursorMaoAltura);
        Cursor.visible = true;   // nas telas 2D a ferramenta na mão pode ter escondido o cursor
    }

    // ================================================================== aviso de chamado novo

    /// <summary>
    /// O aviso que sobe logo acima do celular quando cai um chamado. Ele mostra SÓ o número e o título — de
    /// propósito: solicitante, setor e descrição continuam aparecendo apenas com o celular aberto.
    /// Ele aparece com uma subida curta e some sozinho depois de <see cref="avisoSegundos"/>.
    /// </summary>
    private void DesenharAvisoDeChamadoNovo()
    {
        if (avisoNumero < 0) return;

        float idade = Time.unscaledTime - avisoNasceuEm;
        if (idade > avisoSegundos) { avisoNumero = -1; return; }

        float e = Escala;
        GarantirEstilos(e);

        // Entra rápido e sai mais devagar (some sozinho, sem clique).
        float alfa = Mathf.Clamp01(idade / 0.18f) * Mathf.Clamp01((avisoSegundos - idade) / 0.45f);
        float subida = (1f - Mathf.Clamp01(idade / 0.25f)) * 14f * e;

        float respiro = 12f * e;
        float altura = respiro * 2f + sAvisoEtiqueta.lineHeight + sAvisoChamado.lineHeight + sAvisoDica.lineHeight;
        float largura = larguraAviso * e;
        float x = Screen.width - largura - margem * e;   // alinhado com a direita do celular
        float y = Screen.height - (margem + alturaIcone + folgaAviso) * e - altura + subida;
        Rect cartao = new Rect(x, y, largura, altura);

        // Um único tint para tudo (fundo, faixa e as três linhas de texto): é o que faz o aviso aparecer e sumir.
        Color anterior = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, alfa);
        try
        {
            DesenharRetangulo(cartao, CorCorpo);

            // Faixa na lateral: a cor diz o que é (o vermelho do selo é para "em aberto"; aqui é "coisa nova").
            float faixa = 6f * e;
            DesenharRetangulo(new Rect(cartao.x, cartao.y, faixa, cartao.height), CorAviso);

            float textoX = cartao.x + faixa + respiro;
            float textoLargura = cartao.width - faixa - respiro * 2f;
            float linha = cartao.y + respiro;

            GUI.Label(new Rect(textoX, linha, textoLargura, sAvisoEtiqueta.lineHeight),
                "NOVO CHAMADO", sAvisoEtiqueta);
            linha += sAvisoEtiqueta.lineHeight;
            GUI.Label(new Rect(textoX, linha, textoLargura, sAvisoChamado.lineHeight),
                "#" + avisoNumero + "   " + avisoTitulo, sAvisoChamado);
            linha += sAvisoChamado.lineHeight;
            GUI.Label(new Rect(textoX, linha, textoLargura, sAvisoDica.lineHeight),
                "Abra o celular (C) para ver os detalhes", sAvisoDica);
        }
        finally
        {
            GUI.color = anterior;
        }
    }

    /// <summary>A barra de abas no alto da "tela": CHAMADOS, CORES (só no minigame de rede) e o "X".</summary>
    private void DesenharAbas(Rect tela, float e, bool cores)
    {
        float altura = 34f * e;
        Rect barra = new Rect(tela.x + 4f * e, tela.y + 4f * e, tela.width - 8f * e, altura);

        // Fechar (X) no canto superior direito da "tela".
        bool fechar = GUI.Button(new Rect(barra.xMax - 34f * e, barra.y, 34f * e, altura), "X", sBotao);

        // As abas são sempre criadas (o mesmo número de controles em todos os eventos do OnGUI).
        float x = barra.x;
        bool irParaChamados = GUI.Button(new Rect(x, barra.y, 142f * e, altura), "CHAMADOS", cores ? sAba : sAbaAtiva);
        if (irParaChamados) mostrarCores = false;

        x += 146f * e;
        if (TemOrdemDeCores)
        {
            bool irParaCores = GUI.Button(new Rect(x, barra.y, 112f * e, altura), "CORES", cores ? sAbaAtiva : sAba);
            if (irParaCores) mostrarCores = true;
        }

        if (fechar) fecharPendente = true;
    }

    private void DesenharConteudo(Rect tela, float e, bool cores)
    {
        Rect area = new Rect(tela.x + 6f * e, tela.y + 42f * e, tela.width - 12f * e, tela.height - 48f * e);
        GUILayout.BeginArea(area);
        if (cores) DesenharOrdemDeCores(e);
        else DesenharChamados(e);
        GUILayout.EndArea();
    }

    private void DesenharChamados(float e)
    {
        if (ticketManager == null)
        {
            GUILayout.Label("Sem central de chamados.", sFraco);
            return;
        }

        float proximo = ticketManager.SecondsUntilNextArrival;
        if (proximo >= 0f)
            GUILayout.Label("Próximo chamado em " + Mathf.CeilToInt(proximo) + "s", sFraco);

        rolagem = GUILayout.BeginScrollView(rolagem);
        IReadOnlyList<TicketInstance> lista = ticketManager.Tickets;
        int mostrados = 0;
        for (int i = lista.Count - 1; i >= 0; i--)   // o mais novo em cima
        {
            TicketInstance chamado = lista[i];
            if (chamado == null || chamado.Definition == null) continue;
            DesenharChamado(chamado, e);
            mostrados++;
        }
        if (mostrados == 0) GUILayout.Label("Nenhum chamado por aqui.", sFraco);
        GUILayout.EndScrollView();
    }

    /// <summary>
    /// Um chamado RECOLHIDO mostra só o número e o título. As outras informações (solicitante, setor, descrição)
    /// aparecem depois de "Iniciar atendimento"; embaixo de cada chamado aberto desce a barra do tempo dele.
    /// </summary>
    private void DesenharChamado(TicketInstance chamado, float e)
    {
        GUILayout.BeginVertical(sCard);
        GUILayout.Label("#" + chamado.Number + "   " + chamado.Definition.title, sTitulo);

        if (chamado.Status == TicketStatus.Pending)
        {
            if (GUILayout.Button("Iniciar atendimento", sBotao, GUILayout.Height(32f * e)))
                iniciarChamadoPendente = chamado.Number;
        }
        else if (chamado.Status == TicketStatus.InProgress)
        {
            GUILayout.Label(chamado.Definition.requester + " - " + chamado.Definition.department, sFraco);
            if (!string.IsNullOrWhiteSpace(chamado.Definition.description))
                GUILayout.Label(chamado.Definition.description, sTexto);
        }
        else
        {
            GUILayout.Label(StatusDoChamado(chamado.Status), chamado.Status == TicketStatus.Completed ? sBom : sRuim);
        }

        if (!chamado.IsClosed)
        {
            Rect barra = GUILayoutUtility.GetRect(10f, 14f * e, GUILayout.ExpandWidth(true));
            TicketTimeBar.Desenhar(barra, chamado.TimeFraction);
        }

        GUILayout.EndVertical();
        GUILayout.Space(6f * e);
    }

    /// <summary>O app do padrão de cores (o mesmo desenho da tabela antiga, agora dentro do celular).</summary>
    private void DesenharOrdemDeCores(float e)
    {
        GUILayout.Label("PADRÃO T568B", sTitulo);
        GUILayout.Label("Ordem dos 8 fios, do 1 (ponta do conector) ao 8.", sFraco);
        GUILayout.Space(6f * e);

        for (int i = 0; i < RedeCores.OrdemT568B.Length; i++)
        {
            RedeFio fio = RedeCores.OrdemT568B[i];
            Rect linha = GUILayoutUtility.GetRect(10f, 46f * e, GUILayout.ExpandWidth(true));

            GUI.Label(new Rect(linha.x, linha.y, 26f * e, linha.height), (i + 1).ToString(), sFraco);

            Rect amostra = new Rect(linha.x + 30f * e, linha.y + 10f * e, 46f * e, 26f * e);
            DesenharAmostraDeFio(amostra, fio);

            GUI.Label(new Rect(amostra.xMax + 12f * e, linha.y, Mathf.Max(40f, linha.width - (amostra.xMax - linha.x) - 12f * e), linha.height),
                RedeCores.Nome(fio), sTexto);
        }
    }

    private static string StatusDoChamado(TicketStatus status)
    {
        switch (status)
        {
            case TicketStatus.Pending: return "Pendente";
            case TicketStatus.InProgress: return "Em atendimento";
            case TicketStatus.Completed: return "Concluído";
            case TicketStatus.Expired: return "Tempo esgotado - chamado perdido";
            default: return status.ToString();
        }
    }

    // ================================================================== desenho

    /// <summary>A mesma escala que o CanvasScaler das telas do jogo usa (referência 1920 x 1080).</summary>
    private static float Escala => Mathf.Sqrt((Screen.width / RefLargura) * (Screen.height / RefAltura));

    private static void DesenharRetangulo(Rect retangulo, Color cor)
    {
        Color anterior = GUI.color;
        GUI.color = anterior * cor;   // multiplica: assim o alfa do aviso (GUI.color) vale para tudo
        GUI.DrawTexture(retangulo, Texture2D.whiteTexture);
        GUI.color = anterior;
    }

    /// <summary>Amostra do fio como ele é desenhado na bancada: cor cheia e, nos "branco-X", listras brancas.</summary>
    private static void DesenharAmostraDeFio(Rect retangulo, RedeFio fio)
    {
        DesenharRetangulo(retangulo, RedeCores.CorDoDesenho(fio));
        if (!RedeCores.EhListrado(fio)) return;

        const int listras = 5;
        float largura = Mathf.Max(2f, retangulo.width * 0.07f);
        for (int i = 0; i < listras; i++)
        {
            float x = retangulo.x + retangulo.width * (i + 0.5f) / listras - largura * 0.5f;
            DesenharRetangulo(new Rect(x, retangulo.y, largura, retangulo.height), CorBrancoListra);
        }
    }

    /// <summary>Estilos do celular. GUIStyle guarda fonte e textura: monta de novo só quando a escala muda.</summary>
    private void GarantirEstilos(float escala)
    {
        if (sTitulo != null && Mathf.Approximately(escalaDosEstilos, escala)) return;
        escalaDosEstilos = escala;

        sTitulo = Base(19, CorTexto, escala, TextAnchor.MiddleLeft, true, true);
        sFraco = Base(15, CorFraco, escala, TextAnchor.MiddleLeft, false, true);
        sTexto = Base(16, CorTexto, escala, TextAnchor.UpperLeft, false, true);
        sBom = Base(16, CorVerde, escala, TextAnchor.MiddleLeft, true, false);
        sRuim = Base(16, CorVermelho, escala, TextAnchor.MiddleLeft, true, false);

        sCard = Base(18, CorTexto, escala, TextAnchor.UpperLeft, false, true);
        sCard.padding = new RectOffset(Pad(12, escala), Pad(12, escala), Pad(10, escala), Pad(10, escala));
        sCard.normal.background = Textura(ref tCard, CorCard);

        sBotao = Base(17, CorTexto, escala, TextAnchor.MiddleCenter, true, false);
        sBotao.padding = new RectOffset(Pad(8, escala), Pad(8, escala), Pad(5, escala), Pad(5, escala));
        sBotao.normal.background = Textura(ref tBotao, CorBotao);
        sBotao.hover.background = Textura(ref tBotaoForte, CorBotaoForte);
        sBotao.active.background = Textura(ref tBotaoApertado, CorBotaoApertado);
        sBotao.hover.textColor = Color.white;
        sBotao.active.textColor = CorTexto;

        sAba = Base(16, CorFraco, escala, TextAnchor.MiddleCenter, true, false);
        sAba.padding = new RectOffset(Pad(6, escala), Pad(6, escala), Pad(4, escala), Pad(4, escala));
        sAba.normal.background = Textura(ref tAba, CorAba);
        sAba.hover.background = Textura(ref tAbaAtiva, CorAbaAtiva);
        sAba.hover.textColor = CorTexto;

        sAbaAtiva = Base(16, Color.white, escala, TextAnchor.MiddleCenter, true, false);
        sAbaAtiva.padding = sAba.padding;
        sAbaAtiva.normal.background = Textura(ref tAbaAtiva, CorAbaAtiva);
        sAbaAtiva.hover.background = sAbaAtiva.normal.background;

        // Aviso de chamado novo que sobe acima do celular.
        sAvisoEtiqueta = Base(11, CorAviso, escala, TextAnchor.MiddleLeft, true, false);
        sAvisoChamado = Base(17, CorTexto, escala, TextAnchor.MiddleLeft, true, false);
        sAvisoDica = Base(12, CorFraco, escala, TextAnchor.MiddleLeft, false, false);
    }

    private static GUIStyle Base(int tamanho, Color cor, float escala, TextAnchor alinhamento, bool negrito, bool quebra)
    {
        var estilo = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.Max(9, Mathf.RoundToInt(tamanho * escala)),
            alignment = alinhamento,
            wordWrap = quebra,
            richText = false,
            fontStyle = negrito ? FontStyle.Bold : FontStyle.Normal,
            clipping = TextClipping.Clip
        };
        estilo.normal.textColor = cor;
        return estilo;
    }

    private static int Pad(float pixels, float escala) => Mathf.RoundToInt(pixels * escala);

    /// <summary>Textura 1x1 sólida (fundo de estilo). Criada uma vez e reaproveitada.</summary>
    private static Texture2D Textura(ref Texture2D guardada, Color cor)
    {
        if (guardada != null) return guardada;
        guardada = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
        guardada.SetPixel(0, 0, cor);
        guardada.Apply();
        return guardada;
    }

    // ================================================================== ajudantes do uGUI

    private static GameObject Retangulo(string nome, Transform pai, Color cor)
    {
        var go = new GameObject(nome, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(pai, false);
        var imagem = go.GetComponent<Image>();
        imagem.color = cor;
        imagem.raycastTarget = false;
        return go;
    }

    private static void Ancorar(RectTransform rt, Vector2 ancora, Vector2 tamanho, Vector2 pivor)
    {
        rt.anchorMin = rt.anchorMax = ancora;
        rt.pivot = pivor;
        rt.sizeDelta = tamanho;
    }

    private static void Esticar(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static Text Texto(Transform pai, string nome, string valor, int tamanho, TextAnchor alinhamento, Color cor, bool negrito = false)
    {
        var go = new GameObject(nome, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(pai, false);
        var texto = go.GetComponent<Text>();
        texto.font = Fonte();
        texto.text = valor;
        texto.fontSize = tamanho;
        texto.alignment = alinhamento;
        texto.color = cor;
        texto.raycastTarget = false;
        texto.horizontalOverflow = HorizontalWrapMode.Overflow;
        texto.verticalOverflow = VerticalWrapMode.Overflow;
        if (negrito) texto.fontStyle = FontStyle.Bold;
        return texto;
    }

    private static Font fonte;
    private static Font Fonte()
    {
        if (fonte == null)
        {
            fonte = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (fonte == null) fonte = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
        return fonte;
    }
}
