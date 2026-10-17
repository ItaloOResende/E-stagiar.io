/// <summary>Resultado de clicar num encaixe da impressora (toner ou bandeja de papel).</summary>
public enum ImpressoraEncaixeResultado
{
    /// <summary>A peça da mão foi instalada (o chamador tira da mão e guarda o objeto).</summary>
    Instalado,
    /// <summary>A peça foi retirada; o objeto volta para a mão.</summary>
    Removido,
    /// <summary>Encaixe vazio e nada (ou não uma peça) na mão.</summary>
    PrecisaPeca,
    /// <summary>Encaixe vazio e a peça da mão é de outro tipo.</summary>
    PecaErrada,
    /// <summary>Para retirar uma peça, a mão precisa estar livre.</summary>
    MaoCheia,
    /// <summary>Encaixe vazio e a peça da mão está QUEBRADA (caiu): não é instalada.</summary>
    PecaQuebrada
}

/// <summary>Campo do painel de rede que recebe a digitação.</summary>
public enum ImpressoraCampoRede { Ip, Mascara }

/// <summary>
/// Estado lógico da impressora: se tem toner, se a bandeja tem papel, e o IP e a máscara configurados no painel.
/// Fica no objeto da impressora na cena, então fechar e reabrir o painel não perde nada.
///
/// Regras:
///  - A impressora só fica PRONTA com toner instalado, papel na bandeja e a rede configurada (<see cref="RedeConfigurada"/>):
///    IP válido E máscara de sub-rede válida confirmados, e o IP é um endereço de HOST naquela máscara (não é o endereço
///    da rede nem o de broadcast).
///  - IP e máscara são digitados no painel (teclado numérico; o campo ativo recebe as teclas) e só valem depois de
///    confirmar: 4 grupos de 0 a 255; a máscara com os bits 1 contíguos (ex.: 255.255.255.0), de /8 a /30.
/// Sem dependência de UI, modelo, tag ou nome.
/// </summary>
public class ImpressoraSession
{
    /// <summary>Tamanho máximo do IP digitado (255.255.255.255).</summary>
    public const int MaxDigitos = 15;

    /// <summary>Cartucho de toner instalado.</summary>
    public bool TemToner { get; private set; }
    /// <summary>Papel carregado na bandeja.</summary>
    public bool TemPapel { get; private set; }
    /// <summary>Texto que o jogador está digitando no painel (ainda não confirmado).</summary>
    public string IpDigitado { get; private set; } = "";
    /// <summary>IP já confirmado (vazio enquanto não configurar).</summary>
    public string IpSalvo { get; private set; } = "";

    /// <summary>Máscara de sub-rede sendo digitada (ainda não confirmada).</summary>
    public string MascaraDigitada { get; private set; } = "";
    /// <summary>Máscara confirmada (vazia enquanto não configurar).</summary>
    public string MascaraSalva { get; private set; } = "";
    /// <summary>Campo que recebe as teclas do painel.</summary>
    public ImpressoraCampoRede CampoAtivo { get; private set; } = ImpressoraCampoRede.Ip;

    /// <summary>Máscara usada quando um cenário define só o IP (estado funcional antigo).</summary>
    public const string MascaraPadrao = "255.255.255.0";

    /// <summary>Há um IP confirmado.</summary>
    public bool TemIp => IpSalvo.Length > 0;
    /// <summary>Há uma máscara confirmada.</summary>
    public bool TemMascara => MascaraSalva.Length > 0;
    /// <summary>IP e máscara confirmados e coerentes (o IP é um host válido naquela sub-rede).</summary>
    public bool RedeConfigurada => TemIp && TemMascara && IpCompativel(IpSalvo, MascaraSalva);
    /// <summary>A impressora está configurada e pronta para imprimir.</summary>
    public bool Pronta => TemToner && TemPapel && RedeConfigurada;

    /// <summary>Clique num encaixe (toner ou bandeja): vazio + peça certa na mão instala; ocupado + mão livre retira.</summary>
    public ImpressoraEncaixeResultado UsarEncaixe(ImpressoraParte encaixe, bool temItemNaMao, ImpressoraParte pecaNaMao)
    {
        if (encaixe != ImpressoraParte.Toner && encaixe != ImpressoraParte.Papel)
            return ImpressoraEncaixeResultado.PecaErrada;

        bool ocupado = encaixe == ImpressoraParte.Toner ? TemToner : TemPapel;
        if (ocupado)
        {
            if (temItemNaMao) return ImpressoraEncaixeResultado.MaoCheia;
            if (encaixe == ImpressoraParte.Toner) TemToner = false;
            else TemPapel = false;
            return ImpressoraEncaixeResultado.Removido;
        }

        if (!temItemNaMao) return ImpressoraEncaixeResultado.PrecisaPeca;
        if (pecaNaMao != encaixe) return ImpressoraEncaixeResultado.PecaErrada;

        if (encaixe == ImpressoraParte.Toner) TemToner = true;
        else TemPapel = true;
        return ImpressoraEncaixeResultado.Instalado;
    }

