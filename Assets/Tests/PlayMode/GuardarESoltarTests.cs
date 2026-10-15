using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

/// <summary>
/// Soltar o item da mão conforme a mira (ItemHolder.Drop): guardar de volta na gaveta compatível (DrawerContents.TryStore),
/// colocar numa superfície mirada, ou cair pela física sem destino. E o estado (PickupInteractable.Placement) que o
/// supervisor poderá usar no futuro.
/// </summary>
public class GuardarESoltarTests
{
    private const string Hardware = "Assets/Prefabs/Gameplay/Gaveteiro_Hardware.prefab";
    private GameObject floor, player, cam, hold;
    private Component holder;
    private readonly System.Collections.Generic.List<GameObject> spawned = new System.Collections.Generic.List<GameObject>();

    private static GameObject LoadPrefab(string path)
    {
        Type db = Type.GetType("UnityEditor.AssetDatabase, UnityEditor", true);
        MethodInfo m = db.GetMethod("LoadAssetAtPath", new[] { typeof(string), typeof(Type) });
        return (GameObject)m.Invoke(null, new object[] { path, typeof(GameObject) });
    }

    private static Type T(string name) => Type.GetType(name + ", Assembly-CSharp", true);
    private static object Call(object o, string m, params object[] a) => o.GetType().GetMethod(m).Invoke(o, a);
    private static object Get(object o, string p) => o.GetType().GetProperty(p).GetValue(o);
    private static void SetField(object o, string f, object v) =>
        o.GetType().GetField(f, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(o, v);

    [SetUp]
    public void SetUp()
    {
        floor = GameObject.CreatePrimitive(PrimitiveType.Cube);          // chão: topo em y = 0
        floor.transform.localScale = new Vector3(20f, 0.2f, 20f);
        floor.transform.position = new Vector3(0f, -0.1f, 0f);
        Physics.SyncTransforms();                                          // chão já na física no 1º frame
        player = new GameObject("PlayerTeste");
        cam = new GameObject("Camera"); cam.transform.SetParent(player.transform, false);
        cam.transform.localPosition = new Vector3(0f, 1.5f, 0f);
        hold = new GameObject("HoldPoint"); hold.transform.SetParent(cam.transform, false);
        hold.transform.localPosition = new Vector3(0.3f, -0.12f, 0.48f);
        holder = player.AddComponent(T("ItemHolder"));
        SetField(holder, "holdPoint", hold.transform);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var g in spawned) if (g != null) Object.Destroy(g);
        Object.Destroy(floor); Object.Destroy(player);
    }

    private Component Spawn(string prefab, Vector3 pos)
    {
        GameObject g = Object.Instantiate(LoadPrefab("Assets/Prefabs/Gameplay/" + prefab + ".prefab"), pos, Quaternion.identity);
        spawned.Add(g);
        return g.GetComponent(T("PickupInteractable"));
    }

    private void Hold(Component item) => Assert.IsTrue((bool)Call(holder, "Pick", item));
    private string Placement(Component item) => Get(item, "Placement").ToString();
    private static Bounds WorldBounds(Component item) => (Bounds)Get(item, "WorldBounds");

    private void Aim(Vector3 eye, Vector3 target)
    {
        player.transform.position = Vector3.zero;
        cam.transform.position = eye;
        cam.transform.rotation = Quaternion.LookRotation(target - eye);
    }

    [UnityTest]
    public IEnumerator MirandoOChao_ColocaNoChao()
    {
        Component item = Spawn("Pickup_HD", new Vector3(5f, 0f, 5f));
        yield return null;
        Hold(item);
        Aim(new Vector3(0f, 1.5f, 0f), new Vector3(0f, 0f, 1.2f));
        Call(holder, "Drop");
        Assert.AreEqual("PlacedOnSurface", Placement(item));
        yield return new WaitForSeconds(1f);
        Bounds b = WorldBounds(item);
        Assert.AreEqual(0f, b.min.y, 0.02f, "apoiado no chão");
        Assert.AreEqual(1.2f, b.center.z, 0.1f, "onde a mira apontou");
    }

