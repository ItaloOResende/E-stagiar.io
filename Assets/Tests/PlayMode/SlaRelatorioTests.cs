using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

/// <summary>
/// SLA dos chamados (referência, não expiração), penalidade de atraso no ScoreKeeper e relatório final da fase.
/// Tipos do Assembly-CSharp acessados por reflexão (como em PontuacaoTests).
/// </summary>
public class SlaRelatorioTests
{
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static Type T(string n) => Type.GetType(n + ", Assembly-CSharp", true);
    private static void Set(object o, string f, object v) => o.GetType().GetField(f, Any).SetValue(o, v);
    private static object Get(object o, string p) => o.GetType().GetProperty(p, Any).GetValue(o);
    private static object Call(object o, string m, params object[] a) => o.GetType().GetMethod(m, Any).Invoke(o, a);

    private readonly List<Object> spawned = new List<Object>();
    private Component tickets, score, stage;
    private ScriptableObject def;

    [SetUp]
    public void SetUp()
    {
        T("InfractionLog").GetMethod("ResetAll").Invoke(null, null);
        var go = new GameObject("Fase");
        go.SetActive(false);
        spawned.Add(go);
        tickets = go.AddComponent(T("TicketManager"));
        stage = go.AddComponent(T("StageController"));
        score = go.AddComponent(T("ScoreKeeper"));
        Set(stage, "ticketManager", tickets);
        Set(stage, "startAutomatically", false);
        Set(score, "ticketManager", tickets);
        Set(score, "stageController", stage);
        Set(score, "latePenalty", 7);
        Set(score, "latePenaltyPerMinute", 0);

        def = ScriptableObject.CreateInstance(T("TicketDefinition"));
        spawned.Add(def);
        Set(def, "ticketId", "teste");
        Set(def, "title", "Teste");
        Set(def, "slaSecondsOverride", 0.5f);
        object step = Activator.CreateInstance(T("TicketStepDefinition"));
        Set(step, "id", "resolver");
        ((IList)def.GetType().GetField("steps").GetValue(def)).Add(step);
        ((IList)tickets.GetType().GetField("catalog", Any).GetValue(tickets)).Add(def);
        go.SetActive(true);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (Object o in spawned) if (o != null) Object.DestroyImmediate(o);
        spawned.Clear();
        foreach (var r in Object.FindObjectsByType(T("StageReportScreen"), FindObjectsSortMode.None)) Object.DestroyImmediate(((Component)r).gameObject);
    }

