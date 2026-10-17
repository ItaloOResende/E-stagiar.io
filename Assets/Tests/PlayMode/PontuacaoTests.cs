using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

/// <summary>
/// Pontuação (ScoreKeeper): só escuta chamados concluídos e infrações presenciadas; valores vêm do Inspector.
/// Tempos do atendimento no TicketInstance. Tipos do Assembly-CSharp acessados por reflexão.
/// </summary>
public class PontuacaoTests
{
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static Type T(string n) => Type.GetType(n + ", Assembly-CSharp", true);
    private static void Set(object o, string f, object v) => o.GetType().GetField(f, Any).SetValue(o, v);
    private static object Get(object o, string p) => o.GetType().GetProperty(p).GetValue(o);
    private static object Call(object o, string m, params object[] a) => o.GetType().GetMethod(m).Invoke(o, a);
    private static object Static(string type, string m, params object[] a) => T(type).GetMethod(m).Invoke(null, a);

    private readonly List<Object> spawned = new List<Object>();
    private Component tickets, score;

    [SetUp]
    public void SetUp()
    {
        Static("InfractionLog", "ResetAll");
        var go = new GameObject("Fase");
        go.SetActive(false);                                   // configura antes do OnEnable
        spawned.Add(go);
        tickets = go.AddComponent(T("TicketManager"));
        score = go.AddComponent(T("ScoreKeeper"));
        Set(score, "ticketManager", tickets);

        ScriptableObject def = ScriptableObject.CreateInstance(T("TicketDefinition"));
        spawned.Add(def);
        Set(def, "ticketId", "teste");
        object step = Activator.CreateInstance(T("TicketStepDefinition"));
        Set(step, "id", "resolver");
        var steps = (IList)def.GetType().GetField("steps").GetValue(def);
        steps.Add(step);
        var catalog = (IList)tickets.GetType().GetField("catalog", Any).GetValue(tickets);
        catalog.Add(def);
        go.SetActive(true);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (Object o in spawned) if (o != null) Object.DestroyImmediate(o);
        spawned.Clear();
        Static("InfractionLog", "ResetAll");
    }

    private void CompleteFirstTicket()
    {
        Call(tickets, "BeginStage", 1, 900f);
        Assert.IsTrue((bool)Call(tickets, "StartTicket", 1));
        Assert.IsTrue((bool)Call(tickets, "CompleteStep", 1, "resolver"));
    }

    [UnityTest]
    public IEnumerator ValoresPadrao_SaoZero_SemEstrelas()
    {
        yield return null;
        CompleteFirstTicket();
        Assert.AreEqual(0, Get(score, "Score"), "nenhum valor foi definido ainda");
        Assert.AreEqual(0, Get(score, "Stars"));
        Assert.AreEqual(0, Get(score, "MaxStars"));
        Assert.AreEqual(1, ((ICollection)Get(score, "Entries")).Count, "o lançamento é registrado mesmo valendo 0");
    }

    [UnityTest]
    public IEnumerator ChamadoConcluido_SomaPontos_ERegistraTempos()
    {
        Set(score, "ticketCompletedPoints", 100);
        Set(score, "starThresholds", new List<int> { 50, 100, 150 });
        yield return null;
        CompleteFirstTicket();
        Assert.AreEqual(100, Get(score, "Score"));
        Assert.AreEqual(2, Get(score, "Stars"));
        Assert.AreEqual(3, Get(score, "MaxStars"));

        object ticket = Call(tickets, "Find", 1);
        Assert.GreaterOrEqual((float)Get(ticket, "StartedAt"), 0f);
        Assert.GreaterOrEqual((float)Get(ticket, "CompletedAt"), (float)Get(ticket, "StartedAt"));
        Assert.GreaterOrEqual((float)Get(ticket, "ServiceSeconds"), 0f);
    }

    [UnityTest]
    public IEnumerator SoInfracaoPresenciada_Penaliza_PeloValorDoTipo()
    {
        object penalty = Activator.CreateInstance(T("ScoreKeeper+InfractionPenalty"));
        Set(penalty, "kind", Enum.Parse(T("InfractionKind"), "ComponentBroken"));
        Set(penalty, "penalty", 30);
        var list = (IList)score.GetType().GetField("infractionPenalties", Any).GetValue(score);
        list.Add(penalty);
        yield return null;

        object kind = Enum.Parse(T("InfractionKind"), "ComponentBroken");
        object missed = Static("InfractionLog", "Report", kind, null, "RAM", Vector3.zero);
        Static("InfractionLog", "MarkMissed", missed);
        Assert.AreEqual(0, Get(score, "Score"), "despercebida: sem penalidade");

        object seen = Static("InfractionLog", "Report", kind, null, "RAM", Vector3.zero);
        Static("InfractionLog", "MarkWitnessed", seen, null);
        Assert.AreEqual(-30, Get(score, "Score"));

        object other = Static("InfractionLog", "Report", Enum.Parse(T("InfractionKind"), "ItemLeftOnFloor"), null, "cabo", Vector3.zero);
        Static("InfractionLog", "MarkWitnessed", other, null);
        Assert.AreEqual(-30, Get(score, "Score"), "tipo sem penalidade configurada não muda a pontuação");
    }

