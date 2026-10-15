using System;
using UnityEngine;

/// <summary>
/// Algo do mundo que sofre quedas e pode quebrar: peças internas do PC (<see cref="ComputerPartItem"/>) e equipamentos
/// (<see cref="BreakableEquipment"/>: monitor, gabinete; no futuro teclado, mouse e outros periféricos).
/// O <see cref="PartFallDamage"/> detecta a queda física e chama <see cref="RegisterDrop"/>; quem implementa guarda o
/// estado (intacto → sofreu queda → quebrado) e avisa por <see cref="BreakableChanged"/> e <see cref="Breakables"/>.
/// </summary>
public interface IBreakable
{
    GameObject gameObject { get; }
    Transform transform { get; }
    /// <summary>Nome para mensagens (ex.: "monitor", "memória RAM").</summary>
    string DisplayName { get; }
    /// <summary>Quedas reais sofridas.</summary>
    int DropCount { get; }
    PartCondition Condition { get; }
    bool IsBroken { get; }
    /// <summary>Registra uma queda. <paramref name="dropsToBreak"/> &gt; 0: quebra ao atingir esse número (0 = nunca quebra).</summary>
    void RegisterDrop(int dropsToBreak);
    /// <summary>Este objeto sofreu uma queda ou quebrou.</summary>
    event Action<IBreakable> BreakableChanged;
}

/// <summary>Ponto único para quem precisa saber de QUALQUER queda/quebra (ex.: registro de infrações).</summary>
public static class Breakables
{
    /// <summary>Qualquer <see cref="IBreakable"/> sofreu uma queda ou quebrou (inclusive os criados depois pelas gavetas).</summary>
    public static event Action<IBreakable> AnyConditionChanged;

    /// <summary>Estado depois de mais uma queda (regra comum a peças e equipamentos).</summary>
    public static PartCondition AfterDrop(int dropCount, int dropsToBreak) =>
        dropsToBreak > 0 && dropCount >= dropsToBreak ? PartCondition.Broken : PartCondition.Damaged;

    /// <summary>Chamado por quem implementa <see cref="IBreakable"/> depois de mudar de estado.</summary>
    public static void Raise(IBreakable breakable) => AnyConditionChanged?.Invoke(breakable);
}
