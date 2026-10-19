using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

/// <summary>
/// Integração do gameplay na cena OFICIAL <c>Office_Placeholder</c> (menu "Escritório real: integração do gameplay"):
/// menu inicial → fase, um sistema de cada, cada tipo de chamado assumido por um equipamento REAL da cena (PC, notebook,
/// impressora, rack), um chamado de impressora resolvido de ponta a ponta com os objetos da cena, monitor do estoque
/// substituindo o de uma estação, tela do rack, relatório final e volta ao menu sem estado antigo.
/// Tipos do Assembly-CSharp e do EstagiarIo.Computer por reflexão (como nos outros testes).
/// </summary>
public class OfficeIntegracaoTests
{
    private const string Cena = "Assets/Scenes/Office_Placeholder.unity";
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
    private static Type T(string n) => Type.GetType(n + ", Assembly-CSharp", true);
    private static Type C(string n) => Type.GetType(n + ", EstagiarIo.Computer", true);
    private static object Get(object o, string p) => o.GetType().GetProperty(p, Any).GetValue(o);
    private static object GetF(object o, string f) => o.GetType().GetField(f, Any).GetValue(o);
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

    private static IEnumerator Carregar()
    {
#if UNITY_EDITOR
        yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(Cena, new UnityEngine.SceneManagement.LoadSceneParameters());
#endif
        for (int i = 0; i < 6; i++) yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        T("GameSession").GetProperty("Mode").SetValue(null, Enum.Parse(T("GameMode"), "Normal"));
    }


    // Sai da cena carregada: os testes seguintes montam cenas próprias e não podem herdar os sistemas do escritório.
    [UnityTearDown]
    public IEnumerator SairDaCena()
    {
        Time.timeScale = 1f;
        var vazia = UnityEngine.SceneManagement.SceneManager.CreateScene("Vazia_" + Guid.NewGuid().ToString("N").Substring(0, 6));
        UnityEngine.SceneManagement.SceneManager.SetActiveScene(vazia);
        for (int i = UnityEngine.SceneManagement.SceneManager.sceneCount - 1; i >= 0; i--)
        {
            var s = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
            if (s != vazia && s.isLoaded) yield return UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(s);
        }
    }

    private static Component Um(string tipo, Type t = null)
    {
        Object[] all = Object.FindObjectsByType(t ?? T(tipo), FindObjectsSortMode.None);
        Assert.AreEqual(1, all.Length, $"exatamente um {tipo} na cena");
        return (Component)all[0];
    }

    // ------------------------------------------------------------------ entrada do jogo e sistemas

    [UnityTest]
    public IEnumerator Cena_AbreNoMenuInicial_ComUmSistemaDeCada_EEscolhaComecaAFase()
    {
        yield return Carregar();
        Component stage = Um("StageController");
        Assert.IsTrue((bool)T("MainMenuScreen").GetProperty("Showing").GetValue(null), "a cena oficial abre no menu inicial");
        Assert.IsFalse((bool)Get(stage, "IsRunning"), "a fase só começa depois da escolha");
        foreach (string s in new[] { "TicketManager", "ScoreKeeper", "TicketTabletPrototype", "StageTimerHUD", "UIScreenManager",
                                     "PlayerMovement", "InteractionDetector", "ConductMonitor", "RedeGame", "SupervisorPatrol" })
            Um(s);
        Um("EventSystem", typeof(UnityEngine.EventSystems.EventSystem));
        Assert.IsTrue(UnityEngine.AI.NavMesh.SamplePosition(Um("PlayerMovement").transform.position, out _, 2f, UnityEngine.AI.NavMesh.AllAreas),
            "NavMesh assado sob o jogador");

        Call(stage.GetComponent(T("MainMenuScreen")), "Choose", Enum.Parse(T("GameMode"), "Normal"));
        yield return null;
        Assert.IsTrue((bool)Get(stage, "IsRunning"), "partida iniciada pelo menu");
        Assert.IsFalse((bool)T("MainMenuScreen").GetProperty("Showing").GetValue(null));
    }

    // ------------------------------------------------------------------ chamados nos equipamentos reais

