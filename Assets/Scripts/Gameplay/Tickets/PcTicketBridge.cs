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
    [Tooltip("Penalidade de gabinete incompleto (valor no ScoreKeeper). Vazio = procura na cena.")]
    [SerializeField] private ScoreKeeper scoreKeeper;

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
            // o cabo estragado saiu do PC (desconectado nas duas pontas); testá-lo e descartá-lo é opcional
            case "remover-cabo-defeituoso": return caboDefeituoso == null || power.CableItemOf(PcLink.Network) != caboDefeituoso;
            // o par da porta desta estação está ligado no rack com um cabo bom (sem vínculo: vale a rede do PC)
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
            case "pc-sem-energia": UnplugWallEnd(PcLink.Power); break;
            case "pc-sem-rede": UnplugWallEnd(PcLink.Network); break;
            case "monitor-sem-imagem": UnplugVideo(); break;
            case "pc-sem-video-e-rede": UnplugVideo(); UnplugWallEnd(PcLink.Network); break;
            case TestadorCabo.ChamadoCrimpagem:
            case TestadorCabo.ChamadoCaboEstacao:
                caboDefeituoso = power.CableItemOf(PcLink.Network);
                if (caboDefeituoso == null) { Debug.LogWarning("PcTicketBridge: o PC não tem cabo de rede para estragar.", this); break; }
                CaboRedeEstado.Marcar(caboDefeituoso, PinosQuebrados(), Current != null ? Current.Number : 0);
                power.NotifyNetworkChanged();
                RelatarRede("cabo de rede da estação com defeito");
                break;
            case TestadorCabo.ChamadoRack:
                if (portaRack > 0) RedeGame.Garantir().Desligar(portaRack, out _);
                else Debug.LogWarning("PcTicketBridge: o ponto de rede desta estação não tem porta no rack (PontoDeRedeRack).", this);
                RelatarRede($"par {portaRack} desligado no rack");
                break;
            case "pc-montagem-completa":
                pcCase.SetComponents(PcComponent.None, false);
                UnplugWallEnd(PcLink.Power);
                UnplugWallEnd(PcLink.MonitorPower);
                UnplugVideo();
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

    protected override void OnTicketCompleted(TicketInstance ticket)
    {
        if (scoreKeeper != null && pcCase.IsIncomplete) scoreKeeper.ApplyIncompleteCasePenalty(ticket);
    }
}
