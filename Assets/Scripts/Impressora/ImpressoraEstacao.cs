using UnityEngine;

/// <summary>
/// Estado da impressora no mundo (fica no ROOT LÓGICO do objeto da impressora, nunca na tela 2D):
/// toner, papel e IP. A tela 2D só lê e comanda este componente, então fechar e reabrir não perde nada.
/// Os itens instalados ficam guardados aqui (desativados, nunca destruídos) e voltam para a mão ao serem retirados.
/// </summary>
public class ImpressoraEstacao : MonoBehaviour
{
    private ImpressoraSession sessao;
    private GameObject tonerGuardado;
    private GameObject papelGuardado;

    /// <summary>Estado lógico da impressora (criado na primeira consulta e mantido entre aberturas da tela).</summary>
    public ImpressoraSession Sessao => sessao ?? (sessao = new ImpressoraSession());

    /// <summary>O objeto do cartucho guardado (null se não houver).</summary>
    public GameObject TonerGuardado => tonerGuardado;
    /// <summary>O objeto da resma guardada (null se não houver).</summary>
    public GameObject PapelGuardado => papelGuardado;

    private ImpressoraVista? vistaPedida;

    /// <summary>Impressões de teste que saíram (a tela de teste conta; os chamados validam por ela).</summary>
    public int TestesOk { get; private set; }

    /// <summary>
    /// Impressão de teste pedida na tela: por enquanto a única condição verificada é o PAPEL na bandeja (toner/IP
    /// ainda não têm diagnóstico próprio). Devolve se a folha saiu.
    /// </summary>
    public bool ImprimirTeste()
    {
        bool ok = Sessao.TemPapel;
        if (ok) TestesOk++;
        return ok;
    }

    /// <summary>
    /// Cenário/defeito: define o que está instalado. <paramref name="toner"/>/<paramref name="papel"/> são os objetos
    /// reais (guardados aqui, desativados); null = vazio. Peça que saía é destruída (o defeito a consome).
    /// </summary>
    public void DefinirEstado(GameObject toner, GameObject papel, string ip)
    {
        if (tonerGuardado != null && tonerGuardado != toner) Destroy(tonerGuardado);
        if (papelGuardado != null && papelGuardado != papel) Destroy(papelGuardado);
        tonerGuardado = toner;
        papelGuardado = papel;
        Sessao.DefinirEstado(toner != null, papel != null, ip);
    }

    /// <summary>
    /// Pede que a próxima abertura da tela 2D comece nesta vista (ex.: clicar no painel 3D abre direto no teclado do
    /// IP). Vale uma vez: a <see cref="ImpressoraTela"/> consome o pedido ao abrir.
    /// </summary>
    public void PedirVista(ImpressoraVista vista) => vistaPedida = vista;

    /// <summary>Entrega (e apaga) a vista pedida, se houver.</summary>
    public bool ConsumirVistaPedida(out ImpressoraVista vista)
    {
        vista = vistaPedida ?? default;
        bool havia = vistaPedida.HasValue;
        vistaPedida = null;
        return havia;
    }

    /// <summary>
    /// Clique num encaixe (toner ou bandeja de papel). Com a peça certa na mão instala (o chamador guarda o
    /// objeto na mão com <c>ItemHolder.Stow</c>); com o encaixe ocupado e a mão livre retira e devolve o objeto
    /// em <paramref name="devolver"/> para o chamador colocar na mão.
    /// </summary>
    public ImpressoraEncaixeResultado UsarEncaixe(ImpressoraParte encaixe, PickupInteractable itemNaMao, out GameObject devolver)
    {
        devolver = null;
        ImpressoraPartItem peca = itemNaMao != null ? itemNaMao.GetComponentInParent<ImpressoraPartItem>() : null;

        // Peça quebrada na queda não é instalada (retirar a que já está continua valendo).
        bool ocupado = encaixe == ImpressoraParte.Toner ? Sessao.TemToner : encaixe == ImpressoraParte.Papel && Sessao.TemPapel;
        if (!ocupado && peca != null && peca.IsBroken) return ImpressoraEncaixeResultado.PecaQuebrada;

        ImpressoraEncaixeResultado resultado = Sessao.UsarEncaixe(
            encaixe, itemNaMao != null, peca != null ? peca.Parte : ImpressoraParte.Nenhuma);

        if (resultado == ImpressoraEncaixeResultado.Instalado)
        {
            if (encaixe == ImpressoraParte.Toner) tonerGuardado = peca.gameObject;
            else papelGuardado = peca.gameObject;
        }
        else if (resultado == ImpressoraEncaixeResultado.Removido)
        {
            if (encaixe == ImpressoraParte.Toner) { devolver = tonerGuardado; tonerGuardado = null; }
            else { devolver = papelGuardado; papelGuardado = null; }
        }
        return resultado;
    }
}
