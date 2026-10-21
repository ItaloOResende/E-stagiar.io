using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Rodízio de chamados (<c>TicketCategoryRotation</c>) com subgrupos: Rede passa por crimpagem, rack e cabo defeituoso
/// sem repetir antes de completar os três (e nunca o mesmo duas vezes seguidas); PC alterna peças e cabos; os grupos
/// continuam saindo um de cada por ciclo. Sem cena: chama o seletor direto. Tipos do Assembly-CSharp por reflexão.
/// </summary>
public class RodizioChamadosTests
{
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static Type T(string n) => Type.GetType(n + ", Assembly-CSharp", true);

    private static readonly string[] Crimp = { "crimpar-cabo" }, Rack = { "pc-sem-rede-rack" }, Cabo = { "pc-cabo-rede-defeituoso" };
    private static readonly string[] Pecas = { "pc-sem-ram", "pc-sem-hd", "pc-pecas-ausentes", "pc-montagem-completa", "pc-sem-sistema" };
    private static readonly string[] Cabos = { "pc-sem-energia", "pc-sem-rede", "monitor-sem-imagem", "pc-sem-video-e-rede" };
    private static readonly string[] Notebook = { "notebook-sem-ram", "notebook-sem-ssd", "notebook-sem-sistema", "notebook-sem-boot" };
    private static readonly string[] Impressora = { "impressora-sem-papel", "impressora-sem-toner", "impressora-sem-ip", "impressora-sem-impressao" };

    private GameObject go;
    private readonly List<Object> defs = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        if (go != null) Object.DestroyImmediate(go);
        foreach (Object d in defs) Object.DestroyImmediate(d);
        defs.Clear();
    }

    private object NovoGrupo(string tipo, string nome, string[] ids)
    {
        object g = Activator.CreateInstance(T("TicketCategoryRotation+" + tipo));
        g.GetType().GetField("name").SetValue(g, nome);
        g.GetType().GetField("ticketIds").SetValue(g, ids.ToList());
        return g;
    }

    private object Grupo(string nome, string[] ids, params (string, string[])[] subs)
    {
        object g = NovoGrupo("Group", nome, ids);
        IList lista = (IList)g.GetType().GetField("subgroups").GetValue(g);
        foreach ((string n, string[] s) in subs) lista.Add(NovoGrupo("Subgroup", n, s));
        return g;
    }

    // Monta o rodízio da Office (4 grupos, Rede e PC com subgrupos) e devolve o seletor: (elegíveis, anterior) → escolhido.
    private Func<object, object> Seletor(out IList elegiveis)
    {
        go = new GameObject("Chamados");
        go.AddComponent(T("TicketManager"));
        Component rot = go.AddComponent(T("TicketCategoryRotation"));
        Type tg = T("TicketCategoryRotation+Group");
        IList grupos = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(tg));
        grupos.Add(Grupo("PC", Pecas.Concat(Cabos).ToArray(), ("Peças", Pecas), ("Cabos", Cabos)));
        grupos.Add(Grupo("Notebook", Notebook));
        grupos.Add(Grupo("Impressora", Impressora));
        grupos.Add(Grupo("Rede", Crimp.Concat(Rack).Concat(Cabo).ToArray(), ("Crimpagem", Crimp), ("Rack", Rack), ("Cabo defeituoso", Cabo)));
        rot.GetType().GetField("groups", Any).SetValue(rot, grupos);

        Type td = T("TicketDefinition");
        elegiveis = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(td));
        foreach (string id in Pecas.Concat(Cabos).Concat(Notebook).Concat(Impressora).Concat(Crimp).Concat(Rack).Concat(Cabo))
        {
            ScriptableObject d = ScriptableObject.CreateInstance(td);
            td.GetField("ticketId").SetValue(d, id);
            defs.Add(d);
            elegiveis.Add(d);
        }
        MethodInfo choose = rot.GetType().GetMethod("Choose", Any);
        IList el = elegiveis;
        return anterior => choose.Invoke(rot, new[] { el, anterior });
    }

    private static string Id(object def) => (string)def.GetType().GetField("ticketId").GetValue(def);

    private List<string> Sortear(int n)
    {
        Func<object, object> escolher = Seletor(out _);
        var ids = new List<string>();
        object anterior = null;
        for (int i = 0; i < n; i++) { anterior = escolher(anterior); ids.Add(Id(anterior)); }
        return ids;
    }

    private static string TipoRede(string id) => Crimp.Contains(id) ? "crimp" : Rack.Contains(id) ? "rack" : Cabo.Contains(id) ? "cabo" : null;
    private static string TipoPc(string id) => Pecas.Contains(id) ? "pecas" : Cabos.Contains(id) ? "cabos" : null;

    [Test]
    public void Rede_PassaPelosTresTipos_SemRepetirAntesDeCompletar()
    {
        for (int rodada = 0; rodada < 30; rodada++)
        {
            List<string> rede = Sortear(48).Select(TipoRede).Where(t => t != null).ToList();
            Assert.GreaterOrEqual(rede.Count, 9);
            for (int i = 0; i + 3 <= rede.Count; i += 3)
                CollectionAssert.AreEquivalent(new[] { "crimp", "rack", "cabo" }, rede.GetRange(i, 3), $"bloco {i / 3}: {string.Join(",", rede)}");
            for (int i = 1; i < rede.Count; i++) Assert.AreNotEqual(rede[i - 1], rede[i], "o mesmo tipo de rede duas vezes seguidas");
            TearDown();
        }
    }

    [Test]
    public void Pc_AlternaPecasECabos()
    {
        for (int rodada = 0; rodada < 30; rodada++)
        {
            List<string> pc = Sortear(40).Select(TipoPc).Where(t => t != null).ToList();
            Assert.GreaterOrEqual(pc.Count, 8);
            for (int i = 1; i < pc.Count; i++) Assert.AreNotEqual(pc[i - 1], pc[i], $"PC sem alternar: {string.Join(",", pc)}");
            TearDown();
        }
    }

    [Test]
    public void Grupos_UmDeCadaPorCiclo_PesosPreservados()
    {
        List<string> ids = Sortear(40);
        for (int c = 0; c < 10; c++)
        {
            List<string> ciclo = ids.GetRange(c * 4, 4);
            Assert.AreEqual(1, ciclo.Count(i => TipoPc(i) != null), "um PC por ciclo");
            Assert.AreEqual(1, ciclo.Count(i => Notebook.Contains(i)), "um notebook por ciclo");
            Assert.AreEqual(1, ciclo.Count(i => Impressora.Contains(i)), "uma impressora por ciclo");
            Assert.AreEqual(1, ciclo.Count(i => TipoRede(i) != null), "uma rede por ciclo");
        }
    }

    [Test]
    public void Subgrupo_SemElegivel_ESaltado()
    {
        Func<object, object> escolher = Seletor(out IList elegiveis);
        // fase sem o chamado do rack (ex.: minimumStage maior): a Rede alterna só entre os outros dois
        for (int i = elegiveis.Count - 1; i >= 0; i--) if (Rack.Contains(Id(elegiveis[i]))) elegiveis.RemoveAt(i);
        var rede = new List<string>();
        object anterior = null;
        for (int i = 0; i < 40; i++) { anterior = escolher(anterior); if (TipoRede(Id(anterior)) is string t) rede.Add(t); }
        CollectionAssert.DoesNotContain(rede, "rack");
        CollectionAssert.IsSupersetOf(rede, new[] { "crimp", "cabo" });
    }
}
