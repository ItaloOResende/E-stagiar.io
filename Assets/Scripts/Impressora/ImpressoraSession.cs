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
/// Estado lógico da impressora: se tem toner, se a bandeja tem papel, e o endereço IP configurado no painel.
/// Fica no objeto da impressora na cena, então fechar e reabrir o painel não perde nada.
///
/// Regras:
///  - A impressora só fica PRONTA com toner instalado, papel na bandeja e um IP válido confirmado
///    (<see cref="RedeConfigurada"/>). Não existe máscara de sub-rede: só o endereço IP.
///  - Digitação com MÁSCARA DE ENTRADA IPv4 (formatação, não máscara de sub-rede): só números; os pontos entram
///    sozinhos. Cada grupo (octeto) vai de 0 a 255, sem zeros à esquerda, e passa ao próximo com 3 dígitos, com "."
///    (fecha o grupo antes), quando o próximo dígito estouraria 255 (ex.: "30" + "0" → "30.0") ou quando o grupo é
///    "0" (um octeto que começa com 0 só pode ser 0: "192168061" → "192.168.0.61"). Apagar volta pelos grupos, então
///    dá para corrigir os números anteriores. O visor mostra o IP como o cartaz escreve (<see cref="FormatarVisor"/>,
///    ex.: 192.168.0.6_), sem zeros artificiais; o IP salvo fica normalizado (192.168.0.61).
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
    /// <summary>IP já confirmado, normalizado (vazio enquanto não configurar).</summary>
    public string IpSalvo { get; private set; } = "";

    // grupos em digitação (1 a 4; o último é o que recebe os dígitos)
    private readonly System.Collections.Generic.List<string> grupos = new System.Collections.Generic.List<string> { "" };

    /// <summary>Texto que o jogador está digitando (ainda não confirmado), com os pontos já inseridos: ex. "192.168.1".</summary>
    public string IpDigitado => grupos.Count == 1 && grupos[0].Length == 0 ? "" : string.Join(".", grupos);
    /// <summary>Texto em digitação (o painel só tem o campo do IP).</summary>
    public string Digitado => IpDigitado;

    /// <summary>Há um IP confirmado.</summary>
    public bool TemIp => IpSalvo.Length > 0;
    /// <summary>A rede da impressora está configurada: há um IP aceito. O painel só aceita o IP vigente
    /// (<see cref="ImpressoraRede"/>, o do cartaz; sem regra, qualquer IP válido) e o jogo só define IPs vigentes, então
    /// um IP novo sorteado para o chamado de OUTRA impressora não desconfigura esta.</summary>
    public bool RedeConfigurada => TemIp;

    /// <summary>Por que o último <see cref="ConfirmarIp"/> foi recusado.</summary>
    public enum RecusaIp { Nenhuma, Incompleto, NaoConfere }
    public RecusaIp UltimaRecusa { get; private set; }
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

    /// <summary>Cenário/defeito: define toner, papel e o IP salvo ("" = sem IP). Limpa a digitação.</summary>
    public void DefinirEstado(bool toner, bool papel, string ip)
    {
        TemToner = toner;
        TemPapel = papel;
        IpSalvo = IpEhValido(ip) ? Normalizar(ip) : "";
        Limpar();
    }

    // ---- painel: digitação com máscara de entrada IPv4

    /// <summary>Digita números e/ou pontos (cada caractere passa pela máscara; o resto é ignorado).</summary>
    public void Digitar(string texto)
    {
        if (string.IsNullOrEmpty(texto)) return;
        foreach (char c in texto)
        {
            if (c == '.' || c == ',') FecharGrupo();
            else if (c >= '0' && c <= '9') DigitarNumero(c);
        }
    }

    private void DigitarNumero(char c)
    {
        string atual = grupos[grupos.Count - 1];
        // grupo completo ("0", 3 dígitos) ou o dígito estouraria 255: o número começa o próximo grupo
        if (GrupoCompleto(atual) || (atual.Length > 0 && int.Parse(atual + c) > 255))
        {
            if (grupos.Count == 4) return;   // último grupo completo: não cabe mais nada
            grupos.Add("");
        }
        grupos[grupos.Count - 1] += c;
        // grupo completo: o ponto entra sozinho (menos no último)
        if (GrupoCompleto(grupos[grupos.Count - 1]) && grupos.Count < 4) grupos.Add("");
    }

    // Octeto que não aceita mais dígitos: 3 dígitos, ou "0" (sem zero à esquerda: 0 seguido de dígito não existe).
    private static bool GrupoCompleto(string g) => g.Length == 3 || g == "0";

    // "." fecha o grupo atual antes dos 3 dígitos (ex.: 192.168.1.): grupo vazio ou o último não fecham
    private void FecharGrupo()
    {
        if (grupos[grupos.Count - 1].Length == 0 || grupos.Count == 4) return;
        grupos.Add("");
    }

    /// <summary>Apaga o último dígito; com o grupo vazio, volta ao anterior (o ponto some junto).</summary>
    public void Apagar()
    {
        int i = grupos.Count - 1;
        if (grupos[i].Length > 0) grupos[i] = grupos[i].Substring(0, grupos[i].Length - 1);
        else if (i > 0)
        {
            grupos.RemoveAt(i);
            string g = grupos[i - 1];
            grupos[i - 1] = g.Substring(0, g.Length - 1);
        }
    }

    public void Limpar()
    {
        grupos.Clear();
        grupos.Add("");
    }

    /// <summary>Recomeça a digitação a partir do IP já salvo (para editar).</summary>
    public void EditarIpSalvo()
    {
        Limpar();
        if (!TemIp) return;
        grupos.Clear();
        grupos.AddRange(IpSalvo.Split('.'));
    }

    /// <summary>
    /// Confirma o IP digitado. Devolve false (e nada muda) se não forem 4 grupos de 0 a 255
    /// (<see cref="RecusaIp.Incompleto"/>) ou se não for o IP exigido na rodada (<see cref="RecusaIp.NaoConfere"/>).
    /// </summary>
    public bool ConfirmarIp()
    {
        UltimaRecusa = RecusaIp.Nenhuma;
        if (!IpEhValido(IpDigitado)) { UltimaRecusa = RecusaIp.Incompleto; return false; }
        if (!ImpressoraRede.Confere(IpDigitado)) { UltimaRecusa = RecusaIp.NaoConfere; return false; }
        IpSalvo = Normalizar(IpDigitado);
        Limpar();
        return true;
    }

    /// <summary>
    /// Visor com a máscara de entrada, no mesmo formato do cartaz (sem zeros à esquerda): os grupos como digitados e
    /// cada grupo que falta (ou o atual, vazio) como "_". Ex.: "" → "_._._._"; "192.168.0." → "192.168.0._";
    /// "192.168.0.61" → "192.168.0.61".
    /// </summary>
    public static string FormatarVisor(string digitado)
    {
        string[] g = string.IsNullOrEmpty(digitado) ? new string[0] : digitado.Split('.');
        var partes = new string[4];
        for (int i = 0; i < 4; i++) partes[i] = i < g.Length && g[i].Length > 0 ? g[i] : "_";
        return string.Join(".", partes);
    }

    /// <summary>IP sem zeros à esquerda (192.168.001.010 → 192.168.1.10). Só para IPs válidos.</summary>
    public static string Normalizar(string ip)
    {
        string[] g = ip.Split('.');
        for (int i = 0; i < g.Length; i++) g[i] = int.Parse(g[i]).ToString();
        return string.Join(".", g);
    }

    /// <summary>IPv4 válido: 4 grupos numéricos de 0 a 255 (até 3 dígitos), separados por ponto.</summary>
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