    [UnityTest]
    public IEnumerator SemSuperficieNaMira_CaiPelaFisica_ENaoAtravessaOChao()
    {
        Component item = Spawn("Pickup_Pendrive", new Vector3(5f, 0f, 5f));
        yield return null;
        Hold(item);
        Aim(new Vector3(0f, 1.5f, 0f), new Vector3(0f, 2.5f, 3f));     // olhando para cima: nada ao alcance
        Call(holder, "Drop");
        Assert.AreEqual("Dropped", Placement(item));
        Vector3 start = WorldBounds(item).center;
        Assert.AreEqual(0f, start.x, 0.02f, "começa a cair à frente do olhar");
        Assert.Greater(start.z, 0.55f, "à frente do jogador, fora da cápsula");
        Assert.Less(start.z, 1.0f, "sem ir parar longe (não é teletransporte)");
        yield return new WaitForSeconds(2f);
        Bounds b = WorldBounds(item);
        Assert.GreaterOrEqual(b.min.y, -0.01f, "não atravessa o chão");
        Assert.Less(b.min.y, 0.03f, "não fica suspenso: caiu até o chão");
    }

    [UnityTest]
    public IEnumerator GuardaDeVoltaNaGavetaCompativel_DentroDela_EPodePegarDeNovo()
    {
        GameObject cab = Object.Instantiate(LoadPrefab(Hardware), new Vector3(0f, 0f, 2f), Quaternion.Euler(0f, 180f, 0f));
        spawned.Add(cab);
        yield return null;
        Transform tools = cab.transform.Find("Gaveta_4");                // ferramentas
        Type openable = T("OpenableInteractable");
        Call(tools.GetComponent(openable), "SetOpen", true);
        yield return new WaitForSeconds(0.8f);
        Component drawer = tools.GetComponent(T("DrawerContents"));
        var area = (BoxCollider)Get(drawer, "StorageArea");
        Assert.IsNotNull(area, "gaveta com área de guardar");
        Assert.IsTrue(area.isTrigger);

        // pega uma chave de fenda da gaveta e guarda de volta mirando o interior da gaveta
        Component screwdriver = null;
        foreach (Component c in tools.GetComponentsInChildren(T("PickupInteractable")))
            if (c.name.StartsWith("Pickup_Ferramenta")) { screwdriver = c; break; }
        Assert.AreEqual("StoredInDrawer", Placement(screwdriver), "itens da gaveta já começam guardados");
        Hold(screwdriver);
        int before = ((System.Collections.ICollection)Get(drawer, "Inside")).Count;
        Vector3 inside = area.transform.TransformPoint(area.center);
        Aim(inside + new Vector3(0f, 0.9f, 0f) + (inside - cab.transform.position).normalized * 0.3f, inside);
        Call(holder, "Drop");
        Assert.IsFalse((bool)Get(holder, "IsHolding"), "guardou");
        Assert.AreEqual("StoredInDrawer", Placement(screwdriver));
        Assert.AreSame(drawer, Get(screwdriver, "Drawer"));
        Assert.AreEqual(before + 1, ((System.Collections.ICollection)Get(drawer, "Inside")).Count);
        Assert.IsTrue(screwdriver.transform.IsChildOf(tools), "acompanha a gaveta");
        AssertInsideArea(area, WorldBounds(screwdriver));
        AssertNoOverlap(tools, screwdriver);

        // um cabo não é guardado na gaveta de ferramentas: continua na mão
        Component cable = Spawn("Pickup_CaboRede", new Vector3(5f, 0f, 5f));
        yield return null;
        Hold(cable);
        Assert.IsFalse((bool)Call(drawer, "Accepts", cable));
        Call(holder, "Drop");
        Assert.IsTrue((bool)Get(holder, "IsHolding"), "gaveta incompatível: não solta");
        Call(holder, "Drop");                                            // ainda mirando a gaveta
        Assert.AreSame(cable, Get(holder, "Current"));

        // fechar esconde o item guardado; abrir de novo mostra e dá para pegar outra vez
        Call(tools.GetComponent(openable), "SetOpen", false);
        yield return new WaitForSeconds(0.8f);
        Assert.IsFalse(screwdriver.gameObject.activeInHierarchy);
        Call(tools.GetComponent(openable), "SetOpen", true);
        yield return new WaitForSeconds(0.8f);
        Assert.IsTrue(screwdriver.gameObject.activeInHierarchy);
        Assert.IsTrue((bool)Get(drawer, "IsOpenForStorage"));
    }

