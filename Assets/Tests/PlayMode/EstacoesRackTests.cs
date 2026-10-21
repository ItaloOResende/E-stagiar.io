using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

/// <summary>
/// Office_Placeholder depois do setup "Chamados: estações, rack e rodízio": todas as estações recebem chamados
/// (11 PCs, 5 notebooks), cada PC tem a sua porta no rack (com portas reservas no switch, para os defeitos de porta
/// trocada e laço), o equipamento certo aceita cada chamado, e o nome do funcionário é o mesmo na StationEmployee, nas
/// duas faces da plaquinha e no chamado reservado. Tipos do Assembly-CSharp por reflexão (como nos outros testes).
/// </summary>
public class EstacoesRackTests
{
    private const string Cena = "Assets/Scenes/Office_Placeholder.unity";
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
    private static Type T(string n) => Type.GetType(n + ", Assembly-CSharp", true);
    private static Type C(string n) => Type.GetType(n + ", EstagiarIo.Computer", true);
    private static object Get(object o, string p) => o.GetType().GetProperty(p, Any).GetValue(o);
    private static object GetF(object o, string f) => o.GetType().GetField(f, Any).GetValue(o);

    private static IEnumerator Cena0()
    {
#if UNITY_EDITOR
        yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(Cena, new UnityEngine.SceneManagement.LoadSceneParameters());
#endif
        for (int i = 0; i < 4; i++) yield return null;
    }

    private static List<Component> Estacoes() =>
        Object.FindObjectsByType(T("StationEmployee"), FindObjectsSortMode.None).Cast<Component>().ToList();
    private static string Label(Component e) => (string)GetF(e, "stationLabel");
    private static Component PcDe(Component e) => e.GetComponentInChildren(C("ComputerCase"), true);
    private static Component NotebookDe(Component e) => e.GetComponentInChildren(T("NotebookEstacao"), true);

    // Ponto de rede (parede) da estação com vínculo ao rack.
    private static Component PontoRack(Component e) =>
        e.GetComponentsInChildren(T("PontoDeRedeRack"), true).FirstOrDefault();

    private static object Rede() => T("RedeGame").GetMethod("Garantir").Invoke(null, null);
    private static bool Funciona(object rede, int p) => (bool)rede.GetType().GetMethod("PortaFunciona").Invoke(rede, new object[] { p });
    private static void Desligar(object rede, int p) =>
        rede.GetType().GetMethods().First(m => m.Name == "Desligar" && m.GetParameters()[0].ParameterType == typeof(int))
            .Invoke(rede, new object[] { p, null });
    private static void PreLigar(object rede, int p) => rede.GetType().GetMethod("PreLigar").Invoke(rede, new object[] { p });

    private static IList Catalogo() => (IList)GetF(Object.FindAnyObjectByType(T("TicketManager")), "catalog");
    private static string Id(object def) => (string)def.GetType().GetField("ticketId").GetValue(def);
    private static readonly Dictionary<string, string> NpcDaEstacao = new Dictionary<string, string>
    {
        { "Estação 03", "npc 5" }, { "Estação 05", "npc" }, { "Estação 10", "npc4" }, { "Estação 16", "npc3" },
    };

    private static object Chamado(object def, int n) => Activator.CreateInstance(T("TicketInstance"), n, def, 300f);

    [UnityTest]
    public IEnumerator Equipamentos_TodasAsEstacoes_RecebemChamados()
    {
        yield return Cena0();
        int pcs = 0, nbs = 0;
        foreach (Component e in Estacoes())
        {
            Component pc = PcDe(e), nb = NotebookDe(e);
            Component bridge = pc != null ? pc.GetComponent(T("PcTicketBridge")) : nb != null ? nb.GetComponent(T("NotebookTicketBridge")) : null;
            Assert.IsNotNull(bridge, $"{Label(e)}: equipamento ligado aos chamados");
            StringAssert.Contains(Label(e), (string)Get(bridge, "DeviceLabel"), $"{Label(e)}: rótulo da própria estação");
            if (pc != null) pcs++; else nbs++;
        }
        Assert.AreEqual(11, pcs, "PCs de estação");
        Assert.AreEqual(5, nbs, "notebooks de estação");
    }

    [UnityTest]
    public IEnumerator Compatibilidade_CadaChamado_TemOsEquipamentosCertos()
    {
        yield return Cena0();
        Type bridge = T("TicketDeviceBridge");
        MethodInfo disp = bridge.GetMethod("Availability");
        int n = 1;
        foreach (object def in Catalogo())
        {
            string id = Id(def);
            int esperado = id.StartsWith("notebook-") ? 5 : id.StartsWith("impressora-") ? 1 : 11;
            object[] args = { Chamado(def, n++), 0, 0 };
            disp.Invoke(null, args);
            Assert.AreEqual(esperado, (int)args[1], $"{id}: equipamentos que aceitam");
            Assert.AreEqual(esperado, (int)args[2], $"{id}: livres no início");
        }
    }

