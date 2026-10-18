using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Pontas de cabo que a estação de trabalho (tomada + gabinete + monitor) aceita.</summary>
public enum PcPort
{
    /// <summary>Tomada 1 da parede (ponta "de fora" de qualquer cabo de energia: PC ou monitor).</summary>
    Outlet,
    /// <summary>Entrada de energia da fonte, na traseira do gabinete.</summary>
    CasePower,
    /// <summary>Saída VGA na traseira do gabinete.</summary>
    CaseVideo,
    /// <summary>Entrada VGA na traseira do monitor.</summary>
    MonitorVideo,
    /// <summary>Entrada de energia na traseira do monitor.</summary>
    MonitorPower,
    /// <summary>Tomada 2 da parede. Apesar do nome, aceita qualquer cabo de energia (PC ou monitor), como a 1.</summary>
    MonitorOutlet,
    /// <summary>Saída HDMI na traseira do gabinete.</summary>
    CaseHdmi,
    /// <summary>Entrada HDMI na traseira do monitor.</summary>
    MonitorHdmi,
    /// <summary>Porta Ethernet (RJ45) na traseira do gabinete.</summary>
    CaseNetwork,
    /// <summary>Porta RJ45 do equipamento de rede (ponto de rede na parede).</summary>
    NetworkJack
}

/// <summary>Cada cabo que a estação aceita (um item de cabo por ligação). Valores novos sempre no fim.</summary>
public enum PcLink { Power, MonitorPower, Vga, Hdmi, Network }

/// <summary>O que o monitor mostra. Desligado = tela preta.</summary>
public enum PcPowerState { Off, Bios, OperatingSystem }

/// <summary>
/// Tela exata do monitor (detalha a BIOS): sem armazenamento (nenhum HD), HD sem sistema ("No bootable device"),
/// instalando o sistema pelo pendrive, ou o sistema (com/sem rede: ver <see cref="ComputerPower.Online"/>).
/// <see cref="NoSignal"/>: o PC está ligado, mas a imagem não chega ao monitor (sem cabo de vídeo nas duas pontas ou
/// monitor longe do gabinete). Valores novos sempre no fim.
/// </summary>
public enum PcScreen { Off, NoStorage, NoBootableDevice, Installing, OperatingSystem, NoSignal }

/// <summary>Resultado de clicar numa porta (<see cref="ComputerPower.UsePort"/>).</summary>
public enum PortResult
{
    /// <summary>O cabo da mão foi conectado por uma ponta (o chamador tira o item da mão e o guarda).</summary>
    PluggedFromHand,
    /// <summary>A outra ponta de um cabo já preso ao PC foi conectada.</summary>
    PluggedOtherEnd,
    /// <summary>Uma ponta foi desconectada; a outra continua presa.</summary>
    Unplugged,
    /// <summary>As duas pontas ficaram soltas: o cabo volta para a mão (<c>itemForHand</c>).</summary>
    CableReturned,
    /// <summary>Porta livre e nenhum cabo na mão.</summary>
    NeedsCable,
    /// <summary>Porta livre e o cabo da mão é de outro tipo.</summary>
    WrongCable,
    /// <summary>Para desconectar, a mão precisa estar livre.</summary>
    HandsFull,
    Invalid,
    /// <summary>
    /// O equipamento da porta (ponto elétrico ou monitor) está longe do gabinete (ver <see cref="ComputerPower.CanReachPort"/>):
    /// nada muda. Valor novo no fim.
    /// </summary>
    OutOfReach,
    /// <summary>A tomada/o ponto de rede já está com o cabo de OUTRO PC (<see cref="WallConnectionPoint.OccupantOf"/>). Valor novo no fim.</summary>
    Occupied
}

public enum PowerButtonResult { TurnedOn, TurnedOff, MissingRequirements }

