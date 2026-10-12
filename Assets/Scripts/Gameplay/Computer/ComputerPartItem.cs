using UnityEngine;

/// <summary>
/// Marca um item do mundo (Pickup_PlacaMae, Pickup_MemoriaRAM, Pickup_HD…) como peça interna de PC.
/// Fica no ROOT LÓGICO do item, junto do PickupInteractable. Instalar a peça num gabinete guarda ESTE objeto
/// dentro do <see cref="ComputerCase"/> (desativado, nunca destruído); retirá-la devolve o mesmo objeto à mão.
/// </summary>
public class ComputerPartItem : MonoBehaviour
{
    [Tooltip("Qual componente esta peça é (um só).")]
    [SerializeField] private PcComponent component = PcComponent.Ram;
    [Tooltip("Nome mostrado ao jogador nas mensagens (ex.: 'memória RAM').")]
    [SerializeField] private string displayName = "memória RAM";

    public PcComponent Component => component;
    public string DisplayName => displayName;
}
