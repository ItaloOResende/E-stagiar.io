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

/// <summary>
/// Estado lógico da impressora: se tem toner, se a bandeja tem papel, e o IP configurado no painel.
/// Fica no objeto da impressora na cena, então fechar e reabrir o painel não perde nada.
///
/// Regras:
///  - A impressora só fica PRONTA com toner instalado, papel na bandeja e um IP válido confirmado.
///  - O IP é digitado no painel (teclado numérico) e só vale depois de confirmar: 4 grupos de 0 a 255.
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

    /// <summary>Há um IP confirmado.</summary>
    public bool TemIp => IpSalvo.Length > 0;
    /// <summary>A impressora está configurada e pronta para imprimir.</summary>
    public bool Pronta => TemToner && TemPapel && TemIp;

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

    /// <summary>Cenário/defeito: define toner, papel e o IP salvo ("" = sem IP). Limpa a digitação.</summary>
    public void DefinirEstado(bool toner, bool papel, string ip)
    {
        TemToner = toner;
        TemPapel = papel;
        IpSalvo = ip ?? "";
        IpDigitado = "";
    }

    // ---- painel: digitação do IP
    public void Digitar(string texto)
    {
        if (string.IsNullOrEmpty(texto)) return;
        if (IpDigitado.Length + texto.Length > MaxDigitos) return;
        IpDigitado += texto;
    }

    public void Apagar()
    {
        if (IpDigitado.Length > 0) IpDigitado = IpDigitado.Substring(0, IpDigitado.Length - 1);
    }

    public void Limpar() => IpDigitado = "";

    /// <summary>Recomeça a digitação a partir do IP já salvo (para editar).</summary>
    public void EditarIpSalvo() => IpDigitado = IpSalvo;

    /// <summary>Confirma o IP digitado. Devolve false (e nada muda) se não for um IPv4 válido.</summary>
    public bool ConfirmarIp()
    {
        if (!IpEhValido(IpDigitado)) return false;
        IpSalvo = IpDigitado;
        return true;
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