/// <summary>
/// Energia e funcionamento do PC: cabos (energia: tomada ↔ gabinete e tomada do monitor ↔ monitor; vídeo VGA ou
/// HDMI: gabinete ↔ monitor; rede: ponto de rede ↔ gabinete), botão Power e o que o monitor mostra. Vai no ROOT LÓGICO do gabinete, ao lado do <see cref="ComputerCase"/> (que guarda as peças).
///
/// Regras (GDD/CH-01):
///  - Desligado ou sem requisitos → tela preta.
///  - Power com energia do PC e do monitor (cada cabo nas duas pontas) + fonte + placa-mãe + memória RAM + cooler
///    → liga na BIOS. O vídeo NÃO é requisito para ligar: sem cabo VGA/HDMI nas duas pontas o PC funciona, mas o
///    monitor mostra "Sem sinal" (<see cref="PcScreen.NoSignal"/>). O sinal depende SÓ do estado físico do cabo: levar o
///    gabinete ou o monitor para longe não desliga nada (a distância, <see cref="MonitorInRange"/>, só decide se o monitor
///    aparece e pode ser usado na tela 2D). Ligar o cabo de vídeo mostra o estado real; soltá-lo volta ao "Sem sinal".
///  - Os mesmos requisitos + HD COM SISTEMA (gravado na unidade, ver <see cref="ComputerCase.HddHasOperatingSystem"/>)
///    → liga no sistema operacional.
///  - Tomadas: as duas tomadas da parede (<see cref="PcPort.Outlet"/> e <see cref="PcPort.MonitorOutlet"/>) aceitam
///    qualquer um dos dois cabos de energia. O que distingue os cabos é a ponta do equipamento (fonte do gabinete ou
///    entrada do monitor), não a tomada.
///  - Monitor: o <see cref="MonitorDisplay"/> que aponta para este PC se registra aqui (<see cref="AttachMonitor"/>);
///    a distância dele ao gabinete decide se o monitor aparece na tela 2D e se recebe sinal.
///  - Proximidade ENTRE EQUIPAMENTOS (nunca do jogador): o monitor precisa estar perto do gabinete
///    (<see cref="MonitorInRange"/>) e o ponto elétrico da parede (<see cref="WallConnectionPoint"/>) também
///    (<see cref="IsWallPointInRange"/>). Fora disso, as portas deles não são usadas (<see cref="CanReachPort"/>) e a tela
///    2D os esconde. Sem monitor/ponto registrado, não há limite (ex.: o Gabinete.prefab sozinho).
///  - HD sem sistema + pendrive numa USB da traseira: ao ligar, dá boot pelo pendrive (tela da BIOS por
///    <see cref="installDuration"/> s), grava o sistema no HD e vai para o sistema. Sem HD: fica na BIOS, sem instalar.
///    Tirar o pendrive, o HD ou um requisito durante a instalação a cancela.
///  - Sem os requisitos o botão não liga o PC. Se um requisito some com o PC ligado (cabo solto, peça retirada),
///    o PC desliga: nunca fica num estado incompatível com a configuração.
///  - Sem reiniciar: com o PC na BIOS, encaixar o pendrive (com HD) ou instalar o HD (com o pendrive) INICIA a
///    instalação; instalar um HD com sistema deixa o boot pendente (<see cref="BootPending"/>). Mas a tela só avança
///    com o jogador olhando o monitor deste PC (<see cref="Watched"/>: <see cref="InstallPaused"/> + <see cref="WatchCheck"/>):
///    a ação começa o processo e a transição acontece diante dele.
///  - Rede: opcional para ligar; no sistema operacional, o cabo de rede nas duas pontas deixa o PC "conectado".
/// Cada cabo é um item real: ao conectar a primeira ponta ele sai da mão e fica guardado aqui (desativado);
/// ao soltar as duas pontas o MESMO objeto volta para a mão.
/// Sem dependência de UI, modelo, tag ou nome.
/// </summary>
[RequireComponent(typeof(ComputerCase))]
public class ComputerPower : MonoBehaviour, IPickupBlocker
{
    private class Cable
    {
        public GameObject item;
        public bool endA; // ponta "de fora": tomada / saída de vídeo do gabinete / ponto de rede
        public bool endB; // ponta do equipamento: gabinete / monitor
        public int outlet = -1; // cabos de energia: tomada da parede onde está a ponta A (0 ou 1; -1 = nenhuma)
        public WallConnectionPoint wall; // ponto elétrico onde está a ponta A (energia/rede); null = nenhum ou sem ponto na cena
        public bool Attached => endA || endB;
        public bool Complete => endA && endB;
    }

    [Tooltip("Somente leitura em jogo: o que o monitor mostra agora.")]
    [SerializeField] private PcPowerState state = PcPowerState.Off;
    [Tooltip("Tempo (s) da instalação do sistema pelo pendrive (tela de progresso). 0 = instantâneo.")]
    [Min(0f)] [SerializeField] private float installDuration = 6f;
    [Tooltip("Quantas etapas visuais a instalação tem (uma imagem de progresso por etapa).")]
    [Min(1)] [SerializeField] private int installStages = 5;
    [Tooltip("Distância máxima (m) entre o gabinete e a tela do SEU monitor (o MonitorDisplay que aponta para este PC). " +
             "Mais longe: o monitor some da tela 2D de manutenção e não recebe sinal de vídeo.")]
    [Min(0f)] [SerializeField] private float monitorRange = 1.5f;

    /// <summary>Portas USB da traseira do gabinete (qualquer uma aceita o pendrive).</summary>
    public const int UsbPortCount = 3;

    private static readonly PcLink[] PowerLinks = { PcLink.Power, PcLink.MonitorPower };

    private readonly Dictionary<PcLink, Cable> cables = new Dictionary<PcLink, Cable>();
    private ComputerCase pcCase;
    private bool subscribed;
    private GameObject pendriveItem;
    private int pendrivePort = -1;
    private float installRemaining = -1f;
    private Transform monitor;
    private Transform wallPoint;
    private float wallPointRange;
    private bool lastInRange = true;
    private BreakableEquipment caseCondition;     // estado físico do próprio gabinete (opcional)
    private BreakableEquipment monitorCondition;  // estado físico do monitor associado (opcional)

    /// <summary>Disparado quando o PC liga, desliga ou muda de tela.</summary>
    public event Action<PcPowerState> PowerStateChanged;
    /// <summary>Disparado quando qualquer ponta de cabo é conectada ou desconectada.</summary>
    public event Action ConnectionsChanged;
    /// <summary>
    /// Disparado quando o que o monitor deve mostrar muda (<see cref="Screen"/>, etapa da instalação, rede, sinal de
    /// vídeo, monitor entrando ou saindo do alcance).
    /// </summary>
    public event Action DisplayChanged;

    /// <summary>Cabo (ligação) que mudou por último num <see cref="UsePort"/> (a tela 2D faz a ponta solta dele seguir o mouse).</summary>
    public PcLink? LastChangedLink { get; private set; }

    /// <summary>Tela do monitor deste PC (registrada pelo <see cref="MonitorDisplay"/>). Vazio = PC sem monitor associado.</summary>
    public Transform Monitor => monitor;
    public float MonitorRange { get => monitorRange; set { monitorRange = Mathf.Max(0f, value); CheckMonitorRange(); } }

    /// <summary>
    /// O monitor deste PC está perto o bastante do gabinete (<see cref="monitorRange"/>), em qualquer direção.
    /// Sem monitor associado (ex.: o Gabinete.prefab sozinho) a distância não é exigida.
    /// </summary>
    public bool MonitorInRange =>
        monitor == null || (monitor.position - transform.position).sqrMagnitude <= monitorRange * monitorRange;

