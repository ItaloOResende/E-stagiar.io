using UnityEngine;

/// <summary>
/// Chamado do cabo de rede (<see cref="TicketDeviceBridge"/>) no root da <c>Caixa_CaboRede</c>. Etapas:
/// preparar-cabo = abrir a crimpagem nesta caixa · crimpar-cabo = um cabo crimpado com sucesso depois de assumir o
/// chamado · testar-cabo = um par CERTO (P N ↔ S N) ligado no rack com um cabo bom crimpado depois de assumir o chamado,
/// e a rede do rack funcionando (sem laço no switch) — estado real da topologia (<see cref="RedeGame"/>): ligar o cabo
/// em portas incompatíveis não conclui.
/// </summary>
public class RedeTicketBridge : TicketDeviceBridge
{
    private int crimpadosNoInicio;
    private float assumidoEm;

    protected override void OnClaimed(TicketInstance ticket)
    {
        RedeGame g = RedeGame.Garantir();
        crimpadosNoInicio = g.TotalCrimpados;
        assumidoEm = Time.time;   // cabo pronto da gaveta ou ligado antes do chamado não conclui este chamado
    }

    protected override bool Knows(string id) => id == "preparar-cabo" || id == "crimpar-cabo" || id == "testar-cabo";

    protected override bool Condition(string id)
    {
        RedeGame g = RedeGame.Garantir();
        switch (id)
        {
            case "preparar-cabo": return Inspected;
            case "crimpar-cabo": return g.TotalCrimpados > crimpadosNoInicio;
            default: return g.ParCrimpadoFuncionandoDesde(assumidoEm);
        }
    }
}
