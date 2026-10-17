using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Interação com a estação de trabalho, com os prefabs reais (Estacao_Trabalho, UI_Screen_Gabinete): monitor, teclado
/// e mouse pegáveis; gabinete carregado fechado, que só abre sobre mesa/bancada (WorkSurface); painel de conexões e
/// monitor da tela 2D só com o jogador perto (inclusive validando o clique); painel horizontal na ordem do ponto
/// elétrico 3D; estações independentes; miniatura do item da mão; ganchos de som; e a cena configurada.
/// Componentes do Assembly-CSharp por reflexão (como nos outros testes de Play Mode).
/// </summary>
public class EstacaoInteracaoTests
{
    private const string StationPath = "Assets/Prefabs/Gameplay/Estacao_Trabalho.prefab";
    private const string ScreenPath = "Assets/Prefabs/UI/UI_Screen_Gabinete.prefab";
    private const string PontoPath = "Assets/Prefabs/Gameplay/PontoEletrico.prefab";

    private GameObject player, cam, floor, canvasGo;
    private Component holder, detector, manager;
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
    private static object Call(object target, string method, params object[] args) =>
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public).Invoke(target, args);
    private static object Get(object target, string property) =>
        target.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.Public).GetValue(target);

    [SetUp]
    public void SetUp()
    {
        floor = GameObject.CreatePrimitive(PrimitiveType.Cube);          // chão comum (NÃO é superfície de trabalho)
        floor.name = "Chao";
        floor.transform.localScale = new Vector3(30f, 0.2f, 30f);
        floor.transform.position = new Vector3(0f, -0.1f, 0f);
        Physics.SyncTransforms();
        canvasGo = new GameObject("TestCanvas", typeof(Canvas));
        player = new GameObject("TestPlayer");
        cam = new GameObject("Camera");
        cam.transform.SetParent(player.transform, false);
        cam.transform.localPosition = new Vector3(0f, 1.5f, 0f);
        var hold = new GameObject("HoldPoint").transform;
        hold.SetParent(cam.transform, false);
        hold.localPosition = new Vector3(0.24f, -0.21f, 0.58f);
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
        if (manager != null) Call(manager, "Close");
        foreach (GameObject g in created) if (g != null) Object.Destroy(g);
        created.Clear();
        Object.Destroy(player); Object.Destroy(canvasGo); Object.Destroy(floor);
        foreach (var c in Object.FindObjectsByType<ComputerCase>(FindObjectsSortMode.None)) Object.Destroy(c.gameObject);
    }

    private GameObject Spawn(string path, Vector3 pos)
    {
        GameObject g = Object.Instantiate(Prefab(path), pos, Quaternion.identity);
        created.Add(g);
        return g;
    }

    private static Component Pickup(Transform t) => t.GetComponent(T("PickupInteractable"));
    private static Transform Child(GameObject station, string name) =>
        station.GetComponentsInChildren<Transform>(true).First(t => t.name == name && t.GetComponent(T("PickupInteractable")) != null);

    /// <summary>Jogador em <paramref name="feet"/>, olhando para <paramref name="target"/>.</summary>
    private void Stand(Vector3 feet, Vector3 target)
    {
        player.transform.position = feet;
        player.transform.rotation = Quaternion.identity;
        cam.transform.rotation = Quaternion.LookRotation(target - cam.transform.position);
    }

    private bool Holding => (bool)Get(holder, "IsHolding");

    private GameObject OpenScreen(GameObject source)
    {
        Assert.IsTrue((bool)Call(manager, "Open", Prefab(ScreenPath).GetComponent(T("UIScreen")), source, detector));
        return ((Component)Get(manager, "Current")).gameObject;
    }

    private static Button ButtonNamed(GameObject screen, string name) => screen.GetComponentsInChildren<Button>(true).First(b => b.name == name);

    // ---- 1. monitor, teclado e mouse ---------------------------------------------------------------------------------

    [UnityTest]
    public IEnumerator MonitorTecladoEMouse_PegaETransportaEColocaNaMesa_SemQuebrarOMonitor()
    {
        GameObject st = Spawn(StationPath, Vector3.zero);
        yield return null;
        var power = st.GetComponentInChildren<ComputerPower>();
        Transform tela = st.GetComponentInChildren<MonitorDisplay>().transform;
        var spots = new Dictionary<string, Vector3>                       // pontos livres do tampo (um para cada)
        {
            { "Monitor", new Vector3(-0.4f, 0.75f, -0.2f) }, { "Teclado", new Vector3(-0.05f, 0.75f, 0.25f) }, { "Mouse", new Vector3(-0.62f, 0.75f, 0.3f) },
        };
        foreach (string n in new[] { "Monitor", "Teclado", "Mouse" })
        {
            Vector3 spot = spots[n];
            Transform t = Child(st, n);
            Component p = Pickup(t);
            Stand(new Vector3(0.3f, 0f, 1.1f), t.position);
            Assert.IsTrue((bool)Call(p, "CanInteract", detector), n + ": 'Pegar' disponível");
            Call(p, "Interact", detector);
            Assert.IsTrue(Holding, n + " na mão");
            Assert.AreSame(p, Get(holder, "Current"));
            Assert.IsTrue(t.IsChildOf(((Transform)Get(holder, "HoldPoint"))), n + " preso ao ponto de mão");

            Stand(new Vector3(spot.x, 0f, 1.0f), spot);                   // leva até a mesa e mira o tampo
            yield return null;
            Call(holder, "Drop");
            Assert.IsFalse(Holding);
            Assert.AreEqual("PlacedOnSurface", Get(p, "Placement").ToString(), n + " colocado na mesa");
            yield return new WaitForSeconds(0.4f);
            Assert.Less(Mathf.Abs(t.GetComponentInChildren<Renderer>().bounds.min.y - 0.75f), 0.05f, n + " apoiado no tampo");
            Vector3 toPlayer = Vector3.ProjectOnPlane(player.transform.position - t.position, Vector3.up).normalized;
            Assert.Greater(Vector3.Dot(Vector3.ProjectOnPlane(t.forward, Vector3.up).normalized, toPlayer), 0.9f,
                n + ": frente virada para o jogador (placedYawOffset 180), como na estação");
            Physics.SyncTransforms();
        }
        Assert.AreSame(tela, power.Monitor, "o monitor continua sendo o deste PC");
        Assert.IsTrue(power.MonitorInRange, "colocado na mesma mesa: continua perto do gabinete");
    }

    // ---- 2/3/4/5/6/7/8. gabinete: transporte, abertura só na mesa, manutenção, fechar --------------------------------

    [UnityTest]
    public IEnumerator Gabinete_FechadoCarrega_NoChaoNaoAbre_NaMesaAbre_ManutencaoEFechamento()
    {
        GameObject st = Spawn(StationPath, Vector3.zero);
        yield return null;
        var power = st.GetComponentInChildren<ComputerPower>();
        var pcCase = power.Case;
        Component pick = Pickup(power.transform);
        Assert.IsTrue(pcCase.IsOnWorkSurface, "começa sobre a mesa da estação");
        Assert.IsInstanceOf<WorkSurface>(pcCase.SupportingSurface());

        // carregar fechado e colocar no chão
        Stand(new Vector3(0.75f, 0f, 1.1f), power.transform.position);
        Assert.IsTrue((bool)Call(pick, "CanInteract", detector), "fechado: pode ser carregado");
        Call(pick, "Interact", detector);
        Assert.IsTrue(Holding);
        Assert.IsFalse(pcCase.IsOnWorkSurface, "na mão não está apoiado");
        Stand(new Vector3(-1.5f, 0f, 2.2f), new Vector3(-1.5f, 0f, 1.2f));
        Call(holder, "Drop");
        yield return new WaitForSeconds(0.6f);
        Assert.IsFalse(Holding);
        Assert.Less(pcCase.GetComponent<Collider>().bounds.min.y, 0.05f, "no chão");
        Assert.IsFalse(pcCase.IsOnWorkSurface, "chão não é superfície de trabalho");

        // no chão: nenhuma manutenção (tela não abre; parafusos e tampa recusam)
        GameObject tool = Spawn("Assets/Prefabs/Gameplay/Pickup_Ferramenta.prefab", Vector3.down * 10f);
        Component opener = power.GetComponent(T("UIScreenInteractable"));
        Assert.IsFalse((bool)Call(opener, "CanInteractSecondary", detector), "no chão: sem 'Manutenção'");
        Call(opener, "InteractSecondary", detector);
        Assert.IsFalse((bool)Get(manager, "IsOpen"), "nem forçando o clique direito");
        Assert.AreEqual(ScrewRemovalResult.NotOnWorkSurface, pcCase.TryRemoveScrew(3, ToolType.Screwdriver));
        Assert.IsFalse(pcCase.Screws[3].removed);
        Assert.AreEqual(CaseOpenResult.ScrewsRemaining, pcCase.TryOpen());

        // fechado pode ser carregado de volta para a mesa
        Stand(new Vector3(-1.5f, 0f, 2.0f), pcCase.transform.position);
        Assert.IsTrue((bool)Call(pick, "CanInteract", detector), "fechado: pode ser carregado de novo");
        Call(pick, "Interact", detector);
        Stand(new Vector3(0.75f, 0f, 1.1f), new Vector3(0.75f, 0.75f, 0.15f));
        Call(holder, "Drop");
        yield return new WaitForSeconds(0.6f);
        Assert.IsTrue(pcCase.IsOnWorkSurface, "de volta à mesa");

        // na mesa: a tela abre, os parafusos saem, abre e a manutenção interna funciona
        Assert.IsTrue((bool)Call(opener, "CanInteractSecondary", detector), "na mesa: 'Manutenção'");
        Assert.IsTrue((bool)Call(holder, "Pick", Pickup(tool.transform)));
        GameObject screen = OpenScreen(power.gameObject); yield return null;
        Component ctrl = screen.GetComponent(T("ComputerScreenController"));
        Button next = ButtonNamed(screen, "BotaoAvancar");
        next.onClick.Invoke(); yield return null;                                         // Traseira
        Component[] screws = screen.GetComponentsInChildren(T("ScrewView"), true);
        foreach (int i in new[] { 3, 5, 7 }) { screws[i].GetComponent<Button>().onClick.Invoke(); yield return null; }
        Assert.IsTrue(pcCase.RequiredScrewsRemoved);
        Assert.IsFalse((bool)Get(ctrl, "NextLocked"), "na mesa e sem os obrigatórios: liberado");
        Call(holder, "Stow", Vector3.down * 10f);
        next.onClick.Invoke(); yield return null;
        Assert.AreEqual("Interior", Get(ctrl, "CurrentView").ToString());
        Assert.AreEqual(CaseState.Open, pcCase.State);
        Assert.IsTrue((bool)Call(pick, "CanInteract", detector), "aberto: pode ser carregado (2026-10-15; fica incompleto)");

        Button hdSlot = screen.GetComponentsInChildren<Button>(true).First(b => b.name == "Encaixe_HD");
        hdSlot.onClick.Invoke(); yield return null;
        Assert.IsTrue(Holding, "HD retirado para a mão");
        Assert.IsFalse(pcCase.HasComponent(PcComponent.Hdd));
        hdSlot.onClick.Invoke(); yield return null;
        Assert.IsFalse(Holding, "HD instalado de volta");
        Assert.IsTrue(pcCase.HasComponent(PcComponent.Hdd));

        // recolocar os obrigatórios fecha; fechado volta a poder ser carregado
        Assert.IsTrue((bool)Call(holder, "Retrieve", tool));
        ButtonNamed(screen, "BotaoVoltar").onClick.Invoke(); yield return null;
        foreach (int i in new[] { 3, 5, 7 }) { screws[i].GetComponent<Button>().onClick.Invoke(); yield return null; }
        Assert.AreEqual(CaseState.Closed, pcCase.State);
        Call(manager, "Close"); yield return null; yield return null;
        Call(holder, "Stow", Vector3.down * 10f);
        Assert.IsTrue((bool)Call(pick, "CanInteract", detector), "fechado de novo: pode ser carregado");
    }

    // ---- cabos no canvas: soltar a ponta sem Esc, vários pendurados, nada de arraste ao reabrir -----------------------

    [UnityTest]
    public IEnumerator Cabos_SoltarPonta_VariosPendurados_ReabrirSemArraste()
    {
        GameObject st = Spawn(StationPath, Vector3.zero);
        yield return null;
        var power = st.GetComponentInChildren<ComputerPower>();
        power.GetComponent<Rigidbody>().isKinematic = true;
        Assert.AreEqual(PortResult.PluggedFromHand, power.UsePort(PcPort.CasePower, Spawn("Assets/Prefabs/Gameplay/Pickup_CaboEnergia.prefab", Vector3.down * 10f).GetComponent<CableItem>(), true, out _));
        Assert.AreEqual(PortResult.PluggedOtherEnd, power.UsePort(PcPort.Outlet, null, false, out _));
        Assert.AreEqual(PortResult.PluggedFromHand, power.UsePort(PcPort.CaseNetwork, Spawn("Assets/Prefabs/Gameplay/Pickup_CaboRede.prefab", Vector3.down * 10f).GetComponent<CableItem>(), true, out _));
        Assert.AreEqual(PortResult.PluggedOtherEnd, power.UsePort(PcPort.NetworkJack, null, false, out _));

        Stand(power.transform.position + new Vector3(0f, -1f, 1.2f), power.transform.position);
        GameObject screen = OpenScreen(power.gameObject); yield return null;
        Component ctrl = screen.GetComponent(T("ComputerScreenController"));
        ButtonNamed(screen, "BotaoAvancar").onClick.Invoke(); yield return null;     // Traseira

        ButtonNamed(screen, "Porta_Tomada").onClick.Invoke(); yield return null;
        Assert.AreEqual(PcLink.Power, Get(ctrl, "CarriedLink"), "a ponta da tomada segue o mouse");
        Call(ctrl, "DropCarriedCable"); yield return null;                           // clique no vazio
        Assert.IsNull(Get(ctrl, "CarriedLink"), "soltou sem Esc");
        Assert.IsTrue(power.IsEndPlugged(PcLink.Power, false) && !power.IsEndPlugged(PcLink.Power, true), "pendurado no gabinete");

        ButtonNamed(screen, "Porta_Rede").onClick.Invoke(); yield return null;
        Assert.AreEqual(PcLink.Network, Get(ctrl, "CarriedLink"));
        Call(ctrl, "DropCarriedCable"); yield return null;
        Assert.IsTrue(power.IsEndPlugged(PcLink.Network, false) && !power.IsEndPlugged(PcLink.Network, true));
        Assert.IsTrue(power.IsEndPlugged(PcLink.Power, false), "os dois ficam pendurados ao mesmo tempo");
        Assert.IsFalse(power.BlocksPickup, "nada preso à parede");

        // solta uma ponta e fecha (Esc/Fechar) no meio do arraste: ao reabrir, nada segue o mouse
        ButtonNamed(screen, "Porta_EnergiaGabinete").onClick.Invoke(); yield return null;
        Assert.IsNull(Get(ctrl, "CarriedLink"), "soltou as duas pontas: o cabo volta para a mão, sem arraste");
        Assert.IsTrue(Holding);
        ButtonNamed(screen, "Porta_Tomada").onClick.Invoke(); yield return null;     // liga o cabo da mão na tomada
        Assert.AreEqual(PcLink.Power, Get(ctrl, "CarriedLink"));
        Call(manager, "Close"); yield return null; yield return null;
        Assert.IsTrue(power.IsEndPlugged(PcLink.Power, true), "fechar não desfaz a conexão");
        screen = OpenScreen(power.gameObject); yield return null;
        ctrl = screen.GetComponent(T("ComputerScreenController"));
        Assert.IsNull(Get(ctrl, "CarriedLink"), "reabrir não retoma o arraste");
        Call(manager, "Close"); yield return null;
    }

    // ---- 9/10/12. painel de conexões e monitor por alcance -----------------------------------------------------------

    [UnityTest]
    public IEnumerator PainelDeConexoesEMonitor_PelaDistanciaEntreEquipamentos_NaoDoJogador()
    {
        GameObject st = Spawn(StationPath, Vector3.zero);
        yield return null;
        var power = st.GetComponentInChildren<ComputerPower>();
        power.GetComponent<Rigidbody>().isKinematic = true;
        var ponto = st.GetComponentInChildren<WallConnectionPoint>();
        Assert.AreSame(power, ponto.Source, "o ponto elétrico aponta para o gabinete da própria estação");
        Assert.AreSame(ponto.transform, power.WallPoint);

        // jogador longe do ponto e do monitor, mas mirando o gabinete (até 3 m): tudo continua visível
        Stand(new Vector3(0.75f, 0f, 2.9f), power.transform.position);
        GameObject screen = OpenScreen(power.gameObject); yield return null;
        ButtonNamed(screen, "BotaoAvancar").onClick.Invoke(); yield return null;     // Traseira
        GameObject painel = screen.transform.Find("Traseira/PainelConexoes").gameObject;
        GameObject monitor = screen.transform.Find("Traseira/Monitor").gameObject;
        Assert.IsTrue(painel.activeSelf, "a distância do jogador não esconde o ponto elétrico");
        Assert.IsTrue(monitor.activeSelf, "nem o monitor");
        GameObject cable = Spawn("Assets/Prefabs/Gameplay/Pickup_CaboEnergia.prefab", Vector3.down * 10f);
        Assert.IsTrue((bool)Call(holder, "Pick", Pickup(cable.transform)));
        ButtonNamed(screen, "Porta_Tomada").onClick.Invoke(); yield return null;
        Assert.IsTrue(power.IsPlugged(PcPort.Outlet), "conecta na tomada");
        Call(manager, "Close"); yield return null; yield return null;

        // gabinete levado para longe do ponto e do monitor (outra mesa): ponto e monitor somem
        power.transform.SetParent(null, true);
        power.transform.position += new Vector3(3.5f, 0f, 0f);
        Stand(power.transform.position + new Vector3(0f, -1f, 1.2f), power.transform.position);
        yield return null; yield return null;
        screen = OpenScreen(power.gameObject); yield return null;
        Assert.AreEqual("Rear", Get(screen.GetComponent(T("ComputerScreenController")), "CurrentView").ToString());
        Assert.IsFalse(painel.activeSelf, "ponto elétrico longe do gabinete: some");
        Assert.IsFalse(monitor.activeSelf, "monitor longe do gabinete: some");
        Assert.IsFalse(screen.transform.Find("Traseira/Cabos/CaboEnergia").gameObject.activeSelf, "o cabo da tomada some junto");

        // clique "antigo" num botão escondido: não conecta, o cabo continua na mão
        GameObject rede = Spawn("Assets/Prefabs/Gameplay/Pickup_CaboRede.prefab", Vector3.down * 10f);
        Assert.IsTrue((bool)Call(holder, "Pick", Pickup(rede.transform)));
        ButtonNamed(screen, "Porta_Rede").onClick.Invoke(); yield return null;
        Assert.IsFalse(power.IsPlugged(PcPort.NetworkJack), "fora do alcance não conecta");
        Assert.IsTrue(Holding, "o cabo continua na mão");
        Assert.IsTrue(power.IsEndPlugged(PcLink.Power, true), "o cabo ligado antes continua ligado (na tomada da estação)");
        Assert.IsNotNull(power.WallOf(PcLink.Power), "ponta presa ao ponto elétrico de origem");
        Assert.IsFalse(power.IsPlugged(PcPort.Outlet), "longe: a tomada mostrada é a do ponto atual (nenhum)");
    }

    // ---- 11. painel horizontal na ordem do ponto elétrico 3D --------------------------------------------------------

    [Test]
    public void PainelDeConexoes_ADireita_UmaAbaixoDaOutra_ECabosNosPlugues()
    {
        Transform painel = Prefab(ScreenPath).transform.Find("Traseira/PainelConexoes");
        Assert.IsNotNull(painel);
        var pr = (RectTransform)painel;
        Assert.Greater(pr.anchorMin.x, 0.65f, "à direita do gabinete (a miniatura do item da mão fica na borda direita)");
        Assert.Greater(pr.anchorMax.y - pr.anchorMin.y, pr.anchorMax.x - pr.anchorMin.x, "coluna (mais alta que larga)");
        string[] top2bottom = new[] { "Tomada", "Tomada_Monitor", "PontoRede" };
        var rects = top2bottom.Select(n => (RectTransform)painel.Find(n)).ToList();
        for (int i = 1; i < rects.Count; i++)
        {
            Assert.Less(rects[i].anchorMax.y, rects[i - 1].anchorMin.y + 1e-4f, top2bottom[i] + " abaixo de " + top2bottom[i - 1]);
            Assert.AreEqual(rects[0].anchorMin.x, rects[i].anchorMin.x, 1e-4f, "mesma coluna");
        }
        // o cabo encosta no plugue desenhado (não no centro da área clicável)
        foreach (Component port in painel.GetComponentsInChildren(T("ComputerPortView"), true))
            Assert.AreEqual(port.transform.Find("Plugue"), Get(port, "Anchor"), port.name);
    }

    // ---- 12/18. duas estações ----------------------------------------------------------------------------------------

    [UnityTest]
    public IEnumerator DuasEstacoes_CadaPainelEMonitorDaPropriaEstacao()
    {
        GameObject a = Spawn(StationPath, new Vector3(-3f, 0f, 0f));
        GameObject b = Spawn(StationPath, new Vector3(3f, 0f, 0f));
        yield return null;
        var pa = a.GetComponentInChildren<ComputerPower>();
        var pb = b.GetComponentInChildren<ComputerPower>();
        Assert.IsTrue(pa.WallPoint.IsChildOf(a.transform) && pb.WallPoint.IsChildOf(b.transform), "cada PC com o seu ponto elétrico");

        Stand(new Vector3(-2.4f, 0f, 1.0f), pa.transform.position);
        GameObject screen = OpenScreen(pa.gameObject); yield return null;
        GameObject painel = screen.transform.Find("Traseira/PainelConexoes").gameObject;
        Assert.IsTrue(painel.activeSelf, "A: painel visível");
        GameObject cable = Spawn("Assets/Prefabs/Gameplay/Pickup_CaboEnergia.prefab", Vector3.down * 10f);
        Assert.IsTrue((bool)Call(holder, "Pick", Pickup(cable.transform)));
        ButtonNamed(screen, "BotaoAvancar").onClick.Invoke(); yield return null;
        ButtonNamed(screen, "Porta_Tomada").onClick.Invoke(); yield return null;
        Assert.IsTrue(pa.IsPlugged(PcPort.Outlet));
        Assert.IsFalse(pb.IsPlugged(PcPort.Outlet), "a tomada da outra estação continua livre");
        Call(manager, "Close"); yield return null; yield return null;

        // a mesma tela aberta pelo gabinete B: mostra o ponto e o monitor de B (perto do gabinete B), sem dados de A
        screen = OpenScreen(pb.gameObject); yield return null;
        Assert.IsTrue(painel.activeSelf, "B: ponto elétrico de B perto do gabinete B");
        Assert.IsFalse(screen.transform.Find("Traseira/PainelConexoes/Tomada/Area/Porta_Tomada/Plugue").gameObject.activeSelf,
            "nenhum dado da estação A na tela de B");
    }

    // ---- lados na tela 2D e sinal de vídeo ao mover --------------------------------------------------------------------

    [UnityTest]
    public IEnumerator Tela_MonitorEGabineteNoLadoFisico_ESinalSoDependeDoCabo()
    {
        GameObject st = Spawn(StationPath, Vector3.zero);
        yield return null;
        var power = st.GetComponentInChildren<ComputerPower>();
        power.GetComponent<Rigidbody>().isKinematic = true;
        Stand(new Vector3(0.4f, 0f, 1.1f), power.transform.position);
        GameObject screen = OpenScreen(power.gameObject); yield return null;
        Component ctrl = screen.GetComponent(T("ComputerScreenController"));
        var frenteMon = (RectTransform)screen.transform.Find("Frente/Monitor");
        var frenteCase = (RectTransform)screen.transform.Find("Frente/Gabinete");
        var trasMon = (RectTransform)screen.transform.Find("Traseira/Monitor");
        var trasCase = (RectTransform)screen.transform.Find("Traseira/ContainerTraseira_Area");
        Assert.IsFalse((bool)Get(ctrl, "Mirrored"), "estação padrão: vista de frente, gabinete à esquerda do monitor");
        Assert.Less(frenteCase.anchorMin.x, frenteMon.anchorMin.x);
        Assert.Less(trasMon.anchorMin.x, trasCase.anchorMin.x, "por trás, o contrário");
        Vector2 frenteMonPos = frenteMon.anchorMin, frenteCasePos = frenteCase.anchorMin;

        // cabos de energia e vídeo, PC ligado no sistema
        CableItem C(string p) => Spawn("Assets/Prefabs/Gameplay/" + p + ".prefab", Vector3.down * 10f).GetComponent<CableItem>();
        power.UsePort(PcPort.CasePower, C("Pickup_CaboEnergia"), true, out _); power.UsePort(PcPort.Outlet, null, false, out _);
        power.UsePort(PcPort.MonitorPower, C("Pickup_CaboEnergia"), true, out _); power.UsePort(PcPort.MonitorOutlet, null, false, out _);
        power.UsePort(PcPort.CaseVideo, C("Pickup_CaboVideo"), true, out _); power.UsePort(PcPort.MonitorVideo, null, false, out _);
        power.PressPower();
        Assert.AreEqual(PcScreen.OperatingSystem, power.Screen);
        Call(manager, "Close"); yield return null; yield return null;

        // gabinete levado para o outro lado do monitor: a tela troca os lados; o sinal continua
        power.transform.SetParent(null, true);
        power.transform.position = st.transform.TransformPoint(new Vector3(-0.4f, 1.0f, 0.15f));
        yield return null;
        screen = OpenScreen(power.gameObject); yield return null;
        Assert.IsTrue((bool)Get(ctrl, "Mirrored"), "gabinete à direita do monitor (vista de frente)");
        Assert.Greater(frenteCase.anchorMin.x, frenteMon.anchorMin.x, "Frente: gabinete à direita, como no mundo");
        Assert.AreEqual(frenteMonPos.y, frenteMon.anchorMin.y, 1e-4f, "mesma altura (só troca de lado)");
        Assert.Greater(trasMon.anchorMin.x, trasCase.anchorMin.x, "a Traseira troca");
        var painelRt = screen.transform.Find("Traseira/PainelConexoes").GetComponent<RectTransform>();
        Assert.Less(painelRt.anchorMax.x, trasCase.anchorMin.x, "o ponto elétrico acompanha o lado do gabinete (cabos curtos)");
        Assert.IsTrue(power.Case.IsOnWorkSurface, "monitor do outro lado: o gabinete continua na mesa");
        Assert.IsFalse((bool)Get(power.GetComponent(T("UIScreenInteractable")), "IsScreenBlocked"), "manutenção disponível");
        Assert.IsTrue((bool)Call(Pickup(power.transform), "CanInteract", detector), "e pegar também");
        Call(manager, "Close"); yield return null; yield return null;

        // longe do monitor: o cabo continua ligado e a imagem também (a distância só mexe na interface)
        power.transform.position += Vector3.right * 4f;
        yield return null; yield return null;
        Assert.IsFalse(power.MonitorInRange);
        Assert.IsTrue(power.VideoConnected, "estado físico do cabo preservado");
        Assert.IsTrue(power.HasVideoSignal);
        Assert.AreEqual(PcScreen.OperatingSystem, power.Screen, "não perde o sinal por distância");
        power.transform.position -= Vector3.right * 4f;
        yield return null;
        Assert.AreEqual(PcScreen.OperatingSystem, power.Screen);
    }

    // ---- 13/14. miniatura do item da mão -----------------------------------------------------------------------------

    [UnityTest]
    public IEnumerator MaoAtual_MostraOItemRealmenteSegurado_TrocaNaHora_ESomeAoSoltar()
    {
        var mao = new GameObject("MaoAtual", typeof(RectTransform));
        mao.transform.SetParent(canvasGo.transform, false);
        var moldura = new GameObject("Moldura", typeof(RectTransform), typeof(Image));
        moldura.transform.SetParent(mao.transform, false);
        var raw = new GameObject("Item", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        raw.transform.SetParent(moldura.transform, false);
        moldura.SetActive(false);
        Component preview = mao.AddComponent(T("HeldItemPreview"));
        Set(preview, "holder", holder);
        Set(preview, "screens", manager);
        Set(preview, "panel", moldura);
        Set(preview, "image", raw);

        GameObject case_ = Spawn("Assets/Prefabs/Gameplay/Gabinete.prefab", new Vector3(5f, 0.5f, 0f));
        case_.GetComponent<Rigidbody>().isKinematic = true;
        GameObject cable = Spawn("Assets/Prefabs/Gameplay/Pickup_CaboEnergia.prefab", Vector3.down * 10f);
        GameObject hd = Spawn("Assets/Prefabs/Gameplay/Pickup_HD.prefab", Vector3.down * 12f);
        int cables = Object.FindObjectsByType<CableItem>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        int renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        yield return null;

        Assert.IsTrue((bool)Call(holder, "Pick", Pickup(cable.transform)));
        yield return null;
        Assert.IsNull(Get(preview, "Shown"), "sem tela aberta: nada (o item já aparece na mão em 1ª pessoa)");

        OpenScreen(case_); yield return null; yield return null;
        Assert.AreSame(Pickup(cable.transform), Get(preview, "Shown"), "mostra o cabo que está na mão");
        Assert.IsTrue(moldura.activeSelf);
        Assert.IsNotNull(raw.texture);
        Assert.AreEqual(mao.transform.parent.childCount - 1, mao.transform.GetSiblingIndex(), "por cima da tela aberta");

        Call(holder, "Stow", Vector3.down * 10f);
        Assert.IsTrue((bool)Call(holder, "Pick", Pickup(hd.transform)));
        yield return null;
        Assert.AreSame(Pickup(hd.transform), Get(preview, "Shown"), "trocou o item: troca na hora");

        Call(holder, "Stow", Vector3.down * 10f);
        yield return null;
        Assert.IsNull(Get(preview, "Shown"), "mão vazia: some");
        Assert.IsFalse(moldura.activeSelf);

        Assert.AreEqual(cables, Object.FindObjectsByType<CableItem>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length, "nenhuma cópia do item no mundo");
        Assert.AreEqual(renderers, Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length, "nenhum renderer extra");
        Assert.IsNull(raw.GetComponent<Collider>());
        Assert.IsFalse(raw.raycastTarget, "a miniatura não recebe cliques");
    }

    // ---- 16/sons. eventos de som e ausência de texto ----------------------------------------------------------------

    private readonly List<string> sounds = new List<string>();
    private void OnSound<TSound>(TSound sound, Vector3? at) => sounds.Add(sound.ToString());

    [UnityTest]
    public IEnumerator Sons_AcoesComAnimacaoDisparamEventos_SemAudioNaoDaErro()
    {
        Type hub = T("InteractionSounds");
        Type soundType = T("InteractionSound");
        EventInfo ev = hub.GetEvent("Raised");
        MethodInfo handler = GetType().GetMethod(nameof(OnSound), BindingFlags.Instance | BindingFlags.NonPublic).MakeGenericMethod(soundType);
        Delegate d = Delegate.CreateDelegate(ev.EventHandlerType, this, handler);
        ev.AddEventHandler(null, d);
        var sons = new GameObject("Sons_Teste");
        created.Add(sons);
        sons.AddComponent(T("InteractionSoundPlayer"));                    // sem nenhum áudio configurado
        try
        {
            GameObject g = Spawn("Assets/Prefabs/Gameplay/Gabinete.prefab", new Vector3(5f, 0.5f, 0f));
            g.GetComponent<Rigidbody>().isKinematic = true;
            g.GetComponent<ComputerCase>().RequiresWorkSurface = false;
            GameObject tool = Spawn("Assets/Prefabs/Gameplay/Pickup_Ferramenta.prefab", Vector3.down * 10f);
            Assert.IsTrue((bool)Call(holder, "Pick", Pickup(tool.transform)));
            GameObject screen = OpenScreen(g); yield return null;
            ButtonNamed(screen, "BotaoAvancar").onClick.Invoke(); yield return null;
            Component[] screws = screen.GetComponentsInChildren(T("ScrewView"), true);
            foreach (int i in new[] { 3, 5, 7 }) { screws[i].GetComponent<Button>().onClick.Invoke(); yield return null; }
            ButtonNamed(screen, "BotaoAvancar").onClick.Invoke(); yield return null;
            ButtonNamed(screen, "BotaoVoltar").onClick.Invoke(); yield return null;
            screws[3].GetComponent<Button>().onClick.Invoke(); yield return null;

            CollectionAssert.IsSubsetOf(new[] { "ScrewRemoved", "CaseOpened", "ScrewInstalled" }, sounds);
            Assert.AreEqual(3, sounds.Count(s => s == "ScrewRemoved"), "um por parafuso de verdade");
            foreach (Text t in screen.GetComponentsInChildren<Text>())
                StringAssert.DoesNotContain("Gabinete", t.text, "nenhuma mensagem de texto nas ações");

            var openable = Spawn("Assets/Prefabs/Gameplay/Openable_Gaveta.prefab", new Vector3(-5f, 0f, 0f)).GetComponentInChildren(T("OpenableInteractable"));
            Call(openable, "SetOpen", true);
            Call(openable, "SetOpen", false);
            CollectionAssert.IsSubsetOf(new[] { "DrawerOpened", "DrawerClosed" }, sounds);
            LogAssert.NoUnexpectedReceived();
        }
        finally
        {
            ev.RemoveEventHandler(null, d);
        }
    }

    // ---- cena ----------------------------------------------------------------------------------------------------------

    [UnityTest]
    public IEnumerator Cena_SuperficiesPontosEletricosMaoESonsConfigurados()
    {
        Type esm = Type.GetType("UnityEditor.SceneManagement.EditorSceneManager, UnityEditor", true);
        esm.GetMethod("LoadSceneInPlayMode").Invoke(null, new object[] { "Assets/Scenes/Office_Placeholder.unity", new LoadSceneParameters(LoadSceneMode.Additive) });
        yield return null; yield return null;
        Scene scene = SceneManager.GetSceneByPath("Assets/Scenes/Office_Placeholder.unity");
        try
        {
            GameObject[] roots = scene.GetRootGameObjects();
            IEnumerable<T2> All<T2>() where T2 : Component => roots.SelectMany(r => r.GetComponentsInChildren<T2>(true));
            IEnumerable<Component> AllOf(string type) => roots.SelectMany(r => r.GetComponentsInChildren(T(type), true));

            var surfaces = All<WorkSurface>().Select(s => s.name).ToList();
            CollectionAssert.AreEquivalent(new[] { "Mesa", "Mesa", "Bancada" }, surfaces, "só as mesas das estações e a bancada");

            var points = All<WallConnectionPoint>().ToList();
            Assert.AreEqual(2, points.Count);
            foreach (WallConnectionPoint p in points)
                Assert.AreSame(p.transform.parent, p.Source.transform.parent, "cada ponto elétrico ligado ao gabinete do próprio PC");
            Assert.AreNotSame(points[0].Source, points[1].Source);

            foreach (string n in new[] { "Monitor", "Teclado", "Mouse" })
                Assert.AreEqual(2, AllOf("PickupInteractable").Count(c => c.name == n), n + " pegável nas duas estações");

            Component preview = AllOf("HeldItemPreview").Single();
            Assert.AreEqual("ScreenCanvas", preview.transform.parent.name);
            Component soundPlayer = AllOf("InteractionSoundPlayer").Single();
            var entries = (IEnumerable)Get(soundPlayer, "Entries");
            int count = 0;
            foreach (object e in entries)
            {
                count++;
                Assert.Greater(((AudioClip[])e.GetType().GetField("clips").GetValue(e)).Length, 0, "toda ação tem som");
            }
            Assert.AreEqual(Enum.GetValues(T("InteractionSound")).Length, count, "uma linha por ação");

            Transform camera = roots.First(r => r.name == "Player").GetComponentInChildren<Camera>().transform;
            Assert.AreEqual(0.55f, camera.localPosition.y, 0.001f, "câmera mais perto do centro da cabeça");
            Assert.IsNull(roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == "Aviso"));
        }
        finally
        {
            SceneManager.UnloadSceneAsync(scene);
        }
        yield return null;
    }
}