    /// <summary>
    /// Ponto elétrico em uso agora: o <see cref="CurrentWall"/> (qualquer estação); sem nenhum por perto, o da estação
    /// deste PC (registrado pelo <see cref="WallConnectionPoint"/>, ou por <see cref="AttachWallPoint"/> nos testes).
    /// </summary>
    public Transform WallPoint
    {
        get
        {
            WallConnectionPoint w = CurrentWall;
            return w != null ? w.transform : wallPoint;
        }
    }

    /// <summary>
    /// Ponto elétrico ativo mais próximo do GABINETE, de qualquer estação (<see cref="WallConnectionPoint.Nearest"/>).
    /// Null = nenhum ao alcance (ou nenhum na cena).
    /// </summary>
    public WallConnectionPoint CurrentWall => WallConnectionPoint.Nearest(transform.position);

    /// <summary>
    /// Há um ponto elétrico perto do GABINETE (distância horizontal até o <c>Case Range</c> do ponto). Com pontos na
    /// cena, vale qualquer um (o gabinete pode ser religado em outra estação); sem nenhum, vale o registrado por
    /// <see cref="AttachWallPoint"/> (sem registro = sem limite). Longe, tomadas e rede não são usadas nem mostradas.
    /// </summary>
    public bool IsWallPointInRange =>
        WallConnectionPoint.AnyActive ? CurrentWall != null : WithinReach(wallPoint, wallPointRange, transform.position);

    // ------------------------------------------------------------------ transporte do gabinete

    /// <summary>
    /// O gabinete está preso à estação: algum cabo tem uma ponta no gabinete e a outra conectada fora (tomada, ponto de
    /// rede ou monitor). Cabos presos só no gabinete, o cabo de energia do MONITOR e o pendrive não bloqueiam: vão
    /// junto (o estado fica neste componente) e podem ser ligados em qualquer ponto elétrico no destino.
    /// </summary>
    public bool BlocksPickup => TetheredLinks().Count > 0;

    /// <summary>Texto curto do motivo do bloqueio para o HUD (vazio = não bloqueia).</summary>
    public string BlockReason
    {
        get
        {
            List<PcLink> links = TetheredLinks();
            if (links.Count == 0) return "";
            var names = new List<string>();
            foreach (PcLink l in links)
            {
                string n = l == PcLink.Power ? "energia" : l == PcLink.Network ? "rede" : "vídeo";
                if (!names.Contains(n)) names.Add(n);
            }
            return "Desconecte o cabo de " + string.Join(", ", names);
        }
    }

    /// <summary>
    /// Cabos que prendem o MONITOR agora: energia do monitor ligada nas duas pontas (parede ↔ monitor) e VGA/HDMI ligado
    /// nas duas pontas (gabinete ↔ monitor). Cabo pendurado só no monitor vai junto com ele.
    /// </summary>
    public List<PcLink> MonitorTetheredLinks()
    {
        var list = new List<PcLink>();
        foreach (PcLink link in new[] { PcLink.MonitorPower, PcLink.Vga, PcLink.Hdmi })
            if (cables.TryGetValue(link, out Cable c) && c.Complete) list.Add(link);
        return list;
    }

    /// <summary>Texto do bloqueio do monitor para o HUD (vazio = livre).</summary>
    public string MonitorBlockReason
    {
        get
        {
            List<PcLink> links = MonitorTetheredLinks();
            if (links.Count == 0) return "";
            bool energia = links.Contains(PcLink.MonitorPower), video = links.Count > (energia ? 1 : 0);
            return "Desconecte o cabo de " + (energia && video ? "energia, vídeo" : energia ? "energia" : "vídeo");
        }
    }

    /// <summary>Cabos que prendem o gabinete à estação agora (uma ponta no gabinete, a outra conectada fora).</summary>
    public List<PcLink> TetheredLinks()
    {
        var list = new List<PcLink>();
        foreach (PcLink link in Enum.GetValues(typeof(PcLink)))
        {
            if (link == PcLink.MonitorPower || !cables.TryGetValue(link, out Cable c)) continue;
            if (c.Complete) list.Add(link);   // energia/rede: gabinete <-> parede; VGA/HDMI: gabinete <-> monitor
        }
        return list;
    }

    /// <summary>Distância HORIZONTAL (a altura não conta) entre <paramref name="point"/> e <paramref name="from"/> dentro de <paramref name="reach"/>; sem ponto ou alcance 0 = sem limite.</summary>
    public static bool WithinReach(Transform point, float reach, Vector3 from)
    {
        if (point == null || reach <= 0f) return true;
        Vector3 d = point.position - from;
        d.y = 0f;
        return d.sqrMagnitude <= reach * reach;
    }

    /// <summary>Porta do ponto elétrico da parede (as duas tomadas e o ponto de rede).</summary>
    public static bool IsWallPort(PcPort port) => port == PcPort.Outlet || port == PcPort.MonitorOutlet || port == PcPort.NetworkJack;

    /// <summary>Porta da traseira do monitor.</summary>
    public static bool IsMonitorPort(PcPort port) => port == PcPort.MonitorPower || port == PcPort.MonitorVideo || port == PcPort.MonitorHdmi;

    /// <summary>
    /// A porta pode ser usada agora: portas da parede pedem o ponto elétrico perto do gabinete
    /// (<see cref="IsWallPointInRange"/>), portas do monitor pedem o monitor perto do gabinete (<see cref="MonitorInRange"/>);
    /// as do gabinete, sempre. Não depende de onde o jogador está.
    /// </summary>
    public bool CanReachPort(PcPort port) =>
        IsWallPort(port) ? IsWallPointInRange : !IsMonitorPort(port) || MonitorAvailable;

    /// <summary>
    /// A imagem chega ao monitor: cabo de vídeo (VGA ou HDMI) nas duas pontas. Só o estado físico do cabo conta; a
    /// distância (<see cref="MonitorInRange"/>) é regra de interface/interação, não desconecta nada.
    /// </summary>
    public bool HasVideoSignal => VideoConnected;

