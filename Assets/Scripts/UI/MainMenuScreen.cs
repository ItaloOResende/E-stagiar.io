using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Menu inicial integrado ao StageController; estilo e animações reaproveitados do pause (do Matheus).
/// Configurações (volume e sensibilidade) gravadas no mesmo prefixo do PauseMenu ("Estagiario.Pause"): o que muda
/// aqui vale na pausa e vice-versa. Botões: Iniciar jogo (expediente), Tutorial (cena Tutorial_Montagem, opcional),
/// Controles (a página do PauseMenu), Configurações e Sair.</summary>
[DefaultExecutionOrder(-9000)]
[DisallowMultipleComponent]
public class MainMenuScreen : MonoBehaviour
{
    public static bool Showing { get; private set; }
    private static MainMenuScreen dono;
    [Header("Integração")]
    [SerializeField] private Key teclaVoltar = Key.Escape;
    [SerializeField] private TicketTabletPrototype tablet;
    [Tooltip("PlayerLook atual. O campo float sensitivity é conectado sem alterar seu script.")]
    [SerializeField] private MonoBehaviour controleOlhar;
    [SerializeField] private string campoSensibilidade = "sensitivity";
    [Tooltip("Arraste PlayerMovement, InteractionDetector e minigames que leem input diretamente. PlayerMovement, PlayerLook, InteractionDetector e tablet já são tratados automaticamente.")]
    [SerializeField] private Behaviour[] desativarDuranteMenu;
    [Tooltip("Objetos puramente visuais a esconder durante o menu, como mira ou cursor de ferramenta.")]
    [SerializeField] private GameObject[] ocultarDuranteMenu;

    [Header("Menu principal")]
    [SerializeField] private StageController stage;
    [Tooltip("Opcional: copiar visual e tempos do PauseMenu pelo botão no Inspector.")]
    [SerializeField] private PauseMenu referenciaPause;
    [SerializeField] private Sprite spriteFundo;
    [SerializeField] private Color corFundo = Color.white;
    [SerializeField] private Sprite logo;
    [SerializeField] private Vector2 tamanhoLogo = new Vector2(420, 100);
    [SerializeField] private Vector2 posicaoLogo = new Vector2(0, -85);
    [Tooltip("Botão do tutorial (segunda linha do menu): carrega a cena Tutorial_Montagem. Opcional para o jogador.")]
    [SerializeField] private string textoTutorial = "TUTORIAL";
    [SerializeField] private UnityEvent aoIniciar = new UnityEvent();
    private Image fundoUI, logoUI;
    [SerializeField, HideInInspector] private Button tutorialUI;
    [Tooltip("Botão que abre a página de controles do PauseMenu da cena, sem iniciar a partida.")]
    [SerializeField] private string textoControles = "CONTROLES";
    [SerializeField, HideInInspector] private Button controlesUI;

    [Header("Volume")]
    [SerializeField] private AudioMixer mixer;
    [Tooltip("Parâmetro exposto do mixer em dB. Sem mixer, usa AudioListener.volume.")]
    [SerializeField] private string parametroVolume = "MasterVolume";
    [SerializeField, Range(0, 1)] private float volumeInicial = 1;
    [SerializeField, Range(-90, -40)] private float volumeMinimoDb = -80;
    [Header("Sensibilidade")]
    [SerializeField] private float sensibilidadeMinima = .01f;
    [SerializeField] private float sensibilidadeMaxima = .5f;
    [Tooltip("Sem preferência salva, lê o valor atual do PlayerLook antes de aplicar este fallback.")]
    [SerializeField] private float sensibilidadeInicial = .1f;
    [Tooltip("Opcional: recebe o valor do slider para conectar outro sistema de câmera.")]
    [SerializeField] private UnityEvent<float> aoMudarSensibilidade = new UnityEvent<float>();
    [SerializeField] private bool salvarPreferencias = true;
    [SerializeField] private string prefixoPreferencias = "Estagiario.Pause";

    [Header("Layout e prévia")]
    [SerializeField] private bool previaNoEditor;
    [SerializeField] private bool previaConfiguracoes;
    [SerializeField] private Vector2 tamanhoPainel = new Vector2(780, 740);
    [SerializeField, Range(.4f, 1.6f)] private float escalaMenu = 1;
    [SerializeField] private Vector2 posicaoMenu;
    [SerializeField] private Vector2 tamanhoBotao = new Vector2(440, 72);
    [SerializeField, Min(10)] private float espacoBotoes = 24;
    [SerializeField] private float inicioBotoes = 245;
    [SerializeField] private Vector2 tamanhoSlider = new Vector2(500, 24);
    [SerializeField] private Vector2 posicaoTitulo = new Vector2(0, -82);
    [SerializeField] private Vector2 posicaoVolume = new Vector2(0, -258);
    [SerializeField] private Vector2 posicaoSensibilidade = new Vector2(0, -398);
    [SerializeField] private Vector2 posicaoVoltar = new Vector2(0, -545);
    [SerializeField] private float distanciaLabelSlider = 53;
    [SerializeField] private Color corTrilhoSlider = new Color(.04f, .08f, .13f);
    [SerializeField] private Color corAlcaSlider = Color.white;
    [SerializeField] private Vector2 tamanhoAlcaSlider = new Vector2(32, 38);
    [SerializeField] private string textoVolume = "VOLUME";
    [SerializeField] private string textoSensibilidade = "SENSIBILIDADE";
    [SerializeField] private float tamanhoTitulo = 48;
    [SerializeField] private float tamanhoTexto = 24;
    [Tooltip("Multiplica botões, sliders e textos. O conteúdo de cada página fica centralizado no card, e o card " +
             "acompanha a altura da página aberta.")]
    [SerializeField, Range(.8f, 1.5f)] private float escalaElementos = 1.15f;
    [Tooltip("Folga vertical (em cima e embaixo) entre o conteúdo e a borda do card.")]
    [SerializeField, Min(20)] private float margemCard = 64;
    [Tooltip("Tamanho do nome do jogo, que fica ACIMA do card (fora dele).")]
    [SerializeField] private float tamanhoTituloJogo = 96;
    [SerializeField, Min(0)] private float espacoTituloCard = 28;
    [SerializeField] private string tituloMenu = "E-stagiar.io";
    [SerializeField] private string tituloConfiguracoes = "CONFIGURAÇÕES";
    [SerializeField] private string textoIniciar = "INICIAR JOGO";
    [SerializeField] private string textoConfiguracoes = "CONFIGURAÇÕES";
    [SerializeField] private string textoSair = "SAIR";
    [SerializeField] private string textoVoltar = "VOLTAR";

    [Header("Animações (tempo real, mesmo pausado)")]
    [SerializeField, Min(.05f)] private float duracaoAbertura = .2f;
    [SerializeField, Min(.05f)] private float duracaoFechamento = .18f;
    [SerializeField, Min(.05f)] private float duracaoBounce = .45f;
    [SerializeField, Min(0)] private float intervaloBotoes = .12f;
    [SerializeField, Range(0, 3)] private float intensidadeBounce = 1.5f;
    [SerializeField] private float deslocamentoEntrada = 35;
    [SerializeField, Min(.05f)] private float duracaoPagina = .38f;
    [SerializeField, Range(1, 1.2f)] private float escalaHover = 1.04f;
    [SerializeField, Min(1)] private float velocidadeHover = 16;

    [SerializeField] private Color corEscurecimento = new Color(.015f, .025f, .045f, .5f);