    [UnityTest]
    public IEnumerator GavetaFechada_NaoGuarda()
    {
        GameObject cab = Object.Instantiate(LoadPrefab(Hardware), new Vector3(0f, 0f, 2f), Quaternion.Euler(0f, 180f, 0f));
        spawned.Add(cab);
        yield return null;
        Component drawer = cab.transform.Find("Gaveta_4").GetComponent(T("DrawerContents"));
        Component pendrive = Spawn("Pickup_Pendrive", new Vector3(5f, 0f, 5f));
        yield return null;
        Assert.IsTrue((bool)Call(drawer, "Accepts", pendrive), "pendrive é da gaveta de ferramentas");
        Assert.IsFalse((bool)Call(drawer, "TryStore", pendrive, cab.transform.position), "fechada: não guarda");
    }

    // ---- jogador de verdade (cápsula), olhar para cima/frente/baixo -----------------------------------------------

    private void AddPlayerCapsule()
    {
        var cc = player.AddComponent<CharacterController>();
        cc.height = 1.7f; cc.radius = 0.3f; cc.center = new Vector3(0f, 0.85f, 0f);
        Physics.SyncTransforms();
    }

    [UnityTest]
    public IEnumerator SoltarOlhandoParaCima_CaiNaHora_NaoFicaNaCabecaDoJogador(
        [Values("Pickup_PlacaMae", "Pickup_MemoriaRAM", "Pickup_HD", "Pickup_Ferramenta", "Pickup_CaboRede")] string prefab)
    {
        AddPlayerCapsule();
        Component item = Spawn(prefab, new Vector3(5f, 0f, 5f));
        yield return null;
        Hold(item);
        Aim(new Vector3(0f, 1.5f, 0f), new Vector3(0f, 1.5f + Mathf.Tan(20f * Mathf.Deg2Rad), 1f));   // 20° para cima
        Call(holder, "Drop");
        Assert.AreEqual("Dropped", Placement(item));
        float y0 = WorldBounds(item).center.y;
        yield return new WaitForSeconds(0.25f);
        Assert.Less(WorldBounds(item).center.y, y0 - 0.1f, "começa a cair na hora (não fica preso no jogador)");
        yield return new WaitForSeconds(2f);
        Bounds b = WorldBounds(item);
        Assert.Less(b.min.y, 0.05f, prefab + " chegou ao chão (não ficou na cabeça)");
        Assert.GreaterOrEqual(b.min.y, -0.01f, "não atravessa o chão");
    }

    [UnityTest]
    public IEnumerator SoltarOlhandoParaFrenteEParaBaixo_ChegaAoChao(
        [Values(0f, -35f, -70f)] float pitch)
    {
        AddPlayerCapsule();
        Component item = Spawn("Pickup_PlacaMae", new Vector3(5f, 0f, 5f));
        yield return null;
        Hold(item);
        Vector3 eye = new Vector3(0f, 1.5f, 0f);
        Aim(eye, eye + Quaternion.Euler(-pitch, 0f, 0f) * Vector3.forward);
        Call(holder, "Drop");
        yield return new WaitForSeconds(2f);
        Bounds b = WorldBounds(item);
        Assert.Less(b.min.y, 0.05f, "no chão");
        Assert.GreaterOrEqual(b.min.y, -0.01f);
    }

    [UnityTest]
    // Ângulos dentro do alcance único de 2 m (olhos a 1,573 m: a partir de ~52° para baixo o chão está a ≤ 2 m).
    public IEnumerator Mira_NaoEBloqueadaPeloProprioJogador_ObjetosNoChaoSaoSelecionados(
        [Values(55f, 65f, 75f)] float pitch)
    {
        yield return MiraNoChao(pitch);
        Assert.AreSame(lastItem, Get(lastDetector, "CurrentTarget"), $"cabo no chão selecionado olhando {pitch}° para baixo");
    }

