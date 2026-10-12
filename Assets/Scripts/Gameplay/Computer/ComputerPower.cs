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
///  - Os mesmos requisitos + HD → liga no sistema operacional.
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

    private readonly Dictionary<PcLink, Cable> cables = new Dictionary<PcLink, Cable>();
    private ComputerCase pcCase;
    private bool subscribed;

    /// <summary>Disparado quando o PC liga, desliga ou muda de tela.</summary>
    public event Action<PcPowerState> PowerStateChanged;
    /// <summary>Disparado quando qualquer ponta de cabo é conectada ou desconectada.</summary>
    public event Action ConnectionsChanged;

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

    /// <summary>Requisitos para ligar e chegar ao sistema operacional.</summary>
    public bool CanReachOperatingSystem => CanReachBios && Case.HasComponent(PcComponent.Hdd);

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

    /// <summary>Botão Power do gabinete: liga (se os requisitos forem atendidos) ou desliga.</summary>
    public PowerButtonResult PressPower()
    {
        if (state != PcPowerState.Off)
        {
            SetState(PcPowerState.Off);
            return PowerButtonResult.TurnedOff;
        }
        if (!CanReachBios) return PowerButtonResult.MissingRequirements;
        SetState(CanReachOperatingSystem ? PcPowerState.OperatingSystem : PcPowerState.Bios);
        return PowerButtonResult.TurnedOn;
    }

    /// <summary>Desliga o PC se a configuração atual não sustenta mais o estado em que ele está.</summary>
    public void Revalidate()
    {
        if (state == PcPowerState.Off) return;
        if (!CanReachBios || (state == PcPowerState.OperatingSystem && !Case.HasComponent(PcComponent.Hdd)))
            SetState(PcPowerState.Off);
    }

    private void SetState(PcPowerState value)
    {
        if (state == value) return;
        state = value;
        PowerStateChanged?.Invoke(state);
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
