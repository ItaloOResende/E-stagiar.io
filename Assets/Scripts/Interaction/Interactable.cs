using UnityEngine;

/// <summary>
/// Contrato base de tudo que pode receber o clique do jogador.
/// Subclasses: <see cref="PickupInteractable"/> (carregar), <see cref="OpenableInteractable"/> (abrir/fechar).
/// A instância base serve como "ação genérica" (hoje só registra a mensagem no Console).
/// Não depende de modelo/nome/tag: vive no root lógico do prefab.
/// </summary>
public class Interactable : MonoBehaviour
{
    public string interactionMessage = "Interagir";

    /// <summary>O objeto aceita interação agora? (controla a mãozinha e o clique)</summary>
    public virtual bool CanInteract(InteractionDetector interactor) => isActiveAndEnabled;

    /// <summary>Executa a interação (clique esquerdo).</summary>
    public virtual void Interact(InteractionDetector interactor)
    {
        Debug.Log(interactionMessage, this);
    }
}
