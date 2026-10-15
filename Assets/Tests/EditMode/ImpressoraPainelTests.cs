using System;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Painel 3D da impressora (modelo do Matheus): o visor lê o MESMO estado da lógica do Ítalo (ImpressoraSession) e o
/// clique no painel pede a vista do teclado do IP. Tipos do Assembly-CSharp por reflexão (como nos outros testes).
/// </summary>
public class ImpressoraPainelTests
{
    private static Type T(string n) => Type.GetType(n + ", Assembly-CSharp", true);
    private static object Call(object o, string m, params object[] a) => o.GetType().GetMethod(m).Invoke(o, a);
    private static object Get(object o, string p) => o.GetType().GetProperty(p).GetValue(o);
    private static string Status(object sessao) => (string)T("ImpressoraPainel3D").GetMethod("Status").Invoke(null, new[] { sessao });
    private static string Ip(object sessao) => (string)T("ImpressoraPainel3D").GetMethod("LinhaIp").Invoke(null, new[] { sessao });
    private static object Parte(string n) => Enum.Parse(T("ImpressoraParte"), n);

    [Test]
    public void Visor_MostraOQueFalta_EOIpConfirmado()
    {
        object s = Activator.CreateInstance(T("ImpressoraSession"));
        Assert.AreEqual("SEM TONER", Status(s));
        Assert.AreEqual("IP ---.---.---.---", Ip(s));

        Call(s, "UsarEncaixe", Parte("Toner"), true, Parte("Toner"));
        Assert.AreEqual("SEM PAPEL", Status(s));
        Call(s, "UsarEncaixe", Parte("Papel"), true, Parte("Papel"));
        Assert.AreEqual("CONFIGURE O IP", Status(s));

        Call(s, "Digitar", "192.168.0.50");
        Assert.AreEqual("CONFIGURE O IP", Status(s), "digitado mas não confirmado ainda não vale");
        Assert.IsTrue((bool)Call(s, "ConfirmarIp"));
        Assert.AreEqual("PRONTA", Status(s));
        Assert.AreEqual("IP 192.168.0.50", Ip(s));
        Assert.IsTrue((bool)Get(s, "Pronta"));
    }

    [Test]
    public void PedidoDeVista_ValeUmaVez()
    {
        var go = new GameObject("Impressora");
        try
        {
            Component est = go.AddComponent(T("ImpressoraEstacao"));
            object painel = Enum.Parse(T("ImpressoraVista"), "Painel");
            var args = new object[] { null };
            Assert.IsFalse((bool)est.GetType().GetMethod("ConsumirVistaPedida").Invoke(est, args), "sem pedido");
            Call(est, "PedirVista", painel);
            Assert.IsTrue((bool)est.GetType().GetMethod("ConsumirVistaPedida").Invoke(est, args));
            Assert.AreEqual(painel, args[0]);
            Assert.IsFalse((bool)est.GetType().GetMethod("ConsumirVistaPedida").Invoke(est, args), "consumido");
        }
        finally { Object.DestroyImmediate(go); }
    }
}
