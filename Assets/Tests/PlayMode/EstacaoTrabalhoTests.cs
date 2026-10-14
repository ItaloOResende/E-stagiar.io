using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

/// <summary>
/// Prefabs PC_Escritorio e Estacao_Trabalho: cada cópia tem um PC completo com as referências dentro de si
/// (monitor e cabos 3D ligados ao próprio gabinete), começa funcional e não compartilha estado com as outras.
/// </summary>
public class EstacaoTrabalhoTests
{
    private const string PcPath = "Assets/Prefabs/Gameplay/PC_Escritorio.prefab";
    private const string StationPath = "Assets/Prefabs/Gameplay/Estacao_Trabalho.prefab";

    private readonly List<GameObject> created = new List<GameObject>();

    private static GameObject Prefab(string path)
    {
        Type db = Type.GetType("UnityEditor.AssetDatabase, UnityEditor", true);
        var go = (GameObject)db.GetMethod("LoadAssetAtPath", new[] { typeof(string), typeof(Type) }).Invoke(null, new object[] { path, typeof(GameObject) });
        Assert.IsNotNull(go, path);
        return go;
    }

    private GameObject Spawn(string path, Vector3 pos)
    {
        GameObject g = Object.Instantiate(Prefab(path), pos, Quaternion.identity);
        created.Add(g);
        return g;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject g in created) if (g != null) Object.Destroy(g);
        created.Clear();
    }

    private static void AssertSelfContained(GameObject root)
    {
        ComputerCase pcCase = root.GetComponentsInChildren<ComputerCase>(true).Single();
        var power = pcCase.GetComponent<ComputerPower>();
        Assert.AreSame(power, root.GetComponentsInChildren<MonitorDisplay>(true).Single().Source, "monitor ligado ao próprio gabinete");
        PcCableView3D view = root.GetComponentsInChildren<PcCableView3D>(true).Single();
        Assert.AreEqual(Enum.GetValues(typeof(PcPort)).Length, view.Anchors.Count, "uma âncora 3D por porta");
        foreach (var a in view.Anchors)
        {
            Assert.IsNotNull(a.anchor, a.port.ToString());
            Assert.IsTrue(a.anchor.IsChildOf(root.transform), $"âncora {a.port} dentro da própria cópia");
        }
    }

    [UnityTest]
    public IEnumerator PcEscritorio_CompletoEFuncional()
    {
        GameObject pc = Spawn(PcPath, Vector3.zero);
        yield return null;
        AssertSelfContained(pc);
        ComputerCase pcCase = pc.GetComponentInChildren<ComputerCase>();
        Assert.AreEqual(ComputerCase.AllComponents, pcCase.Components, "computador comum começa com todas as peças");
        Assert.IsTrue(pcCase.HddHasOperatingSystem, "HD de fábrica com sistema");
    }

    [UnityTest]
    public IEnumerator Estacoes_DuplicadasNaoCompartilhamEstado()
    {
        GameObject a = Spawn(StationPath, new Vector3(-5f, 0f, 0f));
        GameObject b = Spawn(StationPath, new Vector3(5f, 0f, 0f));
        yield return null;
        AssertSelfContained(a);
        AssertSelfContained(b);
        Assert.AreEqual(1, a.GetComponentsInChildren<ComputerCase>(true).Length, "um gabinete por estação");

        ComputerCase caseA = a.GetComponentInChildren<ComputerCase>();
        ComputerCase caseB = b.GetComponentInChildren<ComputerCase>();
        Assert.AreNotSame(caseA, caseB);
        caseA.SetComponents(PcComponent.None);
        Assert.AreEqual(PcComponent.None, caseA.Components);
        Assert.AreEqual(ComputerCase.AllComponents, caseB.Components, "mudar um PC não altera o outro");
    }
}