    public PcPowerState State => state;
    public ComputerCase Case
    {
        get
        {
            if (pcCase == null) pcCase = GetComponent<ComputerCase>();
            Subscribe();
            return pcCase;
        }
    }

    /// <summary>Cabo de energia do PC (tomada ↔ fonte do gabinete) nas duas pontas.</summary>
    public bool PowerConnected => IsComplete(PcLink.Power);
    public bool MonitorPowerConnected => IsComplete(PcLink.MonitorPower);
    public bool VgaConnected => IsComplete(PcLink.Vga);
    public bool HdmiConnected => IsComplete(PcLink.Hdmi);
    /// <summary>Há imagem chegando ao monitor (VGA ou HDMI).</summary>
    public bool VideoConnected => VgaConnected || HdmiConnected;
    /// <summary>
    /// Cabo de rede nas duas pontas E, se houver regra registrada (<see cref="NetworkPathCheck"/>), o caminho até a
    /// rede funciona (cabo sem defeito, ligação no rack). Sem regra registrada: só o cabo, como antes.
    /// </summary>
    public bool NetworkConnected => IsComplete(PcLink.Network) && (NetworkPathCheck == null || NetworkPathCheck(this));

    /// <summary>
    /// Regra extra de conectividade, registrada pelo sistema de rede (fora desta assembly). Null = só o cabo conta.
    /// Quem muda algo que afeta a regra chama <see cref="NotifyNetworkChanged"/>.
    /// </summary>
    public static Func<ComputerPower, bool> NetworkPathCheck;

    /// <summary>O objeto do cabo guardado nesta ligação (null = nenhum cabo).</summary>
    public GameObject CableItemOf(PcLink link) => cables.TryGetValue(link, out Cable c) ? c.item : null;

    /// <summary>Algo fora do PC (rack, defeito do cabo) mudou a conectividade: a tela e o monitor se atualizam.</summary>
    public void NotifyNetworkChanged()
    {
        ConnectionsChanged?.Invoke();
        DisplayChanged?.Invoke();
    }
    /// <summary>No sistema operacional e com o cabo de rede ligado.</summary>
    public bool Online => state == PcPowerState.OperatingSystem && NetworkConnected;

    /// <summary>
    /// Requisitos para ligar e chegar à BIOS. O cabo de vídeo não entra: sem ele o PC liga do mesmo jeito e o
    /// monitor mostra "Sem sinal" (<see cref="HasVideoSignal"/>).
    /// </summary>
    public bool CanReachBios =>
        !CaseBroken && PowerConnected && MonitorPowerConnected && Case.HasComponent(PcComponent.Psu) &&
        Case.HasComponent(PcComponent.Motherboard) && Case.HasComponent(PcComponent.Ram) && Case.HasComponent(PcComponent.CpuCooler);

    /// <summary>
    /// O gabinete quebrou numa queda (<see cref="BreakableEquipment"/> no mesmo objeto): não liga mais e, se estava
    /// ligado, desliga. Sem o componente, nunca quebra.
    /// </summary>
    public bool CaseBroken
    {
        get
        {
            _ = Case;   // garante a assinatura do estado físico (também em Edit Mode, sem OnEnable)
            return caseCondition != null && caseCondition.IsBroken;
        }
    }

    /// <summary>
    /// O monitor associado quebrou numa queda: a tela dele fica apagada (<see cref="Screen"/> = Off), mesmo com o PC
    /// ligado. O PC em si continua funcionando.
    /// </summary>
    public bool MonitorBroken => monitor != null && monitorCondition != null && monitorCondition.IsBroken;

    /// <summary>
    /// O monitor pode ser alvo de manutenção agora: perto do gabinete (<see cref="MonitorInRange"/>) e NÃO quebrado.
    /// Quebrado, ele some da tela 2D e as portas dele (energia, VGA, HDMI) recusam (<see cref="CanReachPort"/>).
    /// </summary>
    public bool MonitorAvailable => MonitorInRange && !MonitorBroken;

    /// <summary>Requisitos para ligar e chegar ao sistema operacional (HD com o sistema gravado).</summary>
    public bool CanReachOperatingSystem => CanReachBios && Case.HddHasOperatingSystem;

    /// <summary>Há um pendrive conectado numa das USB.</summary>
    public bool PendriveConnected => pendrivePort >= 0;
    /// <summary>USB onde o pendrive está (-1 = nenhuma).</summary>
    public int PendrivePort => pendrivePort;
    /// <summary>Ligado instalando o sistema pelo pendrive (tela de progresso).</summary>
    public bool IsInstallingOperatingSystem => installRemaining >= 0f;
    /// <summary>Progresso da instalação (0 a 1); 0 fora dela.</summary>
    public float InstallProgress => IsInstallingOperatingSystem && installDuration > 0f
        ? Mathf.Clamp01(1f - installRemaining / installDuration) : 0f;
    /// <summary>Etapa visual da instalação (0 a <see cref="InstallStages"/> - 1).</summary>
    public int InstallStage => Mathf.Min(installStages - 1, Mathf.FloorToInt(InstallProgress * installStages));
    public int InstallStages => installStages;

    /// <summary>
    /// Pausa a instalação sem cancelá-la (apresentação): a tela 2D liga isto enquanto o jogador está em outra vista
    /// que não a da frente, para a sequência não correr escondida. Volta a andar de onde parou.
    /// </summary>
    public bool InstallPaused { get; set; }

    /// <summary>
    /// Quem diz se o jogador está vendo o monitor deste PC (no 3D ou pela tela 2D). Registrado pelo jogo
    /// (<c>MonitorWatchCheck</c>); null = sempre visto (testes e cenas sem jogador).
    /// </summary>
    public static Func<ComputerPower, bool> WatchCheck;
    /// <summary>A transição de tela (instalação, boot pendente) pode andar agora: o jogador está diante do monitor.</summary>
    public bool Watched => !InstallPaused && (WatchCheck == null || WatchCheck(this));
    /// <summary>HD com sistema instalado com o PC na BIOS: entra no sistema assim que o jogador olhar o monitor.</summary>
    public bool BootPending { get; private set; }