    // Só com CAPTURAS_DIR definido: imagem do relatório para conferência visual (o Canvas é Overlay; aqui vai para uma câmera).
    private static void Capturar(Component report, string nome = "relatorio")
    {
        string dir = Environment.GetEnvironmentVariable("CAPTURAS_DIR");
        if (string.IsNullOrEmpty(dir)) return;
        var canvas = report.GetComponentInChildren<Canvas>();
        var camGo = new GameObject("CamCaptura");
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.45f, 0.55f, 0.6f);
        var rt = new RenderTexture(1920, 1080, 24);
        cam.targetTexture = rt;
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = 1f;
        Canvas.ForceUpdateCanvases();
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
        tex.Apply();
        System.IO.Directory.CreateDirectory(dir);
        System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, nome + ".png"), tex.EncodeToPNG());
        RenderTexture.active = null;
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        Object.DestroyImmediate(camGo);
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(tex);
    }

    [Test]
    public void Prazo_PorComplexidade_EOverride()
    {
        Set(def, "slaSecondsOverride", 0f);
        Set(def, "complexity", Enum.Parse(T("TicketComplexity"), "Simple"));
        float simples = (float)Call(tickets, "SlaSecondsFor", def);
        Set(def, "complexity", Enum.Parse(T("TicketComplexity"), "Complex"));
        float complexo = (float)Call(tickets, "SlaSecondsFor", def);
        Assert.Greater(complexo, simples, "reparo complexo tem mais tempo que tarefa simples");
        Set(def, "slaSecondsOverride", 42f);
        Assert.AreEqual(42f, (float)Call(tickets, "SlaSecondsFor", def), "prazo próprio do chamado vale mais");
    }

    [UnityTest]
    public IEnumerator Atrasado_ContinuaAberto_PodeIniciarEConcluir_EPenalizaUmaVez()
    {
        Call(stage, "StartStage");
        var list = (IList)Get(tickets, "Tickets");
        object ticket = list[0];
        yield return new WaitForSeconds(1.4f);   // prazo mínimo do TicketInstance: 1 s

        Assert.IsTrue((bool)Get(ticket, "IsOverdue"), "passou do prazo");
        Assert.IsFalse((bool)Get(ticket, "IsClosed"), "o prazo não fecha o chamado");
        Assert.AreEqual(-7, (int)Get(score, "Score"), "penalidade de atraso aplicada uma vez");
        yield return new WaitForSeconds(0.3f);
        Assert.AreEqual(-7, (int)Get(score, "Score"), "não repete");

        int number = (int)ticket.GetType().GetField("Number").GetValue(ticket);
        Assert.IsTrue((bool)Call(tickets, "StartTicket", number), "chamado atrasado pode ser iniciado");
        Assert.IsTrue((bool)Call(tickets, "CompleteStep", number, "resolver"), "e concluído");
        Assert.IsTrue((bool)Get(ticket, "CompletedLate"));
        Assert.Greater((int)Get(score, "Score"), -7, "concluir atrasado ainda rende o valor-base");

        Call(stage, "EndStageNow");
        yield return null;
        var report = ((Component)score).GetComponent(T("StageReportScreen"));
        Assert.IsNotNull(report, "relatório criado no objeto do ScoreKeeper");
        Assert.IsTrue((bool)Get(report, "IsOpen"), "relatório aberto no fim da fase");
        string resumo = (string)Get(report, "LastSummary");
        StringAssert.Contains("atrasos custaram 7 pts", resumo);
        // 2 chamados: o 1º concluído com atraso; o 2º (chegou depois) aberto = não concluído. Cada um numa só categoria.
        Assert.AreEqual("2|0|1|1", (string)Get(report, "LastCounts"), "recebidos|no prazo|com atraso|não concluídos");
        yield return new WaitForSeconds(1.5f);   // animação de entrada
        Capturar(report);

        Clicar(report, "VerExtrato");
        yield return new WaitForSeconds(0.4f);
        Assert.IsNotNull(Achar(report, "DetalheExtrato"), "extrato completo expandido");
        Assert.IsNotNull(Achar(report, "Lancamento1"), "cada lançamento listado");
        Capturar(report, "relatorio_extrato");
        Clicar(report, "Fundo", "DetalheExtrato");
        yield return new WaitForSeconds(0.4f);
        Assert.IsNull(Achar(report, "DetalheExtrato"), "recolhido");

        Clicar(report, "PorQue");
        yield return new WaitForSeconds(0.4f);
        Assert.IsNotNull(Achar(report, "DetalheEstrelas"), "explicação das estrelas expandida");
        Capturar(report, "relatorio_estrelas");
        Call(report, "Close");
    }

    private static Transform Achar(Component root, string name, string under = null)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name && (under == null || (t.parent != null && t.parent.name == under))) return t;
        return null;
    }

    private static void Clicar(Component root, string name, string under = null)
    {
        Transform t = Achar(root, name, under);
        Assert.IsNotNull(t, "botão " + name);
        t.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
    }

    [UnityTest]
    public IEnumerator FinalizarIncompleto_NaoConclui_PenalizaPorTentativa()
    {
        Set(def, "slaSecondsOverride", 999f);
        Call(stage, "StartStage");
        object ticket = ((IList)Get(tickets, "Tickets"))[0];
        int number = (int)ticket.GetType().GetField("Number").GetValue(ticket);
        Call(tickets, "StartTicket", number);
        Assert.IsFalse((bool)Call(tickets, "TryFinishTicket", number), "etapas pendentes: não finaliza");
        Assert.IsFalse((bool)Get(ticket, "IsClosed"));
        Assert.AreEqual(-5, (int)Get(score, "Score"), "uma penalidade por tentativa");
        yield return null;
        Assert.AreEqual(-5, (int)Get(score, "Score"), "não repete sozinha");
        Call(tickets, "TryFinishTicket", number);
        Assert.AreEqual(-10, (int)Get(score, "Score"), "nova tentativa, nova penalidade");
    }

    [UnityTest]
    public IEnumerator SemChamadosPendentesNemPorVir_EncerraAFase()
    {
        Set(def, "slaSecondsOverride", 999f);
        Set(stage, "durationMinutes", 1f);   // 60 s < 90 s de "parar de gerar": nenhum chamado novo depois do 1º
        Call(stage, "StartStage");
        object ticket = ((IList)Get(tickets, "Tickets"))[0];
        int number = (int)ticket.GetType().GetField("Number").GetValue(ticket);
        Call(tickets, "StartTicket", number);
        Call(tickets, "CompleteStep", number, "resolver");
        yield return null;
        yield return null;
        Assert.IsFalse((bool)Get(stage, "IsRunning"), "fase encerrada sem esperar o relógio");
        Assert.IsTrue((bool)Get(score, "StageFinished"), "pontuação final calculada");
        var report = ((Component)score).GetComponent(T("StageReportScreen"));
        Assert.IsTrue((bool)Get(report, "IsOpen"), "relatório aberto");
        Call(report, "Close");
    }
}
