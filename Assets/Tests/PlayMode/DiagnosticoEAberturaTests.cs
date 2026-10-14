using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Play Mode com os prefabs reais (PC_Escritorio, UI_Screen_Gabinete): "Sem sinal" na tela 2D e no monitor 3D,
/// monitor só com o gabinete por perto (inclusive depois de carregado), cabos de energia em qualquer tomada (2D e 3D),
/// cabo de rede desenhado na frente da placa da parede, Avançar bloqueado até tirar os parafusos obrigatórios (sem
/// avisos de texto) e gabinete aberto que não pode ser carregado. Componentes do Assembly-CSharp por reflexão.
/// O Gabinete.prefab sozinho fica sem mesa embaixo: nesses testes a exigência da superfície de trabalho é desligada
/// (ela tem testes próprios em EstacaoInteracaoTests).
/// </summary>
public class DiagnosticoEAberturaTests
{
    private const string PcPath = "Assets/Prefabs/Gameplay/PC_Escritorio.prefab";
    private const string CasePath = "Assets/Prefabs/Gameplay/Gabinete.prefab";
    private const string ScreenPath = "Assets/Prefabs/UI/UI_Screen_Gabinete.prefab";

    private GameObject player, canvasGo;
    private Component manager, holder, detector;
    private readonly List<GameObject> created = new List<GameObject>();

    private static GameObject Prefab(string path)
    {
        Type db = Type.GetType("UnityEditor.AssetDatabase, UnityEditor", true);
        var go = (GameObject)db.GetMethod("LoadAssetAtPath", new[] { typeof(string), typeof(Type) }).Invoke(null, new object[] { path, typeof(GameObject) });
        Assert.IsNotNull(go, path);
        return go;
    }

