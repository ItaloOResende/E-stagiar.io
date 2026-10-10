using UnityEngine;

/// <summary>
/// Marca um item do mundo (em geral um prefab Pickup_*) como ferramenta de um tipo.
/// Fica no ROOT LÓGICO do item, junto do PickupInteractable. A lógica de gameplay identifica a
/// ferramenta na mão do jogador por este componente, não pelo nome nem pela tag do objeto.
/// </summary>
public class ToolItem : MonoBehaviour
{
    [SerializeField] private ToolType toolType = ToolType.Screwdriver;
    [Tooltip("Nome mostrado ao jogador nas mensagens (ex.: 'chave de fenda').")]
    [SerializeField] private string displayName = "chave de fenda";

    public ToolType Type => toolType;
    public string DisplayName => displayName;
}
