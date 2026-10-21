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
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Pause independente. Input System + TMP. Blur por captura congelada,
/// sem Renderer Feature/shader externo. Não altera o código do player ou do tablet.
/// Pausa OFICIAL do jogo (um por cena). <b>Esc</b> no 3D com a fase rodando abre/fecha; com uma tela 2D aberta o Esc só
/// fecha a tela (<see cref="UIScreenManager"/>), com o celular aberto só fecha o celular. Na pausa: <c>timeScale 0</c>,
/// áudio do jogo pausado, jogador, mira, supervisor, NPCs e monitor de conduta desligados (achados na cena ao pausar,
/// além da lista do Inspector). Configurações (volume geral e sensibilidade do mouse, salvas em PlayerPrefs) também
/// abrem do menu inicial (<see cref="AbrirConfiguracoesAvulsas"/>). Sair = volta ao menu inicial da própria cena.</summary>
[DefaultExecutionOrder(-10000)]
[DisallowMultipleComponent]
public class PauseMenu : MonoBehaviour
{
    public static bool IsPaused { get; private set; }
    private static PauseMenu dono;
    [Header("Integração")]
    [SerializeField] private Key teclaPause = Key.Escape;
    [SerializeField] private TicketTabletPrototype tablet;
    [Tooltip("PlayerLook atual. O campo float sensitivity é conectado sem alterar seu script.")]
    [SerializeField] private MonoBehaviour controleOlhar;
    [SerializeField] private string campoSensibilidade = "sensitivity";
    [Tooltip("Arraste PlayerMovement, InteractionDetector e minigames que leem input diretamente. O controleOlhar e o tablet já são tratados automaticamente.")]
    [SerializeField] private Behaviour[] desativarDurantePause;
    [Tooltip("Ex.: o painel do menu inicial ou do relatório. Enquanto algum deles estiver ativo, ESC não abre o pause.")]
    [SerializeField] private GameObject[] bloquearSeAtivos;
    [Tooltip("Objetos puramente visuais a esconder durante o pause, como mira ou cursor de ferramenta.")]
    [SerializeField] private GameObject[] ocultarDurantePause;
    [SerializeField] private bool reabrirTabletAoContinuar = true;
    [SerializeField] private bool pausarAudioDoJogo = true;

    public enum DestinoSaida { Cena, Evento, MenuInicialDaCena }
    [Header("SAIR: voltar ao menu inicial")]
    [SerializeField] private DestinoSaida destinoSaida = DestinoSaida.MenuInicialDaCena;
    [Tooltip("Nome/caminho exato da cena do menu, incluída na lista de cenas do Build Profile.")]
    [SerializeField] private string cenaMenu = "";
    [Tooltip("Alternativa para um menu na mesma cena: conecte o método público que abre seu menu inicial.")]
    [SerializeField] private UnityEvent aoSairParaMenu = new UnityEvent();

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
    [SerializeField] private bool previaControles;

    [Header("Página de controles")]
    [SerializeField] private string textoControles = "CONTROLES";
    [SerializeField] private string tituloControles = "CONTROLES";
    [SerializeField] private string subtituloControles = "Aprenda a se movimentar e interagir";
    [Tooltip("Tamanho da página de controles (canvas 1920x1080). É mais larga que as outras: o card cresce ao abri-la. " +
             "Os textos são um guia visual; editar aqui não altera as teclas da gameplay.")]
    [SerializeField] private Vector2 tamanhoPaginaControles = new Vector2(1480, 940);
    [SerializeField, HideInInspector] private RectTransform paginaControles;
    [SerializeField, HideInInspector] private TMP_Text tituloControlesUI;
    [SerializeField, HideInInspector] private ScrollRect rolagemControles;
    [SerializeField, HideInInspector] private Button controlesUI, voltarControlesUI;
    // peças animadas da página (W, A, S, D: face da tecla e seta; mouse: botão e título do texto de cada lado)
    [SerializeField, HideInInspector] private Image[] teclasControles, setasControles, botoesMouse;
    [SerializeField, HideInInspector] private TMP_Text[] titulosMouse;
    [SerializeField, HideInInspector] private Vector2[] baseTeclas, baseSetas;
    private float alturaPaginaControles = 940, larguraPaginaControles = 1480;
    private readonly float[] brilhoTeclas = new float[4], brilhoMouse = new float[2];
    [SerializeField] private Vector2 tamanhoPainel = new Vector2(780, 690);
    [SerializeField, Range(.4f, 1.6f)] private float escalaMenu = 1;
    [SerializeField] private Vector2 posicaoMenu;
    [SerializeField] private Vector2 tamanhoBotao = new Vector2(440, 72);
    [SerializeField, Min(10)] private float espacoBotoes = 24;
    [SerializeField] private float inicioBotoes = 230;
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
    [SerializeField] private string tituloPause = "PAUSADO";
    [SerializeField] private string tituloConfiguracoes = "CONFIGURAÇÕES";
    [SerializeField] private string textoContinuar = "CONTINUAR";
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

    [Header("Blur do fundo")]
    [SerializeField] private bool desfocarFundo = true;
    [Tooltip("Largura máxima da captura reduzida. Menor = mais leve e mais desfocado.")]
    [SerializeField, Range(128, 768)] private int resolucaoBlur = 384;
    [SerializeField, Range(1, 20)] private int raioBlur = 5;
    [SerializeField, Range(1, 5)] private int passesBlur = 3;
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
    [SerializeField] private RawImage imagemBlur;
    [SerializeField] private Image escurecimento;
    [SerializeField] private TMP_Text tituloMenuUI, tituloConfigUI, valorVolumeUI, valorSensUI, mensagemUI;
    [SerializeField] private Button continuarUI, configuracoesUI, sairUI, voltarUI;
    [SerializeField] private Slider volumeUI, sensUI;
    [SerializeField] private RectTransform[] entradasMenu;

    private enum Estado { Fechado, Capturando, Aberto, Fechando }
    private Estado estado;
    private int larguraTela, alturaTela;
    private bool iniciou, tabletHabilitado, tabletEstavaAberto, carregandoCena, preferenciasSujas;
    private bool audioPausadoAntes, audioUIIgnoravaPause, configuracoesProntas, avisouMixer, soConfiguracoes;
    private StageController fase;
    private float escalaTempoAnterior, aberturaEm, volumeAtual, sensAtual, pagina, paginaOrigem, paginaAlvo, paginaInicio;
    private float alturaPaginaMenu = 690, alturaPaginaConfig = 690, larguraPainel = 780;
    private CursorLockMode cursorLockAnterior;
    private bool cursorVisivelAnterior;
    private FieldInfo campoMouse;
    private readonly Dictionary<Behaviour, bool> controles = new Dictionary<Behaviour, bool>();
    private readonly Dictionary<GameObject, bool> visibilidade = new Dictionary<GameObject, bool>();
    private readonly List<Hover> hovers = new List<Hover>();
    private readonly List<UnityEngine.Object> recursosVisuais = new List<UnityEngine.Object>();
    private readonly Dictionary<Material, Material> materiais = new Dictionary<Material, Material>();
    private Texture2D captura;
    private Vector2[] entradasBase;
    private CanvasGroup[] entradasAlpha;
    private class Hover { public Button botao; public float escala = 1; public bool sobre; }

