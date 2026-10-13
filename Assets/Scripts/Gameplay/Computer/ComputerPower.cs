using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Pontas de cabo que a estação de trabalho (tomada + gabinete + monitor) aceita.</summary>
public enum PcPort
{
    /// <summary>Tomada da parede (ponta "de fora" do cabo de energia).</summary>
    Outlet,
    /// <summary>Entrada de energia da fonte, na traseira do gabinete.</summary>
    CasePower,
    /// <summary>Saída VGA na traseira do gabinete.</summary>
    CaseVideo,
    /// <summary>Entrada VGA na traseira do monitor.</summary>
    MonitorVideo,
    /// <summary>Entrada de energia na traseira do monitor.</summary>
    MonitorPower,
    /// <summary>Tomada própria do monitor (ponta "de fora" do cabo de energia do monitor).</summary>
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
/// </summary>
public enum PcScreen { Off, NoStorage, NoBootableDevice, Installing, OperatingSystem }

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
    Invalid
}

public enum PowerButtonResult { TurnedOn, TurnedOff, MissingRequirements }

/// <summary>
/// Energia e funcionamento do PC: cabos (energia: tomada ↔ gabinete e tomada do monitor ↔ monitor; vídeo VGA ou
/// HDMI: gabinete ↔ monitor; rede: ponto de rede ↔ gabinete), botão Power e o que o monitor mostra. Vai no ROOT LÓGICO do gabinete, ao lado do <see cref="ComputerCase"/> (que guarda as peças).
///
/// Regras (GDD/CH-01):
///  - Desligado ou sem requisitos → tela preta.
///  - Power com energia do PC e do monitor (cada cabo nas duas pontas) + vídeo (VGA ou HDMI) nas duas pontas
///    + fonte + placa-mãe + memória RAM + cooler → liga na BIOS.
///  - Os mesmos requisitos + HD COM SISTEMA (gravado na unidade, ver <see cref="ComputerCase.HddHasOperatingSystem"/>)
///    → liga no sistema operacional.
///  - HD sem sistema + pendrive numa USB da traseira: ao ligar, dá boot pelo pendrive (tela da BIOS por
///    <see cref="installDuration"/> s), grava o sistema no HD e vai para o sistema. Sem HD: fica na BIOS, sem instalar.
///    Tirar o pendrive, o HD ou um requisito durante a instalação a cancela.
///  - Sem os requisitos o botão não liga o PC. Se um requisito some com o PC ligado (cabo solto, peça retirada),
///    o PC desliga: nunca fica num estado incompatível com a configuração. Instalar o HD com o PC na BIOS
///    só tem efeito depois de reiniciar (desligar e ligar).
///  - Rede: opcional para ligar; no sistema operacional, o cabo de rede nas duas pontas deixa o PC "conectado".
/// Cada cabo é um item real: ao conectar a primeira ponta ele sai da mão e fica guardado aqui (desativado);
/// ao soltar as duas pontas o MESMO objeto volta para a mão.
/// Sem dependência de UI, modelo, tag ou nome.
/// </summary>
[RequireComponent(typeof(ComputerCase))]
public class ComputerPower : MonoBehaviour
{
    private class Cable
    {
        public GameObject item;
        public bool endA; // ponta "de fora": tomada / saída de vídeo do gabinete / ponto de rede
        public bool endB; // ponta do equipamento: gabinete / monitor
        public bool Attached => endA || endB;
        public bool Complete => endA && endB;
    }

    [Tooltip("Somente leitura em jogo: o que o monitor mostra agora.")]
    [SerializeField] private PcPowerState state = PcPowerState.Off;
    [Tooltip("Tempo (s) da instalação do sistema pelo pendrive (tela de progresso). 0 = instantâneo.")]
    [Min(0f)] [SerializeField] private float installDuration = 6f;
    [Tooltip("Quantas etapas visuais a instalação tem (uma imagem de progresso por etapa).")]
    [Min(1)] [SerializeField] private int installStages = 5;

    /// <summary>Portas USB da traseira do gabinete (qualquer uma aceita o pendrive).</summary>
    public const int UsbPortCount = 3;

    private readonly Dictionary<PcLink, Cable> cables = new Dictionary<PcLink, Cable>();
    private ComputerCase pcCase;
    private bool subscribed;
    private GameObject pendriveItem;
    private int pendrivePort = -1;
    private float installRemaining = -1f;

    /// <summary>Disparado quando o PC liga, desliga ou muda de tela.</summary>
    public event Action<PcPowerState> PowerStateChanged;
    /// <summary>Disparado quando qualquer ponta de cabo é conectada ou desconectada.</summary>
    public event Action ConnectionsChanged;
    /// <summary>Disparado quando o que o monitor deve mostrar muda (<see cref="Screen"/>, etapa da instalação, rede).</summary>
    public event Action DisplayChanged;

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
    public bool NetworkConnected => IsComplete(PcLink.Network);
    /// <summary>No sistema operacional e com o cabo de rede ligado.</summary>
    public bool Online => state == PcPowerState.OperatingSystem && NetworkConnected;

    /// <summary>Requisitos para ligar e chegar à BIOS.</summary>
    public bool CanReachBios =>
        PowerConnected && MonitorPowerConnected && VideoConnected && Case.HasComponent(PcComponent.Psu) &&
        Case.HasComponent(PcComponent.Motherboard) && Case.HasComponent(PcComponent.Ram) && Case.HasComponent(PcComponent.CpuCooler);

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

