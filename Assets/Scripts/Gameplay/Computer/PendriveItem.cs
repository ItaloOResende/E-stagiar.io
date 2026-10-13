using UnityEngine;

/// <summary>
/// Marca um item do mundo (Pickup_Pendrive) como pendrive com a mídia de instalação do sistema operacional.
/// Fica no ROOT LÓGICO do item, junto do PickupInteractable. A lógica identifica o pendrive na mão do jogador
/// por este componente, nunca pelo nome ou pela tag. Conectado numa USB do PC (<see cref="ComputerPower.UseUsbPort"/>),
/// o mesmo objeto fica guardado no PC e volta para a mão ao ser desconectado.
/// </summary>
public class PendriveItem : MonoBehaviour
{
    [Tooltip("Nome mostrado ao jogador nas mensagens.")]
    [SerializeField] private string displayName = "pendrive de instalação";

    public string DisplayName => displayName;
}