    [UnityTest]
    public IEnumerator FimDaFase_CongelaPontuacaoEEstrelas_InicioZera()
    {
        Set(score, "ticketCompletedPoints", 100);
        Set(score, "starThresholds", new List<int> { 50, 150 });
        GameObject go = score.gameObject;
        go.SetActive(false);
        Component stage = go.AddComponent(T("StageController"));
        Set(stage, "ticketManager", tickets);
        Set(stage, "startAutomatically", false);
        Set(score, "stageController", stage);
        go.SetActive(true);
        yield return null;

        Call(stage, "StartStage");
        Assert.IsTrue((bool)Call(tickets, "StartTicket", 1));
        Assert.IsTrue((bool)Call(tickets, "CompleteStep", 1, "resolver"));
        Assert.AreEqual(100, Get(score, "Score"));

        int scoredStars = -1;
        score.GetType().GetEvent("StageScored").AddEventHandler(score, new Action<int, int>((p, st) => scoredStars = st));
        Call(score, "FinishStage");
        Assert.IsTrue((bool)Get(score, "StageFinished"));
        Assert.AreEqual(100, Get(score, "FinalScore"));
        Assert.AreEqual(1, Get(score, "FinalStars"), "100 alcança o limite de 50, não o de 150");
        Assert.AreEqual(1, scoredStars);

        object kind = Enum.Parse(T("InfractionKind"), "ComponentBroken");
        object penalty = Activator.CreateInstance(T("ScoreKeeper+InfractionPenalty"));
        Set(penalty, "kind", kind); Set(penalty, "penalty", 30);
        ((IList)score.GetType().GetField("infractionPenalties", Any).GetValue(score)).Add(penalty);
        Static("InfractionLog", "MarkWitnessed", Static("InfractionLog", "Report", kind, null, "RAM", Vector3.zero), null);
        Assert.AreEqual(100, Get(score, "Score"), "depois do fim da fase o resultado não muda");

        Call(stage, "StartStage");
        Assert.AreEqual(0, Get(score, "Score"), "nova fase: zera");
        Assert.IsFalse((bool)Get(score, "StageFinished"));
    }

    [UnityTest]
    public IEnumerator Ponte_ConcluirComGabineteSemParafusos_Penaliza_Completo_NaoPenaliza()
    {
        Set(score, "incompleteCasePenalty", 20);
        Type db = Type.GetType("UnityEditor.AssetDatabase, UnityEditor", true);
        var prefab = (GameObject)db.GetMethod("LoadAssetAtPath", new[] { typeof(string), typeof(Type) })
            .Invoke(null, new object[] { "Assets/Prefabs/Gameplay/Gabinete.prefab", typeof(GameObject) });
        GameObject pc = Object.Instantiate(prefab, new Vector3(0f, -50f, 0f), Quaternion.identity);
        spawned.Add(pc);
        pc.SetActive(false);
        Component bridge = pc.AddComponent(T("ComputerCaseTicketBridge"));
        Set(bridge, "computerCase", pc.GetComponent<ComputerCase>());
        Set(bridge, "ticketManager", tickets);
        Set(bridge, "ticketId", "teste");
        Set(bridge, "stepId", "abrir");
        Set(bridge, "scoreKeeper", score);
        pc.SetActive(true);
        yield return null;

        var pcCase = pc.GetComponent<ComputerCase>();
        Assert.IsFalse(pcCase.IsIncomplete);
        CompleteFirstTicket();
        Assert.AreEqual(0, Get(score, "Score"), "completo: sem penalidade");

        pcCase.RequiresWorkSurface = false;                         // teste sem mesa
        for (int i = 0; i < pcCase.TotalCount; i++) pcCase.TryRemoveScrew(i, ToolType.Screwdriver);
        Assert.IsTrue(pcCase.IsIncomplete);
        CompleteFirstTicket();
        Assert.AreEqual(-20, Get(score, "Score"), "sem os parafusos obrigatórios: penalidade configurada");
    }

    [UnityTest]
    public IEnumerator GabineteIncompleto_AplicaPenalidadeConfigurada()
    {
        Set(score, "incompleteCasePenalty", 20);
        yield return null;
        CompleteFirstTicket();
        Call(score, "ApplyIncompleteCasePenalty", Call(tickets, "Find", 1));
        Assert.AreEqual(-20, Get(score, "Score"));
        Call(score, "ResetScore");
        Assert.AreEqual(0, Get(score, "Score"));
    }
}