    private static string escolheId;
    private static D Escolher<D>(List<D> lista, D anterior) where D : ScriptableObject
    {
        foreach (D d in lista) if ((string)d.GetType().GetField("ticketId").GetValue(d) == escolheId) return d;
        return null;
    }

    private static IEnumerator Iniciar(string id, Action<Component, object> pronto)
    {
        Component tm = Um("TicketManager");
        Component sc = tm.GetComponent(T("StageController"));
        Type td = T("TicketDefinition");
        Type funcT = typeof(Func<,,>).MakeGenericType(typeof(List<>).MakeGenericType(td), td, td);
        MethodInfo escolher = typeof(OfficeIntegracaoTests).GetMethod(nameof(Escolher), BindingFlags.NonPublic | BindingFlags.Static).MakeGenericMethod(td);
        escolheId = id;
        tm.GetType().GetProperty("SelectionOverride").SetValue(tm, Delegate.CreateDelegate(funcT, escolher));
        // pelo menu, como o jogador: o menu inicial aberto deixa o tempo parado (timeScale 0) e um StartStage direto
        // o mantinha aberto — o WaitForSeconds abaixo nunca terminava
        if ((bool)T("MainMenuScreen").GetProperty("Showing").GetValue(null))
            Call(sc.GetComponent(T("MainMenuScreen")), "Choose", Enum.Parse(T("GameMode"), "Normal"));
        else Call(sc, "StartStage");
        object ticket = ((IList)Get(tm, "Tickets"))[0];
        Assert.AreEqual(id, td.GetField("ticketId").GetValue(GetF(ticket, "Definition")), "chamado escolhido");
        Assert.IsTrue((bool)Call(tm, "StartTicket", (int)GetF(ticket, "Number")));
        for (int i = 0; i < 4; i++) yield return null;
        yield return new WaitForSeconds(0.4f);
        pronto(tm, ticket);
    }

    private static Component Dono(object ticket)
    {
        foreach (Object o in Object.FindObjectsByType(T("TicketDeviceBridge"), FindObjectsSortMode.None))
            if (Get(o, "Current") == ticket) return (Component)o;
        return null;
    }

    [UnityTest]
    public IEnumerator ChamadoDePC_AssumidoPorEstacaoDaCena_ComFuncionarioELocal()
    { yield return AssumidoPor("pc-sem-ram", "PcTicketBridge", true); }

    [UnityTest]
    public IEnumerator ChamadoDeNotebook_AssumidoPorNotebookDaCena()
    { yield return AssumidoPor("notebook-sem-ram", "NotebookTicketBridge", true); }

    [UnityTest]
    public IEnumerator ChamadoDeRack_AssumidoPorPcLigadoAoRack()
    { yield return AssumidoPor("pc-sem-rede-rack", "PcTicketBridge", true); }

    [UnityTest]
    public IEnumerator ChamadoDeImpressora_AssumidoPelaImpressoraDaArea()
    { yield return AssumidoPor("impressora-sem-toner", "ImpressoraTicketBridge", false); }

    private IEnumerator AssumidoPor(string id, string ponte, bool deEstacao)
    {
        yield return Carregar();
        Component tm = null; object ticket = null;
        yield return Iniciar(id, (m, t) => { tm = m; ticket = t; });
        Component dono = Dono(ticket);
        Assert.IsNotNull(dono, $"um equipamento da cena assumiu {id}");
        Assert.IsTrue(T(ponte).IsInstanceOfType(dono), $"{id} ficou com {dono.GetType().Name}, esperado {ponte}");
        Assert.IsFalse(string.IsNullOrEmpty((string)Get(ticket, "Location")), "o celular mostra o local do equipamento");
        if (deEstacao) Assert.IsNotNull(dono.GetComponentInParent(T("StationEmployee")), "equipamento de uma estação com funcionário");
        Assert.IsFalse((bool)Call(tm, "TryFinishTicket", (int)GetF(ticket, "Number")), "sem o reparo a entrega é recusada");
    }

    // ------------------------------------------------------------------ ponta a ponta: impressora sem papel

