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
    [Tooltip("Mãozinha do HUD: escondida enquanto a tela está aberta.")]
    [SerializeField] private GameObject handCursor;
    [SerializeField] private bool closeWithEscape = true;

    private readonly Dictionary<UIScreen, UIScreen> instances = new Dictionary<UIScreen, UIScreen>();
    private UIScreen current;
    private int closedFrame = -1;

    public UIScreen Current => current;
    public bool IsOpen => current != null;

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
        instance.transform.SetAsLastSibling();
        instance.Show(this);
        return true;
    }

    public void Close()
    {
        if (!IsOpen) return;
        UIScreen closing = current;
        current = null;
        closedFrame = Time.frameCount;
        closing.Hide();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        StartCoroutine(RestorePlayerNextFrame());
    }

    private void Update()
    {
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
