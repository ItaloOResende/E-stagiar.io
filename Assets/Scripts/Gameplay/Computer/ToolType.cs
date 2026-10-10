/// <summary>
/// Tipos de ferramenta reconhecidos pela lógica de gameplay. Um item do mundo vira "ferramenta"
/// ao receber o componente <see cref="ToolItem"/> (nunca por nome ou tag).
/// </summary>
public enum ToolType
{
    /// <summary>Mãos vazias ou item que não é ferramenta.</summary>
    None = 0,
    Screwdriver = 1,
    /// <summary>Qualquer outra ferramenta (não serve para parafusos).</summary>
    Other = 99
}
