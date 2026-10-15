using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

/// <summary>
/// Infrações da 1ª versão: acontecer (ConductMonitor) × ser percebida (SupervisorPerception) × reação/aviso
/// (SupervisorPatrol.React, SupervisorAlertHUD). Peças reais (Pickup_HD quebra na 2ª queda, Pickup_MemoriaRAM na 1ª).
/// Sem pontuação/vidas: só eventos e métricas do InfractionLog.
/// </summary>
public class InfracoesTests
{
    private readonly List<GameObject> created = new List<GameObject>();

    private static Type T(string n) => Type.GetType(n + ", Assembly-CSharp", true);
    private static object Kind(string n) => Enum.Parse(T("InfractionKind"), n);
    private static object Static(string m, params object[] a) => T("InfractionLog").GetMethod(m).Invoke(null, a);
    private static int Occ(string k) => (int)Static("OccurredCount", Kind(k));
    private static int Seen(string k) => (int)Static("WitnessedCount", Kind(k));
    private static int Lost(string k) => (int)Static("MissedCount", Kind(k));
    private static void Set(object o, string f, object v) => o.GetType().GetField(f, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(o, v);
    private static object Get(object o, string p) => o.GetType().GetProperty(p).GetValue(o);
    private static object Call(object o, string m, params object[] a) => o.GetType().GetMethod(m).Invoke(o, a);

    private static GameObject Prefab(string name)
    {
        Type db = Type.GetType("UnityEditor.AssetDatabase, UnityEditor", true);
        return (GameObject)db.GetMethod("LoadAssetAtPath", new[] { typeof(string), typeof(Type) })
            .Invoke(null, new object[] { "Assets/Prefabs/Gameplay/" + name + ".prefab", typeof(GameObject) });
    }

    private Component monitor;

    [SetUp]
    public void SetUp()
    {
        Static("ResetAll");
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        created.Add(floor);
        floor.name = "Chao";
        floor.transform.localScale = new Vector3(30f, 0.2f, 30f);
        floor.transform.position = new Vector3(0f, -0.1f, 0f);
        Physics.SyncTransforms();
        GameObject m = New("Conduta");
        monitor = m.AddComponent(T("ConductMonitor"));
        Set(monitor, "witnessWindow", 1.2f);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject g in created) if (g != null) Object.Destroy(g);
        created.Clear();
        Static("ResetAll");
    }

    private GameObject New(string n) { var g = new GameObject(n); created.Add(g); return g; }

    /// <summary>Supervisor parado na origem olhando para +Z, só percepção (sem reação).</summary>
    private Component Supervisor(bool react = false)
    {
        GameObject npc = New("Supervisor");
        Component p = npc.AddComponent(T("SupervisorPerception"));
        Set(p, "react", react);
        return p;
    }

    /// <summary>Solta a peça de 1,2 m de altura (cai de verdade: impacto real, conta como queda).</summary>
    private GameObject Drop(string prefab, Vector3 groundPos)
    {
        GameObject g = Object.Instantiate(Prefab(prefab), groundPos + Vector3.up * 1.2f, Quaternion.identity);
        created.Add(g);
        Component pick = g.GetComponent(T("PickupInteractable"));
        pick.GetType().GetMethod("OnReleased").Invoke(pick, new object[] { groundPos + Vector3.up * 3f, null, (Vector3?)(groundPos + Vector3.up * 1.2f) });
        return g;
    }

    private static IEnumerator Wait(float s) { yield return new WaitForSeconds(s); }

    [UnityTest]
    public IEnumerator Queda_NoCampoDeVisao_EPercebida_EAPecaNaoQuebra()
    {
        Supervisor();
        GameObject hd = Drop("Pickup_HD", new Vector3(0.5f, 0f, 5f));
        yield return Wait(2f);
        Assert.AreEqual(1, Occ("ComponentDropped"), "uma queda");
        Assert.AreEqual(1, Seen("ComponentDropped"), "vista");
        Assert.AreEqual(0, Occ("ComponentBroken"));
        var part = hd.GetComponent<ComputerPartItem>();
        Assert.AreEqual(PartCondition.Damaged, part.Condition, "só derrubada: continua funcionando");
        Assert.IsFalse(part.IsBroken);
    }

