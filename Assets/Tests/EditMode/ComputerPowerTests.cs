using NUnit.Framework;
using UnityEngine;

public class ComputerPowerTests
{
    private GameObject pc;
    private ComputerCase pcCase;
    private ComputerPower power;
    private CableItem powerCable, monitorPowerCable, videoCable, hdmiCable, networkCable;
    private ComputerPartItem mobo, ram, hdd, psu, cooler;
    private readonly System.Collections.Generic.List<GameObject> created = new System.Collections.Generic.List<GameObject>();

    [SetUp]
    public void SetUp()
    {
        created.Clear();
        pc = new GameObject("PC_Teste");
        created.Add(pc);
        pcCase = pc.AddComponent<ComputerCase>();
        pcCase.Configure(ComputerCase.CreateDefaultTowerBackScrews());
        pcCase.SetComponents(PcComponent.None);
        power = pc.AddComponent<ComputerPower>();

        powerCable = Item<CableItem>("CaboEnergia", c => Set(c, "cableType", (int)CableType.Power));
        monitorPowerCable = Item<CableItem>("CaboEnergiaMonitor", c => Set(c, "cableType", (int)CableType.Power));
        videoCable = Item<CableItem>("CaboVideo", c => Set(c, "cableType", (int)CableType.Video));
        hdmiCable = Item<CableItem>("CaboHDMI", c => Set(c, "cableType", (int)CableType.Hdmi));
        networkCable = Item<CableItem>("CaboRede", c => Set(c, "cableType", (int)CableType.Network));
        mobo = Item<ComputerPartItem>("PlacaMae", c => Set(c, "component", (int)PcComponent.Motherboard));
        ram = Item<ComputerPartItem>("RAM", c => Set(c, "component", (int)PcComponent.Ram));
        hdd = Item<ComputerPartItem>("HD", c => Set(c, "component", (int)PcComponent.Hdd));
        psu = Item<ComputerPartItem>("Fonte", c => Set(c, "component", (int)PcComponent.Psu));
        cooler = Item<ComputerPartItem>("Cooler", c => Set(c, "component", (int)PcComponent.CpuCooler));
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var o in created) if (o != null) Object.DestroyImmediate(o);
    }

    private T Item<T>(string name, System.Action<T> setup) where T : Component
    {
        var c = new GameObject(name).AddComponent<T>();
        created.Add(c.gameObject);
        setup(c);
        return c;
    }

    private static void Set(Object target, string field, int value)
    {
        var f = target.GetType().GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        f.SetValue(target, System.Enum.ToObject(f.FieldType, value));
    }

    private void OpenCase()
    {
        for (int i = 0; i < pcCase.TotalCount; i++) pcCase.TryRemoveScrew(i, ToolType.Screwdriver);
        Assert.AreEqual(CaseOpenResult.Opened, pcCase.TryOpen());
    }

    private void Connect(CableItem cable, PcPort first, PcPort second)
    {
        Assert.AreEqual(PortResult.PluggedFromHand, power.UsePort(first, cable, true, out _), first.ToString());
        Assert.AreEqual(PortResult.PluggedOtherEnd, power.UsePort(second, null, false, out _), second.ToString());
    }

    private void ConnectAllCables(bool hdmi = false)
    {
        Connect(powerCable, PcPort.CasePower, PcPort.Outlet);
        Connect(monitorPowerCable, PcPort.MonitorPower, PcPort.MonitorOutlet);
        if (hdmi) Connect(hdmiCable, PcPort.CaseHdmi, PcPort.MonitorHdmi);
        else Connect(videoCable, PcPort.CaseVideo, PcPort.MonitorVideo);
    }

    private void Install(params ComputerPartItem[] parts)
    {
        foreach (var p in parts)
            Assert.AreEqual(PartSlotResult.Installed, pcCase.UsePartSlot(p.Component, p, true, out _), p.name);
    }

    private void InstallBiosParts() => Install(psu, mobo, ram, cooler);

    // ---- estados -------------------------------------------------------------------------------------------------

    [Test]
    public void EmptyPc_PowerButtonDoesNothing_ScreenStaysBlack()
    {
        Assert.AreEqual(PowerButtonResult.MissingRequirements, power.PressPower());
        Assert.AreEqual(PcPowerState.Off, power.State);
        Assert.AreEqual(10, power.MissingForBios().Count);
    }

    [Test]
    public void CablesPsuMoboRamCooler_BootsToBios()
    {
        OpenCase();
        ConnectAllCables();
        InstallBiosParts();
        Assert.IsEmpty(power.MissingForBios());
        Assert.AreEqual(PowerButtonResult.TurnedOn, power.PressPower());
        Assert.AreEqual(PcPowerState.Bios, power.State);
    }

    [Test]
    public void EverythingPlusHdd_BootsToOperatingSystem()
    {
        OpenCase();
        ConnectAllCables();
        InstallBiosParts();
        Install(hdd);
        power.PressPower();
        Assert.AreEqual(PcPowerState.OperatingSystem, power.State);
    }

    [Test]
    public void HdmiInsteadOfVga_AlsoGivesImage()
    {
        OpenCase();
        ConnectAllCables(hdmi: true);
        InstallBiosParts();
        Assert.IsTrue(power.HdmiConnected);
        Assert.IsFalse(power.VgaConnected);
        power.PressPower();
        Assert.AreEqual(PcPowerState.Bios, power.State);
        Assert.AreEqual(PortResult.Unplugged, power.UsePort(PcPort.MonitorHdmi, null, false, out _));
        Assert.AreEqual(PcPowerState.Off, power.State, "sem nenhum cabo de vídeo o PC desliga");
    }

    [Test]
    public void EachMissingRequirement_PreventsBoot()
    {
        OpenCase();
        InstallBiosParts();
        Install(hdd);
        // só a energia do PC
        Connect(powerCable, PcPort.CasePower, PcPort.Outlet);
        Assert.AreEqual(PowerButtonResult.MissingRequirements, power.PressPower());
        // monitor sem energia
        Connect(videoCable, PcPort.CaseVideo, PcPort.MonitorVideo);
        Assert.AreEqual(PowerButtonResult.MissingRequirements, power.PressPower());
        CollectionAssert.AreEqual(new[] { "cabo de energia do monitor na tomada", "cabo de energia no monitor" }, power.MissingForBios());
        // energia do monitor só numa ponta
        power.UsePort(PcPort.MonitorPower, monitorPowerCable, true, out _);
        Assert.AreEqual(PowerButtonResult.MissingRequirements, power.PressPower());
        power.UsePort(PcPort.MonitorOutlet, null, false, out _);
        // sem cooler e sem fonte
        foreach (PcComponent missing in new[] { PcComponent.CpuCooler, PcComponent.Psu })
        {
            Assert.AreEqual(PartSlotResult.Removed, pcCase.UsePartSlot(missing, null, false, out _));
            Assert.AreEqual(PowerButtonResult.MissingRequirements, power.PressPower(), missing.ToString());
            Assert.AreEqual(PcPowerState.Off, power.State);
        }
    }

    [Test]
    public void LosingRequirementWhileOn_TurnsOff()
    {
        OpenCase();
        ConnectAllCables();
        InstallBiosParts();
        Install(hdd);
        power.PressPower();
        Assert.AreEqual(PortResult.Unplugged, power.UsePort(PcPort.MonitorOutlet, null, false, out _));
        Assert.AreEqual(PcPowerState.Off, power.State, "monitor sem energia");
        power.UsePort(PcPort.MonitorOutlet, null, false, out _);
        power.PressPower();
        Assert.AreEqual(PartSlotResult.Removed, pcCase.UsePartSlot(PcComponent.Psu, null, false, out _));
        Assert.AreEqual(PcPowerState.Off, power.State, "fonte retirada");
    }

    [Test]
    public void RemovingHddInSystem_TurnsOff_AddingHddInBios_NeedsRestart()
    {
        OpenCase();
        ConnectAllCables();
        InstallBiosParts();
        Install(hdd);
        power.PressPower();
        Assert.AreEqual(PartSlotResult.Removed, pcCase.UsePartSlot(PcComponent.Hdd, null, false, out GameObject back));
        Assert.AreSame(hdd.gameObject, back);
        Assert.AreEqual(PcPowerState.Off, power.State);

        power.PressPower();
        Assert.AreEqual(PcPowerState.Bios, power.State);
        Install(hdd);
        Assert.AreEqual(PcPowerState.Bios, power.State);
        power.PressPower(); // desliga
        power.PressPower(); // liga de novo
        Assert.AreEqual(PcPowerState.OperatingSystem, power.State);
    }

    // ---- rede ----------------------------------------------------------------------------------------------------

    [Test]
    public void Network_IsOptionalToBoot_AndMakesTheSystemOnline()
    {
        OpenCase();
        ConnectAllCables();
        InstallBiosParts();
        Install(hdd);
        power.PressPower();
        Assert.AreEqual(PcPowerState.OperatingSystem, power.State, "liga sem rede");
        Assert.IsFalse(power.Online);

        int changes = 0;
        power.ConnectionsChanged += () => changes++;
        Assert.AreEqual(PortResult.WrongCable, power.UsePort(PcPort.CaseNetwork, videoCable, true, out _));
        Connect(networkCable, PcPort.CaseNetwork, PcPort.NetworkJack);
        Assert.IsTrue(power.NetworkConnected);
        Assert.IsTrue(power.Online);
        Assert.AreEqual(2, changes);

        Assert.AreEqual(PortResult.Unplugged, power.UsePort(PcPort.NetworkJack, null, false, out _));
        Assert.IsFalse(power.Online);
        Assert.AreEqual(PcPowerState.OperatingSystem, power.State, "tirar a rede não desliga o PC");
    }

    // ---- cabos ---------------------------------------------------------------------------------------------------

    [Test]
    public void Ports_RejectWrongOrMissingCable()
    {
        Assert.AreEqual(PortResult.NeedsCable, power.UsePort(PcPort.CasePower, null, false, out _));
        Assert.AreEqual(PortResult.WrongCable, power.UsePort(PcPort.CasePower, videoCable, true, out _));
        Assert.AreEqual(PortResult.WrongCable, power.UsePort(PcPort.CaseVideo, networkCable, true, out _));
        Assert.AreEqual(PortResult.WrongCable, power.UsePort(PcPort.MonitorHdmi, videoCable, true, out _));
        Assert.AreEqual(PortResult.WrongCable, power.UsePort(PcPort.MonitorVideo, hdmiCable, true, out _));
        Assert.AreEqual(PortResult.WrongCable, power.UsePort(PcPort.NetworkJack, powerCable, true, out _));
        Assert.IsFalse(power.IsPlugged(PcPort.CasePower));
    }

    [Test]
    public void EachPort_BelongsToOneCable()
    {
        Assert.AreEqual(PcLink.Power, ComputerPower.LinkFor(PcPort.Outlet, out bool a) ); Assert.IsTrue(a);
        Assert.AreEqual(PcLink.MonitorPower, ComputerPower.LinkFor(PcPort.MonitorOutlet, out a)); Assert.IsTrue(a);
        Assert.AreEqual(PcLink.MonitorPower, ComputerPower.LinkFor(PcPort.MonitorPower, out a)); Assert.IsFalse(a);
        Assert.AreEqual(PcLink.Hdmi, ComputerPower.LinkFor(PcPort.CaseHdmi, out _));
        Assert.AreEqual(PcLink.Network, ComputerPower.LinkFor(PcPort.NetworkJack, out a)); Assert.IsTrue(a);
        Assert.AreEqual(CableType.Power, ComputerPower.CableFor(PcPort.MonitorPower));
        Assert.AreEqual(CableType.Hdmi, ComputerPower.CableFor(PcPort.MonitorHdmi));
        Assert.AreEqual(CableType.Network, ComputerPower.CableFor(PcPort.CaseNetwork));
    }

    [Test]
    public void TwoPowerCables_AreIndependent()
    {
        Connect(powerCable, PcPort.CasePower, PcPort.Outlet);
        Assert.AreEqual(PortResult.PluggedFromHand, power.UsePort(PcPort.MonitorPower, monitorPowerCable, true, out _));
        Assert.IsTrue(power.PowerConnected);
        Assert.IsFalse(power.MonitorPowerConnected);
        Assert.AreEqual(PortResult.CableReturned, power.UsePort(PcPort.MonitorPower, null, false, out GameObject back));
        Assert.AreSame(monitorPowerCable.gameObject, back, "o cabo do monitor volta, não o do PC");
        Assert.IsTrue(power.PowerConnected);
    }

    [Test]
    public void UnpluggingBothEnds_ReturnsSameCableObject_NeedsFreeHand()
    {
        power.UsePort(PcPort.CasePower, powerCable, true, out _);
        power.UsePort(PcPort.Outlet, null, false, out _);
        Assert.AreEqual(PortResult.HandsFull, power.UsePort(PcPort.Outlet, null, true, out _));
        Assert.AreEqual(PortResult.Unplugged, power.UsePort(PcPort.Outlet, null, false, out GameObject none));
        Assert.IsNull(none);
        Assert.AreEqual(PortResult.CableReturned, power.UsePort(PcPort.CasePower, null, false, out GameObject back));
        Assert.AreSame(powerCable.gameObject, back);
        Assert.IsFalse(power.IsPlugged(PcPort.CasePower));
    }

    // ---- peças ---------------------------------------------------------------------------------------------------

    [Test]
    public void PartSlots_RequireOpenCase_AndMatchingPart()
    {
        Assert.AreEqual(PartSlotResult.CaseClosed, pcCase.UsePartSlot(PcComponent.Hdd, hdd, true, out _));
        OpenCase();
        Assert.AreEqual(PartSlotResult.NeedsPart, pcCase.UsePartSlot(PcComponent.Hdd, null, false, out _));
        Assert.AreEqual(PartSlotResult.WrongPart, pcCase.UsePartSlot(PcComponent.Hdd, ram, true, out _));
        Assert.AreEqual(PartSlotResult.Installed, pcCase.UsePartSlot(PcComponent.Hdd, hdd, true, out _));
        Assert.IsTrue(pcCase.HasComponent(PcComponent.Hdd));
        Assert.AreEqual(PartSlotResult.HandsFull, pcCase.UsePartSlot(PcComponent.Hdd, null, true, out _));
        Assert.AreEqual(PartSlotResult.Invalid, pcCase.UsePartSlot(PcComponent.Hdd | PcComponent.Ram, null, false, out _));
    }

    [Test]
    public void PsuAndCooler_InstallAndRemove_ReturningTheSameObject()
    {
        OpenCase();
        Assert.AreEqual(PartSlotResult.WrongPart, pcCase.UsePartSlot(PcComponent.Psu, cooler, true, out _));
        Install(psu, mobo, cooler);
        Assert.IsTrue(pcCase.HasComponent(PcComponent.Psu) && pcCase.HasComponent(PcComponent.CpuCooler));
        Assert.AreEqual(PartSlotResult.Removed, pcCase.UsePartSlot(PcComponent.CpuCooler, null, false, out GameObject c));
        Assert.AreSame(cooler.gameObject, c);
        Assert.AreEqual(PartSlotResult.Removed, pcCase.UsePartSlot(PcComponent.Psu, null, false, out GameObject p));
        Assert.AreSame(psu.gameObject, p);
        Assert.IsFalse(pcCase.HasComponent(PcComponent.Psu) || pcCase.HasComponent(PcComponent.CpuCooler));
    }

    [Test]
    public void RamAndCooler_NeedTheMotherboard_WhichCannotLeaveUnderThem()
    {
        OpenCase();
        Assert.IsFalse(pcCase.IsSlotAvailable(PcComponent.Ram));
        Assert.IsTrue(pcCase.IsSlotAvailable(PcComponent.Psu));
        Assert.AreEqual(PartSlotResult.MissingBase, pcCase.UsePartSlot(PcComponent.Ram, ram, true, out _));
        Assert.AreEqual(PartSlotResult.MissingBase, pcCase.UsePartSlot(PcComponent.CpuCooler, cooler, true, out _));
        Install(mobo, ram, cooler);
        Assert.AreEqual(PartSlotResult.BlockedByParts, pcCase.UsePartSlot(PcComponent.Motherboard, null, false, out _));
        pcCase.UsePartSlot(PcComponent.Ram, null, false, out _);
        Assert.AreEqual(PartSlotResult.BlockedByParts, pcCase.UsePartSlot(PcComponent.Motherboard, null, false, out _));
        pcCase.UsePartSlot(PcComponent.CpuCooler, null, false, out _);
        Assert.AreEqual(PartSlotResult.Removed, pcCase.UsePartSlot(PcComponent.Motherboard, null, false, out GameObject m));
        Assert.AreSame(mobo.gameObject, m);
    }

    [Test]
    public void FactoryPart_WithoutPrefab_CannotBeRemoved()
    {
        pcCase.SetComponents(PcComponent.Hdd | PcComponent.Psu);
        OpenCase();
        Assert.AreEqual(PartSlotResult.NoItemToReturn, pcCase.UsePartSlot(PcComponent.Hdd, null, false, out _));
        Assert.AreEqual(PartSlotResult.NoItemToReturn, pcCase.UsePartSlot(PcComponent.Psu, null, false, out _));
        Assert.IsTrue(pcCase.HasComponent(PcComponent.Hdd) && pcCase.HasComponent(PcComponent.Psu));
    }
}