    /// <summary>
    /// Tela exata do monitor agora. Ligado sem sinal de vídeo = <see cref="PcScreen.NoSignal"/> (o PC continua no
    /// estado real, <see cref="State"/>, e a imagem volta assim que o vídeo chega). A detecção do HD na BIOS é ao vivo.
    /// </summary>
    public PcScreen Screen
    {
        get
        {
            if (state == PcPowerState.Off || MonitorBroken) return PcScreen.Off;
            if (!HasVideoSignal) return PcScreen.NoSignal;
            if (state == PcPowerState.OperatingSystem) return PcScreen.OperatingSystem;
            if (IsInstallingOperatingSystem) return PcScreen.Installing;
            return Case.HasComponent(PcComponent.Hdd) ? PcScreen.NoBootableDevice : PcScreen.NoStorage;
        }
    }

    private void OnEnable() => _ = Case;
    private void OnDisable()
    {
        if (subscribed && pcCase != null) pcCase.ComponentsChanged -= Revalidate;
        if (subscribed && caseCondition != null) caseCondition.BreakableChanged -= OnCaseCondition;
        subscribed = false;
    }

    // Também chamado pelo getter de Case: vale mesmo sem OnEnable (ex.: testes em Edit Mode).
    private void Subscribe()
    {
        if (subscribed || pcCase == null) return;
        pcCase.ComponentsChanged += Revalidate;
        caseCondition = GetComponent<BreakableEquipment>();
        if (caseCondition != null) caseCondition.BreakableChanged += OnCaseCondition;
        subscribed = true;
    }

    // Gabinete quebrou: o Revalidate desliga (CanReachBios deixa de valer).
    private void OnCaseCondition(IBreakable _) => Revalidate();

    // Monitor quebrou: a tela dele apaga (Screen = Off).
    private void OnMonitorCondition(IBreakable _) => CheckMonitorRange(force: true);

    private void SetMonitorCondition(Transform screen)
    {
        BreakableEquipment next = screen != null ? screen.GetComponentInParent<BreakableEquipment>(true) : null;
        if (next == monitorCondition) return;
        if (monitorCondition != null) monitorCondition.BreakableChanged -= OnMonitorCondition;
        monitorCondition = next;
        if (monitorCondition != null) monitorCondition.BreakableChanged += OnMonitorCondition;
    }

    // ------------------------------------------------------------------ monitor associado

    /// <summary>Registra a tela do monitor deste PC (chamado pelo <see cref="MonitorDisplay"/> que aponta para cá).</summary>
    public void AttachMonitor(Transform screen)
    {
        monitor = screen;
        SetMonitorCondition(screen);
        CheckMonitorRange(force: true);
    }

    /// <summary>Desfaz o registro (o monitor foi desativado ou passou a apontar para outro PC).</summary>
    public void DetachMonitor(Transform screen)
    {
        if (monitor != screen) return;
        monitor = null;
        SetMonitorCondition(null);
        CheckMonitorRange(force: true);
    }

    /// <summary>Registra o ponto elétrico da parede deste PC (chamado pelo <see cref="WallConnectionPoint"/>).</summary>
    /// <param name="caseRange">Distância horizontal máxima (m) entre o gabinete e o ponto; 0 = sem limite.</param>
    public void AttachWallPoint(Transform point, float caseRange)
    {
        wallPoint = point;
        wallPointRange = caseRange;
    }

    /// <summary>Desfaz o registro do ponto elétrico.</summary>
    public void DetachWallPoint(Transform point)
    {
        if (wallPoint == point) wallPoint = null;
    }

    // O gabinete pode ser carregado: avisa a tela quando o monitor entra ou sai do alcance.
    private void CheckMonitorRange(bool force = false)
    {
        bool near = MonitorAvailable;
        if (!force && near == lastInRange) return;
        lastInRange = near;
        DisplayChanged?.Invoke();
    }

    // ------------------------------------------------------------------ portas e cabos

    /// <summary>
    /// Cabo (ligação) a que a porta pertence por padrão e se ela é a ponta A ("de fora") dele. As duas tomadas da
    /// parede aceitam qualquer cabo de energia: para saber qual cabo está numa tomada, use <see cref="OutletLink"/>.
    /// </summary>
    public static PcLink LinkFor(PcPort port, out bool isEndA)
    {
        isEndA = port == PcPort.Outlet || port == PcPort.MonitorOutlet || port == PcPort.CaseVideo ||
                 port == PcPort.CaseHdmi || port == PcPort.NetworkJack;
        switch (port)
        {
            case PcPort.Outlet: case PcPort.CasePower: return PcLink.Power;
            case PcPort.MonitorOutlet: case PcPort.MonitorPower: return PcLink.MonitorPower;
            case PcPort.CaseVideo: case PcPort.MonitorVideo: return PcLink.Vga;
            case PcPort.CaseHdmi: case PcPort.MonitorHdmi: return PcLink.Hdmi;
            default: return PcLink.Network;
        }
    }

    public static CableType CableFor(PcLink link)
    {
        switch (link)
        {
            case PcLink.Power: case PcLink.MonitorPower: return CableType.Power;
            case PcLink.Vga: return CableType.Video;
            case PcLink.Hdmi: return CableType.Hdmi;
            default: return CableType.Network;
        }
    }

    public static CableType CableFor(PcPort port) => CableFor(LinkFor(port, out _));

    private Cable Get(PcLink link)
    {
        if (!cables.TryGetValue(link, out Cable c)) cables[link] = c = new Cable();
        return c;
    }

    public bool IsComplete(PcLink link) => cables.TryGetValue(link, out Cable c) && c.Complete;

    private static bool IsPowerLink(PcLink link) => link == PcLink.Power || link == PcLink.MonitorPower;

