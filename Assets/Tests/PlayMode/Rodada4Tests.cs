using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

/// <summary>
/// Rodada 4 (2026-10-17): topologia livre do rack, estação nova (mesa vazia + entrega só montada), entrega com o
/// equipamento aberto (penalidade), Speed Run, menu inicial, selo da pontuação e limite de 1 estrela. Tipos do
/// Assembly-CSharp e do EstagiarIo.Rede por reflexão (como nos outros testes).
/// </summary>
public class Rodada4Tests
{
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
    private static Type T(string n) => Type.GetType(n + ", Assembly-CSharp", true);
    private static Type R(string n) => Type.GetType("EstagiarIo.Rede." + n + ", EstagiarIo.Rede", true);
    private static void Set(object o, string f, object v) => o.GetType().GetField(f, Any).SetValue(o, v);
    private static object Get(object o, string p) => o.GetType().GetProperty(p, Any).GetValue(o);
    private static object Call(object o, string m, params object[] a)
    {
        foreach (MethodInfo mi in o.GetType().GetMethods(Any))
        {
            ParameterInfo[] ps = mi.GetParameters();
            if (mi.Name != m || ps.Length != a.Length) continue;
            bool ok = true;
            for (int i = 0; i < ps.Length && ok; i++)
            {
                Type pt = ps[i].ParameterType.IsByRef ? ps[i].ParameterType.GetElementType() : ps[i].ParameterType;
                ok = a[i] == null ? !pt.IsValueType || ps[i].IsOut : pt.IsInstanceOfType(a[i]);
            }
            if (ok) return mi.Invoke(o, a);
        }
        throw new MissingMethodException(o.GetType().Name, m);
    }
    private static object Porta(bool patch, int n) => Activator.CreateInstance(R("PortaRack"), patch, n);

    private readonly List<Object> spawned = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        T("GameSession").GetProperty("Mode").SetValue(null, Enum.Parse(T("GameMode"), "Normal"));
        foreach (Object o in spawned) if (o != null) Object.Destroy(o);
        spawned.Clear();
    }

    private static IEnumerator Cena()
    {
#if UNITY_EDITOR
        yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
            "Assets/Scenes/Teste_Integrado.unity", new UnityEngine.SceneManagement.LoadSceneParameters());
