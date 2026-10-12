using UnityEngine;

/// <summary>Tipos de cabo reconhecidos pela lógica do PC.</summary>
public enum CableType { Power, Video, Network }

/// <summary>
/// Marca um item do mundo (em geral um prefab Pickup_Cabo*) como cabo de um tipo.
/// Fica no ROOT LÓGICO do item, junto do PickupInteractable. A lógica identifica o cabo na mão do jogador
/// por este componente, nunca pelo nome ou pela tag.
/// </summary>
public class CableItem : MonoBehaviour
{
    [SerializeField] private CableType cableType = CableType.Power;
    [Tooltip("Nome mostrado ao jogador nas mensagens (ex.: 'cabo de energia').")]
    [SerializeField] private string displayName = "cabo de energia";

    public CableType Type => cableType;
    public string DisplayName => displayName;
}
