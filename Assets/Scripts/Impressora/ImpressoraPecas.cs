using UnityEngine;

/// <summary>Peça que a impressora aceita: o cartucho de toner e o papel da bandeja.</summary>
public enum ImpressoraParte { Nenhuma, Toner, Papel }

/// <summary>
/// Marca um item do mundo (Pickup_Toner, Pickup_ResmaPapel) como peça de impressora. Fica no ROOT LÓGICO do
/// item, junto do <see cref="PickupInteractable"/>. Instalar guarda ESTE objeto na impressora (desativado,
/// nunca destruído); retirar devolve o mesmo objeto à mão.
/// </summary>
public class ImpressoraPartItem : MonoBehaviour
{
    [Tooltip("Qual peça da impressora este item é (uma só).")]
    [SerializeField] private ImpressoraParte parte = ImpressoraParte.Toner;
    [Tooltip("Nome mostrado ao jogador nas mensagens.")]
    [SerializeField] private string displayName = "cartucho de toner";

    public ImpressoraParte Parte => parte;
    public string DisplayName => displayName;
}
