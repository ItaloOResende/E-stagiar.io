using UnityEngine;

/// <summary>
/// Abre uma tela 2D (<see cref="UIScreen"/>) com o clique DIREITO do mouse ao mirar este objeto.
/// Reutilizável: vale para gabinete, monitor, quadro, etc. — basta colocar este componente no root lógico
/// e apontar o prefab da tela em <see cref="screen"/>. Convive com outros Interactable no mesmo objeto
/// (ex.: PickupInteractable usa o clique esquerdo).
/// </summary>
public class UIScreenInteractable : Interactable
{
    [Tooltip("Prefab da tela (ou instância na cena) que será aberta.")]
    public UIScreen screen;

    private void Reset() => interactionMessage = "Ver interface";

    // O clique esquerdo fica para os outros Interactable do objeto.
    public override bool CanInteract(InteractionDetector interactor) => false;

    public override bool CanInteractSecondary(InteractionDetector interactor) =>
        isActiveAndEnabled && screen != null && interactor != null && interactor.Screens != null;

    public override void InteractSecondary(InteractionDetector interactor)
    {
        interactor.Screens.Open(screen, gameObject, interactor);
    }
}
