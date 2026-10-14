using UnityEngine;

/// <summary>
/// Abre uma tela 2D com o clique ESQUERDO. Usado nas "estações" do minigame de rede (a caixa de cabo e o rack):
/// o jogador só mira e clica no objeto para abrir a tela.
/// </summary>
public class RedeEstacaoInteractable : Interactable
{
    [Tooltip("Tela 2D que será aberta ao clicar.")]
    public UIScreen screen;

    private void Reset() => interactionMessage = "Usar";

    public override bool CanInteract(InteractionDetector interactor) =>
        isActiveAndEnabled && screen != null && interactor != null && interactor.Screens != null;

    public override void Interact(InteractionDetector interactor) =>
        interactor.Screens.Open(screen, gameObject, interactor);
}
