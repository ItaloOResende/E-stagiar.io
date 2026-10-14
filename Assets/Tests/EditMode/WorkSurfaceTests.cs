using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Abrir o gabinete só apoiado numa superfície de trabalho (<see cref="WorkSurface"/>) e alcance do jogador até o ponto
/// elétrico e o monitor (<see cref="ComputerPower.CanReachPort"/>). Lógica pura, com colliders simples.
/// </summary>
public class WorkSurfaceTests
{
    private readonly System.Collections.Generic.List<GameObject> created = new System.Collections.Generic.List<GameObject>();
    private ComputerCase pcCase;
    private ComputerPower power;

    [SetUp]
    public void SetUp()
    {
        created.Clear();
        var go = New("Gabinete_Teste");
        var box = go.AddComponent<BoxCollider>();                  // base do gabinete na origem do objeto
        box.size = new Vector3(0.2f, 0.4f, 0.4f);
        box.center = new Vector3(0f, 0.2f, 0f);
        pcCase = go.AddComponent<ComputerCase>();
        pcCase.Configure(ComputerCase.CreateDefaultTowerBackScrews());
        power = go.AddComponent<ComputerPower>();
        Assert.IsTrue(pcCase.RequiresWorkSurface, "padrão: exige mesa/bancada");
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var o in created) if (o != null) Object.DestroyImmediate(o);
    }

    private GameObject New(string name)
    {
        var g = new GameObject(name);
        created.Add(g);
        return g;
    }

    /// <summary>Tampo com o topo em <paramref name="top"/> (y). Com <paramref name="workSurface"/>, marcado como mesa.</summary>
    private GameObject Top(string name, float top, bool workSurface)
    {
        GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        created.Add(g);
        g.name = name;
        g.transform.localScale = new Vector3(2f, 0.1f, 2f);
        g.transform.position = new Vector3(0f, top - 0.05f, 0f);
        if (workSurface) g.AddComponent<WorkSurface>();
        return g;
    }

    private void PlaceCaseAt(float y)
    {
        pcCase.transform.position = new Vector3(0f, y + 0.005f, 0f);
        Physics.SyncTransforms();
    }

    private void RemoveRequiredScrews()
    {
        foreach (int i in new[] { 3, 5, 7 }) Assert.AreEqual(ScrewRemovalResult.Removed, pcCase.TryRemoveScrew(i, ToolType.Screwdriver));
    }

    [Test]
    public void NoChao_SemManutencao_NemParafusosNemTampaNemTela()
    {
        Top("Chao", 0f, workSurface: false);
        PlaceCaseAt(0f);
        Assert.IsFalse(pcCase.IsOnWorkSurface);
        Assert.IsTrue(pcCase.BlocksScreen, "a tela de manutenção não abre");
        Assert.AreEqual(ScrewRemovalResult.NotOnWorkSurface, pcCase.TryRemoveScrew(3, ToolType.Screwdriver));
        Assert.IsFalse(pcCase.Screws[3].removed);
        Assert.IsFalse(pcCase.CanOpen);
        foreach (ScrewData s in pcCase.Screws) s.removed = s.required;    // mesmo que já viessem sem os parafusos
        Assert.AreEqual(CaseOpenResult.NotOnWorkSurface, pcCase.TryOpen());
        Assert.AreEqual(ScrewInstallResult.NotOnWorkSurface, pcCase.TryInstallScrew(3, ToolType.Screwdriver));
        Assert.AreEqual(CaseState.Closed, pcCase.State);
        Assert.IsFalse(pcCase.BlocksPickup, "fechado: pode ser carregado até uma mesa");
    }

    [Test]
    public void SobreAMesa_AbreComOsObrigatoriosRemovidos()
    {
        GameObject mesa = Top("Mesa", 0.75f, workSurface: true);
        PlaceCaseAt(0.75f);
        Assert.AreSame(mesa.GetComponent<WorkSurface>(), pcCase.SupportingSurface());
        Assert.AreEqual(CaseOpenResult.ScrewsRemaining, pcCase.TryOpen(), "a regra dos parafusos continua valendo");
        RemoveRequiredScrews();
        Assert.IsFalse(pcCase.BlocksScreen);
        Assert.IsTrue(pcCase.CanOpen);
        Assert.AreEqual(CaseOpenResult.Opened, pcCase.TryOpen());
    }

    [Test]
    public void FilhoDaMesa_ContaComoAMesa_ESuperficieDesligadaNaoConta()
    {
        GameObject mesa = New("Mesa_Raiz");
        var surface = mesa.AddComponent<WorkSurface>();
        GameObject tampo = Top("Tampo", 0.75f, workSurface: false);
        tampo.transform.SetParent(mesa.transform, true);           // collider num filho (ex.: tampo separado)
        PlaceCaseAt(0.75f);
        Assert.IsTrue(pcCase.IsOnWorkSurface, "collider de um filho do objeto marcado");

        surface.AllowsCaseMaintenance = false;
        Assert.IsFalse(pcCase.IsOnWorkSurface, "marcação desligada no Inspector");
    }

    [Test]
    public void LongeDoTampo_OuSemCollider_NaoContaComoApoiado()
    {
        Top("Mesa", 0.75f, workSurface: true);
        PlaceCaseAt(1.1f);                                         // 35 cm acima (caindo, segurado no alto...)
        Assert.IsFalse(pcCase.IsOnWorkSurface);

        PlaceCaseAt(0.75f);
        pcCase.GetComponent<Collider>().enabled = false;           // na mão do jogador os colliders ficam desligados
        Assert.IsFalse(pcCase.IsOnWorkSurface);
    }

    [Test]
    public void Fechar_ExigeOsObrigatorios_EOGabineteFechadoPodeSerCarregado()
    {
        Top("Mesa", 0.75f, workSurface: true);
        PlaceCaseAt(0.75f);
        RemoveRequiredScrews();
        Assert.AreEqual(CaseOpenResult.Opened, pcCase.TryOpen());
        Assert.IsTrue(pcCase.BlocksPickup, "aberto: não pode ser carregado");

        foreach (int i in new[] { 3, 5 }) Assert.AreEqual(ScrewInstallResult.Installed, pcCase.TryInstallScrew(i, ToolType.Screwdriver));
        Assert.AreEqual(CaseState.Open, pcCase.State, "falta um obrigatório: continua aberto");
        Assert.AreEqual(ScrewInstallResult.Installed, pcCase.TryInstallScrew(7, ToolType.Screwdriver));
        Assert.AreEqual(CaseState.Closed, pcCase.State, "recolocar os obrigatórios fecha (os opcionais não contam)");
        Assert.IsFalse(pcCase.BlocksPickup, "fechado: pode ser carregado");
    }

    // ---- alcance do jogador ------------------------------------------------------------------------------------------

    private CableItem Cable(CableType type)
    {
        var c = New("Cabo_" + type).AddComponent<CableItem>();
        typeof(CableItem).GetField("cableType", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(c, type);
        return c;
    }

    [Test]
    public void PontoEletrico_SoComOGabinetePerto_InclusiveNoClique()
    {
        Transform ponto = New("PontoEletrico").transform;
        ponto.position = new Vector3(1f, 0.95f, -0.6f);              // gabinete na origem: ~1,2 m na horizontal
        power.AttachWallPoint(ponto, 2f);
        Assert.IsTrue(power.IsWallPointInRange, "distância horizontal gabinete ↔ ponto (a altura não conta)");
        CableItem energia = Cable(CableType.Power);
        Assert.AreEqual(PortResult.PluggedFromHand, power.UsePort(PcPort.Outlet, energia, true, out _));

        pcCase.transform.position = new Vector3(0f, 0f, 3f);        // gabinete levado para longe do ponto
        Assert.IsFalse(power.IsWallPointInRange);
        foreach (PcPort p in new[] { PcPort.Outlet, PcPort.MonitorOutlet, PcPort.NetworkJack })
            Assert.IsFalse(power.CanReachPort(p), p.ToString());
        Assert.IsTrue(power.CanReachPort(PcPort.CasePower), "portas do próprio gabinete não dependem do ponto");
        Assert.AreEqual(PortResult.OutOfReach, power.UsePort(PcPort.Outlet, null, false, out _), "nem soltar da tomada longe");
        Assert.AreEqual(PortResult.PluggedOtherEnd, power.UsePort(PcPort.CasePower, null, false, out _));
        Assert.IsTrue(power.PowerConnected, "o cabo ligado continua ligado");

        power.DetachWallPoint(ponto);
        Assert.IsTrue(power.IsWallPointInRange, "sem ponto registrado: sem limite (Gabinete.prefab sozinho)");
    }

    [Test]
    public void Monitor_SoPertoDoGabinete_ParaUsarAsPortasDele()
    {
        Transform tela = New("Tela").transform;
        tela.position = new Vector3(0.5f, 1.1f, -0.3f);
        power.AttachMonitor(tela);
        Assert.IsTrue(power.CanReachPort(PcPort.MonitorVideo));
        tela.position = new Vector3(5f, 1.1f, 0f);                // monitor levado para longe do gabinete
        Assert.IsFalse(power.MonitorInRange);
        Assert.IsFalse(power.CanReachPort(PcPort.MonitorVideo));
        Assert.IsTrue(power.CanReachPort(PcPort.CaseVideo));
    }
}
