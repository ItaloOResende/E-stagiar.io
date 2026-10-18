using UnityEngine;

/// <summary>
/// Chamados de PC (<see cref="TicketDeviceBridge"/>) no root lógico do <c>Gabinete</c> de uma estação.
/// Etapas reconhecidas (ids da ficha, <c>TicketCatalogSetup</c>):
///  - inspecionar-pc / inspecionar-video / verificar-rede / verificar-armazenamento: abrir a manutenção deste gabinete;
///  - abrir-gabinete: tampa aberta · fechar-gabinete: fechado com os parafusos obrigatórios;
///  - instalar-placa-mae / -fonte / -cooler / -ram / -hd: a peça está no gabinete;
///  - conectar-energia: energia do PC e do monitor · conectar-video: VGA ou HDMI · conectar-rede: rede;
///  - instalar-sistema: o HD tem sistema · ligar-pc: PC ligado (BIOS ou sistema);
///  - validar-funcionamento / validar-sistema / validar-armazenamento: no sistema operacional ·
///    validar-imagem: no sistema com vídeo · validar-rede: no sistema com rede.
/// Defeito criado ao assumir (pelo nome do chamado; o PC deve começar funcional): sem RAM, sem HD, peças ausentes
/// (RAM + HD), sem sistema (HD sem SO), sem energia / sem rede / sem vídeo (cabo solto na ponta da parede ou do
/// gabinete), montagem completa (sem peças e sem cabos de energia e vídeo), cabo de rede defeituoso / crimpar-cabo
/// (o cabo de rede DESTA estação estraga; no crimpar-cabo a estação só volta com um cabo novo crimpado) e rack (o par
/// do ponto de rede desta estação desligado). A rede só volta pelo estado real: cabo bom nas duas pontas E par do rack
/// ligado (ComputerPower.NetworkConnected); corrigir só um dos lados não conclui.
/// Ao concluir com o gabinete incompleto (tampa aberta ou parafuso obrigatório fora) avisa o <see cref="ScoreKeeper"/>.
/// </summary>
[RequireComponent(typeof(ComputerCase), typeof(ComputerPower))]
public class PcTicketBridge : TicketDeviceBridge
{
    /// <summary>Como fica o cabo nos chamados de cabo (energia, vídeo, rede). Valores novos sempre no fim.</summary>
    public enum CableDefect
    {
        /// <summary>Solto na parede (tomada/ponto de rede) ou, no vídeo, na saída do gabinete: pendurado à vista.</summary>
        LooseAtWall,
        /// <summary>Ligado na parede/gabinete, mas solto na ponta do equipamento (gabinete; no vídeo, o monitor).</summary>
        LooseAtDevice,
        /// <summary>O cabo sumiu da estação: é preciso buscar outro no gaveteiro de cabos da sala de TI.</summary>
        Missing
    }

    [Tooltip("Penalidade de gabinete incompleto (valor no ScoreKeeper). Vazio = procura na cena.")]
    [SerializeField] private ScoreKeeper scoreKeeper;
    [Header("Defeitos de cabo (pc-sem-energia, monitor-sem-imagem, pc-sem-rede, pc-sem-video-e-rede)")]
    [Tooltip("Peso de 'solto na parede' no rodízio. 0 = nunca.")]
    [Min(0)] [SerializeField] private int pesoSoltoNaParede = 1;
    [Tooltip("Peso de 'ligado na parede, solto no equipamento' no rodízio. 0 = nunca.")]
    [Min(0)] [SerializeField] private int pesoSoltoNoEquipamento = 1;
    [Tooltip("Peso de 'cabo ausente' (o jogador busca outro no gaveteiro de cabos) no rodízio. 0 = nunca.")]
    [Min(0)] [SerializeField] private int pesoAusente = 1;

    // rodízio único da cena: os chamados de cabo seguidos (em qualquer estação) passam pelas variações em ordem,
    // na proporção dos pesos — sem sorteio. Zera a cada carga de cena (partida nova).
    private static int cableDefectTurn;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCableTurn() => cableDefectTurn = 0;
    private void OnDestroy() => cableDefectTurn = 0;

    /// <summary>Variação usada no último defeito de cabo desta estação (para testes e para o Console).</summary>
    public CableDefect LastCableDefect { get; private set; }