    /// <summary>Tela exata do monitor agora. A detecção do HD na BIOS é ao vivo (instalar/retirar o HD muda a mensagem).</summary>
    public PcScreen Screen
    {
        get
        {
            if (state == PcPowerState.Off) return PcScreen.Off;
            if (state == PcPowerState.OperatingSystem) return PcScreen.OperatingSystem;
            if (IsInstallingOperatingSystem) return PcScreen.Installing;
            return Case.HasComponent(PcComponent.Hdd) ? PcScreen.NoBootableDevice : PcScreen.NoStorage;
        }
    }

    private void OnEnable() => _ = Case;
    private void OnDisable()
    {
        if (subscribed && pcCase != null) pcCase.ComponentsChanged -= Revalidate;
        subscribed = false;
    }

    // Também chamado pelo getter de Case: vale mesmo sem OnEnable (ex.: testes em Edit Mode).
    private void Subscribe()
    {
        if (subscribed || pcCase == null) return;
        pcCase.ComponentsChanged += Revalidate;
        subscribed = true;
    }

    /// <summary>Cabo (ligação) a que a porta pertence e se ela é a ponta A ("de fora") dele.</summary>
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

    public bool IsPlugged(PcPort port)
    {
        if (!Enum.IsDefined(typeof(PcPort), port)) return false;
        PcLink link = LinkFor(port, out bool isEndA);
        return cables.TryGetValue(link, out Cable c) && (isEndA ? c.endA : c.endB);
    }

    /// <summary>
    /// Clique numa porta. Porta livre: conecta a ponta livre de um cabo já preso ao PC; se não houver, conecta o
    /// cabo da mão (tipo certo). Porta ocupada + mão livre: desconecta essa ponta; soltas as duas, o cabo volta
    /// em <paramref name="itemForHand"/>.
    /// </summary>
    public PortResult UsePort(PcPort port, CableItem heldCable, bool holdingSomething, out GameObject itemForHand)
    {
        itemForHand = null;
        if (!Enum.IsDefined(typeof(PcPort), port)) return PortResult.Invalid;
        PcLink link = LinkFor(port, out bool isEndA);
        Cable cable = Get(link);

        PortResult result;
        if (IsPlugged(port))
        {
            if (holdingSomething) return PortResult.HandsFull;
            SetEnd(cable, isEndA, false);
            if (cable.Attached) result = PortResult.Unplugged;
            else
            {
                itemForHand = cable.item;
                cable.item = null;
                result = PortResult.CableReturned;
            }
        }
        else if (cable.Attached)
        {
            SetEnd(cable, isEndA, true);
            result = PortResult.PluggedOtherEnd;
        }
        else
        {
            if (heldCable == null) return PortResult.NeedsCable;
            if (heldCable.Type != CableFor(port)) return PortResult.WrongCable;
            cable.item = heldCable.gameObject;
            SetEnd(cable, isEndA, true);
            result = PortResult.PluggedFromHand;
        }

        ConnectionsChanged?.Invoke();
        Revalidate();
        return result;
    }

    private static void SetEnd(Cable cable, bool endA, bool value)
    {
        if (endA) cable.endA = value;
        else cable.endB = value;
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
        if (IsInstallingOperatingSystem && !InstallPaused) AdvanceInstall(Time.deltaTime);
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
            SetState(PcPowerState.Off);
            return;
        }
        // Instalação interrompida (pendrive ou HD retirado): fica na BIOS, sem sistema.
        if (IsInstallingOperatingSystem && (!PendriveConnected || !Case.HasComponent(PcComponent.Hdd)))
            installRemaining = -1f;
        DisplayChanged?.Invoke();   // ex.: HD instalado/retirado na BIOS muda a mensagem
    }

    private void SetState(PcPowerState value)
    {
        if (state == value) return;
        state = value;
        PowerStateChanged?.Invoke(state);
        DisplayChanged?.Invoke();
    }

    /// <summary>O que falta para ligar (nomes em português, na ordem de montagem). Vazio = pode ligar.</summary>
    public List<string> MissingForBios()
    {
        var missing = new List<string>();
        if (!IsPlugged(PcPort.Outlet)) missing.Add("cabo de energia na tomada");
        if (!IsPlugged(PcPort.CasePower)) missing.Add("cabo de energia no gabinete");
        if (!IsPlugged(PcPort.MonitorOutlet)) missing.Add("cabo de energia do monitor na tomada");
        if (!IsPlugged(PcPort.MonitorPower)) missing.Add("cabo de energia no monitor");
        if (!VideoConnected)
        {
            bool hdmi = Get(PcLink.Hdmi).Attached && !Get(PcLink.Vga).Attached;
            if (!IsPlugged(hdmi ? PcPort.CaseHdmi : PcPort.CaseVideo)) missing.Add("cabo de vídeo no gabinete");
            if (!IsPlugged(hdmi ? PcPort.MonitorHdmi : PcPort.MonitorVideo)) missing.Add("cabo de vídeo no monitor");
        }
        if (!Case.HasComponent(PcComponent.Psu)) missing.Add("fonte de alimentação");
        if (!Case.HasComponent(PcComponent.Motherboard)) missing.Add("placa-mãe");
        if (!Case.HasComponent(PcComponent.Ram)) missing.Add("memória RAM");
        if (!Case.HasComponent(PcComponent.CpuCooler)) missing.Add("cooler do processador");
        return missing;
    }
}
