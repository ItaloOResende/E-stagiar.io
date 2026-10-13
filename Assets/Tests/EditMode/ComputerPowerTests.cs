using NUnit.Framework;
using UnityEngine;

public class ComputerPowerTests
{
    private GameObject pc;
    private ComputerCase pcCase;
    private ComputerPower power;
    private CableItem powerCable, monitorPowerCable, videoCable, hdmiCable, networkCable;
    private ComputerPartItem mobo, ram, hdd, psu, cooler, blankHdd;
    private PendriveItem pendrive;
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
        // 'hdd' = unidade que já tem o sistema (como a dos PCs prontos); 'blankHdd' = HD novo da gaveta (vazio).
        hdd.HasOperatingSystem = true;
        blankHdd = Item<ComputerPartItem>("HD_Vazio", c => Set(c, "component", (int)PcComponent.Hdd));
        pendrive = Item<PendriveItem>("Pendrive", _ => { });
        SetFloat(power, "installDuration", 2f);
    }

    private static void SetFloat(Object target, string field, float value) =>
        target.GetType().GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .SetValue(target, value);

    private void ReadyForBios()
    {
        OpenCase();
        ConnectAllCables();
        InstallBiosParts();
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
    // ---- sistema operacional (gravado no HD) e pendrive --------------------------------------------------------

    [Test]
    public void BlankHdd_WithoutPendrive_StaysInBios()
    {
        ReadyForBios();
        Install(blankHdd);
        Assert.IsFalse(blankHdd.HasOperatingSystem);
        Assert.IsFalse(power.CanReachOperatingSystem);
        power.PressPower();
        Assert.AreEqual(PcPowerState.Bios, power.State);
        Assert.IsFalse(power.IsInstallingOperatingSystem);
    }

    [Test]
    public void Pendrive_InstallsOnTheHdd_ThenBootsTheSystem_AndShowsTheNetworkState()
    {
        ReadyForBios();
        Install(blankHdd);
        Assert.AreEqual(PortResult.PluggedFromHand, power.UseUsbPort(1, pendrive, true, out _));
        power.PressPower();
        Assert.AreEqual(PcPowerState.Bios, power.State, "instalando: tela da BIOS");
        Assert.IsTrue(power.IsInstallingOperatingSystem);
        power.AdvanceInstall(1f);
        Assert.AreEqual(PcPowerState.Bios, power.State);
        power.AdvanceInstall(1.1f);
        Assert.AreEqual(PcPowerState.OperatingSystem, power.State);
        Assert.IsTrue(blankHdd.HasOperatingSystem, "o sistema fica gravado no HD");
        Assert.IsFalse(power.Online, "sem cabo de rede: tela de sistema sem conexão");
        Connect(networkCable, PcPort.CaseNetwork, PcPort.NetworkJack);
        Assert.IsTrue(power.Online, "com cabo de rede: tela de conectado");

        // desliga, tira o pendrive e liga: o sistema continua no HD
        power.PressPower();
        Assert.AreEqual(PortResult.CableReturned, power.UseUsbPort(1, null, false, out GameObject back));
        Assert.AreSame(pendrive.gameObject, back);
        power.PressPower();
        Assert.AreEqual(PcPowerState.OperatingSystem, power.State);
    }

    [Test]
    public void Pendrive_WithoutHdd_DoesNotInstall()
    {
        ReadyForBios();
        power.UseUsbPort(0, pendrive, true, out _);
        power.PressPower();
        Assert.AreEqual(PcPowerState.Bios, power.State);
        Assert.IsFalse(power.IsInstallingOperatingSystem);
        power.AdvanceInstall(10f);
        Assert.AreEqual(PcPowerState.Bios, power.State);
        Assert.IsFalse(pcCase.HddHasOperatingSystem);
    }

    [Test]
    public void RemovingThePendriveOrTheHdd_CancelsTheInstall()
    {
        ReadyForBios();
        Install(blankHdd);
        power.UseUsbPort(2, pendrive, true, out _);
        power.PressPower();
        power.UseUsbPort(2, null, false, out _);
        Assert.IsFalse(power.IsInstallingOperatingSystem);
        power.AdvanceInstall(10f);
        Assert.AreEqual(PcPowerState.Bios, power.State);
        Assert.IsFalse(blankHdd.HasOperatingSystem);

        power.PressPower(); // desliga
        power.UseUsbPort(2, pendrive, true, out _);
        power.PressPower();
        Assert.AreEqual(PartSlotResult.Removed, pcCase.UsePartSlot(PcComponent.Hdd, null, false, out _));
        Assert.IsFalse(power.IsInstallingOperatingSystem);
        Assert.IsFalse(blankHdd.HasOperatingSystem);
    }

    [Test]
    public void SwappingToABlankHdd_TheSystemStaysWithTheOldDrive()
    {
        ReadyForBios();
        Install(hdd);
        power.PressPower();
        Assert.AreEqual(PcPowerState.OperatingSystem, power.State);
        Assert.AreEqual(PartSlotResult.Removed, pcCase.UsePartSlot(PcComponent.Hdd, null, false, out _));
        Assert.AreEqual(PcPowerState.Off, power.State);
        Install(blankHdd);
        power.PressPower();
        Assert.AreEqual(PcPowerState.Bios, power.State, "HD vazio: não inicia o sistema");
        Assert.IsTrue(hdd.HasOperatingSystem);
    }

    [Test]
    public void FactoryHdd_KeepsItsSystemWhenRemoved_AsConfiguredByTheScenario()
    {
        var prefab = Item<ComputerPartItem>("HD_Prefab", c => Set(c, "component", (int)PcComponent.Hdd));
        typeof(ComputerCase).GetField("hddItemPrefab", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .SetValue(pcCase, prefab.gameObject);
        OpenCase();

        pcCase.SetComponents(ComputerCase.AllComponents);           // padrão: HD de fábrica com sistema
        Assert.IsTrue(pcCase.HddHasOperatingSystem);
        pcCase.UsePartSlot(PcComponent.Hdd, null, false, out GameObject withOs);
        created.Add(withOs);
        Assert.IsTrue(withOs.GetComponent<ComputerPartItem>().HasOperatingSystem);
        Assert.IsFalse(prefab.HasOperatingSystem, "HD novo (prefab/gaveta) vem vazio");

        pcCase.SetComponents(ComputerCase.AllComponents, false);    // cenário: HD de fábrica sem sistema
        Assert.IsFalse(pcCase.HddHasOperatingSystem);
        Assert.IsTrue(pcCase.InstallOperatingSystem());
        Assert.IsTrue(pcCase.HddHasOperatingSystem);
        pcCase.UsePartSlot(PcComponent.Hdd, null, false, out GameObject installed);
        created.Add(installed);
        Assert.IsTrue(installed.GetComponent<ComputerPartItem>().HasOperatingSystem);
        Assert.IsFalse(pcCase.InstallOperatingSystem(), "sem HD não há onde instalar");
    }

    [Test]
    public void UsbPorts_TakeOnePendrive_AndGiveTheSameObjectBack()
    {
        Assert.AreEqual(PortResult.NeedsCable, power.UseUsbPort(0, null, false, out _));
        Assert.AreEqual(PortResult.WrongCable, power.UseUsbPort(0, null, true, out _), "outro item na mão");
        Assert.AreEqual(PortResult.Invalid, power.UseUsbPort(3, pendrive, true, out _));
        Assert.AreEqual(PortResult.PluggedFromHand, power.UseUsbPort(0, pendrive, true, out _));
        Assert.IsTrue(power.PendriveConnected);
        Assert.AreEqual(0, power.PendrivePort);
        Assert.AreEqual(PortResult.Invalid, power.UseUsbPort(1, pendrive, true, out _), "um pendrive por vez");
        Assert.AreEqual(PortResult.HandsFull, power.UseUsbPort(0, null, true, out _));
        Assert.AreEqual(PortResult.CableReturned, power.UseUsbPort(0, null, false, out GameObject back));
        Assert.AreSame(pendrive.gameObject, back);
        Assert.IsFalse(power.PendriveConnected);
    }
    // ---- telas de boot: sem HD x HD sem sistema x instalando x sistema ------------------------------------------

    [Test]
    public void BootScreens_DistinguishNoDriveFromDriveWithoutSystem()
    {
        ReadyForBios();
        Assert.AreEqual(PcScreen.Off, power.Screen);
        power.PressPower();
        Assert.AreEqual(PcScreen.NoStorage, power.Screen, "sem HD: nenhum dispositivo de armazenamento");
        int changes = 0;
        power.DisplayChanged += () => changes++;
        Install(blankHdd);
        Assert.AreEqual(PcScreen.NoBootableDevice, power.Screen, "HD vazio detectado: no bootable device");
        Assert.Greater(changes, 0, "instalar o HD atualiza a tela");
        power.PressPower(); power.PressPower();                       // reinicia
        Assert.AreEqual(PcScreen.NoBootableDevice, power.Screen, "continua sem sistema depois de reiniciar");
        Assert.AreEqual(PartSlotResult.Removed, pcCase.UsePartSlot(PcComponent.Hdd, null, false, out _));
        Assert.AreEqual(PcScreen.NoStorage, power.Screen);
        Install(hdd);                                                // HD com sistema: só depois de reiniciar
        power.PressPower(); power.PressPower();
        Assert.AreEqual(PcScreen.OperatingSystem, power.Screen);
    }

    [Test]
    public void Install_GoesThroughStages_AndOnlyFinishesAtTheEnd()
    {
        ReadyForBios();
        Install(blankHdd);
        power.UseUsbPort(0, pendrive, true, out _);
        int changes = 0;
        power.DisplayChanged += () => changes++;
        power.PressPower();
        Assert.AreEqual(PcScreen.Installing, power.Screen, "a primeira imagem já é a da instalação");
        Assert.AreEqual(0, power.InstallStage);
        int lastStage = 0;
        for (int i = 0; i < 19; i++)
        {
            power.AdvanceInstall(0.1f);                               // installDuration = 2 s
            Assert.GreaterOrEqual(power.InstallStage, lastStage);
            lastStage = power.InstallStage;
            Assert.IsFalse(blankHdd.HasOperatingSystem, "não grava antes do fim");
        }
        Assert.AreEqual(power.InstallStages - 1, lastStage, "passou por todas as etapas");
        Assert.GreaterOrEqual(changes, power.InstallStages, "cada etapa avisa a tela");
        power.AdvanceInstall(0.2f);
        Assert.AreEqual(PcScreen.OperatingSystem, power.Screen);
        Assert.IsTrue(blankHdd.HasOperatingSystem);
    }

    [Test]
    public void LosingARequirementDuringInstall_CancelsWithoutWritingTheSystem()
    {
        ReadyForBios();
        Install(blankHdd);
        power.UseUsbPort(0, pendrive, true, out _);
        power.PressPower();
        power.AdvanceInstall(1f);
        Assert.AreEqual(PortResult.Unplugged, power.UsePort(PcPort.MonitorOutlet, null, false, out _));
        Assert.AreEqual(PcPowerState.Off, power.State, "sem energia no monitor: desliga");
        Assert.IsFalse(power.IsInstallingOperatingSystem);
        power.AdvanceInstall(10f);
        Assert.IsFalse(blankHdd.HasOperatingSystem);
    }
    // ---- quedas e dano das peças -------------------------------------------------------------------------------

    [Test]
    public void FragilePart_BreaksOnTheFirstDrop_AndBrokenCannotBeInstalled()
    {
        int events = 0;
        mobo.ConditionChanged += _ => events++;
        Assert.AreEqual(PartCondition.Intact, mobo.Condition);
        mobo.RegisterDrop(1);
        Assert.AreEqual(PartCondition.Broken, mobo.Condition, "placa-mãe: quebra na 1ª queda");
        Assert.AreEqual(1, mobo.DropCount);
        mobo.RegisterDrop(1);
        Assert.AreEqual(1, mobo.DropCount, "quebrada não muda mais");
        Assert.AreEqual(1, events);
        ram.RegisterDrop(1);
        Assert.IsTrue(ram.IsBroken, "RAM: quebra na 1ª queda");

        OpenCase();
        Assert.AreEqual(PartSlotResult.BrokenPart, pcCase.UsePartSlot(PcComponent.Motherboard, mobo, true, out _));
        Assert.IsFalse(pcCase.HasComponent(PcComponent.Motherboard));
    }

    [Test]
    public void ResistantPart_FirstDropIsRecorded_SecondBreaks_HddKeepsItsSystemWhileWorking()
    {
        hdd.RegisterDrop(2);
        Assert.AreEqual(1, hdd.DropCount);
        Assert.AreEqual(PartCondition.Damaged, hdd.Condition, "1ª queda: registrada, ainda funciona");
        Assert.IsTrue(hdd.HasOperatingSystem, "queda não mexe no sistema gravado no HD");
        ReadyForBios();
        Install(hdd);
        power.PressPower();
        Assert.AreEqual(PcScreen.OperatingSystem, power.Screen, "HD que caiu uma vez continua funcionando, com o sistema");

        foreach (var p in new[] { psu, cooler }) { p.RegisterDrop(2); Assert.AreEqual(PartCondition.Damaged, p.Condition); }
        foreach (var p in new[] { psu, cooler, blankHdd }) { p.RegisterDrop(2); p.RegisterDrop(2); Assert.IsTrue(p.IsBroken, p.name + ": quebra na 2ª queda"); }
        Assert.AreEqual(PartSlotResult.Removed, pcCase.UsePartSlot(PcComponent.Hdd, null, false, out _));
        Assert.AreEqual(PartSlotResult.BrokenPart, pcCase.UsePartSlot(PcComponent.Hdd, blankHdd, true, out _), "HD quebrado não é instalado");
    }
}
