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
    public void Visor_AvisoNeutro_ProntaComIp()
    {
        object s = Activator.CreateInstance(T("ImpressoraSession"));
        Assert.AreEqual("ERRO - VERIFICAR", Status(s), "o visor não diz o que falta");
        Assert.AreEqual("IP ---.---.---.---", Ip(s));

        Call(s, "UsarEncaixe", Parte("Toner"), true, Parte("Toner"));
        Call(s, "UsarEncaixe", Parte("Papel"), true, Parte("Papel"));
        Assert.AreEqual("ERRO - VERIFICAR", Status(s), "sem rede configurada");

        Call(s, "Digitar", "192168050");
        Assert.AreEqual("ERRO - VERIFICAR", Status(s), "digitado mas não confirmado ainda não vale");
        Assert.IsTrue((bool)Call(s, "ConfirmarIp"));
        Assert.AreEqual("PRONTA", Status(s), "só o IP: não existe máscara de sub-rede");
        Assert.AreEqual("IP 192.168.0.50", Ip(s));
        Assert.IsTrue((bool)Get(s, "Pronta"));
    }

    [Test]
    public void MascaraDeEntrada_PontosAutomaticosEOctetosValidos()
    {
        object s = Activator.CreateInstance(T("ImpressoraSession"));
        Func<string> dig = () => (string)Get(s, "IpDigitado");
        Func<string, string> visor = d => (string)T("ImpressoraSession").GetMethod("FormatarVisor").Invoke(null, new object[] { d });

        Call(s, "Digitar", "192");
        Assert.AreEqual("192.", dig(), "3 dígitos: o ponto entra sozinho");
        Call(s, "Digitar", "168.1.");
        Assert.AreEqual("192.168.1.", dig(), "ponto fecha o grupo antes dos 3 dígitos");
        Assert.AreEqual("192.168.1._", visor(dig()), "visor sem zeros artificiais, como o cartaz");
        Call(s, "Digitar", "10");
        Assert.AreEqual("192.168.1.10", visor(dig()));
        Call(s, "Digitar", "99");
        Assert.AreEqual("192.168.1.109", dig(), "último grupo: nada além de 3 dígitos");

        Call(s, "Limpar");
        Assert.AreEqual("_._._._", visor(""));
        Call(s, "Digitar", "300");
        Assert.AreEqual("30.0.", dig(), "o dígito que passaria de 255 começa o próximo grupo (e o 0 fecha sozinho)");
        Call(s, "Digitar", "..");
        Assert.AreEqual("30.0.", dig(), "ponto repetido não cria grupo vazio");

        // o IP do cartaz digitado só com números: o grupo "0" fecha sozinho, sem segmento extra
        Call(s, "Limpar");
        Call(s, "Digitar", "192168061");
        Assert.AreEqual("192.168.0.61", dig());
        Assert.AreEqual("192.168.0.61", visor(dig()));
        Call(s, "Limpar");
        Call(s, "Digitar", "192.168.0.61");
        Assert.AreEqual("192.168.0.61", dig(), "com pontos digitados dá o mesmo resultado");

        // corrigir números anteriores: Apagar volta pelos grupos
        Call(s, "Limpar");
        Call(s, "Digitar", "10.0.0.5");
        Call(s, "Apagar"); Call(s, "Apagar"); Call(s, "Apagar");
        Assert.AreEqual("10.", dig(), "apagar um grupo vazio leva junto o ponto e o último dígito anterior");
        Call(s, "Digitar", "1.2.3");
        Assert.AreEqual("10.1.2.3", dig());
        Assert.IsTrue((bool)Call(s, "ConfirmarIp"));
        Assert.AreEqual("10.1.2.3", (string)Get(s, "IpSalvo"), "IP salvo normalizado");

        Call(s, "Digitar", "1.2.3");
        Assert.IsFalse((bool)Call(s, "ConfirmarIp"), "3 grupos: incompleto");
        Assert.AreEqual("10.1.2.3", (string)Get(s, "IpSalvo"), "IP anterior mantido");
    }

    [Test]
    public void IpDaRodada_SoAceitaOCartaz_ESorteioMuda()
    {
        Type rede = T("ImpressoraRede");
        Action<string> definir = ip => rede.GetMethod("Definir").Invoke(null, new object[] { ip });
        Func<string> daRodada = () => (string)rede.GetProperty("IpDaRodada").GetValue(null);
        try
        {
            definir("192.168.0.42");
            object s = Activator.CreateInstance(T("ImpressoraSession"));
            Call(s, "Digitar", "192.168.0.43");
            Assert.IsFalse((bool)Call(s, "ConfirmarIp"), "IP válido mas diferente do cartaz: recusado");
            Assert.AreEqual("NaoConfere", Get(s, "UltimaRecusa").ToString());
            Assert.AreEqual("", (string)Get(s, "IpSalvo"), "nada é salvo");
            Call(s, "Limpar");
            Call(s, "Digitar", "192168042");
            Assert.IsTrue((bool)Call(s, "ConfirmarIp"), "o IP do cartaz, digitado só com números, vale");
            Assert.IsTrue((bool)Get(s, "RedeConfigurada"));

            string anterior = daRodada();
            string novo = (string)rede.GetMethod("Sortear").Invoke(null, null);
            Assert.AreNotEqual(anterior, novo, "chamado novo nunca repete o IP");
            Assert.AreEqual(novo, daRodada(), "cartaz e validação leem o mesmo valor");
            Assert.IsTrue((bool)Get(s, "RedeConfigurada"), "o IP novo de outro chamado não desconfigura esta impressora");
            StringAssert.StartsWith("192.168.0.", novo);

            object outra = Activator.CreateInstance(T("ImpressoraSession"));
            Call(outra, "Digitar", anterior);
            Assert.IsFalse((bool)Call(outra, "ConfirmarIp"), "o IP do chamado anterior não vale para o chamado novo");
            Call(outra, "Limpar");
            Call(outra, "Digitar", novo);
            Assert.IsTrue((bool)Call(outra, "ConfirmarIp"));
            Assert.AreEqual(novo, daRodada(), "tentar de novo no painel não sorteia outro IP");
        }
        finally { definir(""); }
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
