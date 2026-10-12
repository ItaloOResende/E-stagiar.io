using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

/// <summary>
/// Mira estável do InteractionDetector (itens finos, bordas) e cabos 3D do PcCableView3D (uma ponta = pendente,
/// duas pontas = curva entre as portas, nenhuma = some). Só apresentação: a lógica dos cabos já é coberta em Edit Mode.
/// </summary>
public class MiraECabos3DTests
{
    private readonly List<GameObject> created = new List<GameObject>();

    private static GameObject Prefab(string path)
    {
        Type db = Type.GetType("UnityEditor.AssetDatabase, UnityEditor", true);
        var go = (GameObject)db.GetMethod("LoadAssetAtPath", new[] { typeof(string), typeof(Type) }).Invoke(null, new object[] { path, typeof(GameObject) });
        Assert.IsNotNull(go, path);
        return go;
    }

    private static Type T(string name) => Type.GetType(name + ", Assembly-CSharp", true);

    private static void Set(object target, string field, object value) =>
        target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).SetValue(target, value);

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

    /// <summary>Jogador mínimo: ItemHolder (mão livre) + InteractionDetector com a câmera dada.</summary>
    private Component Detector(Camera cam)
    {
        var player = new GameObject("Player"); created.Add(player);
        var hold = new GameObject("HoldPoint").transform; hold.SetParent(cam.transform, false);
        Component holder = player.AddComponent(T("ItemHolder"));
        Set(holder, "holdPoint", hold);
        Component detector = player.AddComponent(T("InteractionDetector"));
        Set(detector, "holder", holder);
        Set(detector, "playerCamera", cam);
        return detector;
    }

    [UnityTest]
    public IEnumerator Mira_NaoPisca_NaBordaDeUmItemFino()
    {
        // RAM deitada (2 cm de altura) a 1 m, vista de cima em ângulo, como dentro de uma gaveta
        GameObject ram = Spawn("Assets/Prefabs/Gameplay/Pickup_MemoriaRAM.prefab", new Vector3(0f, 0f, 1f));
        var camGo = new GameObject("Cam"); created.Add(camGo);
        var cam = camGo.AddComponent<Camera>();
        Component detector = Detector(cam);
        PropertyInfo target = detector.GetType().GetProperty("CurrentTarget");
        yield return null;

        Bounds b = ram.GetComponent<Collider>().bounds;
        camGo.transform.position = new Vector3(0f, 0.8f, 0.3f);
        // mira oscilando de 1,5 cm ao redor da borda da peça (metade dos quadros o raio exato erra)
        int found = 0, frames = 40;
        for (int i = 0; i < frames; i++)
        {
            float off = (i % 2 == 0 ? 1f : -1f) * 0.015f;
            camGo.transform.LookAt(new Vector3(b.max.x + off, b.center.y, b.center.z));
            yield return null;
            if (target.GetValue(detector) != null) found++;
        }
        Assert.AreEqual(frames, found, "o alvo não pode sumir em quadros alternados (cursor piscando)");

        // longe da peça (20 cm), o alvo some depois da tolerância
        camGo.transform.LookAt(new Vector3(b.max.x + 0.2f, b.center.y, b.center.z));
        yield return new WaitForSeconds(0.3f);
        Assert.IsNull(target.GetValue(detector), "sem alvo de verdade, não fica preso");
    }

    [UnityTest]
    public IEnumerator Mira_NaoAtravessaParede()
    {
        GameObject ram = Spawn("Assets/Prefabs/Gameplay/Pickup_MemoriaRAM.prefab", new Vector3(0f, 0f, 2f));
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); created.Add(wall);
        wall.transform.position = new Vector3(0f, 0.5f, 1.2f); wall.transform.localScale = new Vector3(3f, 3f, 0.1f);
        var camGo = new GameObject("Cam"); created.Add(camGo);
        var cam = camGo.AddComponent<Camera>();
        camGo.transform.position = new Vector3(0f, 0.6f, 0f);
        camGo.transform.LookAt(ram.transform.position);
        Component detector = Detector(cam);
        for (int i = 0; i < 5; i++) yield return null;
        Assert.IsNull(detector.GetType().GetProperty("CurrentTarget").GetValue(detector));
    }

    [UnityTest]
    public IEnumerator Cabo3D_UmaPonta_Pendente_DuasPontas_Curva_NenhumaSome()
    {
        GameObject g = Spawn("Assets/Prefabs/Gameplay/Gabinete.prefab", new Vector3(0f, 0.5f, 0f));
        var floor = GameObject.CreatePrimitive(PrimitiveType.Plane); created.Add(floor);
        var power = g.GetComponent<ComputerPower>();
        var view = g.GetComponent<PcCableView3D>();
        Assert.IsNotNull(view, "Gabinete.prefab precisa de PcCableView3D");
        Assert.AreEqual(4, view.Anchors.Count, "portas 3D do gabinete: energia, VGA, HDMI e rede");

        // porta do monitor de mentira (a 40 cm), para o cabo completo
        var monitorPort = new GameObject("PortaMonitorTeste"); created.Add(monitorPort);
        monitorPort.transform.SetPositionAndRotation(g.transform.position + new Vector3(0.4f, 0f, 0.3f), Quaternion.LookRotation(Vector3.forward));
        var list = (IList)typeof(PcCableView3D).GetField("anchors", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
        list.Add(new PcCableView3D.PortAnchor { port = PcPort.MonitorVideo, anchor = monitorPort.transform });

        GameObject cableGo = Object.Instantiate(Prefab("Assets/Prefabs/Gameplay/Pickup_CaboVideo.prefab")); created.Add(cableGo);
        var cable = cableGo.GetComponent<CableItem>();
        Assert.AreEqual(PortResult.PluggedFromHand, power.UsePort(PcPort.CaseVideo, cable, true, out _));
        cableGo.SetActive(false);
        yield return null;
        Assert.AreEqual(3, view.BuiltCount(PcLink.Vga), "uma ponta: plugue na porta + plugue solto + tubo");
        Transform tube = g.transform.Find("Cabo3D_Vga/Cabo");
        Assert.Less(tube.GetComponent<MeshRenderer>().bounds.min.y, g.transform.Find("Porta3D_CaseVideo").position.y - 0.1f, "pendente: desce da porta");

        power.UsePort(PcPort.MonitorVideo, null, false, out _);
        yield return null;
        Assert.AreEqual(3, view.BuiltCount(PcLink.Vga), "duas pontas: 2 plugues encaixados + tubo");
        Assert.IsNotNull(g.transform.Find("Cabo3D_Vga/Plugue_B"));
        Assert.IsNull(g.transform.Find("Cabo3D_Vga/Plugue_Solto"));

        power.UsePort(PcPort.MonitorVideo, null, false, out _);
        power.UsePort(PcPort.CaseVideo, null, false, out _);
        yield return null;
        Assert.AreEqual(0, view.BuiltCount(PcLink.Vga), "nenhuma ponta: o cabo 3D some (o item voltou para a mão)");
        Assert.AreEqual(0, view.BuiltCount(PcLink.Power));
    }

    [Test]
    public void ItensDeCabo_TemModelosDiferentes_EComCaraDeRolo()
    {
        var names = new HashSet<string>();
        foreach (string p in new[] { "Pickup_CaboEnergia", "Pickup_CaboVideo", "Pickup_CaboHDMI", "Pickup_CaboRede" })
        {
            GameObject prefab = Prefab("Assets/Prefabs/Gameplay/" + p + ".prefab");
            var mf = prefab.transform.Find("Visual").GetComponent<MeshFilter>();
            Assert.IsNotNull(mf.sharedMesh, p);
            Assert.IsTrue(names.Add(mf.sharedMesh.name), "cada tipo de cabo tem seu modelo: " + p);
            Vector3 s = prefab.GetComponent<BoxCollider>().size;
            Assert.Less(Mathf.Max(s.x, s.z), 0.15f, p + " cabe na gaveta");
            Assert.Less(s.y, 0.06f, p + " deitado");
        }
    }
}
