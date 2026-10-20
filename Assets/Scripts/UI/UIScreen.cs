using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Raiz de uma tela 2D (Canvas) aberta pelo jogador: painel + conteúdo. Fica no GameObject raiz do prefab da tela.
/// Abrir/fechar é feito pelo <see cref="UIScreenManager"/> (que também trava/destrava o jogador); o botão de fechar
/// da tela só precisa chamar <see cref="RequestClose"/> (OnClick do Button).
/// Componentes clicáveis futuros (peças, cabos, botões) são apenas filhos desta tela: não exigem mudar este script.
/// </summary>
public class UIScreen : MonoBehaviour
{
    [Tooltip("Nome mostrado só no Inspector/Console (a tela pode ter seu próprio título visual).")]
    [SerializeField] private string screenName = "Tela";

    public UnityEvent onOpened;
    public UnityEvent onClosed;

    public string ScreenName => screenName;
    public bool IsOpen => gameObject.activeSelf;

    /// <summary>
    /// Objeto do mundo que abriu a tela (ex.: o gabinete). A mesma instância de tela é reaproveitada por vários
    /// objetos, então o conteúdo deve ler este vínculo ao abrir (<see cref="onOpened"/> / OnEnable) e nunca guardar
    /// estado do objeto: o estado fica no objeto lógico do mundo.
    /// </summary>
    public GameObject Source { get; private set; }
    /// <summary>Jogador que abriu a tela (dá acesso à mão: <c>Interactor.Holder</c>).</summary>
    public InteractionDetector Interactor { get; private set; }

    private UIScreenManager owner;
    /// <summary>Gerenciador que abriu a tela (null se fechada).</summary>
    public UIScreenManager Owner => owner;

    /// <summary>Chamado pelo manager, antes de <see cref="Show"/>.</summary>
    public void Bind(GameObject source, InteractionDetector interactor)
    {
        Source = source;
        Interactor = interactor;
    }

    /// <summary>Chamado pelo manager.</summary>
    public void Show(UIScreenManager manager)
    {
        owner = manager;
        gameObject.SetActive(true);
        onOpened.Invoke();
    }

    /// <summary>Chamado pelo manager.</summary>
    public void Hide()
    {
        gameObject.SetActive(false);
        onClosed.Invoke();
        owner = null;
    }

    // ------------------------------------------------ botão X ao lado do selo do tempo
    // Todas as telas 2D têm o X (filho "BotaoFechar") no canto superior direito. Com o selo do tempo na tela
    // (StageTimerHUD), o X vai para a ESQUERDA do selo, alinhado a ele; sem o selo (tutorial), volta ao lugar de origem.
    // Só a posição muda: o botão e o OnClick continuam os mesmos.
    private RectTransform botaoFechar;
    private Vector2 posicaoOriginalBotao;
    private bool procurouBotao, botaoDeslocado;
    private readonly Vector3[] cantos = new Vector3[4];

    private void LateUpdate()
    {
        if (!procurouBotao) { procurouBotao = true; botaoFechar = AcharBotaoFechar(transform); if (botaoFechar != null) posicaoOriginalBotao = botaoFechar.anchoredPosition; }
        if (botaoFechar == null) { if (Time.frameCount % 30 == 0) procurouBotao = false; return; }   // telas que montam a UI depois
        if (!StageTimerHUD.SeloNaTela)
        {
            if (botaoDeslocado) { botaoFechar.anchoredPosition = posicaoOriginalBotao; botaoDeslocado = false; }
            return;
        }
        Canvas canvas = botaoFechar.GetComponentInParent<Canvas>();
        if (canvas == null) return;
        var raizCanvas = (RectTransform)canvas.rootCanvas.transform;
        raizCanvas.GetWorldCorners(cantos); Vector3 topoDireita = cantos[2];
        float escala = raizCanvas.lossyScale.x;
        Vector2 alvo = HudEstilo.PosicaoBotaoFechar(botaoFechar.rect.height * botaoFechar.lossyScale.y / Mathf.Max(.0001f, escala));
        Vector3 desejado = topoDireita + new Vector3(alvo.x * escala, alvo.y * escala, 0f);
        botaoFechar.anchoredPosition = posicaoOriginalBotao; botaoFechar.GetWorldCorners(cantos);
        botaoFechar.position += desejado - cantos[2];
        botaoDeslocado = true;
    }

    private static RectTransform AcharBotaoFechar(Transform t)
    {
        foreach (Transform filho in t)
        {
            if (filho.name == "BotaoFechar" && filho is RectTransform rt) return rt;
            RectTransform achado = AcharBotaoFechar(filho);
            if (achado != null) return achado;
        }
        return null;
    }

    /// <summary>Use no OnClick do botão "Fechar" (ou em qualquer evento da tela).</summary>
    public void RequestClose()
    {
        if (owner != null) owner.Close();
    }
}
