using UnityEngine;

/// <summary>
/// Chamados da impressora (<see cref="TicketDeviceBridge"/>) no root de um <c>Impressora_Estacao</c>, como o PC:
/// com <see cref="comecarFuncional"/> a impressora começa com toner e papel reais e o IP configurado, e o DEFEITO do
/// chamado é aplicado ao iniciar o atendimento, só nesta impressora. Peça retirada pelo defeito some (consumida).
/// Defeitos (pelo Ticket Id): sem papel · sem toner · sem IP · sem impressão (sem os três: o cenário antigo).
/// Etapas: inspecionar-impressora = abrir a tela desta impressora (ou do painel) · corrigir-impressora = toner e papel ·
/// repor-papel / trocar-toner = o item corrigido · configurar-ip = um IP válido confirmado ·
/// teste-impressao = pronta (toner, papel e rede) e uma página de teste impressa.
/// Sem <see cref="comecarFuncional"/> (cenas antigas) começa sem toner, papel e IP, como antes.
/// </summary>
[RequireComponent(typeof(ImpressoraEstacao))]
public class ImpressoraTicketBridge : TicketDeviceBridge
{
    public const string SemPapel = "impressora-sem-papel";
    public const string SemToner = "impressora-sem-toner";
    public const string SemIp = "impressora-sem-ip";
    public const string SemImpressao = "impressora-sem-impressao";

    [Header("Estado inicial")]
    [Tooltip("Começa pronta (toner, papel e IP). Os defeitos só aparecem ao iniciar um chamado.")]
    [SerializeField] private bool comecarFuncional;
    [Tooltip("Prefab do cartucho de toner (Pickup_Toner) usado no estado funcional.")]
    [SerializeField] private GameObject tonerPrefab;
    [Tooltip("Prefab da resma de papel (Pickup_ResmaPapel) usado no estado funcional.")]
    [SerializeField] private GameObject papelPrefab;
    [Tooltip("IP do estado funcional quando a cena não sorteia o IP da rodada (sem StageController). Com fase, a " +
             "impressora usa o IP da rodada (ImpressoraRede), o mesmo do cartaz da sala de TI.")]
    [SerializeField] private string ipInicial = "192.168.0.20";
    [Tooltip("Usa o IP vigente (ImpressoraRede, o do cartaz da sala de TI) e sorteia um novo a cada chamado que apaga " +
             "o IP. Outros chamados e o início da fase não mudam o IP.")]
    [SerializeField] private bool sortearIpDaRodada = true;

    private StageController fase;

    private ImpressoraEstacao estacao;
    private int testesNoInicio;

    protected override void OnClaimed(TicketInstance ticket) => testesNoInicio = estacao.TestesOk;

    protected override void Awake()
    {
        base.Awake();
        estacao = GetComponent<ImpressoraEstacao>();
    }

    private void Start()
    {
        fase = sortearIpDaRodada ? FindAnyObjectByType<StageController>() : null;
        string ip = fase != null ? ImpressoraRede.Garantir() : ipInicial;
        if (comecarFuncional) estacao.DefinirEstado(Criar(tonerPrefab), Criar(papelPrefab), ip);
    }

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
        GameObject toner = estacao.TonerGuardado, papel = estacao.PapelGuardado;
        string ip = estacao.Sessao.IpSalvo;
        switch (ticketId)
        {
            case SemPapel: estacao.DefinirEstado(toner, null, ip); break;
            case SemToner: estacao.DefinirEstado(null, papel, ip); break;
            case SemIp: NovoIpDoChamado(); estacao.DefinirEstado(toner, papel, ""); break;          // sem IP
            case SemImpressao: NovoIpDoChamado(); estacao.DefinirEstado(null, null, ""); break;
            default: return;
        }
        Debug.Log($"[Impressora] {DeviceLabel}: defeito '{ticketId}' aplicado.", this);
    }

    // O chamado apagou o IP: cada chamado desses pede um IP novo (nunca o do chamado anterior), que o cartaz da sala de
    // TI mostra durante todo o chamado. Reabrir o cartaz ou errar no painel não sorteia de novo; as outras impressoras,
    // já configuradas, continuam funcionando com o IP delas.
    private void NovoIpDoChamado()
    {
        if (fase != null) ImpressoraRede.Sortear();
    }

    protected override bool Knows(string id) =>
        id == "inspecionar-impressora" || id == "corrigir-impressora" || id == "teste-impressao" ||
        id == "repor-papel" || id == "trocar-toner" || id == "configurar-ip";

    protected override bool Condition(string id)
    {
        ImpressoraSession s = estacao.Sessao;
        switch (id)
        {
            case "inspecionar-impressora": return Inspected;
            case "corrigir-impressora": return s.TemToner && s.TemPapel;
            case "repor-papel": return s.TemPapel;
            case "trocar-toner": return s.TemToner;
            // o IP da rodada (cartaz da sala de TI) confirmado no painel
            case "configurar-ip": return s.RedeConfigurada;
            // teste-impressao: pronta E uma página de teste saiu DEPOIS de assumir o chamado (tela "Teste de impressão")
            default: return s.Pronta && estacao.TestesOk > testesNoInicio;
        }
    }
}
