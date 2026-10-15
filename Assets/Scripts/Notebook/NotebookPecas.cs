using UnityEngine;

/// <summary>Peça que o notebook aceita (memória SO-DIMM menor e o SSD). Nada de placa-mãe, fonte ou cooler.</summary>
public enum NotebookParte { Nenhuma, Ram, Ssd }

/// <summary>
/// Marca um item do mundo (Pickup_RamSodimm, Pickup_Ssd) como peça de notebook. Fica no ROOT LÓGICO do item,
/// junto do <see cref="PickupInteractable"/>. Instalar guarda ESTE objeto dentro do notebook (desativado, nunca
/// destruído); retirar devolve o mesmo objeto à mão.
/// </summary>
public class NotebookPartItem : MonoBehaviour, IBreakable
{
    [Tooltip("Qual peça do notebook este item é (uma só).")]
    [SerializeField] private NotebookParte parte = NotebookParte.Ram;
    [Tooltip("Nome mostrado ao jogador nas mensagens.")]
    [SerializeField] private string displayName = "memória RAM (notebook)";
    [Tooltip("Só para o SSD: o sistema operacional está gravado NESTA unidade (estado de jogo, não de Inspector).")]
    [SerializeField] private bool temSistema;

    public NotebookParte Parte => parte;
    public string DisplayName => displayName;

    /// <summary>Só para o SSD: sistema gravado nesta unidade. Todo SSD novo começa vazio.</summary>
    public bool TemSistema
    {
        get => temSistema;
        set => temSistema = value;
    }

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