    [UnityTest]
    public IEnumerator Queda_ForaDoCampoDeVisao_EDaAudicao_Despercebida()
    {
        Supervisor();
        Drop("Pickup_HD", new Vector3(0f, 0f, -6f));             // atrás, além da audição (3 m)
        yield return Wait(2.5f);
        Assert.AreEqual(1, Occ("ComponentDropped"));
        Assert.AreEqual(0, Seen("ComponentDropped"));
        Assert.AreEqual(1, Lost("ComponentDropped"), "janela passou sem ninguém ver");
    }

    [UnityTest]
    public IEnumerator Queda_AtrasDePertoEOuvida()
    {
        Supervisor();
        Drop("Pickup_HD", new Vector3(0f, 0f, -2f));             // atrás, mas a 2 m: ouve o barulho
        yield return Wait(2f);
        Assert.AreEqual(1, Seen("ComponentDropped"), "queda ouvida de perto");
    }

    [UnityTest]
    public IEnumerator Obstaculo_BloqueiaALinhaDeVisao()
    {
        Supervisor();
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        created.Add(wall);
        wall.transform.position = new Vector3(0f, 1.5f, 3f);
        wall.transform.localScale = new Vector3(4f, 3f, 0.2f);
        Physics.SyncTransforms();
        Drop("Pickup_HD", new Vector3(0f, 0f, 5.5f));
        yield return Wait(2.5f);
        Assert.AreEqual(1, Occ("ComponentDropped"));
        Assert.AreEqual(0, Seen("ComponentDropped"), "parede no meio");
        Assert.AreEqual(1, Lost("ComponentDropped"));
    }

    [UnityTest]
    public IEnumerator Quebra_EQueda_SaoEstadosEInfracoesDiferentes()
    {
        Supervisor();
        GameObject ram = Drop("Pickup_MemoriaRAM", new Vector3(-1f, 0f, 4f));   // RAM quebra na 1ª queda
        yield return Wait(2f);
        Assert.AreEqual(1, Occ("ComponentBroken"));
        Assert.AreEqual(1, Seen("ComponentBroken"));
        Assert.AreEqual(0, Occ("ComponentDropped"), "a quebra não conta também como 'só derrubou'");
        Assert.IsTrue(ram.GetComponent<ComputerPartItem>().IsBroken, "estado de dano preservado");
    }

    [UnityTest]
    public IEnumerator LargadoNoChao_DepoisDaTolerancia_UmaVezSo_ENaBancadaNao()
    {
        Set(monitor, "abandonDelay", 1f);
        Supervisor();
        GameObject bench = GameObject.CreatePrimitive(PrimitiveType.Cube);
        created.Add(bench);
        bench.transform.position = new Vector3(2f, 0.375f, 3f);
        bench.transform.localScale = new Vector3(1.2f, 0.75f, 0.8f);
        bench.AddComponent<WorkSurface>();
        Physics.SyncTransforms();

        GameObject onFloor = Object.Instantiate(Prefab("Pickup_Ferramenta"), Vector3.down * 5f, Quaternion.identity);
        GameObject onBench = Object.Instantiate(Prefab("Pickup_Ferramenta"), Vector3.down * 8f, Quaternion.identity);
        created.Add(onFloor); created.Add(onBench);
        yield return null;
        Call(onFloor.GetComponent(T("PickupInteractable")), "OnPlaced", new Vector3(-1f, 0f, 3.5f), 0f, null);
        Call(onBench.GetComponent(T("PickupInteractable")), "OnPlaced", new Vector3(2f, 0.75f, 3f), 0f, null);
        yield return Wait(0.6f);
        Assert.AreEqual(0, Occ("ItemLeftOnFloor"), "ainda dentro da tolerância (pode estar só reposicionando)");
        yield return Wait(1.2f);
        Assert.AreEqual(1, Occ("ItemLeftOnFloor"), "só o do chão; o da bancada não");
        Assert.AreEqual(1, Seen("ItemLeftOnFloor"), "o supervisor está olhando para lá");
        yield return Wait(2f);
        Assert.AreEqual(1, Occ("ItemLeftOnFloor"), "o mesmo item parado no chão não gera de novo");
        Assert.AreEqual(1, Seen("ItemLeftOnFloor"));
    }