    /// <summary>Índice da tomada da parede (0 ou 1) ou -1 se a porta não é uma tomada.</summary>
    public static int OutletIndex(PcPort port) => port == PcPort.Outlet ? 0 : port == PcPort.MonitorOutlet ? 1 : -1;

    private static PcPort OutletPort(int index) => index == 1 ? PcPort.MonitorOutlet : PcPort.Outlet;

    /// <summary>Cabo de energia que está nesta tomada agora (null = tomada livre ou porta que não é tomada).</summary>
    public PcLink? OutletLink(PcPort outlet)
    {
        int index = OutletIndex(outlet);
        if (index < 0) return null;
        foreach (PcLink link in PowerLinks)
            if (cables.TryGetValue(link, out Cable c) && c.endA && c.outlet == index && IsOnCurrentWall(c)) return link;
        return null;
    }

    // A ponta A do cabo está no ponto elétrico em uso agora (sem ponto registrado: sempre).
    private bool IsOnCurrentWall(Cable c) => c.wall == null || c.wall == CurrentWall;

    /// <summary>
    /// A ponta "de fora" deste cabo NÃO está num ponto elétrico distante: solta, ou no ponto em uso agora. A tela 2D só
    /// desenha cabos de parede que estão aqui (ex.: o cabo do monitor continua na tomada da estação de origem).
    /// </summary>
    public bool IsWallEndHere(PcLink link) => !cables.TryGetValue(link, out Cable c) || !c.endA || IsOnCurrentWall(c);

    /// <summary>Outro PC está usando esta porta do ponto elétrico atual (aparece como plugue, sem linha).</summary>
    public bool IsWallPortTakenByOther(PcPort port)
    {
        WallConnectionPoint w = IsWallPort(port) ? CurrentWall : null;
        ComputerPower other = w != null ? w.OccupantOf(port) : null;
        return other != null && other != this;
    }

    public bool IsPlugged(PcPort port)
    {
        if (!Enum.IsDefined(typeof(PcPort), port)) return false;
        if (IsWallPortTakenByOther(port)) return true;
        if (OutletIndex(port) >= 0) return OutletLink(port).HasValue;
        PcLink link = LinkFor(port, out bool isEndA);
        return cables.TryGetValue(link, out Cable c) && (isEndA ? c.endA && (!IsWallPort(port) || IsOnCurrentWall(c)) : c.endB);
    }

    /// <summary>A ponta A ("de fora") ou B (equipamento) deste cabo está conectada.</summary>
    public bool IsEndPlugged(PcLink link, bool endA) => cables.TryGetValue(link, out Cable c) && (endA ? c.endA : c.endB);

    /// <summary>Ponto elétrico onde está a ponta "de fora" do cabo (energia/rede); null = solta ou sem ponto na cena.</summary>
    public WallConnectionPoint WallOf(PcLink link) => cables.TryGetValue(link, out Cable c) && c.endA ? c.wall : null;

    /// <summary>Portas padrão das duas pontas de um cabo (energia do PC: tomada 1 ↔ fonte; do monitor: tomada 2 ↔ monitor).</summary>
    public static void DefaultPorts(PcLink link, out PcPort endA, out PcPort endB)
    {
        endA = endB = default;
        foreach (PcPort p in Enum.GetValues(typeof(PcPort)))
        {
            if (LinkFor(p, out bool isA) != link) continue;
            if (isA) endA = p; else endB = p;
        }
    }

    /// <summary>Portas onde as pontas deste cabo estão (ou estariam): a de energia usa a tomada em que foi ligada.</summary>
    public void PortsOf(PcLink link, out PcPort endA, out PcPort endB)
    {
        DefaultPorts(link, out endA, out endB);
        if (IsPowerLink(link) && cables.TryGetValue(link, out Cable c) && c.endA && c.outlet >= 0) endA = OutletPort(c.outlet);
    }

    /// <summary>
    /// Clique numa porta. Porta livre: conecta a ponta livre de um cabo já preso ao PC; se não houver, conecta o
    /// cabo da mão (tipo certo). Porta ocupada + mão livre: desconecta essa ponta; soltas as duas, o cabo volta
    /// em <paramref name="itemForHand"/>. Tomadas da parede: qualquer cabo de energia serve (ver <see cref="UseOutlet"/>).
    /// </summary>
    public PortResult UsePort(PcPort port, CableItem heldCable, bool holdingSomething, out GameObject itemForHand)
    {
        // equipamento da porta longe do gabinete (monitor ou ponto elétrico): nada muda, mesmo com uma interface antiga
        itemForHand = null;
        if (!CanReachPort(port)) return PortResult.OutOfReach;
        return UsePortCore(port, heldCable, holdingSomething, out itemForHand);
    }

    private PortResult UsePortCore(PcPort port, CableItem heldCable, bool holdingSomething, out GameObject itemForHand)
    {
        itemForHand = null;
        if (!Enum.IsDefined(typeof(PcPort), port)) return PortResult.Invalid;

        if (IsWallPortTakenByOther(port)) return PortResult.Occupied;

        PortResult result;
        PcLink link;
        int outlet = OutletIndex(port);
        if (outlet >= 0)
        {
            result = UseOutlet(outlet, heldCable, holdingSomething, out itemForHand, out link);
            if (result != PortResult.PluggedFromHand && result != PortResult.PluggedOtherEnd &&
                result != PortResult.Unplugged && result != PortResult.CableReturned) return result;
        }
        else
        {
            link = LinkFor(port, out bool isEndA);
            Cable cable = Get(link);
            // a outra ponta está num ponto elétrico distante, ou no monitor longe: não dá para puxar o cabo até aqui
            if (!IsPlugged(port) && !OtherEndHere(link, isEndA)) return PortResult.OutOfReach;
            if (IsPlugged(port))
            {
                if (holdingSomething) return PortResult.HandsFull;
                SetEnd(cable, isEndA, false, port);
                if (cable.Attached) result = PortResult.Unplugged;
                else
                {
                    itemForHand = cable.item;
                    cable.item = null;
                    result = PortResult.CableReturned;
                }
            }
            else if (cable.Attached || (!isEndA && IsPowerLink(link) && TakeWallOnlyPowerCable(link, out cable)))
            {
                SetEnd(cable, isEndA, true, port);
                result = PortResult.PluggedOtherEnd;
            }
            else
            {
                if (heldCable == null) return PortResult.NeedsCable;
                if (heldCable.Type != CableFor(port)) return PortResult.WrongCable;
                cable.item = heldCable.gameObject;
                SetEnd(cable, isEndA, true, port);
                result = PortResult.PluggedFromHand;
            }
        }

        LastChangedLink = link;
        ConnectionsChanged?.Invoke();
        Revalidate();
        return result;
    }