    [UnityTest]
    public IEnumerator Rack_CadaPcComPortaPropria_EDefeitosReais()
    {
        yield return Cena0();
        object rede = Rede();
        int total = (int)Get(rede, "PortasDoRack");
        var portas = new List<int>();
        foreach (Component e in Estacoes().Where(e => PcDe(e) != null))
        {
            Component ponto = PontoRack(e);
            Assert.IsNotNull(ponto, $"{Label(e)}: ponto de rede ligado ao rack");
            int p = (int)Get(ponto, "PortaRack");
            Assert.That(p, Is.InRange(1, total), $"{Label(e)}: porta válida");
            portas.Add(p);

            // a porta é a desta estação: desligar o par derruba a rede do PC dela, e só dela
            Component power = PcDe(e).GetComponent(C("ComputerPower"));
            bool pcNoPonto = power.GetType().GetMethod("WallOf").Invoke(power, new[] { Enum.Parse(C("PcLink"), "Network") }) is Component w
                             && w.gameObject == ponto.gameObject;
            Assert.IsTrue(Funciona(rede, p), $"{Label(e)}: começa com rede");
            if (pcNoPonto) Assert.IsTrue((bool)Get(power, "NetworkConnected"), $"{Label(e)}: PC conectado");
            Desligar(rede, p);
            Assert.IsFalse(Funciona(rede, p), $"{Label(e)}: par desligado = sem rede");
            if (pcNoPonto) Assert.IsFalse((bool)Get(power, "NetworkConnected"), $"{Label(e)}: o PC perde a rede");
            PreLigar(rede, p);
            Assert.IsTrue(Funciona(rede, p), $"{Label(e)}: religado");
        }
        Assert.AreEqual(portas.Count, portas.Distinct().Count(), "uma porta por estação");
        Assert.GreaterOrEqual(total - portas.Count, 2, "portas reservas no switch");

        // defeito "porta trocada": o par vai para uma porta livre do switch e a estação fica sem rede
        int trocada = (int)rede.GetType().GetMethod("TrocarParaPortaErrada").Invoke(rede, new object[] { portas[0] });
        Assert.Greater(trocada, 0, "havia porta livre no switch");
        Assert.IsFalse(portas.Contains(trocada), "a porta trocada é uma reserva");
        Assert.IsFalse(Funciona(rede, portas[0]));
        Desligar(rede, portas[0]);
        PreLigar(rede, portas[0]);
        Assert.IsTrue(Funciona(rede, portas[0]));
    }

    [UnityTest]
    public IEnumerator Nomes_Funcionario_Plaquinha_EChamado_Iguais()
    {
        yield return Cena0();
        Type bridgeT = T("TicketDeviceBridge");
        MethodInfo claim = bridgeT.GetMethod("Claim", Any), release = bridgeT.GetMethod("Release", Any);
        IList catalogo = Catalogo();
        int n = 900;
        foreach (Component e in Estacoes())
        {
            string nome = (string)GetF(e, "employeeName");
            Assert.IsFalse(string.IsNullOrWhiteSpace(nome), $"{Label(e)}: estação com funcionário");
            Assert.IsFalse(string.IsNullOrWhiteSpace((string)GetF(e, "department")), $"{Label(e)}: setor");
            if (NpcDaEstacao.TryGetValue(Label(e), out string npc))
                Assert.AreEqual(npc, ((Transform)GetF(e.GetComponentInChildren(bridgeT, true), "stationNpc"))?.name, $"{Label(e)}: NPC sentado");
            Component placa = e.GetComponentInChildren(T("StationNamePlate"), true);
            Assert.IsNotNull(placa, $"{Label(e)}: plaquinha");
            foreach (TextMesh t in (TextMesh[])GetF(placa, "nameTexts")) Assert.AreEqual(nome, t.text, $"{Label(e)}: face da plaquinha");

            // o chamado reservado nesta estação é deste funcionário, no local desta estação
            Component bridge = e.GetComponentInChildren(bridgeT, true);
            var aceitos = (IList<string>)Get(bridge, "AcceptedTicketIds");
            object def = catalogo.Cast<object>().First(d => aceitos.Contains(Id(d)));
            object t0 = Chamado(def, n++);
            claim.Invoke(bridge, new[] { t0 });
            Assert.AreEqual(nome, Get(t0, "Requester"), $"{Label(e)}: solicitante na listagem");
            StringAssert.Contains(Label(e), (string)Get(t0, "Location"), $"{Label(e)}: local na listagem");
            release.Invoke(bridge, null);
        }
    }
}
