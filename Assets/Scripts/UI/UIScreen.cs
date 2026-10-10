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

    /// <summary>Use no OnClick do botão "Fechar" (ou em qualquer evento da tela).</summary>
    public void RequestClose()
    {
        if (owner != null) owner.Close();
    }
}
