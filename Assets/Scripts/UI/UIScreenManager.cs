using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Abre/fecha telas 2D (<see cref="UIScreen"/>) por cima da visão em primeira pessoa e devolve o controle ao jogador.
/// Fica no Player. Enquanto há uma tela aberta:
///  - o cursor é liberado e visível (para clicar na UI);
///  - os componentes de "Disable While Open" (PlayerLook, PlayerMovement...) ficam desligados;
///  - o <see cref="InteractionDetector"/> não processa cliques (os cliques vão só para a UI).
/// Ao fechar (Esc, botão da tela ou <see cref="Close"/>), o cursor volta a ser travado e os componentes religam.
/// Precisa de um EventSystem com InputSystemUIInputModule na cena para os botões funcionarem.
/// </summary>
public class UIScreenManager : MonoBehaviour
{
    [Tooltip("Canvas (ou filho dele) onde as telas são criadas quando o prefab da tela é passado em Open.")]
    [SerializeField] private Transform screenRoot;
    [Tooltip("Componentes desligados enquanto uma tela está aberta (ex.: PlayerLook, PlayerMovement).")]
    [SerializeField] private Behaviour[] disableWhileOpen;
    [Header("Cursor das telas")]
    [Tooltip("Cursor padrão (mãozinha) em TODAS as telas abertas. Uma tela pode trocá-lo enquanto aberta " +
             "(ex.: chave de fenda na traseira do PC) e voltar a ele com ApplyDefaultCursor.")]
    [SerializeField] private Texture2D defaultCursor;
    [Tooltip("Ponta do cursor, em pixels da textura (origem no canto superior esquerdo).")]
    [SerializeField] private Vector2 defaultCursorHotspot;
    [SerializeField] private int defaultCursorHeight = 48;
    [Tooltip("Mãozinha do HUD: escondida enquanto a tela está aberta.")]
    [SerializeField] private GameObject handCursor;
    [SerializeField] private bool closeWithEscape = true;

    private readonly Dictionary<UIScreen, UIScreen> instances = new Dictionary<UIScreen, UIScreen>();
    private UIScreen current;
    private int closedFrame = -1;

    public UIScreen Current => current;

    /// <summary>Uma tela 2D abriu a partir de um objeto do mundo (<c>source</c>). Ex.: chamados contam "inspecionar".</summary>
    public static event System.Action<UIScreen, GameObject> AnyScreenOpened;
    public bool IsOpen => current != null;

    /// <summary>Gerenciador ativo da cena. O celular usa para saber se há tela 2D aberta e para pegar a mãozinha.</summary>
    public static UIScreenManager Active { get; private set; }

    /// <summary>Mãozinha configurada para as telas 2D (a MESMA que o celular usa em cima do aparelho aberto).</summary>
    public Texture2D CursorMao => defaultCursor;
    /// <summary>Ponta da mãozinha, em pixels da textura (origem no canto superior esquerdo).</summary>
    public Vector2 CursorMaoPonta => defaultCursorHotspot;
    /// <summary>Altura da mãozinha, em pixels da tela.</summary>
    public int CursorMaoAltura => defaultCursorHeight;

    private void OnEnable() => Active = this;

    private void OnDisable()
    {
        if (Active == this) Active = null;
    }

    /// <summary>
    /// Verdadeiro com tela aberta e também no frame em que ela fechou: o clique que fechou a tela
    /// (ex.: botão Fechar) não pode virar um clique 3D no mesmo frame.
    /// </summary>
    public bool BlocksGameplayInput => IsOpen || Time.frameCount <= closedFrame;

    /// <summary>
    /// Abre a tela. Aceita um prefab (criado uma vez sob <c>screenRoot</c> e reaproveitado) ou uma
    /// instância já existente na cena. Retorna false se já houver uma tela aberta.
    /// <paramref name="source"/> (objeto do mundo que abriu a tela) e <paramref name="interactor"/> (jogador) são
    /// repassados à tela via <see cref="UIScreen.Bind"/> antes de ela aparecer.
    /// </summary>
    public bool Open(UIScreen screen, GameObject source = null, InteractionDetector interactor = null)
    {
        if (screen == null || IsOpen) return false;

        UIScreen instance = Resolve(screen);
        if (instance == null) return false;

        instance.Bind(source, interactor);
        current = instance;
        SetPlayerControl(false);
        if (handCursor != null) handCursor.SetActive(false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        ApplyDefaultCursor();   // antes de mostrar: a tela pode trocar o cursor ao abrir
        instance.transform.SetAsLastSibling();
        instance.Show(this);
        AnyScreenOpened?.Invoke(instance, source);
        return true;
    }

    public void Close()
    {
        if (!IsOpen) return;
        UIScreen closing = current;
        current = null;
        closedFrame = Time.frameCount;
        closing.Hide();
        UICursor.Reset();       // fora das telas, nenhum cursor personalizado fica ativo
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        StartCoroutine(RestorePlayerNextFrame());
    }

    /// <summary>
    /// Cursor coerente com o estado atual: com tela 2D aberta, livre e visível; no 3D, travado no centro da janela e
    /// escondido. Quem mexe no cursor por conta própria (celular) chama isto ao devolver o controle.
    /// </summary>
    public void ApplyCursorForCurrentState()
    {
        bool tela = IsOpen;
        Cursor.lockState = tela ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = tela;
    }

    /// <summary>Volta ao cursor padrão das telas (mãozinha); sem textura configurada, o do sistema.</summary>
    public void ApplyDefaultCursor()
    {
        if (defaultCursor != null) UICursor.Set(defaultCursor, defaultCursorHotspot, defaultCursorHeight);
        else UICursor.Reset();
    }

    private void Update()
    {
        // Rede de segurança: no 3D (sem tela, sem celular, sem relatório) e com a janela em foco, o cursor fica
        // travado. Algo que solte o cursor e não o devolva (ex.: fechar uma tela por outro caminho) não deixa o
        // jogador com o mouse saindo da janela.
        if (!IsOpen && !TicketTabletPrototype.CelularAberto && !StageReportScreen.Showing && Application.isFocused
            && Time.frameCount > closedFrame + 1 && Cursor.lockState != CursorLockMode.Locked)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        if (closeWithEscape && IsOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            Close();
    }

    private UIScreen Resolve(UIScreen screen)
    {
        // Objeto da cena: usa direto. Prefab (asset): instancia uma vez e reaproveita.
        if (screen.gameObject.scene.IsValid()) return screen;

        if (instances.TryGetValue(screen, out UIScreen existing) && existing != null) return existing;

        if (screenRoot == null)
        {
            Debug.LogError("UIScreenManager: defina 'Screen Root' (Canvas) para instanciar prefabs de tela.", this);
            return null;
        }

        UIScreen created = Instantiate(screen, screenRoot, false);
        created.gameObject.SetActive(false);
        instances[screen] = created;
        return created;
    }

    // Religa um frame depois: evita o "pulo" de câmera causado pelo delta do mouse ao travar o cursor.
    private IEnumerator RestorePlayerNextFrame()
    {
        yield return null;
        if (!IsOpen) SetPlayerControl(true);
    }

    private void SetPlayerControl(bool enabled)
    {
        if (disableWhileOpen == null) return;
        foreach (Behaviour b in disableWhileOpen)
            if (b != null) b.enabled = enabled;
    }
}
