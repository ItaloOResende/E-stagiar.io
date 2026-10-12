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
        Assert.AreEqual(new Vector2(1f, 0f), nav.anchorMin, "canto inferior direito");
        CollectionAssert.AreEqual(new[] { "BotaoVoltar", "BotaoAvancar", "BotaoFechar" },
            nav.GetComponentsInChildren<Button>(true).Select(b => b.name).ToArray());
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
        Assert.AreEqual("Computador - frente", TextNamed(screen, "Titulo").text);
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
        StringAssert.Contains("faltam parafusos", TextNamed(screen, "Instrucao").text);

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
            CloseScreen();
            yield return null;
        }
    }

    [UnityTest]
    public IEnumerator Componentes_Fixos_AparecemConformeOEstado()
    {
        GameObject g = SpawnCase(Vector3.zero);
        var pcCase = g.GetComponent<ComputerCase>();
        pcCase.SetComponents(PcComponent.Ram | PcComponent.Hdd);
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
        Assert.IsTrue(Active(screen, c + "MemoriaRAM"));
        Assert.IsTrue(Active(screen, c + "HD"));
        StringAssert.Contains("memória RAM, HD", TextNamed(screen, "Instrucao").text);
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
        StringAssert.Contains("chave de fenda", TextNamed(screen, "Instrucao").text);
        Assert.AreEqual("Na mão: nada", TextNamed(screen, "Ferramenta").text);
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
}
