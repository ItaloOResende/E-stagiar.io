using System.Collections.Generic;
using EstagiarIo.Rede;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Interface do tablet. Mantém a API pública e as integrações do protótipo.
/// Gere a interface no Inspector para personalizar os objetos sem entrar em Play.
/// Não altera Time.timeScale nem as regras dos chamados.
/// A aba APLICATIVOS traz o guia do antigo celular (peças, cabos, ferramentas, impressora, notebook, rede), com ícones vetoriais de TabletIcones.</summary>
[DisallowMultipleComponent]
public class TicketTabletPrototype : MonoBehaviour
{
    [Header("Integrações existentes")]
    [SerializeField] private TicketManager ticketManager;
    [SerializeField] private StageController stageController;
    [SerializeField] private InteractionDetector interactionDetector;
    [SerializeField] private Behaviour[] disableWhileTabletOpen;
    [SerializeField] private Key toggleKey = Key.T;
    [SerializeField] private Key freeCursorKey = Key.LeftAlt;

    [Header("Tablet / prévia")]
    [Tooltip("Abre ao iniciar o jogo e mostra o tablet na Scene View, mesmo fora do Play.")]
    [SerializeField] private bool comecarAberto;
    [SerializeField] private bool previaNoEditor = true;
    [SerializeField] private Vector2 tamanhoTablet = new Vector2(1000, 720);
    [SerializeField, Range(.3f, 2f)] private float escalaTablet = 1;
    [SerializeField] private Vector2 posicaoTablet;
    [SerializeField, Min(0)] private float margem = 24;
    [SerializeField] private Vector2 tamanhoAtalho = new Vector2(170, 54);
    [SerializeField, Min(.05f)] private float duracaoAbertura = .3f;
    [SerializeField, Range(0, .15f)] private float bounceAbertura = .035f;
    [SerializeField, Range(0, 180)] private float deslocamentoEntrada = 45;

    [Header("Arte (aplicada ao gerar / atualizar aparência)")]
    [SerializeField] private TMP_FontAsset fonte;
    [Header("Carcaça e tela do tablet")]
    [Tooltip("Sprite completo da carcaça com a tela. A escala abaixo afeta somente essa arte.")]
    [SerializeField] private Sprite fundoSprite;
    [SerializeField] private Vector2 escalaFundoTablet = Vector2.one;
    [SerializeField] private Vector2 deslocamentoFundoTablet;
    [SerializeField] private bool preservarProporcaoFundo = true;
    [SerializeField] private Color corSpriteTablet = Color.white;
    [SerializeField] private Color corFundo = new Color(.08f, .12f, .18f, 1);
    [Header("Chamados e painéis") ]
    [Tooltip("Cor de fundo de todos os chamados, incluindo a prévia e os cards já criados.")]
    [SerializeField] private Color corCard = new Color(.13f, .19f, .26f, 1);
    [SerializeField] private Color corBotao = new Color(.2f, .29f, .38f, 1);
    [SerializeField] private Color corDestaque = new Color(.25f, .85f, .77f, 1);
    [Tooltip("Azul da bolinha atrás do número de chamados no botão do tablet.")]
    [SerializeField] private Color corSeloFundo = new Color(.24f, .55f, .96f, 1);   // azul
    [SerializeField] private Color corTexto = new Color(.94f, .97f, 1, 1);
    [SerializeField] private Color corAlerta = new Color(1, .55f, .3f, 1);
    [SerializeField, Range(0, .8f)] private float escurecerFundo = .2f;
    [SerializeField, Range(1, 1.2f)] private float escalaHover = 1.04f;
    [SerializeField, Min(1)] private float velocidadeHover = 16;

    [SerializeField, Range(0, 48)] private float raioPaineis = 16;
    [Header("Textos: outline e sombra projetada (TMP SDF)")]
    [SerializeField] private Color corOutlineTexto = new Color(.02f, .035f, .06f, 1);
    [SerializeField, Range(0, .4f)] private float larguraOutlineTexto = .13f;
    [SerializeField] private Color corSombraTexto = new Color(0, 0, 0, .65f);
    [Tooltip("Unidades do shader TMP: X positivo para direita, Y negativo para baixo.")]
    [SerializeField] private Vector2 deslocamentoSombraTexto = new Vector2(.5f, -.5f);
    [SerializeField, Range(0, 1)] private float suavidadeSombraTexto = .25f;
    [SerializeField, Range(-1, 1)] private float expandirSombraTexto = .05f;

    [Header("Botões: bordas, gradiente e glow")]
    [SerializeField, Range(0, 40)] private float raioBotoes = 12;
    [SerializeField] private Color gradienteTopo = new Color(.32f, .46f, .58f, 1);
    [SerializeField] private Color gradienteBase = new Color(.14f, .23f, .32f, 1);
    [SerializeField] private Color corInnerGlow = new Color(.65f, .92f, 1, .75f);
    [SerializeField, Range(0, 20)] private float larguraInnerGlow = 5;
    [SerializeField, Range(0, 1)] private float intensidadeInnerGlow = .45f;
    [SerializeField] private Color corOuterGlow = new Color(.2f, .8f, 1, .65f);
    [SerializeField, Range(0, 40)] private float larguraOuterGlow = 12;
    [SerializeField, Range(0, 1)] private float intensidadeOuterGlow = .4f;

    [System.Serializable]
    private class BordaESombra
    {
        public bool outlineAtivo;
        public Color corOutline = new Color(.02f, .04f, .07f, 1);
        [Range(0, 8)] public float espessuraOutline = 2;
        public bool sombraAtiva;
        public Color corSombra = new Color(0, 0, 0, .45f);
        public Vector2 deslocamentoSombra = new Vector2(4, -5);
        public int Hash()
        {
            unchecked
            {
                int h = outlineAtivo.GetHashCode(); h = h * 31 + corOutline.GetHashCode();
                h = h * 31 + espessuraOutline.GetHashCode(); h = h * 31 + sombraAtiva.GetHashCode();
                h = h * 31 + corSombra.GetHashCode(); return h * 31 + deslocamentoSombra.GetHashCode();
            }
        }
    }
    [Header("Cards: outline e sombra opcionais")]
    [SerializeField] private BordaESombra efeitosCards = new BordaESombra();
    [Header("Botões: outline e sombra opcionais")]
    [SerializeField] private BordaESombra efeitosBotoes = new BordaESombra();

    [Header("Cursor: ponta acompanha o mouse; corpo tem spring")]
    [SerializeField] private Sprite cursorSprite;
    [SerializeField] private Vector2 tamanhoCursor = new Vector2(46, 54);
    [Tooltip("Posição da ponta dentro do sprite (0..1), origem embaixo à esquerda.")]
    [SerializeField] private Vector2 pontaCursor = new Vector2(.1f, .9f);
    [SerializeField, Min(1)] private float forcaSpring = 160;
    [SerializeField, Min(1)] private float amortecimentoSpring = 20;
    [SerializeField, Range(0, .1f)] private float sensibilidadeCursor = .016f;
    [SerializeField, Range(0, 40)] private float inclinacaoMaxima = 18;
    [SerializeField, Range(0, .4f)] private float compressaoClique = .15f;
    [Header("Clique: três círculos")]
    [SerializeField] private Sprite circuloSprite;
    [SerializeField] private Color corClique = new Color(.35f, 1, .85f, .8f);
    [SerializeField, Min(.1f)] private float duracaoClique = .45f;
    [SerializeField, Min(0)] private float intervaloCirculos = .07f;
    [SerializeField, Min(4)] private float diametroClique = 90;

    [Header("Notificações e sons")]
    [SerializeField] private bool avisarChamadoNovo = true;
    [SerializeField, Min(.5f)] private float avisoSegundos = 5;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip somAbrir, somFechar, somClique, somNovoChamado, somSucesso, somRecusa;

    [Header("Referências geradas (editáveis na hierarquia)")]
    [SerializeField] private RectTransform raizUI, painel, bloqueio, conteudo, cursorVisual, efeitos;
    [SerializeField] private Canvas canvas;
    [SerializeField] private CanvasGroup grupo;
    [SerializeField] private ScrollRect scroll;
    [SerializeField] private Button atalho, fechar, abaPendentes, abaAtivos, abaHistorico, abaCores, abaAplicativos;
    [SerializeField] private TMP_Text selo, aviso, proximo, titulo;
    [SerializeField] private RectTransform seloFundo;   // bolinha atrás do número de chamados
    [SerializeField] private RectTransform modeloCard;

    public static bool PonteiroSobreOAparelho { get; private set; }
    public static bool CelularAberto { get; private set; }
    public static bool CursorLiberado { get; private set; }
    public bool IsOpen => aberto;
    public void Abrir() { if (Application.isPlaying && !Oculto) SetAberto(true); }
    // Fechar também devolve controles retidos sem o tablet aberto (Alt solta o cursor), p.ex. quando a pausa fecha o tablet.
    public void Fechar() { if (!Application.isPlaying) return; SetAberto(false); RestaurarControles(false); }

    private bool aberto, controlesRetidos, cursorNosso, iniciou, capturaNoMundo;
    private int aba;   // 0 pendentes, 1 em atendimento, 2 histórico, 3 cores (só no minigame de rede), 4 aplicativos
    private int telaApps, appSelecionado, itemSelecionado;   // aplicativos: 0 = início, 1 = itens do app, 2 = detalhe do item
    private readonly Dictionary<string, Sprite> iconesApps = new Dictionary<string, Sprite>();
    private readonly Dictionary<string, Sprite> fotosApps = new Dictionary<string, Sprite>();
    private float animacao, angulo, velocidadeAngular, clickSquash;
    private Vector2 ultimoMouse;
    private bool mouseInicializado;
    private readonly Dictionary<Behaviour, bool> controlesAnteriores = new Dictionary<Behaviour, bool>();
    private readonly List<Card> cards = new List<Card>();
    private readonly List<Hover> hovers = new List<Hover>();
    private readonly List<Impacto> impactos = new List<Impacto>();
    private readonly Queue<string> avisos = new Queue<string>();
    private readonly HashSet<int> conhecidos = new HashSet<int>();
    private string assinatura = "";
    private float avisoAte, recusaAte;
    private int recusaNumero = -1;
    private Texture2D texturaCirculo;
    private Sprite circuloGerado;
    private Sprite circuloCheioGerado;   // bolinha cheia atrás do número de chamados do tablet
    [Header("Foto do solicitante")]
    [SerializeField] private bool mostrarFotos = true;
    [SerializeField, Min(24f)] private float tamanhoFoto = 72f;
    [SerializeField] private Color corFoto = Color.white;
    private class Card { public Image foto; public TicketInstance ticket; public TMP_Text tempo, recusa; public Button acao; public RectTransform barra; }
    private class Hover { public Button button; public Vector3 baseScale; public float valor = 1; }
    private class Impacto { public RectTransform rect; public Image image; public float inicio; }
    private static bool Oculto => MainMenuScreen.Showing || StageReportScreen.Showing || PauseMenu.IsPaused;
    private static bool NoMundo3D() => UIScreenManager.Active == null || !UIScreenManager.Active.IsOpen;
    private static bool TemOrdemDeCores => RedeCrimpScreen.Aberta;