    /// <summary>Próxima variação do rodízio, pelos pesos (ex.: 1/1/1 → parede, equipamento, ausente, parede...).</summary>
    public CableDefect NextCableDefect()
    {
        int[] pesos = { pesoSoltoNaParede, pesoSoltoNoEquipamento, pesoAusente };
        int total = pesos[0] + pesos[1] + pesos[2];
        if (total <= 0) return CableDefect.LooseAtWall;
        int k = cableDefectTurn++ % total;
        // intercalado (não em blocos): percorre as variações uma a uma enquanto cada uma ainda tem "vez" na volta
        int volta = 0;
        while (true)
        {
            for (int v = 0; v < 3; v++)
                if (pesos[v] > volta && k-- == 0) return (CableDefect)v;
            volta++;
        }
    }

    private ComputerCase pcCase;
    private ComputerPower power;
    private GameObject caboDefeituoso;   // pc-cabo-rede-defeituoso: o cabo que o chamado estragou
    private int portaRack;
    private int crimpadosNoInicio;       // crimpar-cabo: cabos crimpados até reservar o chamado               // porta do rack do ponto de rede desta estação (0 = sem vínculo)

    protected override void Awake()
    {
        base.Awake();
        pcCase = GetComponent<ComputerCase>();
        power = GetComponent<ComputerPower>();
        if (scoreKeeper == null) scoreKeeper = FindAnyObjectByType<ScoreKeeper>();
        RedeConectividade.Garantir();
        if (HomeStation != null) homeDesk = HomeStation.GetComponentInChildren<WorkSurface>(true);
        Peripherals();
    }

    protected override bool Knows(string id)
    {
        switch (id)
        {
            case "inspecionar-pc": case "inspecionar-video": case "verificar-rede": case "verificar-armazenamento":
            case "abrir-gabinete": case "fechar-gabinete":
            case "instalar-placa-mae": case "instalar-fonte": case "instalar-cooler": case "instalar-ram": case "instalar-hd":
            case "conectar-energia": case "conectar-video": case "conectar-rede":
            case "instalar-sistema": case "ligar-pc":
            case "validar-funcionamento": case "validar-sistema": case "validar-armazenamento":
            case "validar-imagem": case "validar-rede":
            case "remover-cabo-defeituoso": case "religar-rack":
            case "preparar-cabo": case "crimpar-cabo": case "testar-cabo":
            case "posicionar-equipamentos":
                return true;
            default:
                return false;
        }
    }

    protected override bool Condition(string id)
    {
        bool sistema = power.State == PcPowerState.OperatingSystem;
        switch (id)
        {
            case "inspecionar-pc": case "inspecionar-video": case "verificar-rede": case "verificar-armazenamento":
                return Inspected;
            case "abrir-gabinete": return pcCase.State == CaseState.Open;
            // estação nova: o gabinete e o monitor trazidos da sala de TI estão apoiados na mesa DESTA estação
            case "posicionar-equipamentos": return EquipmentOnHomeDesk();
            // o cabo estragado saiu do PC (desconectado nas duas pontas); testá-lo e descartá-lo é opcional
            case "remover-cabo-defeituoso": return caboDefeituoso == null || power.CableItemOf(PcLink.Network) != caboDefeituoso;
            // o par da porta desta estação está CERTO no rack (P N ↔ S N, cabo bom, sem laço) — sem vínculo: a rede do PC
            case "religar-rack": return portaRack > 0 ? RedeGame.Garantir().PortaFunciona(portaRack) : power.NetworkConnected;
            // crimpar-cabo (chamado da estação): investigar o PC, crimpar um cabo novo e a estação voltar a ter rede
            // COM um cabo crimpado depois do chamado ligado ao gabinete (o cabo estragado não serve)
            case "preparar-cabo": return Inspected;
            case "crimpar-cabo": return RedeGame.Garantir().TotalCrimpados > crimpadosNoInicio;
            case "testar-cabo":
            {
                GameObject cabo = power.CableItemOf(PcLink.Network);
                return sistema && power.NetworkConnected && cabo != null && cabo != caboDefeituoso
                       && cabo.GetComponentInChildren<CaboCrimpado>(true) != null;
            }
            case "fechar-gabinete": return pcCase.State == CaseState.Closed && pcCase.RequiredScrewsInstalled;
            case "instalar-placa-mae": return pcCase.HasComponent(PcComponent.Motherboard);
            case "instalar-fonte": return pcCase.HasComponent(PcComponent.Psu);
            case "instalar-cooler": return pcCase.HasComponent(PcComponent.CpuCooler);
            case "instalar-ram": return pcCase.HasComponent(PcComponent.Ram);
            case "instalar-hd": return pcCase.HasComponent(PcComponent.Hdd);
            case "conectar-energia": return power.PowerConnected && power.MonitorPowerConnected;
            case "conectar-video": return power.VideoConnected;
            case "conectar-rede": return power.NetworkConnected;
            case "instalar-sistema": return pcCase.HddHasOperatingSystem;
            case "ligar-pc": return power.State != PcPowerState.Off;
            case "validar-imagem": return sistema && power.HasVideoSignal;
            case "validar-rede": return sistema && power.NetworkConnected;
            default: return sistema;   // validar-funcionamento / -sistema / -armazenamento
        }
    }

