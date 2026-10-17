using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

/// <summary>
/// Transporte do gabinete com cabos: preso enquanto houver cabo do gabinete ligado fora (tomada, rede, monitor);
/// cabos só no gabinete, o cabo do monitor e o pendrive não prendem. No destino, os cabos que vieram junto ligam no
/// ponto elétrico de OUTRA estação, sem misturar tomadas entre PCs.
/// </summary>
public class TransporteGabineteTests
{
    private const string StationPath = "Assets/Prefabs/Gameplay/Estacao_Trabalho.prefab";
    private readonly List<GameObject> created = new List<GameObject>();

    private static Type T(string name) => Type.GetType(name + ", Assembly-CSharp", true);
    private static object Get(object o, string p) => o.GetType().GetProperty(p).GetValue(o);

    private static GameObject Prefab(string path)
    {
        Type db = Type.GetType("UnityEditor.AssetDatabase, UnityEditor", true);
        return (GameObject)db.GetMethod("LoadAssetAtPath", new[] { typeof(string), typeof(Type) })
            .Invoke(null, new object[] { path, typeof(GameObject) });
    }

    private GameObject Spawn(string path, Vector3 pos)
    {
        GameObject g = Object.Instantiate(Prefab(path), pos, Quaternion.identity);
        created.Add(g);
        return g;
    }

