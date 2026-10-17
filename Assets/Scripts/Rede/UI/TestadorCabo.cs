using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Regras do testador de cabos que valem fora da tela: o resultado do teste (só o estado REAL do cabo,
/// <see cref="CaboRedeEstado"/>) e o bônus no <see cref="ScoreKeeper"/>. O teste NÃO é etapa de chamado: é opcional.
/// Bônus (valor <c>cableTestBonus</c> do ScoreKeeper; no máximo uma vez por chamado e por tipo):
///  - diagnóstico: testar o cabo que o chamado deixou defeituoso (<see cref="CaboRedeEstado.Chamado"/>) e ver a falha;
///  - verificação: testar um cabo feito na crimpagem (<see cref="CaboCrimpado"/>) e ele passar, com um chamado de rede
///    em atendimento. Cada cabo só verifica uma vez.
/// </summary>
public static class TestadorCabo
{
    public const string ChamadoCaboEstacao = "pc-cabo-rede-defeituoso";
    public const string ChamadoRack = "pc-sem-rede-rack";
    public const string ChamadoCrimpagem = "crimpar-cabo";

    private const string Diagnostico = "teste-diagnostico";
    private const string Verificacao = "teste-verificacao";

    private static readonly HashSet<GameObject> verificados = new HashSet<GameObject>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Zerar() => verificados.Clear();

    /// <summary>Quantos bônus de teste o chamado permite (entra na pontuação possível da fase).</summary>
    public static int BonusPossiveis(string ticketId)
    {
        switch (ticketId)
        {
            case ChamadoCaboEstacao: return 2;   // diagnosticar o cabo velho + verificar o novo
            case ChamadoRack: return 1;          // verificar o cabo novo antes de ligar no rack
            case ChamadoCrimpagem: return 1;     // verificar o cabo crimpado
            default: return 0;
        }
    }

    /// <summary>O cabo passa no teste? (sem defeito registrado)</summary>
    public static bool Aprovado(GameObject cabo) => !CaboRedeEstado.EhDefeituoso(cabo);

    /// <summary>Um teste terminou: concede o bônus que couber (nunca repetido).</summary>
    public static void RegistrarTeste(GameObject cabo, bool aprovado)
    {
        if (cabo == null) return;
        ScoreKeeper score = Object.FindAnyObjectByType<ScoreKeeper>();
        TicketManager tickets = Object.FindAnyObjectByType<TicketManager>();
        if (score == null || tickets == null) return;

        if (!aprovado)
        {
            CaboRedeEstado estado = cabo.GetComponentInParent<CaboRedeEstado>(true);
            TicketInstance t = estado != null && estado.Chamado > 0 ? tickets.Find(estado.Chamado) : null;
            if (t != null && t.Status == TicketStatus.InProgress)
                score.AwardTicketBonus(t, Diagnostico, "Testador: cabo defeituoso diagnosticado");
            return;
        }

        if (cabo.GetComponentInParent<CaboCrimpado>(true) == null || verificados.Contains(cabo)) return;
        foreach (TicketInstance t in tickets.Tickets)
        {
            if (t.Status != TicketStatus.InProgress || t.Definition == null || BonusPossiveis(t.Definition.ticketId) == 0) continue;
            if (score.AwardTicketBonus(t, Verificacao, "Testador: cabo crimpado verificado antes de instalar"))
            {
                verificados.Add(cabo);
                return;
            }
        }
    }
}