    [Header("Arte e estilo (mesmo padrão do tablet)")]
    [SerializeField] private TMP_FontAsset fonte;
    [SerializeField] private Sprite spritePainel;
    [SerializeField] private Color corPainel = new Color(.08f, .12f, .18f, .94f);
    [SerializeField] private Color corTexto = new Color(.94f, .97f, 1, 1);
    [SerializeField] private Color corDestaque = new Color(.25f, .85f, .77f, 1);
    [SerializeField, Range(0, 40)] private float raioBotoes = 12;
    [SerializeField] private Color gradienteTopo = new Color(.32f, .46f, .58f, 1);
    [SerializeField] private Color gradienteBase = new Color(.14f, .23f, .32f, 1);
    [SerializeField] private Color corInnerGlow = new Color(.65f, .92f, 1, .75f);
    [SerializeField, Range(0, 20)] private float larguraInnerGlow = 5;
    [SerializeField, Range(0, 1)] private float intensidadeInnerGlow = .45f;
    [SerializeField] private Color corOuterGlow = new Color(.2f, .8f, 1, .65f);
    [SerializeField, Range(0, 40)] private float larguraOuterGlow = 12;
    [SerializeField, Range(0, 1)] private float intensidadeOuterGlow = .4f;
    [SerializeField] private Color corOutlineTexto = new Color(.02f, .035f, .06f, 1);
    [SerializeField, Range(0, .4f)] private float larguraOutlineTexto = .13f;
    [SerializeField] private Color corSombraTexto = new Color(0, 0, 0, .65f);
    [SerializeField] private Vector2 deslocamentoSombraTexto = new Vector2(.5f, -.5f);
    [SerializeField, Range(0, 1)] private float suavidadeSombraTexto = .25f;
    [SerializeField, Range(-1, 1)] private float expandirSombraTexto = .05f;
    [Serializable] private class BordaESombra
    {
        public bool outlineAtivo;
        public Color corOutline = new Color(.02f, .04f, .07f, 1);
        [Range(0, 8)] public float espessuraOutline = 2;
        public bool sombraAtiva;
        public Color corSombra = new Color(0, 0, 0, .45f);
        public Vector2 deslocamentoSombra = new Vector2(4, -5);
    }
    [SerializeField] private BordaESombra efeitosBotoes = new BordaESombra();
    [Header("Cursor e sons (opcionais)")]
    [SerializeField] private Sprite spriteCursor;
    [SerializeField] private Vector2 tamanhoCursor = new Vector2(42, 50);
    [SerializeField] private Vector2 pontaCursor = new Vector2(.1f, .9f);
    [SerializeField] private AudioSource audioUI;
    [SerializeField] private AudioClip somAbrir, somFechar, somClique, somHover;

    [Header("Interface gerada — editável na hierarquia")]
    [SerializeField] private RectTransform raiz, painel, paginaMenu, paginaConfig, cursorUI;
    [SerializeField] private Canvas canvas;
    [SerializeField] private CanvasGroup grupo;
    [SerializeField] private Image escurecimento;
    [SerializeField] private TMP_Text tituloMenuUI, tituloConfigUI, valorVolumeUI, valorSensUI, mensagemUI;
    [SerializeField] private Button iniciarUI, configuracoesUI, sairUI, voltarUI;
    [SerializeField] private Slider volumeUI, sensUI;
    [SerializeField] private RectTransform[] entradasMenu;

    private enum Estado { Fechado, Aberto, Fechando }
    private Estado estado;
    private int larguraTela, alturaTela;
    private bool iniciou, carregandoCena, preferenciasSujas;
    private bool configuracoesProntas, avisouMixer;
    private float escalaTempoAnterior, aberturaEm, volumeAtual, sensAtual, pagina, paginaOrigem, paginaAlvo, paginaInicio;
    private float alturaPaginaMenu = 740, alturaPaginaConfig = 740, larguraPainel = 780, escalaAtual = 1, alturaTituloJogo = 120;
    private CursorLockMode cursorLockAnterior;
    private bool cursorVisivelAnterior;
    private FieldInfo campoMouse;
    private readonly Dictionary<Behaviour, bool> controles = new Dictionary<Behaviour, bool>();
    private readonly Dictionary<GameObject, bool> visibilidade = new Dictionary<GameObject, bool>();
    private readonly List<Hover> hovers = new List<Hover>();
    private readonly List<UnityEngine.Object> recursosVisuais = new List<UnityEngine.Object>();
    private readonly Dictionary<Material, Material> materiais = new Dictionary<Material, Material>();
    private Vector2[] entradasBase;
    private CanvasGroup[] entradasAlpha;
    private class Hover { public Button botao; public float escala = 1; public bool sobre; }

    private void Awake()
    {
        if (dono != null && dono != this) { enabled = false; return; }
        dono = this;
        if (stage == null) stage = GetComponent<StageController>();
        if (controleOlhar == null) controleOlhar = FindFirstObjectByType<PlayerLook>();
        if (tablet == null) tablet = FindFirstObjectByType<TicketTabletPrototype>();
        // criado em tempo de execução (StageController → Open): mesmo visual, sons e preferências do PauseMenu da cena
        if (referenciaPause == null) referenciaPause = FindFirstObjectByType<PauseMenu>();
        if (raiz == null && referenciaPause != null) CopiarEstiloDoPause();
        if (audioUI == null)
        {
            audioUI = gameObject.AddComponent<AudioSource>();
            audioUI.playOnAwake = false; audioUI.spatialBlend = 0f; audioUI.ignoreListenerPause = true;
        }
        if (raiz == null) GerarInterface();
        AtualizarLayout(); AtualizarAparencia(); VincularBotoes(); ConfigurarSensibilidade();
        estado = Estado.Fechado; raiz.gameObject.SetActive(false); iniciou = true;
    }
    public static MainMenuScreen Open(StageController controller)
    {
        if (controller == null) return null;
        var menu = controller.GetComponent<MainMenuScreen>();
        if (menu == null) menu = controller.gameObject.AddComponent<MainMenuScreen>();
        menu.stage = controller; menu.enabled = true; menu.AbrirMenu(); return menu;
    }
    private void OnValidate()
    {
        sensibilidadeMinima = Mathf.Max(.0001f, sensibilidadeMinima);
        sensibilidadeMaxima = Mathf.Max(sensibilidadeMinima + .001f, sensibilidadeMaxima);
        tamanhoPainel.x = Mathf.Max(300, tamanhoPainel.x); tamanhoPainel.y = Mathf.Max(300, tamanhoPainel.y);
        tamanhoBotao.x = Mathf.Max(80, tamanhoBotao.x); tamanhoBotao.y = Mathf.Max(24, tamanhoBotao.y);
        duracaoBounce = Mathf.Max(.05f, duracaoBounce); duracaoPagina = Mathf.Max(.05f, duracaoPagina);
    }
    private void CarregarPreferencias()
    {
        volumeAtual = salvarPreferencias ? PlayerPrefs.GetFloat(prefixoPreferencias + ".Volume", volumeInicial) : volumeInicial;
        float inicial = campoMouse != null && controleOlhar != null ? (float)campoMouse.GetValue(controleOlhar) : sensibilidadeInicial;
        sensAtual = salvarPreferencias ? PlayerPrefs.GetFloat(prefixoPreferencias + ".Sensitivity", inicial) : inicial;
        volumeAtual = Mathf.Clamp01(volumeAtual); sensAtual = Mathf.Clamp(sensAtual, sensibilidadeMinima, sensibilidadeMaxima);
        volumeUI.SetValueWithoutNotify(volumeAtual); sensUI.SetValueWithoutNotify(sensAtual);
        configuracoesProntas = true; AplicarVolume(volumeAtual); AplicarSensibilidade(sensAtual); preferenciasSujas = false;
        if (EventSystem.current == null) Debug.LogWarning("MainMenuScreen: configure um EventSystem com InputSystemUIInputModule na cena.", this);
    }
    private void Start() { if (!configuracoesProntas) CarregarPreferencias(); }
    private void OnEnable() { if (Application.isPlaying) Canvas.willRenderCanvases += GarantirCursor; }
    private void OnDisable()
    {
        Canvas.willRenderCanvases -= GarantirCursor;
        if (!Application.isPlaying) { LimparEstilos(); return; }
        StopAllCoroutines(); Salvar();
        if (Showing && dono == this) RestaurarJogo(false);
        if (raiz != null) raiz.gameObject.SetActive(false);
    }
    private void OnDestroy() { LimparEstilos(); if (dono == this) { dono = null; Showing = false; } }
    private void OnApplicationQuit() { Salvar(); }

