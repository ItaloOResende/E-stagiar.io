using UnityEngine;

/// <summary>
/// Testador de cabos de rede (objeto 3D). Segue o padrão das telas usadas com algo na mão
/// (<see cref="UIScreenInteractable"/>): com item na mão o clique ESQUERDO sempre solta o item
/// (<see cref="InteractionDetector"/>), então a tela do teste abre com o clique DIREITO, com ou sem cabo na mão.
/// O clique esquerdo com a mão livre fica para o <see cref="PickupInteractable"/> do mesmo objeto (pegar o testador).
/// A tela (<see cref="TestadorCaboScreen"/>) lê o cabo da mão na hora de testar.
/// </summary>
public class TestadorCaboInteractable : Interactable
{
    [Tooltip("Tela 2D do testador (UI_Screen_TestadorCabo).")]
    public UIScreen screen;
    [Tooltip("Texto do prompt do clique direito.")]
    [SerializeField] private string secondaryMessage = "Testar cabo de rede";

    private void Reset() => interactionMessage = "Testar cabo de rede";

    public override string PromptText => secondaryMessage;

    // clique esquerdo: nunca (é do PickupInteractable); o direito abre a tela
    public override bool CanInteract(InteractionDetector interactor) => false;

    public override bool CanInteractSecondary(InteractionDetector interactor) =>
        isActiveAndEnabled && screen != null && interactor != null && interactor.Screens != null && !interactor.Screens.IsOpen;

    public override void InteractSecondary(InteractionDetector interactor) =>
        interactor.Screens.Open(screen, gameObject, interactor);

    /// <summary>O cabo de REDE na mão do jogador (null = mão vazia ou outra coisa).</summary>
    public static CableItem CaboNaMao(InteractionDetector interactor)
    {
        ItemHolder mao = interactor != null ? interactor.Holder : null;
        CableItem cabo = mao != null && mao.Current != null ? mao.Current.GetComponentInParent<CableItem>(true) : null;
        return cabo != null && cabo.Type == CableType.Network ? cabo : null;
    }
}
