using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Integração em Play Mode (batchmode): prefabs reais do gabinete, da tela e da chave, com um "Player" mínimo.
/// Os componentes do projeto vivem no Assembly-CSharp (que um asmdef de teste não pode referenciar), então são
/// usados por reflexão. Cobre a fiação dos prefabs e a navegação; a aparência, o cursor e o clique real do mouse
/// continuam sendo teste manual.
/// </summary>
public class ComputerScreenFlowTests
{
    private const string CasePrefab = "Assets/Prefabs/Gameplay/Gabinete.prefab";
    private const string ScreenPrefab = "Assets/Prefabs/UI/UI_Screen_Gabinete.prefab";
    private const string ToolPrefab = "Assets/Prefabs/Gameplay/Pickup_Ferramenta.prefab";

    private GameObject player, canvasGo;
    private Component manager, holder, detector;
    private Transform holdPoint;

    // UnityEditor não é referenciável num assembly de Play Mode multi-plataforma: acesso por reflexão (só roda no Editor).
    private static GameObject LoadPrefab(string path)
    {
        Type db = Type.GetType("UnityEditor.AssetDatabase, UnityEditor", true);
        MethodInfo m = db.GetMethod("LoadAssetAtPath", new[] { typeof(string), typeof(Type) });
        var go = (GameObject)m.Invoke(null, new object[] { path, typeof(GameObject) });
        Assert.IsNotNull(go, "prefab não encontrado: " + path);
        return go;
    }

    private static Type T(string name) => Type.GetType(name + ", Assembly-CSharp", true);

    private static void Set(object target, string field, object value)
    {
        FieldInfo f = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.IsNotNull(f, target.GetType().Name + "." + field + " não existe");
        f.SetValue(target, value);
    }