    private void Update()
    {
        if (!iniciou) return;
        if (Keyboard.current != null && teclaVoltar != Key.None && Keyboard.current[teclaVoltar].wasPressedThisFrame)
        {
            if (AvisoSairAberto) FecharAvisoSair();
            else if (estado == Estado.Aberto && paginaAlvo > .5f) MostrarPagina(false);
        }
        if (estado != Estado.Aberto) return;
        if (Screen.width != larguraTela || Screen.height != alturaTela) { Canvas.ForceUpdateCanvases(); AtualizarLayout(); }
        float dt = Time.unscaledDeltaTime;
        grupo.alpha = Mathf.Clamp01((Time.unscaledTime - aberturaEm) / Mathf.Max(.05f, duracaoAbertura));
        float u = Mathf.Clamp01((Time.unscaledTime - paginaInicio) / Mathf.Max(.05f, duracaoPagina));
        pagina = Mathf.Lerp(paginaOrigem, paginaAlvo, u * u * (3 - 2 * u)); PosicionarPaginas();
        bool transitando = u < 1;
        paginaMenu.GetComponent<CanvasGroup>().interactable = !transitando && paginaAlvo < .5f;
        paginaMenu.GetComponent<CanvasGroup>().blocksRaycasts = !transitando && paginaAlvo < .5f;
        paginaConfig.GetComponent<CanvasGroup>().interactable = !transitando && paginaAlvo > .5f;
        paginaConfig.GetComponent<CanvasGroup>().blocksRaycasts = !transitando && paginaAlvo > .5f;
        for (int i = 0; i < entradasMenu.Length; i++)
        {
            float t = Mathf.Clamp01((Time.unscaledTime - aberturaEm - i * intervaloBotoes) / Mathf.Max(.05f, duracaoBounce));
            float b = EaseOutBack(t, intensidadeBounce);
            entradasMenu[i].localScale = Vector3.one * Mathf.LerpUnclamped(.65f, 1, b);
            entradasMenu[i].anchoredPosition = entradasBase[i] + Vector2.down * deslocamentoEntrada * (1 - b);
            entradasAlpha[i].alpha = Mathf.Clamp01(t * 3);
            entradasAlpha[i].interactable = t >= .95f; entradasAlpha[i].blocksRaycasts = t >= .95f;
        }
        // página do pause aberta por cima (controles): os botões do menu ficam cobertos, sem hover
        bool coberto = (referenciaPause != null && referenciaPause.PaginaAvulsaAberta) || AvisoSairAberto;
        Vector2 mouse = Mouse.current != null && !coberto ? Mouse.current.position.ReadValue() : Vector2.one * -10000;
        foreach (var h in hovers)
        {
            bool sobre = h.botao.IsActive() && h.botao.IsInteractable() && RectTransformUtility.RectangleContainsScreenPoint((RectTransform)h.botao.transform, mouse, null);
            if (sobre && !h.sobre) Tocar(somHover);
            h.sobre = sobre; h.escala = Mathf.Lerp(h.escala, sobre ? escalaHover : 1, 1 - Mathf.Exp(-velocidadeHover * dt));
            h.botao.transform.localScale = Vector3.one * h.escala;
        }
    }
    public void AbrirMenu()
    {
        if (!iniciou || estado != Estado.Fechado || (dono != null && dono != this)) return;
        if (stage != null && stage.IsRunning) { Debug.LogWarning("Volte ao menu recarregando a cena com GameSession.GoToMainMenu().", this); return; }
        dono = this;
        CarregarPreferencias();
        Showing = true; escalaTempoAnterior = Time.timeScale;
        cursorLockAnterior = Cursor.lockState; cursorVisivelAnterior = Cursor.visible;
        controles.Clear();
        foreach (var c in FindObjectsByType<PlayerLook>(FindObjectsSortMode.None)) GuardarControle(c);
        foreach (var c in FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None)) GuardarControle(c);
        foreach (var c in FindObjectsByType<InteractionDetector>(FindObjectsSortMode.None)) GuardarControle(c);
        foreach (var c in FindObjectsByType<TicketTabletPrototype>(FindObjectsSortMode.None)) { c.Fechar(); GuardarControle(c); }
        GuardarControle(controleOlhar);
        if (desativarDuranteMenu != null) foreach (var c in desativarDuranteMenu) GuardarControle(c);
        foreach (var c in controles) if (c.Key != null) c.Key.enabled = false;
        visibilidade.Clear();
        if (ocultarDuranteMenu != null) foreach (var go in ocultarDuranteMenu)
            if (go != null && go != gameObject && !transform.IsChildOf(go.transform) && !go.transform.IsChildOf(raiz) && go.transform != raiz && !visibilidade.ContainsKey(go))
            { visibilidade.Add(go, go.activeSelf); go.SetActive(false); }
        Time.timeScale = 0;
        UICursor.Reset(); Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        raiz.gameObject.SetActive(true); Canvas.ForceUpdateCanvases(); AtualizarLayout();
        grupo.alpha = 0; grupo.interactable = grupo.blocksRaycasts = true;
        pagina = paginaOrigem = paginaAlvo = 0; paginaInicio = Time.unscaledTime - duracaoPagina;
        aberturaEm = Time.unscaledTime; mensagemUI.text = ""; carregandoCena = false;
        for (int i = 0; i < entradasMenu.Length; i++)
        { entradasAlpha[i].alpha = 0; entradasAlpha[i].interactable = entradasAlpha[i].blocksRaycasts = false; }
        PosicionarPaginas(); estado = Estado.Aberto; Tocar(somAbrir);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }
    private void GuardarControle(Behaviour c)
    {
        if (c != null && c != this && c != audioUI && !controles.ContainsKey(c)) controles.Add(c, c.enabled);
    }

    // Pode ser conectado ao evento de saída do PauseMenu no Inspector.
    public void VoltarAoMenuInicial() { GameSession.GoToMainMenu(); }
    public void IniciarJogo() { Choose(GameMode.Normal); }
    /// <summary>Menu → tutorial (cena Tutorial_Montagem). O expediente continua no "Iniciar jogo", sem obrigação.</summary>
    public void IniciarTutorial()
    {
        if (estado != Estado.Aberto || carregandoCena) return;
        carregandoCena = true; StartCoroutine(TutorialAnimado());
    }
    private IEnumerator TutorialAnimado()
    {
        yield return FecharAnimado(false);
        GameSession.StartTutorial();
    }
    public void Choose(GameMode mode)
    {
        if (estado != Estado.Aberto || carregandoCena) return;
        if (stage == null) { mensagemUI.text = "Atribua o StageController no Inspector."; return; }
        carregandoCena = true; StartCoroutine(IniciarAnimado(mode));
    }
    private IEnumerator IniciarAnimado(GameMode mode)
    {
        yield return FecharAnimado(false);
        stage.StartStageWithMode(mode);
        aoIniciar.Invoke();
        carregandoCena = false;
    }
    public void MostrarConfiguracoes() { if (estado == Estado.Aberto) MostrarPagina(true); }
    /// <summary>Controles: a página do PauseMenu da cena, aberta por cima do menu (Voltar/Esc volta ao menu).</summary>
    public void MostrarControles()
    {
        if (estado != Estado.Aberto || carregandoCena) return;
        if (referenciaPause == null) referenciaPause = FindFirstObjectByType<PauseMenu>();
        if (referenciaPause != null) referenciaPause.AbrirControlesAvulsos();
        else mensagemUI.text = "Esta cena não tem a tela de controles (PauseMenu).";
    }
    public void Voltar() { if (estado == Estado.Aberto) MostrarPagina(false); }
    private void MostrarPagina(bool config)
    {
        paginaOrigem = pagina; paginaAlvo = config ? 1 : 0; paginaInicio = Time.unscaledTime;
        if (!config) Salvar();
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }
    public void SairDoJogo()
    {
        if (estado != Estado.Aberto || carregandoCena) return;
        // no navegador Application.Quit não faz nada (o menu sumia e ficava só o fundo escuro): avisa para fechar a aba
        if (Application.platform == RuntimePlatform.WebGLPlayer) { AbrirAvisoSair(); return; }
        carregandoCena = true; StartCoroutine(FecharAnimado(true));
    }
    private RectTransform avisoSair;
    private bool AvisoSairAberto => avisoSair != null && avisoSair.gameObject.activeSelf;
    private void AbrirAvisoSair()
    {
        if (avisoSair == null)
        {
            // por cima do menu inteiro, bloqueando os botões dele; card com o mesmo fundo do painel do menu
            avisoSair = Imagem("AvisoSair", raiz, new Color(0, 0, 0, .55f), true).rectTransform; Stretch(avisoSair);
            var fundoPainel = painel.GetComponent<Image>();
            var card = Imagem("Card", avisoSair, fundoPainel.color, true);
            card.sprite = fundoPainel.sprite; card.type = fundoPainel.type;
            Centro(card.rectTransform, Vector2.zero, new Vector2(760, 380));
            var titulo = Texto(card.transform, "Titulo", "SAIR DO JOGO", tamanhoTitulo);
            titulo.fontStyle = FontStyles.Bold; titulo.color = corDestaque;
            Centro(titulo.rectTransform, new Vector2(0, 120), new Vector2(680, 70));
            var texto = Texto(card.transform, "Texto", "Para encerrar o jogo, é só fechar esta aba do navegador.", tamanhoTexto);
            texto.color = corTexto; Centro(texto.rectTransform, new Vector2(0, 20), new Vector2(640, 100));
            var holder = Rect("Voltar", card.transform); Centro(holder, new Vector2(0, -110), tamanhoBotao);
            var voltar = Imagem("Botao", holder, Color.white, true); Stretch(voltar.rectTransform);
            var b = voltar.gameObject.AddComponent<Button>(); b.targetGraphic = voltar;
            b.navigation = new Navigation { mode = Navigation.Mode.None };
            if (referenciaPause != null) referenciaPause.EstilizarBotaoExterno(b, "VOLTAR AO JOGO");
            else Texto(voltar.transform, "Texto", "VOLTAR AO JOGO", tamanhoTexto);
            b.onClick.AddListener(() => { Tocar(somClique); FecharAvisoSair(); });
        }
        avisoSair.SetAsLastSibling(); avisoSair.gameObject.SetActive(true);
        if (cursorUI != null) cursorUI.SetAsLastSibling();
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }
    private void FecharAvisoSair()
    {
        if (avisoSair != null) avisoSair.gameObject.SetActive(false);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }
    private IEnumerator FecharAnimado(bool sair)
    {
        estado = Estado.Fechando; grupo.interactable = false; Tocar(somFechar);
        float alpha = grupo.alpha, inicio = Time.unscaledTime;
        while (Time.unscaledTime - inicio < Mathf.Max(.05f, duracaoFechamento))
        { grupo.alpha = alpha * (1 - Mathf.Clamp01((Time.unscaledTime - inicio) / Mathf.Max(.05f, duracaoFechamento))); yield return null; }
        Salvar(); RestaurarJogo(!sair); raiz.gameObject.SetActive(false);
        if (sair)
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
    private void RestaurarJogo(bool iniciar)
    {
        Time.timeScale = escalaTempoAnterior;
        foreach (var c in controles) if (c.Key != null) c.Key.enabled = c.Value;
        controles.Clear();
        foreach (var item in visibilidade) if (item.Key != null) item.Key.SetActive(item.Value);
        visibilidade.Clear();
        UICursor.LimparSobreposicao();
        Cursor.lockState = iniciar ? CursorLockMode.Locked : cursorLockAnterior;
        Cursor.visible = iniciar ? false : cursorVisivelAnterior;
        estado = Estado.Fechado; Showing = false;
    }
    private void GarantirCursor()
    {
        if (estado == Estado.Fechado || raiz == null || !raiz.gameObject.activeInHierarchy) return;
        Cursor.lockState = CursorLockMode.None;
        bool sprite = spriteCursor != null && Mouse.current != null;
        Cursor.visible = !sprite; cursorUI.gameObject.SetActive(sprite);
        // sem sprite próprio: a MESMA mãozinha das telas 2D, na camada de cima do UICursor (ao fechar, o cursor
        // da tela de trás volta sozinho)
        if (sprite) UICursor.LimparSobreposicao(); else UICursor.SobreporMao();
        if (sprite)
        {
            cursorUI.GetComponent<Image>().sprite = spriteCursor; cursorUI.sizeDelta = tamanhoCursor; cursorUI.pivot = pontaCursor;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(raiz, Mouse.current.position.ReadValue(), null, out Vector2 p);
            cursorUI.anchoredPosition = p;
        }
    }
    private void ConfigurarSensibilidade()
    {
        campoMouse = null;
        if (controleOlhar == null) return;
        for (Type t = controleOlhar.GetType(); t != null; t = t.BaseType)
        {
            var f = t.GetField(campoSensibilidade, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (f != null && f.FieldType == typeof(float) && !f.IsInitOnly) { campoMouse = f; return; }
        }
        Debug.LogWarning("MainMenuScreen: não foi encontrado o campo float '" + campoSensibilidade + "' em " + controleOlhar.GetType().Name + ". Configure Ao Mudar Sensibilidade para outra integração.", this);
    }
    private void AplicarVolume(float valor)
    {
        volumeAtual = Mathf.Clamp01(valor);
        if (mixer != null)
        {
            float db = volumeAtual <= .0001f ? volumeMinimoDb : Mathf.Max(volumeMinimoDb, 20 * Mathf.Log10(volumeAtual));
            if (!mixer.SetFloat(parametroVolume, db) && !avisouMixer)
            { avisouMixer = true; Debug.LogWarning("MainMenuScreen: exponha o parâmetro '" + parametroVolume + "' no AudioMixer.", this); }
        }
        else AudioListener.volume = volumeAtual;
        valorVolumeUI.text = textoVolume + "   " + Mathf.RoundToInt(volumeAtual * 100) + "%";
        preferenciasSujas = true;
    }
    private void AplicarSensibilidade(float valor)
    {
        sensAtual = Mathf.Clamp(valor, sensibilidadeMinima, sensibilidadeMaxima);
        if (campoMouse != null && controleOlhar != null) campoMouse.SetValue(controleOlhar, sensAtual);
        aoMudarSensibilidade.Invoke(sensAtual);
        valorSensUI.text = textoSensibilidade + "   " + sensAtual.ToString("0.000"); preferenciasSujas = true;
    }
    private void Salvar()
    {
        if (!salvarPreferencias || !configuracoesProntas || !preferenciasSujas) return;
        PlayerPrefs.SetFloat(prefixoPreferencias + ".Volume", volumeAtual);
        PlayerPrefs.SetFloat(prefixoPreferencias + ".Sensitivity", sensAtual); PlayerPrefs.Save(); preferenciasSujas = false;
    }
    private void Tocar(AudioClip clip) { if (clip != null && audioUI != null) audioUI.PlayOneShot(clip); }



    private static float EaseOutBack(float t, float intensidade) { float x = t - 1; return 1 + (intensidade + 1) * x * x * x + intensidade * x * x; }

    [ContextMenu("Menu principal/Gerar interface editável")]
    public void GerarInterface()
    {
        if (raiz != null) return;
        raiz = new GameObject("MenuPrincipalUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup)).GetComponent<RectTransform>();
        raiz.SetParent(transform, false); raiz.pivot = Vector2.one * .5f;
        canvas = raiz.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32760;
        var scaler = raiz.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
        grupo = raiz.GetComponent<CanvasGroup>();
        fundoUI = Imagem("ArteFundo", raiz, corFundo); Stretch(fundoUI.rectTransform); fundoUI.sprite = spriteFundo; fundoUI.gameObject.SetActive(spriteFundo != null);
        escurecimento = Imagem("BloqueioEFundo", raiz, corEscurecimento, true); Stretch(escurecimento.rectTransform);
        painel = Imagem("Painel", raiz, corPainel, true).rectTransform; painel.sizeDelta = tamanhoPainel;
        // Folga para glow e bounce dentro de cada página; máscara impede páginas vazarem lateralmente.
        painel.gameObject.AddComponent<RectMask2D>();
        paginaMenu = Rect("PaginaPrincipal", painel); Stretch(paginaMenu); paginaMenu.gameObject.AddComponent<CanvasGroup>();
        paginaConfig = Rect("PaginaConfiguracoes", painel); Stretch(paginaConfig); paginaConfig.gameObject.AddComponent<CanvasGroup>();
        tituloMenuUI = Texto(raiz, "TituloJogo", tituloMenu, tamanhoTituloJogo); // acima do card, fora da máscara
        tituloConfigUI = Texto(paginaConfig, "Titulo", tituloConfiguracoes, tamanhoTitulo);
        entradasMenu = new RectTransform[5];
        entradasMenu[0] = Holder("Iniciar", paginaMenu); iniciarUI = Botao(entradasMenu[0], textoIniciar);
        entradasMenu[1] = Holder("Tutorial", paginaMenu); tutorialUI = Botao(entradasMenu[1], textoTutorial);
        entradasMenu[2] = Holder("Controles", paginaMenu); controlesUI = Botao(entradasMenu[2], textoControles);
        entradasMenu[3] = Holder("Configuracoes", paginaMenu); configuracoesUI = Botao(entradasMenu[3], textoConfiguracoes);
        entradasMenu[4] = Holder("Sair", paginaMenu); sairUI = Botao(entradasMenu[4], textoSair);
        logoUI = Imagem("Logo", paginaMenu, Color.white); logoUI.preserveAspect = true;
        valorVolumeUI = Texto(paginaConfig, "VolumeLabel", "VOLUME", tamanhoTexto);
        valorSensUI = Texto(paginaConfig, "SensibilidadeLabel", "SENSIBILIDADE", tamanhoTexto);
        volumeUI = CriarSlider(paginaConfig, "Volume", 0, 1);
        sensUI = CriarSlider(paginaConfig, "Sensibilidade", sensibilidadeMinima, sensibilidadeMaxima);
        var holderVoltar = Holder("Voltar", paginaConfig); voltarUI = Botao(holderVoltar, textoVoltar);
        mensagemUI = Texto(painel, "Mensagem", "", 18); mensagemUI.color = new Color(1, .65f, .4f);
        cursorUI = Imagem("CursorPause", raiz, Color.white, false).rectTransform; cursorUI.gameObject.SetActive(false);
        AtualizarLayout(); AtualizarAparencia();
        if (!Application.isPlaying) raiz.gameObject.SetActive(previaNoEditor);
    }
    private RectTransform Holder(string nome, Transform pai)
    { var rt = Rect(nome, pai); rt.gameObject.AddComponent<CanvasGroup>(); return rt; }
    private Button Botao(Transform pai, string label)
    {
        var img = Imagem("Botao", pai, Color.white, true); Stretch(img.rectTransform);
        var b = img.gameObject.AddComponent<Button>(); b.targetGraphic = img;
        b.navigation = new Navigation { mode = Navigation.Mode.None };
        var texto = Texto(img.transform, "Texto", label, tamanhoTexto); Stretch(texto.rectTransform, new Vector2(12, 5), new Vector2(-12, -5));
        return b;
    }
    private Slider CriarSlider(Transform pai, string nome, float min, float max)
    {
        var rt = Rect(nome, pai); var slider = rt.gameObject.AddComponent<Slider>();
        var bg = Imagem("Trilho", rt, new Color(.04f, .08f, .13f), true); Stretch(bg.rectTransform);
        var fillArea = Rect("AreaPreenchimento", rt); Stretch(fillArea, new Vector2(4, 4), new Vector2(-4, -4));
        var fill = Imagem("Preenchimento", fillArea, corDestaque); Stretch(fill.rectTransform);
        var area = Rect("AreaAlca", rt); Stretch(area, new Vector2(12, 0), new Vector2(-12, 0));
        var handle = Imagem("Alca", area, Color.white, true); handle.rectTransform.sizeDelta = new Vector2(32, 38);
        handle.rectTransform.anchorMin = new Vector2(.5f, .5f); handle.rectTransform.anchorMax = new Vector2(.5f, .5f);
        slider.fillRect = fill.rectTransform; slider.handleRect = handle.rectTransform; slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight; slider.minValue = min; slider.maxValue = Mathf.Max(min + .001f, max);
        slider.navigation = new Navigation { mode = Navigation.Mode.None }; return slider;
    }
    private void VincularBotoes()
    {
        hovers.Clear();
        Ligar(iniciarUI, IniciarJogo); Ligar(tutorialUI, IniciarTutorial);
        if (controlesUI != null) Ligar(controlesUI, MostrarControles);
        Ligar(configuracoesUI, MostrarConfiguracoes); Ligar(sairUI, SairDoJogo); Ligar(voltarUI, Voltar);
        volumeUI.onValueChanged.AddListener(AplicarVolume); sensUI.onValueChanged.AddListener(AplicarSensibilidade);
        entradasBase = new Vector2[entradasMenu.Length]; entradasAlpha = new CanvasGroup[entradasMenu.Length];
        for (int i = 0; i < entradasMenu.Length; i++) { entradasBase[i] = entradasMenu[i].anchoredPosition; entradasAlpha[i] = entradasMenu[i].GetComponent<CanvasGroup>(); }
    }
    private void Ligar(Button b, UnityAction acao)
    { b.onClick.AddListener(() => { Tocar(somClique); acao(); }); hovers.Add(new Hover { botao = b }); }
    public void AtualizarLayout()
    {
        if (raiz == null)
        {
            if (!Application.isPlaying && previaNoEditor) GerarInterface();
            if (raiz == null) return;
        }
        larguraTela = Screen.width; alturaTela = Screen.height;
        // O nome do jogo fica fora do card, acima dele (mesmo quando a interface veio de uma versão antiga).
        if (tituloMenuUI.transform.parent != raiz) { tituloMenuUI.transform.SetParent(raiz, false); tituloMenuUI.name = "TituloJogo"; }
        tituloMenuUI.transform.SetSiblingIndex(painel.GetSiblingIndex() + 1);
        int visiveis = 0;
        foreach (var e in entradasMenu) if (e != null && e.gameObject.activeSelf) visiveis++;
        // Cada página é uma coluna centralizada (eixo no centro do card); o card toma a altura da página aberta.
        float k = escalaElementos;
        Vector2 botao = tamanhoBotao * k, slider = tamanhoSlider * k;
        float espaco = espacoBotoes * k, alturaTitulo = tamanhoTitulo * k * 1.15f * 1.5f, rotulo = 42 * k;
        alturaTituloJogo = tamanhoTituloJogo * k * 1.3f;
        larguraPainel = Mathf.Max(tamanhoPainel.x * .85f, botao.x + 200, slider.x + 160);
        float blocoMenu = visiveis * botao.y + Mathf.Max(0, visiveis - 1) * espaco;
        float grupoSlider = rotulo + 10 * k + slider.y;
        float blocoConfig = alturaTitulo + espaco * 1.5f + grupoSlider * 2 + espaco * 2 + espaco * 2 + botao.y;
        alturaPaginaMenu = blocoMenu + margemCard * 2; alturaPaginaConfig = blocoConfig + margemCard * 2;
        float alturaConjunto = Mathf.Max(alturaPaginaMenu + espacoTituloCard + alturaTituloJogo, alturaPaginaConfig);
        float largura = Mathf.Max(larguraPainel, tamanhoTituloJogo * k * 6);
        float scale = escalaMenu;
        if (Application.isPlaying)
        {
            Vector2 disponivel = raiz.rect.size - new Vector2(Mathf.Abs(posicaoMenu.x), Mathf.Abs(posicaoMenu.y)) * 2 - Vector2.one * 40;
            if (disponivel.x > 1 && disponivel.y > 1) scale = Mathf.Max(.05f, Mathf.Min(scale, disponivel.x / largura, disponivel.y / alturaConjunto));
        }
        escalaAtual = scale;
        painel.localScale = Vector3.one * scale;
        tituloMenuUI.rectTransform.localScale = Vector3.one * scale;
        Centro(tituloMenuUI.rectTransform, Vector2.zero, new Vector2(largura, alturaTituloJogo));
        float y = blocoMenu * .5f;
        for (int i = 0; i < entradasMenu.Length; i++)
        {
            if (entradasMenu[i] == null || !entradasMenu[i].gameObject.activeSelf) continue;
            Centro(entradasMenu[i], new Vector2(0, y - botao.y * .5f), botao); y -= botao.y + espaco;
            if (entradasBase != null && i < entradasBase.Length) entradasBase[i] = entradasMenu[i].anchoredPosition;
        }
        y = blocoConfig * .5f;
        Centro(tituloConfigUI.rectTransform, new Vector2(0, y - alturaTitulo * .5f), new Vector2(larguraPainel - 50, alturaTitulo));
        y -= alturaTitulo + espaco * 1.5f;
        foreach (var par in new[] { (valorVolumeUI, volumeUI), (valorSensUI, sensUI) })
        {
            Centro(par.Item1.rectTransform, new Vector2(0, y - rotulo * .5f), new Vector2(slider.x, rotulo));
            Centro((RectTransform)par.Item2.transform, new Vector2(0, y - rotulo - 10 * k - slider.y * .5f), slider);
            y -= grupoSlider + espaco * 2;
        }
        Centro((RectTransform)voltarUI.transform.parent, new Vector2(0, y - botao.y * .5f), botao);
        var msg = mensagemUI.rectTransform; msg.anchorMin = msg.anchorMax = new Vector2(.5f, 0);
        msg.sizeDelta = new Vector2(larguraPainel - 50, 50); msg.anchoredPosition = new Vector2(0, margemCard * .5f);
        tituloMenuUI.text = tituloMenu; tituloConfigUI.text = tituloConfiguracoes;
        iniciarUI.GetComponentInChildren<TMP_Text>().text = textoIniciar; configuracoesUI.GetComponentInChildren<TMP_Text>().text = textoConfiguracoes;
        sairUI.GetComponentInChildren<TMP_Text>().text = textoSair; voltarUI.GetComponentInChildren<TMP_Text>().text = textoVoltar;
        sensUI.minValue = Mathf.Max(.0001f, sensibilidadeMinima); sensUI.maxValue = Mathf.Max(sensUI.minValue + .001f, sensibilidadeMaxima);
        escurecimento.color = corEscurecimento;
        if (fundoUI == null) fundoUI = raiz.Find("ArteFundo")?.GetComponent<Image>();
        if (logoUI == null) logoUI = paginaMenu.Find("Logo")?.GetComponent<Image>();
        if (fundoUI != null) { fundoUI.sprite = spriteFundo; fundoUI.color = corFundo; fundoUI.gameObject.SetActive(spriteFundo != null); }
        if (logoUI != null) { logoUI.sprite = logo; logoUI.gameObject.SetActive(logo != null); Posicao(logoUI.rectTransform, posicaoLogo, tamanhoLogo); }
        tituloMenuUI.gameObject.SetActive(logo == null);
        if (logoUI != null && logo != null) { logoUI.transform.SetParent(raiz, false); Centro(logoUI.rectTransform, Vector2.zero, tamanhoLogo); }
        tutorialUI.GetComponentInChildren<TMP_Text>().text = textoTutorial;
        if (controlesUI != null) controlesUI.GetComponentInChildren<TMP_Text>().text = textoControles;
        if (!Application.isPlaying)
        {
            raiz.gameObject.SetActive(previaNoEditor); grupo.alpha = 1;
            pagina = previaConfiguracoes ? 1 : 0;
            foreach (var entrada in entradasMenu) { entrada.localScale = Vector3.one; entrada.GetComponent<CanvasGroup>().alpha = 1; }

        }
        PosicionarPaginas();
    }
    private void PosicionarPaginas()
    {
        float distancia = larguraPainel + 60;
        painel.sizeDelta = new Vector2(larguraPainel, Mathf.Lerp(alturaPaginaMenu, alturaPaginaConfig, pagina));
        // Título + card centralizados juntos na tela; nas configurações (o card tem título próprio) o nome do jogo some.
        float deslocamento = (alturaTituloJogo + espacoTituloCard) * .5f * escalaAtual;
        painel.anchoredPosition = posicaoMenu + Vector2.down * deslocamento * (1 - pagina);
        var topo = posicaoMenu + Vector2.up * ((alturaPaginaMenu * .5f + espacoTituloCard + alturaTituloJogo * .5f) * escalaAtual - deslocamento);
        tituloMenuUI.rectTransform.anchoredPosition = topo;
        if (logoUI != null) logoUI.rectTransform.anchoredPosition = topo;
        tituloMenuUI.alpha = 1 - pagina; if (logoUI != null) { var c = logoUI.color; c.a = 1 - pagina; logoUI.color = c; }
        paginaMenu.anchoredPosition = new Vector2(-pagina * distancia, 0);
        paginaConfig.anchoredPosition = new Vector2((1 - pagina) * distancia, 0);
    }
    [ContextMenu("Menu principal/Copiar estilo do tablet")]
    public void CopiarEstiloDoTablet()
    {
        if (tablet == null) return;
        // Copia somente configuração visual. Não escreve nem invoca métodos do tablet.
        string[] nomes = { "fonte", "corTexto", "corDestaque", "raioBotoes", "gradienteTopo", "gradienteBase", "corInnerGlow", "larguraInnerGlow", "intensidadeInnerGlow", "corOuterGlow", "larguraOuterGlow", "intensidadeOuterGlow", "corOutlineTexto", "larguraOutlineTexto", "corSombraTexto", "deslocamentoSombraTexto", "suavidadeSombraTexto", "expandirSombraTexto", "escalaHover", "velocidadeHover" };
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        foreach (var nome in nomes)
        {
            var origem = tablet.GetType().GetField(nome, flags); var destino = GetType().GetField(nome, flags);
            if (origem != null && destino != null && origem.FieldType == destino.FieldType) destino.SetValue(this, origem.GetValue(tablet));
        }
        var efeitos = tablet.GetType().GetField("efeitosBotoes", flags);
        if (efeitos != null && efeitos.GetValue(tablet) != null) JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(efeitos.GetValue(tablet)), efeitosBotoes);
        if (raiz != null) AtualizarAparencia();
    }
    [ContextMenu("Menu principal/Copiar estilo do pause")]
    public void CopiarEstiloDoPause()
    {
        if (referenciaPause == null) return;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        string[] campos = { "fonte", "spritePainel", "corPainel", "corTexto", "corDestaque", "raioBotoes", "gradienteTopo", "gradienteBase", "corInnerGlow", "larguraInnerGlow", "intensidadeInnerGlow", "corOuterGlow", "larguraOuterGlow", "intensidadeOuterGlow", "corOutlineTexto", "larguraOutlineTexto", "corSombraTexto", "deslocamentoSombraTexto", "suavidadeSombraTexto", "expandirSombraTexto", "duracaoAbertura", "duracaoFechamento", "duracaoBounce", "intervaloBotoes", "intensidadeBounce", "deslocamentoEntrada", "duracaoPagina", "escalaHover", "velocidadeHover", "spriteCursor", "tamanhoCursor", "pontaCursor", "somAbrir", "somFechar", "somClique", "somHover", "mixer", "parametroVolume", "prefixoPreferencias", "salvarPreferencias", "sensibilidadeMinima", "sensibilidadeMaxima" };
        foreach (var nome in campos)
        {
            var origem = referenciaPause.GetType().GetField(nome, flags); var destino = GetType().GetField(nome, flags);
            if (origem != null && destino != null && origem.FieldType == destino.FieldType) destino.SetValue(this, origem.GetValue(referenciaPause));
        }
        var efeito = referenciaPause.GetType().GetField("efeitosBotoes", flags);
        if (efeito != null && efeito.GetValue(referenciaPause) != null) JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(efeito.GetValue(referenciaPause)), efeitosBotoes);
        AtualizarLayout(); AtualizarAparencia();
    }
    [ContextMenu("Menu principal/Atualizar aparência")]
    public void AtualizarAparencia()
    {
        if (raiz == null) return;
        LimparEstilos();
        Sprite redondo = CriarArredondado(18);
        painel.GetComponent<Image>().sprite = spritePainel != null ? spritePainel : redondo;
        painel.GetComponent<Image>().type = spritePainel != null ? Image.Type.Simple : Image.Type.Sliced;
        painel.GetComponent<Image>().color = corPainel;
        var cache = new Dictionary<Vector2Int, Sprite[]>();
        Canvas.ForceUpdateCanvases();
        foreach (var b in raiz.GetComponentsInChildren<Button>(true))
        {
            var size = ((RectTransform)b.transform).rect.size;
            var key = new Vector2Int(Mathf.Clamp(Mathf.RoundToInt(size.x), 2, 2048), Mathf.Clamp(Mathf.RoundToInt(size.y), 2, 1024));
            if (!cache.TryGetValue(key, out Sprite[] sprites))
            {
                sprites = CriarEstiloBotao(key.x, key.y); cache.Add(key, sprites);
                foreach (var sp in sprites) { recursosVisuais.Add(sp.texture); recursosVisuais.Add(sp); }
            }
            b.image.sprite = sprites[0]; b.image.type = Image.Type.Simple; b.image.color = Color.white;
            AplicarBordaESombra(b.image, efeitosBotoes);
            var filho = b.transform.Find("OuterGlow");
            Image glow = filho != null ? filho.GetComponent<Image>() : Imagem("OuterGlow", b.transform, Color.white);
            glow.transform.SetAsFirstSibling(); float margemGlow = Mathf.Ceil(Mathf.Max(0, larguraOuterGlow)) + 2;
            Stretch(glow.rectTransform, -Vector2.one * margemGlow, Vector2.one * margemGlow);
            glow.sprite = sprites[1]; glow.type = Image.Type.Simple; glow.raycastTarget = false;
        }
        foreach (var slider in new[] { volumeUI, sensUI })
        {
            foreach (var imagem in slider.GetComponentsInChildren<Image>(true)) { imagem.sprite = redondo; imagem.type = Image.Type.Sliced; }
            slider.fillRect.GetComponent<Image>().color = corDestaque;
            slider.transform.Find("Trilho").GetComponent<Image>().color = corTrilhoSlider;
            slider.handleRect.GetComponent<Image>().color = corAlcaSlider;
            slider.handleRect.sizeDelta = tamanhoAlcaSlider * escalaElementos;
        }
        foreach (var t in raiz.GetComponentsInChildren<TMP_Text>(true))
        {
            if (fonte != null) t.font = fonte;
            t.color = t == mensagemUI ? new Color(1, .65f, .4f) : corTexto;
            t.fontSize = (t == tituloMenuUI ? tamanhoTituloJogo : t == tituloConfigUI ? tamanhoTitulo * 1.15f : t == mensagemUI ? 18 : tamanhoTexto) * escalaElementos;
            Material origem = t.fontSharedMaterial;
            if (origem == null) continue;
            if (!materiais.TryGetValue(origem, out Material material))
            {
                material = new Material(origem) { name = "Pause_Texto", hideFlags = HideFlags.HideAndDontSave }; materiais.Add(origem, material);
                if (material.HasProperty("_OutlineWidth"))
                { material.SetFloat("_OutlineWidth", larguraOutlineTexto); material.SetColor("_OutlineColor", corOutlineTexto); material.EnableKeyword("OUTLINE_ON"); }
                if (material.HasProperty("_UnderlayColor"))
                {
                    material.DisableKeyword("UNDERLAY_INNER"); material.EnableKeyword("UNDERLAY_ON"); material.SetColor("_UnderlayColor", corSombraTexto);
                    material.SetFloat("_UnderlayOffsetX", deslocamentoSombraTexto.x); material.SetFloat("_UnderlayOffsetY", deslocamentoSombraTexto.y);
                    material.SetFloat("_UnderlaySoftness", suavidadeSombraTexto); material.SetFloat("_UnderlayDilate", expandirSombraTexto);
                }
            }
            t.fontSharedMaterial = material; t.extraPadding = true; t.UpdateMeshPadding();
        }
        EstilizarTitulo(tituloMenuUI, .55f); EstilizarTitulo(tituloConfigUI, .35f);
    }
    // Título em destaque com o visual dos botões: gradiente verde (gradienteTopo → gradienteBase), contorno escuro
    // (o mesmo dos botões) e brilho externo na cor do glow dos botões (underlay sem deslocamento).
    private void EstilizarTitulo(TMP_Text t, float brilho)
    {
        if (t == null) return;
        t.color = Color.white; t.fontStyle = FontStyles.Bold;
        t.enableVertexGradient = true; t.colorGradient = new VertexGradient(gradienteTopo, gradienteTopo, gradienteBase, gradienteBase);
        Material origem = t.font != null ? t.font.material : t.fontSharedMaterial;
        if (origem == null) return;
        var material = new Material(origem) { name = "Menu_Titulo", hideFlags = HideFlags.HideAndDontSave };
        recursosVisuais.Add(material);
        if (material.HasProperty("_OutlineWidth"))
        {
            Color contorno = efeitosBotoes != null && efeitosBotoes.outlineAtivo ? efeitosBotoes.corOutline : corOutlineTexto;
            material.SetFloat("_OutlineWidth", Mathf.Max(.2f, larguraOutlineTexto)); material.SetColor("_OutlineColor", contorno); material.EnableKeyword("OUTLINE_ON");
        }
        if (material.HasProperty("_UnderlayColor"))
        {
            Color glow = corOuterGlow; glow.a = Mathf.Clamp01(glow.a * 1.3f);
            material.DisableKeyword("UNDERLAY_INNER"); material.EnableKeyword("UNDERLAY_ON"); material.SetColor("_UnderlayColor", glow);
            material.SetFloat("_UnderlayOffsetX", 0); material.SetFloat("_UnderlayOffsetY", 0);
            material.SetFloat("_UnderlaySoftness", brilho); material.SetFloat("_UnderlayDilate", brilho * .5f);
        }
        t.fontSharedMaterial = material; t.extraPadding = true; t.UpdateMeshPadding();
    }
    private Sprite CriarArredondado(int raio)
    {
        int n = raio * 2 + 6; var pixels = new Color[n * n];
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            pixels[y * n + x] = new Color(1, 1, 1, Mathf.Clamp01(.5f - DistanciaBorda(x + .5f - n * .5f, y + .5f - n * .5f, n, n, raio)));
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
        tex.SetPixels(pixels); tex.Apply(false, true);
        var sp = Sprite.Create(tex, new Rect(0, 0, n, n), Vector2.one * .5f, 100, 0, SpriteMeshType.FullRect, Vector4.one * (raio + 2));
        sp.hideFlags = HideFlags.HideAndDontSave; recursosVisuais.Add(tex); recursosVisuais.Add(sp); return sp;
    }
    private void LimparEstilos()
    {
        if (raiz != null) foreach (var t in raiz.GetComponentsInChildren<TMP_Text>(true))
        {
            foreach (var par in materiais) if (t.fontSharedMaterial == par.Value) { t.fontSharedMaterial = par.Key; break; }
            if (t.fontSharedMaterial != null && recursosVisuais.Contains(t.fontSharedMaterial) && t.font != null) t.fontSharedMaterial = t.font.material;
        }
        foreach (var par in materiais) Destruir(par.Value); materiais.Clear();
        foreach (var recurso in recursosVisuais) Destruir(recurso); recursosVisuais.Clear();
    }
    private static void Destruir(UnityEngine.Object objeto)
    { if (objeto == null) return; if (Application.isPlaying) Destroy(objeto); else DestroyImmediate(objeto); }
    private RectTransform Rect(string nome, Transform pai)
    {
        var r = new GameObject(nome, typeof(RectTransform)).GetComponent<RectTransform>(); r.SetParent(pai, false);
        r.anchorMin = r.anchorMax = Vector2.one * .5f; return r;
    }
    private Image Imagem(string nome, Transform pai, Color cor, bool raycast = false)
    { var r = Rect(nome, pai); var img = r.gameObject.AddComponent<Image>(); img.color = cor; img.raycastTarget = raycast; return img; }
    private TMP_Text Texto(Transform pai, string nome, string texto, float tamanho)
    {
        var r = Rect(nome, pai); var t = r.gameObject.AddComponent<TextMeshProUGUI>(); if (fonte != null) t.font = fonte;
        t.text = texto; t.fontSize = tamanho; t.color = corTexto; t.alignment = TextAlignmentOptions.Center; t.richText = false; t.raycastTarget = false; return t;
    }
    private static void Stretch(RectTransform r) { Stretch(r, Vector2.zero, Vector2.zero); }
    private static void Stretch(RectTransform r, Vector2 min, Vector2 max) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = min; r.offsetMax = max; }
    private static void Centro(RectTransform r, Vector2 pos, Vector2 size) { r.anchorMin = r.anchorMax = r.pivot = Vector2.one * .5f; r.sizeDelta = size; r.anchoredPosition = pos; }
    private static void Posicao(RectTransform r, Vector2 pos, Vector2 size) { r.anchorMin = r.anchorMax = new Vector2(.5f, 1); r.sizeDelta = size; r.anchoredPosition = pos; }

    private Sprite[] CriarEstiloBotao(int largura, int altura)
    {
        float raio = Mathf.Clamp(raioBotoes, 0, Mathf.Min(largura, altura) * .5f);
        var pixels = new Color[largura * altura];
        for (int y = 0; y < altura; y++) for (int x = 0; x < largura; x++)
        {
            float d = DistanciaBorda(x + .5f - largura * .5f, y + .5f - altura * .5f, largura, altura, raio);
            Color cor = Color.Lerp(gradienteBase, gradienteTopo, (y + .5f) / altura);
            float inner = larguraInnerGlow <= 0 ? 0 : Mathf.Pow(Mathf.Clamp01(1 + d / larguraInnerGlow), 2);
            float mistura = Mathf.Clamp01(inner * intensidadeInnerGlow * corInnerGlow.a);
            float alfa = cor.a;
            cor = Color.Lerp(cor, corInnerGlow, mistura);
            cor.a = alfa * Mathf.Clamp01(.5f - d);
            pixels[y * largura + x] = cor;
        }
        Sprite fundo = SpriteDosPixels(pixels, largura, altura, "Tablet_Botao_Gradiente_InnerGlow");
        int margemGlow = Mathf.CeilToInt(Mathf.Max(0, larguraOuterGlow)) + 2;
        int w = largura + margemGlow * 2, h = altura + margemGlow * 2;
        pixels = new Color[w * h];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
        {
            float d = DistanciaBorda(x + .5f - w * .5f, y + .5f - h * .5f, largura, altura, raio);
            float fade = larguraOuterGlow <= 0 ? 0 : Mathf.Pow(Mathf.Clamp01(1 - Mathf.Max(0, d) / larguraOuterGlow), 2);
            Color cor = corOuterGlow;
            cor.a *= Mathf.Clamp01(intensidadeOuterGlow) * fade * Mathf.Clamp01(d + .5f);
            pixels[y * w + x] = cor;
        }
        return new[] { fundo, SpriteDosPixels(pixels, w, h, "Tablet_Botao_OuterGlow") };
    }
    private static float DistanciaBorda(float x, float y, float w, float h, float r)
    {
        float qx = Mathf.Abs(x) - (w * .5f - r), qy = Mathf.Abs(y) - (h * .5f - r);
        return new Vector2(Mathf.Max(qx, 0), Mathf.Max(qy, 0)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0) - r;
    }
    private static Sprite SpriteDosPixels(Color[] pixels, int w, int h, string nome)
    {
        var textura = new Texture2D(w, h, TextureFormat.RGBA32, false)
        { name = nome, hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        textura.SetPixels(pixels); textura.Apply(false, true);
        var sprite = Sprite.Create(textura, new Rect(0, 0, w, h), Vector2.one * .5f, 100, 0, SpriteMeshType.FullRect);
        sprite.name = nome; sprite.hideFlags = HideFlags.HideAndDontSave; return sprite;
    }

    private static void AplicarBordaESombra(Image imagem, BordaESombra estilo)
    {
        if (imagem == null) return;
        bool usarOutline = estilo != null && estilo.outlineAtivo && estilo.espessuraOutline > 0;
        bool usarSombra = estilo != null && estilo.sombraAtiva;
        // Outline herda de Shadow: busque a sombra pelo tipo exato para não confundir os dois.
        Shadow sombra = null;
        foreach (var componente in imagem.GetComponents<Shadow>())
            if (componente.GetType() == typeof(Shadow)) { sombra = componente; break; }
        if (sombra == null && usarSombra) sombra = imagem.gameObject.AddComponent<Shadow>();
        if (sombra != null)
        {
            sombra.enabled = usarSombra;
            if (estilo != null)
            {
                sombra.effectColor = estilo.corSombra;
                sombra.effectDistance = estilo.deslocamentoSombra;
                sombra.useGraphicAlpha = true;
            }
        }
        var outline = imagem.GetComponent<Outline>();
        if (outline == null && usarOutline) outline = imagem.gameObject.AddComponent<Outline>();
        if (outline != null)
        {
            outline.enabled = usarOutline;
            if (estilo != null)
            {
                outline.effectColor = estilo.corOutline;
                outline.effectDistance = Vector2.one * Mathf.Max(0, estilo.espessuraOutline);
                outline.useGraphicAlpha = true;
            }
        }
    }


}

