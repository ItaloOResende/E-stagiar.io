using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Correções da demo de 19/10 na <c>Office_Placeholder</c> (relatório em <c>Logs/Integracao1910.txt</c>; backup da cena
/// em <c>Art/_Backup/2026-10-19_integracao/</c>). Idempotente. Menu: Tools &gt; E-stagiar.io &gt; Integração 19-10.
///  1. NPCs: modelo centrado no agente e virado para onde anda; colisor do tamanho do agente com Rigidbody cinemático
///     (o jogador não atravessa); NavMeshObstacle do jogador sem carving (o furo no NavMesh empurrava os agentes).
///  2. NavMesh: volume só da área da demo (até a parede do corredor) e re-assado.
///  3. Gaveteiros da sala de TI: os 3 de 4 gavetas viram os de 3 gavetas do Matheus (<c>gavetas.fbx</c>), prefabs
///     <c>Gaveteiro3_*</c>; os prefabs antigos (<c>Gaveteiro_*</c>) ficam no projeto como referência.
///  4. Tablet menor (escala 0,8) e fonte do HUD (Varela Round) no cronômetro/pontuação.
/// </summary>
public static class Integracao1910Setup
{
    private const string Cena = "Assets/Scenes/Office_Placeholder.unity";
    private const string Gameplay = "Assets/Prefabs/Gameplay/";
    private const string Modelo = "Assets/Art/Models/gavetas.fbx";
    private const string Labels = "Assets/Art/Textures/Plaquinhas/";
    private const string Mats = "Assets/Art/Materials/";
    private static readonly StringBuilder log = new StringBuilder();

    [MenuItem("Tools/E-stagiar.io/Integração 19-10 (NPCs, NavMesh, gaveteiros, HUD)")]
    public static void Rodar()
    {
        log.Clear();
        var prefabs = MontarGaveteiros();
        var scene = EditorSceneManager.OpenScene(Cena, OpenSceneMode.Single);
        Npcs();
        TrocarGaveteiros(prefabs);
        Hud();
        Plaquinhas();
        NavMeshDaDemo();
        PontosAlcancaveis();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Validar();
        AssetDatabase.SaveAssets();
        System.IO.Directory.CreateDirectory("Logs");
        System.IO.File.WriteAllText("Logs/Integracao1910.txt", log.ToString());
        Debug.Log("Integracao1910Setup: concluído (Logs/Integracao1910.txt).");
    }

    // ------------------------------------------------------------------ 1. NPCs
    // Rotação local do modelo para ele olhar para +Z do root (a direção em que o NavMeshAgent anda), por FBX.
    // Família npc*.fbx: (270, 180, 0) com escala negativa olha para −Z (os sentados olham a mesa assim) → (270, 0, 0).
    private static readonly Dictionary<string, Vector3> RotacaoDoModelo = new Dictionary<string, Vector3>
    {
        { "npc3", new Vector3(270f, 0f, 0f) },
        { "npc 8", new Vector3(270f, 0f, 0f) },   // estava (270, 90, 0): andava de perfil
        { "npc 6", new Vector3(90f, 0f, 0f) },     // estava (90, 270, 0): andava de lado (corpo deslocado em X, não em +Z)
    };

    // Agente estreito (cabe nos corredores entre as estações); colisor do corpo mais largo, do tamanho dos modelos (o
    // jogador não entra no NPC). Velocidade de caminhada calma; a rotação é do NpcCaminhada (pela velocidade real).
    private const float RaioNpc = 0.3f, RaioCorpo = 0.4f, AlturaNpc = 1.75f;
    private const float VelocidadeCaminhante = 0.85f, VelocidadeChefe = 1.0f, Aceleracao = 3f, Giro = 200f;

    private static void Npcs()
    {
        log.AppendLine("== NPCs");
        foreach (NavMeshAgent ag in Object.FindObjectsByType<NavMeshAgent>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            GameObject go = ag.gameObject;
            if (go.GetComponent<NpcCaminhada>() == null && go.GetComponent<SupervisorPatrol>() == null) continue;
            bool supervisor = go.GetComponent<SupervisorPatrol>() != null;

            // modelo: o filho com renderers
            Transform visual = go.transform.Cast<Transform>().FirstOrDefault(c => c.GetComponentInChildren<Renderer>(true) != null);
            if (visual != null)
            {
                string fbx = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromOriginalSource(visual.gameObject)));
                if (!supervisor && fbx != null && RotacaoDoModelo.TryGetValue(fbx, out Vector3 rot)) visual.localEulerAngles = rot;
                Bounds b = Limites(visual);
                Vector3 p = go.transform.position;
                Vector3 desloc = new Vector3(p.x - b.center.x, p.y - b.min.y, p.z - b.center.z);
                if (desloc.magnitude > 0.02f) visual.position += desloc;
                log.AppendLine($"{go.name}: modelo {visual.name} ({fbx}) deslocado {desloc:F2}, rot {visual.localEulerAngles:F0}, limites {Limites(visual).size:F2}");
            }

            ag.radius = RaioNpc;
            ag.height = AlturaNpc;
            ag.baseOffset = 0f;
            ag.acceleration = Aceleracao;
            ag.angularSpeed = Giro;
            ag.speed = supervisor ? VelocidadeChefe : VelocidadeCaminhante;
            if (!supervisor) ag.stoppingDistance = 0.4f;
            ag.autoBraking = true;
            ag.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;

            CapsuleCollider cap = go.GetComponent<CapsuleCollider>();
            if (cap == null) cap = go.AddComponent<CapsuleCollider>();
            cap.isTrigger = false;
            cap.radius = RaioCorpo;
            cap.height = AlturaNpc;
            cap.center = new Vector3(0f, AlturaNpc / 2f, 0f);
            Rigidbody rb = go.GetComponent<Rigidbody>();
            if (rb == null) rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.None;
            if (go.GetComponent<NavMeshModifier>() == null) go.AddComponent<NavMeshModifier>();
            var mod = go.GetComponent<NavMeshModifier>();
            mod.ignoreFromBuild = true;
            mod.applyToChildren = true;
            EditorUtility.SetDirty(go);
            log.AppendLine($"   agente r {ag.radius} h {ag.height} vel {ag.speed} acel {ag.acceleration} giro {ag.angularSpeed}; colisor r {cap.radius}; Rigidbody cinemático");
        }

        EspalharCaminhantes();

        foreach (NavMeshObstacle obs in Object.FindObjectsByType<NavMeshObstacle>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (obs.GetComponent<CharacterController>() != null)
            {
                obs.carving = false;
                EditorUtility.SetDirty(obs);
                log.AppendLine($"{obs.name}: NavMeshObstacle sem carving (os agentes desviam sem serem empurrados pelo furo)");
            }

        foreach (NpcSentado s in Object.FindObjectsByType<NpcSentado>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            StationEmployee est = Object.FindObjectsByType<StationEmployee>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .OrderBy(e => e.transform.Find("Cadeira") != null ? Vector3.Distance(e.transform.Find("Cadeira").position, s.transform.position) : 99f).FirstOrDefault();
            Transform cad = est != null ? est.transform.Find("Cadeira") : null;
            float d = cad != null ? Vector2.Distance(new Vector2(cad.position.x, cad.position.z), new Vector2(s.transform.position.x, s.transform.position.z)) : -1f;
            log.AppendLine($"{s.name}: sentado na cadeira da {(est != null ? est.name + " (" + new SerializedObject(est).FindProperty("employeeName").stringValue + ")" : "?")} a {d:F2} m");
        }
    }

    // Caminhantes que nascem em cima de outro (cópias do mesmo NPC sem mover) vão cada um para um ponto de circulação
    // livre: empilhados, a separação os empurrava para fora uns dos outros no primeiro quadro ("teleporte").
    private static void EspalharCaminhantes()
    {
        var andando = Object.FindObjectsByType<NpcCaminhada>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .OrderBy(n => n.name).ToList();
        GameObject grupo = GameObject.Find("Pontos_Circulacao");
        if (grupo == null) return;
        var livres = grupo.transform.Cast<Transform>().Select(t => t.position).ToList();
        // o chefe (e qualquer outro agente que não é caminhante) também ocupa o lugar onde nasce
        var ocupados = Object.FindObjectsByType<NavMeshAgent>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(a => a.GetComponent<NpcCaminhada>() == null).Select(a => a.transform.position).ToList();
        livres.RemoveAll(l => ocupados.Any(o => Vector3.Distance(l, o) < 1.2f));
        foreach (NpcCaminhada n in andando)
        {
            Vector3 p = n.transform.position;
            bool empilhado = ocupados.Any(o => Vector2.Distance(new Vector2(o.x, o.z), new Vector2(p.x, p.z)) < 1.2f);
            if (empilhado)
            {
                Vector3 alvo = livres.OrderByDescending(l => ocupados.Count == 0 ? 0f : ocupados.Min(o => Vector3.Distance(o, l))).First();
                livres.Remove(alvo);
                n.transform.position = new Vector3(alvo.x, p.y, alvo.z);
                log.AppendLine($"{n.name}: nascia em cima de outro NPC em {p:F2} → ponto livre {n.transform.position:F2}");
                EditorUtility.SetDirty(n.transform);
            }
            ocupados.Add(n.transform.position);
            livres.RemoveAll(l => Vector3.Distance(l, n.transform.position) < 1.2f);
        }
    }

    // ------------------------------------------------------------------ 2. NavMesh
    private const float LimiteLesteDaDemo = 41.5f;   // parede Demo_Parede_Corredor em x ≈ 41,8

    private static void NavMeshDaDemo()
    {
        log.AppendLine("== NavMesh");
        NavMeshSurface s = Object.FindObjectsByType<NavMeshSurface>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
        if (s == null) { log.AppendLine("sem NavMeshSurface"); return; }
        Vector3 min = s.center - s.size / 2f, max = s.center + s.size / 2f;
        max.x = Mathf.Min(max.x, LimiteLesteDaDemo);
        s.center = (min + max) / 2f;
        s.size = max - min;
        s.BuildNavMesh();
        string path = AssetDatabase.GetAssetPath(s.navMeshData);
        NavMeshData novo = s.navMeshData;
        string antigo = "Assets/Scenes/Office_Placeholder/NavMesh-NavMesh_Escritorio.asset";
        if (string.IsNullOrEmpty(path))
        {
            var existente = AssetDatabase.LoadAssetAtPath<NavMeshData>(antigo);
            if (existente != null)
            {
                EditorUtility.CopySerialized(novo, existente);
                existente.name = System.IO.Path.GetFileNameWithoutExtension(antigo);
                EditorUtility.SetDirty(existente);
                s.RemoveData();
                s.navMeshData = existente;
                s.AddData();
                Object.DestroyImmediate(novo);
            }
            else AssetDatabase.CreateAsset(novo, antigo);
        }
        EditorUtility.SetDirty(s);
        var tri = NavMesh.CalculateTriangulation();
        Bounds b = new Bounds(tri.vertices[0], Vector3.zero); foreach (var v in tri.vertices) b.Encapsulate(v);
        log.AppendLine($"volume {s.center:F2} / {s.size:F2}; NavMesh {tri.vertices.Length} vértices, {b.min:F2} .. {b.max:F2}; asset {AssetDatabase.GetAssetPath(s.navMeshData)}");
    }

    // Pontos de circulação/ronda sem caminho a partir do salão (salas do norte, isoladas no NavMesh já antes desta
    // rodada) vão para um grupo DESLIGADO ao lado do original: ninguém tenta ir para onde não chega. Nada é apagado.
    private static void PontosAlcancaveis()
    {
        log.AppendLine("== Pontos fora da área alcançável");
        NavMesh.SamplePosition(new Vector3(26.2f, 0f, 1.4f), out NavMeshHit salao, 2f, NavMesh.AllAreas);
        foreach (var (grupo, fora) in new[] { ("Pontos_Circulacao", "Pontos_Fora_Da_Demo"), ("Ronda_Supervisor", "Ronda_Fora_Da_Demo") })
        {
            GameObject g = GameObject.Find(grupo);
            if (g == null) { log.AppendLine($"AVISO: {grupo} não encontrado"); continue; }
            Transform destino = g.transform.parent != null ? g.transform.parent.Find(fora) : null;
            if (destino == null)
            {
                destino = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(t => t.name == fora);
                if (destino == null)
                {
                    destino = new GameObject(fora).transform;
                    destino.SetParent(g.transform.parent, false);
                    destino.gameObject.SetActive(false);
                }
            }
            foreach (Transform p in g.transform.Cast<Transform>().ToList())
            {
                var path = new NavMeshPath();
                bool ok = NavMesh.SamplePosition(p.position, out NavMeshHit h, 0.5f, NavMesh.AllAreas)
                          && NavMesh.CalculatePath(salao.position, h.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete
                          && path.corners.All(c => c.x <= LimiteLesteDaDemo);
                if (ok) continue;
                p.SetParent(destino, true);
                log.AppendLine($"{grupo}/{p.name} {p.position:F1}: sem caminho a partir do salão → {destino.name} (desligado)");
            }
            log.AppendLine($"{grupo}: {g.transform.childCount} pontos válidos");
        }
    }

    // ------------------------------------------------------------------ 3. Gaveteiros de 3 gavetas
    // Modelo (gavetas.fbx, espaço do root do FBX): corpo x,z ±1, y 2,022..5,827; frente das gavetas em −X (x −1,044),
    // parede interna da frente em x −0,936, fundo interno 0,033 acima da base da gaveta, laterais internas z ±0,796.
    // No prefab: escala 0,36 (a do Matheus), frente em +Z, origem no chão: raiz = (0,36 z, 0,36 y − 0,728, −0,36 x).
    private const float K = 0.36f, BaseY = 2.022f;
    private static readonly Quaternion RotModelo = Quaternion.Euler(0f, 90f, 0f);   // −X do modelo → +Z do prefab
    private const float Largura = 2f * K, Altura = (5.827f - BaseY) * K, Profundidade = 2f * K;
    private const float FrenteZ = 1.044f * K;            // frente das gavetas
    private const float InternoFrente = 0.936f * K, InternoFundo = -0.70f * K;
    private const float InternoMeiaLargura = 0.796f * K - 0.01f;
    private const float GavetaAltura = 0.926f * K, GavetaLargura = 1.654f * K;

    // Gaveta_1 = a de cima. Nó do FBX, base da gaveta (y do modelo) e o que vai dentro.
    private struct Gaveta { public string no; public float baseY; public string plaquinha; public Item[] itens; public bool trancaRede; }
    private struct Item { public string prefab; public Vector3 pos; public Vector3 step; public float yaw; }
    private static Item I(string prefab, float x, float z, float sx = 0f, float sz = 0f, float yaw = 0f) =>
        new Item { prefab = prefab, pos = new Vector3(x, 0.002f, z), step = new Vector3(sx, 0f, sz), yaw = yaw };

    private static readonly (string nome, string antigo, string titulo, Gaveta[] gavetas)[] Gaveteiros =
    {
        ("Gaveteiro3_Hardware", "Gaveteiro_Hardware", "hardware", new[]
        {
            new Gaveta { no = "Cube.003", baseY = 4.721f, plaquinha = "ferramentas", itens = new[] { I("Pickup_Ferramenta", -0.14f, 0.09f, 0.28f), I("Pickup_Pendrive", 0f, 0.09f), I("Pickup_Alicate", 0f, -0.10f) } },
            new Gaveta { no = "Cube.001", baseY = 3.455f, plaquinha = "placa_ram_cooler", itens = new[] { I("Pickup_PlacaMae", -0.135f, -0.13f, 0.27f), I("Pickup_MemoriaRAM", -0.15f, 0.25f, 0f, -0.07f), I("Pickup_Cooler", 0.10f, 0.20f, 0.11f) } },
            new Gaveta { no = "Cube.002", baseY = 2.168f, plaquinha = "hd_fonte", itens = new[] { I("Pickup_HD", -0.22f, 0.10f, 0.12f), I("Pickup_Fonte", 0.04f, 0.10f, 0.155f) } },
        }),
        ("Gaveteiro3_Cabos", "Gaveteiro_Cabos", "cabos", new[]
        {
            new Gaveta { no = "Cube.003", baseY = 4.721f, plaquinha = "energia", itens = new[] { I("Pickup_CaboEnergia", -0.14f, 0.0f, 0.28f) } },
            new Gaveta { no = "Cube.001", baseY = 3.455f, plaquinha = "video", itens = new[] { I("Pickup_CaboVideo", -0.14f, 0.14f, 0.28f), I("Pickup_CaboHDMI", -0.14f, -0.14f, 0.28f) } },
            new Gaveta { no = "Cube.002", baseY = 2.168f, plaquinha = "rede", trancaRede = true, itens = new[] { I("Pickup_CaboRede", -0.14f, 0.0f, 0.28f) } },
        }),
        ("Gaveteiro3_Suprimentos", "Gaveteiro_Suprimentos", "suprimentos", new[]
        {
            new Gaveta { no = "Cube.003", baseY = 4.721f, plaquinha = "memoria_ssd_pendrive", itens = new[] { I("Pickup_RamSodimm", -0.15f, 0.15f, 0.13f), I("Pickup_Ssd", -0.15f, -0.05f, 0.13f), I("Pickup_Pendrive", -0.15f, -0.20f) } },
            new Gaveta { no = "Cube.001", baseY = 3.455f, plaquinha = "toner", itens = new[] { I("Pickup_Toner", 0f, 0.11f, 0f, -0.2f) } },
            new Gaveta { no = "Cube.002", baseY = 2.168f, plaquinha = "papel", itens = new[] { I("Pickup_ResmaPapel", -0.13f, 0f, 0.26f) } },
        }),
    };

    /// <summary>Caixa interna da gaveta (espaço local da Gaveta_N: origem no fundo interno, no centro).</summary>
    public static readonly Bounds Interno = new Bounds(new Vector3(0f, 0.15f, 0f),
        new Vector3(2f * InternoMeiaLargura, 0.30f, InternoFrente - InternoFundo - 0.01f));

    /// <summary>Centro da caixa interna em z (no espaço do gaveteiro).</summary>
    private const float CentroZ = (InternoFrente + InternoFundo) / 2f;

    private static Dictionary<string, GameObject> MontarGaveteiros()
    {
        log.AppendLine("== Gaveteiros (prefabs)");
        var resultado = new Dictionary<string, GameObject>();
        GameObject modelo = AssetDatabase.LoadAssetAtPath<GameObject>(Modelo);
        Matrix4x4 raizDoModelo = Matrix4x4.TRS(new Vector3(0f, -BaseY * K, 0f), RotModelo, Vector3.one * K);
        var metal = AssetDatabase.LoadAssetAtPath<Material>(Mats + "M_PC_Chapa.mat");
        Mesh plate = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Art/Meshes/Gaveteiro_Plaquinha.asset");

        foreach (var (nome, antigo, titulo, gavetas) in Gaveteiros)
        {
            var root = new GameObject(nome);
            var box = root.AddComponent<BoxCollider>();
            // corpo recuado atrás das frentes: a mira nas gavetas não bate no corpo (que não é interagível)
            float frenteCorpo = FrenteZ - 0.04f;
            box.size = new Vector3(Largura, Altura, frenteCorpo + Profundidade / 2f);
            box.center = new Vector3(0f, Altura / 2f, (frenteCorpo - Profundidade / 2f) / 2f);

            Transform corpoFbx = modelo.transform.Find("Cube");
            CopiaDoNo(corpoFbx, "Visual_Corpo", root.transform, raizDoModelo, Matrix4x4.identity);
            Parte("Plaquinha_Titulo", root.transform, plate, LabelMaterial("titulo_" + titulo),
                new Vector3(0f, (5.647f - BaseY) * K + (Altura - (5.647f - BaseY) * K) / 2f, Profundidade / 2f + 0.0015f), new Vector3(0.36f, 0.055f, 1f));

            for (int i = 0; i < gavetas.Length; i++)
            {
                Gaveta g = gavetas[i];
                float pisoY = (g.baseY + 0.033f - BaseY) * K, centroY = (g.baseY + 0.463f - BaseY) * K;
                var gaveta = new GameObject("Gaveta_" + (i + 1));
                gaveta.transform.SetParent(root.transform, false);
                gaveta.transform.localPosition = new Vector3(0f, pisoY, CentroZ);
                Matrix4x4 gavetaInv = gaveta.transform.localToWorldMatrix.inverse * root.transform.localToWorldMatrix;

                var col = gaveta.AddComponent<BoxCollider>();   // só a frente: não cobre os itens dentro
                col.size = new Vector3(GavetaLargura, GavetaAltura, 0.04f);
                col.center = new Vector3(0f, centroY - pisoY, FrenteZ - CentroZ - 0.015f);
                var open = gaveta.AddComponent<OpenableInteractable>();
                var so = new SerializedObject(open);
                so.FindProperty("interactionMessage").stringValue = "Abrir/Fechar";
                so.FindProperty("localAxis").vector3Value = Vector3.forward;
                so.FindProperty("openAmount").floatValue = 0.42f;
                so.ApplyModifiedPropertiesWithoutUndo();

                CopiaDoNo(modelo.transform.Find(g.no), "Visual", gaveta.transform, gavetaInv * raizDoModelo, Matrix4x4.identity);
                Parte("Plaquinha", gaveta.transform, plate, LabelMaterial(g.plaquinha),
                    new Vector3(0f, centroY - pisoY - 0.03f, FrenteZ - CentroZ + 0.0015f), new Vector3(0.30f, 0.075f, 1f));

                var contents = gaveta.AddComponent<DrawerContents>();
                var cso = new SerializedObject(contents);
                SerializedProperty list = cso.FindProperty("entries");
                list.arraySize = g.itens.Length;
                for (int k = 0; k < g.itens.Length; k++)
                {
                    var e = list.GetArrayElementAtIndex(k);
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Gameplay + g.itens[k].prefab + ".prefab");
                    e.FindPropertyRelative("prefab").objectReferenceValue = prefab.GetComponent<PickupInteractable>();
                    e.FindPropertyRelative("quantity").intValue = 1;   // quantidades: as do gaveteiro antigo (abaixo)
                    e.FindPropertyRelative("delivery").intValue = (int)DrawerContents.Delivery.PhysicalInDrawer;
                    e.FindPropertyRelative("localPosition").vector3Value = g.itens[k].pos;
                    e.FindPropertyRelative("localEuler").vector3Value = new Vector3(0f, g.itens[k].yaw, 0f);
                    e.FindPropertyRelative("step").vector3Value = g.itens[k].step;
                }
                cso.FindProperty("hideContentsWhenClosed").boolValue = true;
                cso.FindProperty("storageArea").objectReferenceValue = AreaGuardar(gaveta.transform);
                cso.ApplyModifiedPropertiesWithoutUndo();
                if (g.trancaRede) gaveta.AddComponent<DrawerTicketLock>();
            }
            CopiarQuantidades(antigo, root);

            string path = Gameplay + nome + ".prefab";
            resultado[nome] = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            log.AppendLine($"{path}: {gavetas.Length} gavetas ({string.Join(" | ", gavetas.Select(g => g.plaquinha))})");
        }
        return resultado;
    }

    // Quantidade de cada item = a do gaveteiro antigo (prefab), para não perder nem duplicar estoque.
    private static void CopiarQuantidades(string antigo, GameObject novo)
    {
        GameObject velho = AssetDatabase.LoadAssetAtPath<GameObject>(Gameplay + antigo + ".prefab");
        var qtd = new Dictionary<PickupInteractable, int>();
        foreach (DrawerContents d in velho.GetComponentsInChildren<DrawerContents>(true))
            for (int i = 0; i < d.EntryCount; i++)
                if (d.EntryPrefab(i) != null) qtd[d.EntryPrefab(i)] = (qtd.TryGetValue(d.EntryPrefab(i), out int q) ? q : 0) + d.EntryQuantity(i);
        var usados = new HashSet<PickupInteractable>();
        foreach (DrawerContents d in novo.GetComponentsInChildren<DrawerContents>(true))
        {
            var so = new SerializedObject(d);
            var list = so.FindProperty("entries");
            for (int i = 0; i < list.arraySize; i++)
            {
                var e = list.GetArrayElementAtIndex(i);
                var p = (PickupInteractable)e.FindPropertyRelative("prefab").objectReferenceValue;
                if (p != null && qtd.TryGetValue(p, out int q)) { e.FindPropertyRelative("quantity").intValue = q; usados.Add(p); }
                else log.AppendLine($"   AVISO {novo.name}/{d.name}: {(p ? p.name : "null")} não existia no {antigo} (quantidade 1)");
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        foreach (var p in qtd.Keys.Where(k => !usados.Contains(k))) log.AppendLine($"   AVISO {antigo}: {p.name} ficou sem gaveta no {novo.name}");
        log.AppendLine($"   {novo.name}: estoque do {antigo} = {string.Join(", ", qtd.Select(kv => kv.Key.name + " x" + kv.Value))}");
    }

    private static void CopiaDoNo(Transform no, string nome, Transform pai, Matrix4x4 paiDoModelo, Matrix4x4 _)
    {
        Matrix4x4 m = paiDoModelo * Matrix4x4.TRS(no.localPosition, no.localRotation, no.localScale);
        var go = new GameObject(nome);
        go.transform.SetParent(pai, false);
        go.transform.localPosition = m.GetColumn(3);
        go.transform.localRotation = m.rotation;
        go.transform.localScale = m.lossyScale;
        go.AddComponent<MeshFilter>().sharedMesh = no.GetComponent<MeshFilter>().sharedMesh;
        go.AddComponent<MeshRenderer>().sharedMaterials = no.GetComponent<MeshRenderer>().sharedMaterials;
    }

    private static void Parte(string nome, Transform pai, Mesh mesh, Material mat, Vector3 pos, Vector3 escala)
    {
        var go = new GameObject(nome);
        go.transform.SetParent(pai, false);
        go.transform.localPosition = pos;
        go.transform.localScale = escala;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
    }

    private static BoxCollider AreaGuardar(Transform gaveta)
    {
        var t = new GameObject("AreaGuardar").transform;
        t.SetParent(gaveta, false);
        var box = t.gameObject.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(Interno.size.x, 0.25f, Interno.size.z);
        box.center = new Vector3(0f, 0.125f, 0f);
        return box;
    }

    // Igual ao GaveteirosSetup.LabelMaterial: textura pixel art sem filtro + material URP/Lit por plaquinha.
    private static Material LabelMaterial(string id)
    {
        string tex = Labels + "plaquinha_" + id + ".png";
        var imp = (TextureImporter)AssetImporter.GetAtPath(tex);
        if (imp.filterMode != FilterMode.Point || imp.mipmapEnabled || imp.wrapMode != TextureWrapMode.Clamp || !imp.alphaIsTransparency)
        {
            imp.filterMode = FilterMode.Point; imp.mipmapEnabled = false; imp.wrapMode = TextureWrapMode.Clamp;
            imp.alphaIsTransparency = true; imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();
        }
        string path = Mats + "Plaquinhas/M_Plaquinha_" + id + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, path); }
        mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(tex));
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_Smoothness", 0.25f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static void TrocarGaveteiros(Dictionary<string, GameObject> prefabs)
    {
        log.AppendLine("== Gaveteiros (cena)");
        var todos = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var (nome, antigo, _, _) in Gaveteiros)
        {
            Transform velho = todos.FirstOrDefault(t => t != null && t.name == antigo && PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject));
            Transform jaNovo = todos.FirstOrDefault(t => t != null && t.name == nome && PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject));
            if (velho == null && jaNovo != null) { log.AppendLine($"{nome}: já na cena ({Caminho(jaNovo)})"); continue; }
            if (velho == null) { log.AppendLine($"AVISO {antigo}: não está na cena"); continue; }

            var novo = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[nome], velho.parent);
            novo.transform.SetSiblingIndex(velho.GetSiblingIndex());
            // mesmo lugar e giro; as costas encostam onde as do antigo encostavam (o novo é 0,16 m mais fundo)
            float recuo = Profundidade / 2f - 0.28f;
            novo.transform.SetPositionAndRotation(velho.position + velho.forward * recuo, velho.rotation);
            Remapear(velho, novo.transform);
            log.AppendLine($"{Caminho(velho)} → {Caminho(novo.transform)} em {novo.transform.position:F2} (antigo em {velho.position:F2})");
            Object.DestroyImmediate(velho.gameObject);
        }

        // amostra solta do Matheus (gavetas.fbx na raiz, afundada em y −0,72 ao lado da impressora): desligada, não
        // apagada — o modelo agora está nos gaveteiros da sala de TI
        foreach (Transform t in todos.Where(t => t != null && t.parent == null && PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)
                                                && AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromOriginalSource(t.gameObject)) == Modelo))
        {
            t.gameObject.SetActive(false);
            if (!t.name.EndsWith("(amostra do Matheus)")) t.name += " (amostra do Matheus)";
            log.AppendLine($"{t.name}: amostra solta desligada em {t.position:F2}");
        }
    }

    // Referências da cena para o gaveteiro antigo (ou peças dele) passam para o novo (mesmo nome de gaveta, se houver).
    private static void Remapear(Transform velho, Transform novo)
    {
        var mapa = new Dictionary<Object, Object>();
        foreach (Transform t in velho.GetComponentsInChildren<Transform>(true))
        {
            Transform par = t == velho ? novo : novo.Find(RelPath(velho, t));
            mapa[t.gameObject] = par != null ? par.gameObject : null;
            foreach (Component c in t.GetComponents<Component>())
                mapa[c] = par != null && c != null ? par.GetComponent(c.GetType()) : null;
        }
        foreach (MonoBehaviour mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (mb == null || mb.transform.IsChildOf(velho)) continue;
            var so = new SerializedObject(mb);
            var it = so.GetIterator();
            bool mudou = false;
            while (it.Next(true))
            {
                if (it.propertyType != SerializedPropertyType.ObjectReference || it.objectReferenceValue == null) continue;
                if (!mapa.TryGetValue(it.objectReferenceValue, out Object alvo)) continue;
                log.AppendLine($"   referência {Caminho(mb.transform)}.{mb.GetType().Name}.{it.propertyPath}: {it.objectReferenceValue.name} → {(alvo ? alvo.name : "NADA")}");
                it.objectReferenceValue = alvo;
                mudou = true;
            }
            if (mudou) so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static string RelPath(Transform raiz, Transform t) => t == raiz ? "" : t.parent == raiz ? t.name : RelPath(raiz, t.parent) + "/" + t.name;

    // ------------------------------------------------------------------ 4. HUD
    private static void Hud()
    {
        log.AppendLine("== HUD");
        foreach (var tab in Object.FindObjectsByType<TicketTabletPrototype>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var so = new SerializedObject(tab);
            float antes = so.FindProperty("escalaTablet").floatValue;
            so.FindProperty("escalaTablet").floatValue = 0.8f;
            so.ApplyModifiedPropertiesWithoutUndo();
            log.AppendLine($"tablet {Caminho(tab.transform)}: escala {antes} → 0,8 (tamanho {so.FindProperty("tamanhoTablet").vector2Value})");
        }
        var fonte = AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/Fonts/VarelaRound-Regular.ttf");
        foreach (var hud in Object.FindObjectsByType<StageTimerHUD>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var so = new SerializedObject(hud);
            so.FindProperty("fonte").objectReferenceValue = fonte;
            so.ApplyModifiedPropertiesWithoutUndo();
            log.AppendLine($"StageTimerHUD {Caminho(hud.transform)}: fonte {(fonte ? fonte.name : "NÃO ENCONTRADA")}");
        }
    }

    // ------------------------------------------------------------------ 5. Plaquinhas das mesas
    // Nomes fictícios SÓ para a plaquinha das mesas sem funcionário (StationEmployee.nomeNaPlaca): elas continuam sem
    // receber chamados. Uma cor de detalhe por mesa (StationEmployee.corDaPlaca), na ordem das estações.
    private static readonly string[] NomesFicticios = { "Beatriz", "Rafael", "Tânia", "Diego", "Helena", "Otávio", "Sofia", "Renata", "Caio" };
    private static readonly Color[] CoresDasPlacas =
    {
        new Color(0.16f, 0.45f, 0.86f), new Color(0.98f, 0.78f, 0.20f), new Color(0.93f, 0.42f, 0.36f), new Color(0.20f, 0.70f, 0.66f),
        new Color(0.55f, 0.36f, 0.80f), new Color(0.96f, 0.56f, 0.18f), new Color(0.35f, 0.72f, 0.38f), new Color(0.90f, 0.45f, 0.65f),
        new Color(0.18f, 0.27f, 0.55f), new Color(0.80f, 0.62f, 0.16f), new Color(0.80f, 0.22f, 0.24f), new Color(0.45f, 0.82f, 0.74f),
        new Color(0.70f, 0.60f, 0.90f), new Color(0.40f, 0.68f, 0.95f), new Color(0.55f, 0.60f, 0.25f), new Color(0.74f, 0.28f, 0.62f),
    };

    private static void Plaquinhas()
    {
        log.AppendLine("== Plaquinhas das mesas");
        var estacoes = Object.FindObjectsByType<StationEmployee>(FindObjectsSortMode.None)
            .Where(e => e.gameObject.activeInHierarchy).OrderBy(e => e.stationLabel).ThenBy(e => e.name).ToList();
        int livre = 0, ok = 0;
        for (int i = 0; i < estacoes.Count; i++)
        {
            StationEmployee e = estacoes[i];
            if (!e.Occupied && string.IsNullOrEmpty(e.nomeNaPlaca)) e.nomeNaPlaca = NomesFicticios[livre++ % NomesFicticios.Length];
            e.corDaPlaca = CoresDasPlacas[i % CoresDasPlacas.Length];
            EditorUtility.SetDirty(e);

            var placas = e.GetComponentsInChildren<StationNamePlate>(true);
            string problema = placas.Length == 0 ? "SEM PLAQUINHA" : placas.Length > 1 ? placas.Length + " plaquinhas" : null;
            foreach (StationNamePlate p in placas)
            {
                if (!p.gameObject.activeInHierarchy) problema = "plaquinha desligada";
                var rs = p.GetComponentsInChildren<Renderer>(true);
                if (rs.Any(r => !r.enabled || r.sharedMaterials.Any(m => m == null))) problema = "renderer desligado ou sem material";
                if (!rs.Any(r => r.name == "Faixa")) problema = "sem faixa colorida";
            }
            string nome = e.DisplayName;
            if (string.IsNullOrEmpty(nome) || nome.StartsWith("Estação") || nome == "Funcionário") problema = "nome inválido: '" + nome + "'";
            if (problema == null) ok++;
            log.AppendLine($"{e.stationLabel} ({e.name}): '{nome}'{(e.Occupied ? "" : " (sem chamados)")} cor {ColorUtility.ToHtmlStringRGB(e.corDaPlaca)} " +
                           $"{(placas.Length == 1 ? "plaquinha em " + placas[0].transform.position.ToString("F2") : "")} {(problema ?? "OK")}");
        }
        log.AppendLine($"{ok}/{estacoes.Count} mesas com plaquinha OK");
    }

    // ------------------------------------------------------------------ validação
    private static void Validar()
    {
        log.AppendLine("== Validação");
        // divisórias e paredes fora do NavMesh: amostra no centro de cada uma
        foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)
                     .Where(r => r.name.StartsWith("Divisoria") || r.transform.parent != null && r.transform.parent.name.StartsWith("Divisoria")))
        {
            bool tem = NavMesh.SamplePosition(new Vector3(r.bounds.center.x, 0f, r.bounds.center.z), out NavMeshHit hit, 0.05f, NavMesh.AllAreas);
            log.AppendLine($"{Caminho(r.transform)}: centro {(tem ? "NO NavMesh (ruim)" : "fora do NavMesh")}");
        }
        // destinos e ronda alcançáveis a partir de cada NPC
        foreach (NavMeshAgent ag in Object.FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None))
        {
            Transform pontos = ag.GetComponent<NpcCaminhada>() ? ag.GetComponent<NpcCaminhada>().Destinos : GameObject.Find("Ronda_Supervisor")?.transform;
            if (pontos == null) continue;
            NavMesh.SamplePosition(ag.transform.position, out NavMeshHit ini, 2f, NavMesh.AllAreas);
            var falhas = new List<string>();
            foreach (Transform p in pontos)
            {
                var path = new NavMeshPath();
                if (!NavMesh.SamplePosition(p.position, out NavMeshHit h, 0.5f, NavMesh.AllAreas) || !NavMesh.CalculatePath(ini.position, h.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
                    falhas.Add(p.name);
                else if (path.corners.Any(c => c.x > LimiteLesteDaDemo)) falhas.Add(p.name + "(fora da demo)");
            }
            log.AppendLine($"{ag.name}: {pontos.childCount - falhas.Count}/{pontos.childCount} destinos alcançáveis {(falhas.Count > 0 ? "falhas: " + string.Join(",", falhas) : "")}");
        }
    }

    private static Bounds Limites(Transform t)
    {
        var rs = t.GetComponentsInChildren<Renderer>(true);
        Bounds b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    private static string Caminho(Transform t) => t.parent == null ? t.name : Caminho(t.parent) + "/" + t.name;

    private static class Path
    {
        public static string GetFileNameWithoutExtension(string p) => string.IsNullOrEmpty(p) ? null : System.IO.Path.GetFileNameWithoutExtension(p);
    }
}