    private void SetEnd(Cable cable, bool endA, bool value, PcPort port)
    {
        if (endA) cable.endA = value;
        else cable.endB = value;
        if (!endA || !IsWallPort(port)) return;
        if (value) PlugWall(cable, port);
        else UnplugWall(cable, port);
    }

    // Ponta A ligada no ponto elétrico atual: guarda qual é e marca a porta como ocupada por este PC.
    private void PlugWall(Cable c, PcPort port)
    {
        c.wall = CurrentWall;
        if (c.wall != null) c.wall.TryOccupy(port, this);
    }

    private void UnplugWall(Cable c, PcPort port)
    {
        if (c.wall != null) c.wall.Release(port, this);
        c.wall = null;
    }

    // A ponta oposta à porta clicada está solta, ou alcançável daqui (ponto elétrico atual / monitor perto).
    private bool OtherEndHere(PcLink link, bool clickedEndA)
    {
        if (!cables.TryGetValue(link, out Cable c)) return true;
        DefaultPorts(link, out PcPort pa, out PcPort pb);
        if (clickedEndA)
        {
            if (c.endA && IsWallPort(pa)) return false;   // a ponta de parede já está noutro ponto elétrico
            return !c.endB || !IsMonitorPort(pb) || MonitorAvailable;
        }
        if (!c.endA) return true;
        if (IsPowerLink(link) || IsWallPort(pa)) return IsOnCurrentWall(c) && IsWallPointInRange;
        return true;
    }

    /// <summary>
    /// Clique numa tomada da parede (qualquer cabo de energia serve). Ocupada + mão livre: solta o cabo que está nela.
    /// Livre: liga a ponta solta de um cabo de energia já preso a um equipamento (o último mexido, se houver dois);
    /// senão, o cabo de energia da mão, como um cabo ainda sem equipamento (vira o do PC ou o do monitor conforme a
    /// porta do equipamento em que a outra ponta for ligada).
    /// </summary>
    private PortResult UseOutlet(int outlet, CableItem heldCable, bool holdingSomething, out GameObject itemForHand, out PcLink link)
    {
        itemForHand = null;
        PcLink? occupant = OutletLink(OutletPort(outlet));
        if (occupant.HasValue)
        {
            link = occupant.Value;
            if (holdingSomething) return PortResult.HandsFull;
            Cable c = Get(link);
            c.endA = false;
            c.outlet = -1;
            UnplugWall(c, OutletPort(outlet));
            if (c.Attached) return PortResult.Unplugged;
            itemForHand = c.item;
            c.item = null;
            return PortResult.CableReturned;
        }

        PcLink? hanging = null;
        foreach (PcLink l in PowerLinks)
        {
            Cable c = Get(l);
            if (l == PcLink.MonitorPower && !MonitorAvailable) continue;   // o cabo do monitor só vem do monitor que está perto
            if (c.endB && !c.endA && (hanging == null || l == LastChangedLink)) hanging = l;
        }
        if (hanging.HasValue)
        {
            link = hanging.Value;
            Cable c = Get(link);
            c.endA = true;
            c.outlet = outlet;
            PlugWall(c, OutletPort(outlet));
            return PortResult.PluggedOtherEnd;
        }

        link = PcLink.Power;
        if (heldCable == null) return PortResult.NeedsCable;
        if (heldCable.Type != CableType.Power) return PortResult.WrongCable;
        foreach (PcLink l in PowerLinks)
        {
            Cable c = Get(l);
            if (c.Attached) continue;
            link = l;
            c.item = heldCable.gameObject;
            c.endA = true;
            c.outlet = outlet;
            PlugWall(c, OutletPort(outlet));
            return PortResult.PluggedFromHand;
        }
        return PortResult.Invalid;   // os dois cabos de energia já estão em uso
    }

    /// <summary>
    /// Cabo de energia preso só numa tomada (sem equipamento) passa a ser o cabo de <paramref name="target"/> quando o
    /// jogador liga a outra ponta na porta desse equipamento.
    /// </summary>
    private bool TakeWallOnlyPowerCable(PcLink target, out Cable cable)
    {
        cable = Get(target);
        PcLink? from = null;
        foreach (PcLink l in PowerLinks)
        {
            if (l == target) continue;
            Cable c = Get(l);
            if (c.endA && !c.endB && IsOnCurrentWall(c) && (from == null || l == LastChangedLink)) from = l;
        }
        if (!from.HasValue) return false;
        cable = cables[from.Value];
        cables[target] = cable;
        cables[from.Value] = new Cable();
        return true;
    }

