using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// Integração do gameplay na <c>Office_Placeholder</c> (cena oficial): Tools &gt; E-stagiar.io &gt; Escritório real:
/// integração do gameplay.
///  1. Assets: puff (Props/Puff), carrinho do rack (Props/Carrinho_Rack), monitor de estoque REAL
///     (Gameplay/Monitor_Estoque = variante do OF_Monitor com os componentes do monitor do PC_Escritorio, avulso:
///     <see cref="MonitorDisplay"/> "Connect When Placed") e âncoras de cabo no MonitorDisplay do PC_Escritorio.
///  2. Sala de TI (só o pedido; o resto da sala não muda): puff à esquerda dos armários, os monitores decorativos da
///     estante trocados pelo Monitor_Estoque nos 4 lugares, RackInternet sobre o carrinho.
///  3. Sistemas que faltavam, MOVIDOS da <c>Teste_Integrado</c> (aberta só em memória, nunca salva): Chamados
///     (TicketManager, fase, celular, relógio, pontuação, menu inicial e relatório), EventSystem, Sons_Interacao,
///     NPC_Supervisor, NavMesh_Escritorio, Caixa_CaboRede e Testador_CaboRede. Referências para objetos que existem
///     nas duas cenas (Player, HUD, ScreenCanvas...) passam para os da Office pelo caminho na hierarquia. Player, HUD,
///     ScreenCanvas, telas, Conduta e RedeGame são os da própria Office (HUD e ScreenCanvas estavam desligados).
///  4. Chamados ligados aos equipamentos reais: estações (funcionário, plaquinha, NPC, ponte do PC/notebook, porta do
///     rack), impressora da área de impressão, caixa de cabo, retirada da estação nova na bancada de manutenção,
///     reposição de estoque, ronda do supervisor e NavMesh assado para a geometria atual.
/// Tudo é achado por componente (estações = raízes "Estacao*" com ComputerCase/NotebookEstacao), não por coordenada:
/// mover os móveis depois não quebra nada; rodar de novo reaplica (idempotente) e re-assa o NavMesh.
/// Relatório: Logs/IntegracaoGameplay.txt.
/// </summary>
public static class OfficeGameplaySetup
{
    private const string Office = "Assets/Scenes/Office_Placeholder.unity";
    private const string Teste = "Assets/Scenes/Teste_Integrado.unity";
    private const string NavAsset = "Assets/Scenes/Office_Placeholder/NavMesh_Office.asset";
    private const string Models = "Assets/Art/Models/";
    private const string MatDir = "Assets/Art/Materials/Mobiliario/";
    private const string Props = "Assets/Prefabs/Props/";
    private const string Gameplay = "Assets/Prefabs/Gameplay/";
    private const string MonitorPrefab = Gameplay + "Monitor_Estoque.prefab";
    private const string LogPath = "Logs/IntegracaoGameplay.txt";
    private const float NpcHeight = 1.65f;

    private static readonly string[] MoveFromTeste =
        { "Chamados", "EventSystem", "Sons_Interacao", "NPC_Supervisor", "NavMesh_Escritorio", "Caixa_CaboRede", "Testador_CaboRede" };

    // funcionários (solicitantes reais das fichas), na ordem das estações (desktops primeiro, depois notebooks)
    private static readonly (string nome, string setor)[] Funcionarios =
    {
        ("Francisco", "Comercial"), ("Marcia", "Financeiro"), ("Carmen", "RH"),
        ("Paula", "Marketing"), ("Laura", "Diretoria"), ("Joana", "Administrativo"),
    };
    private static readonly string[] NpcModels = { "Assets/Art/Models/npc/npc2.fbx", "Assets/Art/Models/npc/character2.fbx" };

    internal static List<string> log;