#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(MainMenuScreen))]
public class MainMenuScreenEditor : UnityEditor.Editor
{
    private void OnEnable() { UnityEditor.EditorApplication.delayCall += Atualizar; }
    private void Atualizar()
    {
        if (this == null || target == null || Application.isPlaying) return;
        var menu = (MainMenuScreen)target; menu.AtualizarLayout(); menu.AtualizarAparencia(); UnityEditor.SceneView.RepaintAll();
    }
    public override void OnInspectorGUI()
    {
        UnityEditor.EditorGUI.BeginChangeCheck(); DrawDefaultInspector(); bool mudou = UnityEditor.EditorGUI.EndChangeCheck();
        var menu = (MainMenuScreen)target;
        UnityEditor.EditorGUILayout.HelpBox("Adicione este componente ao mesmo objeto do StageController e marque Show Start Menu nele. Use Prévia No Editor para visualizar. Requer TMP e EventSystem com InputSystemUIInputModule.", UnityEditor.MessageType.Info);
        if (GUILayout.Button("Gerar interface editável")) { UnityEditor.Undo.RegisterFullObjectHierarchyUndo(menu.gameObject, "Gerar menu"); menu.GerarInterface(); mudou = true; }
        if (GUILayout.Button("Copiar estilo do pause")) { UnityEditor.Undo.RecordObject(menu, "Copiar estilo"); menu.CopiarEstiloDoPause(); mudou = true; }
        if (GUILayout.Button("Copiar estilo do tablet")) { UnityEditor.Undo.RecordObject(menu, "Copiar estilo"); menu.CopiarEstiloDoTablet(); mudou = true; }
        if (GUILayout.Button("Atualizar aparência")) mudou = true;
        if (mudou && !Application.isPlaying)
        {
            menu.AtualizarLayout(); menu.AtualizarAparencia(); UnityEditor.EditorUtility.SetDirty(menu);
            UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(menu);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(menu.gameObject.scene); UnityEditor.SceneView.RepaintAll();
        }
    }
}
#endif
