using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Estado do notebook no mundo (fica no ROOT LÓGICO do objeto do notebook, nunca na tela 2D): parafusos,
/// peças instaladas (RAM SO-DIMM e SSD) e o pendrive. A tela 2D só lê e comanda este componente, então
/// fechar e reabrir a tela não perde o progresso. Os itens instalados ficam guardados aqui (desativados,
/// nunca destruídos) e voltam para a mão ao serem retirados.
/// </summary>
public class NotebookEstacao : MonoBehaviour
{
    [Tooltip("Tempo (s) da instalação do sistema pelo pendrive (tela de progresso). 0 = instantâneo.")]
    [Min(0f)] [SerializeField] private float duracaoInstalacao = 6f;
    [Tooltip("Quantas etapas visuais a instalação tem.")]
    [Min(1)] [SerializeField] private int etapasInstalacao = 5;
    [Tooltip("Quantos parafusos prendem a tampa de baixo.")]
    [Min(1)] [SerializeField] private int parafusos = 6;

    private NotebookSession sessao;
    private readonly Dictionary<NotebookParte, GameObject> instaladas = new Dictionary<NotebookParte, GameObject>();
    private GameObject pendriveGuardado;

    /// <summary>Estado lógico do notebook (criado na primeira consulta e mantido entre aberturas da tela).</summary>
    public NotebookSession Sessao
    {
        get
        {
            if (sessao == null)
            {
                sessao = new NotebookSession(parafusos)
                {
                    DuracaoInstalacao = duracaoInstalacao,
                    EtapasInstalacao = etapasInstalacao
                };
            }
            return sessao;
        }
    }

    /// <summary>O objeto do item instalado num encaixe (null se o encaixe está vazio).</summary>
    public GameObject Instalada(NotebookParte encaixe) =>
        instaladas.TryGetValue(encaixe, out GameObject item) ? item : null;

    /// <summary>O pendrive guardado na USB (null se não houver).</summary>
    public GameObject PendriveGuardado => pendriveGuardado;

    private void Update()
    {
        // a instalação só anda com o jogador olhando o notebook (no 3D ou pela tela dele)
        if (sessao != null && sessao.Instalando && MonitorWatchCheck.Vendo(transform, gameObject)) sessao.Avancar(Time.deltaTime);
    }

    public NotebookParafusoResultado RemoverParafuso(int indice, ToolType ferramenta) =>
        Sessao.RemoverParafuso(indice, ferramenta);

    /// <summary>Clique num furo vazio: recoloca o parafuso (precisa da chave de fenda na mão).</summary>
    public NotebookParafusoResultado InstalarParafuso(int indice, ToolType ferramenta) =>
        Sessao.InstalarParafuso(indice, ferramenta);

    public NotebookAberturaResultado Abrir() => Sessao.Abrir();

    /// <summary>
    /// Clique num encaixe (RAM ou SSD). Com a peça certa na mão instala (o chamador guarda o objeto na mão
    /// com <c>ItemHolder.Stow</c>); com o encaixe ocupado e a mão livre retira e devolve o objeto em
    /// <paramref name="devolver"/> para o chamador colocar na mão.
    /// </summary>
    public NotebookEncaixeResultado UsarEncaixe(NotebookParte encaixe, PickupInteractable itemNaMao, out GameObject devolver)
    {
        devolver = null;
        NotebookPartItem peca = itemNaMao != null ? itemNaMao.GetComponentInParent<NotebookPartItem>() : null;
        bool tinhaSistema = Sessao.SsdTemSistema;

        // Peça quebrada na queda não é instalada (o encaixe vazio recusa; retirar a que já está continua valendo).
        bool ocupado = encaixe == NotebookParte.Ram ? Sessao.TemRam : encaixe == NotebookParte.Ssd && Sessao.TemSsd;
        if (!ocupado && peca != null && peca.IsBroken && Sessao.Estado == NotebookEstado.Aberto)
            return NotebookEncaixeResultado.PecaQuebrada;

        NotebookEncaixeResultado resultado = Sessao.UsarEncaixe(
            encaixe, itemNaMao != null, peca != null ? peca.Parte : NotebookParte.Nenhuma);

        if (resultado == NotebookEncaixeResultado.Instalado)
        {
            instaladas[encaixe] = peca.gameObject;
            if (encaixe == NotebookParte.Ssd) Sessao.DefinirSistemaNoSsd(peca.TemSistema);
        }
        else if (resultado == NotebookEncaixeResultado.Removido)
        {
            if (instaladas.TryGetValue(encaixe, out GameObject guardado))
            {
                instaladas.Remove(encaixe);
                // O sistema (se houver) viaja com a unidade, igual ao HD do PC.
                if (encaixe == NotebookParte.Ssd && guardado != null)
                {
                    NotebookPartItem p = guardado.GetComponentInParent<NotebookPartItem>(true);
                    if (p != null) p.TemSistema = tinhaSistema;
                }
                devolver = guardado;
            }
        }
        return resultado;
    }

    /// <summary>Clique na USB da lateral: pendrive na mão conecta; com o pendrive preso e a mão livre devolve.</summary>
    public NotebookUsbResultado UsarUsb(PickupInteractable itemNaMao, out GameObject devolver)
    {
        devolver = null;
        PendriveItem pendrive = itemNaMao != null ? itemNaMao.GetComponentInParent<PendriveItem>() : null;

        if (Sessao.PendriveConectado)
        {
            if (itemNaMao != null) return NotebookUsbResultado.MaoCheia;
            devolver = pendriveGuardado;
            pendriveGuardado = null;
            Sessao.DesconectarPendrive();
            return NotebookUsbResultado.Devolvido;
        }

        if (pendrive == null) return itemNaMao != null ? NotebookUsbResultado.ItemErrado : NotebookUsbResultado.PrecisaPendrive;

        pendriveGuardado = pendrive.gameObject;
        Sessao.ConectarPendrive();
        return NotebookUsbResultado.Conectado;
    }

    public NotebookBotaoResultado PressPower() => Sessao.PressPower();

    /// <summary>
    /// Cenário/defeito: define as peças do notebook. <paramref name="ram"/>/<paramref name="ssd"/> são os objetos reais
    /// instalados (guardados aqui, desativados, como os que o jogador instala); null = encaixe vazio. Peça que saía do
    /// encaixe é destruída (o defeito a consome, como no PC). Nunca mexe em peças que continuam no lugar.
    /// </summary>
    public void DefinirPecas(GameObject ram, GameObject ssd, bool ssdTemSistema)
    {
        Trocar(NotebookParte.Ram, ram);
        Trocar(NotebookParte.Ssd, ssd);
        if (ssd != null)
        {
            NotebookPartItem p = ssd.GetComponentInParent<NotebookPartItem>(true);
            if (p != null) p.TemSistema = ssdTemSistema;
        }
        Sessao.DefinirPecas(ram != null, ssd != null, ssdTemSistema);
    }

    private void Trocar(NotebookParte parte, GameObject novo)
    {
        GameObject atual = Instalada(parte);
        if (atual == novo) return;
        if (atual != null) Destroy(atual);
        if (novo != null) instaladas[parte] = novo;
        else instaladas.Remove(parte);
    }
}