    [UnityTest]
    public IEnumerator Alcance_ObjetoNoChaoAlemDoAlcance_NaoESelecionado()
    {
        yield return MiraNoChao(40f);                                        // a 2,45 m dos olhos (> 2 m)
        Assert.IsNull(Get(lastDetector, "CurrentTarget"), "longe demais: não pega de longe");
    }

    private Component lastDetector, lastItem;

    private IEnumerator MiraNoChao(float pitch)
    {
        AddPlayerCapsule();
        var capsule = player.AddComponent<CapsuleCollider>();               // a cápsula que sobrou do placeholder
        capsule.height = 1.7f; capsule.radius = 0.43f; capsule.center = new Vector3(0f, 0.85f, 0f);
        cam.transform.localPosition = new Vector3(0f, 1.573f, 0f);           // olhos na borda do topo da cápsula
        Component det = player.AddComponent(T("InteractionDetector"));
        ((Behaviour)det).enabled = false;
        SetField(det, "playerCamera", cam.AddComponent<Camera>());
        SetField(det, "holder", holder);
        float d = 1.573f / Mathf.Tan(pitch * Mathf.Deg2Rad);
        Component item = Spawn("Pickup_CaboRede", new Vector3(0f, 0f, d));
        yield return null;
        Physics.SyncTransforms();
        Vector3 aim = WorldBounds(item).center;
        cam.transform.rotation = Quaternion.LookRotation(aim - cam.transform.position);
        det.GetType().GetMethod("FindTargets", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(det, new object[] { false });
        lastDetector = det;
        lastItem = item;
    }

    // ---- quedas e dano --------------------------------------------------------------------------------------------

    private IEnumerator DropFromHand(Component item)
    {
        Hold(item);
        Aim(new Vector3(0f, 1.5f, 0f), new Vector3(0f, 2.5f, 3f));                 // sem superfície na mira: cai
        Call(holder, "Drop");
        yield return new WaitForSeconds(2f);
    }

    private static Component Part(Component item) => item.GetComponent(Type.GetType("ComputerPartItem, EstagiarIo.Computer", true));

    private IEnumerator AssertBrokenAndRejected(Component item, string cabinet, string drawerName)
    {
        Transform visual = item.transform.Find("Visual"), broken = item.transform.Find("Visual_Quebrado");
        Assert.AreEqual("Broken", Get(Part(item), "Condition").ToString(), item.name + " quebrou");
        Assert.IsFalse(visual.gameObject.activeSelf);
        Assert.IsTrue(broken.gameObject.activeSelf, "visual quebrado no chão");
        Assert.IsTrue(item.gameObject.activeInHierarchy, "continua no mundo");
        Assert.Less(WorldBounds(item).min.y, 0.08f, "no chão");
        GameObject cab = Object.Instantiate(LoadPrefab(cabinet), new Vector3(0f, 0f, 3f), Quaternion.Euler(0f, 180f, 0f));
        spawned.Add(cab);
        yield return null;
        Assert.IsFalse((bool)Call(cab.transform.Find(drawerName).GetComponent(T("DrawerContents")), "Accepts", item),
            "quebrada não volta para a gaveta");
    }

    [UnityTest]
    public IEnumerator PlacaMaeERam_QuebramNaPrimeiraQueda_ETrocamOVisual(
        [Values("Pickup_PlacaMae", "Pickup_MemoriaRAM")] string prefab)
    {
        Component item = Spawn(prefab, new Vector3(5f, 0f, 5f));
        yield return null;
        Assert.IsNotNull(item.transform.Find("Visual_Quebrado"), "modelo quebrado no prefab");
        Assert.IsFalse(item.transform.Find("Visual_Quebrado").gameObject.activeSelf);
        yield return DropFromHand(item);
        Assert.AreEqual(1, (int)Get(Part(item), "DropCount"), "contou uma vez (quiques não contam)");
        yield return AssertBrokenAndRejected(item, Hardware, prefab == "Pickup_PlacaMae" ? "Gaveta_2" : "Gaveta_1");
    }

    [UnityTest]
    public IEnumerator ColocarNoChaoDePropositoEGuardar_NaoContamComoQueda()
    {
        Component item = Spawn("Pickup_PlacaMae", new Vector3(5f, 0f, 5f));
        yield return null;
        Hold(item);
        Aim(new Vector3(0f, 1.5f, 0f), new Vector3(0f, 0f, 1.0f));
        Call(holder, "Drop");
        Assert.AreEqual("PlacedOnSurface", Placement(item));
        yield return new WaitForSeconds(1f);
        Hold(item);
        Aim(new Vector3(0f, 1.5f, 0f), new Vector3(0.5f, 0f, 1.0f));
        Call(holder, "Drop");
        yield return new WaitForSeconds(1f);
        Assert.AreEqual(0, (int)Get(Part(item), "DropCount"), "colocar no chão não é queda");
        Assert.AreEqual("Intact", Get(Part(item), "Condition").ToString());
    }

    [UnityTest]
    public IEnumerator HdFonteCooler_PrimeiraQuedaRegistra_SegundaQuebra(
        [Values("Pickup_HD", "Pickup_Fonte", "Pickup_Cooler")] string prefab)
    {
        Component item = Spawn(prefab, new Vector3(5f, 0f, 5f));
        yield return null;
        Component part = Part(item);
        if (prefab == "Pickup_HD") part.GetType().GetProperty("HasOperatingSystem").SetValue(part, true);
        string drawerName = prefab == "Pickup_Cooler" ? "Gaveta_1" : "Gaveta_3";

        yield return DropFromHand(item);
        Assert.AreEqual(1, (int)Get(part, "DropCount"), "1ª queda registrada");
        Assert.AreEqual("Damaged", Get(part, "Condition").ToString(), "ainda funciona");
        Assert.IsTrue(item.transform.Find("Visual").gameObject.activeSelf);
        if (prefab == "Pickup_HD") Assert.IsTrue((bool)part.GetType().GetProperty("HasOperatingSystem").GetValue(part), "o sistema continua no HD");
        GameObject cab = Object.Instantiate(LoadPrefab(Hardware), new Vector3(0f, 0f, 3f), Quaternion.Euler(0f, 180f, 0f));
        spawned.Add(cab);
        yield return null;
        Assert.IsTrue((bool)Call(cab.transform.Find(drawerName).GetComponent(T("DrawerContents")), "Accepts", item), "ainda pode ser guardado");
        Object.Destroy(cab);

        yield return DropFromHand(item);
        yield return AssertBrokenAndRejected(item, Hardware, drawerName);
    }

    [UnityTest]
    public IEnumerator Cabos_NaoQuebram_ESaoGuardaveis()
    {
        Component item = Spawn("Pickup_CaboRede", new Vector3(5f, 0f, 5f));
        yield return null;
        yield return DropFromHand(item);
        yield return DropFromHand(item);
        yield return DropFromHand(item);
        Assert.AreEqual("Dropped", Placement(item));
        Assert.IsNull(item.GetComponent(Type.GetType("PartFallDamage, EstagiarIo.Computer", true)), "cabo não tem dano");
        GameObject cab = Object.Instantiate(LoadPrefab("Assets/Prefabs/Gameplay/Gaveteiro_Cabos.prefab"), new Vector3(0f, 0f, 3f), Quaternion.Euler(0f, 180f, 0f));
        spawned.Add(cab);
        yield return null;
        Assert.IsTrue((bool)Call(cab.transform.Find("Gaveta_4").GetComponent(T("DrawerContents")), "Accepts", item));
    }

    [UnityTest]
    public IEnumerator SoltarSemDestino_CaiAFrenteDoJogador_EDaParaPegarDeNovo()
    {
        AddPlayerCapsule();
        Component item = Spawn("Pickup_PlacaMae", new Vector3(5f, 0f, 5f));
        yield return null;
        Hold(item);
        Aim(new Vector3(0f, 1.5f, 0f), new Vector3(0f, 2.5f, 3f));                 // sem destino: cai
        Call(holder, "Drop");
        yield return new WaitForSeconds(2f);
        Bounds b = WorldBounds(item);
        float ahead = b.center.z;
        Assert.Greater(ahead, 0.45f, "caiu à frente do jogador (fora da cápsula)");
        Assert.Less(ahead, 1.4f, "não foi parar longe");
        Assert.Less(b.min.y, 0.05f);
        Hold(item);
        Assert.AreEqual("Held", Placement(item), "pode ser pego de novo");
    }

    // ---- estoque finito -------------------------------------------------------------------------------------------

    [UnityTest]
    public IEnumerator Estoque_Finito_AbrirEFecharNaoDuplica_PegarTiraDoEstoque()
    {
        GameObject cab = Object.Instantiate(LoadPrefab(Hardware), new Vector3(0f, 0f, 2f), Quaternion.Euler(0f, 180f, 0f));
        spawned.Add(cab);
        yield return null;
        Type openable = T("OpenableInteractable");
        Transform boards = cab.transform.Find("Gaveta_2");
        Component drawer = boards.GetComponent(T("DrawerContents"));
        int Total() => Object.FindObjectsByType(T("PickupInteractable"), FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        int total = Total();
        Assert.AreEqual(2, (int)Get(drawer, "RemainingCount"), "2 placas-mãe");
        for (int i = 0; i < 3; i++)
        {
            Call(boards.GetComponent(openable), "SetOpen", true); yield return new WaitForSeconds(0.7f);
            Call(boards.GetComponent(openable), "SetOpen", false); yield return new WaitForSeconds(0.7f);
        }
        Assert.AreEqual(2, (int)Get(drawer, "RemainingCount"), "abrir e fechar não cria unidades");
        Assert.AreEqual(total, Total(), "nenhum objeto novo no mundo");
        Call(boards.GetComponent(openable), "SetOpen", true); yield return new WaitForSeconds(0.7f);
        Component board = boards.GetComponentInChildren(T("PickupInteractable"));
        Hold(board);
        Assert.AreEqual(1, (int)Get(drawer, "RemainingCount"), "a unidade saiu do estoque");
        Assert.AreEqual(total, Total(), "é o mesmo objeto, não uma cópia");
        Call(boards.GetComponent(openable), "SetOpen", false); yield return new WaitForSeconds(0.7f);
        Call(boards.GetComponent(openable), "SetOpen", true); yield return new WaitForSeconds(0.7f);
        Assert.AreEqual(1, (int)Get(drawer, "RemainingCount"));
        Assert.AreEqual(total, Total());
    }

    private static void AssertInsideArea(BoxCollider area, Bounds world)
    {
        Vector3 min = area.center - area.size / 2f, max = area.center + area.size / 2f;
        for (int i = 0; i < 8; i++)
        {
            Vector3 c = world.center + Vector3.Scale(world.extents, new Vector3((i & 1) * 2 - 1, (i & 2) - 1, (i & 4) / 2 - 1));
            Vector3 p = area.transform.InverseTransformPoint(c);
            Assert.IsTrue(p.x >= min.x - 0.003f && p.x <= max.x + 0.003f && p.z >= min.z - 0.003f && p.z <= max.z + 0.003f &&
                          p.y >= min.y - 0.003f, $"ponto {p:F3} fora da área {min:F3}..{max:F3}");
        }
    }

    private void AssertNoOverlap(Transform drawer, Component stored)
    {
        Bounds a = WorldBounds(stored);
        foreach (Component other in drawer.GetComponentsInChildren(T("PickupInteractable")))
        {
            if (other == stored) continue;
            Bounds b = WorldBounds(other);
            bool overlap = a.min.x < b.max.x - 0.001f && a.max.x > b.min.x + 0.001f && a.min.z < b.max.z - 0.001f && a.max.z > b.min.z + 0.001f;
            Assert.IsFalse(overlap, stored.name + " sobre " + other.name);
        }
    }
}
