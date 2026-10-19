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
/// sem Renderer Feature/shader externo. Não altera o código do player ou do tablet.</summary>
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

    public enum DestinoSaida { Cena, Evento }
    [Header("SAIR: voltar ao menu inicial")]
    [SerializeField] private DestinoSaida destinoSaida = DestinoSaida.Cena;
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
    private bool audioPausadoAntes, audioUIIgnoravaPause, configuracoesProntas, avisouMixer;
    private float escalaTempoAnterior, aberturaEm, volumeAtual, sensAtual, pagina, paginaOrigem, paginaAlvo, paginaInicio;
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
        if (estado != Estado.Fechado) RestaurarJogo(false);
        Salvar(); if (raiz != null) raiz.gameObject.SetActive(false);
        if (dono == this) { dono = null; IsPaused = false; }
    }
    private void OnDestroy() { LiberarCaptura(); LimparEstilos(); }
    private void OnApplicationQuit() { Salvar(); }
    private bool PodeAbrir()
    {
        if (carregandoCena || !configuracoesProntas) return false;
        if (MainMenuScreen.Showing || StageReportScreen.Showing) return false;
        if (bloquearSeAtivos != null) foreach (var go in bloquearSeAtivos) if (go != null && go.activeInHierarchy) return false;
        return true;
    }
    private void Update()
    {
        if (!iniciou) return;
        if (Keyboard.current != null && teclaPause != Key.None && Keyboard.current[teclaPause].wasPressedThisFrame)
        {
            if (estado == Estado.Fechado) AbrirPause();
            else if (estado == Estado.Aberto)
            { if (paginaAlvo > .5f) MostrarPagina(false); else Continuar(); }
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
        estado = Estado.Capturando; IsPaused = true;
        escalaTempoAnterior = Time.timeScale; audioPausadoAntes = AudioListener.pause;
        cursorLockAnterior = Cursor.lockState; cursorVisivelAnterior = Cursor.visible;
        tabletHabilitado = tablet != null && tablet.enabled; tabletEstavaAberto = tabletHabilitado && tablet.IsOpen;
        if (tabletHabilitado) { tablet.Fechar(); tablet.enabled = false; }
        controles.Clear(); GuardarControle(controleOlhar);
        if (desativarDurantePause != null) foreach (var c in desativarDurantePause) GuardarControle(c);
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
        StartCoroutine(FecharAnimado(false));
    }
    public void MostrarConfiguracoes() { if (estado == Estado.Aberto) MostrarPagina(true); }
    public void Voltar() { if (estado == Estado.Aberto) MostrarPagina(false); }
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
        Cursor.lockState = cursorLockAnterior; Cursor.visible = cursorVisivelAnterior;
        if (tablet != null && tabletHabilitado && tabletEstavaAberto && reabrir && reabrirTabletAoContinuar) tablet.Abrir();
        if (audioUI != null) audioUI.ignoreListenerPause = audioUIIgnoravaPause;
        estado = Estado.Fechado; IsPaused = false;
        if (grupo != null) { grupo.interactable = true; grupo.blocksRaycasts = true; }
    }
    private void GarantirCursor()
    {
        if (estado == Estado.Fechado || raiz == null || !raiz.gameObject.activeInHierarchy) return;
        Cursor.lockState = CursorLockMode.None; UICursor.LimparSobreposicao();
        bool sprite = spriteCursor != null && Mouse.current != null;
        Cursor.visible = !sprite; cursorUI.gameObject.SetActive(sprite);
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
        larguraTela = Screen.width; alturaTela = Screen.height;
        painel.sizeDelta = tamanhoPainel;
        float scale = escalaMenu;
        if (Application.isPlaying)
        {
            Vector2 disponivel = raiz.rect.size - new Vector2(Mathf.Abs(posicaoMenu.x), Mathf.Abs(posicaoMenu.y)) * 2 - Vector2.one * 40;
            if (disponivel.x > 1 && disponivel.y > 1) scale = Mathf.Max(.05f, Mathf.Min(scale, disponivel.x / tamanhoPainel.x, disponivel.y / tamanhoPainel.y));
        }
        painel.localScale = Vector3.one * scale; painel.anchoredPosition = posicaoMenu;
        Posicao(tituloMenuUI.rectTransform, posicaoTitulo, new Vector2(tamanhoPainel.x - 50, 85));
        Posicao(tituloConfigUI.rectTransform, posicaoTitulo, new Vector2(tamanhoPainel.x - 50, 85));
        for (int i = 0; i < entradasMenu.Length; i++)
        {
            Posicao(entradasMenu[i], new Vector2(0, -inicioBotoes - i * (tamanhoBotao.y + espacoBotoes)), tamanhoBotao);
            if (entradasBase != null && i < entradasBase.Length) entradasBase[i] = entradasMenu[i].anchoredPosition;
        }
        Posicao(valorVolumeUI.rectTransform, posicaoVolume + Vector2.up * distanciaLabelSlider, new Vector2(tamanhoSlider.x, 42));
        Posicao((RectTransform)volumeUI.transform, posicaoVolume, tamanhoSlider);
        Posicao(valorSensUI.rectTransform, posicaoSensibilidade + Vector2.up * distanciaLabelSlider, new Vector2(tamanhoSlider.x, 42));
        Posicao((RectTransform)sensUI.transform, posicaoSensibilidade, tamanhoSlider);
        Posicao((RectTransform)voltarUI.transform.parent, posicaoVoltar, tamanhoBotao);
        Posicao(mensagemUI.rectTransform, new Vector2(0, -tamanhoPainel.y + 45), new Vector2(tamanhoPainel.x - 50, 70));
        tituloMenuUI.text = tituloPause; tituloConfigUI.text = tituloConfiguracoes;
        continuarUI.GetComponentInChildren<TMP_Text>().text = textoContinuar; configuracoesUI.GetComponentInChildren<TMP_Text>().text = textoConfiguracoes;
        sairUI.GetComponentInChildren<TMP_Text>().text = textoSair; voltarUI.GetComponentInChildren<TMP_Text>().text = textoVoltar;
        sensUI.minValue = Mathf.Max(.0001f, sensibilidadeMinima); sensUI.maxValue = Mathf.Max(sensUI.minValue + .001f, sensibilidadeMaxima);
        escurecimento.color = corEscurecimento;
        if (!Application.isPlaying)
        {
            raiz.gameObject.SetActive(previaNoEditor); grupo.alpha = 1;
            pagina = previaConfiguracoes ? 1 : 0;
            foreach (var entrada in entradasMenu) { entrada.localScale = Vector3.one; entrada.GetComponent<CanvasGroup>().alpha = 1; }
            imagemBlur.gameObject.SetActive(false);
        }
        PosicionarPaginas();
    }
    private void PosicionarPaginas()
    {
        float distancia = tamanhoPainel.x + 60;
        paginaMenu.anchoredPosition = new Vector2(-pagina * distancia, 0);
        paginaConfig.anchoredPosition = new Vector2((1 - pagina) * distancia, 0);
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
            slider.handleRect.sizeDelta = tamanhoAlcaSlider;
        }
        foreach (var t in raiz.GetComponentsInChildren<TMP_Text>(true))
        {
            if (fonte != null) t.font = fonte;
            t.color = t == mensagemUI ? new Color(1, .65f, .4f) : corTexto;
            t.fontSize = t == tituloMenuUI || t == tituloConfigUI ? tamanhoTitulo : t == mensagemUI ? 18 : tamanhoTexto;
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
            foreach (var par in materiais) if (t.fontSharedMaterial == par.Value) { t.fontSharedMaterial = par.Key; break; }
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