    private T2 Item<T2>(string prefab) where T2 : Component =>
        Spawn("Assets/Prefabs/Gameplay/" + prefab + ".prefab", Vector3.down * 10f).GetComponent<T2>();

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject g in created) if (g != null) Object.Destroy(g);
        created.Clear();
    }

    [UnityTest]
    public IEnumerator CabosNaParede_Prendem_OsQueFicamNoGabinete_VaoJuntoParaOutraEstacao()
    {
        GameObject a = Spawn(StationPath, new Vector3(-10f, 0f, 0f));
        GameObject b = Spawn(StationPath, new Vector3(10f, 0f, 0f));
        yield return null;
        ComputerPower pa = a.GetComponentInChildren<ComputerPower>();
        ComputerPower pb = b.GetComponentInChildren<ComputerPower>();
        WallConnectionPoint wallA = a.GetComponentInChildren<WallConnectionPoint>();
        WallConnectionPoint wallB = b.GetComponentInChildren<WallConnectionPoint>();
        Component pickup = pa.GetComponent(T("PickupInteractable"));
        Assert.AreSame(wallA, pa.CurrentWall);
        Assert.IsFalse(pa.BlocksPickup, "sem cabos: transporte livre (fluxo de montagem de sempre)");

        // energia do PC e do monitor, rede e pendrive na estação A
        Assert.AreEqual(PortResult.PluggedFromHand, pa.UsePort(PcPort.CasePower, Item<CableItem>("Pickup_CaboEnergia"), true, out _));
        Assert.AreEqual(PortResult.PluggedOtherEnd, pa.UsePort(PcPort.Outlet, null, false, out _));
        Assert.AreEqual(PortResult.PluggedFromHand, pa.UsePort(PcPort.MonitorPower, Item<CableItem>("Pickup_CaboEnergia"), true, out _));
        Assert.AreEqual(PortResult.PluggedOtherEnd, pa.UsePort(PcPort.MonitorOutlet, null, false, out _));
        Assert.AreEqual(PortResult.PluggedFromHand, pa.UsePort(PcPort.CaseNetwork, Item<CableItem>("Pickup_CaboRede"), true, out _));
        Assert.AreEqual(PortResult.PluggedOtherEnd, pa.UsePort(PcPort.NetworkJack, null, false, out _));
        Assert.AreEqual(PortResult.PluggedFromHand, pa.UseUsbPort(0, Item<PendriveItem>("Pickup_Pendrive"), true, out _));

        Assert.IsTrue(pa.BlocksPickup);
        StringAssert.Contains("energia", pa.BlockReason);
        StringAssert.Contains("rede", pa.BlockReason);
        Assert.IsTrue((bool)Get(pickup, "IsPickupBlocked"));
        StringAssert.StartsWith("Desconecte", (string)Get(pickup, "PromptText"), "o HUD mostra o motivo no lugar de 'Pegar'");

        // solta as pontas da parede: os cabos ficam pendurados no gabinete
        Assert.AreEqual(PortResult.Unplugged, pa.UsePort(PcPort.Outlet, null, false, out _));
        Assert.IsTrue(pa.BlocksPickup, "a rede ainda prende");
        Assert.AreEqual(PortResult.Unplugged, pa.UsePort(PcPort.NetworkJack, null, false, out _));
        Assert.IsFalse(pa.BlocksPickup, "cabo do monitor e pendrive não prendem o gabinete");
        Assert.IsTrue(pa.MonitorPowerConnected);
        Assert.IsFalse((bool)Get(pickup, "IsPickupBlocked"));
        Assert.AreEqual("Pegar", Get(pickup, "PromptText"));

        // o PC da estação B já usa a tomada 1 dela
        Assert.AreEqual(PortResult.PluggedFromHand, pb.UsePort(PcPort.CasePower, Item<CableItem>("Pickup_CaboEnergia"), true, out _));
        Assert.AreEqual(PortResult.PluggedOtherEnd, pb.UsePort(PcPort.Outlet, null, false, out _));

        // leva o gabinete A para perto do ponto elétrico B
        pa.transform.position = pb.transform.position + Vector3.right * 0.6f;
        yield return null;
        Assert.AreSame(wallB, pa.CurrentWall, "usa o ponto elétrico da estação onde está");
        Assert.IsTrue(pa.IsWallPointInRange);
        Assert.IsTrue(pa.IsPlugged(PcPort.Outlet), "tomada 1 de B aparece ocupada (cabo do PC B)");
        Assert.AreEqual(PortResult.Occupied, pa.UsePort(PcPort.Outlet, null, false, out _), "não usa a tomada de outro PC");
        Assert.IsFalse(pa.IsPlugged(PcPort.MonitorOutlet), "o cabo do monitor A continua na estação A, não aqui");
        Assert.IsFalse(pa.IsWallEndHere(PcLink.MonitorPower));

        Assert.AreEqual(PortResult.PluggedOtherEnd, pa.UsePort(PcPort.MonitorOutlet, null, false, out _), "liga o cabo que veio junto");
        Assert.AreEqual(PcLink.Power, pa.OutletLink(PcPort.MonitorOutlet));
        Assert.AreSame(wallB, pa.WallOf(PcLink.Power));
        Assert.AreSame(wallA, pa.WallOf(PcLink.MonitorPower), "o cabo do monitor segue com o monitor");
        Assert.AreEqual(PortResult.PluggedOtherEnd, pa.UsePort(PcPort.NetworkJack, null, false, out _));
        Assert.IsTrue(pa.PowerConnected && pa.NetworkConnected);
        Assert.IsTrue(pa.BlocksPickup, "ligado no destino: preso de novo");

        Assert.IsTrue(pb.IsPlugged(PcPort.MonitorOutlet), "para o PC B, a tomada 2 está ocupada pelo PC A");
        Assert.AreEqual(PortResult.Occupied, pb.UsePort(PcPort.MonitorOutlet, Item<CableItem>("Pickup_CaboEnergia"), true, out _));

        yield return null;
        Assert.Greater(pa.GetComponent<PcCableView3D>().BuiltCount(PcLink.Power), 0, "cabo 3D desenhado até a tomada de B");
        Assert.AreEqual(0, pa.PendrivePort, "o pendrive veio junto");
    }

    [UnityTest]
    public IEnumerator CaboQueFicouNaParedeDeOrigem_NaoEPuxadoDeLonge()
    {
        GameObject a = Spawn(StationPath, new Vector3(-10f, 0f, 0f));
        GameObject b = Spawn(StationPath, new Vector3(10f, 0f, 0f));
        yield return null;
        ComputerPower pa = a.GetComponentInChildren<ComputerPower>();
        ComputerPower pb = b.GetComponentInChildren<ComputerPower>();

        Assert.AreEqual(PortResult.PluggedFromHand, pa.UsePort(PcPort.CasePower, Item<CableItem>("Pickup_CaboEnergia"), true, out _));
        Assert.AreEqual(PortResult.PluggedOtherEnd, pa.UsePort(PcPort.Outlet, null, false, out _));
        Assert.AreEqual(PortResult.Unplugged, pa.UsePort(PcPort.CasePower, null, false, out _), "solta do gabinete; fica na tomada A");
        Assert.IsFalse(pa.BlocksPickup);

        pa.transform.position = pb.transform.position + Vector3.right * 0.6f;
        yield return null;
        Assert.AreEqual(PortResult.OutOfReach, pa.UsePort(PcPort.CasePower, null, false, out _),
            "a outra ponta está na tomada da estação A");
        Assert.IsFalse(pa.IsEndPlugged(PcLink.Power, false));
    }
}