    protected override void PrepareProblem(string ticketId)
    {
        PcComponent all = ComputerCase.AllComponents;
        switch (ticketId)
        {
            case "pc-sem-ram": pcCase.SetComponents(all & ~PcComponent.Ram, true); break;
            case "pc-sem-hd": pcCase.SetComponents(all & ~PcComponent.Hdd, true); break;
            case "pc-pecas-ausentes": pcCase.SetComponents(all & ~(PcComponent.Ram | PcComponent.Hdd), true); break;
            case "pc-sem-sistema": pcCase.SetComponents(all, false); break;
            case "pc-sem-energia": CableProblem(PcLink.Power); break;
            case "pc-sem-rede": CableProblem(PcLink.Network); break;
            case "monitor-sem-imagem": CableProblem(PcLink.Vga, PcLink.Hdmi); break;
            case "pc-sem-video-e-rede": CableProblem(PcLink.Vga, PcLink.Hdmi, PcLink.Network); break;
            case TestadorCabo.ChamadoCrimpagem:
            case TestadorCabo.ChamadoCaboEstacao:
                caboDefeituoso = power.CableItemOf(PcLink.Network);
                if (caboDefeituoso == null) { Debug.LogWarning("PcTicketBridge: o PC não tem cabo de rede para estragar.", this); break; }
                CaboRedeEstado.Marcar(caboDefeituoso, PinosQuebrados(), Current != null ? Current.Number : 0);
                power.NotifyNetworkChanged();
                RelatarRede("cabo de rede da estação com defeito");
                break;
            case TestadorCabo.ChamadoRack:
                // o rack fica errado de verdade: ou o cabo do patch panel desta porta sai, ou vai parar numa porta
                // TROCADA do switch (conexão física existe, mas fora da topologia: só P N ↔ S N dá rede)
                if (portaRack > 0)
                {
                    RedeGame g = RedeGame.Garantir();
                    int trocada = Random.value < 0.5f ? g.TrocarParaPortaErrada(portaRack) : 0;
                    if (trocada == 0) g.Desligar(portaRack, out _);
                    RelatarRede(trocada > 0 ? $"P{portaRack} ligado em S{trocada} no rack" : $"par {portaRack} desligado no rack");
                }
                else Debug.LogWarning("PcTicketBridge: o ponto de rede desta estação não tem porta no rack (PontoDeRedeRack).", this);
                break;
            case "pc-montagem-completa":
                // estação "nova": a mesa começa VAZIA. Gabinete (sem peças), monitor, teclado, mouse e todos os cabos
                // (soltos das duas pontas) vão para a bancada da sala de TI (AssemblyPickupPoint); o jogador os traz,
                // monta e liga tudo. Sem ponto de retirada na cena, tudo fica na mesa (comportamento antigo).
                pcCase.SetComponents(PcComponent.None, false);
                var kit = new System.Collections.Generic.List<PickupInteractable>();
                int n = 0;
                foreach (PcLink link in new[] { PcLink.Power, PcLink.MonitorPower, PcLink.Vga, PcLink.Hdmi, PcLink.Network })
                    if (RemoveCable(link, n, kit)) n++;
                MoveKitToItRoom(kit);
                break;
        }
    }

    protected override void OnClaimed(TicketInstance ticket)
    {
        caboDefeituoso = null;
        crimpadosNoInicio = RedeGame.Garantir().TotalCrimpados;
        portaRack = PontoDeRedeRack.PortaDe(power.WallOf(PcLink.Network));
        if (portaRack == 0) portaRack = PontoDeRedeRack.PortaDe(power.CurrentWall);
        // a etiqueta do ponto de rede: o jogador sabe qual par procurar no rack
        if (portaRack > 0) ticket.Location += $" · ponto de rede → porta {portaRack} do rack";
    }