    [MenuItem("Tools/E-stagiar.io/Escritório real: integração do gameplay")]
    public static void Run()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        log = new List<string>();
        try
        {
            Material amarelo = Mat("M_Mobiliario_Amarelo", new Color(0.95f, 0.76f, 0.19f));
            Material amareloEsc = Mat("M_Mobiliario_AmareloEscuro", new Color(0.78f, 0.58f, 0.10f));
            Material preto = Mat("M_Mobiliario_Preto", new Color(0.07f, 0.07f, 0.08f));
            Material metal = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "M_Mobiliario_Metal.mat");
            Material escuro = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "M_Mobiliario_Escuro.mat");
            GameObject puff = PropPrefab("puff", "Puff", new Dictionary<string, Material> { { "Tecido", amarelo }, { "Costura", amareloEsc } });
            GameObject carrinho = PropPrefab("carrinho_rack", "Carrinho_Rack",
                new Dictionary<string, Material> { { "Preto", preto }, { "Metal", metal }, { "Escuro", escuro } });
            MonitorAnchorsInPcPrefab();

            Scene office = EditorSceneManager.OpenScene(Office, OpenSceneMode.Single);
            GameObject monitor = StockMonitorPrefab(office);

            SalaTI(office, puff, carrinho, monitor);

            Scene teste = EditorSceneManager.OpenScene(Teste, OpenSceneMode.Additive);
            MoveSystems(teste, office);
            EditorSceneManager.CloseScene(teste, true);   // sem salvar: a Teste_Integrado fica como estava
            SceneManager.SetActiveScene(office);

            Systems(office);
            Bake(office);
            Stations(office);
            Devices(office);
            Patrol(office);
            Validate(office);

            EditorSceneManager.MarkSceneDirty(office);
            EditorSceneManager.SaveScene(office);
            AssetDatabase.SaveAssets();
        }
        catch (System.Exception e)
        {
            log.Add("ERRO: " + e);
            Debug.LogException(e);
        }
        File.WriteAllLines(LogPath, log);
        Debug.Log("[IntegracaoGameplay]\n" + string.Join("\n", log));
    }

    // ================================================================== assets

    internal static Material Mat(string name, Color c)
    {
        string path = MatDir + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            var baseMat = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "M_Mobiliario_Claro.mat");
            m = new Material(baseMat);
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_BaseColor", c);
        EditorUtility.SetDirty(m);
        return m;
    }

    // Root lógico (BoxCollider no tamanho do modelo, escala 1) + filho Visual com o FBX e os materiais por slot.
    internal static GameObject PropPrefab(string fbx, string name, Dictionary<string, Material> mats)
    {
        AssetDatabase.ImportAsset(Models + fbx + ".fbx", ImportAssetOptions.ForceUpdate);
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(Models + fbx + ".fbx");
        var root = new GameObject(name);
        var visual = (GameObject)PrefabUtility.InstantiatePrefab(model);
        visual.name = "Visual";
        visual.transform.SetParent(root.transform, false);
        foreach (Renderer r in visual.GetComponentsInChildren<Renderer>())
            r.sharedMaterials = r.sharedMaterials.Select(m => m != null && mats.TryGetValue(m.name, out Material x) ? x : m).ToArray();
        Bounds b = WorldBounds(root);
        var box = root.AddComponent<BoxCollider>();
        box.center = b.center;
        box.size = b.size;
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, Props + name + ".prefab");
        Object.DestroyImmediate(root);
        log.Add($"prefab {name}: {b.size.x:0.00} x {b.size.y:0.00} x {b.size.z:0.00} m");
        return prefab;
    }

    // Âncoras dos cabos no próprio monitor (MonitorDisplay.portAnchors): o cabo acompanha o monitor certo mesmo depois
    // de uma troca por um do estoque. Só acrescenta dados; o resto do PC_Escritorio não muda.
    private static void MonitorAnchorsInPcPrefab()
    {
        string path = Gameplay + "PC_Escritorio.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            foreach (MonitorDisplay md in root.GetComponentsInChildren<MonitorDisplay>(true)) FillAnchors(md);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    internal static void FillAnchors(MonitorDisplay md)
    {
        var item = md.GetComponentInParent<PickupInteractable>(true);
        Transform top = item != null ? item.transform : md.transform.parent;
        var list = new List<(PcPort, Transform)>();
        foreach (Transform t in top.GetComponentsInChildren<Transform>(true))
            if (t.name.StartsWith("Porta3D_") && System.Enum.TryParse(t.name.Substring(8), out PcPort port) && ComputerPower.IsMonitorPort(port))
                list.Add((port, t));
        var so = new SerializedObject(md);
        SerializedProperty arr = so.FindProperty("portAnchors");
        arr.arraySize = list.Count;
        for (int i = 0; i < list.Count; i++)
        {
            arr.GetArrayElementAtIndex(i).FindPropertyRelative("port").enumValueIndex = (int)list[i].Item1;
            arr.GetArrayElementAtIndex(i).FindPropertyRelative("anchor").objectReferenceValue = list[i].Item2;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Monitor de estoque = o MESMO monitor do PC_Escritorio (tela 3D, traseira com portas, quebrável, pegável), mas sem
    // PC e com "Connect When Placed". Variante do OF_Monitor (o modelo do Matheus não muda).
    private static GameObject StockMonitorPrefab(Scene scene)
    {
        var pc = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Gameplay + "PC_Escritorio.prefab"), scene);
        PrefabUtility.UnpackPrefabInstance(pc, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
        MonitorDisplay md = pc.GetComponentInChildren<MonitorDisplay>(true);
        Transform mon = md.GetComponentInParent<PickupInteractable>(true).transform;
        mon.SetParent(null, true);
        mon.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        mon.name = "Monitor_Estoque";
        Object.DestroyImmediate(pc);
        var so = new SerializedObject(md);
        so.FindProperty("source").objectReferenceValue = null;
        so.FindProperty("connectWhenPlaced").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();
        FillAnchors(md);
        if (mon.GetComponent<MonitorAvulso>() == null) mon.gameObject.AddComponent<MonitorAvulso>();
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(mon.gameObject, MonitorPrefab);
        Object.DestroyImmediate(mon.gameObject);
        log.Add($"prefab Monitor_Estoque ({(PrefabUtility.IsPartOfVariantPrefab(prefab) ? "variante do OF_Monitor" : "prefab próprio")}), " +
                $"{prefab.GetComponentsInChildren<Component>(true).Select(c => c.GetType().Name).Distinct().Count()} tipos de componente");
        return prefab;
    }

    // ================================================================== sala de TI (pedidos pontuais)

    private static void SalaTI(Scene scene, GameObject puffPrefab, GameObject cartPrefab, GameObject monitorPrefab)
    {
        Transform salaTI = Find(scene, "Sala_TI");
        Transform comp = Find(scene, "Sala_TI_Complementos");
        Physics.SyncTransforms();

        // ---- puff: à esquerda dos armários para quem olha para eles, encostado na parede
        var armarios = salaTI.GetComponentsInChildren<Transform>(true)
            .Where(t => PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject) && Source(t) == "Armario_TI").ToList();
        Transform puff = Find(scene, "Puff_Amarelo");
        if (armarios.Count > 0)
        {
            Bounds ab = WorldBounds(armarios[0].gameObject);
            foreach (Transform a in armarios) ab.Encapsulate(WorldBounds(a.gameObject));
            Vector3 frente = Flat(armarios[0].forward);
            Vector3 sala = Flat(WorldBounds(salaTI.gameObject).center - ab.center);
            if (Vector3.Dot(frente, sala) < 0f) frente = -frente;   // a frente aponta para dentro da sala
            Vector3 esquerda = Vector3.Cross(Vector3.up, frente);   // esquerda de quem está de frente para os armários
            if (puff == null)
            {
                puff = ((GameObject)PrefabUtility.InstantiatePrefab(puffPrefab, scene)).transform;
                puff.name = "Puff_Amarelo";
            }
            puff.SetParent(comp.Find("Moveis"), true);
            Bounds pb = WorldBounds(puff.gameObject);
            float r = Mathf.Max(pb.extents.x, pb.extents.z);
            float lateral = Mathf.Abs(Vector3.Dot(ab.extents, Abs(esquerda)));
            float fundo = Mathf.Abs(Vector3.Dot(ab.extents, Abs(frente)));
            Vector3 p = ab.center + esquerda * (lateral + 0.10f + r) - frente * (fundo - r - 0.06f);
            p.y = 0f;
            puff.SetPositionAndRotation(p, Quaternion.LookRotation(frente));
            Physics.SyncTransforms();
            // se encostar em algo (parede torta, outro móvel), afasta para dentro da sala
            for (int i = 0; i < 10 && Blocked(puff.gameObject); i++) { puff.position += frente * 0.03f; Physics.SyncTransforms(); }
            log.Add($"puff em {puff.position:F2} ({(Blocked(puff.gameObject) ? "ENCOSTANDO EM ALGO" : "livre")})");
        }
        else log.Add("FALTA Armario_TI: puff não colocado");

        // ---- monitores reais nos 4 lugares da estante (2 por estante, prateleira de baixo)
        var estantes = salaTI.GetComponentsInChildren<Transform>(true)
            .Where(t => PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject) && Source(t) == "Estante_Estoque").ToList();
        var decor = salaTI.GetComponentsInChildren<Transform>(true)
            .Where(t => PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject) && Source(t) == "OF_Monitor" && t.GetComponentInChildren<MonitorDisplay>(true) == null).ToList();
        var existentes = salaTI.GetComponentsInChildren<MonitorDisplay>(true).Where(m => m.ConnectWhenPlaced)
            .Select(m => m.GetComponentInParent<PickupInteractable>(true).transform).ToList();
        var lugares = new List<(Vector3 pos, Quaternion rot, Transform pai, float baseY)>();
        foreach (Transform e in estantes)
        {
            var aqui = decor.Concat(existentes).Where(m => Flat(m.position - e.position).magnitude < 0.8f).ToList();
            if (aqui.Count == 0) { log.Add($"{e.name}: sem monitor de referência, lugares não definidos"); continue; }
            Transform refM = aqui[0];
            Bounds rb = WorldBounds(refM.gameObject);
            Vector3 eixo = Flat(e.right);
            float off = Vector3.Dot(Flat(rb.center - e.position), eixo);
            foreach (float s in new[] { -Mathf.Abs(off), Mathf.Abs(off) })
            {
                Vector3 c = e.position + eixo * s + Flat(e.forward) * Vector3.Dot(Flat(rb.center - e.position), Flat(e.forward));
                lugares.Add((new Vector3(c.x, 0f, c.z), refM.rotation, refM.parent, rb.min.y));
            }
        }
        foreach (Transform d in decor) Object.DestroyImmediate(d.gameObject);
        foreach (Transform d in existentes) Object.DestroyImmediate(d.gameObject);
        for (int i = 0; i < lugares.Count; i++)
        {
            var (pos, rot, pai, baseY) = lugares[i];
            var go = (GameObject)PrefabUtility.InstantiatePrefab(monitorPrefab, scene);
            go.name = "Monitor_Estoque_" + (i + 1);
            go.transform.SetParent(pai, true);
            go.transform.rotation = rot;
            go.transform.position = pos;
            Bounds b = WorldBounds(go);
            go.transform.position += new Vector3(pos.x - b.center.x, baseY - b.min.y, pos.z - b.center.z);
        }
        Physics.SyncTransforms();
        log.Add($"monitores reais na estante: {lugares.Count} (decorativos trocados: {decor.Count})");

        // monitor decorativo que vem DENTRO de outro modelo (ex.: "decoração paredes" do Matheus) no mesmo lugar de um
        // monitor real: só a peça dele é desligada NESTA cena (override reversível no Inspector; o prefab não muda)
        var reais = salaTI.GetComponentsInChildren<MonitorDisplay>(true).Select(m => m.GetComponentInParent<PickupInteractable>(true).transform).ToList();
        foreach (Transform real in reais)
        {
            Bounds rb = WorldBounds(real.gameObject);
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (MonitorDisplay outro in root.GetComponentsInChildren<MonitorDisplay>(false))
                {
                    if (outro.transform.IsChildOf(real) || outro.GetComponentInParent<PickupInteractable>(true) != null) continue;
                    Transform peca = outro.transform.parent != null ? outro.transform.parent : outro.transform;
                    Bounds ob = WorldBounds(peca.gameObject);
                    if (!ob.Intersects(rb) || ob.size.magnitude > 1.2f) continue;
                    peca.gameObject.SetActive(false);
                    log.Add($"monitor decorativo {PathOf(peca)} desligado nesta cena (estava no lugar de {real.name})");
                }
        }

        // ---- rack sobre o carrinho, no mesmo lugar
        RedeEstacaoInteractable rack = comp.GetComponentsInChildren<RedeEstacaoInteractable>(true)
            .FirstOrDefault(r => r.GetComponentInParent<StationEmployee>() == null && r.GetComponent<UnityEngine.Rigidbody>() == null
                                 && PrefabUtility.GetCorrespondingObjectFromSource(r.gameObject)?.name == "RackInternet");
        if (rack == null) { log.Add("FALTA RackInternet na Sala_TI_Complementos"); return; }
        Transform cart = Find(scene, "Carrinho_Rack");
        if (cart == null)
        {
            cart = ((GameObject)PrefabUtility.InstantiatePrefab(cartPrefab, scene)).transform;
            cart.name = "Carrinho_Rack";
            // o rack que já estava sobre um carrinho (rodando de novo) volta ao chão antes de recalcular
        }
        cart.SetParent(rack.transform.parent, true);
        Bounds rkb = WorldBounds(rack.gameObject);
        if (rkb.min.y > 0.2f) { rack.transform.position -= Vector3.up * rkb.min.y; rkb = WorldBounds(rack.gameObject); }
        Vector3 rackFrente = Flat(rack.transform.forward);
        cart.rotation = Quaternion.LookRotation(rackFrente);
        cart.position = new Vector3(rkb.center.x, 0f, rkb.center.z);
        Physics.SyncTransforms();
        // afasta da parede de trás até o carrinho não encostar nela
        for (int i = 0; i < 15 && Blocked(cart.gameObject, rack.transform); i++) { cart.position += rackFrente * 0.02f; Physics.SyncTransforms(); }
        float topo = TopY(cart);
        rack.transform.position += new Vector3(cart.position.x - rkb.center.x, topo + 0.002f - rkb.min.y, cart.position.z - rkb.center.z);
        Physics.SyncTransforms();
        Bounds cb = WorldBounds(cart.gameObject);
        Vector3 frenteRack = new Vector3(cart.position.x, 0.9f, cart.position.z) + rackFrente * (cb.extents.magnitude * 0.5f + 0.55f);
        bool livre = Physics.OverlapBox(frenteRack, new Vector3(0.3f, 0.7f, 0.3f), Quaternion.LookRotation(rackFrente), ~0, QueryTriggerInteraction.Ignore)
            .All(c => c.bounds.max.y < 0.05f);
        log.Add($"carrinho do rack em {cart.position:F2}, rack apoiado a y {topo:0.000} (topo do rack {WorldBounds(rack.gameObject).max.y:0.00} m); " +
                $"carrinho {(Blocked(cart.gameObject, rack.transform) ? "ENCOSTANDO EM ALGO" : "livre")}; frente do rack {(livre ? "livre" : "BLOQUEADA")}");
    }

    // topo do carrinho: raio de cima no centro, só nos colisores do carrinho; sem acerto, o topo dos renderers - 2 cm
    private static float TopY(Transform cart)
    {
        Bounds b = WorldBounds(cart.gameObject);
        foreach (RaycastHit h in Physics.RaycastAll(new Vector3(b.center.x, b.max.y + 1f, b.center.z), Vector3.down, 3f))
            if (h.collider.transform.IsChildOf(cart)) return Mathf.Min(h.point.y, b.max.y - 0.02f);
        return b.max.y - 0.02f;
    }

    // ================================================================== sistemas da Teste_Integrado

    private static void MoveSystems(Scene teste, Scene office)
    {
        var moved = new List<GameObject>();
        foreach (string name in MoveFromTeste)
        {
            if (Find(office, name) != null) { log.Add($"{name}: já existe na Office (mantido)"); continue; }
            Transform t = Find(teste, name);
            if (t == null) { log.Add($"FALTA {name} na Teste_Integrado"); continue; }
            t.SetParent(null, true);
            SceneManager.MoveGameObjectToScene(t.gameObject, office);
            moved.Add(t.gameObject);
        }
        // referências que apontavam para a Teste_Integrado: mesmo caminho e tipo na Office, senão vazias
        int ok = 0;
        var faltas = new List<string>();
        foreach (GameObject go in moved)
            foreach (Component c in go.GetComponentsInChildren<Component>(true))
            {
                if (c == null || c is Transform) continue;
                var so = new SerializedObject(c);
                SerializedProperty it = so.GetIterator();
                bool changed = false;
                while (it.Next(true))
                {
                    if (it.propertyType != SerializedPropertyType.ObjectReference || it.objectReferenceValue == null) continue;
                    Object o = it.objectReferenceValue;
                    GameObject g = o is GameObject gg ? gg : o is Component cc ? cc.gameObject : null;
                    if (g == null || g.scene != teste) continue;
                    Transform alvo = FindPath(office, PathOf(g.transform));
                    Object novo = alvo == null ? null : o is GameObject ? alvo.gameObject : (Object)alvo.GetComponent(o.GetType());
                    it.objectReferenceValue = novo;
                    changed = true;
                    if (novo != null) ok++; else faltas.Add($"{go.name}: {c.GetType().Name}.{it.propertyPath} ({PathOf(g.transform)})");
                }
                if (changed) so.ApplyModifiedPropertiesWithoutUndo();
            }
        log.Add($"movidos da Teste_Integrado: {string.Join(", ", moved.Select(m => m.name))}; {ok} referência(s) religada(s) à Office");
        foreach (string f in faltas) log.Add("  sem equivalente na Office (refeito abaixo): " + f);
    }

    private static void Systems(Scene scene)
    {
        foreach (string n in new[] { "HUD", "ScreenCanvas" })
        {
            Transform t = Find(scene, n);
            if (t != null && !t.gameObject.activeSelf) { t.gameObject.SetActive(true); log.Add(n + " ligado"); }
        }
        StageController stage = Object.FindAnyObjectByType<StageController>(FindObjectsInactive.Include);
        var so = new SerializedObject(stage);
        so.FindProperty("showStartMenu").boolValue = true;   // cena oficial: a partida começa pelo menu inicial
        so.ApplyModifiedPropertiesWithoutUndo();
        log.Add("menu inicial ligado no StageController (a fase só começa ao escolher o modo)");
    }

    // ================================================================== NavMesh

    internal static void Bake(Scene scene)
    {
        var surface = Object.FindAnyObjectByType<Unity.AI.Navigation.NavMeshSurface>(FindObjectsInactive.Include);
        if (surface == null) { log.Add("FALTA NavMeshSurface"); return; }
        Bounds mapa = WorldBounds(Find(scene, "MAPA").gameObject);
        surface.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        surface.collectObjects = Unity.AI.Navigation.CollectObjects.Volume;
        surface.center = new Vector3(mapa.center.x, 1f, mapa.center.z);   // piso até 3 m: teto e forros ficam de fora
        surface.size = new Vector3(mapa.size.x + 1f, 4f, mapa.size.z + 1f);
        Physics.SyncTransforms();
        surface.BuildNavMesh();
        if (surface.navMeshData == null) { log.Add("NavMesh saiu vazio"); return; }
        if (AssetDatabase.LoadAssetAtPath<NavMeshData>(NavAsset) != null) AssetDatabase.DeleteAsset(NavAsset);
        AssetDatabase.CreateAsset(surface.navMeshData, NavAsset);
        EditorUtility.SetDirty(surface);
        NavMeshTriangulation tri = NavMesh.CalculateTriangulation();
        log.Add($"NavMesh assado ({tri.indices.Length / 3} triângulos) em {NavAsset}");
    }

    // ================================================================== estações

    internal static List<Transform> StationRoots(Scene scene)
    {
        var roots = new List<Transform>();
        foreach (GameObject r in scene.GetRootGameObjects())
            foreach (Component c in r.GetComponentsInChildren<ComputerCase>(true).Cast<Component>()
                         .Concat(r.GetComponentsInChildren<NotebookEstacao>(true)))
            {
                Transform t = c.transform;
                while (t != null && !t.name.StartsWith("Estacao")) t = t.parent;
                if (t != null && t.gameObject.activeInHierarchy && !roots.Contains(t)) roots.Add(t);
            }
        // desktops primeiro; depois por posição (estável ao rodar de novo)
        return roots.OrderBy(t => t.GetComponentInChildren<ComputerCase>() != null ? 0 : 1)
            .ThenBy(t => t.position.x).ThenBy(t => t.position.z).ToList();
    }

    private static void Stations(Scene scene)
    {
        TicketManager tickets = Object.FindAnyObjectByType<TicketManager>(FindObjectsInactive.Include);
        ScoreKeeper score = tickets.GetComponent<ScoreKeeper>();
        string[] pcIds = Field<string[]>("PcTickets").Concat(Field<string[]>("RedePcTickets")).ToArray();
        string[] nbIds = Field<string[]>("NotebookTickets");
        var placa = AssetDatabase.LoadAssetAtPath<GameObject>(Props + "Placa_Mesa.prefab");
        var placaFn = typeof(CorrecoesRodada2Setup).GetMethod("ColocarPlaca", BindingFlags.NonPublic | BindingFlags.Static);
        var placaLog = new List<string>();

        List<Transform> stations = StationRoots(scene);
        int porta = 0;
        for (int i = 0; i < stations.Count; i++)
        {
            Transform st = stations[i];
            string letra = Letra(st, i);
            var (nome, setor) = Funcionarios[i % Funcionarios.Length];
            var emp = st.GetComponent<StationEmployee>(); if (emp == null) emp = st.gameObject.AddComponent<StationEmployee>();
            emp.employeeName = nome;
            emp.department = setor;
            EditorUtility.SetDirty(emp);
            Transform npc = EnsureNpc(st, i, scene);
            if (placa != null) placaFn.Invoke(null, new object[] { st, placa, scene, placaLog });

            ComputerCase pcCase = st.GetComponentInChildren<ComputerCase>();
            if (pcCase != null)
            {
                MakeFunctional(pcCase);
                Bridge<PcTicketBridge>(pcCase.gameObject, tickets, $"PC da Estação {letra}", pcIds, npc,
                    so => so.FindProperty("scoreKeeper").objectReferenceValue = score);
                var wall = st.GetComponentInChildren<WallConnectionPoint>();
                if (wall != null)
                {
                    var link = wall.GetComponent<PontoDeRedeRack>(); if (link == null) link = wall.gameObject.AddComponent<PontoDeRedeRack>();
                    var so = new SerializedObject(link);
                    so.FindProperty("portaRack").intValue = ++porta;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                log.Add($"{st.name} = Estação {letra}: desktop de {nome} ({setor}), porta {porta} do rack, NPC em {npc.position:F2}");
            }
            else
            {
                NotebookEstacao nb = st.GetComponentInChildren<NotebookEstacao>();
                Bridge<NotebookTicketBridge>(nb.gameObject, tickets, $"Notebook da Estação {letra}", nbIds, npc, Extra("NotebookExtra"));
                log.Add($"{st.name} = Estação {letra}: notebook de {nome} ({setor}), NPC em {npc.position:F2}");
            }
        }
        foreach (string l in placaLog) log.Add("  " + l);
    }

    private static string Letra(Transform st, int i)
    {
        // Estacao_A_Desktop -> A; outros nomes (ex.: a estação do Matheus) recebem a próxima letra livre
        string[] p = st.name.Split('_');
        if (p.Length >= 2 && p[1].Length == 1) return p[1];
        return "B";
    }

    internal static void MakeFunctional(ComputerCase pcCase)
    {
        var soCase = new SerializedObject(pcCase);
        soCase.FindProperty("randomizeComponents").boolValue = false;
        soCase.FindProperty("fixedComponents").intValue = (int)ComputerCase.AllComponents;
        soCase.FindProperty("startingHddHasOperatingSystem").boolValue = true;
        soCase.ApplyModifiedPropertiesWithoutUndo();
        var estado = pcCase.GetComponent<PcEstadoInicial>();
        if (estado == null) return;
        var so = new SerializedObject(estado);
        so.FindProperty("chanceCaboSolto").floatValue = 0f;
        so.FindProperty("ligarAoIniciar").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // NPC da estação: de pé ao lado da cadeira, num ponto livre (sem colisor) e no NavMesh, olhando para a mesa.
    private static Transform EnsureNpc(Transform station, int index, Scene scene)
    {
        Transform npc = station.Find("NPC_Estacao");
        if (npc == null)
        {
            var go = new GameObject("NPC_Estacao");
            SceneManager.MoveGameObjectToScene(go, scene);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(NpcModels[index % NpcModels.Length]);
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
            visual.name = "Visual";
            visual.transform.SetParent(go.transform, false);
            Bounds vb = WorldBounds(visual);
            if (vb.size.y > 0.01f) visual.transform.localScale *= NpcHeight / vb.size.y;
            vb = WorldBounds(visual);
            visual.transform.position += new Vector3(-vb.center.x, -vb.min.y, -vb.center.z);
            var obs = go.AddComponent<NavMeshObstacle>();
            obs.shape = NavMeshObstacleShape.Capsule;
            obs.radius = 0.3f;
            obs.height = NpcHeight;
            obs.center = new Vector3(0f, NpcHeight / 2f, 0f);
            obs.carving = true;
            go.transform.SetParent(station, true);
            npc = go.transform;
        }
        WorkSurface mesa = station.GetComponentInChildren<WorkSurface>();
        Bounds db = WorldBounds(mesa.gameObject);
        Transform cadeira = station.Find("Cadeira");
        Vector3 c = cadeira != null ? cadeira.position : db.center - station.forward;
        Vector3 paraCadeira = Flat(c - db.center).normalized;
        Vector3 lado = Vector3.Cross(Vector3.up, paraCadeira);
        Transform equip = station.GetComponentInChildren<ComputerCase>() != null ? station.GetComponentInChildren<ComputerCase>().transform
            : station.GetComponentInChildren<NotebookEstacao>().transform;
        float sinal = Vector3.Dot(equip.position - db.center, lado) > 0f ? -1f : 1f;   // lado oposto ao equipamento
        Vector3 preferido = c + lado * sinal * 0.75f;
        var cand = new List<Vector3>();
        foreach (float s in new[] { sinal, -sinal })
            foreach (float a in new[] { 0.6f, 0.75f, 0.9f, 1.1f })
                foreach (float f in new[] { 0f, 0.3f, -0.3f, 0.6f })
                    cand.Add(c + lado * s * a + paraCadeira * f);
        Physics.SyncTransforms();
        Vector3 melhor = preferido;
        bool achou = false;
        foreach (Vector3 p in cand.OrderBy(p => (p - preferido).sqrMagnitude))
        {
            Vector3 q = new Vector3(p.x, 0f, p.z);
            bool livre = Physics.OverlapCapsule(q + Vector3.up * 0.35f, q + Vector3.up * 1.5f, 0.3f, ~0, QueryTriggerInteraction.Ignore)
                .All(col => col.bounds.max.y < 0.05f || col.transform.IsChildOf(npc));
            if (!livre || !NavMesh.SamplePosition(q, out _, 0.3f, NavMesh.AllAreas)) continue;
            melhor = q; achou = true; break;
        }
        if (!achou) log.Add($"  {station.name}: NPC sem ponto livre, ficou no lugar padrão");
        npc.position = melhor;
        Vector3 olhar = Flat(db.center - melhor);
        if (olhar.sqrMagnitude > 1e-4f) npc.rotation = Quaternion.LookRotation(olhar.normalized);
        return npc;
    }

    internal static void Bridge<T>(GameObject go, TicketManager tickets, string label, string[] ids, Transform npc,
                                  System.Action<SerializedObject> extra) where T : TicketDeviceBridge
    {
        T bridge = go.GetComponent<T>(); if (bridge == null) bridge = go.AddComponent<T>();
        var so = new SerializedObject(bridge);
        so.FindProperty("ticketManager").objectReferenceValue = tickets;
        so.FindProperty("deviceLabel").stringValue = label;
        so.FindProperty("stationNpc").objectReferenceValue = npc;
        SerializedProperty acc = so.FindProperty("acceptedTicketIds");
        acc.arraySize = ids.Length;
        for (int i = 0; i < ids.Length; i++) acc.GetArrayElementAtIndex(i).stringValue = ids[i];
        extra?.Invoke(so);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ================================================================== impressora, caixa de cabo, bancada, estoque

    private static void Devices(Scene scene)
    {
        TicketManager tickets = Object.FindAnyObjectByType<TicketManager>(FindObjectsInactive.Include);
        foreach (ImpressoraEstacao pr in Object.FindObjectsByType<ImpressoraEstacao>(FindObjectsSortMode.None))
            if (pr.GetComponentInParent<StationEmployee>() == null)
            {
                Bridge<ImpressoraTicketBridge>(pr.gameObject, tickets, "Impressora da área de impressão", Field<string[]>("PrinterTickets"), null, Extra("PrinterExtra"));
                log.Add($"impressora {pr.name} ligada aos chamados de impressora");
            }

        // caixa de cabo de rede (crimpagem) e testador: sobre a bancada com armários da sala de TI, em trecho livre
        WorkSurface bancada = Find(scene, "Sala_TI_Complementos").GetComponentsInChildren<WorkSurface>()
            .OrderByDescending(w => w.transform.position.z).FirstOrDefault();
        Transform caixa = Find(scene, "Caixa_CaboRede"), testador = Find(scene, "Testador_CaboRede");
        if (bancada != null)
        {
            Transform pai = bancada.transform.parent;
            // a caixa de cabo (0,67 m) não cabe no tampo de 0,60 m: fica no chão da sala, encostada numa parede
            if (caixa != null)
            {
                caixa.SetParent(pai, true);
                log.Add($"{caixa.name}: " + (PlaceOnFloor(scene, caixa, testador != null ? testador : bancada.transform) ? $"no chão em {caixa.position:F2}" : "SEM LUGAR LIVRE NO CHÃO"));
            }
            if (testador != null)
            {
                testador.SetParent(pai, true);
                log.Add($"{testador.name} sobre {bancada.name}: " + (PutOnSurface(testador, bancada) ? $"em {testador.position:F2}" : "SEM TRECHO LIVRE"));
            }
        }
        var rede = caixa != null ? caixa.GetComponent<RedeTicketBridge>() : null;
        if (rede != null)
        {
            var so = new SerializedObject(rede);
            so.FindProperty("ticketManager").objectReferenceValue = tickets;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // retirada dos equipamentos da estação nova (montagem completa): bancada de manutenção
        typeof(CorrecoesRodada4Setup).GetMethod("PontoDeRetirada", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { log });
        // reposição: gavetas + peças soltas de impressora/notebook (toner e resmas da área de impressão)
        typeof(TesteIntegradoSetup).GetMethod("ConfigureRestock", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { tickets.gameObject, log });
    }

    // Chão da sala de TI: ponto livre encostado numa parede (até 25 cm dela), fora da porta e da frente dos móveis da sala
    // (0,9 m à frente de cada um, onde o jogador fica para usá-los); o mais perto de 'perto'.
    internal static bool PlaceOnFloor(Scene scene, Transform item, Transform perto)
    {
        Physics.SyncTransforms();
        Transform sala = Find(scene, "Sala_TI"), comp = Find(scene, "Sala_TI_Complementos");
        Bounds room = WorldBounds(sala.gameObject);
        room.Encapsulate(WorldBounds(comp.Find("Moveis").gameObject));
        var zonas = new List<Bounds>();
        foreach (Transform grupo in new[] { sala.Find("Moveis"), comp.Find("Moveis") })
            foreach (Transform m in grupo)
            {
                if (m == item || m.GetComponent<Rigidbody>() != null) continue;
                Bounds b = WorldBounds(m.gameObject);
                Vector3 f = Flat(m.forward).normalized;
                b.Encapsulate(b.center + f * (Mathf.Abs(Vector3.Dot(b.extents, Abs(f))) + 0.9f));
                zonas.Add(b);
            }
        Transform placa = Find(scene, "Placa_SalaTI");   // a placa fica do lado de fora, ao lado da porta
        Bounds ib = WorldBounds(item.gameObject);
        Vector3 half = ib.extents + new Vector3(0.03f, -0.02f, 0.03f);
        var cand = new List<Vector3>();
        for (float x = room.min.x; x <= room.max.x; x += 0.05f)
            for (float z = room.min.z; z <= room.max.z; z += 0.05f)
                cand.Add(new Vector3(x, ib.extents.y + 0.02f, z));
        foreach (Vector3 c in cand.OrderBy(c => Flat(c - perto.position).sqrMagnitude))
        {
            var b = new Bounds(c, half * 2f);
            if (zonas.Any(z => z.Intersects(b))) continue;
            if (Physics.OverlapBox(c, half, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)
                    .Any(col => !col.transform.IsChildOf(item) && col.bounds.max.y > 0.05f)) continue;
            // encostada: uma parede a até 25 cm de um dos lados
            bool parede = false;
            foreach (Vector3 d in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
                if (Physics.Raycast(c, d, out RaycastHit h, Mathf.Abs(Vector3.Dot(half, d)) + 0.25f, ~0, QueryTriggerInteraction.Ignore)
                    && h.collider.transform.IsChildOf(Find(scene, "MAPA"))) parede = true;
            if (!parede) continue;
            // fora do vão da porta (onde a placa externa indica a entrada)
            if (placa != null && Flat(c - placa.position).magnitude < 1.6f) continue;
            item.position += new Vector3(c.x - ib.center.x, 0.005f - ib.min.y, c.z - ib.center.z);
            return true;
        }
        return false;
    }

    // Apoia o objeto no tampo, no primeiro trecho livre (varre o comprimento a partir da ponta +X da bancada).
    internal static bool PutOnSurface(Transform item, WorkSurface surface)
    {
        Physics.SyncTransforms();
        Bounds sb = WorldBounds(surface.gameObject);
        Bounds ib = WorldBounds(item.gameObject);
        float topo = sb.max.y;
        foreach (RaycastHit h in Physics.RaycastAll(new Vector3(sb.center.x, sb.max.y + 1f, sb.center.z), Vector3.down, 3f))
            if (h.collider.transform.IsChildOf(surface.transform)) { topo = h.point.y; break; }
        bool eixoX = sb.size.x >= sb.size.z;
        Vector3 ao = eixoX ? Vector3.right : Vector3.forward;
        float comp = eixoX ? sb.size.x : sb.size.z;
        Vector3 fundo = eixoX ? Vector3.forward : Vector3.right;
        float prof = eixoX ? sb.size.z : sb.size.x;
        Vector3 half = new Vector3(ib.extents.x + 0.02f, ib.extents.y, ib.extents.z + 0.02f);
        float margem = Mathf.Abs(Vector3.Dot(half, fundo));
        foreach (float d in new[] { 0f, 0.08f, -0.08f, 0.16f, -0.16f })
        for (float s = comp / 2 - Mathf.Max(half.x, half.z) - 0.05f; s > -comp / 2; s -= 0.05f)
        {
            if (Mathf.Abs(d) + margem > prof / 2) continue;   // não passa da borda do tampo
            Vector3 c = sb.center + ao * s + fundo * d;
            Vector3 centro = new Vector3(c.x, topo + 0.01f + ib.extents.y, c.z);
            bool livre = Physics.OverlapBox(centro, half, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)
                .All(col => col.transform.IsChildOf(surface.transform) || col.transform.IsChildOf(item));
            if (!livre) continue;
            item.position += centro - new Vector3(ib.center.x, ib.center.y - 0.005f, ib.center.z);
            return true;
        }
        return false;
    }

    // ================================================================== supervisor

    private static void Patrol(Scene scene)
    {
        Transform ronda = Find(scene, "Ronda_Supervisor");
        if (ronda == null) { ronda = new GameObject("Ronda_Supervisor").transform; SceneManager.MoveGameObjectToScene(ronda.gameObject, scene); }
        ronda.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        Transform player = Find(scene, "Player");

        // área de trabalho = estações + impressora + porta da sala de TI, com 3 m de folga
        var marcos = StationRoots(scene).Select(t => t.position).ToList();
        foreach (ImpressoraEstacao p in Object.FindObjectsByType<ImpressoraEstacao>(FindObjectsSortMode.None)) marcos.Add(p.transform.position);
        Bounds area = new Bounds(marcos[0], Vector3.zero);
        foreach (Vector3 m in marcos) area.Encapsulate(m);
        area.Expand(new Vector3(6f, 10f, 6f));
        Bounds salaTI = WorldBounds(Find(scene, "Sala_TI").gameObject);
        NavMesh.SamplePosition(player.position, out NavMeshHit inicio, 2f, NavMesh.AllAreas);

        var pontos = new List<Vector3>();
        for (float x = area.min.x; x <= area.max.x; x += 1f)
            for (float z = area.min.z; z <= area.max.z; z += 1f)
            {
                if (!NavMesh.SamplePosition(new Vector3(x, 0f, z), out NavMeshHit h, 0.4f, NavMesh.AllAreas) || h.position.y > 0.3f) continue;
                if (salaTI.Contains(new Vector3(h.position.x, salaTI.center.y, h.position.z))) continue;
                if (!NavMesh.FindClosestEdge(h.position, out NavMeshHit borda, NavMesh.AllAreas) || borda.distance < 0.7f) continue;
                var path = new NavMeshPath();
                if (!NavMesh.CalculatePath(inicio.position, h.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                pontos.Add(h.position);
            }
        // 8 pontos espalhados (o mais distante dos já escolhidos), percorridos em volta
        var escolhidos = new List<Vector3>();
        if (pontos.Count > 0) escolhidos.Add(pontos.OrderBy(p => (p - area.center).sqrMagnitude).First());
        while (escolhidos.Count < 8 && escolhidos.Count < pontos.Count)
            escolhidos.Add(pontos.OrderByDescending(p => escolhidos.Min(e => (p - e).sqrMagnitude)).First());
        Vector3 meio = escolhidos.Aggregate(Vector3.zero, (a, b) => a + b) / Mathf.Max(1, escolhidos.Count);
        escolhidos = escolhidos.OrderBy(p => Mathf.Atan2(p.z - meio.z, p.x - meio.x)).ToList();

        for (int i = ronda.childCount - 1; i >= 0; i--) Object.DestroyImmediate(ronda.GetChild(i).gameObject);
        for (int i = 0; i < escolhidos.Count; i++)
        {
            var p = new GameObject("Ponto_" + (i + 1)).transform;
            p.SetParent(ronda, false);
            p.position = escolhidos[i] + Vector3.up * 0.05f;
        }

        SupervisorPatrol boss = Object.FindAnyObjectByType<SupervisorPatrol>(FindObjectsInactive.Include);
        if (boss == null) { log.Add("FALTA NPC_Supervisor"); return; }
        var so = new SerializedObject(boss);
        so.FindProperty("route").objectReferenceValue = ronda;
        so.FindProperty("player").objectReferenceValue = player;
        SerializedProperty ip = so.FindProperty("interestPoints");
        var interesse = StationRoots(scene).ToList();
        WorkSurface manut = Find(scene, "Sala_TI").GetComponentInChildren<WorkSurface>();
        if (manut != null) interesse.Add(manut.transform);
        foreach (ImpressoraEstacao p in Object.FindObjectsByType<ImpressoraEstacao>(FindObjectsSortMode.None)) interesse.Add(p.transform);
        ip.arraySize = interesse.Count;
        for (int i = 0; i < interesse.Count; i++) ip.GetArrayElementAtIndex(i).objectReferenceValue = interesse[i];
        so.ApplyModifiedPropertiesWithoutUndo();
        var per = boss.GetComponent<SupervisorPerception>();
        if (per != null)
        {
            var sp = new SerializedObject(per);
            sp.FindProperty("player").objectReferenceValue = player;
            sp.ApplyModifiedPropertiesWithoutUndo();
        }
        if (escolhidos.Count > 0) boss.transform.position = escolhidos[0] + Vector3.up * 0.05f;
        log.Add($"ronda do supervisor: {escolhidos.Count} pontos ({pontos.Count} candidatos alcançáveis); {interesse.Count} pontos de interesse");
    }

    // ================================================================== validação

    private static void Validate(Scene scene)
    {
        var problemas = new List<string>();
        Physics.SyncTransforms();
        Transform player = Find(scene, "Player");
        var cc = player.GetComponent<CharacterController>();
        if (!NavMesh.SamplePosition(player.position, out NavMeshHit inicio, 2f, NavMesh.AllAreas)) problemas.Add("Player fora do NavMesh");

        void Alcance(string nome, Vector3 alvo)
        {
            if (!NavMesh.SamplePosition(alvo, out NavMeshHit h, 1.0f, NavMesh.AllAreas)) { problemas.Add($"{nome}: sem NavMesh perto"); return; }
            var path = new NavMeshPath();
            bool ok = NavMesh.CalculatePath(inicio.position, h.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
            log.Add($"  alcance {nome}: {(ok ? "ok" : "SEM CAMINHO")} ({Flat(h.position - alvo).magnitude:0.00} m do alvo)");
            if (!ok) problemas.Add(nome + " inalcançável");
        }
        log.Add("VALIDAÇÃO");
        foreach (Transform st in StationRoots(scene))
        {
            Transform cad = st.Find("Cadeira");
            Alcance(st.name, cad != null ? cad.position + Flat(cad.position - WorldBounds(st.GetComponentInChildren<WorkSurface>().gameObject).center).normalized * 0.5f : st.position);
        }
        foreach (ImpressoraEstacao p in Object.FindObjectsByType<ImpressoraEstacao>(FindObjectsSortMode.None))
            Alcance("impressora", p.transform.position + Flat(p.transform.forward) * 0.8f);
        Transform rack = Find(scene, "RackInternet");
        if (rack != null) Alcance("frente do rack", rack.position + Flat(rack.forward) * 0.8f);
        WorkSurface manut = Find(scene, "Sala_TI").GetComponentInChildren<WorkSurface>();
        if (manut != null) Alcance("bancada de manutenção", WorldBounds(manut.gameObject).center + Flat(manut.transform.forward) * 0.8f);
        foreach (MonitorDisplay m in Object.FindObjectsByType<MonitorDisplay>(FindObjectsSortMode.None).Where(m => m.ConnectWhenPlaced))
            Alcance(m.GetComponentInParent<PickupInteractable>().name, m.transform.position + Flat(m.transform.parent.parent.forward) * 0.9f);
        Transform puff = Find(scene, "Puff_Amarelo");
        if (puff != null && Blocked(puff.gameObject)) problemas.Add("puff encostando em algo");

        // sistemas: um de cada
        void Um<T>() where T : Object
        {
            int n = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            log.Add($"  {typeof(T).Name}: {n}");
            if (n != 1) problemas.Add($"{typeof(T).Name}: {n} (esperado 1)");
        }
        Um<TicketManager>(); Um<StageController>(); Um<ScoreKeeper>(); Um<TicketTabletPrototype>(); Um<StageTimerHUD>();
        Um<UnityEngine.EventSystems.EventSystem>(); Um<UIScreenManager>(); Um<ConductMonitor>(); Um<RedeGame>(); Um<SupervisorPatrol>();
        Um<PlayerMovement>(); Um<InteractionSoundPlayer>();

        int pontes = 0;
        foreach (TicketDeviceBridge b in Object.FindObjectsByType<TicketDeviceBridge>(FindObjectsSortMode.None))
        {
            pontes++;
            var so = new SerializedObject(b);
            if (so.FindProperty("ticketManager").objectReferenceValue == null) problemas.Add($"{b.name}: ponte sem TicketManager");
        }
        log.Add($"  pontes de chamado: {pontes}");
        // telas: toda interação de tela aponta para uma tela da cena
        foreach (UIScreenInteractable u in Object.FindObjectsByType<UIScreenInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var s = new SerializedObject(u).FindProperty("screen").objectReferenceValue as Component;
            if (s == null) problemas.Add($"{PathOf(u.transform)}: sem tela");
            else if (!s.gameObject.scene.IsValid()) log.Add($"  {PathOf(u.transform)}: tela = prefab {s.name} (instanciada ao abrir)");
        }
        // scripts ausentes na cena
        foreach (GameObject r in scene.GetRootGameObjects())
            foreach (Component c in r.GetComponentsInChildren<Component>(true))
                if (c == null) problemas.Add($"script ausente em {PathOf(r.transform)}");
        log.Add($"VALIDAÇÃO: {problemas.Count} problema(s)");
        foreach (string p in problemas) log.Add("  PROBLEMA: " + p);
    }

    // ================================================================== util

    internal static T Field<T>(string name) =>
        (T)typeof(TesteIntegradoSetup).GetField(name, BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);

    internal static System.Action<SerializedObject> Extra(string name)
    {
        MethodInfo m = typeof(TesteIntegradoSetup).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
        return so => m.Invoke(null, new object[] { so });
    }

    internal static bool Blocked(GameObject go, Transform ignore = null)
    {
        Bounds b = WorldBounds(go);
        Vector3 half = b.extents - new Vector3(0.01f, 0.01f, 0.01f);
        Vector3 c = b.center + Vector3.up * 0.02f;
        return Physics.OverlapBox(c, half, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)
            .Any(col => !col.transform.IsChildOf(go.transform) && (ignore == null || !col.transform.IsChildOf(ignore)) && col.bounds.max.y > 0.05f);
    }

    private static string Source(Transform t)
    {
        var src = PrefabUtility.GetCorrespondingObjectFromOriginalSource(t.gameObject);
        return src != null ? src.name : null;
    }

    internal static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
    private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

    internal static Bounds WorldBounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>(true).Where(r => !(r is ParticleSystemRenderer)).ToArray();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        Bounds b = rs[0].bounds;
        foreach (Renderer r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    internal static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;

    private static Transform FindPath(Scene scene, string path)
    {
        string[] parts = path.Split('/');
        GameObject root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == parts[0]);
        if (root == null) return null;
        Transform t = root.transform;
        for (int i = 1; i < parts.Length && t != null; i++) t = t.Find(parts[i]);
        return t;
    }

    internal static Transform Find(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == name) return root.transform;
            Transform t = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == name);
            if (t != null) return t;
        }
        return null;
    }
}
