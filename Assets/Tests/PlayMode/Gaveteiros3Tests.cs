using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

/// <summary>
/// Gaveteiros oficiais da sala de TI, de 3 gavetas (gavetas.fbx do Matheus, Integracao1910Setup): com as gavetas
/// abertas, cada item fica inteiro dentro da caixa da sua gaveta, sem atravessar outro item; cada gaveta tem plaquinha,
/// o conteúdo esperado (o mesmo estoque dos gaveteiros antigos de 4 gavetas) e a gaveta de cabos de rede tem a trava.
/// </summary>
public class Gaveteiros3Tests
{
    // Caixa interna da gaveta (espaço local da Gaveta_N) = Integracao1910Setup.Interno (assembly de Editor).
    private static readonly Bounds Inner = new Bounds(new Vector3(0f, 0.15f, 0f), new Vector3(0.5531f, 0.30f, 0.5790f));
    private const float Tolerance = 0.003f;

    private static GameObject LoadPrefab(string path)
    {
        Type db = Type.GetType("UnityEditor.AssetDatabase, UnityEditor", true);
        MethodInfo m = db.GetMethod("LoadAssetAtPath", new[] { typeof(string), typeof(Type) });
        var go = (GameObject)m.Invoke(null, new object[] { path, typeof(GameObject) });
        Assert.IsNotNull(go, "prefab não encontrado: " + path);
        return go;
    }

    private static Type T(string name) => Type.GetType(name + ", Assembly-CSharp", true);

    [TestCase("Assets/Prefabs/Gameplay/Gaveteiro3_Hardware.prefab",
        "Ferramenta,Ferramenta,Pendrive,Alicate|PlacaMae,PlacaMae,MemoriaRAM,MemoriaRAM,MemoriaRAM,Cooler,Cooler|HD,HD,Fonte,Fonte", -1, ExpectedResult = null)]
    [TestCase("Assets/Prefabs/Gameplay/Gaveteiro3_Cabos.prefab",
        "CaboEnergia,CaboEnergia|CaboVideo,CaboVideo,CaboHDMI,CaboHDMI|CaboRede,CaboRede", 3, ExpectedResult = null)]
    [TestCase("Assets/Prefabs/Gameplay/Gaveteiro3_Suprimentos.prefab",
        "RamSodimm,RamSodimm,RamSodimm,Ssd,Ssd,Ssd,Pendrive|Toner,Toner|ResmaPapel,ResmaPapel", -1, ExpectedResult = null)]
    public IEnumerator TresGavetas_PecasDentroESemSobrepor(string path, string expected, int gavetaComTrava)
    {
        GameObject cab = Object.Instantiate(LoadPrefab(path), new Vector3(10f, 0f, 0f), Quaternion.Euler(0f, 180f, 0f));
        yield return null;
        Type openable = T("OpenableInteractable");
        string[] perDrawer = expected.Split('|');
        Assert.IsNull(cab.transform.Find("Gaveta_4"), "3 gavetas");
        for (int d = 0; d < perDrawer.Length; d++)
        {
            Transform drawer = cab.transform.Find("Gaveta_" + (d + 1));
            Assert.IsNotNull(drawer, "Gaveta_" + (d + 1));
            Assert.IsNotNull(drawer.Find("Plaquinha"), "plaquinha na Gaveta_" + (d + 1));
            Assert.AreEqual(d + 1 == gavetaComTrava, drawer.GetComponent(T("DrawerTicketLock")) != null, "trava na Gaveta_" + (d + 1));
            openable.GetMethod("SetOpen").Invoke(drawer.GetComponent(openable), new object[] { true });
        }
        yield return new WaitForSeconds(0.8f);

        for (int d = 0; d < perDrawer.Length; d++)
        {
            Transform drawer = cab.transform.Find("Gaveta_" + (d + 1));
            Component[] items = drawer.GetComponentsInChildren(T("PickupInteractable"), true);
            CollectionAssert.AreEquivalent(perDrawer[d].Split(','),
                items.Select(i => i.name.Replace("Pickup_", "").Replace("(Clone)", "")).ToArray(), drawer.name);
            foreach (Component item in items)
            {
                Assert.IsTrue(item.gameObject.activeInHierarchy, item.name + " visível com a gaveta aberta");
                foreach (Renderer r in item.GetComponentsInChildren<Renderer>())
                    AssertInside(drawer, r, item.name + " em " + drawer.name);
            }
            for (int a = 0; a < items.Length; a++)
                for (int b = a + 1; b < items.Length; b++)
                {
                    Bounds ba = LocalBounds(drawer, items[a]), bb = LocalBounds(drawer, items[b]);
                    Vector3 ov = Vector3.Min(ba.max, bb.max) - Vector3.Max(ba.min, bb.min);
                    bool sobrepoe = ov.x > 0.01f && ov.y > 0.01f && ov.z > 0.01f;
                    Assert.IsFalse(sobrepoe, $"{items[a].name} e {items[b].name} se atravessam em {drawer.name} ({ov:F3})");
                }
        }
        Assert.IsNotNull(cab.transform.Find("Plaquinha_Titulo"));
        Object.Destroy(cab);
    }

    private static Bounds LocalBounds(Transform drawer, Component item)
    {
        bool first = true; Bounds b = default;
        foreach (Renderer r in item.GetComponentsInChildren<Renderer>())
        {
            var mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            Matrix4x4 m = drawer.worldToLocalMatrix * r.transform.localToWorldMatrix;
            Bounds mb = mf.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 p = m.MultiplyPoint3x4(mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                if (first) { b = new Bounds(p, Vector3.zero); first = false; } else b.Encapsulate(p);
            }
        }
        return b;
    }

    private static void AssertInside(Transform drawer, Renderer r, string what)
    {
        var mf = r.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return;
        Bounds mb = mf.sharedMesh.bounds;
        Matrix4x4 m = drawer.worldToLocalMatrix * r.transform.localToWorldMatrix;
        for (int i = 0; i < 8; i++)
        {
            Vector3 c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            Vector3 p = m.MultiplyPoint3x4(c);
            Assert.IsTrue(p.x >= Inner.min.x - Tolerance && p.x <= Inner.max.x + Tolerance &&
                          p.y >= Inner.min.y - Tolerance && p.y <= Inner.max.y + Tolerance &&
                          p.z >= Inner.min.z - Tolerance && p.z <= Inner.max.z + Tolerance,
                $"{what}: ponto {p:F3} fora da caixa {Inner.min:F3}..{Inner.max:F3}");
        }
    }
}
