using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

/// <summary>
/// Estações esquerda/direita (CenaTesteSetup): o ponto elétrico acompanha o gabinete, a funcional começa montada e
/// ligada no sistema (cabos reais, ainda interativos) e a com problema sorteia peças/cabos.
/// </summary>
public class EstacoesLadosTests
{
    private const string Gameplay = "Assets/Prefabs/Gameplay/";
    private readonly List<GameObject> created = new List<GameObject>();

    private static string Station(bool direita, bool funcional) =>
        Gameplay + $"Estacao_{(direita ? "Direita" : "Esquerda")}_{(funcional ? "Funcional" : "Problema")}.prefab";

    private GameObject Spawn(string path, Vector3 pos)
    {
        Type db = Type.GetType("UnityEditor.AssetDatabase, UnityEditor", true);
        var prefab = (GameObject)db.GetMethod("LoadAssetAtPath", new[] { typeof(string), typeof(Type) }).Invoke(null, new object[] { path, typeof(GameObject) });
        Assert.IsNotNull(prefab, path);
        GameObject g = Object.Instantiate(prefab, pos, Quaternion.identity);
        created.Add(g);
        return g;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject g in created) if (g != null) Object.Destroy(g);
        created.Clear();
        foreach (CableItem c in Object.FindObjectsByType<CableItem>(FindObjectsInactive.Include))
            Object.Destroy(c.gameObject);
    }

    [UnityTest]
    public IEnumerator Funcional_ComecaMontadaELigadaNoSistema([Values(false, true)] bool direita)
    {
        GameObject s = Spawn(Station(direita, true), new Vector3(direita ? 6f : -6f, 0f, 0f));
        yield return null;
        var power = s.GetComponentInChildren<ComputerPower>();
        Assert.AreEqual(ComputerCase.AllComponents, power.Case.Components);
        Assert.IsTrue(power.PowerConnected && power.MonitorPowerConnected && power.HdmiConnected && power.NetworkConnected, "cabos ligados");
        Assert.AreEqual(PcPowerState.OperatingSystem, power.State, "PC ligado no sistema");
        Assert.AreEqual(PcScreen.OperatingSystem, power.Screen);
        Assert.IsTrue(power.Online, "rede ligada");
    }

    [UnityTest]
    public IEnumerator Funcional_CaboContinuaInterativo()
    {
        GameObject s = Spawn(Station(false, true), Vector3.zero);
        yield return null;
        var power = s.GetComponentInChildren<ComputerPower>();
        Assert.AreEqual(PortResult.Unplugged, power.UsePort(PcPort.Outlet, null, false, out _));
        Assert.AreEqual(PcPowerState.Off, power.State, "sem energia o PC desliga");
        Assert.AreEqual(PortResult.CableReturned, power.UsePort(PcPort.CasePower, null, false, out GameObject item));
        Assert.IsNotNull(item, "o mesmo cabo volta para a mão");
        Assert.IsNotNull(item.GetComponent<CableItem>());
    }

    [UnityTest]
    public IEnumerator Direita_PontoEletricoAcompanhaOGabinete()
    {
        GameObject e = Spawn(Station(false, true), new Vector3(-6f, 0f, 0f));
        GameObject d = Spawn(Station(true, true), new Vector3(6f, 0f, 0f));
        yield return null;
        float Lado(GameObject st, Component c) => st.transform.InverseTransformPoint(c.transform.position).x;
        var caseE = e.GetComponentInChildren<ComputerCase>();
        var caseD = d.GetComponentInChildren<ComputerCase>();
        var wallE = e.GetComponentInChildren<WallConnectionPoint>();
        var wallD = d.GetComponentInChildren<WallConnectionPoint>();
        // direita: não é o espelho exato (-0,746 punha o gabinete em cima do mouse); fica em x -0,85 (CenaTesteSetup.CaseXRight)
        Assert.AreEqual(-0.85f, Lado(d, caseD), 0.02f, "gabinete do lado direito, na ponta da mesa");
        Assert.AreEqual(-Math.Sign(Lado(e, caseE)), Math.Sign(Lado(d, caseD)), "lado oposto ao da estação esquerda");
        Assert.Less(caseD.transform.eulerAngles.z % 360f < 180f ? caseD.transform.eulerAngles.z % 360f : 360f - caseD.transform.eulerAngles.z % 360f, 1f,
            "gabinete reto (antes tombava ~10° sobre o mouse)");
        Assert.AreEqual(-Lado(e, wallE), Lado(d, wallD), 0.02f, "ponto elétrico espelhado");
        Assert.AreEqual(Math.Sign(Lado(d, caseD)), Math.Sign(Lado(d, wallD)), "ponto do mesmo lado do gabinete");
        Assert.IsTrue(caseD.GetComponent<ComputerPower>().IsWallPointInRange);
    }

    [UnityTest]
    public IEnumerator Problema_SorteiaPecasOuCabos([Values(false, true)] bool direita)
    {
        // Em várias cópias, ao menos uma vem com defeito (peça faltando ou cabo solto) e nenhuma fica num estado inválido.
        int comDefeito = 0;
        var power = new List<ComputerPower>();
        for (int i = 0; i < 8; i++)
            power.Add(Spawn(Station(direita, false), new Vector3(i * 4f, 0f, 20f)).GetComponentInChildren<ComputerPower>());
        yield return null;
        foreach (ComputerPower p in power)
        {
            bool completo = p.Case.Components == ComputerCase.AllComponents && p.PowerConnected && p.MonitorPowerConnected && p.HdmiConnected;
            if (!completo) comDefeito++;
            if (p.State != PcPowerState.Off) Assert.IsTrue(p.CanReachBios, "só liga com os requisitos");
            else Assert.IsFalse(p.CanReachBios && p.Case.HddHasOperatingSystem && p.Case.HasComponent(PcComponent.Hdd), "com tudo certo, liga");
        }
        Assert.Greater(comDefeito, 0, "estações com problema sorteiam defeitos");
    }
}
