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
    public void Visor_AvisoNeutro_ProntaSoComIpEMascara()
    {
        object s = Activator.CreateInstance(T("ImpressoraSession"));
        Assert.AreEqual("ERRO - VERIFICAR", Status(s), "o visor não diz o que falta");
        Assert.AreEqual("IP ---.---.---.---", Ip(s));

        Call(s, "UsarEncaixe", Parte("Toner"), true, Parte("Toner"));
        Call(s, "UsarEncaixe", Parte("Papel"), true, Parte("Papel"));
        Assert.AreEqual("ERRO - VERIFICAR", Status(s), "sem rede configurada");

        Call(s, "Digitar", "192.168.0.50");
        Assert.AreEqual("ERRO - VERIFICAR", Status(s), "digitado mas não confirmado ainda não vale");
        Assert.IsTrue((bool)Call(s, "ConfirmarIp"));
        Assert.IsFalse((bool)Get(s, "Pronta"), "só o IP não basta: falta a máscara");

        Call(s, "SelecionarCampo", Enum.Parse(T("ImpressoraCampoRede"), "Mascara"));
        Call(s, "Digitar", "255.255.0.255");
        Assert.IsFalse((bool)Call(s, "ConfirmarMascara"), "máscara com bits 1 não contíguos é inválida");
        Call(s, "Limpar");
        Call(s, "Digitar", "255.255.255.0");
        Assert.IsTrue((bool)Call(s, "ConfirmarMascara"));
        Assert.AreEqual("PRONTA", Status(s));
        Assert.AreEqual("IP 192.168.0.50 /24", Ip(s));
        Assert.IsTrue((bool)Get(s, "Pronta"));
    }

    [Test]
    public void Mascara_IpPrecisaSerHostDaSubRede()
    {
        Func<string, string, bool> ok = (ip, m) => (bool)T("ImpressoraSession").GetMethod("IpCompativel").Invoke(null, new object[] { ip, m });
        Func<string, bool> mascara = m => (bool)T("ImpressoraSession").GetMethod("MascaraEhValida").Invoke(null, new object[] { m });
        Assert.IsTrue(mascara("255.255.255.0"));
        Assert.IsTrue(mascara("255.255.0.0"));
        Assert.IsFalse(mascara("255.0.255.0"));
        Assert.IsFalse(mascara("0.0.0.0"));
        Assert.IsFalse(mascara("255.255.255.255"));
        Assert.IsTrue(ok("192.168.0.20", "255.255.255.0"));
        Assert.IsFalse(ok("192.168.0.0", "255.255.255.0"), "endereço da rede");
        Assert.IsFalse(ok("192.168.0.255", "255.255.255.0"), "broadcast");
        Assert.IsTrue(ok("192.168.0.255", "255.255.0.0"), "na /16 o .0.255 é host");
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
