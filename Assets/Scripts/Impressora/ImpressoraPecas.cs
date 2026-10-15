using UnityEngine;

/// <summary>Peça que a impressora aceita: o cartucho de toner e o papel da bandeja.</summary>
public enum ImpressoraParte { Nenhuma, Toner, Papel }

/// <summary>
/// Marca um item do mundo (Pickup_Toner, Pickup_ResmaPapel) como peça de impressora. Fica no ROOT LÓGICO do
/// item, junto do <see cref="PickupInteractable"/>. Instalar guarda ESTE objeto na impressora (desativado,
/// nunca destruído); retirar devolve o mesmo objeto à mão.
/// </summary>
public class ImpressoraPartItem : MonoBehaviour, IBreakable
{
    [Tooltip("Qual peça da impressora este item é (uma só).")]
    [SerializeField] private ImpressoraParte parte = ImpressoraParte.Toner;
    [Tooltip("Nome mostrado ao jogador nas mensagens.")]
    [SerializeField] private string displayName = "cartucho de toner";

    public ImpressoraParte Parte => parte;
    public string DisplayName => displayName;

    /// <summary>Quedas reais sofridas (registradas pelo <see cref="PartFallDamage"/>, como nas peças do PC).</summary>
    public int DropCount { get; private set; }
    public PartCondition Condition { get; private set; } = PartCondition.Intact;
    /// <summary>Quebrada: não é instalada (o encaixe recusa) e não volta para gaveta.</summary>
    public bool IsBroken => Condition == PartCondition.Broken;
    public event System.Action<IBreakable> BreakableChanged;

    public void RegisterDrop(int dropsToBreak)
    {
        if (IsBroken) return;
        DropCount++;
        Condition = Breakables.AfterDrop(DropCount, dropsToBreak);
        BreakableChanged?.Invoke(this);
        Breakables.Raise(this);
    }
}
