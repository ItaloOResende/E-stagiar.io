using UnityEngine;

/// <summary>
/// Chamados do notebook (<see cref="TicketDeviceBridge"/>) no root de um <c>Notebook_Estacao</c>, como o PC:
/// com <see cref="comecarFuncional"/> o notebook começa com RAM e SSD reais (com sistema) e ligado, e o DEFEITO do
/// chamado é aplicado ao iniciar o atendimento, só neste notebook. Peça retirada pelo defeito some (como no PC).
/// Defeitos (pelo Ticket Id): sem RAM · sem SSD · sem sistema · sem boot (sem RAM e sem SSD: o cenário antigo).
/// Etapas: diagnosticar-notebook = abrir a tela deste notebook · reparar-notebook = RAM e SSD instalados ·
/// instalar-ram-notebook / instalar-ssd-notebook / instalar-sistema-notebook = a peça/o sistema no lugar ·
/// validar-notebook = notebook no sistema operacional.
/// Sem <see cref="comecarFuncional"/> (cenas antigas) o notebook começa vazio, como antes.
/// </summary>
[RequireComponent(typeof(NotebookEstacao))]
public class NotebookTicketBridge : TicketDeviceBridge
{
    public const string SemRam = "notebook-sem-ram";
    public const string SemSsd = "notebook-sem-ssd";
    public const string SemSistema = "notebook-sem-sistema";
    public const string SemBoot = "notebook-sem-boot";

    [Header("Estado inicial")]
    [Tooltip("Começa funcionando (RAM + SSD com sistema, ligado). Os defeitos só aparecem ao iniciar um chamado.")]
    [SerializeField] private bool comecarFuncional;
    [Tooltip("Prefab da memória SO-DIMM (Pickup_RamSodimm) usada no estado funcional.")]
    [SerializeField] private GameObject ramPrefab;
    [Tooltip("Prefab do SSD (Pickup_Ssd) usado no estado funcional.")]
    [SerializeField] private GameObject ssdPrefab;

    private NotebookEstacao estacao;

    protected override void Awake()
    {
        base.Awake();
        estacao = GetComponent<NotebookEstacao>();
    }

    private void Start()
    {
        if (!comecarFuncional) return;
        estacao.DefinirPecas(Criar(ramPrefab), Criar(ssdPrefab), true);
        if (!estacao.Sessao.Ligado) estacao.PressPower();
    }

    // Peça real guardada no notebook (desativada, "no equipamento"), como os itens que o jogador instala.
    private GameObject Criar(GameObject prefab)
    {
        if (prefab == null) return null;
        GameObject item = Instantiate(prefab, transform.position, transform.rotation);
        item.name = prefab.name;
        PickupInteractable pickup = item.GetComponentInChildren<PickupInteractable>(true);
        if (pickup != null) pickup.MarkInEquipment();
        item.SetActive(false);
        return item;
    }

    protected override void PrepareProblem(string ticketId)
    {
        GameObject ram = estacao.Instalada(NotebookParte.Ram), ssd = estacao.Instalada(NotebookParte.Ssd);
        bool sistema = estacao.Sessao.SsdTemSistema;
        switch (ticketId)
        {
            case SemRam: estacao.DefinirPecas(null, ssd, sistema); break;
            case SemSsd: estacao.DefinirPecas(ram, null, false); break;
            case SemSistema: estacao.DefinirPecas(ram, ssd, false); break;
            case SemBoot: estacao.DefinirPecas(null, null, false); break;
            default: return;
        }
        Debug.Log($"[Notebook] {DeviceLabel}: defeito '{ticketId}' aplicado.", this);
    }

    // Notebook com parafuso da tampa de baixo fora não é entregue (tampa solta): o chamado não conclui e a tentativa é penalizada.
    protected override string DeliveryBlocker() =>
        estacao.Sessao.TodosParafusosInstalados ? null : "o notebook ainda está aberto: recoloque os parafusos da tampa";

    protected override bool Knows(string id) =>
        id == "diagnosticar-notebook" || id == "reparar-notebook" || id == "validar-notebook" ||
        id == "instalar-ram-notebook" || id == "instalar-ssd-notebook" || id == "instalar-sistema-notebook";

    protected override bool Condition(string id)
    {
        NotebookSession s = estacao.Sessao;
        switch (id)
        {
            case "diagnosticar-notebook": return Inspected;
            case "reparar-notebook": return s.TemRam && s.TemSsd;
            case "instalar-ram-notebook": return s.TemRam;
            case "instalar-ssd-notebook": return s.TemSsd;
            case "instalar-sistema-notebook": return s.TemSistema;
            default: return s.Tela == NotebookEstadoTela.Sistema;
        }
    }
}
