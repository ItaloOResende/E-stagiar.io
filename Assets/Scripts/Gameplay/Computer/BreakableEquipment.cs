using System;
using UnityEngine;

/// <summary>
/// Estado físico de um EQUIPAMENTO que pode quebrar ao cair (monitor, gabinete; preparado para teclado, mouse e outros
/// periféricos). Fica no ROOT LÓGICO do equipamento, junto do <see cref="PickupInteractable"/>, do Rigidbody e de um
/// <see cref="PartFallDamage"/> (que detecta a queda e troca o visual). Não sabe o que o equipamento faz: quem depende
/// dele consulta <see cref="IsBroken"/> (ex.: <see cref="ComputerPower"/> não liga com o gabinete quebrado e o monitor
/// quebrado fica apagado).
/// Para um periférico novo: este componente + <see cref="PartFallDamage"/> (Drops To Break, Broken Visual) no root
/// lógico; a consequência lógica, se houver, é responsabilidade do sistema daquele periférico.
/// </summary>
[DisallowMultipleComponent]
public class BreakableEquipment : MonoBehaviour, IBreakable, IScreenBlocker
{
    /// <summary>
    /// Quebrado não é mais alvo de manutenção: a tela 2D do próprio objeto não abre (o prompt "Manutenção" some).
    /// Continua podendo ser carregado (para tirar do lugar/descartar).
    /// </summary>
    public bool BlocksScreen => IsBroken;

    [Tooltip("Nome mostrado ao jogador nas mensagens (ex.: 'monitor').")]
    [SerializeField] private string displayName = "monitor";

    public string DisplayName => displayName;
    public int DropCount { get; private set; }
    public PartCondition Condition { get; private set; } = PartCondition.Intact;
    public bool IsBroken => Condition == PartCondition.Broken;
    public event Action<IBreakable> BreakableChanged;

    public void RegisterDrop(int dropsToBreak)
    {
        if (IsBroken) return;
        DropCount++;
        Condition = Breakables.AfterDrop(DropCount, dropsToBreak);
        BreakableChanged?.Invoke(this);
        Breakables.Raise(this);
    }
}
