using System;
using UnityEngine;

/// <summary>Estado físico de uma peça: intacta, já sofreu queda (ainda funciona) ou quebrada (não serve).</summary>
public enum PartCondition { Intact, Damaged, Broken }

/// <summary>
/// Marca um item do mundo (Pickup_PlacaMae, Pickup_MemoriaRAM, Pickup_HD…) como peça interna de PC.
/// Fica no ROOT LÓGICO do item, junto do PickupInteractable. Instalar a peça num gabinete guarda ESTE objeto
/// dentro do <see cref="ComputerCase"/> (desativado, nunca destruído); retirá-la devolve o mesmo objeto à mão.
/// </summary>
public class ComputerPartItem : MonoBehaviour, IBreakable
{
    [Tooltip("Qual componente esta peça é (um só).")]
    [SerializeField] private PcComponent component = PcComponent.Ram;
    [Tooltip("Nome mostrado ao jogador nas mensagens (ex.: 'memória RAM').")]
    [SerializeField] private string displayName = "memória RAM";

    public PcComponent Component => component;
    public string DisplayName => displayName;

    /// <summary>
    /// Só para HD: o sistema operacional está gravado NESTA unidade (estado de jogo, não de Inspector).
    /// Todo HD criado do prefab (gaveta, item novo) começa vazio; o HD que já vinha no gabinete herda o sistema
    /// ao ser retirado (<see cref="ComputerCase"/>). Instalar pelo pendrive grava aqui (<see cref="ComputerPower"/>).
    /// </summary>
    public bool HasOperatingSystem { get; set; }

    /// <summary>Quedas reais sofridas (impactos registrados por <see cref="PartFallDamage"/>).</summary>
    public int DropCount { get; private set; }
    public PartCondition Condition { get; private set; } = PartCondition.Intact;
    public bool IsBroken => Condition == PartCondition.Broken;
    /// <summary>Disparado quando a peça sofre uma queda ou quebra.</summary>
    public event Action<ComputerPartItem> ConditionChanged;
    /// <summary>Igual a <see cref="ConditionChanged"/>, para QUALQUER peça (ex.: registro de infrações, sem precisar
    /// achar e assinar cada peça da cena, inclusive as criadas depois pelas gavetas).</summary>
    public static event Action<ComputerPartItem> AnyConditionChanged;
    /// <summary>Mesmo aviso de <see cref="ConditionChanged"/>, pela interface comum (<see cref="IBreakable"/>).</summary>
    public event Action<IBreakable> BreakableChanged;

    /// <summary>
    /// Registra uma queda. <paramref name="dropsToBreak"/> &gt; 0: quebra ao atingir esse número de quedas
    /// (0 = peça resistente, nunca quebra). Peça quebrada não muda mais.
    /// </summary>
    public void RegisterDrop(int dropsToBreak)
    {
        if (IsBroken) return;
        DropCount++;
        Condition = Breakables.AfterDrop(DropCount, dropsToBreak);
        ConditionChanged?.Invoke(this);
        BreakableChanged?.Invoke(this);
        AnyConditionChanged?.Invoke(this);
        Breakables.Raise(this);
    }
}