    /// <summary>
    /// Clique numa USB (<paramref name="index"/> de 0 a <see cref="UsbPortCount"/> - 1). Livre + pendrive na mão:
    /// conecta (o chamador tira o item da mão e o guarda). Com o pendrive + mão livre: desconecta e devolve o MESMO
    /// objeto em <paramref name="itemForHand"/>. Só um pendrive por vez.
    /// </summary>
    public PortResult UseUsbPort(int index, PendriveItem heldPendrive, bool holdingSomething, out GameObject itemForHand)
    {
        itemForHand = null;
        if (index < 0 || index >= UsbPortCount) return PortResult.Invalid;
        PortResult result;
        if (pendrivePort == index)
        {
            if (holdingSomething) return PortResult.HandsFull;
            itemForHand = pendriveItem;
            pendriveItem = null;
            pendrivePort = -1;
            result = PortResult.CableReturned;
        }
        else if (PendriveConnected) return PortResult.Invalid;
        else
        {
            if (heldPendrive == null) return holdingSomething ? PortResult.WrongCable : PortResult.NeedsCable;
            pendriveItem = heldPendrive.gameObject;
            pendrivePort = index;
            result = PortResult.PluggedFromHand;
        }
        ConnectionsChanged?.Invoke();
        Revalidate();
        DisplayChanged?.Invoke();
        return result;
    }

    /// <summary>Botão Power do gabinete: liga (se os requisitos forem atendidos) ou desliga.</summary>
    public PowerButtonResult PressPower()
    {
        if (state != PcPowerState.Off)
        {
            installRemaining = -1f;
            BootPending = false;
            SetState(PcPowerState.Off);
            return PowerButtonResult.TurnedOff;
        }
        if (!CanReachBios) return PowerButtonResult.MissingRequirements;
        if (CanReachOperatingSystem) SetState(PcPowerState.OperatingSystem);
        else
        {
            // Boot pelo pendrive: só instala se houver HD (sem HD a instalação nem começa).
            bool install = PendriveConnected && Case.HasComponent(PcComponent.Hdd);
            if (install) installRemaining = installDuration;
            SetState(PcPowerState.Bios);
            if (install && installDuration <= 0f) AdvanceInstall(0f);
        }
        return PowerButtonResult.TurnedOn;
    }

    private void Update()
    {
        Tick(Time.deltaTime);
        if (monitor != null) CheckMonitorRange();
    }

    /// <summary>
    /// Avança o que depende do jogador estar vendo a tela (<see cref="Watched"/>): a instalação e o boot pendente.
    /// Público para testes (o Update chama com o deltaTime).
    /// </summary>
    public void Tick(float deltaTime)
    {
        if (!Watched) return;
        if (IsInstallingOperatingSystem) AdvanceInstall(deltaTime);
        else if (BootPending)
        {
            BootPending = false;
            if (state == PcPowerState.Bios && CanReachOperatingSystem) SetState(PcPowerState.OperatingSystem);
        }
    }

    /// <summary>Avança a instalação em andamento (chamado pelo Update quando não está pausada; público para testes).</summary>
    public void AdvanceInstall(float deltaTime)
    {
        if (!IsInstallingOperatingSystem) return;
        int stage = InstallStage;
        installRemaining -= deltaTime;
        if (installRemaining > 0f)
        {
            if (InstallStage != stage) DisplayChanged?.Invoke();
            return;
        }
        installRemaining = -1f;
        // Só grava no HD se tudo ainda estiver de pé (o Revalidate já cancelaria antes; checagem defensiva).
        if (state == PcPowerState.Bios && PendriveConnected && Case.InstallOperatingSystem()) SetState(PcPowerState.OperatingSystem);
        else DisplayChanged?.Invoke();
    }

    /// <summary>Desliga o PC se a configuração atual não sustenta mais o estado em que ele está.</summary>
    public void Revalidate()
    {
        if (state == PcPowerState.Off) return;
        if (!CanReachBios || (state == PcPowerState.OperatingSystem && !Case.HddHasOperatingSystem))
        {
            installRemaining = -1f;
            BootPending = false;
            SetState(PcPowerState.Off);
            return;
        }
        // Instalação interrompida (pendrive ou HD retirado): fica na BIOS, sem sistema.
        if (IsInstallingOperatingSystem && (!PendriveConnected || !Case.HasComponent(PcComponent.Hdd)))
            installRemaining = -1f;
        // Na BIOS, a peça/pendrive que faltava chegou: o próximo passo começa sozinho (sem reiniciar)
        if (state == PcPowerState.Bios && !IsInstallingOperatingSystem)
        {
            BootPending = Case.HddHasOperatingSystem;
            if (!BootPending && PendriveConnected && Case.HasComponent(PcComponent.Hdd)) installRemaining = installDuration;
        }
        DisplayChanged?.Invoke();   // ex.: HD instalado/retirado na BIOS muda a mensagem
    }

    private void SetState(PcPowerState value)
    {
        if (state == value) return;
        state = value;
        PowerStateChanged?.Invoke(state);
        DisplayChanged?.Invoke();
    }

    /// <summary>
    /// O que falta para ligar (nomes em português, na ordem de montagem). Vazio = pode ligar. O vídeo não entra
    /// (sem ele o PC liga e o monitor mostra "Sem sinal").
    /// </summary>
    public List<string> MissingForBios()
    {
        var missing = new List<string>();
        if (CaseBroken) missing.Add("gabinete em bom estado (este quebrou na queda)");
        if (!IsEndPlugged(PcLink.Power, true)) missing.Add("cabo de energia na tomada");
        if (!IsEndPlugged(PcLink.Power, false)) missing.Add("cabo de energia no gabinete");
        if (!IsEndPlugged(PcLink.MonitorPower, true)) missing.Add("cabo de energia do monitor na tomada");
        if (!IsEndPlugged(PcLink.MonitorPower, false)) missing.Add("cabo de energia no monitor");
        if (!Case.HasComponent(PcComponent.Psu)) missing.Add("fonte de alimentação");
        if (!Case.HasComponent(PcComponent.Motherboard)) missing.Add("placa-mãe");
        if (!Case.HasComponent(PcComponent.Ram)) missing.Add("memória RAM");
        if (!Case.HasComponent(PcComponent.CpuCooler)) missing.Add("cooler do processador");
        return missing;
    }
}