    private void Awake()
    {
        if (dono != null && dono != this) { Debug.LogWarning("Já existe um PauseMenu ativo nesta cena.", this); enabled = false; return; }
        dono = this;
        if (tablet == null) tablet = FindAnyObjectByType<TicketTabletPrototype>();
        if (controleOlhar == null) controleOlhar = FindAnyObjectByType<PlayerLook>();
        fase = GetComponent<StageController>();
        if (fase == null) fase = FindAnyObjectByType<StageController>();
        if (audioUI == null)
        {
            audioUI = gameObject.AddComponent<AudioSource>();
            audioUI.playOnAwake = false; audioUI.spatialBlend = 0f;
        }
        if (raiz == null) GerarInterface();
        AtualizarLayout(); AtualizarAparencia();
        VincularBotoes(); ConfigurarSensibilidade();
        estado = Estado.Fechado; raiz.gameObject.SetActive(false); iniciou = true;
    }
    private void OnValidate()
    {
        sensibilidadeMinima = Mathf.Max(.0001f, sensibilidadeMinima);
        sensibilidadeMaxima = Mathf.Max(sensibilidadeMinima + .001f, sensibilidadeMaxima);
        tamanhoPainel.x = Mathf.Max(300, tamanhoPainel.x); tamanhoPainel.y = Mathf.Max(300, tamanhoPainel.y);
        tamanhoBotao.x = Mathf.Max(80, tamanhoBotao.x); tamanhoBotao.y = Mathf.Max(24, tamanhoBotao.y);
        duracaoBounce = Mathf.Max(.05f, duracaoBounce); duracaoPagina = Mathf.Max(.05f, duracaoPagina);
    }
    private void Start()
    {
        volumeAtual = salvarPreferencias ? PlayerPrefs.GetFloat(prefixoPreferencias + ".Volume", volumeInicial) : volumeInicial;
        float inicial = campoMouse != null && controleOlhar != null ? (float)campoMouse.GetValue(controleOlhar) : sensibilidadeInicial;
        sensAtual = salvarPreferencias ? PlayerPrefs.GetFloat(prefixoPreferencias + ".Sensitivity", inicial) : inicial;
        volumeAtual = Mathf.Clamp01(volumeAtual); sensAtual = Mathf.Clamp(sensAtual, sensibilidadeMinima, sensibilidadeMaxima);
        volumeUI.SetValueWithoutNotify(volumeAtual); sensUI.SetValueWithoutNotify(sensAtual);
        configuracoesProntas = true; AplicarVolume(volumeAtual); AplicarSensibilidade(sensAtual); preferenciasSujas = false;
        if (EventSystem.current == null) Debug.LogWarning("PauseMenu: configure um EventSystem com InputSystemUIInputModule na cena.", this);
    }
    private void OnEnable() { if (Application.isPlaying) Canvas.willRenderCanvases += GarantirCursor; }
    private void OnDisable()
    {
        Canvas.willRenderCanvases -= GarantirCursor;
        if (!Application.isPlaying) { LimparEstilos(); return; }
        if (!iniciou) return;
        StopAllCoroutines();
        if (soConfiguracoes) FecharConfiguracoesAvulsas();
        else if (estado != Estado.Fechado) RestaurarJogo(false);
        Salvar(); if (raiz != null) raiz.gameObject.SetActive(false);
        if (dono == this) { dono = null; IsPaused = false; }
    }
    private void OnDestroy() { LiberarCaptura(); LimparEstilos(); }
    private void OnApplicationQuit() { Salvar(); }
    private bool PodeAbrir()
    {
        if (carregandoCena || !configuracoesProntas) return false;
        if (MainMenuScreen.Showing || StageReportScreen.Showing) return false;
        if (fase != null && !fase.IsRunning) return false;
        // tela 2D aberta (ou fechada neste quadro pelo mesmo Esc): o Esc é dela
        if (UIScreenManager.Active != null && UIScreenManager.Active.BlocksGameplayInput) return false;
        if (bloquearSeAtivos != null) foreach (var go in bloquearSeAtivos) if (go != null && go.activeInHierarchy) return false;
        return true;
    }
    private void Update()
    {
        if (!iniciou) return;
        if (Keyboard.current != null && teclaPause != Key.None && Keyboard.current[teclaPause].wasPressedThisFrame)
        {
            if (estado == Estado.Fechado)
            {
                // celular aberto: o Esc fecha o celular, não pausa
                if (TicketTabletPrototype.CelularAberto && tablet != null && tablet.isActiveAndEnabled) tablet.Fechar();
                else AbrirPause();
            }
            else if (estado == Estado.Aberto)
            { if (soConfiguracoes || Mathf.Abs(paginaAlvo) < .5f) Continuar(); else MostrarPagina(false); }
        }
        if (estado != Estado.Aberto) return;
        if (Screen.width != larguraTela || Screen.height != alturaTela) { Canvas.ForceUpdateCanvases(); AtualizarLayout(); }
        float dt = Time.unscaledDeltaTime;
        grupo.alpha = Mathf.Clamp01((Time.unscaledTime - aberturaEm) / Mathf.Max(.05f, duracaoAbertura));
        float u = Mathf.Clamp01((Time.unscaledTime - paginaInicio) / Mathf.Max(.05f, duracaoPagina));
        pagina = Mathf.Lerp(paginaOrigem, paginaAlvo, u * u * (3 - 2 * u)); PosicionarPaginas();
        bool transitando = u < 1;
        paginaMenu.GetComponent<CanvasGroup>().interactable = !transitando && Mathf.Abs(paginaAlvo) < .5f;
        paginaMenu.GetComponent<CanvasGroup>().blocksRaycasts = !transitando && Mathf.Abs(paginaAlvo) < .5f;
        paginaConfig.GetComponent<CanvasGroup>().interactable = !transitando && paginaAlvo > .5f;
        paginaConfig.GetComponent<CanvasGroup>().blocksRaycasts = !transitando && paginaAlvo > .5f;
        var grupoControles = paginaControles.GetComponent<CanvasGroup>();
        grupoControles.interactable = !transitando && paginaAlvo < -.5f;
        grupoControles.blocksRaycasts = grupoControles.interactable;
        if (pagina < -.01f) AnimarControles(dt);
        for (int i = 0; i < entradasMenu.Length; i++)
        {
            float t = Mathf.Clamp01((Time.unscaledTime - aberturaEm - i * intervaloBotoes) / Mathf.Max(.05f, duracaoBounce));
            float b = EaseOutBack(t, intensidadeBounce);
            entradasMenu[i].localScale = Vector3.one * Mathf.LerpUnclamped(.65f, 1, b);
            entradasMenu[i].anchoredPosition = entradasBase[i] + Vector2.down * deslocamentoEntrada * (1 - b);
            entradasAlpha[i].alpha = Mathf.Clamp01(t * 3);
            entradasAlpha[i].interactable = t >= .95f; entradasAlpha[i].blocksRaycasts = t >= .95f;
        }
        Vector2 mouse = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.one * -10000;
        foreach (var h in hovers)
        {
            bool sobre = h.botao.IsActive() && h.botao.IsInteractable() && RectTransformUtility.RectangleContainsScreenPoint((RectTransform)h.botao.transform, mouse, null);
            if (sobre && !h.sobre) Tocar(somHover);
            h.sobre = sobre; h.escala = Mathf.Lerp(h.escala, sobre ? escalaHover : 1, 1 - Mathf.Exp(-velocidadeHover * dt));
            h.botao.transform.localScale = Vector3.one * h.escala;
        }
    }
    public void AbrirPause()
    {
        if (estado != Estado.Fechado || !PodeAbrir()) return;
        if (dono != null && dono != this) return; dono = this;
        RecarregarPreferenciasCompartilhadas();
        // O tablet fecha ANTES de a pausa valer e de os controles serem guardados: fechando já em pausa ele não devolvia
        // os controles que tinha desligado (a pausa conta como "tela por cima"), a pausa guardava esse "desligado" e ao
        // continuar o jogador ficava sem andar/interagir até sair do Play Mode.
        tabletHabilitado = tablet != null && tablet.enabled; tabletEstavaAberto = tabletHabilitado && tablet.IsOpen;
        if (tabletHabilitado) { tablet.Fechar(); tablet.enabled = false; }
        estado = Estado.Capturando; IsPaused = true;
        escalaTempoAnterior = Time.timeScale; audioPausadoAntes = AudioListener.pause;
        cursorLockAnterior = Cursor.lockState; cursorVisivelAnterior = Cursor.visible;
        controles.Clear(); GuardarControle(controleOlhar);
        if (desativarDurantePause != null) foreach (var c in desativarDurantePause) GuardarControle(c);
        // o que lê input ou anda sozinho: achado na hora (só ao pausar, não por quadro)
        foreach (var c in FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None)) GuardarControle(c);
        foreach (var c in FindObjectsByType<PlayerLook>(FindObjectsSortMode.None)) GuardarControle(c);
        foreach (var c in FindObjectsByType<InteractionDetector>(FindObjectsSortMode.None)) GuardarControle(c);
        foreach (var c in FindObjectsByType<SupervisorPatrol>(FindObjectsSortMode.None)) GuardarControle(c);
        foreach (var c in FindObjectsByType<SupervisorPerception>(FindObjectsSortMode.None)) GuardarControle(c);
        foreach (var c in FindObjectsByType<SupervisorFeedback>(FindObjectsSortMode.None)) GuardarControle(c);
        foreach (var c in FindObjectsByType<NpcCaminhada>(FindObjectsSortMode.None)) GuardarControle(c);
        foreach (var c in FindObjectsByType<ConductMonitor>(FindObjectsSortMode.None)) GuardarControle(c);
        foreach (var c in controles) if (c.Key != null) c.Key.enabled = false;
        visibilidade.Clear();
        if (ocultarDurantePause != null) foreach (var go in ocultarDurantePause)
            if (go != null && go != gameObject && !transform.IsChildOf(go.transform) && !go.transform.IsChildOf(raiz) && go.transform != raiz && !visibilidade.ContainsKey(go))
            { visibilidade.Add(go, go.activeSelf); go.SetActive(false); }
        Time.timeScale = 0;
        if (pausarAudioDoJogo) AudioListener.pause = true;
        if (audioUI != null) { audioUIIgnoravaPause = audioUI.ignoreListenerPause; audioUI.ignoreListenerPause = true; }
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        UICursor.LimparSobreposicao();
        raiz.gameObject.SetActive(false); LiberarCaptura();
        StartCoroutine(AbrirComCaptura());
    }
    private void RecarregarPreferenciasCompartilhadas()
    {
        if (!salvarPreferencias || !configuracoesProntas) return;
        float volume = Mathf.Clamp01(PlayerPrefs.GetFloat(prefixoPreferencias + ".Volume", volumeAtual));
        float sens = Mathf.Clamp(PlayerPrefs.GetFloat(prefixoPreferencias + ".Sensitivity", sensAtual), sensibilidadeMinima, sensibilidadeMaxima);
        volumeUI.SetValueWithoutNotify(volume); sensUI.SetValueWithoutNotify(sens);
        AplicarVolume(volume); AplicarSensibilidade(sens); preferenciasSujas = false;
    }
    private void GuardarControle(Behaviour c)
    {
        if (c != null && c != this && c != tablet && c != audioUI && !controles.ContainsKey(c)) controles.Add(c, c.enabled);
    }
    private IEnumerator AbrirComCaptura()
    {
        // Espera o quadro sem o menu; captura tudo que já foi renderizado, inclusive HUDs.
        if (desfocarFundo)
        {
            yield return new WaitForEndOfFrame();
            CapturarBlur();
        }
        if (estado != Estado.Capturando) yield break;
        grupo.alpha = 0; raiz.gameObject.SetActive(true); Canvas.ForceUpdateCanvases(); AtualizarLayout();
        imagemBlur.texture = captura; imagemBlur.gameObject.SetActive(captura != null);
        pagina = paginaAlvo = paginaOrigem = 0; paginaInicio = Time.unscaledTime - duracaoPagina;
        aberturaEm = Time.unscaledTime; mensagemUI.text = "";
        for (int i = 0; i < entradasMenu.Length; i++) { entradasAlpha[i].alpha = 0; entradasMenu[i].localScale = Vector3.one * .65f; }
        PosicionarPaginas(); estado = Estado.Aberto; Tocar(somAbrir);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }
    public void Continuar()
    {
        if (estado != Estado.Aberto || carregandoCena) return;
        if (soConfiguracoes) { FecharConfiguracoesAvulsas(); return; }
        StartCoroutine(FecharAnimado(false));
    }
    public void MostrarControles()
    {
        if (estado != Estado.Aberto || soConfiguracoes) return;
        paginaOrigem = pagina; paginaAlvo = -1; paginaInicio = Time.unscaledTime;
        rolagemControles.StopMovement(); rolagemControles.verticalNormalizedPosition = 1;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }
    public void MostrarConfiguracoes() { if (estado == Estado.Aberto) MostrarPagina(true); }
    public void Voltar()
    {
        if (estado != Estado.Aberto) return;
        if (soConfiguracoes) FecharConfiguracoesAvulsas(); else MostrarPagina(false);
    }

    /// <summary>Configurações sem pausar (menu inicial): mesma tela e mesmas preferências do pause.</summary>
    public void AbrirConfiguracoesAvulsas() => AbrirPaginaAvulsa(1);
    /// <summary>Uma página avulsa (configurações ou controles, sem pausar) está aberta.</summary>
    public bool PaginaAvulsaAberta => soConfiguracoes && estado != Estado.Fechado;
    /// <summary>Controles sem pausar (menu inicial): a mesma página de controles do pause; Voltar/Esc fecham.</summary>
    public void AbrirControlesAvulsos()
    {
        AbrirPaginaAvulsa(-1);
        if (estado == Estado.Aberto && rolagemControles != null) { rolagemControles.StopMovement(); rolagemControles.verticalNormalizedPosition = 1; }
    }
    // página do pause aberta sozinha, sem pausar o jogo (1 = configurações, -1 = controles); por cima do menu inicial
    private void AbrirPaginaAvulsa(float paginaAvulsa)
    {
        if (!iniciou || estado != Estado.Fechado || carregandoCena) return;
        if (canvas != null) canvas.sortingOrder = 32767;
        soConfiguracoes = true;
        if (audioUI != null) { audioUIIgnoravaPause = audioUI.ignoreListenerPause; audioUI.ignoreListenerPause = true; }
        grupo.alpha = 0; grupo.interactable = true; grupo.blocksRaycasts = true;
        raiz.gameObject.SetActive(true); Canvas.ForceUpdateCanvases(); AtualizarLayout();
        imagemBlur.gameObject.SetActive(false); mensagemUI.text = "";
        pagina = paginaAlvo = paginaOrigem = paginaAvulsa; paginaInicio = Time.unscaledTime - duracaoPagina;
        aberturaEm = Time.unscaledTime;
        for (int i = 0; i < entradasMenu.Length; i++) { entradasAlpha[i].alpha = 0; entradasMenu[i].localScale = Vector3.one * .65f; }
        PosicionarPaginas(); estado = Estado.Aberto; Tocar(somAbrir);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    private void FecharConfiguracoesAvulsas()
    {
        Salvar(); Tocar(somFechar);
        raiz.gameObject.SetActive(false);
        if (audioUI != null) audioUI.ignoreListenerPause = audioUIIgnoravaPause;
        soConfiguracoes = false; estado = Estado.Fechado;
        pagina = paginaAlvo = paginaOrigem = 0;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }
    private void MostrarPagina(bool config)
    {
        paginaOrigem = pagina; paginaAlvo = config ? 1 : 0; paginaInicio = Time.unscaledTime;
        if (!config) Salvar();
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }
    public void SairParaMenu()
    {
        if (estado != Estado.Aberto || carregandoCena) return;
        if (destinoSaida == DestinoSaida.Cena && (string.IsNullOrWhiteSpace(cenaMenu) || !Application.CanStreamedLevelBeLoaded(cenaMenu)))
        { mensagemUI.text = "Configure a cena do menu no Inspector e na lista de cenas do Build Profile."; return; }
        if (destinoSaida == DestinoSaida.Evento && aoSairParaMenu.GetPersistentEventCount() == 0)
        { mensagemUI.text = "Conecte o evento Ao Sair Para Menu no Inspector."; return; }
        if (soConfiguracoes) return;
        carregandoCena = true; StartCoroutine(FecharAnimado(true));
    }
    private IEnumerator FecharAnimado(bool sair)
    {
        estado = Estado.Fechando; grupo.interactable = false; Tocar(somFechar);
        float alpha = grupo.alpha;
        float inicio = Time.unscaledTime;
        while (Time.unscaledTime - inicio < Mathf.Max(.05f, duracaoFechamento))
        { grupo.alpha = alpha * (1 - Mathf.Clamp01((Time.unscaledTime - inicio) / Mathf.Max(.05f, duracaoFechamento))); yield return null; }
        // Sempre em outro quadro: PlayerLook não recebe de novo o ESC que fechou o pause.
        Salvar(); RestaurarJogo(!sair); raiz.gameObject.SetActive(false); LiberarCaptura();
        if (sair)
        {
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            if (destinoSaida == DestinoSaida.MenuInicialDaCena)
            {
                carregandoCena = false;
                GameSession.GoToMainMenu();   // recarrega a cena do zero e abre o menu inicial
                yield break;
            }
            if (destinoSaida == DestinoSaida.Cena)
            {
                var operacao = SceneManager.LoadSceneAsync(cenaMenu);
                if (operacao != null) yield return operacao;
            }
            else aoSairParaMenu.Invoke();
            carregandoCena = false;
        }
    }
    private void RestaurarJogo(bool reabrir)
    {
        Time.timeScale = escalaTempoAnterior; AudioListener.pause = audioPausadoAntes;
        foreach (var c in controles) if (c.Key != null) c.Key.enabled = c.Value; controles.Clear();
        foreach (var go in visibilidade) if (go.Key != null) go.Key.SetActive(go.Value); visibilidade.Clear();
        if (tablet != null && tabletHabilitado) tablet.enabled = true;
        UICursor.LimparSobreposicao();
        Cursor.lockState = cursorLockAnterior; Cursor.visible = cursorVisivelAnterior;
        // no 3D, o cursor volta ao estado do jogo (travado), não a um estado solto antigo
        if (reabrir && !TicketTabletPrototype.CelularAberto && UIScreenManager.Active != null) UIScreenManager.Active.ApplyCursorForCurrentState();
        if (tablet != null && tabletHabilitado && tabletEstavaAberto && reabrir && reabrirTabletAoContinuar) tablet.Abrir();
        if (audioUI != null) audioUI.ignoreListenerPause = audioUIIgnoravaPause;
        estado = Estado.Fechado; IsPaused = false;
        if (grupo != null) { grupo.interactable = true; grupo.blocksRaycasts = true; }
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
        Debug.LogWarning("PauseMenu: não foi encontrado o campo float '" + campoSensibilidade + "' em " + controleOlhar.GetType().Name + ". Configure Ao Mudar Sensibilidade para outra integração.", this);
    }
    private void AplicarVolume(float valor)
    {
        volumeAtual = Mathf.Clamp01(valor);
        if (mixer != null)
        {
            float db = volumeAtual <= .0001f ? volumeMinimoDb : Mathf.Max(volumeMinimoDb, 20 * Mathf.Log10(volumeAtual));
            if (!mixer.SetFloat(parametroVolume, db) && !avisouMixer)
            { avisouMixer = true; Debug.LogWarning("PauseMenu: exponha o parâmetro '" + parametroVolume + "' no AudioMixer.", this); }
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
    private void CapturarBlur()
    {
        RenderTexture full = null, pequena = null; RenderTexture anterior = RenderTexture.active;
        try
        {
            int w = Mathf.Min(Screen.width, Mathf.Clamp(resolucaoBlur, 128, 768));
            int h = Mathf.Max(1, Mathf.RoundToInt(w * (float)Screen.height / Mathf.Max(1, Screen.width)));
            full = RenderTexture.GetTemporary(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32);
            ScreenCapture.CaptureScreenshotIntoRenderTexture(full);
            pequena = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(full, pequena); RenderTexture.active = pequena;
            captura = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "Pause_Blur", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            captura.ReadPixels(new Rect(0, 0, w, h), 0, 0, false);
            Color32[] a = captura.GetPixels32(), b = new Color32[a.Length];
            for (int i = 0; i < Mathf.Clamp(passesBlur, 1, 5); i++) { BoxBlur(a, b, w, h, Mathf.Clamp(raioBlur, 1, 20), true); BoxBlur(b, a, w, h, Mathf.Clamp(raioBlur, 1, 20), false); }
            captura.SetPixels32(a); captura.Apply(false, false);
        }
        catch (Exception e) { LiberarCaptura(); Debug.LogWarning("PauseMenu: não foi possível capturar o blur; usando fundo escuro. " + e.Message, this); }
        finally
        {
            RenderTexture.active = anterior;
            if (full != null) RenderTexture.ReleaseTemporary(full);
            if (pequena != null) RenderTexture.ReleaseTemporary(pequena);
        }
    }
    private static void BoxBlur(Color32[] origem, Color32[] destino, int w, int h, int raio, bool horizontal)
    {
        int linhas = horizontal ? h : w, comprimento = horizontal ? w : h;
        int passo = horizontal ? 1 : w, janela = raio * 2 + 1;
        for (int linha = 0; linha < linhas; linha++)
        {
            int inicio = horizontal ? linha * w : linha;
            int r = 0, g = 0, b = 0;
            for (int k = -raio; k <= raio; k++)
            { Color32 c = origem[inicio + Mathf.Clamp(k, 0, comprimento - 1) * passo]; r += c.r; g += c.g; b += c.b; }
            for (int x = 0; x < comprimento; x++)
            {
                destino[inicio + x * passo] = new Color32((byte)(r / janela), (byte)(g / janela), (byte)(b / janela), 255);
                Color32 sai = origem[inicio + Mathf.Clamp(x - raio, 0, comprimento - 1) * passo];
                Color32 entra = origem[inicio + Mathf.Clamp(x + raio + 1, 0, comprimento - 1) * passo];
                r += entra.r - sai.r; g += entra.g - sai.g; b += entra.b - sai.b;
            }
        }
    }
    private void LiberarCaptura() { if (imagemBlur != null) imagemBlur.texture = null; if (captura != null) Destruir(captura); captura = null; }
    private static float EaseOutBack(float t, float intensidade) { float x = t - 1; return 1 + (intensidade + 1) * x * x * x + intensidade * x * x; }

    [ContextMenu("Pause/Gerar interface editável")]
    public void GerarInterface()
    {
        if (raiz != null) return;
        if (tablet != null) CopiarEstiloDoTablet();
        raiz = new GameObject("PauseUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup)).GetComponent<RectTransform>();
        raiz.SetParent(transform, false); raiz.pivot = Vector2.one * .5f;
        canvas = raiz.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32760;
        var scaler = raiz.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
        grupo = raiz.GetComponent<CanvasGroup>();
        var bg = Rect("BlurCapturado", raiz); Stretch(bg); imagemBlur = bg.gameObject.AddComponent<RawImage>(); imagemBlur.raycastTarget = false;
        escurecimento = Imagem("BloqueioEFundo", raiz, corEscurecimento, true); Stretch(escurecimento.rectTransform);
        painel = Imagem("Painel", raiz, corPainel, true).rectTransform; painel.sizeDelta = tamanhoPainel;
        // Folga para glow e bounce dentro de cada página; máscara impede páginas vazarem lateralmente.
        painel.gameObject.AddComponent<RectMask2D>();
        paginaMenu = Rect("PaginaPause", painel); Stretch(paginaMenu); paginaMenu.gameObject.AddComponent<CanvasGroup>();
        paginaConfig = Rect("PaginaConfiguracoes", painel); Stretch(paginaConfig); paginaConfig.gameObject.AddComponent<CanvasGroup>();
        tituloMenuUI = Texto(paginaMenu, "Titulo", tituloPause, tamanhoTitulo);
        tituloConfigUI = Texto(paginaConfig, "Titulo", tituloConfiguracoes, tamanhoTitulo);
        entradasMenu = new RectTransform[3];
        entradasMenu[0] = Holder("Continuar", paginaMenu); continuarUI = Botao(entradasMenu[0], textoContinuar);
        entradasMenu[1] = Holder("Configuracoes", paginaMenu); configuracoesUI = Botao(entradasMenu[1], textoConfiguracoes);
        entradasMenu[2] = Holder("Sair", paginaMenu); sairUI = Botao(entradasMenu[2], textoSair);
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
    // Acrescenta a página também às interfaces que já estavam salvas na cena (e refaz uma versão antiga dela).
    private void GarantirPaginaControles()
    {
        if (controlesUI == null)
        {
            var holder = Holder("Controles", paginaMenu);
            controlesUI = Botao(holder, textoControles);
            var entradas = new List<RectTransform>(entradasMenu);
            entradas.Insert(Mathf.Max(0, entradas.Count - 1), holder);
            entradasMenu = entradas.ToArray();
        }
        if (paginaControles != null && (rolagemControles == null || teclasControles == null || teclasControles.Length != 4 || teclasControles[0] == null))
        {
            var antiga = paginaControles.gameObject; paginaControles = null;
            antiga.transform.SetParent(null); antiga.SetActive(false); Destruir(antiga);
        }
        if (paginaControles != null) return;
        paginaControles = Rect("PaginaControles", painel); Stretch(paginaControles);
        var cg = paginaControles.gameObject.AddComponent<CanvasGroup>();
        cg.interactable = false; cg.blocksRaycasts = false;
        tituloControlesUI = Texto(paginaControles, "Titulo", tituloControles, tamanhoTitulo);
        TextoControle(paginaControles, "Subtitulo", subtituloControles, 24, new Vector2(0, 318), new Vector2(1200, 36))
            .color = CorTextoSuave;

        // Abaixo do cabeçalho, tudo rola: ANDAR e MOUSE no topo (o que aparece ao abrir); teclas e ícones da mira embaixo.
        // Coordenadas fixas no canvas de 1920x1080 (página de 1480x940), a partir do centro de cada peça.
        var area = Rect("Rolagem", paginaControles); Centro(area, new Vector2(0, -30), new Vector2(1440, 640));
        rolagemControles = area.gameObject.AddComponent<ScrollRect>();
        var viewport = Imagem("Viewport", area, new Color(0, 0, 0, 0), true).rectTransform;
        Stretch(viewport, Vector2.zero, new Vector2(-24, 0)); viewport.gameObject.AddComponent<RectMask2D>();
        var conteudo = Rect("Conteudo", viewport);
        conteudo.anchorMin = conteudo.anchorMax = conteudo.pivot = new Vector2(.5f, 1);
        conteudo.sizeDelta = new Vector2(1400, 1028);   // 988 de cartões + 20 de folga em cima e embaixo conteudo.anchoredPosition = Vector2.zero;
        var meio = Rect("Meio", conteudo); Centro(meio, Vector2.zero, Vector2.zero);   // origem no centro do conteúdo
        rolagemControles.viewport = viewport; rolagemControles.content = conteudo;
        rolagemControles.horizontal = false; rolagemControles.vertical = true;
        rolagemControles.movementType = ScrollRect.MovementType.Clamped; rolagemControles.scrollSensitivity = 35;
        rolagemControles.verticalScrollbar = BarraRolagem(area);
        rolagemControles.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

        var cartaoTeclado = Cartao(meio, "CartaoMovimento", new Vector2(-350, 294), new Vector2(660, 400));
        var cartaoMouse = Cartao(meio, "CartaoMouse", new Vector2(350, 294), new Vector2(660, 400));
        var cartaoTeclas = Cartao(meio, "CartaoTeclas", new Vector2(0, -25), new Vector2(1360, 190));
        var cartaoMira = Cartao(meio, "CartaoMira", new Vector2(0, -319), new Vector2(1360, 350));
        TituloCartao(cartaoTeclado, "ANDAR", 166);
        TituloCartao(cartaoMouse, "MOUSE", 166);
        TituloCartao(cartaoTeclas, "TECLAS", 70);
        TituloCartao(cartaoMira, "NA MIRA", 150);

        // W A S D (PlayerMovement): tecla física com a seta da direção do lado de fora
        string[] letras = { "W", "A", "S", "D" }, acoes = { "FRENTE", "ESQUERDA", "TRÁS", "DIREITA" };
        Vector2[] pos = { new Vector2(0, 46), new Vector2(-136, -90), new Vector2(0, -90), new Vector2(136, -90) };
        Vector2[] dir = { Vector2.up, Vector2.left, Vector2.down, Vector2.right };
        teclasControles = new Image[4]; setasControles = new Image[4]; baseTeclas = new Vector2[4]; baseSetas = new Vector2[4];
        for (int i = 0; i < 4; i++)
        {
            Centro(Imagem("Tecla" + letras[i], cartaoTeclado, CorTeclaBase).rectTransform, pos[i] + Vector2.down * 8, new Vector2(120, 120));
            var face = Imagem("Face", cartaoTeclado, CorTecla); Centro(face.rectTransform, pos[i], new Vector2(120, 120));
            Contorno(face, .35f);
            var letra = TextoControle(face.rectTransform, "Letra", letras[i], 56, new Vector2(0, 12), new Vector2(110, 70));
            letra.fontStyle = FontStyles.Bold; letra.color = HudEstilo.Valor;
            TextoControle(face.rectTransform, "Acao", acoes[i], 15, new Vector2(0, -36), new Vector2(112, 24)).color = HudEstilo.Rotulo;
            var seta = Imagem("Seta", cartaoTeclado, HudEstilo.Borda);
            Centro(seta.rectTransform, pos[i] + dir[i] * 84, new Vector2(28, 34));
            seta.rectTransform.localEulerAngles = new Vector3(0, 0, Mathf.Atan2(dir[i].y, dir[i].x) * Mathf.Rad2Deg);
            teclasControles[i] = face; setasControles[i] = seta; baseTeclas[i] = pos[i]; baseSetas[i] = pos[i] + dir[i] * 84;
        }

        // Mouse (InteractionDetector): os botões são filhos do corpo e recortados pelo formato dele (Mask), então
        // nunca passam da borda; cada botão liga ao seu texto por um traço
        var corpo = Imagem("CorpoMouse", cartaoMouse, CorTecla);
        Centro(corpo.rectTransform, new Vector2(0, -10), new Vector2(170, 270));
        corpo.gameObject.AddComponent<Mask>().showMaskGraphic = true;
        botoesMouse = new Image[2]; titulosMouse = new TMP_Text[2];
        for (int i = 0; i < 2; i++)
        {
            float lado = i == 0 ? -1 : 1;
            var botaoMouse = Imagem(i == 0 ? "BotaoEsquerdo" : "BotaoDireito", corpo.rectTransform, CorTeclaClara);
            Centro(botaoMouse.rectTransform, new Vector2(lado * 43, 80), new Vector2(82, 110));
            botoesMouse[i] = botaoMouse;
            var linha = Imagem("Linha", cartaoMouse, new Color(HudEstilo.Borda.r, HudEstilo.Borda.g, HudEstilo.Borda.b, .6f));
            Centro(linha.rectTransform, new Vector2(lado * 112, 70), new Vector2(50, 3));
            titulosMouse[i] = TextoControle(cartaoMouse, "Titulo", i == 0 ? "CLIQUE ESQUERDO" : "CLIQUE DIREITO", 20,
                new Vector2(lado * 232, 96), new Vector2(190, 30));
            titulosMouse[i].fontStyle = FontStyles.Bold;
            TextoControle(cartaoMouse, "Acao", i == 0 ? "Pegar, abrir e usar.\nCom item na mão: soltar." :
                "Abrir a tela do equipamento.\nVale com item na mão.", 17, new Vector2(lado * 232, 32), new Vector2(190, 96))
                .color = CorTextoSuave;
        }
        Centro(Imagem("Roda", corpo.rectTransform, CorTeclaBase).rectTransform, new Vector2(0, 86), new Vector2(14, 42));
        // contorno FORA da máscara e depois dela: fica por cima dos botões e esconde o serrilhado do recorte
        Centro(Imagem("ContornoMouse", cartaoMouse, HudEstilo.Borda).rectTransform, new Vector2(0, -10), new Vector2(170, 270));
        TextoControle(cartaoMouse, "Olhar", "Mover o mouse: olhar ao redor", 18, new Vector2(0, -172), new Vector2(600, 28))
            .color = CorTextoSuave;

        // Teclas (TicketTabletPrototype, PauseMenu/UIScreenManager, telas com vistas, TutorialMontagem): tecla em cima,
        // o que ela faz embaixo
        (string, string)[] atalhos =
        {
            ("T", "Tablet: chamados e guia"), ("ESC", "Fechar a tela ou pausar"), ("ALT", "Segurar: soltar o cursor"),
            ("SETAS", "Trocar a vista do aparelho"), ("TAB", "Minimizar o tutorial"),
        };
        for (int i = 0; i < atalhos.Length; i++)
        {
            float x = -544 + i * 272;
            Centro(Imagem("Atalho" + atalhos[i].Item1, cartaoTeclas, CorTeclaBase).rectTransform, new Vector2(x, 4), new Vector2(104, 52));
            var tecla = Imagem("Atalho" + atalhos[i].Item1, cartaoTeclas, CorTecla); Contorno(tecla, .35f);
            Centro(tecla.rectTransform, new Vector2(x, 10), new Vector2(104, 52));
            var rotulo = TextoControle(tecla.rectTransform, "Tecla", atalhos[i].Item1, 20, Vector2.zero, new Vector2(96, 44));
            rotulo.fontStyle = FontStyles.Bold; rotulo.color = HudEstilo.Valor;
            TextoControle(cartaoTeclas, "Texto", atalhos[i].Item2, 17, new Vector2(x, -50), new Vector2(250, 40)).color = CorTextoSuave;
        }

        // Na mira (InteractionPrompt + IconesInteracao.Para): todos os ícones que o HUD mostra, pelo clique que os usa
        SubtituloGrupo(cartaoMira, "CLIQUE ESQUERDO", -340);
        SubtituloGrupo(cartaoMira, "CLIQUE DIREITO", 340);
        (Sprite, string)[] esquerdo =
        {
            (IconesInteracao.Pegar, "Pegar ou usar um objeto"), (IconesInteracao.Abrir, "Abrir gaveta ou porta"),
            (IconesInteracao.Rack, "Abrir o rack de rede"), (IconesInteracao.Bloqueado, "Ação bloqueada agora (o motivo aparece na mira)"),
        };
        (Sprite, string)[] direito =
        {
            (IconesInteracao.ChaveDeFenda, "Manutenção do gabinete ou do notebook"), (IconesInteracao.Monitor, "Manutenção do PC pelo monitor"),
            (IconesInteracao.Impressora, "Painel da impressora"), (IconesInteracao.Testador, "Testador de cabo de rede"),
            (IconesInteracao.Crimpador, "Crimpar o cabo na caixa (com o alicate, também no esquerdo)"),
        };
        for (int i = 0; i < esquerdo.Length; i++) ItemMira(cartaoMira, esquerdo[i], new Vector2(-500 + i % 2 * 320, 50 - i / 2 * 80));
        for (int i = 0; i < direito.Length; i++) ItemMira(cartaoMira, direito[i], new Vector2(180 + i % 2 * 320, 50 - i / 2 * 80));
        Centro(Imagem("Linha", cartaoMira, new Color(HudEstilo.Borda.r, HudEstilo.Borda.g, HudEstilo.Borda.b, .18f)).rectTransform,
            new Vector2(0, -40), new Vector2(2, 250));

        voltarControlesUI = Botao(Holder("Voltar", paginaControles), textoVoltar);
        if (mensagemUI != null) mensagemUI.transform.SetAsLastSibling();
    }
    private void LayoutControles(Vector2 botao)
    {
        float topo = alturaPaginaControles * .5f;
        Centro(tituloControlesUI.rectTransform, new Vector2(0, topo - 90), new Vector2(1200, 80));
        Centro((RectTransform)voltarControlesUI.transform.parent, new Vector2(0, -400), botao);
        tituloControlesUI.text = tituloControles;
        paginaControles.Find("Subtitulo").GetComponent<TMP_Text>().text = subtituloControles;
        controlesUI.GetComponentInChildren<TMP_Text>().text = textoControles;
        voltarControlesUI.GetComponentInChildren<TMP_Text>().text = textoVoltar;
    }
    // Cartões e teclas com os cantos arredondados do card; setas com o triângulo desenhado (sem glifo de fonte).
    // Cores da página: as do HUD de jogo (selos de pontos e tempo, HudEstilo), não o gradiente dos botões.
    private void EstilizarControles(Sprite redondo)
    {
        if (paginaControles == null) return;
        Sprite triangulo = CriarTriangulo(); recursosVisuais.Add(triangulo.texture); recursosVisuais.Add(triangulo);
        // mouse em resolução própria (raio 60 sem esticar o sprite do card, que borrava) e contorno de 3 px por cima
        Sprite corpoMouse = CriarArredondado(60), contornoMouse = CriarContorno(60, 3);
        foreach (var img in paginaControles.GetComponentsInChildren<Image>(true))
        {
            if (img.GetComponentInParent<Button>() != null || img.name == "Icone" || img.name == "Viewport") continue;
            if (img.name == "Seta") { img.sprite = triangulo; img.type = Image.Type.Simple; continue; }
            if (img.name == "Linha") continue;
            if (img.name == "CorpoMouse" || img.name == "ContornoMouse")
            { img.sprite = img.name == "CorpoMouse" ? corpoMouse : contornoMouse; img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 1; continue; }
            img.sprite = redondo; img.type = Image.Type.Sliced;
            // cantos bem redondos no corpo do mouse; nos botões dele, cantos pequenos (os de fora são os do corpo)
            img.pixelsPerUnitMultiplier = img.name.StartsWith("Botao") ? 2f : img.name == "Roda" ? .5f : 1f;
        }
        // o título desta página fica no branco dos valores do HUD, com o contorno escuro dos textos
        tituloControlesUI.enableVertexGradient = false; tituloControlesUI.color = HudEstilo.Valor;
        AplicarMaterialTexto(tituloControlesUI);
        // o material da fonte do tablet tinge a face das letras (amarelado): aqui a face é branca e a cor vem só do texto
        var brancos = new Dictionary<Material, Material>();
        foreach (var t in paginaControles.GetComponentsInChildren<TMP_Text>(true))
        {
            if (t.GetComponentInParent<Button>() != null) continue;
            Material origem = t.fontSharedMaterial;
            if (origem == null || !origem.HasProperty("_FaceColor")) continue;
            if (!brancos.TryGetValue(origem, out Material branco))
            {
                branco = new Material(origem) { name = "Pause_Controles", hideFlags = HideFlags.HideAndDontSave };
                brancos.Add(origem, branco); recursosVisuais.Add(branco);
            }
            branco.SetColor("_FaceColor", Color.white);
            t.fontSharedMaterial = branco;
        }
    }
    // W A S D acendem em sequência (a tecla afunda e a seta dá um passo na direção); os botões do mouse alternam.
    private void AnimarControles(float dt)
    {
        if (teclasControles == null || botoesMouse == null) return;
        float t = Time.unscaledTime, suave = 1 - Mathf.Exp(-12 * dt);
        int acesa = Mathf.FloorToInt(t / .6f) % 5;   // 4 = pausa curta com todas apagadas
        for (int i = 0; i < 4; i++)
        {
            brilhoTeclas[i] = Mathf.Lerp(brilhoTeclas[i], i == acesa ? 1 : 0, suave);
            float b = brilhoTeclas[i];
            teclasControles[i].rectTransform.anchoredPosition = baseTeclas[i] + Vector2.down * 6 * b;
            teclasControles[i].color = Color.Lerp(CorTecla, CorTeclaAcesa, b);
            Vector2 passo = (baseSetas[i] - baseTeclas[i]).normalized;
            setasControles[i].rectTransform.anchoredPosition = baseSetas[i] + passo * 10 * b;
            setasControles[i].color = new Color(HudEstilo.Borda.r, HudEstilo.Borda.g, HudEstilo.Borda.b, .4f + .6f * b);
        }
        int lado = Mathf.FloorToInt(t / 1.4f) % 2;
        for (int i = 0; i < 2; i++)
        {
            brilhoMouse[i] = Mathf.Lerp(brilhoMouse[i], i == lado ? 1 : 0, suave);
            botoesMouse[i].color = Color.Lerp(CorTeclaClara, CorTeclaAcesa, brilhoMouse[i]);
            titulosMouse[i].color = Color.Lerp(HudEstilo.Valor, HudEstilo.Borda, brilhoMouse[i]);
        }
    }
    // tons das teclas: o azul-marinho do selo do HUD, um pouco mais claro; acesa = verde-água da borda dos selos
    private static readonly Color CorTecla = new Color(.11f, .17f, .26f, 1), CorTeclaClara = new Color(.16f, .24f, .35f, 1),
        CorTeclaBase = new Color(.03f, .05f, .09f, 1), CorTeclaAcesa = new Color(.13f, .45f, .46f, 1),
        CorTextoSuave = new Color(.82f, .88f, .94f, 1);
    private RectTransform Cartao(Transform pai, string nome, Vector2 pos, Vector2 tamanho)
    {
        var img = Imagem(nome, pai, HudEstilo.Fundo); Contorno(img, .45f);
        Centro(img.rectTransform, pos, tamanho); return img.rectTransform;
    }
    private static void Contorno(Image img, float alfa)
    {
        var o = img.gameObject.AddComponent<Outline>();
        o.effectColor = new Color(HudEstilo.Borda.r, HudEstilo.Borda.g, HudEstilo.Borda.b, alfa);
        o.effectDistance = new Vector2(1.5f, -1.5f); o.useGraphicAlpha = true;
    }
    private void TituloCartao(RectTransform cartao, string texto, float y)
    {
        var t = TextoControle(cartao, "TituloCartao", texto, 24, new Vector2(0, y), new Vector2(600, 36));
        t.fontStyle = FontStyles.Bold; t.color = HudEstilo.Rotulo; t.characterSpacing = 8;
    }
    private void SubtituloGrupo(RectTransform cartao, string texto, float x)
    {
        var t = TextoControle(cartao, "Grupo", texto, 17, new Vector2(x, 106), new Vector2(620, 28));
        t.fontStyle = FontStyles.Bold; t.color = HudEstilo.Borda;
    }
    // ícone num ladrilho escuro (o mesmo para a mãozinha em pixel art e os ícones cartoon) e o significado ao lado
    private void ItemMira(RectTransform cartao, (Sprite, string) item, Vector2 pos)
    {
        var ladrilho = Imagem("Ladrilho", cartao, CorTecla); Centro(ladrilho.rectTransform, pos + new Vector2(-120, 0), new Vector2(64, 64));
        var icone = Imagem("Icone", ladrilho.rectTransform, Color.white); icone.sprite = item.Item1; icone.preserveAspect = true;
        Centro(icone.rectTransform, Vector2.zero, new Vector2(48, 48));
        var texto = TextoControle(cartao, "Texto", item.Item2, 17, pos + new Vector2(40, 0), new Vector2(232, 64));
        texto.alignment = TextAlignmentOptions.MidlineLeft; texto.color = HudEstilo.Valor;
    }
    private Scrollbar BarraRolagem(RectTransform area)
    {
        var trilho = Imagem("BarraRolagem", area, new Color(HudEstilo.Borda.r, HudEstilo.Borda.g, HudEstilo.Borda.b, .12f), true).rectTransform;
        trilho.anchorMin = new Vector2(1, 0); trilho.anchorMax = Vector2.one; trilho.pivot = new Vector2(1, .5f);
        trilho.sizeDelta = new Vector2(8, -20); trilho.anchoredPosition = Vector2.zero;
        var deslize = Rect("Deslize", trilho); Stretch(deslize);
        var alca = Imagem("Alca", deslize, new Color(HudEstilo.Borda.r, HudEstilo.Borda.g, HudEstilo.Borda.b, .7f), true);
        Stretch(alca.rectTransform);
        var barra = trilho.gameObject.AddComponent<Scrollbar>();
        barra.handleRect = alca.rectTransform; barra.targetGraphic = alca; barra.direction = Scrollbar.Direction.BottomToTop;
        barra.navigation = new Navigation { mode = Navigation.Mode.None };
        return barra;
    }
    private TMP_Text TextoControle(Transform pai, string nome, string texto, float tamanho, Vector2 pos, Vector2 caixa)
    {
        var t = Texto(pai, nome, texto, tamanho); Centro(t.rectTransform, pos, caixa);
        t.enableAutoSizing = true; t.fontSizeMin = 12; t.fontSizeMax = tamanho; return t;
    }
    // só a borda de um retângulo arredondado (espessura em px), suavizada dos dois lados
    private Sprite CriarContorno(int raio, float espessura)
    {
        int n = raio * 2 + 6; var pixels = new Color[n * n];
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
        {
            float d = DistanciaBorda(x + .5f - n * .5f, y + .5f - n * .5f, n, n, raio);
            pixels[y * n + x] = new Color(1, 1, 1, Mathf.Clamp01(.5f - d) * Mathf.Clamp01(.5f + d + espessura));
        }
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
        tex.SetPixels(pixels); tex.Apply(false, true);
        var sp = Sprite.Create(tex, new Rect(0, 0, n, n), Vector2.one * .5f, 100, 0, SpriteMeshType.FullRect, Vector4.one * (raio + 2));
        sp.hideFlags = HideFlags.HideAndDontSave; recursosVisuais.Add(tex); recursosVisuais.Add(sp); return sp;
    }
    private Sprite CriarTriangulo()
    {
        const int n = 64; var pixels = new Color[n * n];
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
        {
            // aponta para a direita; meia altura cai linearmente de x = 0 a x = n
            float meia = (n - x - .5f) * .5f, dy = Mathf.Abs(y + .5f - n * .5f);
            pixels[y * n + x] = new Color(1, 1, 1, Mathf.Clamp01(meia - dy + .5f));
        }
        return SpriteDosPixels(pixels, n, n, "Pause_Seta");
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
        Ligar(controlesUI, MostrarControles); Ligar(voltarControlesUI, Voltar);
        Ligar(continuarUI, Continuar); Ligar(configuracoesUI, MostrarConfiguracoes); Ligar(sairUI, SairParaMenu); Ligar(voltarUI, Voltar);
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
        GarantirPaginaControles();
        larguraTela = Screen.width; alturaTela = Screen.height;
        // Cada página é uma coluna centralizada (eixo no centro do card); o card toma a altura da página aberta.
        float k = escalaElementos;
        Vector2 botao = tamanhoBotao * k, slider = tamanhoSlider * k;
        float espaco = espacoBotoes * k, alturaTitulo = tamanhoTitulo * k * 1.15f * 1.5f, rotulo = 42 * k;
        float largura = Mathf.Max(tamanhoPainel.x, botao.x + 200, slider.x + 160);
        float blocoMenu = alturaTitulo + espaco * 1.5f + entradasMenu.Length * botao.y + (entradasMenu.Length - 1) * espaco;
        float grupoSlider = rotulo + 10 * k + slider.y;
        float blocoConfig = alturaTitulo + espaco * 1.5f + grupoSlider * 2 + espaco * 2 + espaco * 2 + botao.y;
        alturaPaginaMenu = blocoMenu + margemCard * 2; alturaPaginaConfig = blocoConfig + margemCard * 2;
        larguraPaginaControles = Mathf.Max(largura, tamanhoPaginaControles.x);
        alturaPaginaControles = Mathf.Max(600, tamanhoPaginaControles.y);
        float alturaMax = Mathf.Max(alturaPaginaMenu, alturaPaginaConfig, alturaPaginaControles);
        larguraPainel = largura;
        float scale = escalaMenu;
        if (Application.isPlaying)
        {
            Vector2 disponivel = raiz.rect.size - new Vector2(Mathf.Abs(posicaoMenu.x), Mathf.Abs(posicaoMenu.y)) * 2 - Vector2.one * 40;
            if (disponivel.x > 1 && disponivel.y > 1) scale = Mathf.Max(.05f, Mathf.Min(scale, disponivel.x / larguraPaginaControles, disponivel.y / alturaMax));
        }
        painel.localScale = Vector3.one * scale; painel.anchoredPosition = posicaoMenu;
        float y = blocoMenu * .5f;
        Centro(tituloMenuUI.rectTransform, new Vector2(0, y - alturaTitulo * .5f), new Vector2(largura - 50, alturaTitulo));
        y -= alturaTitulo + espaco * 1.5f;
        for (int i = 0; i < entradasMenu.Length; i++)
        {
            Centro(entradasMenu[i], new Vector2(0, y - botao.y * .5f - i * (botao.y + espaco)), botao);
            if (entradasBase != null && i < entradasBase.Length) entradasBase[i] = entradasMenu[i].anchoredPosition;
        }
        y = blocoConfig * .5f;
        Centro(tituloConfigUI.rectTransform, new Vector2(0, y - alturaTitulo * .5f), new Vector2(largura - 50, alturaTitulo));
        y -= alturaTitulo + espaco * 1.5f;
        foreach (var par in new[] { (valorVolumeUI, volumeUI), (valorSensUI, sensUI) })
        {
            Centro(par.Item1.rectTransform, new Vector2(0, y - rotulo * .5f), new Vector2(slider.x, rotulo));
            Centro((RectTransform)par.Item2.transform, new Vector2(0, y - rotulo - 10 * k - slider.y * .5f), slider);
            y -= grupoSlider + espaco * 2;
        }
        Centro((RectTransform)voltarUI.transform.parent, new Vector2(0, y - botao.y * .5f), botao);
        LayoutControles(botao);
        var msg = mensagemUI.rectTransform; msg.anchorMin = msg.anchorMax = new Vector2(.5f, 0);
        msg.sizeDelta = new Vector2(largura - 50, 50); msg.anchoredPosition = new Vector2(0, margemCard * .5f);
        tituloMenuUI.text = tituloPause; tituloConfigUI.text = tituloConfiguracoes;
        continuarUI.GetComponentInChildren<TMP_Text>().text = textoContinuar; configuracoesUI.GetComponentInChildren<TMP_Text>().text = textoConfiguracoes;
        sairUI.GetComponentInChildren<TMP_Text>().text = textoSair; voltarUI.GetComponentInChildren<TMP_Text>().text = textoVoltar;
        sensUI.minValue = Mathf.Max(.0001f, sensibilidadeMinima); sensUI.maxValue = Mathf.Max(sensUI.minValue + .001f, sensibilidadeMaxima);
        escurecimento.color = corEscurecimento;
        if (!Application.isPlaying)
        {
            raiz.gameObject.SetActive(previaNoEditor); grupo.alpha = 1;
            pagina = previaControles ? -1 : previaConfiguracoes ? 1 : 0;
            foreach (var entrada in entradasMenu) { entrada.localScale = Vector3.one; entrada.GetComponent<CanvasGroup>().alpha = 1; }
            imagemBlur.gameObject.SetActive(false);
        }
        PosicionarPaginas();
    }
    private void PosicionarPaginas()
    {
        // a página de controles é mais larga: o card cresce na largura também, e as páginas ficam afastadas pela maior
        float distancia = Mathf.Max(larguraPainel, larguraPaginaControles) + 60;
        float u = Mathf.Abs(pagina);
        painel.sizeDelta = pagina < 0
            ? new Vector2(Mathf.Lerp(larguraPainel, larguraPaginaControles, u), Mathf.Lerp(alturaPaginaMenu, alturaPaginaControles, u))
            : new Vector2(larguraPainel, Mathf.Lerp(alturaPaginaMenu, alturaPaginaConfig, u));
        paginaMenu.anchoredPosition = new Vector2(-pagina * distancia, 0);
        paginaConfig.anchoredPosition = new Vector2((1 - pagina) * distancia, 0);
        if (paginaControles != null) paginaControles.anchoredPosition = new Vector2((-1 - pagina) * distancia, 0);
    }
    [ContextMenu("Pause/Copiar estilo do tablet")]
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
    [ContextMenu("Pause/Atualizar aparência")]
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
            // textos desenhados da página de controles: tamanho e cor próprios (dados ao construir a página)
            if (paginaControles != null && t != tituloControlesUI && t.transform.IsChildOf(paginaControles) && t.GetComponentInParent<Button>() == null)
            { AplicarMaterialTexto(t); continue; }
            t.color = t == mensagemUI ? new Color(1, .65f, .4f) : corTexto;
            t.fontSize = (t == tituloMenuUI || t == tituloConfigUI || t == tituloControlesUI ? tamanhoTitulo * 1.15f : t == mensagemUI ? 18 : tamanhoTexto) * escalaElementos;
            AplicarMaterialTexto(t);
        }
        EstilizarTitulo(tituloMenuUI); EstilizarTitulo(tituloConfigUI); EstilizarTitulo(tituloControlesUI);
        EstilizarControles(redondo);
    }
    // Título em destaque com o visual dos botões: gradiente verde (gradienteTopo → gradienteBase), contorno escuro
    // (o mesmo dos botões) e brilho externo na cor do glow dos botões (underlay sem deslocamento).
    private void EstilizarTitulo(TMP_Text t)
    {
        if (t == null) return;
        t.color = Color.white; t.fontStyle = FontStyles.Bold;
        t.enableVertexGradient = true; t.colorGradient = new VertexGradient(gradienteTopo, gradienteTopo, gradienteBase, gradienteBase);
        Material origem = t.font != null ? t.font.material : t.fontSharedMaterial;
        if (origem == null) return;
        var material = new Material(origem) { name = "Pause_Titulo", hideFlags = HideFlags.HideAndDontSave };
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
            material.SetFloat("_UnderlaySoftness", .35f); material.SetFloat("_UnderlayDilate", .2f);
        }
        t.fontSharedMaterial = material; t.extraPadding = true; t.UpdateMeshPadding();
    }
    private static void Centro(RectTransform r, Vector2 pos, Vector2 size) { r.anchorMin = r.anchorMax = r.pivot = Vector2.one * .5f; r.sizeDelta = size; r.anchoredPosition = pos; }
    // Contorno e sombra do texto (cópia local do material da fonte; o asset da fonte nunca muda).
    private void AplicarMaterialTexto(TMP_Text t)
    {
        {
            Material origem = t.fontSharedMaterial;
            if (origem == null) return;
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
    }
    // ================================================================== estilo dos botões para outras telas
    /// <summary>
    /// Dá a <paramref name="b"/> (de OUTRA tela, ex.: o relatório final) o MESMO visual dos botões da pausa e do menu:
    /// gradiente com brilho interno, brilho externo, cantos e o texto com a fonte, a cor, o contorno e a sombra
    /// configurados aqui. Os sprites ficam com quem chamou (não são limpos quando a pausa refaz o layout).
    /// </summary>
    public TMP_Text EstilizarBotaoExterno(Button b, string label, float tamanhoFonte = 0f)
    {
        if (b == null) return null;
        Canvas.ForceUpdateCanvases();
        var size = ((RectTransform)b.transform).rect.size;
        Sprite[] sprites = CriarEstiloBotao(Mathf.Clamp(Mathf.RoundToInt(size.x), 2, 2048), Mathf.Clamp(Mathf.RoundToInt(size.y), 2, 1024));
        b.image.sprite = sprites[0]; b.image.type = Image.Type.Simple; b.image.color = Color.white;
        AplicarBordaESombra(b.image, efeitosBotoes);
        Image glow = Imagem("OuterGlow", b.transform, Color.white);
        glow.transform.SetAsFirstSibling(); float margemGlow = Mathf.Ceil(Mathf.Max(0, larguraOuterGlow)) + 2;
        Stretch(glow.rectTransform, -Vector2.one * margemGlow, Vector2.one * margemGlow);
        glow.sprite = sprites[1]; glow.type = Image.Type.Simple; glow.raycastTarget = false;
        var texto = Texto(b.transform, "Texto", label, tamanhoFonte > 0f ? tamanhoFonte : tamanhoTexto);
        Stretch(texto.rectTransform, new Vector2(12, 5), new Vector2(-12, -5));
        texto.enableAutoSizing = true; texto.fontSizeMin = 14; texto.fontSizeMax = texto.fontSize;
        AplicarMaterialTexto(texto);
        texto.extraPadding = true; texto.UpdateMeshPadding();
        ColorBlock cb = b.colors; cb.highlightedColor = Color.white; cb.pressedColor = new Color(.85f, .85f, .85f); b.colors = cb;
        return texto;
    }
    /// <summary>Escala do hover dos botões da pausa (para outras telas imitarem).</summary>
    public float EscalaHoverBotoes => escalaHover;
    public void TocarCliqueBotao() => Tocar(somClique);
    public void TocarHoverBotao() => Tocar(somHover);

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
[UnityEditor.CustomEditor(typeof(PauseMenu))]
public class PauseMenuEditor : UnityEditor.Editor
{
    private void OnEnable() { UnityEditor.EditorApplication.delayCall += RestaurarPrevia; }
    private void RestaurarPrevia()
    {
        if (this == null || target == null || Application.isPlaying) return;
        var menu = (PauseMenu)target; menu.AtualizarLayout(); menu.AtualizarAparencia(); UnityEditor.SceneView.RepaintAll();
    }
    public override void OnInspectorGUI()
    {
        UnityEditor.EditorGUI.BeginChangeCheck(); DrawDefaultInspector(); bool mudou = UnityEditor.EditorGUI.EndChangeCheck();
        var menu = (PauseMenu)target;
        UnityEditor.EditorGUILayout.Space();
        UnityEditor.EditorGUILayout.HelpBox("1. Atribua o tablet e PlayerLook.\n2. Adicione movimento/interação e minigames com input direto à lista de bloqueio.\n3. Informe a cena do menu ou conecte o evento de saída.\n4. Gere a interface e use Prévia No Editor. Blur real aparece em Play.\nUse EventSystem + InputSystemUIInputModule e fontes TMP SDF.", UnityEditor.MessageType.Info);
        if (GUILayout.Button("Gerar interface editável"))
        {
            UnityEditor.Undo.RegisterFullObjectHierarchyUndo(menu.gameObject, "Gerar pause");
            menu.GerarInterface(); mudou = true;
        }
        if (GUILayout.Button("Copiar estilo do tablet"))
        {
            UnityEditor.Undo.RegisterFullObjectHierarchyUndo(menu.gameObject, "Copiar estilo");
            menu.CopiarEstiloDoTablet(); mudou = true;
        }
        if (GUILayout.Button("Atualizar aparência")) { menu.AtualizarAparencia(); mudou = true; }
        if (mudou && !Application.isPlaying)
        {
            menu.AtualizarLayout(); menu.AtualizarAparencia(); UnityEditor.EditorUtility.SetDirty(menu);
            UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(menu);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(menu.gameObject.scene); UnityEditor.SceneView.RepaintAll();
        }
        if (Application.isPlaying && GUILayout.Button(PauseMenu.IsPaused ? "Continuar jogo" : "Abrir pause"))
        { if (PauseMenu.IsPaused) menu.Continuar(); else menu.AbrirPause(); }
    }
}
#endif
