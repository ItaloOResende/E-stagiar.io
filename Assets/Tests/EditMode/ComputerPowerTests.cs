using NUnit.Framework;
using UnityEngine;

public class ComputerPowerTests
{
    private GameObject pc;
    private ComputerCase pcCase;
    private ComputerPower power;
    private CableItem powerCable, videoCable, networkCable;
    private ComputerPartItem mobo, ram, hdd;
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
        videoCable = Item<CableItem>("CaboVideo", c => Set(c, "cableType", (int)CableType.Video));
        networkCable = Item<CableItem>("CaboRede", c => Set(c, "cableType", (int)CableType.Network));
        mobo = Item<ComputerPartItem>("PlacaMae", c => Set(c, "component", (int)PcComponent.Motherboard));
        ram = Item<ComputerPartItem>("RAM", c => Set(c, "component", (int)PcComponent.Ram));
        hdd = Item<ComputerPartItem>("HD", c => Set(c, "component", (int)PcComponent.Hdd));
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

    private void ConnectAllCables()
    {
        Assert.AreEqual(PortResult.PluggedFromHand, power.UsePort(PcPort.CasePower, powerCable, true, out _));
        Assert.AreEqual(PortResult.PluggedOtherEnd, power.UsePort(PcPort.Outlet, null, false, out _));
        Assert.AreEqual(PortResult.PluggedFromHand, power.UsePort(PcPort.CaseVideo, videoCable, true, out _));
        Assert.AreEqual(PortResult.PluggedOtherEnd, power.UsePort(PcPort.MonitorVideo, null, false, out _));
    }

    private void Install(params ComputerPartItem[] parts)
    {
        foreach (var p in parts)
            Assert.AreEqual(PartSlotResult.Installed, pcCase.UsePartSlot(p.Component, p, true, out _));
    }

    // ---- estados -------------------------------------------------------------------------------------------------

    [Test]
    public void EmptyPc_PowerButtonDoesNothing_ScreenStaysBlack()
    {
        Assert.AreEqual(PowerButtonResult.MissingRequirements, power.PressPower());
        Assert.AreEqual(PcPowerState.Off, power.State);
        Assert.AreEqual(6, power.MissingForBios().Count);
    }

    [Test]
    public void CablesMoboRam_BootsToBios()
    {
        OpenCase();
        ConnectAllCables();
        Install(mobo, ram);
        Assert.AreEqual(PowerButtonResult.TurnedOn, power.PressPower());
        Assert.AreEqual(PcPowerState.Bios, power.State);
    }

    [Test]
    public void EverythingPlusHdd_BootsToOperatingSystem()
    {
        OpenCase();
        ConnectAllCables();
        Install(mobo, ram, hdd);
        power.PressPower();
        Assert.AreEqual(PcPowerState.OperatingSystem, power.State);
    }

    [Test]
    public void EachMissingRequirement_PreventsBoot()
    {
        OpenCase();
        Install(mobo, ram, hdd);
        // só energia (sem vídeo)
        power.UsePort(PcPort.CasePower, powerCable, true, out _);
        power.UsePort(PcPort.Outlet, null, false, out _);
        Assert.AreEqual(PowerButtonResult.MissingRequirements, power.PressPower());
        // vídeo só numa ponta
        power.UsePort(PcPort.CaseVideo, videoCable, true, out _);
        Assert.AreEqual(PowerButtonResult.MissingRequirements, power.PressPower());
        power.UsePort(PcPort.MonitorVideo, null, false, out _);
        // sem RAM
        pcCase.UsePartSlot(PcComponent.Ram, null, false, out _);
        Assert.AreEqual(PowerButtonResult.MissingRequirements, power.PressPower());
        Assert.AreEqual(PcPowerState.Off, power.State);
    }

    [Test]
    public void LosingRequirementWhileOn_TurnsOff()
    {
        OpenCase();
        ConnectAllCables();
        Install(mobo, ram, hdd);
        power.PressPower();
        Assert.AreEqual(PortResult.Unplugged, power.UsePort(PcPort.Outlet, null, false, out _));
        Assert.AreEqual(PcPowerState.Off, power.State);
    }

    [Test]
    public void RemovingHddInSystem_TurnsOff_AddingHddInBios_NeedsRestart()
    {
        OpenCase();
        ConnectAllCables();
        Install(mobo, ram, hdd);
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

    // ---- cabos ---------------------------------------------------------------------------------------------------

    [Test]
    public void Ports_RejectWrongOrMissingCable()
    {
        Assert.AreEqual(PortResult.NeedsCable, power.UsePort(PcPort.CasePower, null, false, out _));
        Assert.AreEqual(PortResult.WrongCable, power.UsePort(PcPort.CasePower, videoCable, true, out _));
        Assert.AreEqual(PortResult.WrongCable, power.UsePort(PcPort.CaseVideo, networkCable, true, out _));
        Assert.IsFalse(power.IsPlugged(PcPort.CasePower));
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
        Assert.AreEqual(PartSlotResult.CaseClosed, pcCase.UsePartSlot(PcComponent.Ram, ram, true, out _));
        OpenCase();
        Assert.AreEqual(PartSlotResult.NeedsPart, pcCase.UsePartSlot(PcComponent.Ram, null, false, out _));
        Assert.AreEqual(PartSlotResult.WrongPart, pcCase.UsePartSlot(PcComponent.Ram, hdd, true, out _));
        Assert.AreEqual(PartSlotResult.Installed, pcCase.UsePartSlot(PcComponent.Ram, ram, true, out _));
        Assert.IsTrue(pcCase.HasComponent(PcComponent.Ram));
        Assert.AreEqual(PartSlotResult.HandsFull, pcCase.UsePartSlot(PcComponent.Ram, null, true, out _));
    }

    [Test]
    public void FactoryPart_WithoutPrefab_CannotBeRemoved()
    {
        pcCase.SetComponents(PcComponent.Hdd);
        OpenCase();
        Assert.AreEqual(PartSlotResult.NoItemToReturn, pcCase.UsePartSlot(PcComponent.Hdd, null, false, out _));
        Assert.IsTrue(pcCase.HasComponent(PcComponent.Hdd));
    }
}