    [UnityTest]
    public IEnumerator Impressora_SemPapel_ResolvidaComResmaDaCena_EntregaPontuaERelatorio()
    {
        yield return Carregar();
        Component tm = null; object ticket = null;
        yield return Iniciar("impressora-sem-papel", (m, t) => { tm = m; ticket = t; });
        Component bridge = Dono(ticket);
        Assert.IsNotNull(bridge);
        Component estacao = bridge.GetComponent(T("ImpressoraEstacao"));
        Assert.IsFalse((bool)Get(Get(estacao, "Sessao"), "TemPapel"), "defeito aplicado: sem papel");
        int n = (int)GetF(ticket, "Number");

        // 1) inspeciona: abre a tela desta impressora
        Component player = Um("UIScreenManager");
        Component detector = player.GetComponent(T("InteractionDetector"));
        Component tela = (Component)GetF(bridge.GetComponent(T("UIScreenInteractable")), "screen");
        Assert.IsTrue((bool)Call(player, "Open", tela, bridge.gameObject, detector), "tela da impressora abre");
        yield return null;
        Call(player, "Close");
        yield return null;

        // 2) repõe o papel com uma resma REAL da área de impressão
        Component resma = null;
        foreach (Object o in Object.FindObjectsByType(T("ImpressoraPartItem"), FindObjectsSortMode.None))
            if (((Component)o).gameObject.name.StartsWith("Resma")) { resma = (Component)o; break; }
        Assert.IsNotNull(resma, "resma na cena");
        object papel = Enum.Parse(T("ImpressoraParte"), "Papel");
        object[] args = { papel, resma.GetComponent(T("PickupInteractable")), null };
        MethodInfo usar = T("ImpressoraEstacao").GetMethod("UsarEncaixe");
        usar.Invoke(estacao, args);
        Assert.IsTrue((bool)Get(Get(estacao, "Sessao"), "TemPapel"), "papel instalado");

        // 3) página de teste
        Assert.IsTrue((bool)Call(estacao, "ImprimirTeste"), "impressora pronta imprime");
        yield return new WaitForSeconds(0.6f);

        Component score = tm.GetComponent(T("ScoreKeeper"));
        int antes = (int)Get(score, "Score");
        Assert.IsTrue((bool)Call(tm, "TryFinishTicket", n), "entrega aceita depois do reparo real");
        Assert.Greater((int)Get(score, "Score"), antes, "pontuação atualizada");

        // fim da fase → relatório → menu inicial sem estado antigo
        Component stage = tm.GetComponent(T("StageController"));
        Call(stage, "EndStageNow");
        yield return null;
        Component report = score.GetComponent(T("StageReportScreen"));
        Assert.IsTrue((bool)Get(report, "IsOpen"), "relatório final aberto");
        T("GameSession").GetMethod("GoToMainMenu").Invoke(null, null);
        for (int i = 0; i < 10; i++) yield return null;
        Component tm2 = Um("TicketManager");
        Assert.AreNotSame(tm, tm2, "cena recarregada");
        Assert.AreEqual(0, ((IList)Get(tm2, "Tickets")).Count, "nenhum chamado da partida anterior");
        Assert.AreEqual(0, (int)Get(tm2.GetComponent(T("ScoreKeeper")), "Score"), "pontuação zerada");
        Assert.IsTrue((bool)T("MainMenuScreen").GetProperty("Showing").GetValue(null), "de volta ao menu inicial");
    }

    // ------------------------------------------------------------------ monitor do estoque

