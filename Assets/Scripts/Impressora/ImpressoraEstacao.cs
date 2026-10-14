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

    /// <summary>
    /// Clique num encaixe (toner ou bandeja de papel). Com a peça certa na mão instala (o chamador guarda o
    /// objeto na mão com <c>ItemHolder.Stow</c>); com o encaixe ocupado e a mão livre retira e devolve o objeto
    /// em <paramref name="devolver"/> para o chamador colocar na mão.
    /// </summary>
    public ImpressoraEncaixeResultado UsarEncaixe(ImpressoraParte encaixe, PickupInteractable itemNaMao, out GameObject devolver)
    {
        devolver = null;
        ImpressoraPartItem peca = itemNaMao != null ? itemNaMao.GetComponentInParent<ImpressoraPartItem>() : null;

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