    [UnityTest]
    public IEnumerator ItensDosMinigames_LargadosPeloJogador_Contam_OsQueJaComecaramNoChaoNao()
    {
        Set(monitor, "abandonDelay", 1f);
        Supervisor();
        var largados = new List<GameObject>();
        foreach (string p in new[] { "Pickup_Ssd", "Pickup_RamSodimm", "Pickup_Toner", "Pickup_ResmaPapel", "Pickup_Alicate", "Pickup_CaboRede" })
        {
            GameObject g = Object.Instantiate(Prefab(p), Vector3.down * 5f, Quaternion.identity);
            created.Add(g);
            largados.Add(g);
        }
        // cabo de cenário: começa no chão (posição inicial da cena), nunca foi mexido pelo jogador
        GameObject cenario = Object.Instantiate(Prefab("Pickup_CaboRede"), new Vector3(1.5f, 0.02f, 3f), Quaternion.identity);
        created.Add(cenario);
        // cabo posto pelo JOGO (ex.: crimpado com a mão ocupada, apoiado na caixa): também não é "largado"
        GameObject doJogo = Object.Instantiate(Prefab("Pickup_CaboRede"), Vector3.down * 9f, Quaternion.identity);
        created.Add(doJogo);
        yield return null;
        for (int i = 0; i < largados.Count; i++)
            Call(largados[i].GetComponent(T("PickupInteractable")), "OnPlaced", new Vector3(-1.5f + i * 0.6f, 0f, 3.5f), 0f, null);
        Call(doJogo.GetComponent(T("PickupInteractable")), "OnDropped", new Vector3(2.2f, 0f, 3f), 0f);
        yield return Wait(2.2f);
        Assert.AreEqual(largados.Count, Occ("ItemLeftOnFloor"), "peças do notebook, consumíveis da impressora, alicate e cabo largados");
        Assert.AreEqual(largados.Count, Seen("ItemLeftOnFloor"), "o supervisor está olhando para lá");

        // o cabo de cenário, depois de pego e largado pelo jogador, passa a contar
        Call(cenario.GetComponent(T("PickupInteractable")), "OnPlaced", new Vector3(1.5f, 0f, 3f), 0f, null);
        yield return Wait(1.6f);
        Assert.AreEqual(largados.Count + 1, Occ("ItemLeftOnFloor"));
    }

    [UnityTest]
    public IEnumerator PecasDoNotebook_QuebramNaPrimeiraQueda_ComOModeloQuebrado()
    {
        Supervisor();
        GameObject ssd = Drop("Pickup_Ssd", new Vector3(-0.5f, 0f, 4f));
        GameObject ram = Drop("Pickup_RamSodimm", new Vector3(0.5f, 0f, 4f));
        yield return Wait(2f);
        foreach (GameObject g in new[] { ssd, ram })
        {
            var peca = (IBreakable)g.GetComponent(T("NotebookPartItem"));
            Assert.IsTrue(peca.IsBroken, g.name + ": frágil, quebra na 1ª queda (estado da lógica)");
            Assert.IsTrue(g.transform.Find("Visual_Quebrado").gameObject.activeSelf, g.name + ": modelo quebrado");
            Assert.IsFalse(g.transform.Find("Visual").gameObject.activeSelf);
        }
        Assert.AreEqual(2, Occ("ComponentBroken"), "as duas quebras viram infração");
    }

