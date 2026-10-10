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
/// usados por reflexão. Cobre a fiação dos prefabs; a aparência e o clique real do mouse continuam sendo teste manual.
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

    private GameObject SpawnCase(Vector3 pos)
    {
        var prefab = LoadPrefab(CasePrefab);
        Assert.IsNotNull(prefab);
        return Object.Instantiate(prefab, pos, Quaternion.identity);
    }

    private GameObject OpenScreen(GameObject source)
    {
        var screenPrefab = LoadPrefab(ScreenPrefab);
        Component screenComp = screenPrefab.GetComponent(T("UIScreen"));
        bool opened = (bool)Call(manager, "Open", screenComp, source, detector);
        Assert.IsTrue(opened, "UIScreenManager.Open recusou");
        return canvasGo.transform.GetChild(0).gameObject;
    }

    private void CloseScreen() => Call(manager, "Close");

    private static Text TextNamed(GameObject screen, string name) =>
        screen.GetComponentsInChildren<Text>(true).First(t => t.name == name);

    private static Component[] Views(GameObject screen) => screen.GetComponentsInChildren(T("ScrewView"), true);

    private static Transform Child(GameObject screen, string path) => screen.transform.Find(path);

    private void GiveScrewdriverToPlayer()
    {
        var toolGo = Object.Instantiate(LoadPrefab(ToolPrefab));
        Assert.IsNotNull(toolGo.GetComponent<ToolItem>(), "Pickup_Ferramenta sem ToolItem");
        Assert.AreEqual(ToolType.Screwdriver, toolGo.GetComponent<ToolItem>().Type);
        bool picked = (bool)Call(holder, "Pick", toolGo.GetComponent(T("PickupInteractable")));
        Assert.IsTrue(picked);
    }

    [UnityTest]
    public IEnumerator Gabinete_Prefab_TemRootLogicoComVisual()
    {
        GameObject g = SpawnCase(Vector3.zero);
        yield return null;
        Assert.IsNotNull(g.GetComponent<ComputerCase>());
        Assert.IsNotNull(g.GetComponent(T("UIScreenInteractable")));
        Assert.IsNotNull(g.GetComponent(T("PickupInteractable")));
        Assert.IsNotNull(g.GetComponent<BoxCollider>());
        Assert.IsNull(g.GetComponent<MeshRenderer>(), "malha deve ficar só no Visual");
        Assert.IsNotNull(g.transform.Find("Visual")?.GetComponent<MeshRenderer>());
        Assert.AreEqual(Vector3.one, g.transform.localScale);
        Assert.AreEqual(8, g.GetComponent<ComputerCase>().TotalCount);
    }

    [UnityTest]
    public IEnumerator Fluxo_Completo_SemFerramenta_Depois_ComChave_AbreInterior()
    {
        GameObject g = SpawnCase(Vector3.zero);
        var pcCase = g.GetComponent<ComputerCase>();
        yield return null;

        // --- abre a tela com as mãos vazias -------------------------------------------------------------------
        GameObject screen = OpenScreen(g);
        yield return null;

        Component[] views = Views(screen);
        Assert.AreEqual(8, views.Length, "um ScrewView por parafuso");
        Assert.IsTrue(Child(screen, "Moldura/Traseira").gameObject.activeSelf, "vista traseira visível");
        Assert.IsFalse(Child(screen, "Moldura/Interior").gameObject.activeSelf, "interior bloqueado");
        Assert.AreEqual("Parafusos removidos: 0/8", TextNamed(screen, "Progresso").text);
        Assert.AreEqual("Na mão: nada", TextNamed(screen, "Ferramenta").text);
        Assert.AreEqual("Gabinete - traseira", TextNamed(screen, "Titulo").text);
        Button openButton = screen.GetComponentsInChildren<Button>(true).First(b => b.name == "BotaoAbrir");
        Assert.IsFalse(openButton.interactable, "Abrir gabinete bloqueado");

        // Posição de cada parafuso = dado do ComputerCase (Y invertido para âncora de UI).
        for (int i = 0; i < 8; i++)
        {
            var rt = (RectTransform)views[i].transform;
            Vector2 p = pcCase.Screws[i].normalizedPosition;
            Assert.AreEqual(p.x, rt.anchorMin.x, 1e-5f);
            Assert.AreEqual(1f - p.y, rt.anchorMin.y, 1e-5f);
        }

        // --- clique sem ferramenta: nada acontece, mensagem de ajuda --------------------------------------------
        views[0].GetComponent<Button>().onClick.Invoke();
        yield return null;
        Assert.AreEqual(0, pcCase.RemovedCount);
        StringAssert.Contains("chave de fenda", TextNamed(screen, "Instrucao").text);

        // --- pega a chave (item real), reabre a tela -------------------------------------------------------------
        CloseScreen();
        yield return null;
        GiveScrewdriverToPlayer();
        screen = OpenScreen(g);
        yield return null;
        Assert.AreEqual("Na mão: chave de fenda", TextNamed(screen, "Ferramenta").text);
        views = Views(screen);

        // --- remove 7: ainda não abre ----------------------------------------------------------------------------
        for (int i = 0; i < 7; i++)
        {
            views[i].GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.IsTrue(pcCase.Screws[i].removed);
            Assert.IsFalse(views[i].GetComponent<Button>().interactable, "parafuso removido não é mais clicável");
            Assert.IsTrue(views[i].transform.Find("Furo").gameObject.activeSelf, "furo visível = feedback de remoção");
            Assert.AreEqual($"Parafusos removidos: {i + 1}/8", TextNamed(screen, "Progresso").text);
        }
        Assert.IsFalse(openButton.interactable, "com 7/8 continua bloqueado");
        Assert.AreEqual(CaseOpenResult.ScrewsRemaining, pcCase.TryOpen());

        // --- fecha e reabre a tela: estado persiste no objeto lógico ----------------------------------------------
        CloseScreen();
        yield return null;
        screen = OpenScreen(g);
        yield return null;
        views = Views(screen);
        Assert.AreEqual(7, pcCase.RemovedCount);
        Assert.AreEqual("Parafusos removidos: 7/8", TextNamed(screen, "Progresso").text);
        for (int i = 0; i < 7; i++) Assert.IsFalse(views[i].GetComponent<Button>().interactable);
        Assert.IsTrue(views[7].GetComponent<Button>().interactable);

        // --- último parafuso libera o botão; abrir mostra o interior -----------------------------------------------
        views[7].GetComponent<Button>().onClick.Invoke();
        yield return null;
        openButton = screen.GetComponentsInChildren<Button>(true).First(b => b.name == "BotaoAbrir");
        Assert.IsTrue(openButton.interactable, "8/8 libera o botão");
        Assert.AreEqual(CaseState.Closed, pcCase.State, "só abre ao clicar em Abrir");

        openButton.onClick.Invoke();
        yield return null;
        Assert.AreEqual(CaseState.Open, pcCase.State);
        Assert.IsFalse(Child(screen, "Moldura/Traseira").gameObject.activeSelf);
        Assert.IsTrue(Child(screen, "Moldura/Interior").gameObject.activeSelf);
        Assert.AreEqual("Gabinete - interior", TextNamed(screen, "Titulo").text);

        // --- reabrir depois de aberto continua no interior ---------------------------------------------------------
        CloseScreen();
        yield return null;
        screen = OpenScreen(g);
        yield return null;
        Assert.IsTrue(Child(screen, "Moldura/Interior").gameObject.activeSelf);
        Assert.IsFalse(Child(screen, "Moldura/Traseira").gameObject.activeSelf);
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
        Views(screen)[2].GetComponent<Button>().onClick.Invoke();
        yield return null;
        Assert.AreEqual(1, a.GetComponent<ComputerCase>().RemovedCount);
        CloseScreen();
        yield return null;

        screen = OpenScreen(b);
        yield return null;
        Assert.AreEqual(0, b.GetComponent<ComputerCase>().RemovedCount);
        Assert.AreEqual("Parafusos removidos: 0/8", TextNamed(screen, "Progresso").text);
        Assert.AreEqual(8, Views(screen).Count(v => v.GetComponent<Button>().interactable), "views reconstruídas para o outro gabinete");
        CloseScreen();
        yield return null;

        screen = OpenScreen(a);
        yield return null;
        Assert.AreEqual("Parafusos removidos: 1/8", TextNamed(screen, "Progresso").text);
        Assert.IsFalse(Views(screen)[2].GetComponent<Button>().interactable);
    }
}