    // Diz no Console qual estação ficou sem rede e confere que a conectividade caiu de verdade.
    private void RelatarRede(string defeito)
    {
        if (power.NetworkConnected)
            Debug.LogError($"PcTicketBridge ({DeviceLabel}): {defeito}, mas a estação ainda tem rede. " +
                           $"Regra de rede registrada: {ComputerPower.NetworkPathCheck != null}.", this);
        else Debug.Log($"[Rede] {DeviceLabel}: {defeito} — estação sem rede.", this);
    }

    // Defeito de cabo: um ou dois pinos sem continuidade (sorteio, como os outros defeitos do jogo).
    private static int[] PinosQuebrados()
    {
        int a = Random.Range(0, 8);
        return Random.value < 0.5f ? new[] { a } : new[] { a, (a + 1 + Random.Range(0, 7)) % 8 };
    }

    // Solta a ponta da parede (tomada/ponto de rede): o cabo fica pendurado no equipamento, à vista do jogador.
    private void UnplugWallEnd(PcLink link)
    {
        if (!power.IsEndPlugged(link, true)) return;
        power.PortsOf(link, out PcPort wallEnd, out _);
        power.UsePort(wallEnd, null, false, out _);
    }

    // Equipamentos da estação levados à sala de TI no chamado de estação nova (o gabinete é este objeto).
    private void MoveKitToItRoom(System.Collections.Generic.List<PickupInteractable> cables)
    {
        AssemblyPickupPoint point = AssemblyPickupPoint.Nearest(transform.position);
        if (point == null)
        {
            Debug.LogWarning($"PcTicketBridge ({DeviceLabel}): sem AssemblyPickupPoint na cena; o gabinete e o monitor ficam na mesa.", this);
            return;
        }
        var kit = new System.Collections.Generic.List<PickupInteractable>();
        kit.Add(GetComponent<PickupInteractable>());
        PickupInteractable monitorItem = MonitorItem();
        if (monitorItem != null) kit.Add(monitorItem);
        kit.AddRange(Peripherals());
        kit.AddRange(cables);
        kit.RemoveAll(p => p == null);
        for (int i = kit.Count - 1; i > 0; i--) if (kit.IndexOf(kit[i]) < i) kit.RemoveAt(i);   // sem repetidos, na ordem
        point.Place(kit);
        Debug.Log($"[Montagem] {DeviceLabel}: mesa vazia; {kit.Count} itens na sala de TI ({point.name}).", this);
    }

    private PickupInteractable MonitorItem() =>
        power.Monitor != null ? power.Monitor.GetComponentInParent<PickupInteractable>() : null;

    // Teclado e mouse do PC_Escritorio (pegáveis irmãos do gabinete, lidos no Awake: depois de pegos saem da hierarquia).
    private System.Collections.Generic.List<PickupInteractable> peripherals;
    private System.Collections.Generic.List<PickupInteractable> Peripherals()
    {
        if (peripherals != null) return peripherals;
        peripherals = new System.Collections.Generic.List<PickupInteractable>();
        Transform pc = transform.parent;
        if (pc == null) return peripherals;
        PickupInteractable monitorItem = MonitorItem();
        foreach (PickupInteractable p in pc.GetComponentsInChildren<PickupInteractable>(true))
        {
            if (p.transform == transform || p.transform.IsChildOf(transform) || p == monitorItem) continue;
            if (p.GetComponentInChildren<MonitorDisplay>(true) != null) continue;   // o monitor vai à parte (MonitorItem)
            if (p.GetComponentInParent<CableItem>(true) != null || p.GetComponent<ComputerPartItem>() != null) continue;
            if (!p.gameObject.activeInHierarchy) continue;
            peripherals.Add(p);
        }
        return peripherals;
    }

    // Mesa da estação dona deste PC (lida no Awake).
    private WorkSurface homeDesk;

    private bool EquipmentOnHomeDesk()
    {
        if (homeDesk == null) return pcCase.IsOnWorkSurface;   // fora de estação: qualquer mesa/bancada
        if (pcCase.SupportingSurface() != homeDesk) return false;
        PickupInteractable monitorItem = MonitorItem();
        return monitorItem == null || (!monitorItem.IsHeld && SurfaceUnder(monitorItem) == homeDesk);
    }