    /// <summary>
    /// Cenário/defeito: define toner, papel e o IP salvo ("" = sem IP). A máscara acompanha: sem IP fica sem máscara;
    /// com IP, mantém a máscara salva (ou usa a <see cref="MascaraPadrao"/>). Limpa a digitação.
    /// </summary>
    public void DefinirEstado(bool toner, bool papel, string ip)
    {
        string mascara = string.IsNullOrEmpty(ip) ? "" : (MascaraSalva.Length > 0 ? MascaraSalva : MascaraPadrao);
        DefinirEstado(toner, papel, ip, mascara);
    }

    /// <summary>Cenário/defeito: define toner, papel, IP e máscara salvos ("" = não configurado). Limpa a digitação.</summary>
    public void DefinirEstado(bool toner, bool papel, string ip, string mascara)
    {
        TemToner = toner;
        TemPapel = papel;
        IpSalvo = ip ?? "";
        MascaraSalva = mascara ?? "";
        IpDigitado = "";
        MascaraDigitada = "";
        CampoAtivo = ImpressoraCampoRede.Ip;
    }

    // ---- painel: digitação (no campo ativo)
    /// <summary>Troca o campo que recebe as teclas (clique no campo ou Tab).</summary>
    public void SelecionarCampo(ImpressoraCampoRede campo) => CampoAtivo = campo;

    /// <summary>Texto em digitação do campo ativo.</summary>
    public string Digitado => CampoAtivo == ImpressoraCampoRede.Ip ? IpDigitado : MascaraDigitada;

    private void SetDigitado(string v)
    {
        if (CampoAtivo == ImpressoraCampoRede.Ip) IpDigitado = v;
        else MascaraDigitada = v;
    }

    public void Digitar(string texto)
    {
        if (string.IsNullOrEmpty(texto)) return;
        if (Digitado.Length + texto.Length > MaxDigitos) return;
        SetDigitado(Digitado + texto);
    }

    public void Apagar()
    {
        if (Digitado.Length > 0) SetDigitado(Digitado.Substring(0, Digitado.Length - 1));
    }

    public void Limpar() => SetDigitado("");

    /// <summary>Recomeça a digitação a partir do IP já salvo (para editar).</summary>
    public void EditarIpSalvo() => IpDigitado = IpSalvo;

    /// <summary>Confirma o IP digitado. Devolve false (e nada muda) se não for um IPv4 válido.</summary>
    public bool ConfirmarIp()
    {
        if (!IpEhValido(IpDigitado)) return false;
        IpSalvo = IpDigitado;
        IpDigitado = "";
        return true;
    }

    /// <summary>Confirma a máscara digitada. Devolve false (e nada muda) se não for uma máscara válida.</summary>
    public bool ConfirmarMascara()
    {
        if (!MascaraEhValida(MascaraDigitada)) return false;
        MascaraSalva = MascaraDigitada;
        MascaraDigitada = "";
        return true;
    }

    /// <summary>
    /// Máscara de sub-rede válida: IPv4 válido com os bits 1 contíguos à esquerda (ex.: 255.255.255.0 = /24), de /8 a /30
    /// (0.0.0.0, 255.255.255.255 e /31 não deixam hosts para a impressora).
    /// </summary>
    public static bool MascaraEhValida(string mascara)
    {
        if (!IpEhValido(mascara)) return false;
        uint m = ParaNumero(mascara);
        uint invertida = ~m;
        if ((invertida & (invertida + 1)) != 0) return false;   // zeros só à direita (bits 1 contíguos)
        int prefixo = Prefixo(m);
        return prefixo >= 8 && prefixo <= 30;
    }

    /// <summary>O IP é um endereço de HOST na sub-rede da máscara (nem o endereço da rede, nem o de broadcast).</summary>
    public static bool IpCompativel(string ip, string mascara)
    {
        if (!IpEhValido(ip) || !MascaraEhValida(mascara)) return false;
        uint a = ParaNumero(ip), m = ParaNumero(mascara);
        uint host = a & ~m;
        return host != 0 && host != (~m);
    }

    /// <summary>Tamanho do prefixo (/N) de uma máscara válida (0 se inválida).</summary>
    public static int PrefixoDe(string mascara) => MascaraEhValida(mascara) ? Prefixo(ParaNumero(mascara)) : 0;

    private static int Prefixo(uint m)
    {
        int n = 0;
        for (int i = 31; i >= 0 && ((m >> i) & 1u) == 1u; i--) n++;
        return n;
    }

    private static uint ParaNumero(string ip)
    {
        string[] g = ip.Split('.');
        uint v = 0;
        for (int i = 0; i < 4; i++) v = (v << 8) | uint.Parse(g[i]);
        return v;
    }

    /// <summary>IPv4 válido: 4 grupos numéricos de 0 a 255, separados por ponto.</summary>
    public static bool IpEhValido(string ip)
    {
        if (string.IsNullOrEmpty(ip)) return false;
        string[] grupos = ip.Split('.');
        if (grupos.Length != 4) return false;
        foreach (string grupo in grupos)
        {
            if (grupo.Length == 0 || grupo.Length > 3) return false;
            for (int i = 0; i < grupo.Length; i++)
            {
                char c = grupo[i];
                if (c < '0' || c > '9') return false;
            }
            if (!int.TryParse(grupo, out int valor) || valor > 255) return false;
        }
        return true;
    }
}