    private void Awake()
    {
        if (ticketManager == null) ticketManager = GetComponent<TicketManager>();
        if (stageController == null) stageController = GetComponent<StageController>();
        if (audioSource == null && !TryGetComponent(out audioSource))
        { audioSource = gameObject.AddComponent<AudioSource>(); audioSource.playOnAwake = false; audioSource.spatialBlend = 0f; }
        if (raizUI == null) GerarInterface();
        GarantirAbaAplicativos();
        Vincular();
        // interface gerada no Inspector guarda o texto do atalho antigo: acompanha a tecla atual
        if (atalho != null)
            foreach (TMP_Text tx in atalho.GetComponentsInChildren<TMP_Text>(true))
                if (tx.text.StartsWith("TABLET [")) tx.text = "TABLET [" + toggleKey + "]";
        iniciou = true;
        painel.gameObject.SetActive(false);
        grupo.alpha = 0;
        AtualizarGeometria();
    }
    private void Start()
    {
        if (UnityEngine.EventSystems.EventSystem.current == null)
            Debug.LogWarning("Tablet: configure um EventSystem com InputSystemUIInputModule na cena para os botões receberem cliques.", this);
        if (TMP_Settings.defaultFontAsset == null && fonte == null)
            Debug.LogWarning("Tablet: importe TMP Essential Resources ou atribua uma fonte TMP no Inspector.", this);
        if (comecarAberto && !Oculto) SetAberto(true);
    }
    private void OnEnable() { if (iniciou && raizUI != null) raizUI.gameObject.SetActive(true); }
    private void OnDisable()
    {
        if (!Application.isPlaying) { LimparEstilosBotoes(); LimparVisuais(); estiloInicializado = false; return; }
        aberto = false; CelularAberto = false; CursorLiberado = false; PonteiroSobreOAparelho = false;
        RestaurarControles(false); LiberarCursor(); AplicarCursorDoJogo();
        if (raizUI != null) raizUI.gameObject.SetActive(false);
    }
    private void OnDestroy()
    {
        LimparEstilosBotoes(); LimparVisuais(); LimparIconesApps();
        if (circuloGerado != null) Destroy(circuloGerado);
        if (texturaCirculo != null) Destroy(texturaCirculo);
    }
    private void Update()
    {
        if (raizUI == null) return;
        bool oculto = Oculto;
        if (oculto && aberto) SetAberto(false);
        raizUI.gameObject.SetActive(!oculto);
        if (oculto)
        {
            CursorLiberado = false; PonteiroSobreOAparelho = false;
            RestaurarControles(false); LiberarCursor(); return;
        }
        var teclado = Keyboard.current;
        if (teclado != null && toggleKey != Key.None && teclado[toggleKey].wasPressedThisFrame) SetAberto(!aberto);
        bool alt = !aberto && NoMundo3D() && teclado != null && freeCursorKey != Key.None && teclado[freeCursorKey].isPressed;
        bool mudouAlt = CursorLiberado != alt;
        CursorLiberado = alt;
        if (aberto || alt) ReterControles(); else RestaurarControles(false);
        if (aberto || alt) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        else if (mudouAlt) AplicarCursorDoJogo();
        AtualizarGeometria();
        animacao = Mathf.MoveTowards(animacao, aberto ? 1 : 0, Time.unscaledDeltaTime / Mathf.Max(.05f, duracaoAbertura));
        float t = animacao;
        float ease = 1 - Mathf.Pow(1 - t, 3);
        grupo.alpha = ease;
        painel.localScale = Vector3.one * EscalaEfetiva() * (.94f + .06f * ease + Mathf.Sin(t * Mathf.PI) * bounceAbertura);
        painel.anchoredPosition = posicaoTablet + Vector2.down * deslocamentoEntrada * (1 - ease);
        painel.gameObject.SetActive(aberto || animacao > 0);
        grupo.interactable = aberto;
        grupo.blocksRaycasts = aberto;
        bloqueio.gameObject.SetActive(aberto && NoMundo3D());
        atalho.gameObject.SetActive(!aberto);
        if (!TemOrdemDeCores && aba == 3) { aba = 0; assinatura = ""; }
        abaCores.gameObject.SetActive(TemOrdemDeCores);
        MarcarAbas();
        VerificarNovos();
        AtualizarConteudo();
        AtualizarCards();
        bool temSelo = ticketManager != null && ticketManager.OpenCount > 0;
        selo.text = temSelo ? ticketManager.OpenCount.ToString() : "";
        if (seloFundo != null && seloFundo.gameObject.activeSelf != temSelo) seloFundo.gameObject.SetActive(temSelo);
        // a chegada dos chamados é surpresa: o agendamento (TicketManager.SecondsUntilNextArrival) continua, só não aparece
        if (proximo != null && proximo.gameObject.activeSelf) { proximo.text = ""; proximo.gameObject.SetActive(false); }
        EstilizarAviso();
        if (Time.unscaledTime >= avisoAte && avisos.Count > 0)
        { aviso.text = avisos.Dequeue(); avisoAte = Time.unscaledTime + avisoSegundos; Tocar(somNovoChamado); }
        // com o tablet aberto o chamado já está na lista: a notificação não fica por cima da arte do aparelho
        aviso.transform.parent.gameObject.SetActive(Time.unscaledTime < avisoAte && !aberto);
        Vector2 mouse = Mouse.current != null ? Mouse.current.position.ReadValue() : new Vector2(-10000, -10000);
        PonteiroSobreOAparelho = aberto ? Dentro(painel, mouse) : ((!NoMundo3D() || alt) && Dentro((RectTransform)atalho.transform, mouse));
        AtualizarHover(mouse);
        if (aberto && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && (NoMundo3D() || PonteiroSobreOAparelho))
        { CriarImpacto(mouse); clickSquash = compressaoClique; Tocar(somClique); }
        AtualizarImpactos();
    }
    private void LateUpdate()
    {
        if (raizUI == null || Oculto) return;
        AtualizarEstilosBotoes(); AtualizarVisuais();
        MarcarAbas();
        bool usar = Mouse.current != null && (aberto && NoMundo3D() || PonteiroSobreOAparelho || CursorLiberado);
        if (!usar) { LiberarCursor(); return; }
        cursorNosso = true;
        // Reutiliza o mecanismo de sobreposição existente quando não há sprite próprio.
        if (cursorSprite == null)
        {
            cursorVisual.gameObject.SetActive(false);
            var telas = UIScreenManager.Active;
            if (telas != null && telas.CursorMao != null) UICursor.Sobrepor(telas.CursorMao, telas.CursorMaoPonta, telas.CursorMaoAltura);
            Cursor.visible = true; return;
        }
        UICursor.LimparSobreposicao();
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.None;
        cursorVisual.gameObject.SetActive(true);
        cursorVisual.GetComponent<Image>().sprite = cursorSprite;
        cursorVisual.sizeDelta = tamanhoCursor;
        cursorVisual.pivot = pontaCursor;
        Vector2 mouse = Mouse.current.position.ReadValue();
        RectTransformUtility.ScreenPointToLocalPointInRectangle(raizUI, mouse, null, out Vector2 local);
        cursorVisual.anchoredPosition = local;
        float dt = Mathf.Min(Time.unscaledDeltaTime, .05f);
        Vector2 velocidade = mouseInicializado ? (mouse - ultimoMouse) / Mathf.Max(.001f, dt) : Vector2.zero;
        ultimoMouse = mouse; mouseInicializado = true;
        float alvo = Mathf.Clamp(-velocidade.x * sensibilidadeCursor, -inclinacaoMaxima, inclinacaoMaxima);
        int passos = Mathf.Max(1, Mathf.CeilToInt(dt / .008f));
        float h = dt / passos;
        for (int i = 0; i < passos; i++)
        { velocidadeAngular += ((alvo - angulo) * forcaSpring - velocidadeAngular * amortecimentoSpring) * h; angulo += velocidadeAngular * h; }
        cursorVisual.localRotation = Quaternion.Euler(0, 0, angulo);
        clickSquash = Mathf.Lerp(clickSquash, 0, 1 - Mathf.Exp(-16 * dt));
        cursorVisual.localScale = new Vector3(1 + clickSquash, 1 - clickSquash, 1);
    }
    private void SetAberto(bool valor)
    {
        if (aberto == valor) return;
        aberto = valor; CelularAberto = valor;
        if (valor)
        {
            aba = TemOrdemDeCores ? 3 : aba == 3 ? 0 : aba;
            assinatura = ""; mouseInicializado = false;
            ReterControles(); Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            painel.gameObject.SetActive(true);
            if (somAbrir != null) Tocar(somAbrir); else InteractionSounds.Raise(InteractionSound.DrawerOpened);
        }
        else
        {
            CursorLiberado = false; RestaurarControles(false); LiberarCursor(); AplicarCursorDoJogo();
            if (somFechar != null) Tocar(somFechar); else InteractionSounds.Raise(InteractionSound.DrawerClosed);
        }
    }
    private void ReterControles()
    {
        if (!controlesRetidos)
        {
            controlesAnteriores.Clear(); Memorizar(interactionDetector);
            if (disableWhileTabletOpen != null) foreach (var c in disableWhileTabletOpen) Memorizar(c);
            controlesRetidos = true; capturaNoMundo = NoMundo3D();
        }
        // Se uma tela 2D fechou, registra os controles que ela acaba de devolver.
        if (!capturaNoMundo && NoMundo3D())
        {
            var chaves = new List<Behaviour>(controlesAnteriores.Keys);
            foreach (var c in chaves) if (c != null && c.enabled) controlesAnteriores[c] = true;
            capturaNoMundo = true;
        }
        foreach (var par in controlesAnteriores) if (par.Key != null) par.Key.enabled = false;
    }
    private void Memorizar(Behaviour c) { if (c != null && c != this && !controlesAnteriores.ContainsKey(c)) controlesAnteriores.Add(c, c.enabled); }
    private void RestaurarControles(bool forcar)
    {
        if (!controlesRetidos) return;
        // Uma tela externa assume os controles ao fechar o tablet nesse contexto. Mesmo aqui o DETECTOR de interação
        // precisa voltar: ele é desligado só pelo tablet e nenhuma tela 2D o religa (não está em disableWhileOpen).
        // Sem isso, fechar o tablet com uma tela 2D ainda aberta (ex.: crimpagem) deixava o detector desligado para
        // sempre e o jogador não conseguia mais soltar o item da mão.
        if (!forcar && (!NoMundo3D() || Oculto))
        {
            if (interactionDetector != null && interactionDetector.enabled == false && !aberto) interactionDetector.enabled = true;
            controlesAnteriores.Clear(); controlesRetidos = false; return;
        }
        // Só RELIGA o que estava ligado quando o tablet o desligou: nunca desliga de volta. Um controle memorizado como
        // desligado tinha outro dono (a tela 2D); "restaurar" esse valor deixava o jogador travado sem andar, interagir
        // ou soltar o item até sair do Play Mode.
        foreach (var par in controlesAnteriores) if (par.Key != null && par.Value) par.Key.enabled = true;
        if (interactionDetector != null) interactionDetector.enabled = true;   // rede de segurança: o detector nunca fica preso
        controlesAnteriores.Clear(); controlesRetidos = false;
        // A tela 2D fechada com o tablet aberto deixou os controles para o tablet devolver (UIScreenManager).
        if (UIScreenManager.Active != null) UIScreenManager.Active.DevolverControlesDoJogador();
    }
    private void AplicarCursorDoJogo()
    {
        if (Oculto) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        else if (UIScreenManager.Active != null) UIScreenManager.Active.ApplyCursorForCurrentState();
        else { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
    }
    private void LiberarCursor()
    {
        if (cursorVisual != null) cursorVisual.gameObject.SetActive(false);
        if (cursorNosso) { UICursor.LimparSobreposicao(); Cursor.visible = true; cursorNosso = false; if (!aberto && !CursorLiberado) AplicarCursorDoJogo(); }
        mouseInicializado = false;
    }
    private void Tocar(AudioClip clip) { if (clip != null && audioSource != null) audioSource.PlayOneShot(clip); }
    private bool Dentro(RectTransform r, Vector2 p) => r != null && r.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(r, p, null);
    private float EscalaEfetiva()
    {
        if (raizUI == null) return escalaTablet;
        Vector2 disponivel = raizUI.rect.size - Vector2.one * margem * 2 - new Vector2(Mathf.Abs(posicaoTablet.x), Mathf.Abs(posicaoTablet.y)) * 2;
        return Mathf.Max(.05f, Mathf.Min(escalaTablet, disponivel.x / Mathf.Max(1, tamanhoTablet.x), disponivel.y / Mathf.Max(1, tamanhoTablet.y)));
    }
    public void AtualizarGeometria()
    {
        if (painel == null)
        {
            if (!Application.isPlaying && (comecarAberto || previaNoEditor)) GerarInterface();
            if (painel == null) return;
        }
        painel.sizeDelta = tamanhoTablet;
        if (!Application.isPlaying)
        {
            painel.localScale = Vector3.one * escalaTablet; painel.anchoredPosition = posicaoTablet;
            bool mostrarPrevia = comecarAberto || previaNoEditor;
            raizUI.gameObject.SetActive(true);
            painel.gameObject.SetActive(mostrarPrevia); grupo.alpha = 1;
            atalho.gameObject.SetActive(!mostrarPrevia); bloqueio.gameObject.SetActive(false);
        }
        // Botão no canto inferior direito, dentro da área reservada a ele (RedeUi): as setas "►" das telas de
        // manutenção (PC, notebook, impressora) ficam à esquerda dessa área, lado a lado, sem sobreposição.
        // Subido para não sobrepor o cursor/tablet com a seta "►" dos minigames nem com a notificação.
        var rtAtalho = (RectTransform)atalho.transform;
        rtAtalho.anchorMin = rtAtalho.anchorMax = rtAtalho.pivot = new Vector2(1, 0);
        rtAtalho.sizeDelta = Vector2.Min(tamanhoAtalho, new Vector2(RedeUi.CelularLargura, RedeUi.CelularAltura));
        rtAtalho.anchoredPosition = new Vector2(-RedeUi.CelularMargem, RedeUi.CelularMargem + 20f);
        // Notificação de chamado novo: canto INFERIOR DIREITO, um pouco acima do botão do tablet. O botão fica em
        // (1,0) com margem; a notificação sobe acima dele, alinhada à direita. Subimos um pouco para não sobrepor a
        // seta para a direita dos minigames.
        if (aviso != null)
        {
            var toast = (RectTransform)aviso.transform.parent;
            toast.anchorMin = toast.anchorMax = toast.pivot = new Vector2(1, 0);
            float alturaAtalho = rtAtalho.sizeDelta.y;
            toast.anchoredPosition = new Vector2(-RedeUi.CelularMargem, RedeUi.CelularMargem + 20f + alturaAtalho + 12f);
        }
        bloqueio.GetComponent<Image>().color = new Color(0, 0, 0, escurecerFundo);
        if (!Application.isPlaying) { AtualizarEstilosBotoes(); AtualizarVisuais(); }
    }
    private void Vincular()
    {
        Ligar(atalho, Abrir); Ligar(fechar, Fechar);
        Ligar(abaPendentes, () => TrocarAba(0)); Ligar(abaAtivos, () => TrocarAba(1));
        Ligar(abaHistorico, () => TrocarAba(2)); Ligar(abaCores, () => TrocarAba(3));
        if (abaAplicativos != null) Ligar(abaAplicativos, () => TrocarAba(4));
        AjustarRotulosDasAbas();
    }
    private void Ligar(Button b, UnityEngine.Events.UnityAction acao)
    { b.onClick.AddListener(acao); hovers.Add(new Hover { button = b, baseScale = b.transform.localScale }); }
    private void MarcarAbas()
    {
        MarcarAba(abaPendentes, aba == 0); MarcarAba(abaAtivos, aba == 1);
        MarcarAba(abaHistorico, aba == 2); MarcarAba(abaCores, aba == 3); MarcarAba(abaAplicativos, aba == 4);
    }
    private void MarcarAba(Button b, bool selecionada)
    { if (b == null) return; b.image.color = selecionada ? corDestaque : Color.white; b.GetComponentInChildren<TMP_Text>().color = selecionada ? corFundo : corTexto; }
    private void TrocarAba(int valor) { aba = valor; if (valor == 4) telaApps = 0; assinatura = ""; scroll.verticalNormalizedPosition = 1; }
    private void AtualizarHover(Vector2 mouse)
    {
        for (int i = hovers.Count - 1; i >= 0; i--)
        {
            var v = hovers[i]; if (v.button == null) { if (!ReferenceEquals(v.button, null)) tamanhosEstilizados.Remove(v.button); hovers.RemoveAt(i); continue; }
            bool sobre = v.button.IsInteractable() && Dentro((RectTransform)v.button.transform, mouse);
            v.valor = Mathf.Lerp(v.valor, sobre ? escalaHover : 1, 1 - Mathf.Exp(-velocidadeHover * Time.unscaledDeltaTime));
            v.button.transform.localScale = v.baseScale * v.valor;
        }
    }
    private void VerificarNovos()
    {
        if (ticketManager == null) return;
        var lista = ticketManager.Tickets;
        if (lista.Count == 0) { conhecidos.Clear(); avisos.Clear(); avisoAte = 0; return; }
        foreach (var c in lista)
            if (c != null && conhecidos.Add(c.Number) && !c.IsClosed && avisarChamadoNovo)
                avisos.Enqueue(TextoDoAviso(c));
    }
    // Notificação de chamado novo: rótulo pequeno na cor de destaque do tablet, número do chamado em evidência e o
    // título em negrito. Sem "Abrir tablet: T" (o botão do tablet já mostra a tecla).
    private string TextoDoAviso(TicketInstance c)
    {
        // um só destaque: o verde do selo de pontuação (HudEstilo.Rotulo); o título em branco
        string verde = ColorUtility.ToHtmlStringRGB(HudEstilo.Rotulo);
        string titulo = c.Definition != null ? c.Definition.title : "";
        return $"<size=68%><color=#{verde}><b>NOVO CHAMADO   #{c.Number}</b></color></size>\n<color=#FFFFFF>{titulo}</color>";
    }

    // Visual da notificação (uma vez, depois que a interface existe): fundo escuro do tablet com borda/sombra dos cards,
    // barra de destaque à esquerda e texto alinhado à esquerda. Mesmo lugar de antes (abaixo dos selos do HUD).
    private bool avisoEstilizado;
    private void EstilizarAviso()
    {
        if (avisoEstilizado || aviso == null) return;
        avisoEstilizado = true;
        var toast = (RectTransform)aviso.transform.parent;
        toast.sizeDelta = new Vector2(400f, 100f);
        var fundo = toast.GetComponent<Image>();
        if (fundo != null)
        {
            Color c = corFundo; c.a = 0.96f;
            fundo.color = c;
            AplicarBordaESombra(fundo, efeitosCards);
        }
        var barra = toast.Find("Destaque") as RectTransform;
        if (barra == null)
        {
            barra = new GameObject("Destaque", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            barra.SetParent(toast, false);
        }
        barra.anchorMin = new Vector2(0f, 0f); barra.anchorMax = new Vector2(0f, 1f); barra.pivot = new Vector2(0f, 0.5f);
        barra.offsetMin = new Vector2(10f, 14f); barra.offsetMax = new Vector2(16f, -14f);
        var img = barra.GetComponent<Image>(); img.color = HudEstilo.Borda; img.raycastTarget = false;
        aviso.rectTransform.offsetMin = new Vector2(30f, 12f); aviso.rectTransform.offsetMax = new Vector2(-16f, -12f);
        aviso.alignment = TextAlignmentOptions.Left;
        aviso.enableAutoSizing = true; aviso.fontSizeMin = 16f; aviso.fontSizeMax = 24f;
        aviso.color = corTexto; aviso.richText = true; aviso.lineSpacing = 4f;
        if (fonte != null) aviso.font = fonte;
    }

    private void AtualizarConteudo()
    {
        if (!aberto) return;
        var key = new System.Text.StringBuilder().Append(aba).Append('|');
        if (aba == 4) key.Append(telaApps).Append(':').Append(appSelecionado).Append(':').Append(itemSelecionado);
        else if (ticketManager != null) foreach (var c in ticketManager.Tickets) if (c != null) key.Append(c.Number).Append(':').Append(c.Status).Append(';');
        string nova = key.ToString(); if (nova == assinatura) return; assinatura = nova;
        float pos = scroll.verticalNormalizedPosition;
        foreach (Transform child in conteudo) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        cards.Clear();
        titulo.text = aba == 3 ? "REFERÊNCIA • T568B" : "CENTRAL DE CHAMADOS";
        if (aba == 3) { ConstruirCores(); return; }
        if (aba == 4) { ConstruirAplicativos(); Canvas.ForceUpdateCanvases(); scroll.verticalNormalizedPosition = 1; return; }
        if (ticketManager != null)
        {
            var lista = ticketManager.Tickets;
            for (int i = lista.Count - 1; i >= 0; i--)
            {
                var c = lista[i]; if (c == null || c.Definition == null) continue;
                bool mostrar = aba == 0 ? c.Status == TicketStatus.Pending : aba == 1 ? c.Status == TicketStatus.InProgress : c.IsClosed;
                if (mostrar) CriarCard(c);
            }
        }
        if (cards.Count == 0) Texto(conteudo, "Vazio", ticketManager == null ? "Sem central de chamados." : "Nenhum chamado nesta aba.", 23);
        Canvas.ForceUpdateCanvases(); scroll.verticalNormalizedPosition = pos;
    }
    private void CriarCard(TicketInstance c)
    {
        var root = Instantiate(modeloCard, conteudo); root.name = "Chamado_" + c.Number; root.gameObject.SetActive(true);
        root.Find("Titulo").GetComponent<TMP_Text>().text = "#" + c.Number + "  " + c.Definition.title;
        root.Find("Descricao").GetComponent<TMP_Text>().text = c.Definition.description;
        root.Find("Detalhes").GetComponent<TMP_Text>().text = c.Status == TicketStatus.InProgress
            ? c.Requester + " — " + c.Department + (string.IsNullOrEmpty(c.Location) ? "" : "\nLocal: " + c.Location + (string.IsNullOrEmpty(c.AssignedNpc) ? "" : " (com " + c.AssignedNpc + ")"))
            : c.IsClosed ? (c.Status == TicketStatus.Completed ? "Concluído" : "Encerrado") : "Pendente";
        var v = new Card { ticket = c, tempo = root.Find("Tempo").GetComponent<TMP_Text>(), recusa = root.Find("Feedback").GetComponent<TMP_Text>(), acao = root.Find("Acao").GetComponent<Button>(), barra = (RectTransform)root.Find("BarraPrazo") };
        v.recusa.gameObject.SetActive(false); v.acao.gameObject.SetActive(!c.IsClosed); v.barra.gameObject.SetActive(!c.IsClosed);
        Ligar(v.acao, () =>
        {
            if (ticketManager == null) return;
            if (c.Status == TicketStatus.Pending)
            {
                if (TicketDeviceBridge.CanStart(c)) ticketManager.StartTicket(c.Number);
                if (c.Status == TicketStatus.InProgress) { TrocarAba(1); Tocar(somSucesso); }
            }
            else if (c.Status == TicketStatus.InProgress)
            {
                if (!ticketManager.TryFinishTicket(c.Number))
                {
                    // Fica FIXO: o cartão mostra o porquê enquanto o chamado continuar pendente travado (não é aviso que some).
                    recusaNumero = c.Number; recusaAte = float.PositiveInfinity; Tocar(somRecusa);
                }
                else
                {
                    Tocar(somSucesso);
                    // Último atendimento finalizado: volta para a aba de PENDENTES (se não sobrou nenhum em atendimento).
                    assinatura = "";
                    bool aindaAtendendo = false;
                    foreach (TicketInstance t in ticketManager.Tickets) if (t.Status == TicketStatus.InProgress) { aindaAtendendo = true; break; }
                    if (!aindaAtendendo) TrocarAba(0);
                }
            }
            assinatura = "";
        });
        v.foto = GarantirFoto(root);
        AtualizarFoto(v);
        cards.Add(v); visuaisPendentes = true;
    }
    private Image GarantirFoto(RectTransform root)
    {
        var child = root.Find("FotoSolicitante");
        Image foto = child != null ? child.GetComponent<Image>() : null;
        if (foto == null)
        {
            var go = new GameObject("FotoSolicitante", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(root, false);
            go.transform.SetAsFirstSibling();
            foto = go.GetComponent<Image>();
        }
        foto.preserveAspect = true;
        foto.raycastTarget = false;
        return foto;
    }
    private void AtualizarFoto(Card card)
    {
        if (card.foto == null) return;
        card.foto.sprite = ticketManager != null ? ticketManager.FotoDoSolicitante(card.ticket.Requester) : null;
        card.foto.color = corFoto;
        card.foto.gameObject.SetActive(mostrarFotos && card.foto.sprite != null);
        var layout = card.foto.GetComponent<LayoutElement>();
        if (layout == null) layout = card.foto.gameObject.AddComponent<LayoutElement>();
        layout.minHeight = layout.preferredHeight = Mathf.Max(24f, tamanhoFoto);
        layout.minWidth = layout.preferredWidth = Mathf.Max(24f, tamanhoFoto);
        layout.flexibleWidth = layout.flexibleHeight = 0;
    }
    private void AtualizarCards()
    {
        if (!aberto) return;
        foreach (var v in cards)
        {
            AtualizarFoto(v);
            var c = v.ticket;
            // ocupados = todos os equipamentos desse tipo estão com outro chamado: libera quando um for finalizado
            int totalEquip = 0;
            bool livre = c.Status != TicketStatus.Pending
                || TicketDeviceBridge.Availability(c, out totalEquip, out _) != TicketDeviceBridge.StartBlock.AllBusy;
            v.acao.interactable = livre;
            v.acao.GetComponentInChildren<TMP_Text>().text = c.Status == TicketStatus.Pending
                ? livre ? "Iniciar atendimento" : $"Aguardando equipamento livre ({totalEquip} em atendimento)"
                : "Finalizar atendimento";
            float seg = Mathf.Max(0, c.IsOverdue ? c.OverdueSeconds : c.SecondsLeft);
            v.tempo.text = c.IsClosed ? "" : (c.IsOverdue ? "Atrasado há " : "Prazo: ") + Mathf.FloorToInt(seg / 60) + ":" + Mathf.FloorToInt(seg % 60).ToString("00");
            v.tempo.color = c.IsOverdue ? corAlerta : corTexto;
            // Recado FIXO: continua no cartão enquanto o atendimento não pode ser entregue. Reavalia o motivo a cada
            // quadro, então some sozinho assim que o jogador resolve o que faltava (ex.: fechou os parafusos).
            string bloqueio = c.Status == TicketStatus.InProgress ? TicketDeviceBridge.DeliveryBlockerFor(c) : null;
            bool rejeitado = recusaNumero == c.Number && (Time.unscaledTime < recusaAte || c.Status == TicketStatus.InProgress);
            if (rejeitado && !string.IsNullOrEmpty(bloqueio)) v.recusa.text = "Não dá para entregar: " + bloqueio + ".";
            else if (rejeitado && string.IsNullOrEmpty(c.LastRejectReason) == false) v.recusa.text = "Não dá para entregar: " + c.LastRejectReason + ".";
            else if (rejeitado && c.Status == TicketStatus.InProgress) v.recusa.text = "Ainda há etapas pendentes neste atendimento.";
            else { rejeitado = false; recusaNumero = recusaNumero == c.Number ? -1 : recusaNumero; }
            v.recusa.gameObject.SetActive(rejeitado);
        }
    }
    // Mantém o desenho original do prazo sem presumir campos novos de TicketInstance.
    // Toda a interface restante é uGUI/TMP; a barra só desenha no Repaint e respeita o viewport.
    private void OnGUI()
    {
        if (!Application.isPlaying || !aberto || Oculto || animacao < .99f || aba >= 3 || Event.current.type != EventType.Repaint) return;
        Rect clip = TelaRect(scroll.viewport);
        GUI.BeginGroup(clip);
        Color anterior = GUI.color; GUI.color = Color.white;
        foreach (var v in cards) if (!v.ticket.IsClosed && v.barra != null)
        { Rect r = TelaRect(v.barra); r.position -= clip.position; TicketTimeBar.Desenhar(r, v.ticket); }
        GUI.color = anterior; GUI.EndGroup();
    }
    private static Rect TelaRect(RectTransform rt)
    {
        Vector3[] pontos = new Vector3[4]; rt.GetWorldCorners(pontos);
        return new Rect(pontos[0].x, Screen.height - pontos[1].y, pontos[2].x - pontos[0].x, pontos[1].y - pontos[0].y);
    }
    private void ConstruirCores()
    {
        Texto(conteudo, "Instrucao", "PADRÃO T568B — ordem dos 8 fios", 26);
        for (int i = 0; i < RedeCores.OrdemT568B.Length; i++)
        {
            var fio = RedeCores.OrdemT568B[i];
            var linha = Retangulo("Fio_" + (i + 1), conteudo, corCard); Altura(linha, 55);
            var amostra = Retangulo("Amostra", linha, RedeCores.CorDoDesenho(fio));
            amostra.anchorMin = amostra.anchorMax = new Vector2(0, .5f); amostra.anchoredPosition = new Vector2(70, 0); amostra.sizeDelta = new Vector2(64, 28);
            if (RedeCores.EhListrado(fio)) for (int n = 0; n < 5; n++)
            { var faixa = Retangulo("Listra", amostra, Color.white); faixa.sizeDelta = new Vector2(6, 28); faixa.anchoredPosition = new Vector2(-26 + n * 13, 0); }
            var txt = Texto(linha, "Nome", (i + 1) + "     " + RedeCores.Nome(fio), 22); Esticar(txt.rectTransform, new Vector2(120, 0), new Vector2(-12, 0));
        }
    }
    // ====================================================== aba APLICATIVOS (vinda do antigo celular)
    // Início: grade de aplicativos, cada um com ícone vetorial (TabletIcones). Toque num app para ver os itens
    // e toque num item para ler o detalhe. O visual é o mesmo do resto do tablet: cards, botões em gradiente e TMP.
    private sealed class AppEntrada
    {
        public readonly string Nome, ImagemId, OQueE, Onde, Como, Atencao;
        public AppEntrada(string nome, string imagemId, string oQueE, string onde, string como, string atencao)
        { Nome = nome; ImagemId = imagemId; OQueE = oQueE; Onde = onde; Como = como; Atencao = atencao; }
    }
    private sealed class AppDef
    {
        public readonly string Nome, IconeId; public readonly AppEntrada[] Entradas;
        public AppDef(string nome, string iconeId, params AppEntrada[] entradas) { Nome = nome; IconeId = iconeId; Entradas = entradas; }
    }

    private static readonly AppDef[] Aplicativos = {
        new AppDef("Peças do computador", "pecas",
            new AppEntrada("Gabinete", "gabinete", "Estrutura que recebe os componentes do PC.", "Estoque/ponto de retirada de equipamentos.", "Transporte o gabinete até a bancada do chamado; use a interação do gabinete para soltar os parafusos e abrir a tampa.", "Não tente instalar peças com a tampa fechada."),
            new AppEntrada("Placa-mãe", "placa-mae", "Conecta os componentes do computador.", "Gaveta de componentes.", "Com o gabinete aberto, posicione a placa-mãe sobre os pontos de fixação do chassi. No minigame, escolha o encaixe da placa-mãe (a base grande onde serão ligados CPU, RAM, armazenamento e cabos); confirme a fixação antes das outras peças.", "Ela é a base para os demais componentes."),
            new AppEntrada("Fonte de alimentação", "fonte", "Converte e fornece energia ao PC.", "Gaveta de componentes.", "Coloque a fonte no compartimento de alimentação do gabinete, normalmente na parte traseira inferior ou superior conforme o modelo exibido. Depois ligue os conectores de energia da fonte aos pontos indicados na placa-mãe; isso é diferente do cabo externo da tomada.", "Instalar a fonte não substitui ligar os cabos de energia."),
            new AppEntrada("Memória RAM", "ram", "Memória temporária de trabalho.", "Gaveta de memória e armazenamento.", "Localize os slots longos de memória ao lado do processador na placa-mãe. Alinhe o recorte do módulo RAM com a guia do slot e encaixe-o na posição sinalizada pelo minigame. Não é um conector de cabo nem uma porta externa.", "Confira a orientação da peça antes de instalar."),
            new AppEntrada("Armazenamento HD / SSD", "ssd", "Guarda arquivos e o sistema operacional.", "Gaveta de memória e armazenamento.", "Confira qual disco o chamado exige. Um SSD/HD SATA fica na baia de armazenamento do gabinete e precisa de dados SATA até a placa-mãe e energia da fonte, se o jogo oferecer essas conexões. Um SSD M.2 entra direto no slot M.2 da placa-mãe, sem cabo SATA. Siga o modelo realmente exibido no minigame.", "Sem armazenamento adequado, a instalação do sistema não fica completa."),
            new AppEntrada("Cooler", "cooler", "Ajuda a resfriar o processador.", "Gaveta de componentes.", "O cooler do processador é fixado SOBRE o processador, na placa-mãe, usando o suporte indicado. Quando existir o conector CPU_FAN no minigame, ligue ali o cabo da ventoinha; não confunda com o cooler do gabinete.", "Verifique se a montagem permite fechar a tampa."),
            new AppEntrada("Monitor", "monitor", "Mostra a imagem do computador.", "Ponto de retirada de equipamentos.", "Posicione o monitor na mesa correta. Conecte o cabo de vídeo entre a ENTRADA HDMI/VGA do monitor e a SAÍDA de vídeo do PC; conecte separadamente a energia do monitor. A tela não recebe vídeo apenas por estar ligada na tomada.", "Sem cabo de vídeo, o monitor não recebe imagem."),
            new AppEntrada("Sistema operacional", "sistema", "Permite usar o computador após a montagem.", "Confira os dispositivos de instalação no estoque.", "Depois de montar e conectar o PC, use a interação de instalação de sistema oferecida pelo equipamento.", "Teste a inicialização até o sistema, não somente até a BIOS.")),
        new AppDef("Cabos e conexões", "cabos",
            new AppEntrada("Cabo de energia do PC", "energia", "Leva energia para o gabinete.", "Suprimentos e cabos.", "Uma ponta entra na tomada de alimentação da FONTE, atrás do gabinete; a outra vai à tomada/régua de energia da mesa. Esse cabo não entra na placa-mãe nem na porta de rede. Se houver etapa de ligação no minigame, conclua as duas extremidades.", "Confira as duas extremidades."),
            new AppEntrada("Cabo de energia do monitor", "energia-monitor", "Alimenta a tela.", "Suprimentos e cabos.", "Conecte uma ponta na entrada de energia atrás do monitor e a outra na tomada/régua. Não confunda com HDMI/VGA: a energia faz o monitor ligar, mas não transmite imagem.", "Monitor desligado não significa necessariamente PC com defeito."),
            new AppEntrada("Cabo HDMI", "hdmi", "Transporta imagem digital.", "Gaveteiro de cabos.", "Ligue uma ponta na porta HDMI de SAÍDA do computador (atrás do gabinete) e a outra na HDMI de ENTRADA do monitor. No minigame, selecione as duas portas compatíveis; HDMI transmite vídeo, não substitui a alimentação elétrica.", "Vídeo e energia são conexões distintas."),
            new AppEntrada("Cabo VGA", "vga", "Transporta imagem para monitores compatíveis.", "Gaveteiro de cabos.", "Ligue o conector VGA (15 pinos) à saída VGA do computador e o outro à entrada VGA do monitor. Use somente quando ambos mostrarem portas VGA. Caso o jogo exija apertar travas/parafusos do conector, conclua essa etapa.", "Use HDMI ou VGA de acordo com as portas da estação."),
            new AppEntrada("Cabo de rede RJ45", "rede", "Conecta a estação à rede.", "Gaveteiro de cabos.", "A primeira ponta RJ45 entra na porta Ethernet/LAN do PC, normalmente atrás do gabinete. A outra deve ir à tomada de rede da mesa ou à porta do switch/rack indicada no chamado. Se o cabo foi criado no minigame de crimpagem, finalize a crimpagem e o teste antes de conectá-lo. Não use uma porta USB nem HDMI.", "Uma rede sem conexão pode ser um problema de cabo ou de rack.")),
        new AppDef("Ferramentas", "ferramentas",
            new AppEntrada("Chave de fenda", "chave", "Permite remover e prender parafusos.", "Área de ferramentas.", "Pegue a ferramenta, interaja com os parafusos do gabinete e abra/feche a tampa conforme a mecânica apresentada.", "Verifique todos os parafusos antes de forçar a tampa."),
            new AppEntrada("Alicate de crimpar", "alicate", "Prende conectores de rede aos cabos.", "Gaveteiro de ferramentas.", "1) Pegue o cabo UTP e os conectores RJ45. 2) Abra o minigame de crimpagem. 3) Organize os oito fios na ordem solicitada (T568A ou T568B, conforme o jogo), insira no conector e crimpe. 4) Se houver testador, valide. 5) LEVE o cabo pronto e conecte-o entre a LAN do PC e o ponto de rede/switch solicitado: crimpar sozinho não instala a rede.", "Confira a ordem dos fios antes da crimpagem."),
            new AppEntrada("Pendrive", "pendrive", "Dispositivo removível para tarefas de manutenção.", "Gaveteiro de suprimentos.", "Pegue o pendrive e use a interação disponível no computador ou notebook para a tarefa solicitada.", "Para instalar sistema, observe as opções do equipamento."),
            new AppEntrada("Testador de cabo", "testador", "Verifica continuidade de um cabo de rede.", "Estação ou área de rede.", "Depois de crimpar, conecte as duas pontas RJ45 do cabo nas portas do testador (principal e remoto, se existirem). Rode o teste do minigame e confira o resultado. Se falhar, volte à crimpagem; só depois conecte o cabo no PC e no ponto de rede.", "Um cabo aparentemente inteiro pode falhar no teste.")),
        new AppDef("Impressora", "impressora",
            new AppEntrada("Impressora", "impressora", "Imprime documentos do escritório.", "Estação de impressão.", "Interaja com o equipamento para abrir a tela do minigame de impressora e identificar o problema do chamado.", "Leia o painel antes de substituir qualquer item."),
            new AppEntrada("Toner", "toner", "Suprimento de impressão.", "Gaveteiro de suprimentos.", "Abra o compartimento de consumíveis da impressora pelo minigame. Retire o cartucho antigo se a tarefa pedir e encaixe o toner no trilho/suporte próprio, nunca na bandeja de papel. Feche a tampa e teste a impressão.", "Nem toda falha de impressão é falta de toner."),
            new AppEntrada("Resma de papel", "resma", "Papel para impressão.", "Gaveteiro de suprimentos.", "Abra a BANDEJA DE PAPEL da impressora, alinhe as folhas e reabasteça a bandeja na quantidade pedida. Não coloque papel no compartimento do toner. Depois, use a opção de teste se o minigame apresentar.", "Confira primeiro se há papel no equipamento."),
            new AppEntrada("Configuração de rede", "impressora-rede", "Permite que a impressora seja acessada pela rede.", "Painel da impressora.", "Use a interface de diagnóstico/configuração do minigame e siga os indicadores disponíveis.", "Uma impressora ligada pode estar sem comunicação de rede.")),
        new AppDef("Notebook", "notebook",
            new AppEntrada("Notebook", "notebook", "Computador portátil com componentes internos próprios.", "Estação do chamado de notebook.", "Interaja com o notebook para abrir o minigame específico; suas peças não são instaladas como no gabinete de mesa.", "Observe o tipo de falha antes de desmontar."),
            new AppEntrada("RAM do notebook", "ram-notebook", "Memória de trabalho do notebook.", "Suprimentos para notebook.", "Abra o minigame de manutenção do notebook; o módulo SO-DIMM encaixa no slot de memória da placa do notebook, geralmente acessível por tampa inferior. Alinhe a ranhura e trave no suporte indicado; ele não vai na porta USB.", "Não confunda o encaixe do notebook com o do PC."),
            new AppEntrada("SSD do notebook", "ssd-notebook", "Armazenamento interno do notebook.", "Suprimentos para notebook.", "No minigame do notebook, procure a baia SATA de 2,5 polegadas OU o slot M.2, conforme a peça mostrada. Encaixe no alojamento correto e prenda pela fixação indicada antes de fechar a tampa e iniciar a instalação do sistema.", "Pode ser necessário instalar o sistema depois."),
            new AppEntrada("Sistema do notebook", "sistema-notebook", "Software necessário para inicialização.", "Ferramentas e dispositivos de instalação.", "Use a opção de instalação do próprio minigame do notebook após preparar o armazenamento.", "Confira a inicialização completa no final.")),
        new AppDef("Rede e rack", "rede",
            new AppEntrada("Rack de rede", "rack", "Centraliza conexões da rede do escritório.", "Área de infraestrutura de rede.", "No rack, identifique o SWITCH (portas Ethernet ativas) e, se presente, o PATCH PANEL (portas ligadas às tomadas das mesas). Conecte o RJ45 à porta indicada pelo chamado. Um cabo de rede do PC pode terminar em uma tomada da mesa; a ligação desta tomada ao switch pode exigir um patch cord dentro do rack.", "Identifique o ponto solicitado antes de trocar cabos."),
            new AppEntrada("Crimpagem RJ45", "crimpagem", "Preparação de um cabo de rede com conector.", "Estação de crimpagem.", "Prepare o cabo de rede no minigame: desencape a ponta, ordene os oito condutores no padrão mostrado, encaixe no plugue RJ45 e acione o alicate. Confira no testador, se disponível. Em seguida, use o cabo montado na porta LAN do computador e no ponto de rede/switch do chamado.", "Use o testador depois de montar o cabo."),
            new AppEntrada("Teste de conectividade", "conectividade", "Confirma se o caminho da rede está funcional.", "Ponto de rede, rack ou testador.", "Confira as DUAS pontas do cabo (PC na porta LAN e outra ponta no ponto de rede/switch). Se o jogo oferecer testador ou minigame de conectividade, execute-o depois da instalação e observe o resultado. Apenas crimpar ou apenas conectar uma ponta não basta.", "Concluir a etapa exige verificar o resultado, não só encaixar um cabo."))
    };

    // Ícone vetorial do aplicativo (gerado uma vez e guardado) e imagem opcional do item (Resources/TutorialIcons/<id>).
    private Sprite IconeApp(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (!iconesApps.TryGetValue(id, out Sprite sprite) || sprite == null)
        { sprite = TabletIcones.Criar(id); iconesApps[id] = sprite; }
        return sprite;
    }
    private Sprite FotoApp(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (!fotosApps.TryGetValue(id, out Sprite sprite))
        {
            Texture2D textura = Resources.Load<Texture2D>("TutorialIcons/" + id);
            sprite = textura != null ? Sprite.Create(textura, new Rect(0, 0, textura.width, textura.height), Vector2.one * .5f) : null;
            fotosApps[id] = sprite;
        }
        return sprite;
    }
    private void LimparIconesApps()
    {
        foreach (var par in iconesApps) if (par.Value != null) { DestruirVisual(par.Value.texture); DestruirVisual(par.Value); }
        iconesApps.Clear();
        foreach (var par in fotosApps) if (par.Value != null) DestruirVisual(par.Value); // só o sprite: a textura é do Resources
        fotosApps.Clear();
    }

    // A aba pode não existir numa interface gerada antes desta versão: cria sem mexer no resto da hierarquia.
    private void GarantirAbaAplicativos()
    {
        if (abaAplicativos != null || painel == null) return;
        Transform tabs = abaPendentes != null ? abaPendentes.transform.parent : painel.Find("Abas");
        if (tabs == null) return;
        Transform existente = tabs.Find("Aplicativos");
        if (existente != null) abaAplicativos = existente.GetComponent<Button>();
        if (abaAplicativos == null) abaAplicativos = Botao(tabs, "Aplicativos", "APLICATIVOS");
    }
    // Com 5 abas (CORES aparece no minigame de rede) "EM ATENDIMENTO" não cabe numa linha: sem quebra e com auto-ajuste.
    private void AjustarRotulosDasAbas()
    {
        foreach (var b in new[] { abaPendentes, abaAtivos, abaHistorico, abaCores, abaAplicativos })
        {
            if (b == null) continue;
            TMP_Text tx = b.GetComponentInChildren<TMP_Text>(true); if (tx == null) continue;
            string texto = tx.text.Replace("<nobr>", "").Replace("</nobr>", "");
            tx.richText = true; tx.text = "<nobr>" + texto + "</nobr>";
            if (!tx.enableAutoSizing) { tx.fontSizeMax = tx.fontSize; tx.fontSizeMin = Mathf.Min(12f, tx.fontSize); tx.enableAutoSizing = true; }
        }
    }

    private void IrParaApp(int app) { appSelecionado = app; telaApps = 1; NavegouNosApps(); }
    private void IrParaItem(int item) { itemSelecionado = item; telaApps = 2; NavegouNosApps(); }
    private void VoltarApps() { telaApps = Mathf.Max(0, telaApps - 1); NavegouNosApps(); }
    private void NavegouNosApps() { assinatura = ""; scroll.verticalNormalizedPosition = 1; }

    private void ConstruirAplicativos()
    {
        if (appSelecionado < 0 || appSelecionado >= Aplicativos.Length) { appSelecionado = 0; telaApps = 0; }
        AppDef app = Aplicativos[appSelecionado];
        if (telaApps == 2 && (itemSelecionado < 0 || itemSelecionado >= app.Entradas.Length)) telaApps = 1;
        titulo.text = telaApps == 0 ? "APLICATIVOS" : "APLICATIVOS • " + app.Nome.ToUpperInvariant();
        if (telaApps == 0) { ConstruirInicioApps(); return; }
        AppEntrada item = telaApps == 2 ? app.Entradas[itemSelecionado] : null;
        CabecalhoApp(app, item != null ? item.Nome : app.Nome);
        if (item == null) ConstruirItensApp(app); else ConstruirDetalheApp(app, item);
    }

    // Tela inicial: grade de aplicativos, três por linha.
    private void ConstruirInicioApps()
    {
        Texto(conteudo, "Instrucao", "Escolha um aplicativo para consultar.", 23);
        const int colunas = 3;
        for (int i = 0; i < Aplicativos.Length; i += colunas)
        {
            var linha = Retangulo("LinhaApps", conteudo, Color.clear); Altura(linha, 236);
            var grade = linha.gameObject.AddComponent<HorizontalLayoutGroup>();
            grade.spacing = 16; grade.childAlignment = TextAnchor.UpperCenter;
            grade.childControlWidth = grade.childControlHeight = true;
            grade.childForceExpandWidth = grade.childForceExpandHeight = true;
            for (int j = 0; j < colunas; j++)
            {
                if (i + j < Aplicativos.Length) CriarAtalhoApp(linha, i + j);
                else Retangulo("Vazio", linha, Color.clear).gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            }
        }
    }
    private void CriarAtalhoApp(Transform pai, int indice)
    {
        AppDef app = Aplicativos[indice];
        var tile = Retangulo("AppCard_" + app.IconeId, pai, corCard, true);
        var botao = tile.gameObject.AddComponent<Button>();
        botao.targetGraphic = tile.GetComponent<Image>(); botao.navigation = new Navigation { mode = Navigation.Mode.None };
        Ligar(botao, () => IrParaApp(indice));
        var icone = IconeDoApp(tile, app.IconeId);
        icone.anchorMin = icone.anchorMax = icone.pivot = new Vector2(.5f, 1);
        icone.anchoredPosition = new Vector2(0, -14); icone.sizeDelta = Vector2.one * 128;
        var nome = Texto(tile, "Nome", app.Nome, 22);
        nome.alignment = TextAlignmentOptions.Center; nome.fontStyle = FontStyles.Bold;
        nome.enableAutoSizing = true; nome.fontSizeMin = 15; nome.fontSizeMax = 22;
        Faixa(nome.rectTransform, -146, 52);
        var qtd = Texto(tile, "Quantidade", app.Entradas.Length + (app.Entradas.Length == 1 ? " item" : " itens"), 17);
        qtd.alignment = TextAlignmentOptions.Center; qtd.color = new Color(corTexto.r, corTexto.g, corTexto.b, .65f);
        Faixa(qtd.rectTransform, -200, 26);
    }
    // Imagem do ícone vetorial do aplicativo (sem raycast: o clique fica com o botão do tile).
    private RectTransform IconeDoApp(Transform pai, string iconeId)
    {
        var rt = Retangulo("IconeApp", pai, Color.white);
        var img = rt.GetComponent<Image>(); img.type = Image.Type.Simple; img.preserveAspect = true; img.raycastTarget = false;
        img.sprite = IconeApp(iconeId); if (img.sprite == null) img.color = Color.clear;
        return rt;
    }
    private static void Faixa(RectTransform r, float yTopo, float altura)
    { r.anchorMin = new Vector2(0, 1); r.anchorMax = Vector2.one; r.pivot = new Vector2(.5f, 1); r.anchoredPosition = new Vector2(0, yTopo); r.sizeDelta = new Vector2(-20, altura); }
    private static void Dimensionar(RectTransform rt, float largura, float altura)
    {
        var l = rt.GetComponent<LayoutElement>(); if (l == null) l = rt.gameObject.AddComponent<LayoutElement>();
        l.minWidth = l.preferredWidth = largura; l.minHeight = l.preferredHeight = altura;
    }

    // Faixa de cima das telas internas: VOLTAR + ícone do app + título.
    private void CabecalhoApp(AppDef app, string texto)
    {
        var linha = Retangulo("CabecalhoApp", conteudo, Color.clear); Altura(linha, 66);
        var h = linha.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 16; h.childAlignment = TextAnchor.MiddleLeft;
        h.childControlWidth = h.childControlHeight = true; h.childForceExpandWidth = h.childForceExpandHeight = false;
        var voltar = Botao(linha, "Voltar", "< VOLTAR"); Dimensionar((RectTransform)voltar.transform, 150, 50);
        Ligar(voltar, VoltarApps);
        Dimensionar(IconeDoApp(linha, app.IconeId), 60, 60);
        var t = Texto(linha, "TituloApp", texto, 28);
        t.fontStyle = FontStyles.Bold; t.alignment = TextAlignmentOptions.MidlineLeft;
        var le = t.gameObject.AddComponent<LayoutElement>(); le.flexibleWidth = 1; le.preferredHeight = 60;
    }

    private void ConstruirItensApp(AppDef app)
    {
        Texto(conteudo, "Instrucao", "Escolha um item para ver os detalhes.", 21);
        for (int i = 0; i < app.Entradas.Length; i++)
        {
            int item = i;
            var b = Botao(conteudo, "Item_" + i, app.Entradas[i].Nome); Altura((RectTransform)b.transform, 62);
            var tx = b.GetComponentInChildren<TMP_Text>();
            tx.alignment = TextAlignmentOptions.MidlineLeft; tx.fontSize = 22;
            tx.rectTransform.offsetMin = new Vector2(26, 2); tx.rectTransform.offsetMax = new Vector2(-60, -2);
            var seta = Texto(b.transform, "Seta", ">", 28);
            seta.alignment = TextAlignmentOptions.MidlineRight; Esticar(seta.rectTransform, new Vector2(10, 2), new Vector2(-24, -2));
            Ligar(b, () => IrParaItem(item));
        }
    }

    private void ConstruirDetalheApp(AppDef app, AppEntrada item)
    {
        var quadro = Retangulo("AppCard_Imagem", conteudo, corCard); Altura(quadro, 176);
        var figura = Retangulo("ImagemItem", quadro, Color.white); figura.sizeDelta = Vector2.one * 144;
        var img = figura.GetComponent<Image>(); img.type = Image.Type.Simple; img.preserveAspect = true; img.raycastTarget = false;
        Sprite foto = FotoApp(item.ImagemId);
        img.sprite = foto != null ? foto : IconeApp(app.IconeId);
        SecaoApp("O QUE É", item.OQueE, corDestaque);
        SecaoApp("ONDE ENCONTRAR", item.Onde, corDestaque);
        SecaoApp("ONDE ENCAIXAR / COMO USAR", item.Como, corDestaque);
        SecaoApp("ATENÇÃO", item.Atencao, corAlerta);
    }
    private void SecaoApp(string cabecalho, string corpo, Color corCabecalho)
    {
        var card = Retangulo("AppCard_Secao", conteudo, corCard); Vertical(card, 6, 18);
        var t = Texto(card, "TituloSecao", cabecalho, 19); t.fontStyle = FontStyles.Bold; t.color = corCabecalho;
        Texto(card, "TextoSecao", corpo, 22);
    }

    private void CriarImpacto(Vector2 mouse)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(raizUI, mouse, null, out Vector2 p);
        if (circuloSprite == null && circuloGerado == null)
        {
            texturaCirculo = new Texture2D(128, 128, TextureFormat.RGBA32, false);
            var pixels = new Color[128 * 128];
            for (int y = 0; y < 128; y++) for (int x = 0; x < 128; x++)
            { float d = new Vector2(x - 63.5f, y - 63.5f).magnitude; pixels[y * 128 + x] = new Color(1, 1, 1, Mathf.Clamp01(2 - Mathf.Abs(d - 58))); }
            texturaCirculo.SetPixels(pixels); texturaCirculo.Apply();
            circuloGerado = Sprite.Create(texturaCirculo, new Rect(0, 0, 128, 128), Vector2.one * .5f);
        }
        // Limita a quantidade de efeitos em cliques muito rápidos.
        while (impactos.Count > 57) { Destroy(impactos[0].rect.gameObject); impactos.RemoveAt(0); }
        for (int i = 0; i < 3; i++)
        {
            var rt = Retangulo("Impacto", efeitos, Color.clear); rt.anchoredPosition = p;
            var img = rt.GetComponent<Image>(); img.sprite = circuloSprite != null ? circuloSprite : circuloGerado;
            impactos.Add(new Impacto { rect = rt, image = img, inicio = Time.unscaledTime + intervaloCirculos * i });
        }
    }
    private void AtualizarImpactos()
    {
        for (int i = impactos.Count - 1; i >= 0; i--)
        {
            var v = impactos[i]; float t = (Time.unscaledTime - v.inicio) / duracaoClique;
            if (t >= 1) { Destroy(v.rect.gameObject); impactos.RemoveAt(i); continue; }
            v.rect.sizeDelta = Vector2.one * Mathf.Lerp(6, diametroClique, 1 - Mathf.Pow(1 - Mathf.Clamp01(t), 2));
            Color cor = corClique; cor.a *= t < 0 ? 0 : 1 - t; v.image.color = cor;
        }
    }

    [ContextMenu("Tablet/Gerar interface editável")]
    public void GerarInterface()
    {
        if (raizUI != null) return; // Não sobrescreve a arte que você já editou.
        raizUI = new GameObject("TabletUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<RectTransform>();
        raizUI.SetParent(transform, false);
        raizUI.pivot = Vector2.one * .5f;
        canvas = raizUI.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32000;
        var scaler = raizUI.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
        bloqueio = Retangulo("Bloqueio3D", raizUI, new Color(0, 0, 0, escurecerFundo), true); Esticar(bloqueio);
        painel = Retangulo("PainelCentral", raizUI, corFundo, true); painel.sizeDelta = tamanhoTablet;
        painel.GetComponent<Image>().sprite = fundoSprite;
        grupo = painel.gameObject.AddComponent<CanvasGroup>();
        titulo = Texto(painel, "Titulo", "CENTRAL DE CHAMADOS", 30); Esticar(titulo.rectTransform, new Vector2(28, -76), new Vector2(-90, -20)); titulo.rectTransform.anchorMin = new Vector2(0, 1);
        fechar = Botao(painel, "Fechar", "×"); Canto((RectTransform)fechar.transform, new Vector2(1, 1), new Vector2(-32, -32), new Vector2(42, 42));
        var tabs = Retangulo("Abas", painel, Color.clear); Esticar(tabs, new Vector2(28, -130), new Vector2(-28, -80)); tabs.anchorMin = new Vector2(0, 1);
        var horizontal = tabs.gameObject.AddComponent<HorizontalLayoutGroup>(); horizontal.spacing = 14; horizontal.childControlWidth = true; horizontal.childControlHeight = true; horizontal.childForceExpandWidth = true;
        abaPendentes = Botao(tabs, "Pendentes", "PENDENTES"); abaAtivos = Botao(tabs, "Atendimento", "EM ATENDIMENTO");
        abaHistorico = Botao(tabs, "Historico", "HISTÓRICO"); abaCores = Botao(tabs, "Cores", "CORES"); abaAplicativos = Botao(tabs, "Aplicativos", "APLICATIVOS");
        var area = Retangulo("Lista", painel, Color.clear); Esticar(area, new Vector2(28, 62), new Vector2(-28, -148));
        scroll = area.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 36;
        var viewport = Retangulo("Viewport", area, new Color(1, 1, 1, .001f), true); Esticar(viewport); viewport.gameObject.AddComponent<RectMask2D>(); scroll.viewport = viewport;
        conteudo = Retangulo("Conteudo", viewport, Color.clear); conteudo.anchorMin = new Vector2(0, 1); conteudo.anchorMax = Vector2.one; conteudo.pivot = new Vector2(.5f, 1); conteudo.sizeDelta = Vector2.zero;
        Vertical(conteudo, 14, 10); scroll.content = conteudo;
        proximo = Texto(painel, "Proximo", "", 18); Esticar(proximo.rectTransform, new Vector2(28, 14), new Vector2(-28, 50)); proximo.rectTransform.anchorMax = new Vector2(1, 0);
        atalho = Botao(raizUI, "AbrirTablet", "TABLET [" + toggleKey + "]"); Canto((RectTransform)atalho.transform, new Vector2(1, 0), new Vector2(-margem, margem + 20f), tamanhoAtalho); ((RectTransform)atalho.transform).pivot = new Vector2(1, 0);
        // Bolinha atrás do número de chamados: círculo cheio AZUL (mesma família dos brilhos azuis do tablet),
        // ligado/desligado com o número.
        seloFundo = Retangulo("SeloFundo", atalho.transform, corSeloFundo);
        Canto(seloFundo, new Vector2(1, 1), new Vector2(-12, -14), new Vector2(42, 42));
        var seloImg = seloFundo.GetComponent<Image>(); seloImg.type = Image.Type.Simple; seloImg.raycastTarget = false;
        seloFundo.SetAsLastSibling();
        selo = Texto(atalho.transform, "Selo", "", 20); Canto(selo.rectTransform, new Vector2(1, 1), new Vector2(-12, -14), new Vector2(42, 42));
        selo.alignment = TextAlignmentOptions.Center; selo.color = Color.white; selo.fontStyle = FontStyles.Bold; selo.transform.SetAsLastSibling();
        var toast = Retangulo("Notificacao", raizUI, corCard); Canto(toast, new Vector2(0, 1), new Vector2(24, -24), new Vector2(400, 120)); toast.pivot = new Vector2(0, 1);
        aviso = Texto(toast, "Texto", "", 19); Esticar(aviso.rectTransform, Vector2.one * 14, Vector2.one * -14); toast.gameObject.SetActive(false);
        modeloCard = Retangulo("ModeloCard_EDITAVEL", raizUI, corCard); Vertical(modeloCard, 9, 18);
        Texto(modeloCard, "Titulo", "#1  Título do chamado", 25);
        Texto(modeloCard, "Descricao", "Descrição fornecida pelo solicitante.", 21);
        Texto(modeloCard, "Detalhes", "Pendente", 18);
        var barra = Retangulo("BarraPrazo", modeloCard, Color.clear); Altura(barra, 14);
        Texto(modeloCard, "Tempo", "Prazo: 2:00", 18);
        Texto(modeloCard, "Feedback", "", 18).color = corAlerta;
        var acao = Botao(modeloCard, "Acao", "Iniciar atendimento"); Altura((RectTransform)acao.transform, 46);
        modeloCard.gameObject.SetActive(false);
        efeitos = Retangulo("EfeitosClique", raizUI, Color.clear); Esticar(efeitos);
        cursorVisual = Retangulo("Cursor", raizUI, Color.white); cursorVisual.gameObject.SetActive(false);
        if (!Application.isPlaying)
        {
            var exemplo = Instantiate(modeloCard, conteudo); exemplo.name = "PREVIA_Chamado"; exemplo.gameObject.SetActive(true);
            exemplo.Find("Feedback").gameObject.SetActive(false);
        }
        AtualizarGeometria();
    }
    [ContextMenu("Tablet/Atualizar aparência")]
    public void AtualizarAparencia()
    {
        if (raizUI == null) GerarInterface();
        GarantirAbaAplicativos(); AjustarRotulosDasAbas();
        painel.GetComponent<Image>().color = corFundo; painel.GetComponent<Image>().sprite = fundoSprite;
        foreach (var t in raizUI.GetComponentsInChildren<TMP_Text>(true)) { if (fonte != null) t.font = fonte; t.color = corTexto; }
        estiloInicializado = false;
        AtualizarEstilosBotoes();
        modeloCard.GetComponent<Image>().color = corCard;
        visuaisPendentes = true; AtualizarVisuais();
        AtualizarGeometria(); assinatura = "";
    }
    // Texturas compartilhadas por tamanho; não exige shader, pacote ou material externo.
    private readonly Dictionary<Vector2Int, Sprite[]> estilosBotoes = new Dictionary<Vector2Int, Sprite[]>();
    private readonly Dictionary<Button, Vector2Int> tamanhosEstilizados = new Dictionary<Button, Vector2Int>();
    private int hashEstilo;
    private bool estiloInicializado;
    private int HashEstilo()
    {
        unchecked
        {
            int h = raioBotoes.GetHashCode();
            h = h * 31 + gradienteTopo.GetHashCode(); h = h * 31 + gradienteBase.GetHashCode();
            h = h * 31 + corInnerGlow.GetHashCode(); h = h * 31 + larguraInnerGlow.GetHashCode();
            h = h * 31 + intensidadeInnerGlow.GetHashCode(); h = h * 31 + corOuterGlow.GetHashCode();
            h = h * 31 + larguraOuterGlow.GetHashCode(); h = h * 31 + intensidadeOuterGlow.GetHashCode();
            h = h * 31 + (efeitosBotoes != null ? efeitosBotoes.Hash() : 0);
            return h;
        }
    }
    private void AtualizarEstilosBotoes()
    {
        if (raizUI == null) return;
        int hash = HashEstilo();
        if (!estiloInicializado || hash != hashEstilo)
        {
            LimparEstilosBotoes(); hashEstilo = hash; estiloInicializado = true;
        }
        if (!Application.isPlaying)
        {
            Canvas.ForceUpdateCanvases();
            foreach (var b in raizUI.GetComponentsInChildren<Button>(true)) EstilizarBotao(b);
        }
        else
        {
            foreach (var h in hovers) if (h.button != null) EstilizarBotao(h.button);
        }
    }
    private void EstilizarBotao(Button b)
    {
        if (b == null || b.image == null) return;
        if (b.name.StartsWith("AppCard")) return; // tiles de aplicativo seguem o visual de card, não o de botão
        var rt = (RectTransform)b.transform;
        Vector2 size = rt.rect.size;
        if (size.x < 2 || size.y < 2) return;
        var chave = new Vector2Int(Mathf.Clamp(Mathf.RoundToInt(size.x), 2, 2048), Mathf.Clamp(Mathf.RoundToInt(size.y), 2, 1024));
        if (tamanhosEstilizados.TryGetValue(b, out Vector2Int anterior) && anterior == chave && b.image.sprite != null) return;
        if (!estilosBotoes.TryGetValue(chave, out Sprite[] sprites))
        {
            sprites = CriarEstiloBotao(chave.x, chave.y);
            estilosBotoes.Add(chave, sprites);
        }
        b.image.sprite = sprites[0]; b.image.type = Image.Type.Simple; b.image.color = Color.white;
        b.image.raycastTarget = true;
        AplicarBordaESombra(b.image, efeitosBotoes);
        var filho = b.transform.Find("OuterGlow_Tablet");
        RectTransform glow;
        if (filho == null)
        {
            glow = Retangulo("OuterGlow_Tablet", b.transform, Color.white);
            glow.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        }
        else glow = (RectTransform)filho;
        glow.SetAsFirstSibling();
        float margemGlow = Mathf.Ceil(Mathf.Max(0, larguraOuterGlow)) + 2;
        Esticar(glow, -Vector2.one * margemGlow, Vector2.one * margemGlow);
        var imagem = glow.GetComponent<Image>(); imagem.sprite = sprites[1]; imagem.type = Image.Type.Simple;
        imagem.color = Color.white; imagem.raycastTarget = false;
        // Apenas a parte externa é desenhada, então não cobre o gradiente ou o texto.
        tamanhosEstilizados[b] = chave;
    }
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
    private void LimparEstilosBotoes()
    {
        foreach (var par in estilosBotoes) foreach (var sprite in par.Value)
            if (sprite != null) { DestruirVisual(sprite.texture); DestruirVisual(sprite); }
        estilosBotoes.Clear(); tamanhosEstilizados.Clear();
    }
    private static void DestruirVisual(Object objeto)
    {
        if (objeto == null) return;
        if (Application.isPlaying) Destroy(objeto); else DestroyImmediate(objeto);
    }

    // Materiais são cópias locais: nunca modifica o material do arquivo de fonte.
    private readonly Dictionary<Material, Material> materiaisTexto = new Dictionary<Material, Material>();
    private Sprite spriteArredondado;
    private float raioGerado = -1;
    private bool visuaisPendentes = true;
    private int hashVisuais;
    [SerializeField, HideInInspector] private RectTransform fundoVisual;

    private void AtualizarVisuais()
    {
        if (raizUI == null || painel == null) return;
        if (fundoVisual == null)
        {
            var existente = painel.Find("CarcacaTablet");
            fundoVisual = existente != null ? (RectTransform)existente : Retangulo("CarcacaTablet", painel, Color.white);
            fundoVisual.SetAsFirstSibling(); Esticar(fundoVisual);
        }
        // A arte pode aumentar sem aumentar fontes, botões ou área de interação.
        fundoVisual.localScale = new Vector3(Mathf.Max(.05f, escalaFundoTablet.x), Mathf.Max(.05f, escalaFundoTablet.y), 1);
        fundoVisual.anchoredPosition = deslocamentoFundoTablet;
        var fundo = fundoVisual.GetComponent<Image>();
        fundo.sprite = fundoSprite; fundo.type = Image.Type.Simple;
        fundo.color = fundoSprite != null ? corSpriteTablet : corFundo;
        fundo.preserveAspect = preservarProporcaoFundo && fundoSprite != null;
        painel.GetComponent<Image>().color = Color.clear;

        int hash;
        unchecked
        {
            hash = raioPaineis.GetHashCode(); hash = hash * 31 + corCard.GetHashCode();
            hash = hash * 31 + corOutlineTexto.GetHashCode(); hash = hash * 31 + larguraOutlineTexto.GetHashCode();
            hash = hash * 31 + corSombraTexto.GetHashCode(); hash = hash * 31 + deslocamentoSombraTexto.GetHashCode();
            hash = hash * 31 + suavidadeSombraTexto.GetHashCode();
            hash = hash * 31 + expandirSombraTexto.GetHashCode();
            hash = hash * 31 + (efeitosCards != null ? efeitosCards.Hash() : 0);
        }
        if (!Application.isPlaying) visuaisPendentes = true;
        if (hash != hashVisuais) { hashVisuais = hash; visuaisPendentes = true; }
        if (spriteArredondado == null || !Mathf.Approximately(raioGerado, raioPaineis))
        {
            if (spriteArredondado != null) { DestruirVisual(spriteArredondado.texture); DestruirVisual(spriteArredondado); }
            raioGerado = raioPaineis;
            int borda = Mathf.Max(2, Mathf.CeilToInt(raioPaineis) + 2);
            int lado = borda * 2 + 2;
            var pixels = new Color[lado * lado];
            for (int y = 0; y < lado; y++) for (int x = 0; x < lado; x++)
            {
                float d = DistanciaBorda(x + .5f - lado * .5f, y + .5f - lado * .5f, lado, lado, raioPaineis);
                pixels[y * lado + x] = new Color(1, 1, 1, Mathf.Clamp01(.5f - d));
            }
            var tex = new Texture2D(lado, lado, TextureFormat.RGBA32, false)
            { name = "Tablet_PaineisArredondados", hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            tex.SetPixels(pixels); tex.Apply(false, true);
            spriteArredondado = Sprite.Create(tex, new Rect(0, 0, lado, lado), Vector2.one * .5f, 100, 0, SpriteMeshType.FullRect, Vector4.one * borda);
            spriteArredondado.hideFlags = HideFlags.HideAndDontSave;
            visuaisPendentes = true;
        }
        if (fundoSprite == null) { fundo.sprite = spriteArredondado; fundo.type = Image.Type.Sliced; }
        if (seloFundo != null && circuloCheioGerado == null)
        {
            circuloCheioGerado = CirculoCheio();
            if (seloFundo != null) { var imgSelo = seloFundo.GetComponent<Image>(); imgSelo.sprite = circuloCheioGerado; imgSelo.type = Image.Type.Simple; }
        }
        else if (seloFundo != null)
        {
            var imgSelo = seloFundo.GetComponent<Image>(); imgSelo.sprite = circuloCheioGerado; imgSelo.type = Image.Type.Simple;
        }
        if (!visuaisPendentes) return;
        visuaisPendentes = false;
        foreach (var imagem in raizUI.GetComponentsInChildren<Image>(true))
        {
            string nome = imagem.name;
            bool card = imagem.transform == modeloCard || nome.StartsWith("Chamado_") || nome.StartsWith("PREVIA_") || nome.StartsWith("AppCard");
            bool painelSimples = card || nome == "Notificacao" || nome.StartsWith("Fio_") || nome == "Amostra";
            if (!painelSimples) continue; // Cursor, efeitos e listras são desenhos, não painéis.
            imagem.sprite = spriteArredondado; imagem.type = Image.Type.Sliced;
            if (card) { imagem.color = corCard; AplicarBordaESombra(imagem, efeitosCards); }
            if (nome == "Amostra" && imagem.GetComponent<Mask>() == null)
            { var mask = imagem.gameObject.AddComponent<Mask>(); mask.showMaskGraphic = true; }
        }
        foreach (var texto in raizUI.GetComponentsInChildren<TMP_Text>(true)) AplicarEfeitosTexto(texto);
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

    private void AplicarEfeitosTexto(TMP_Text texto)
    {
        Material origem = texto.fontSharedMaterial;
        if (origem == null) return;
        // Cards clonados herdam o material do modelo. Resolve de volta à fonte original.
        foreach (var par in materiaisTexto) if (par.Value == origem) { origem = par.Key; break; }
        if (!materiaisTexto.TryGetValue(origem, out Material material))
        {
            material = new Material(origem) { name = origem.name + " (Tablet local)", hideFlags = HideFlags.HideAndDontSave };
            materiaisTexto.Add(origem, material);
        }
        if (material.HasProperty("_OutlineWidth"))
        {
            material.SetFloat("_OutlineWidth", larguraOutlineTexto);
            material.SetColor("_OutlineColor", corOutlineTexto);
            if (larguraOutlineTexto > 0) material.EnableKeyword("OUTLINE_ON"); else material.DisableKeyword("OUTLINE_ON");
        }
        if (material.HasProperty("_UnderlayColor"))
        {
            material.DisableKeyword("UNDERLAY_INNER");
            if (corSombraTexto.a > 0) material.EnableKeyword("UNDERLAY_ON"); else material.DisableKeyword("UNDERLAY_ON");
            material.SetColor("_UnderlayColor", corSombraTexto);
            material.SetFloat("_UnderlayOffsetX", deslocamentoSombraTexto.x);
            material.SetFloat("_UnderlayOffsetY", deslocamentoSombraTexto.y);
            material.SetFloat("_UnderlaySoftness", suavidadeSombraTexto);
            material.SetFloat("_UnderlayDilate", expandirSombraTexto);
        }
        texto.fontSharedMaterial = material;
        texto.extraPadding = true; texto.UpdateMeshPadding(); texto.SetMaterialDirty();
    }
    private void LimparVisuais()
    {
        if (raizUI != null)
            foreach (var texto in raizUI.GetComponentsInChildren<TMP_Text>(true))
                foreach (var par in materiaisTexto)
                    if (texto.fontSharedMaterial == par.Value) { texto.fontSharedMaterial = par.Key; break; }
        foreach (var par in materiaisTexto) DestruirVisual(par.Value);
        materiaisTexto.Clear();
        if (spriteArredondado != null) { DestruirVisual(spriteArredondado.texture); DestruirVisual(spriteArredondado); }
        spriteArredondado = null; visuaisPendentes = true;
        if (circuloCheioGerado != null) { DestruirVisual(circuloCheioGerado.texture); DestruirVisual(circuloCheioGerado); circuloCheioGerado = null; }
    }

    private static Sprite CirculoCheio()
    {
        const int lado = 128;
        var tex = new Texture2D(lado, lado, TextureFormat.RGBA32, false)
        { name = "Tablet_CirculoCheio", hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var pixels = new Color[lado * lado];
        for (int y = 0; y < lado; y++) for (int x = 0; x < lado; x++)
        {
            float d = new Vector2(x + .5f - lado * .5f, y + .5f - lado * .5f).magnitude;
            pixels[y * lado + x] = new Color(1, 1, 1, Mathf.Clamp01(lado * .5f - d));   // 1 px de antialias na borda
        }
        tex.SetPixels(pixels); tex.Apply(false, true);
        var sp = Sprite.Create(tex, new Rect(0, 0, lado, lado), Vector2.one * .5f);
        sp.hideFlags = HideFlags.HideAndDontSave; return sp;
    }
    private RectTransform Retangulo(string nome, Transform pai, Color cor, bool raycast = false)
    {
        if (nome != "Impacto" && nome != "OuterGlow_Tablet") visuaisPendentes = true;
        var r = new GameObject(nome, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>(); r.SetParent(pai, false);
        r.anchorMin = r.anchorMax = Vector2.one * .5f;
        var img = r.GetComponent<Image>(); img.color = cor; img.raycastTarget = raycast; img.type = Image.Type.Sliced; return r;
    }
    private TMP_Text Texto(Transform pai, string nome, string valor, float tamanho)
    {
        visuaisPendentes = true;
        var t = new GameObject(nome, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>(); t.transform.SetParent(pai, false);
        if (fonte != null) t.font = fonte;
        t.text = valor; t.fontSize = tamanho; t.color = corTexto; t.raycastTarget = false; t.richText = false; return t;
    }
    private Button Botao(Transform pai, string nome, string label)
    {
        var rt = Retangulo(nome, pai, corBotao, true);
        var b = rt.gameObject.AddComponent<Button>(); b.targetGraphic = rt.GetComponent<Image>(); b.navigation = new Navigation { mode = Navigation.Mode.None };
        var t = Texto(rt, "Texto", label, 19); t.alignment = TextAlignmentOptions.Center; Esticar(t.rectTransform, new Vector2(9, 2), new Vector2(-9, -2));
        return b;
    }
    private static void Vertical(RectTransform rt, float espaco, int padding)
    {
        var l = rt.gameObject.AddComponent<VerticalLayoutGroup>(); l.spacing = espaco; l.padding = new RectOffset(padding, padding, padding, padding);
        l.childControlHeight = true; l.childControlWidth = true; l.childForceExpandHeight = false;
        var f = rt.gameObject.AddComponent<ContentSizeFitter>(); f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }
    private static void Altura(RectTransform rt, float h) { var l = rt.gameObject.AddComponent<LayoutElement>(); l.minHeight = h; l.preferredHeight = h; }
    private static void Esticar(RectTransform r) { Esticar(r, Vector2.zero, Vector2.zero); }
    private static void Esticar(RectTransform r, Vector2 min, Vector2 max) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = min; r.offsetMax = max; }
    private static void Canto(RectTransform r, Vector2 anchor, Vector2 pos, Vector2 size) { r.anchorMin = r.anchorMax = anchor; r.anchoredPosition = pos; r.sizeDelta = size; }
}

#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(TicketTabletPrototype))]
public class TicketTabletPrototypeEditor : UnityEditor.Editor
{
    private void OnEnable()
    {
        if (!Application.isPlaying && target != null)
            UnityEditor.EditorApplication.delayCall += RestaurarPrevia;
    }
    private void RestaurarPrevia()
    {
        if (this != null && target != null && !Application.isPlaying)
        { ((TicketTabletPrototype)target).AtualizarGeometria(); UnityEditor.SceneView.RepaintAll(); }
    }
    public override void OnInspectorGUI()
    {
        UnityEditor.EditorGUI.BeginChangeCheck();
        DrawDefaultInspector();
        bool camposAlterados = UnityEditor.EditorGUI.EndChangeCheck();
        var tablet = (TicketTabletPrototype)target;
        UnityEditor.EditorGUILayout.Space();
        UnityEditor.EditorGUILayout.HelpBox("Gere uma vez para editar o Canvas na hierarquia. Prévia No Editor mostra o painel sem Play. Comecar Aberto mostra também na Scene View e abre ao iniciar. O ModeloCard_EDITAVEL é o modelo dos chamados; mantenha seus nomes e filhos. A aba APLICATIVOS é criada sozinha em Play; use \"Atualizar aparência\" para gravá-la na hierarquia. Configure um EventSystem com InputSystemUIInputModule na cena e importe TMP Essential Resources.", UnityEditor.MessageType.Info);
        if (GUILayout.Button("Gerar interface editável (preserva a existente)"))
        {
            UnityEditor.Undo.RegisterFullObjectHierarchyUndo(tablet.gameObject, "Gerar tablet");
            tablet.GerarInterface(); UnityEditor.EditorUtility.SetDirty(tablet);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(tablet.gameObject.scene);
        }
        if (GUILayout.Button("Atualizar aparência pelos campos do Inspector"))
        {
            UnityEditor.Undo.RegisterFullObjectHierarchyUndo(tablet.gameObject, "Aparência tablet");
            tablet.AtualizarAparencia(); UnityEditor.EditorUtility.SetDirty(tablet);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(tablet.gameObject.scene);
        }
        if (!Application.isPlaying && camposAlterados)
        {
            tablet.AtualizarGeometria(); UnityEditor.EditorUtility.SetDirty(tablet);
            UnityEditor.SceneView.RepaintAll();
            UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(tablet);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(tablet.gameObject.scene);
        }
        if (Application.isPlaying && GUILayout.Button(tablet.IsOpen ? "Fechar tablet" : "Abrir tablet"))
        { if (tablet.IsOpen) tablet.Fechar(); else tablet.Abrir(); }
    }
}
#endif