    [UnityTest]
    public IEnumerator MonitorDoEstoque_EhOModeloReal_EVira_OMonitorDeUmaEstacaoSemMonitor()
    {
        yield return Carregar();
        Type md = C("MonitorDisplay");
        Component estoque = null;
        var estoques = new List<Component>();
        foreach (Object o in Object.FindObjectsByType(md, FindObjectsSortMode.None))
            if ((bool)Get(o, "ConnectWhenPlaced")) estoques.Add((Component)o);
        Assert.GreaterOrEqual(estoques.Count, 4, "4 monitores reais na estante (+ os de reserva das bancadas)");
        estoque = estoques[0];
        Component item = estoque.GetComponentInParent(T("PickupInteractable"));
        Assert.IsNotNull(item, "pegável");
        Assert.IsNotNull(item.GetComponent<Collider>(), "com colisor");
        Assert.IsNotNull(item.GetComponent(C("BreakableEquipment")), "quebrável como o das estações");
        Assert.IsNotNull(item.GetComponent(T("MonitorAvulso")));
        Type port = C("PcPort");
        foreach (string p in new[] { "MonitorVideo", "MonitorHdmi", "MonitorPower" })
            Assert.IsNotNull(Call(estoque, "AnchorFor", Enum.Parse(port, p)), $"porta {p} na traseira");

        // estação A: o monitor dela vai embora (sem cabos) e o do estoque é colocado ao lado do gabinete
        Component power = null;
        foreach (Object o in Object.FindObjectsByType(C("ComputerPower"), FindObjectsSortMode.None))
            if (((Component)o).GetComponentInParent(T("StationEmployee")) != null && Get(o, "Monitor") != null) { power = (Component)o; break; }
        Assert.IsNotNull(power);
        // tira da traseira do monitor os cabos ligados (como o jogador, de mão vazia)
        MethodInfo usePort = C("ComputerPower").GetMethod("UsePort");
        foreach (string p in new[] { "MonitorPower", "MonitorVideo", "MonitorHdmi" })
            usePort.Invoke(power, new object[] { Enum.Parse(port, p), null, false, null });
        Assert.IsTrue((bool)md.GetMethod("CanTake").Invoke(null, new object[] { power }) == false, "monitor próprio ainda perto: não aceita outro");
        Transform antigo = (Transform)Get(power, "Monitor");
        Component antigoItem = antigo.GetComponentInParent(T("PickupInteractable"));
        Call(antigoItem, "OnDropped", antigoItem.transform.position + Vector3.right * 6f, 0f);
        Call(item, "OnDropped", power.transform.position + power.transform.right * 0.5f, power.transform.eulerAngles.y);
        yield return new WaitForSeconds(0.3f);
        Assert.AreSame(power, Call(estoque, "TryConnectToNearby"), "o monitor do estoque passou a ser o desta estação");
        Assert.AreSame(estoque.transform, Get(power, "Monitor"));
        Assert.IsNull(Get(antigo.GetComponent(md), "Source"), "o monitor antigo ficou avulso");
        Assert.IsTrue((bool)Get(power, "MonitorInRange"));
    }

    // ------------------------------------------------------------------ rack

    [UnityTest]
    public IEnumerator Rack_NoCarrinho_AbreATelaDoRackDaCena_EFechaSemTravarOJogador()
    {
        yield return Carregar();
        Component rack = null;
        foreach (Object o in Object.FindObjectsByType(T("RedeEstacaoInteractable"), FindObjectsSortMode.None))
        {
            var ui = ((Component)o).GetComponent(T("UIScreenInteractable"));
            var s = ui != null ? (Component)GetF(ui, "screen") : null;
            if (s != null && s.GetComponent(T("RedeRackScreen")) != null) rack = (Component)o;
        }
        Assert.IsNotNull(rack, "rack ligado à UI_Screen_Rack");
        Bounds rb = rack.GetComponent<Collider>().bounds;
        Assert.Greater(rb.min.y, 0.7f, "rack apoiado no carrinho (não no chão)");
        Assert.IsTrue(Physics.Raycast(rb.center + Vector3.down * 0.1f, Vector3.down, out RaycastHit h, 0.4f), "carrinho embaixo");
        Assert.IsFalse(h.collider.GetComponentInParent(T("PickupInteractable")), "o carrinho não é coletável");

        Component player = Um("UIScreenManager");
        Component tela = (Component)GetF(rack.GetComponent(T("UIScreenInteractable")), "screen");
        Assert.IsTrue((bool)Call(player, "Open", tela, rack.gameObject, player.GetComponent(T("InteractionDetector"))));
        yield return null;
        Assert.IsFalse(((Behaviour)player.GetComponent(T("PlayerLook"))).enabled, "câmera parada com a tela aberta");
        Call(player, "Close");
        yield return null;
        Assert.IsTrue(((Behaviour)player.GetComponent(T("PlayerLook"))).enabled, "câmera volta ao fechar");
        Assert.IsTrue(((Behaviour)player.GetComponent(T("PlayerMovement"))).enabled, "movimento volta ao fechar");
    }
}
