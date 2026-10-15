using UnityEngine;

/// <summary>
/// Clique no painel 3D da impressora (filho <c>Painel</c>, com collider próprio): abre a tela 2D da impressora JÁ na
/// vista do painel (visor + teclado numérico do IP), com a mesma estação como <c>Source</c>. O resto da impressora
/// (corpo, bandeja) continua abrindo a tela como antes, pelo <see cref="RedeEstacaoInteractable"/>/<see cref="UIScreenInteractable"/>.
/// Clique esquerdo e direito fazem o mesmo.
/// </summary>
public class ImpressoraPainelInteractable : Interactable
{
    [Tooltip("Tela 2D da impressora (prefab UI_Screen_Impressora ou a instância da cena).")]
    public UIScreen screen;
    [Tooltip("Estado da impressora. Vazio = o ImpressoraEstacao do pai.")]
    [SerializeField] private ImpressoraEstacao estacao;

    private void Reset() => interactionMessage = "Configurar o IP";

    private ImpressoraEstacao Estacao => estacao != null ? estacao : estacao = GetComponentInParent<ImpressoraEstacao>();

    public override bool CanInteract(InteractionDetector interactor) =>
        isActiveAndEnabled && screen != null && Estacao != null && interactor != null && interactor.Screens != null;

    public override bool CanInteractSecondary(InteractionDetector interactor) => CanInteract(interactor);

    public override void Interact(InteractionDetector interactor)
    {
        Estacao.PedirVista(ImpressoraVista.Painel);
        interactor.Screens.Open(screen, Estacao.gameObject, interactor);
    }

    public override void InteractSecondary(InteractionDetector interactor) => Interact(interactor);
}