#endif
        for (int i = 0; i < 6; i++) yield return null;
    }

    // ------------------------------------------------------------------ rack

    [UnityTest]
    public IEnumerator Rack_LigacaoTrocadaOuLaco_NaoDaRede_SoPNSNResolve()
    {
        yield return Cena();
        object g = T("RedeGame").GetMethod("Garantir").Invoke(null, null);
        // um PC da cena com rede pelo rack
        Component pc = null; int porta = 0;
        foreach (Object o in Object.FindObjectsByType(Type.GetType("ComputerPower, EstagiarIo.Computer", true), FindObjectsSortMode.None))
        {
            var c = (Component)o;
            object wall = Call(c, "WallOf", Enum.Parse(Type.GetType("PcLink, EstagiarIo.Computer", true), "Network"));
            int p = (int)T("PontoDeRedeRack").GetMethod("PortaDe").Invoke(null, new[] { wall });
            if (p > 0 && (bool)Get(c, "NetworkConnected")) { pc = c; porta = p; break; }
        }
        Assert.IsNotNull(pc, "um PC com rede pelo rack");

        int trocada = (int)Call(g, "TrocarParaPortaErrada", porta);
        Assert.Greater(trocada, 0, "havia porta livre no switch");
        Assert.IsFalse((bool)Call(g, "PortaFunciona", porta), "P N em S M: conexão física, mas sem rede");
        Assert.IsFalse((bool)Get(pc, "NetworkConnected"), "o PC da estação perde a rede de verdade");

        object topo = Get(g, "Topologia");
        Call(g, "Desligar", Porta(true, porta), null);
        Assert.IsNotNull(Call(g, "Ligar", Porta(true, porta), Porta(false, porta), null), "religa P N ↔ S N");
        Assert.IsTrue((bool)Get(pc, "NetworkConnected"));

        // laço S ↔ S em duas portas livres: a rede inteira cai
        var livres = new List<int>();
        for (int i = 1; i <= (int)Get(g, "PortasDoRack"); i++)
            if (!(bool)Call(topo, "Ocupada", Porta(false, i))) livres.Add(i);
        Assume.That(livres.Count >= 2, "duas portas livres no switch");
        Assert.IsNotNull(Call(g, "Ligar", Porta(false, livres[0]), Porta(false, livres[1]), null), "S com S é permitido");
        Assert.IsTrue((bool)Get(topo, "LacoNoSwitch"));
        Assert.IsFalse((bool)Get(pc, "NetworkConnected"), "laço derruba a rede");
        Call(g, "Desligar", Porta(false, livres[0]), null);
        Assert.IsTrue((bool)Get(pc, "NetworkConnected"));
    }

    // ------------------------------------------------------------------ chamados com equipamento

    private static IEnumerator IniciarChamado(string id, Action<Component, object> pronto)
    {
        Component tm = (Component)Object.FindAnyObjectByType(T("TicketManager"));
        Component sc = tm.GetComponent(T("StageController"));
        // SelectionOverride: Func<List<TicketDefinition>, TicketDefinition, TicketDefinition>
        Type td = T("TicketDefinition");
        Type funcT = typeof(Func<,,>).MakeGenericType(typeof(List<>).MakeGenericType(td), td, td);
        MethodInfo escolher = typeof(Rodada4Tests).GetMethod(nameof(Escolher), BindingFlags.NonPublic | BindingFlags.Static).MakeGenericMethod(td);
        escolheId = id;
        tm.GetType().GetProperty("SelectionOverride").SetValue(tm, Delegate.CreateDelegate(funcT, escolher));
        Call(sc, "StartStage");
        object ticket = ((IList)Get(tm, "Tickets"))[0];
        Assert.AreEqual(id, td.GetField("ticketId").GetValue(ticket.GetType().GetField("Definition").GetValue(ticket)), "chamado escolhido");
        Assert.IsTrue((bool)Call(tm, "StartTicket", (int)ticket.GetType().GetField("Number").GetValue(ticket)));
        for (int i = 0; i < 4; i++) yield return null;
        yield return new WaitForSeconds(0.4f);
        pronto(tm, ticket);
    }

    private static string escolheId;
    private static D Escolher<D>(List<D> lista, D anterior) where D : ScriptableObject
    {
        foreach (D d in lista) if ((string)d.GetType().GetField("ticketId").GetValue(d) == escolheId) return d;
        return null;
    }

    private static Component Dono(object ticket)
    {
        foreach (Object o in Object.FindObjectsByType(T("PcTicketBridge"), FindObjectsSortMode.None))
            if (Get(o, "Current") == ticket) return (Component)o;
        return null;
    }

    [UnityTest]
    public IEnumerator NovaEstacao_MesaVazia_EquipamentosNaSalaDeTI_SoEntregaMontadaNaMesa()
    {
        yield return Cena();
        Assert.IsNotNull(Object.FindAnyObjectByType(T("AssemblyPickupPoint")), "ponto de retirada na bancada (menu Rodada 4)");
        // posições originais na mesa de cada PC (para "trazer de volta" depois)
        var antes = new Dictionary<Component, (Vector3, Vector3)>();
        foreach (Object o in Object.FindObjectsByType(T("PcTicketBridge"), FindObjectsSortMode.None))
        {
            var b = (Component)o;
            Transform mon = (Transform)Get(b.GetComponent(Type.GetType("ComputerPower, EstagiarIo.Computer")), "Monitor");
            antes[b] = (b.transform.position, mon != null ? mon.GetComponentInParent(T("PickupInteractable")).transform.position : Vector3.zero);
        }
        Component tm = null; object ticket = null;
        yield return IniciarChamado("pc-montagem-completa", (m, t) => { tm = m; ticket = t; });
        Component pc = Dono(ticket);
        Assert.IsNotNull(pc, "um PC assumiu o chamado");
        Component ponto = (Component)Object.FindAnyObjectByType(T("AssemblyPickupPoint"));
        Assert.Less(Vector3.Distance(pc.transform.position, ponto.transform.position), 2f, "o gabinete foi para a sala de TI");
        Component power = pc.GetComponent(Type.GetType("ComputerPower, EstagiarIo.Computer"));
        Component monitor = ((Transform)Get(power, "Monitor")).GetComponentInParent(T("PickupInteractable"));
        Assert.Less(Vector3.Distance(monitor.transform.position, ponto.transform.position), 2.5f, "o monitor também");
        Assert.IsFalse((bool)Call(ticket, "HasCompleted", "posicionar-equipamentos"), "mesa vazia: etapa não vale");
        Assert.IsFalse((bool)Call(tm, "TryFinishTicket", (int)ticket.GetType().GetField("Number").GetValue(ticket)));

        // o jogador traz os dois de volta para a mesa da estação
        (Vector3 casePos, Vector3 monPos) = antes[pc];
        Call(pc.GetComponent(T("PickupInteractable")), "OnDropped", casePos, pc.transform.eulerAngles.y);
        Call(monitor, "OnDropped", monPos, monitor.transform.eulerAngles.y);
        yield return new WaitForSeconds(1.2f);
        Assert.IsTrue((bool)Call(ticket, "HasCompleted", "posicionar-equipamentos"), "gabinete e monitor na mesa da estação");
    }

    [UnityTest]
    public IEnumerator Entrega_ComGabineteAberto_NaoConclui_EPerdePontos()
    {
        yield return Cena();
        Component tm = null; object ticket = null;
        yield return IniciarChamado("pc-sem-ram", (m, t) => { tm = m; ticket = t; });
        Component pc = Dono(ticket);
        Assert.IsNotNull(pc);
        var pcCase = pc.GetComponent(Type.GetType("ComputerCase, EstagiarIo.Computer"));
        Type tool = Type.GetType("ToolType, EstagiarIo.Computer", true);
        object chave = Enum.Parse(tool, "Screwdriver");
        var screws = (IList)Get(pcCase, "Screws");
        for (int i = 0; i < screws.Count; i++) Call(pcCase, "TryRemoveScrew", i, chave);
        Call(pcCase, "TryOpen");
        Assert.IsTrue((bool)Get(pcCase, "IsIncomplete"), "gabinete aberto");
        int n = (int)ticket.GetType().GetField("Number").GetValue(ticket);
        Type td = T("TicketDefinition");
        foreach (object step in (IList)td.GetField("steps").GetValue(ticket.GetType().GetField("Definition").GetValue(ticket)))
            Call(tm, "CompleteStep", n, (string)step.GetType().GetField("id").GetValue(step));
        Assert.IsTrue((bool)Get(ticket, "ReadyToFinish"));

        Component score = tm.GetComponent(T("ScoreKeeper"));
        int antes = (int)Get(score, "Score");
        Assert.IsFalse((bool)Call(tm, "TryFinishTicket", n), "com o gabinete aberto não entrega");
        StringAssert.Contains("gabinete", (string)Get(ticket, "LastRejectReason"));
        Assert.Less((int)Get(score, "Score"), antes, "a tentativa custa pontos");
        bool lancado = false;
        foreach (object e in (IList)Get(score, "Entries"))
            if (((string)Get(e, "Reason")).StartsWith("Entrega com o equipamento aberto")) lancado = true;
        Assert.IsTrue(lancado, "lançamento no extrato oficial");
        Assert.AreEqual(Enum.Parse(T("TicketStatus"), "InProgress"), Get(ticket, "Status"));
    }

    // ------------------------------------------------------------------ Speed Run, menu, HUD, estrelas (isolados)

    private Component NovaFase(out Component stage, out Component score, int chamadosNoCatalogo = 1)
    {
        T("InfractionLog").GetMethod("ResetAll").Invoke(null, null);
        var go = new GameObject("Fase");
        go.SetActive(false);
        spawned.Add(go);
        Component tickets = go.AddComponent(T("TicketManager"));
        stage = go.AddComponent(T("StageController"));
        score = go.AddComponent(T("ScoreKeeper"));
        Set(stage, "ticketManager", tickets);
        Set(stage, "startAutomatically", false);
        Set(score, "ticketManager", tickets);
        Set(score, "stageController", stage);
        for (int k = 0; k < chamadosNoCatalogo; k++)
        {
            var def = ScriptableObject.CreateInstance(T("TicketDefinition"));
            spawned.Add(def);
            Set(def, "ticketId", "teste" + k);
            Set(def, "title", "Teste " + k);
            object step = Activator.CreateInstance(T("TicketStepDefinition"));
            Set(step, "id", "resolver");
            ((IList)def.GetType().GetField("steps").GetValue(def)).Add(step);
            ((IList)tickets.GetType().GetField("catalog", Any).GetValue(tickets)).Add(def);
        }
        go.SetActive(true);
        return tickets;
    }

    [UnityTest]
    public IEnumerator MenuInicial_AbreAntesDaFase_EExpedienteComeca()
    {
        T("GameSession").GetField("menuRequested", Any).SetValue(null, true);
        Component tm = NovaFase(out Component stage, out _);
        yield return null;   // Start do StageController
        Assert.IsTrue((bool)T("MainMenuScreen").GetProperty("Showing").GetValue(null), "menu inicial na tela");
        Assert.IsFalse((bool)Get(stage, "IsRunning"), "a fase ainda não começou");
        Component menu = stage.GetComponent(T("MainMenuScreen"));
        Call(menu, "Choose", Enum.Parse(T("GameMode"), "Normal"));
        Assert.IsFalse((bool)T("MainMenuScreen").GetProperty("Showing").GetValue(null));
        Assert.IsTrue((bool)Get(stage, "IsRunning"));
    }

    [UnityTest]
    public IEnumerator SeloDaPontuacao_LeOScoreKeeper_E1EstrelaEm20()
    {
        Component tm = NovaFase(out Component stage, out Component score);
        Set(tm, "requirePlayerToFinish", false);
        var limites = (IList)Get(score, "StarThresholds");
        Assert.AreEqual(20f, (float)limites[0], "1 estrela a partir de 20%");
        Component hud = score.GetComponent(T("ScoreHUD"));
        Assert.IsNotNull(hud, "ScoreHUD criado pelo ScoreKeeper");
        Call(stage, "StartStage");
        object t = ((IList)Get(tm, "Tickets"))[0];
        int n = (int)t.GetType().GetField("Number").GetValue(t);
        Call(tm, "StartTicket", n);
        Call(tm, "CompleteStep", n, "resolver");
        yield return null;
        int pontos = (int)Get(score, "Score");
        Assert.Greater(pontos, 0);
        Assert.AreEqual($"PONTOS   {pontos}", (string)Get(hud, "Text"), "mesma fonte do relatório");
    }
}
