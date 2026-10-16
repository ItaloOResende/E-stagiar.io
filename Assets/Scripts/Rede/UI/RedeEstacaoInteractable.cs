using UnityEngine;

/// <summary>
/// Abre uma tela 2D com o clique ESQUERDO. Usado nas "estações" do minigame de rede (a caixa de cabo e o rack):
/// o jogador só mira e clica no objeto para abrir a tela.
/// </summary>
public class RedeEstacaoInteractable : Interactable
{
    [Tooltip("Tela 2D que será aberta ao clicar.")]
    public UIScreen screen;

    [Tooltip("Só aceita o clique com o alicate de crimpar na mão (a caixa de cabo de rede): sem o alicate, o " +
             "clique esquerdo fica para o objeto pegável, que põe a caixa na mão.")]
    [SerializeField] private bool exigirAlicate;

    private void Reset() => interactionMessage = "Usar";

    public override bool CanInteract(InteractionDetector interactor) =>
        isActiveAndEnabled && screen != null && interactor != null && interactor.Screens != null
        && (!exigirAlicate || TemAlicate(interactor));

    public override void Interact(InteractionDetector interactor) =>
        interactor.Screens.Open(screen, gameObject, interactor);

    /// <summary>O jogador está com o alicate de crimpar (item com <see cref="ToolType.CrimpingTool"/>) na mão?</summary>
    public static bool TemAlicate(InteractionDetector interactor)
    {
        ItemHolder mao = interactor != null ? interactor.Holder : null;
        ToolItem ferramenta = mao != null && mao.Current != null ? mao.Current.GetComponentInParent<ToolItem>() : null;
        return ferramenta != null && ferramenta.Type == ToolType.CrimpingTool;
    }
}
