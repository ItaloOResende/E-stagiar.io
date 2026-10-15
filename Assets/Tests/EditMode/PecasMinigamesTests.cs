using System;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Peças dos minigames quebráveis (SSD, SO-DIMM, toner): o estado é da lógica (IBreakable), não só visual — o encaixe
/// recusa a peça quebrada e ela perde o tipo de gaveta. Tipos do Assembly-CSharp por reflexão (como nos outros testes).
/// </summary>
public class PecasMinigamesTests
{
    private static Type T(string n) => Type.GetType(n + ", Assembly-CSharp", true);
    private static object E(string tipo, string valor) => Enum.Parse(T(tipo), valor);

    private GameObject Item(string componente, string campo, object valor)
    {
        var go = new GameObject(componente);
        go.AddComponent(T("PickupInteractable"));
        Component c = go.AddComponent(T(componente));
        c.GetType().GetField(campo, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(c, valor);
        return go;
    }

    [Test]
    public void Notebook_SsdQuebrado_NaoEInstalado_EOInteiroE()
    {
        var note = new GameObject("Notebook");
        GameObject ssd = Item("NotebookPartItem", "parte", E("NotebookParte", "Ssd"));
        GameObject outro = Item("NotebookPartItem", "parte", E("NotebookParte", "Ssd"));
        try
        {
            Component est = note.AddComponent(T("NotebookEstacao"));
            object sessao = est.GetType().GetProperty("Sessao").GetValue(est);
            for (int i = 0; i < 6; i++)
                sessao.GetType().GetMethod("RemoverParafuso").Invoke(sessao, new object[] { i, ToolType.Screwdriver });

            var quebrado = (IBreakable)ssd.GetComponent(T("NotebookPartItem"));
            quebrado.RegisterDrop(1);
            Assert.IsTrue(quebrado.IsBroken);
            Assert.IsNull(ItemKind(ssd), "quebrado não volta para gaveta");

            var usar = est.GetType().GetMethod("UsarEncaixe");
            var args = new object[] { E("NotebookParte", "Ssd"), ssd.GetComponent(T("PickupInteractable")), null };
            Assert.AreEqual(E("NotebookEncaixeResultado", "PecaQuebrada"), usar.Invoke(est, args));
            Assert.IsFalse((bool)sessao.GetType().GetProperty("TemSsd").GetValue(sessao));

            args = new object[] { E("NotebookParte", "Ssd"), outro.GetComponent(T("PickupInteractable")), null };
            Assert.AreEqual(E("NotebookEncaixeResultado", "Instalado"), usar.Invoke(est, args), "um SSD inteiro instala");
        }
        finally { Object.DestroyImmediate(note); Object.DestroyImmediate(ssd); Object.DestroyImmediate(outro); }
    }

    [Test]
    public void Impressora_TonerQuebradoNaSegundaQueda_NaoEInstalado()
    {
        var imp = new GameObject("Impressora");
        GameObject toner = Item("ImpressoraPartItem", "parte", E("ImpressoraParte", "Toner"));
        try
        {
            Component est = imp.AddComponent(T("ImpressoraEstacao"));
            var t = (IBreakable)toner.GetComponent(T("ImpressoraPartItem"));
            t.RegisterDrop(2);
            Assert.AreEqual(PartCondition.Damaged, t.Condition, "1ª queda: ainda funciona");
            var usar = est.GetType().GetMethod("UsarEncaixe");
            t.RegisterDrop(2);
            Assert.IsTrue(t.IsBroken);
            var args = new object[] { E("ImpressoraParte", "Toner"), toner.GetComponent(T("PickupInteractable")), null };
            Assert.AreEqual(E("ImpressoraEncaixeResultado", "PecaQuebrada"), usar.Invoke(est, args));
        }
        finally { Object.DestroyImmediate(imp); Object.DestroyImmediate(toner); }
    }

    private static string ItemKind(GameObject g) =>
        (string)T("ItemKind").GetMethod("Of").Invoke(null, new object[] { g.transform });
}
