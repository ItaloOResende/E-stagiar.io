using UnityEngine;

/// <summary>
/// Chamado do cabo de rede (<see cref="TicketDeviceBridge"/>) no root da <c>Caixa_CaboRede</c>. Etapas:
/// preparar-cabo = abrir a crimpagem nesta caixa · crimpar-cabo = um cabo crimpado com sucesso depois de assumir o
/// chamado · testar-cabo = um par novo ligado no rack (<see cref="RedeGame"/>). Cada chamado conta a partir dos totais
/// de quando foi assumido.
/// </summary>
public class RedeTicketBridge : TicketDeviceBridge
{
    private int crimpadosNoInicio, ligadosNoInicio;

    protected override void OnClaimed(TicketInstance ticket)
    {
        RedeGame g = RedeGame.Garantir();
        crimpadosNoInicio = g.TotalCrimpados;
        ligadosNoInicio = g.TotalParesComCaboCrimpado;   // cabo pronto da gaveta no rack não conclui este chamado
    }

    protected override bool Knows(string id) => id == "preparar-cabo" || id == "crimpar-cabo" || id == "testar-cabo";

    protected override bool Condition(string id)
    {
        RedeGame g = RedeGame.Garantir();
        switch (id)
        {
            case "preparar-cabo": return Inspected;
            case "crimpar-cabo": return g.TotalCrimpados > crimpadosNoInicio;
            default: return g.TotalParesComCaboCrimpado > ligadosNoInicio;
        }
    }
}
