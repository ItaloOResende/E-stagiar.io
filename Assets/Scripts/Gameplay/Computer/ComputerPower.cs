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
    MonitorVideo
}

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
/// Energia e funcionamento do PC: cabos (energia: tomada ↔ gabinete; vídeo: gabinete ↔ monitor), botão Power e
/// o que o monitor mostra. Vai no ROOT LÓGICO do gabinete, ao lado do <see cref="ComputerCase"/> (que guarda as peças).
///
/// Regras (GDD/CH-01):
///  - Desligado ou sem requisitos → tela preta.
///  - Power com energia nas duas pontas + vídeo nas duas pontas + placa-mãe + memória RAM → liga na BIOS.
///  - Os mesmos requisitos + HD → liga no sistema operacional.
///  - Sem os requisitos o botão não liga o PC. Se um requisito some com o PC ligado (cabo solto, peça retirada),
///    o PC desliga: nunca fica num estado incompatível com a configuração. Instalar o HD com o PC na BIOS
///    só tem efeito depois de reiniciar (desligar e ligar).
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
        public bool endA; // Outlet / CaseVideo
        public bool endB; // CasePower / MonitorVideo
        public bool Attached => endA || endB;
        public bool Complete => endA && endB;
    }

    [Tooltip("Somente leitura em jogo: o que o monitor mostra agora.")]
    [SerializeField] private PcPowerState state = PcPowerState.Off;

    private readonly Cable power = new Cable();
    private readonly Cable video = new Cable();
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

    public bool PowerConnected => power.Complete;
    public bool VideoConnected => video.Complete;

    /// <summary>Requisitos para ligar e chegar à BIOS.</summary>
    public bool CanReachBios =>
        PowerConnected && VideoConnected && Case.HasComponent(PcComponent.Motherboard) && Case.HasComponent(PcComponent.Ram);

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

    public static CableType CableFor(PcPort port) =>
        port == PcPort.Outlet || port == PcPort.CasePower ? CableType.Power : CableType.Video;

    public bool IsPlugged(PcPort port)
    {
        switch (port)
        {
            case PcPort.Outlet: return power.endA;
            case PcPort.CasePower: return power.endB;
            case PcPort.CaseVideo: return video.endA;
            case PcPort.MonitorVideo: return video.endB;
            default: return false;
        }
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
        Cable cable = CableFor(port) == CableType.Power ? power : video;
        bool isEndA = port == PcPort.Outlet || port == PcPort.CaseVideo;

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
        if (!power.endA) missing.Add("cabo de energia na tomada");
        if (!power.endB) missing.Add("cabo de energia no gabinete");
        if (!video.endA) missing.Add("cabo de vídeo no gabinete");
        if (!video.endB) missing.Add("cabo de vídeo no monitor");
        if (!Case.HasComponent(PcComponent.Motherboard)) missing.Add("placa-mãe");
        if (!Case.HasComponent(PcComponent.Ram)) missing.Add("memória RAM");
        return missing;
    }
}
