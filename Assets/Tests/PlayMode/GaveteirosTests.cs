using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

/// <summary>
/// Gaveteiros da sala (Gaveteiro_Hardware e Gaveteiro_Cabos): com todas as gavetas abertas, cada item fica
/// inteiro dentro da caixa da sua gaveta, e cada gaveta tem plaquinha e o conteúdo esperado.
/// </summary>
public class GaveteirosTests
{
    // Caixa interna da gaveta (espaço local da gaveta) = GaveteirosSetup.Inner (assembly de Editor, não referenciável aqui).
    private static readonly Bounds Inner = new Bounds(new Vector3(0f, -0.004f, 0.016f), new Vector3(0.576f, 0.138f, 0.488f));
    private const float Tolerance = 0.002f;

    private static GameObject LoadPrefab(string path)
    {
        Type db = Type.GetType("UnityEditor.AssetDatabase, UnityEditor", true);
        MethodInfo m = db.GetMethod("LoadAssetAtPath", new[] { typeof(string), typeof(Type) });
        var go = (GameObject)m.Invoke(null, new object[] { path, typeof(GameObject) });
        Assert.IsNotNull(go, "prefab não encontrado: " + path);
        return go;
    }

    private static Type T(string name) => Type.GetType(name + ", Assembly-CSharp", true);

    [TestCase("Assets/Prefabs/Gameplay/Gaveteiro_Hardware.prefab",
        "MemoriaRAM,MemoriaRAM,MemoriaRAM,Cooler|PlacaMae|HD,Fonte|Ferramenta,Ferramenta", ExpectedResult = null)]
    [TestCase("Assets/Prefabs/Gameplay/Gaveteiro_Cabos.prefab",
        "CaboEnergia,CaboEnergia|CaboVideo|CaboHDMI|CaboRede,CaboRede", ExpectedResult = null)]
    public IEnumerator TodasAsPecas_FicamDentroDaGaveta_ComAGavetaAberta(string path, string expected)
    {
        GameObject cab = Object.Instantiate(LoadPrefab(path), new Vector3(10f, 0f, 0f), Quaternion.Euler(0f, 180f, 0f));
        yield return null;
        Type openable = T("OpenableInteractable");
        string[] perDrawer = expected.Split('|');
        for (int d = 0; d < perDrawer.Length; d++)
        {
            Transform drawer = cab.transform.Find("Gaveta_" + (d + 1));
            Assert.IsNotNull(drawer, "Gaveta_" + (d + 1));
            Assert.IsNotNull(drawer.Find("Plaquinha"), "plaquinha na Gaveta_" + (d + 1));
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
        }
        Assert.IsNotNull(cab.transform.Find("Plaquinha_Titulo"));
        Object.Destroy(cab);
    }

    private static void AssertInside(Transform drawer, Renderer r, string what)
    {
        // cantos dos bounds da malha, no espaço local da gaveta
        var mf = r.GetComponent<MeshFilter>();
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
