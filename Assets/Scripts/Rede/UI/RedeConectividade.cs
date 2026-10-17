using UnityEngine;

/// <summary>
/// Regra de conectividade da rede, registrada no <see cref="ComputerPower.NetworkPathCheck"/> (o PC fica noutra
/// assembly e não conhece o rack). O PC tem rede quando, além do cabo nas duas pontas (regra antiga):
///  1. o cabo ligado nele não é defeituoso (<see cref="CaboRedeEstado"/>);
///  2. se o ponto da parede tem porta no rack (<see cref="PontoDeRedeRack"/>), esse par está ligado no rack com um
///     cabo bom (<see cref="RedeGame.PortaFunciona"/>).
/// Cenas sem pontos vinculados e sem cabos defeituosos funcionam exatamente como antes.
/// </summary>
public static class RedeConectividade
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Registrar() => ComputerPower.NetworkPathCheck = Verificar;

    /// <summary>Garante a regra registrada (chamado também por quem depende dela, ex.: os chamados de rede).</summary>
    public static void Garantir()
    {
        if (ComputerPower.NetworkPathCheck == null) Registrar();
    }

    public static bool Verificar(ComputerPower pc)
    {
        if (pc == null) return false;
        if (CaboRedeEstado.EhDefeituoso(pc.CableItemOf(PcLink.Network))) return false;
        int porta = PontoDeRedeRack.PortaDe(pc.WallOf(PcLink.Network));
        if (porta <= 0) return true;
        RedeGame g = RedeGame.Instance;
        return g == null || g.PortaFunciona(porta);
    }

    /// <summary>O rack ou um cabo mudou: todos os PCs reavaliam a rede (tela 2D e monitor).</summary>
    public static void NotificarPcs()
    {
        foreach (ComputerPower pc in Object.FindObjectsByType<ComputerPower>(FindObjectsSortMode.None))
            pc.NotifyNetworkChanged();
    }
}