    // Superfície de trabalho logo abaixo de um item (raio para baixo a partir da base dele, ignorando o próprio item).
    private static WorkSurface SurfaceUnder(PickupInteractable item)
    {
        Bounds b = item.WorldBounds;
        Vector3 from = new Vector3(b.center.x, b.min.y + 0.05f, b.center.z);
        RaycastHit[] hits = Physics.RaycastAll(from, Vector3.down, 0.3f, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));
        foreach (RaycastHit h in hits)
        {
            if (h.collider.transform.IsChildOf(item.transform)) continue;
            return h.collider.GetComponentInParent<WorkSurface>();
        }
        return null;
    }

    // Tira o cabo das duas pontas e o deixa sobre a mesa, ao lado do gabinete (nunca é destruído).
    private bool RemoveCable(PcLink link, int slot, System.Collections.Generic.List<PickupInteractable> removed = null)
    {
        if (power.CableItemOf(link) == null) return false;
        power.PortsOf(link, out PcPort a, out PcPort b);
        GameObject item = null;
        foreach (PcPort port in new[] { a, b })
        {
            power.UsePort(port, null, false, out GameObject back);
            if (back != null) item = back;
        }
        if (item == null) return false;
        item.SetActive(true);
        PickupInteractable pickup = item.GetComponentInChildren<PickupInteractable>(true);
        if (pickup != null)
        {
            removed?.Add(pickup);
            Vector3 lado = transform.right * (0.32f + slot * 0.09f);
            pickup.OnDropped(transform.position + lado + Vector3.up * 0.005f, transform.eulerAngles.y);
        }
        return true;
    }

    // Defeito de cabo nos cabos ligados destas ligações, numa variação do rodízio (a mesma para todos). Ponta A = parede
    // (no vídeo, a saída do gabinete); ponta B = equipamento (gabinete; no vídeo, o monitor). "Ausente" tira o cabo da
    // estação de vez (destruído): a solução é outro cabo do gaveteiro. Todas se resolvem só com a ligação certa nas
    // duas pontas (etapas conectar-energia / conectar-video / conectar-rede, pelo estado real).
    private void CableProblem(params PcLink[] links)
    {
        CableDefect d = NextCableDefect();
        LastCableDefect = d;
        int n = 0;
        foreach (PcLink link in links)
        {
            if (power.CableItemOf(link) == null) continue;
            power.PortsOf(link, out PcPort a, out PcPort b);
            switch (d)
            {
                case CableDefect.LooseAtWall:
                    if (power.IsEndPlugged(link, true)) power.UsePort(a, null, false, out _);
                    break;
                case CableDefect.LooseAtDevice:
                    if (power.IsEndPlugged(link, false)) power.UsePort(b, null, false, out _);
                    break;
                case CableDefect.Missing:
                    var removidos = new System.Collections.Generic.List<PickupInteractable>();
                    if (RemoveCable(link, n++, removidos))
                        foreach (PickupInteractable c in removidos) if (c != null) Destroy(c.gameObject);
                    break;
            }
        }
        Debug.Log($"[Chamado] {DeviceLabel}: cabo(s) {string.Join("/", links)} → {d}.", this);
    }

    // Solta o vídeo na saída do gabinete: o cabo fica pendurado no monitor.
    private void UnplugVideo()
    {
        foreach (PcLink link in new[] { PcLink.Vga, PcLink.Hdmi })
        {
            if (!power.IsEndPlugged(link, true)) continue;
            power.PortsOf(link, out PcPort caseEnd, out _);
            power.UsePort(caseEnd, null, false, out _);
        }
    }

    // Entrega com o gabinete aberto (tampa aberta ou parafuso obrigatório fora) contraria o procedimento: o chamado não
    // é concluído e a tentativa é penalizada (ScoreKeeper). Durante as etapas o gabinete aberto é legítimo.
    protected override string DeliveryBlocker()
    {
        if (pcCase.IsIncomplete) return "o gabinete ainda está aberto: feche a tampa e recoloque os parafusos";
        // estação nova: só é entregue montada NA MESA dela (gabinete e monitor apoiados ali)
        if (Current != null && Current.Definition != null && Current.Definition.ticketId == "pc-montagem-completa" && !EquipmentOnHomeDesk())
            return "o gabinete e o monitor precisam estar na mesa da estação";
        return null;
    }

    // abrir-gabinete é passagem: a entrega exige o gabinete fechado (DeliveryBlocker), então ele não pode "deixar de valer"
    protected override bool MustHoldOnDelivery(string id) => id != "abrir-gabinete";

    protected override void OnTicketCompleted(TicketInstance ticket)
    {
        if (scoreKeeper != null && pcCase.IsIncomplete) scoreKeeper.ApplyIncompleteCasePenalty(ticket);
    }
}