    private static Type T(string name) => Type.GetType(name + ", Assembly-CSharp", true);
    private static void Set(object target, string field, object value) =>
        target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(target, value);
    private static object Field(object target, string field) =>
        target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(target);
    private static object Call(object target, string method, params object[] args) =>
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public).Invoke(target, args);
    private static object Get(object target, string property) =>
        target.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.Public).GetValue(target);

    [SetUp]
    public void SetUp()
    {
        canvasGo = new GameObject("TestCanvas", typeof(Canvas));
        player = new GameObject("TestPlayer");
        var hold = new GameObject("HoldPoint").transform;
        hold.SetParent(player.transform, false);
        holder = player.AddComponent(T("ItemHolder"));
        Set(holder, "holdPoint", hold);
        detector = player.AddComponent(T("InteractionDetector"));
        ((Behaviour)detector).enabled = false;
        Set(detector, "holder", holder);
        manager = player.AddComponent(T("UIScreenManager"));
        Set(manager, "screenRoot", canvasGo.transform);
        Set(manager, "disableWhileOpen", new Behaviour[0]);
        Set(detector, "screens", manager);
    }

    [TearDown]
    public void TearDown()
    {
        Object.Destroy(player);
        Object.Destroy(canvasGo);
        foreach (GameObject g in created) if (g != null) Object.Destroy(g);
        created.Clear();
        foreach (var c in Object.FindObjectsByType<ComputerCase>(FindObjectsSortMode.None)) Object.Destroy(c.gameObject);
    }

    private GameObject Spawn(string path, Vector3 pos)
    {
        GameObject g = Object.Instantiate(Prefab(path), pos, Quaternion.identity);
        created.Add(g);
        return g;
    }

    // PC_Escritorio sem mesa embaixo: o gabinete (Rigidbody com física) cairia durante o teste. Parado, o teste só mede
    // o que interessa (tela, distância, cabos), sem depender de quantos quadros a física rodou.
    private static ComputerPower Frozen(ComputerPower power)
    {
        power.GetComponent<Rigidbody>().isKinematic = true;
        return power;
    }

    private GameObject SpawnLooseCase(Vector3 pos)
    {
        GameObject g = Spawn(CasePath, pos);
        g.GetComponent<ComputerCase>().RequiresWorkSurface = false;
        return g;
    }

    // Nenhum texto de aviso na tela (os avisos de bloqueio/desbloqueio/fechamento saíram: o feedback é só visual).
    private static void AssertNoFeedbackText(GameObject screen)
    {
        Assert.IsNull(screen.transform.Find("Aviso"), "a faixa de avisos de texto não existe mais");
        foreach (Text t in screen.GetComponentsInChildren<Text>())
            foreach (string s in new[] { "bloquead", "desbloquead", "fechado", "Acesso" })
                StringAssert.DoesNotContain(s, t.text, "texto visível: " + t.name);
    }

    private CableItem Cable(string prefab) => Spawn("Assets/Prefabs/Gameplay/" + prefab + ".prefab", Vector3.down * 10f).GetComponent<CableItem>();

    private GameObject OpenScreen(GameObject source)
    {
        Component screenComp = Prefab(ScreenPath).GetComponent(T("UIScreen"));
        Assert.IsTrue((bool)Call(manager, "Open", screenComp, source, detector));
        return canvasGo.transform.GetChild(0).gameObject;
    }

    private static Component Controller(GameObject screen) => screen.GetComponent(T("ComputerScreenController"));
    private static Image MonitorScreen(GameObject screen) => screen.transform.Find("Frente/Monitor/Area/Tela").GetComponent<Image>();
    private static Button ButtonNamed(GameObject screen, string name) => screen.GetComponentsInChildren<Button>(true).First(b => b.name == name);

    private void PowerCables(ComputerPower power)
    {
        Assert.AreEqual(PortResult.PluggedFromHand, power.UsePort(PcPort.CasePower, Cable("Pickup_CaboEnergia"), true, out _));
        Assert.AreEqual(PortResult.PluggedOtherEnd, power.UsePort(PcPort.Outlet, null, false, out _));
        Assert.AreEqual(PortResult.PluggedFromHand, power.UsePort(PcPort.MonitorPower, Cable("Pickup_CaboEnergia"), true, out _));
        Assert.AreEqual(PortResult.PluggedOtherEnd, power.UsePort(PcPort.MonitorOutlet, null, false, out _));
    }

    // ---- 1. sem sinal ----------------------------------------------------------------------------------------------

    [UnityTest]
    public IEnumerator SemCaboDeVideo_LigaEMostraSemSinal_No2DENo3D_AteLigarOVga()
    {
        GameObject pc = Spawn(PcPath, Vector3.zero);
        var power = Frozen(pc.GetComponentInChildren<ComputerPower>());
        var display = pc.GetComponentInChildren<MonitorDisplay>();
        yield return null;
        PowerCables(power);
        GameObject screen = OpenScreen(power.gameObject); yield return null;
        Image tela = MonitorScreen(screen);
        Assert.IsNull(tela.sprite, "desligado: tela preta");

        ButtonNamed(screen, "BotaoPower").onClick.Invoke(); yield return null;
        Assert.AreEqual(PcPowerState.OperatingSystem, power.State, "o PC completo liga sem cabo de vídeo");
        Assert.AreEqual(PcScreen.NoSignal, power.Screen);
        Assert.AreEqual("PH_ui_tela_sem_sinal", tela.sprite.name, "tela 2D: sem sinal");
        Assert.AreEqual("PH_ui_tela_sem_sinal", BaseMap(display).name, "monitor 3D: sem sinal");

        power.UsePort(PcPort.CaseVideo, Cable("Pickup_CaboVideo"), true, out _);
        power.UsePort(PcPort.MonitorVideo, null, false, out _);
        yield return null;
        Assert.AreEqual("PH_ui_tela_sistema", tela.sprite.name, "VGA ligado: o estado real (sistema)");
        Assert.AreEqual("PH_ui_tela_sistema", BaseMap(display).name);

        power.UsePort(PcPort.MonitorVideo, null, false, out _);
        yield return null;
        Assert.AreEqual("PH_ui_tela_sem_sinal", tela.sprite.name, "VGA solto durante o uso");
        Assert.AreEqual(PcPowerState.OperatingSystem, power.State);
    }

    private static Texture BaseMap(MonitorDisplay d)
    {
        var block = new MaterialPropertyBlock();
        d.GetComponent<Renderer>().GetPropertyBlock(block);
        return block.GetTexture("_BaseMap");
    }

    // ---- 2. proximidade do monitor ---------------------------------------------------------------------------------

    [UnityTest]
    public IEnumerator MonitorNaTela_SoComOGabinetePerto_InclusiveDepoisDeCarregado()
    {
        GameObject pc = Spawn(PcPath, Vector3.zero);
        var power = Frozen(pc.GetComponentInChildren<ComputerPower>());
        var display = pc.GetComponentInChildren<MonitorDisplay>();
        yield return null;
        Assert.AreSame(display.transform, power.Monitor, "o monitor do próprio PC se registra no gabinete");
        Assert.IsTrue(power.MonitorInRange, "posição inicial da estação");

        PowerCables(power);
        power.UsePort(PcPort.CaseHdmi, Cable("Pickup_CaboHDMI"), true, out _);
        power.UsePort(PcPort.MonitorHdmi, null, false, out _);
        power.PressPower();
        GameObject screen = OpenScreen(power.gameObject); yield return null;
        GameObject frenteMonitor = screen.transform.Find("Frente/Monitor").gameObject;
        Assert.IsTrue(frenteMonitor.activeSelf);
        Assert.AreEqual(PcScreen.OperatingSystem, power.Screen);

        // carregado para longe (sai da hierarquia da estação, como ao soltar)
        power.transform.SetParent(null, true);
        power.transform.position += Vector3.right * 4f;
        yield return null; yield return null;
        Assert.IsFalse(power.MonitorInRange);
        Assert.IsFalse(frenteMonitor.activeSelf, "monitor longe: some da tela 2D");
        Assert.IsFalse(screen.transform.Find("Traseira/Monitor").gameObject.activeSelf, "...e as portas dele também");
        Assert.AreEqual(PcScreen.OperatingSystem, power.Screen, "longe: a imagem continua (o cabo segue ligado; a distância só mexe na tela 2D)");

        // trazido de volta para o outro lado do monitor
        power.transform.position = display.transform.position + Vector3.left * 0.6f;
        yield return null; yield return null;
        Assert.IsTrue(power.MonitorInRange);
        Assert.IsTrue(frenteMonitor.activeSelf, "perto de novo: volta");
        Assert.AreEqual(PcScreen.OperatingSystem, power.Screen);
    }

    // ---- 3. cabo de rede na placa da parede ------------------------------------------------------------------------

    [UnityTest]
    public IEnumerator Traseira_PlacaDoPontoDeRede_FicaAtrasDoCabo()
    {
        GameObject g = SpawnLooseCase(Vector3.zero);
        yield return null;
        GameObject screen = OpenScreen(g); yield return null;
        Transform traseira = screen.transform.Find("Traseira");
        int cabos = traseira.Find("Cabos").GetSiblingIndex();
        Assert.Less(traseira.Find("PainelConexoes").GetSiblingIndex(), cabos, "o painel das placas é desenhado antes dos cabos (cabo na frente do plugue)");
        foreach (string placa in new[] { "PontoRede", "Tomada", "Tomada_Monitor" })
            Assert.IsNotNull(traseira.Find("PainelConexoes/" + placa), placa + " no painel do ponto elétrico");

        var power = g.GetComponent<ComputerPower>();
        Assert.AreEqual(PortResult.PluggedFromHand, power.UsePort(PcPort.NetworkJack, Cable("Pickup_CaboRede"), true, out _));
        Assert.AreEqual(PortResult.PluggedOtherEnd, power.UsePort(PcPort.CaseNetwork, null, false, out _));
        Assert.IsTrue(power.NetworkConnected, "a conexão do cabo de rede continua funcionando");
    }

    // ---- 4. tomadas compatíveis ------------------------------------------------------------------------------------

    [UnityTest]
    public IEnumerator CaboDeEnergiaNaOutraTomada_DesenhadoNaTomadaReal_No2DENo3D()
    {
        GameObject pc = Spawn(PcPath, Vector3.zero);
        var power = Frozen(pc.GetComponentInChildren<ComputerPower>());
        var view = power.GetComponent<PcCableView3D>();
        yield return null;
        GameObject screen = OpenScreen(power.gameObject); yield return null;
        Button next = ButtonNamed(screen, "BotaoAvancar");
        next.onClick.Invoke(); yield return null;                                  // Traseira

        Assert.AreEqual(PortResult.PluggedFromHand, power.UsePort(PcPort.CasePower, Cable("Pickup_CaboEnergia"), true, out _));
        Assert.AreEqual(PortResult.PluggedOtherEnd, power.UsePort(PcPort.MonitorOutlet, null, false, out _), "PC na tomada 2");
        yield return null; yield return null;
        Assert.IsTrue(power.PowerConnected);

        Transform plugA = power.transform.Find("Cabo3D_Power/Plugue_A");
        Transform outlet2 = view.Anchors.First(a => a.port == PcPort.MonitorOutlet).anchor;
        Assert.IsNotNull(plugA);
        Assert.Less(Vector3.Distance(plugA.position, outlet2.position), 0.001f, "3D: plugue na tomada 2");

        Component line = screen.GetComponentsInChildren(T("CableLineView"), true).First(l => (PcLink)Get(l, "Link") == PcLink.Power);
        RectTransform from = (RectTransform)Field(line, "from");
        Assert.AreEqual("Porta_TomadaMonitor", from.parent.name, "2D: o cabo do PC sai (do plugue) da tomada em que foi ligado");
        Assert.IsTrue(screen.transform.Find("Traseira/PainelConexoes/Tomada_Monitor/Area/Porta_TomadaMonitor/Plugue").gameObject.activeSelf);
        Assert.IsFalse(screen.transform.Find("Traseira/PainelConexoes/Tomada/Area/Porta_Tomada/Plugue").gameObject.activeSelf);
    }

    // ---- 5. abertura do gabinete -----------------------------------------------------------------------------------

    [UnityTest]
    public IEnumerator Avancar_BloqueadoAteTirarOsParafusosObrigatorios_DepoisLibera()
    {
        GameObject g = SpawnLooseCase(Vector3.zero);
        var pcCase = g.GetComponent<ComputerCase>();
        yield return null;
        GameObject screen = OpenScreen(g); yield return null;
        Component ctrl = Controller(screen);
        Button next = ButtonNamed(screen, "BotaoAvancar");
        var group = next.GetComponent<CanvasGroup>();
        Assert.IsFalse((bool)Get(ctrl, "NextLocked"), "na Frente o Avançar só leva à Traseira");
        Assert.AreEqual(1f, group.alpha);

        next.onClick.Invoke(); yield return null;
        Assert.AreEqual("Rear", Get(ctrl, "CurrentView").ToString());
        Assert.IsTrue((bool)Get(ctrl, "NextLocked"));
        Assert.Less(group.alpha, 0.6f, "bloqueado: opacidade reduzida");
        Assert.IsTrue(next.interactable, "continua clicável (o botão balança)");

        Component[] screws = screen.GetComponentsInChildren(T("ScrewView"), true);
        next.onClick.Invoke(); yield return null;
        Assert.AreEqual("Rear", Get(ctrl, "CurrentView").ToString(), "não avança");
        AssertNoFeedbackText(screen);
        yield return new WaitForSecondsRealtime(0.1f);
        Assert.AreNotEqual(0f, ((RectTransform)next.transform).anchoredPosition.x - (float)((Vector2)Field(ctrl, "nextRestPosition")).x,
            "o Avançar balança (indisponível)");
        foreach (Component s in screws)
            Assert.AreEqual(Vector2.zero, ((RectTransform)s.transform).anchoredPosition, "os parafusos não são destacados (não entregam a solução)");
        Assert.AreEqual(CaseState.Closed, pcCase.State);

        // a chave de fenda na mão e só os opcionais: continua bloqueado (vale o estado real dos parafusos)
        var tool = Spawn("Assets/Prefabs/Gameplay/Pickup_Ferramenta.prefab", Vector3.down * 10f);
        Assert.IsTrue((bool)Call(holder, "Pick", tool.GetComponent(T("PickupInteractable"))));
        Component[] views = screen.GetComponentsInChildren(T("ScrewView"), true);
        foreach (int i in new[] { 0, 1, 2, 4, 6 }) { views[i].GetComponent<Button>().onClick.Invoke(); yield return null; }
        Assert.IsTrue((bool)Get(ctrl, "NextLocked"), "parafusos opcionais não liberam");
        foreach (int i in new[] { 3, 5 }) { views[i].GetComponent<Button>().onClick.Invoke(); yield return null; }
        Assert.IsTrue((bool)Get(ctrl, "NextLocked"));
        views[7].GetComponent<Button>().onClick.Invoke(); yield return null;
        Assert.IsFalse((bool)Get(ctrl, "NextLocked"), "último obrigatório: liberado");
        Assert.Greater(next.transform.localScale.x, 1f, "o Avançar pulsa ao desbloquear");
        AssertNoFeedbackText(screen);
        yield return new WaitForSecondsRealtime(0.8f);
        Assert.AreEqual(1f, group.alpha, 0.001f, "opacidade total depois da animação");
        Assert.AreEqual(Vector3.one, next.transform.localScale);

        next.onClick.Invoke(); yield return null;
        Assert.AreEqual("Interior", Get(ctrl, "CurrentView").ToString());
        Assert.AreEqual(CaseState.Open, pcCase.State);
    }

    [UnityTest]
    public IEnumerator GabineteAberto_NaoPodeSerPego_MasPecasETelaContinuam()
    {
        GameObject g = SpawnLooseCase(new Vector3(5f, 0f, 0f));     // fora de qualquer estação
        var pcCase = g.GetComponent<ComputerCase>();
        Component pickup = g.GetComponent(T("PickupInteractable"));
        Component screenOpener = g.GetComponent(T("UIScreenInteractable"));
        yield return null;
        Assert.IsFalse((bool)Get(pickup, "IsPickupBlocked"));
        Assert.IsTrue((bool)Call(pickup, "CanInteract", detector), "fechado: pode pegar");

        for (int i = 0; i < pcCase.TotalCount; i++) pcCase.TryRemoveScrew(i, ToolType.Screwdriver);
        pcCase.TryOpen();
        Assert.IsTrue((bool)Get(pickup, "IsPickupBlocked"));
        Assert.IsFalse((bool)Call(pickup, "CanInteract", detector), "aberto: sem 'Pegar'");
        Call(pickup, "Interact", detector);
        Assert.IsFalse((bool)Get(holder, "IsHolding"), "nem forçando o clique");
        Assert.IsTrue(g.GetComponent<Collider>().enabled, "o collider continua (mira e tela)");
        Assert.IsTrue((bool)Call(screenOpener, "CanInteractSecondary", detector), "a tela de manutenção continua abrindo");

        var hdd = Spawn("Assets/Prefabs/Gameplay/Pickup_HD.prefab", Vector3.down * 10f).GetComponent<ComputerPartItem>();
        pcCase.SetComponents(ComputerCase.AllComponents & ~PcComponent.Hdd);
        Assert.AreEqual(PartSlotResult.Installed, pcCase.UsePartSlot(PcComponent.Hdd, hdd, true, out _), "componentes continuam funcionando");
    }

    // ---- 6. estações independentes ---------------------------------------------------------------------------------

    [UnityTest]
    public IEnumerator DuasEstacoes_NaoCompartilhamConexoesMonitorNemParafusos()
    {
        GameObject a = Spawn("Assets/Prefabs/Gameplay/Estacao_Trabalho.prefab", new Vector3(-5f, 0f, 0f));
        GameObject b = Spawn("Assets/Prefabs/Gameplay/Estacao_Trabalho.prefab", new Vector3(5f, 0f, 0f));
        yield return null;
        var pa = a.GetComponentInChildren<ComputerPower>();
        var pb = b.GetComponentInChildren<ComputerPower>();
        Assert.IsTrue(pa.Monitor.IsChildOf(a.transform), "cada gabinete com o monitor da própria estação");
        Assert.IsTrue(pb.Monitor.IsChildOf(b.transform));

        PowerCables(pa);
        pa.UsePort(PcPort.CaseVideo, Cable("Pickup_CaboVideo"), true, out _);
        pa.UsePort(PcPort.MonitorVideo, null, false, out _);
        Assert.IsTrue(pa.HasVideoSignal);
        Assert.IsFalse(pb.VideoConnected);
        Assert.IsFalse(pb.IsPlugged(PcPort.Outlet), "tomadas da outra estação continuam livres");

        pb.transform.SetParent(null, true);
        pb.transform.position += Vector3.forward * 5f;
        yield return null;
        Assert.IsFalse(pb.MonitorInRange);
        Assert.IsTrue(pa.MonitorInRange, "levar um gabinete não afeta o outro");

        pa.Case.TryRemoveScrew(3, ToolType.Screwdriver);
        Assert.IsTrue(pa.Case.Screws[3].removed);
        Assert.IsFalse(pb.Case.Screws[3].removed, "parafusos independentes");
    }
    // ---- 7. fechar recolocando os parafusos ------------------------------------------------------------------------

    [UnityTest]
    public IEnumerator RecolocarOsObrigatorios_FechaOGabinete_ETudoVoltaAoEstadoFechado()
    {
        GameObject g = SpawnLooseCase(new Vector3(5f, 0f, 0f));     // fora de estação
        var pcCase = g.GetComponent<ComputerCase>();
        Component pickup = g.GetComponent(T("PickupInteractable"));
        yield return null;
        GameObject screen = OpenScreen(g); yield return null;
        Component ctrl = Controller(screen);
        Button next = ButtonNamed(screen, "BotaoAvancar"), back = ButtonNamed(screen, "BotaoVoltar");
        var group = next.GetComponent<CanvasGroup>();
        Component[] views = screen.GetComponentsInChildren(T("ScrewView"), true);
        bool Removed(int i) => (bool)Get(views[i], "Removed");

        next.onClick.Invoke(); yield return null;                                   // Traseira
        var tool = Spawn("Assets/Prefabs/Gameplay/Pickup_Ferramenta.prefab", Vector3.down * 10f);
        Assert.IsTrue((bool)Call(holder, "Pick", tool.GetComponent(T("PickupInteractable"))));
        foreach (int i in new[] { 0, 3, 5, 7 }) { views[i].GetComponent<Button>().onClick.Invoke(); yield return null; }
        next.onClick.Invoke(); yield return null;
        Assert.AreEqual("Interior", Get(ctrl, "CurrentView").ToString());
        Assert.AreEqual(CaseState.Open, pcCase.State);
        Assert.IsTrue((bool)Get(pickup, "IsPickupBlocked"));

        back.onClick.Invoke(); yield return null;                                   // Traseira, tampa aberta
        Assert.IsFalse((bool)Get(ctrl, "NextLocked"), "aberto: o interior continua acessível");
        Assert.IsTrue(views[3].GetComponent<Button>().interactable, "o furo continua clicável");

        Call(holder, "Stow", Vector3.down * 10f); yield return null;                // mão livre (chave guardada)
        Assert.IsFalse((bool)Get(holder, "IsHolding"));
        views[3].GetComponent<Button>().onClick.Invoke(); yield return null;
        Assert.IsTrue(Removed(3), "sem a chave o parafuso não volta");
        Assert.IsTrue((bool)Call(holder, "Retrieve", tool));

        views[3].GetComponent<Button>().onClick.Invoke(); yield return null;
        views[5].GetComponent<Button>().onClick.Invoke(); yield return null;
        Assert.IsFalse(Removed(3)); Assert.IsFalse(Removed(5));
        Assert.AreEqual(CaseState.Open, pcCase.State, "falta um obrigatório");
        next.onClick.Invoke(); yield return null;
        Assert.AreEqual("Interior", Get(ctrl, "CurrentView").ToString(), "ainda aberto: dá para voltar ao interior");
        back.onClick.Invoke(); yield return null;

        views[7].GetComponent<Button>().onClick.Invoke(); yield return null;
        Assert.AreEqual(CaseState.Closed, pcCase.State, "último obrigatório recolocado: fechado");
        AssertNoFeedbackText(screen);
        Assert.IsTrue((bool)Get(ctrl, "NextLocked"), "fechado e parafusado: Avançar bloqueado de novo");
        Assert.Less(group.alpha, 0.6f);
        Assert.IsFalse((bool)Get(pickup, "IsPickupBlocked"), "fechado: pode ser carregado de novo");
        Assert.IsTrue(Removed(0), "o opcional retirado não precisa voltar para fechar");

        next.onClick.Invoke(); yield return null;
        Assert.AreEqual("Rear", Get(ctrl, "CurrentView").ToString(), "o interior não fica acessível como se estivesse aberto");
        AssertNoFeedbackText(screen);

        // fechar e reabrir a tela: abre na Traseira (não no Interior)
        Call(manager, "Close"); yield return null;
        screen = OpenScreen(g); yield return null;
        Assert.AreNotEqual("Interior", Get(Controller(screen), "CurrentView").ToString());
    }
}
