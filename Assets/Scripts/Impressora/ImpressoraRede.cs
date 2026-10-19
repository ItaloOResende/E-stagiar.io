using System;
using UnityEngine;

/// <summary>
/// Endereço IP que a impressora da rede DEVE ter nesta rodada: a única referência, lida pelo cartaz da sala de TI
/// (<see cref="CartazIpImpressora"/>) e pela validação do painel (<see cref="ImpressoraSession.ConfirmarIp"/>).
/// Sorteado no início de cada fase (<see cref="ImpressoraTicketBridge"/>, no <c>StageStarted</c>) e de novo a cada
/// chamado que apaga o IP da impressora, sempre diferente do anterior: o jogador precisa ler o cartaz, não decorar.
/// Rede do escritório: 192.168.0.0/24 (a mesma do IP inicial que a impressora já usava); a impressora fica na faixa
/// 192.168.0.20–99. Sem rodada (cenas antigas, testes Edit Mode): <see cref="IpDaRodada"/> vazio = qualquer IPv4
/// válido é aceito, como antes.
/// </summary>
public static class ImpressoraRede
{
    public const string Prefixo = "192.168.0.";
    public const int HostMinimo = 20, HostMaximo = 99;

    /// <summary>IP exigido agora, normalizado ("" = sem regra).</summary>
    public static string IpDaRodada { get; private set; } = "";
    public static bool TemRegra => IpDaRodada.Length > 0;

    /// <summary>O IP da rodada mudou (o cartaz se redesenha).</summary>
    public static event Action<string> Mudou;

    /// <summary>Sorteia um IP novo, diferente do atual.</summary>
    public static string Sortear()
    {
        string novo;
        do novo = Prefixo + UnityEngine.Random.Range(HostMinimo, HostMaximo + 1);
        while (novo == IpDaRodada);
        Definir(novo);
        return novo;
    }

    /// <summary>Garante um IP (sorteia se ainda não houver) e o devolve.</summary>
    public static string Garantir() => TemRegra ? IpDaRodada : Sortear();

    /// <summary>Define o IP da rodada ("" desliga a regra). Para cenários e testes.</summary>
    public static void Definir(string ip)
    {
        string normal = ImpressoraSession.IpEhValido(ip) ? ImpressoraSession.Normalizar(ip) : "";
        if (normal == IpDaRodada) return;
        IpDaRodada = normal;
        Mudou?.Invoke(IpDaRodada);
    }

    /// <summary>O IP (válido) é o exigido nesta rodada? Sem regra, qualquer IP válido confere.</summary>
    public static bool Confere(string ip) =>
        ImpressoraSession.IpEhValido(ip) && (!TemRegra || ImpressoraSession.Normalizar(ip) == IpDaRodada);
}