    private static object Call(object target, string method, params object[] args)
    {
        MethodInfo m = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public);
        Assert.IsNotNull(m, target.GetType().Name + "." + method + " não existe");
        return m.Invoke(target, args);
    }

    private static object Get(object target, string property) =>
        target.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.Public).GetValue(target);

    [SetUp]
    public void SetUp()
    {
        canvasGo = new GameObject("TestCanvas", typeof(Canvas));
        player = new GameObject("TestPlayer");
        holdPoint = new GameObject("HoldPoint").transform;
        holdPoint.SetParent(player.transform, false);

        holder = player.AddComponent(T("ItemHolder"));
        Set(holder, "holdPoint", holdPoint);

        detector = player.AddComponent(T("InteractionDetector"));
        ((Behaviour)detector).enabled = false; // sem raycast/Update: o teste só precisa de Holder e Screens
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
        foreach (var c in Object.FindObjectsByType<ComputerCase>(FindObjectsSortMode.None)) Object.Destroy(c.gameObject);
    }

    private GameObject SpawnCase(Vector3 pos) => Object.Instantiate(LoadPrefab(CasePrefab), pos, Quaternion.identity);

    private GameObject OpenScreen(GameObject source)
    {
        Component screenComp = LoadPrefab(ScreenPrefab).GetComponent(T("UIScreen"));
        bool opened = (bool)Call(manager, "Open", screenComp, source, detector);
        Assert.IsTrue(opened, "UIScreenManager.Open recusou");
        return canvasGo.transform.GetChild(0).gameObject;
    }

    private void CloseScreen() => Call(manager, "Close");

    private static Text TextNamed(GameObject screen, string name) =>
        screen.GetComponentsInChildren<Text>(true).First(t => t.name == name);

    private static Button ButtonNamed(GameObject screen, string name) =>
        screen.GetComponentsInChildren<Button>(true).First(b => b.name == name);

    private static Component[] Views(GameObject screen) => screen.GetComponentsInChildren(T("ScrewView"), true);

    private static bool Active(GameObject screen, string path) => screen.transform.Find(path).gameObject.activeSelf;

    private static string CurrentView(GameObject screen) =>
        Get(screen.GetComponent(T("ComputerScreenController")), "CurrentView").ToString();

    private void GiveScrewdriverToPlayer()
    {
        var toolGo = Object.Instantiate(LoadPrefab(ToolPrefab));
        Assert.AreEqual(ToolType.Screwdriver, toolGo.GetComponent<ToolItem>().Type);
        Assert.IsTrue((bool)Call(holder, "Pick", toolGo.GetComponent(T("PickupInteractable"))));
    }

    private static void AssertOnly(GameObject screen, string view)
    {
        foreach (string v in new[] { "Frente", "Traseira", "Interior" })
            Assert.AreEqual(v == view, Active(screen, v), $"vista {v} (esperada só {view})");
    }

    [UnityTest]
    public IEnumerator Gabinete_Prefab_TemRootLogicoComVisual_E3ParafusosObrigatorios()
    {
        GameObject g = SpawnCase(Vector3.zero);
        yield return null;
        var pcCase = g.GetComponent<ComputerCase>();
        Assert.IsNotNull(g.GetComponent(T("UIScreenInteractable")));
        Assert.IsNotNull(g.GetComponent(T("PickupInteractable")));
        Assert.IsNotNull(g.GetComponent<BoxCollider>());
        Assert.IsNull(g.GetComponent<MeshRenderer>(), "malha deve ficar só no Visual");
        Assert.IsNotNull(g.transform.Find("Visual")?.GetComponent<MeshRenderer>());
        Assert.AreEqual(Vector3.one, g.transform.localScale);
        Assert.AreEqual(8, pcCase.TotalCount);
        Assert.AreEqual(3, pcCase.Screws.Count(s => s.required));
    }

    [UnityTest]
    public IEnumerator Tela_TemTresVistasEmTelaCheia_E3Botoes_SemBarraDeProgresso()
    {
        GameObject g = SpawnCase(Vector3.zero);
        yield return null;
        GameObject screen = OpenScreen(g);
        yield return null;

        var rt = (RectTransform)screen.transform;
        Assert.AreEqual(Vector2.zero, rt.anchorMin); Assert.AreEqual(Vector2.one, rt.anchorMax);
        foreach (string v in new[] { "Fundo", "Frente", "Traseira", "Interior" })
        {
            var vr = (RectTransform)screen.transform.Find(v);
            Assert.AreEqual(Vector2.zero, vr.anchorMin, v); Assert.AreEqual(Vector2.one, vr.anchorMax, v);
            Assert.AreEqual(Vector2.zero, vr.offsetMin, v); Assert.AreEqual(Vector2.zero, vr.offsetMax, v);
        }
        var nav = (RectTransform)screen.transform.Find("Navegacao");
        CollectionAssert.AreEqual(new[] { "BotaoVoltar", "BotaoAvancar", "BotaoFechar" },
            nav.GetComponentsInChildren<Button>(true).Select(b => b.name).ToArray());
        // Setas nas extremidades de baixo; X no canto superior direito (com margem, não centralizados).
        var voltar = (RectTransform)ButtonNamed(screen, "BotaoVoltar").transform;
        var avancar = (RectTransform)ButtonNamed(screen, "BotaoAvancar").transform;
        var fechar = (RectTransform)ButtonNamed(screen, "BotaoFechar").transform;
        Assert.AreEqual(Vector2.zero, voltar.anchorMin, "Voltar no canto inferior esquerdo");
        Assert.AreEqual(new Vector2(1f, 0f), avancar.anchorMin, "Avançar no canto inferior direito");
        Assert.AreEqual(Vector2.one, fechar.anchorMin, "Fechar no canto superior direito");
        Assert.AreEqual("X", fechar.GetComponentInChildren<Text>(true).text);
        // margem para dentro (não colado na borda nem centralizado)
        Assert.IsTrue(voltar.anchoredPosition.x > 0 && voltar.anchoredPosition.y > 0, "Voltar com margem");
        Assert.IsTrue(avancar.anchoredPosition.x < 0 && avancar.anchoredPosition.y > 0, "Avançar com margem");
        Assert.IsTrue(fechar.anchoredPosition.x < 0 && fechar.anchoredPosition.y < 0, "Fechar com margem");
        Assert.IsFalse(screen.GetComponentsInChildren<Text>(true).Any(t => t.name == "Progresso"));
        Assert.IsNull(screen.transform.Find("BarraFundo"));
        Assert.IsFalse(screen.GetComponentsInChildren<Transform>(true).Any(t => t.name == "BarraFundo" || t.name == "BotaoAbrir"));
    }

    [UnityTest]
    public IEnumerator Navegacao_Linear_InteriorBloqueadoAteParafusosObrigatorios()
    {
        GameObject g = SpawnCase(Vector3.zero);
        var pcCase = g.GetComponent<ComputerCase>();
        GiveScrewdriverToPlayer();
        yield return null;
        GameObject screen = OpenScreen(g);
        yield return null;
        Button back = ButtonNamed(screen, "BotaoVoltar"), next = ButtonNamed(screen, "BotaoAvancar");

        // Abre na frente; Voltar não dá a volta.
        AssertOnly(screen, "Frente");
        Assert.IsFalse(back.interactable);
        back.onClick.Invoke(); yield return null;
        AssertOnly(screen, "Frente");

        // Frente → Traseira (8 parafusos clicáveis).
        next.onClick.Invoke(); yield return null;
        AssertOnly(screen, "Traseira");
        Assert.AreEqual("Rear", CurrentView(screen));
        Component[] views = Views(screen);
        Assert.AreEqual(8, views.Length);

        // Sem nenhum parafuso: Avançar não sai da traseira.
        next.onClick.Invoke(); yield return null;
        AssertOnly(screen, "Traseira");
        Assert.AreEqual(CaseState.Closed, pcCase.State);

        // Bloqueado: Traseira ↔ Frente continua funcionando.
        back.onClick.Invoke(); yield return null;
        AssertOnly(screen, "Frente");
        next.onClick.Invoke(); yield return null;
        AssertOnly(screen, "Traseira");

        // Todos os opcionais (topo + lado esquerdo): ainda bloqueado.
        foreach (int i in new[] { 0, 1, 2, 4, 6 }) { views[i].GetComponent<Button>().onClick.Invoke(); yield return null; }
        Assert.AreEqual(5, pcCase.RemovedCount);
        next.onClick.Invoke(); yield return null;
        AssertOnly(screen, "Traseira");

        // 2 de 3 obrigatórios: ainda bloqueado.
        views[3].GetComponent<Button>().onClick.Invoke(); yield return null;
        views[5].GetComponent<Button>().onClick.Invoke(); yield return null;
        next.onClick.Invoke(); yield return null;
        AssertOnly(screen, "Traseira");
        Assert.IsTrue(views[3].transform.Find("Furo").gameObject.activeSelf, "feedback individual do parafuso");

        // Último obrigatório: libera.
        views[7].GetComponent<Button>().onClick.Invoke(); yield return null;
        next.onClick.Invoke(); yield return null;
        AssertOnly(screen, "Interior");
        Assert.AreEqual(CaseState.Open, pcCase.State);
        Assert.IsFalse(next.interactable, "última tela: Avançar não volta à frente");
        next.onClick.Invoke(); yield return null;
        AssertOnly(screen, "Interior");

        // Interior → Traseira → Frente.
        back.onClick.Invoke(); yield return null;
        AssertOnly(screen, "Traseira");
        back.onClick.Invoke(); yield return null;
        AssertOnly(screen, "Frente");

        // Fechar (botão real, listener persistente) fecha a tela.
        ButtonNamed(screen, "BotaoFechar").onClick.Invoke(); yield return null;
        Assert.IsFalse((bool)Get(manager, "IsOpen"));
        Assert.IsFalse(screen.activeSelf);
    }

    [UnityTest]
    public IEnumerator FecharEReabrir_VoltaNaUltimaVista()
    {
        GameObject g = SpawnCase(Vector3.zero);
        GiveScrewdriverToPlayer();
        yield return null;
        GameObject screen = OpenScreen(g);
        yield return null;
        AssertOnly(screen, "Frente");

        // Frente: fecha e reabre na Frente.
        CloseScreen(); yield return null;
        screen = OpenScreen(g); yield return null;
        AssertOnly(screen, "Frente");

        // Traseira: reabre na Traseira, e a navegação continua funcionando.
        Click(screen, "BotaoAvancar"); yield return null;
        ButtonNamed(screen, "BotaoFechar").onClick.Invoke(); yield return null;
        screen = OpenScreen(g); yield return null;
        AssertOnly(screen, "Traseira");
        Assert.IsTrue(ButtonNamed(screen, "BotaoVoltar").interactable);

        // Interior (desbloqueado): reabre no Interior, com a tampa ainda aberta.
        Component[] views = Views(screen);
        foreach (int i in new[] { 3, 5, 7 }) { views[i].GetComponent<Button>().onClick.Invoke(); yield return null; }
        Click(screen, "BotaoAvancar"); yield return null;
        AssertOnly(screen, "Interior");
        CloseScreen(); yield return null;
        screen = OpenScreen(g); yield return null;
        AssertOnly(screen, "Interior");
        Assert.AreEqual(CaseState.Open, g.GetComponent<ComputerCase>().State);
        Assert.IsFalse(ButtonNamed(screen, "BotaoAvancar").interactable);
        Click(screen, "BotaoVoltar"); yield return null;
        AssertOnly(screen, "Traseira");

        // Outro gabinete não herda a vista do primeiro.
        CloseScreen(); yield return null;
        GameObject g2 = SpawnCase(new Vector3(3f, 0f, 0f));
        yield return null;
        screen = OpenScreen(g2); yield return null;
        AssertOnly(screen, "Frente");
    }

    private static object CursorNow => T("UICursor").GetProperty("Current").GetValue(null);

    [UnityTest]
    public IEnumerator Cursor_MaozinhaPadrao_ChaveDeFendaSoNaTraseiraComAFerramenta()
    {
        GameObject g = SpawnCase(Vector3.zero);
        yield return null;
        var hand = new Texture2D(8, 8);
        Set(manager, "defaultCursor", hand);

        // sem a ferramenta: mãozinha em todas as vistas, inclusive na Traseira
        GameObject screen = OpenScreen(g); yield return null;
        Component ctrl = screen.GetComponent(T("ComputerScreenController"));
        var rear = (Texture2D)ctrl.GetType().GetField("rearCursor", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(ctrl);
        Assert.IsNotNull(rear, "cursor de chave de fenda configurado");
        Assert.AreSame(hand, CursorNow, "Frente: mãozinha");
        Click(screen, "BotaoAvancar"); yield return null;
        Assert.AreSame(hand, CursorNow, "Traseira sem a chave na mão: mãozinha");
        CloseScreen(); yield return null;
        Assert.IsNull(CursorNow, "fora das telas, nenhum cursor personalizado");

        // com a chave de fenda na mão: chave só na Traseira, proporcional aos parafusos
        GiveScrewdriverToPlayer();
        screen = OpenScreen(g); yield return null;
        Assert.AreEqual("Rear", CurrentView(screen), "reabre onde parou");
        Assert.AreSame(rear, CursorNow, "Traseira com a chave: chave de fenda");
        int h = (int)Call(ctrl, "ScrewdriverCursorHeight");
        Assert.GreaterOrEqual(h, 48, "cursor grande o bastante");
        Click(screen, "BotaoVoltar"); yield return null;
        Assert.AreSame(hand, CursorNow, "Frente: volta a mãozinha");
        Click(screen, "BotaoAvancar"); yield return null;
        Assert.AreSame(rear, CursorNow);
        CloseScreen(); yield return null;
        Assert.IsNull(CursorNow, "sair da manutenção não deixa a chave de fenda ativa");
        Object.Destroy(hand);
    }

    [UnityTest]
    public IEnumerator Instalacao_MostraEtapas_ESoAndaComOJogadorNaFrente()
    {
        GameObject g = SpawnCase(Vector3.zero);
        var pcCase = g.GetComponent<ComputerCase>();
        var power = g.GetComponent<ComputerPower>();
        pcCase.SetComponents(ComputerCase.AllComponents, false);      // PC completo, HD de fábrica sem sistema
        yield return null;
        foreach (var (prefab, a, b) in new[] { ("Pickup_CaboEnergia", PcPort.CasePower, PcPort.Outlet),
                                               ("Pickup_CaboEnergia", PcPort.MonitorPower, PcPort.MonitorOutlet),
                                               ("Pickup_CaboVideo", PcPort.CaseVideo, PcPort.MonitorVideo) })
        {
            var cable = Object.Instantiate(LoadPrefab("Assets/Prefabs/Gameplay/" + prefab + ".prefab")).GetComponent<CableItem>();
            Assert.AreEqual(PortResult.PluggedFromHand, power.UsePort(a, cable, true, out _));
            Assert.AreEqual(PortResult.PluggedOtherEnd, power.UsePort(b, null, false, out _));
        }
        GameObject screen = OpenScreen(g); yield return null;
        Image monitor = MonitorScreen(screen);

        Click(screen, "BotaoPower");
        Assert.AreEqual(PcScreen.NoBootableDevice, power.Screen, "HD sem sistema e sem pendrive");
        Assert.AreEqual("PH_ui_tela_bios_sem_sistema", monitor.sprite.name);
        Click(screen, "BotaoPower");

        var pendrive = Object.Instantiate(LoadPrefab("Assets/Prefabs/Gameplay/Pickup_Pendrive.prefab")).GetComponent<PendriveItem>();
        power.UseUsbPort(0, pendrive, true, out _);
        Click(screen, "BotaoPower");
        Assert.AreEqual(PcScreen.Installing, power.Screen);
        Assert.AreEqual("PH_ui_tela_instalando_0", monitor.sprite.name);
        yield return new WaitForSeconds(0.5f);
        float onFront = power.InstallProgress;
        Assert.Greater(onFront, 0f, "na Frente a instalação anda");

        // Traseira: pausa (não cancela, não conclui)
        Click(screen, "BotaoAvancar"); yield return null;
        Assert.IsTrue(power.InstallPaused);
        float paused = power.InstallProgress;
        yield return new WaitForSeconds(0.5f);
        Assert.AreEqual(paused, power.InstallProgress, 1e-4f, "parada fora da Frente");
        Assert.IsTrue(power.IsInstallingOperatingSystem);

        // de volta à Frente: continua de onde parou
        Click(screen, "BotaoVoltar"); yield return null;
        Assert.IsFalse(power.InstallPaused);
        yield return new WaitForSeconds(0.3f);
        Assert.Greater(power.InstallProgress, paused);
        StringAssert.StartsWith("PH_ui_tela_instalando_", monitor.sprite.name);
        power.AdvanceInstall(100f);
        yield return null;
        Assert.AreEqual(PcScreen.OperatingSystem, power.Screen);
        Assert.AreEqual("PH_ui_tela_sistema", monitor.sprite.name, "sem cabo de rede");
        Assert.IsTrue(pcCase.HddHasOperatingSystem);
        CloseScreen();
        Assert.IsFalse(power.InstallPaused);
    }

    [Test]
    public void Traseira_TomadaDoMonitorDesenhadaAntesDosCabos()
    {
        Transform rear = LoadPrefab(ScreenPrefab).transform.Find("Traseira");
        Assert.Less(rear.Find("Tomada_Monitor").GetSiblingIndex(), rear.Find("Cabos").GetSiblingIndex(),
            "cabo da tomada do monitor desenhado na frente do plugue");
        Assert.Less(rear.Find("Tomada").GetSiblingIndex(), rear.Find("Cabos").GetSiblingIndex());
    }

    [UnityTest]
    public IEnumerator SoObrigatorios_LiberamOInterior()
    {
        GameObject g = SpawnCase(Vector3.zero);
        GiveScrewdriverToPlayer();
        yield return null;
        GameObject screen = OpenScreen(g);
        yield return null;
        Button next = ButtonNamed(screen, "BotaoAvancar");
        next.onClick.Invoke(); yield return null;
        Component[] views = Views(screen);
        foreach (int i in new[] { 3, 5, 7 }) { views[i].GetComponent<Button>().onClick.Invoke(); yield return null; }
        next.onClick.Invoke(); yield return null;
        AssertOnly(screen, "Interior");
        Assert.AreEqual(3, g.GetComponent<ComputerCase>().RemovedCount, "opcionais continuam no lugar");
    }

    [UnityTest]
    public IEnumerator Componentes_NaoSaoSorteadosDeNovo_AoReabrir()
    {
        GameObject g = SpawnCase(Vector3.zero);
        var pcCase = g.GetComponent<ComputerCase>();
        GiveScrewdriverToPlayer();
        yield return null;
        PcComponent rolled = pcCase.Components;

        GameObject screen = OpenScreen(g);
        yield return null;
        Button next = ButtonNamed(screen, "BotaoAvancar");
        next.onClick.Invoke(); yield return null;
        Component[] views = Views(screen);
        foreach (int i in new[] { 3, 5, 7 }) { views[i].GetComponent<Button>().onClick.Invoke(); yield return null; }

        for (int round = 0; round < 5; round++)
        {
            if (round > 0)
            {
                screen = OpenScreen(g);
                yield return null;
                next = ButtonNamed(screen, "BotaoAvancar");
                next.onClick.Invoke(); yield return null;
            }
            next.onClick.Invoke(); yield return null;
            AssertOnly(screen, "Interior");
            Assert.AreEqual(rolled, pcCase.Components);
            Assert.AreEqual(pcCase.HasComponent(PcComponent.Motherboard), Active(screen, "Interior/ContainerInterior_Area/ContainerInterior/Componentes/PlacaMae"));
            Assert.AreEqual(pcCase.HasComponent(PcComponent.Ram), Active(screen, "Interior/ContainerInterior_Area/ContainerInterior/Componentes/MemoriaRAM"));
            Assert.AreEqual(pcCase.HasComponent(PcComponent.Hdd), Active(screen, "Interior/ContainerInterior_Area/ContainerInterior/Componentes/HD"));
            Assert.AreEqual(pcCase.HasComponent(PcComponent.Psu), Active(screen, "Interior/ContainerInterior_Area/ContainerInterior/Componentes/Fonte"));
            Assert.AreEqual(pcCase.HasComponent(PcComponent.CpuCooler), Active(screen, "Interior/ContainerInterior_Area/ContainerInterior/Componentes/Cooler"));
            CloseScreen();
            yield return null;
        }
    }

    [UnityTest]
    public IEnumerator Componentes_Fixos_AparecemConformeOEstado()
    {
        GameObject g = SpawnCase(Vector3.zero);
        var pcCase = g.GetComponent<ComputerCase>();
        pcCase.SetComponents(PcComponent.Psu | PcComponent.Hdd);
        GiveScrewdriverToPlayer();
        yield return null;
        GameObject screen = OpenScreen(g);
        yield return null;
        Button next = ButtonNamed(screen, "BotaoAvancar");
        next.onClick.Invoke(); yield return null;
        Component[] views = Views(screen);
        foreach (int i in new[] { 3, 5, 7 }) { views[i].GetComponent<Button>().onClick.Invoke(); yield return null; }
        next.onClick.Invoke(); yield return null;
        const string c = "Interior/ContainerInterior_Area/ContainerInterior/Componentes/";
        Assert.IsFalse(Active(screen, c + "PlacaMae"));
        Assert.IsFalse(Active(screen, c + "MemoriaRAM"));
        Assert.IsTrue(Active(screen, c + "HD"));
        Assert.IsTrue(Active(screen, c + "Fonte"));
        Assert.IsFalse(Active(screen, c + "Cooler"));
        // RAM e cooler encaixam na placa-mãe: sem ela, os encaixes nem aparecem
        const string e = "Interior/ContainerInterior_Area/ContainerInterior/Encaixes/";
        Assert.IsFalse(Active(screen, e + "Encaixe_MemoriaRAM"));
        Assert.IsFalse(Active(screen, e + "Encaixe_Cooler"));
        Assert.IsTrue(Active(screen, e + "Encaixe_Fonte"));
        Assert.IsTrue(Active(screen, e + "Encaixe_PlacaMae"));
    }

    [UnityTest]
    public IEnumerator SemFerramenta_ParafusoNaoSai_E_SegundaTelaNaoAbre()
    {
        GameObject g = SpawnCase(Vector3.zero);
        yield return null;
        GameObject screen = OpenScreen(g);
        yield return null;
        Assert.IsFalse((bool)Call(manager, "Open", LoadPrefab(ScreenPrefab).GetComponent(T("UIScreen")), g, detector),
            "não abre outra instância com uma já aberta");
        ButtonNamed(screen, "BotaoAvancar").onClick.Invoke(); yield return null;
        Views(screen)[3].GetComponent<Button>().onClick.Invoke(); yield return null;
        Assert.AreEqual(0, g.GetComponent<ComputerCase>().RemovedCount);
    }

    [UnityTest]
    public IEnumerator DoisGabinetes_CompartilhamATela_MasNaoOEstado()
    {
        GameObject a = SpawnCase(Vector3.zero);
        GameObject b = SpawnCase(new Vector3(3f, 0f, 0f));
        GiveScrewdriverToPlayer();
        yield return null;

        GameObject screen = OpenScreen(a);
        yield return null;
        ButtonNamed(screen, "BotaoAvancar").onClick.Invoke(); yield return null;
        Views(screen)[2].GetComponent<Button>().onClick.Invoke();
        yield return null;
        Assert.AreEqual(1, a.GetComponent<ComputerCase>().RemovedCount);
        CloseScreen();
        yield return null;

        screen = OpenScreen(b);
        yield return null;
        AssertOnly(screen, "Frente");
        Assert.AreEqual(0, b.GetComponent<ComputerCase>().RemovedCount);
        Assert.AreEqual(8, Views(screen).Count(v => v.GetComponent<Button>().interactable), "views reconstruídas para o outro gabinete");
        CloseScreen();
        yield return null;

        screen = OpenScreen(a);
        yield return null;
        Assert.IsFalse(Views(screen)[2].GetComponent<Button>().interactable);
    }

    private Component GiveToPlayer(string prefabPath)
    {
        var go = Object.Instantiate(LoadPrefab(prefabPath));
        Component pickup = go.GetComponent(T("PickupInteractable"));
        Assert.IsTrue((bool)Call(holder, "Pick", pickup), "não pegou " + prefabPath);
        return pickup;
    }

    private bool Holding => (bool)Get(holder, "IsHolding");

    private static void Click(GameObject screen, string name) => ButtonNamed(screen, name).onClick.Invoke();

    private static Image MonitorScreen(GameObject screen) =>
        screen.GetComponentsInChildren<Image>(true).First(i => i.name == "Tela");

    [UnityTest]
    public IEnumerator Montagem_CabosEPecas_PelaTela_LigaNaBios_EDepoisNoSistema()
    {
        GameObject g = SpawnCase(Vector3.zero);
        var pcCase = g.GetComponent<ComputerCase>();
        var power = g.GetComponent<ComputerPower>();
        Assert.IsNotNull(power, "Gabinete.prefab precisa de ComputerPower");
        pcCase.SetComponents(PcComponent.None);
        for (int i = 0; i < pcCase.TotalCount; i++) pcCase.TryRemoveScrew(i, ToolType.Screwdriver);
        yield return null;

        GameObject screen = OpenScreen(g);
        yield return null;
        Image monitor = MonitorScreen(screen);
        Assert.IsNull(monitor.sprite, "desligado = tela preta");

        // Sem nada: Power não liga.
        Click(screen, "BotaoPower");
        Assert.AreEqual(PcPowerState.Off, power.State);
        const string led = "Frente/Gabinete/Area/BotaoPower/LED";
        Assert.IsFalse(Active(screen, led), "desligado: LED do Power apagado");

        // Cabo de energia: gabinete (sai da mão) e tomada.
        CloseScreen(); yield return null;
        Component powerCable = GiveToPlayer("Assets/Prefabs/Gameplay/Pickup_CaboEnergia.prefab");
        screen = OpenScreen(g); yield return null;
        Click(screen, "BotaoAvancar"); yield return null;
        Assert.AreEqual("Rear", CurrentView(screen));
        Click(screen, "Porta_EnergiaGabinete");
        Assert.IsFalse(Holding, "o cabo conectado sai da mão");
        Assert.IsFalse(powerCable.gameObject.activeSelf, "guardado (desativado), não destruído");
        Click(screen, "Porta_Tomada");
        Assert.IsTrue(power.PowerConnected);
        Assert.IsTrue(screen.transform.Find("Traseira/Cabos/CaboEnergia").gameObject.activeSelf, "linha do cabo visível");

        // Cabo de energia do monitor: tomada própria e entrada do monitor.
        CloseScreen(); yield return null;
        GiveToPlayer("Assets/Prefabs/Gameplay/Pickup_CaboEnergia.prefab");
        screen = OpenScreen(g); yield return null;
        Assert.AreEqual("Rear", CurrentView(screen), "reabre na Traseira, onde parou");
        Click(screen, "Porta_EnergiaMonitor");
        Click(screen, "Porta_TomadaMonitor");
        Assert.IsTrue(power.MonitorPowerConnected);
        Assert.IsTrue(screen.transform.Find("Traseira/Cabos/CaboEnergiaMonitor").gameObject.activeSelf);

        // Cabo de vídeo: HDMI (gabinete e monitor).
        CloseScreen(); yield return null;
        GiveToPlayer("Assets/Prefabs/Gameplay/Pickup_CaboHDMI.prefab");
        screen = OpenScreen(g); yield return null;
        Assert.AreEqual("Rear", CurrentView(screen), "reabre na Traseira, onde parou");
        Click(screen, "Porta_VGAGabinete");
        Assert.IsTrue(Holding, "HDMI não entra na VGA");
        Click(screen, "Porta_HDMIGabinete");
        Click(screen, "Porta_HDMIMonitor");
        Assert.IsTrue(power.VideoConnected);

        // Abre a tampa e instala placa-mãe e RAM pelos encaixes do interior.
        Click(screen, "BotaoAvancar"); yield return null;
        Assert.AreEqual("Interior", CurrentView(screen));
        foreach (var (prefab, slot) in new[] { ("Pickup_Fonte", "Encaixe_Fonte"), ("Pickup_PlacaMae", "Encaixe_PlacaMae"),
                                               ("Pickup_MemoriaRAM", "Encaixe_MemoriaRAM"), ("Pickup_Cooler", "Encaixe_Cooler") })
        {
            GiveToPlayer("Assets/Prefabs/Gameplay/" + prefab + ".prefab");
            Click(screen, slot);
            Assert.IsFalse(Holding, slot);
        }
        Assert.AreEqual(PcComponent.Psu | PcComponent.Motherboard | PcComponent.Ram | PcComponent.CpuCooler, pcCase.Components);

        // Power na frente: BIOS.
        Click(screen, "BotaoVoltar"); Click(screen, "BotaoVoltar"); yield return null;
        Click(screen, "BotaoPower");
        Assert.AreEqual(PcPowerState.Bios, power.State);
        Assert.AreEqual("PH_ui_tela_bios", monitor.sprite.name);
        Assert.IsTrue(Active(screen, led), "ligado: LED do Power aceso");

        // HD novo (da gaveta) vem vazio: mesmo reiniciando, fica na BIOS.
        Click(screen, "BotaoAvancar"); Click(screen, "BotaoAvancar"); yield return null;
        GiveToPlayer("Assets/Prefabs/Gameplay/Pickup_HD.prefab");
        Click(screen, "Encaixe_HD");
        Click(screen, "BotaoVoltar"); Click(screen, "BotaoVoltar"); yield return null;
        Click(screen, "BotaoPower");
        Assert.AreEqual(PcPowerState.Off, power.State);
        Assert.IsNull(monitor.sprite);
        Click(screen, "BotaoPower");
        Assert.AreEqual(PcPowerState.Bios, power.State, "HD sem sistema");
        Click(screen, "BotaoPower");

        // Pendrive numa USB da traseira: some da mão, aparece na porta (2D e 3D).
        Click(screen, "BotaoAvancar"); yield return null;
        GiveToPlayer("Assets/Prefabs/Gameplay/Pickup_Pendrive.prefab");
        Click(screen, "Porta_USB_1");
        Assert.IsFalse(Holding, "pendrive conectado sai da mão");
        Assert.AreEqual(1, power.PendrivePort);
        Assert.IsTrue(Active(screen, "Traseira/ContainerTraseira_Area/ContainerTraseira/Portas/Porta_USB_1/Plugue"));
        Assert.IsTrue(g.transform.Find("Pendrive3D").gameObject.activeSelf, "pendrive encaixado no gabinete 3D");

        // Liga: boot pelo pendrive (BIOS) → instala no HD → sistema.
        Click(screen, "BotaoVoltar"); yield return null;
        Click(screen, "BotaoPower");
        Assert.AreEqual(PcPowerState.Bios, power.State);
        Assert.IsTrue(power.IsInstallingOperatingSystem);
        power.AdvanceInstall(10f);
        yield return null;
        Assert.AreEqual(PcPowerState.OperatingSystem, power.State);
        Assert.AreEqual("PH_ui_tela_sistema", monitor.sprite.name);
        Assert.IsTrue(pcCase.HddHasOperatingSystem);

        // Desconecta o pendrive (mão livre): volta o MESMO objeto para a mão; o sistema continua no HD.
        Click(screen, "BotaoAvancar"); yield return null;
        Click(screen, "Porta_USB_1");
        Assert.IsTrue(Holding);
        Assert.IsNotNull(((Component)Get(holder, "Current")).GetComponent<PendriveItem>());
        Assert.IsFalse(g.transform.Find("Pendrive3D").gameObject.activeSelf);
        Assert.AreEqual(PcPowerState.OperatingSystem, power.State);
        Click(screen, "Porta_USB_1");                 // conecta de novo: mão livre para o resto do teste
        Assert.IsFalse(Holding);
        Click(screen, "BotaoVoltar"); yield return null;

        // Cabo de rede: gabinete ↔ ponto de rede → sistema "conectado".
        CloseScreen(); yield return null;
        GiveToPlayer("Assets/Prefabs/Gameplay/Pickup_CaboRede.prefab");
        screen = OpenScreen(g); yield return null;
        monitor = MonitorScreen(screen);
        Click(screen, "BotaoAvancar"); yield return null;
        Click(screen, "Porta_RedeGabinete");
        Click(screen, "Porta_Rede");
        Assert.IsTrue(power.Online);
        Assert.AreEqual("PH_ui_tela_sistema_rede", monitor.sprite.name);
        Click(screen, "BotaoVoltar"); yield return null;

        // Soltar a tomada com o PC ligado desliga (estado incompatível não é permitido).
        Click(screen, "BotaoAvancar"); yield return null;
        Click(screen, "Porta_Tomada");
        Assert.AreEqual(PcPowerState.Off, power.State);
        Assert.IsNull(monitor.sprite);
        Assert.IsFalse(Active(screen, led), "desligou: LED apaga");

        // Soltar a outra ponta devolve o MESMO cabo à mão.
        Click(screen, "Porta_EnergiaGabinete");
        Assert.IsTrue(Holding);
        Assert.AreSame(powerCable, Get(holder, "Current"));
        CloseScreen();
        foreach (var p in Object.FindObjectsByType(T("PickupInteractable"), FindObjectsInactive.Include, FindObjectsSortMode.None))
            Object.Destroy(((Component)p).gameObject);
    }
}
