using UnityEngine;

/// <summary>
/// "Tipo" de um item pegável para saber em que gaveta ele pode ser guardado: identificado pelos componentes de
/// gameplay do item (cabo de tal tipo, peça tal, ferramenta tal, pendrive), nunca por nome ou tag.
/// Itens sem nenhum desses componentes, ou peças quebradas, não têm tipo (não são guardados em gavetas).
/// </summary>
public static class ItemKind
{
    public static string Of(Component item)
    {
        if (item == null) return null;
        GameObject g = item.gameObject;
        if (g.TryGetComponent(out CableItem cable)) return "cabo:" + cable.Type;
        // peça quebrada não tem tipo: não volta para a gaveta como se fosse uma peça funcionando
        if (g.TryGetComponent(out ComputerPartItem part)) return part.IsBroken ? null : "peca:" + part.Component;
        if (g.TryGetComponent(out ToolItem tool)) return "ferramenta:" + tool.Type;
        if (g.TryGetComponent(out PendriveItem _)) return "pendrive";
        return null;
    }
}