    [UnityTest]
    public IEnumerator Equipamentos_GabineteRegistraAQueda_MonitorQuebraNaPrimeira()
    {
        Supervisor();
        GameObject gabinete = Drop("Gabinete", new Vector3(-1f, 0f, 4f));
        yield return Wait(2f);
        Assert.AreEqual(1, Occ("ComponentDropped"), "gabinete: 1ª queda registrada, ainda funciona");
        Assert.AreEqual(0, Occ("ComponentBroken"));
        Assert.IsFalse(gabinete.GetComponent<BreakableEquipment>().IsBroken);

        // monitor: BreakableEquipment + PartFallDamage (quebra na 1ª queda) com a camada de rachaduras
        GameObject mon = GameObject.CreatePrimitive(PrimitiveType.Cube);
        created.Add(mon);
        mon.transform.localScale = new Vector3(0.5f, 0.35f, 0.05f);
        mon.AddComponent(T("PickupInteractable"));
        var eq = mon.AddComponent<BreakableEquipment>();
        eq.GetType().GetField("displayName", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(eq, "monitor");
        var rachaduras = new GameObject("Visual_Quebrado");
        rachaduras.transform.SetParent(mon.transform, false);
        rachaduras.SetActive(false);
        var dano = mon.AddComponent<PartFallDamage>();
        Set(dano, "dropsToBreak", 1);
        Set(dano, "brokenVisual", rachaduras);
        mon.transform.position = new Vector3(1f, 1.2f, 4f);
        Call(mon.GetComponent(T("PickupInteractable")), "OnReleased", new Vector3(1f, 3f, 4f), null, (Vector3?)new Vector3(1f, 1.2f, 4f));
        yield return Wait(2f);
        Assert.IsTrue(eq.IsBroken, "monitor quebra na 1ª queda");
        Assert.IsTrue(rachaduras.activeSelf, "camada de rachaduras aparece por cima do modelo");
        Assert.IsTrue(mon.GetComponent<Renderer>().enabled, "o modelo oficial continua (só ganha as rachaduras)");
        Assert.AreEqual(1, Occ("ComponentBroken"));
        Assert.AreEqual(1, Seen("ComponentBroken"));
    }

    [UnityTest]
    public IEnumerator Reacao_InterrompeARonda_OlhaEReprova_EDepoisRetoma()
    {
        GameObject floor = created[0];
        var surface = floor.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Children;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.BuildNavMesh();
        Transform route = New("Rota").transform;
        foreach (Vector3 p in new[] { new Vector3(0f, 0f, 8f), new Vector3(0f, 0f, -8f) })
        { var c = new GameObject("P").transform; c.SetParent(route, false); c.position = p; }
        Transform player = New("Jogador").transform; player.position = new Vector3(6f, 0f, 0f);

        GameObject npc = New("Supervisor");
        npc.transform.position = new Vector3(0f, 0f, -8f);
        var agent = npc.AddComponent<NavMeshAgent>(); agent.speed = 3f;
        Component patrol = npc.AddComponent(T("SupervisorPatrol"));
        Set(patrol, "route", route);
        Set(patrol, "waitRange", new Vector2(0.2f, 0.3f));
        Set(patrol, "player", player);
        Set(patrol, "watchChance", 0f);
        Component perception = npc.AddComponent(T("SupervisorPerception"));
        yield return Wait(0.5f);
        Assert.AreEqual("Walking", Get(patrol, "State").ToString());

        Drop("Pickup_HD", npc.transform.position + npc.transform.forward * 3f);
        float t = 0f;
        while (t < 2f && Get(patrol, "State").ToString() != "Reacting") { t += Time.deltaTime; yield return null; }
        Assert.AreEqual("Reacting", Get(patrol, "State").ToString(), "interrompeu a ronda");
        Assert.IsTrue(agent.isStopped);
        Assert.AreEqual(1, (int)Get(patrol, "ReactionCount"));
        yield return Wait(4f);
        Assert.AreNotEqual("Reacting", Get(patrol, "State").ToString(), "terminou a reação");
        int arrivals = (int)Get(patrol, "Arrivals");
        yield return Wait(8f);
        Assert.Greater((int)Get(patrol, "Arrivals"), arrivals, "retomou a ronda");
        Assert.AreEqual(1, Seen("ComponentDropped"), "uma infração, um registro");
    }

    [UnityTest]
    public IEnumerator Aviso_JogadorDeCostas_MostraCartaoMensagemEIndicadorNaBorda()
    {
        Supervisor();
        GameObject camGo = New("CameraJogador");
        var cam = camGo.AddComponent<Camera>();
        camGo.transform.position = new Vector3(0f, 1.5f, 6f);
        camGo.transform.rotation = Quaternion.LookRotation(Vector3.forward);   // de costas para o supervisor (na origem)
        GameObject canvas = New("HUD");
        canvas.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        Component hud = canvas.AddComponent(T("SupervisorAlertHUD"));
        Set(hud, "playerCamera", cam);
        yield return null;
        Assert.IsFalse((bool)Get(hud, "IsShowing"));

        Drop("Pickup_HD", new Vector3(0f, 0f, 4f));
        float t = 0f;
        while (t < 2f && !(bool)Get(hud, "IsShowing")) { t += Time.deltaTime; yield return null; }
        yield return new WaitForSeconds(0.1f);   // depois do LateUpdate do HUD (a borda sobe em ~0,2 s)
        Assert.IsTrue((bool)Get(hud, "IsShowing"), "cartão de aviso");
        StringAssert.Contains("HD", (string)Get(hud, "CurrentMessage"), "diz o motivo e o item");
        Assert.IsTrue((bool)Get(hud, "IndicatorVisible"));
        Assert.IsTrue((bool)Get(hud, "IndicatorOffscreen"), "supervisor atrás: seta na borda da tela");
        var arrowRt = (RectTransform)canvas.transform.Find("AlertaSupervisor/Indicador");
        Assert.Less(arrowRt.anchoredPosition.y, 0f, "atrás: a seta fica na metade de baixo da tela");
        Assert.Greater((float)Get(hud, "VignetteAlpha"), 0f, "borda vermelha");
        Assert.AreEqual(Quaternion.LookRotation(Vector3.forward), camGo.transform.rotation, "a câmera não é girada");
        yield return Wait(5f);
        Assert.IsFalse((bool)Get(hud, "IsShowing"), "o cartão sai sozinho");
    }

    [UnityTest]
    public IEnumerator Simulacao_RondaComQuedasAleatorias_MedeProporcaoPercebida()
    {
        GameObject floor = created[0];
        var surface = floor.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Children;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.BuildNavMesh();
        Transform route = New("Rota").transform;
        foreach (Vector3 p in new[] { new Vector3(-7f, 0f, -7f), new Vector3(7f, 0f, -7f), new Vector3(7f, 0f, 7f), new Vector3(-7f, 0f, 7f) })
        { var c = new GameObject("P").transform; c.SetParent(route, false); c.position = p; }
        GameObject npc = New("Supervisor");
        npc.transform.position = new Vector3(-7f, 0f, 7f);
        npc.AddComponent<NavMeshAgent>().speed = 1.3f;
        Component patrol = npc.AddComponent(T("SupervisorPatrol"));
        Set(patrol, "route", route);
        Set(patrol, "player", New("Jogador").transform);
        Set(patrol, "watchChance", 0f);
        Component perception = npc.AddComponent(T("SupervisorPerception"));
        Set(perception, "react", false);

        var rng = new System.Random(1234);
        const int drops = 24;
        for (int i = 0; i < drops; i++)
        {
            Drop("Pickup_HD", new Vector3((float)rng.NextDouble() * 16f - 8f, 0f, (float)rng.NextDouble() * 16f - 8f));
            yield return Wait(1f);
        }
        yield return Wait(2f);
        int occ = Occ("ComponentDropped") + Occ("ComponentBroken");
        int seen = Seen("ComponentDropped") + Seen("ComponentBroken");
        int lost = Lost("ComponentDropped") + Lost("ComponentBroken");
        string summary = (string)Static("Summary");
        Debug.Log("[Infrações] simulação: " + summary);
        TestContext.WriteLine($"Simulação (sala 16 x 16 m, ronda nos cantos, quedas aleatórias): {seen}/{occ} percebidas ({100f * seen / Mathf.Max(1, occ):F0}%). {summary}");
        Assert.GreaterOrEqual(occ, drops, "toda queda vira ocorrência (uma peça caindo sobre outra também conta)");
        Assert.AreEqual(occ, seen + lost, "toda ocorrência tem desfecho (vista ou despercebida)");
        Assert.Greater(seen, 0, "o supervisor tem oportunidades reais");
        Assert.Greater(lost, 0, "mas não vê tudo");
    }
}
