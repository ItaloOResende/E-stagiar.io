using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Supervisor provisório e a ronda dele (idempotente; Tools > E-stagiar.io > Supervisor (ronda)):
///  - prefab <c>Prefabs/Gameplay/NPC_Supervisor</c>: root lógico (NavMeshAgent, CapsuleCollider, <see cref="SupervisorPatrol"/>,
///    NavMeshModifier que o tira do NavMesh) + filho <c>Visual</c> = <c>character2.fbx</c> do Matheus (sem alterar o FBX),
///    com o material <c>M_Character_Tex1_1</c>, ~1,75 m, pés na origem;
///  - cena <c>Office_Placeholder</c>: <c>NavMesh_Escritorio</c> (NavMeshSurface por colliders físicos, assado em
///    <c>Scenes/Office_Placeholder/NavMesh_Escritorio.asset</c>), rota <c>Ronda_Supervisor</c> (um filho por ponto) e o NPC;
///  - agente padrão do NavMesh com raio 0,3 m (o escritório tem corredores estreitos entre mesas e cadeiras).
/// O jogador fica fora do NavMesh (NavMeshModifier), senão abriria um buraco onde ele está ao assar.
/// </summary>
public static class SupervisorSetup
{
    private const string ScenePath = "Assets/Scenes/Office_Placeholder.unity";
    private const string PrefabPath = "Assets/Prefabs/Gameplay/NPC_Supervisor.prefab";
    private const string ModelPath = "Assets/Art/Models/character2.fbx";
    private const string MaterialPath = "Assets/Art/Materials/M_Character_Tex1_1.mat";
    private const string NavDataPath = "Assets/Scenes/Office_Placeholder/NavMesh_Escritorio.asset";
    private const float Height = 1.75f;
    /// <summary>Giro do modelo para a frente dele ficar no +Z do root (conferido em captura).</summary>
    public static float VisualYaw = 0f;

    // pontos da ronda (sala de teste 14 x 8 m, EscritorioTesteSetup): corredor em frente às estações, entre elas, lados
    // (bancada a oeste, rack/porta a leste) e fundo; longe de móveis para ele não encostar em nada
    private static readonly Vector3[] Route =
    {
        new Vector3(-5.6f, 0f, 0.2f), new Vector3(-1.6f, 0f, 0.4f), new Vector3(0f, 0f, 1.2f), new Vector3(1.6f, 0f, 0.4f),
        new Vector3(5.6f, 0f, 0.2f), new Vector3(5.2f, 0f, -3.6f), new Vector3(0f, 0f, -4.1f), new Vector3(-5.0f, 0f, -3.6f),
    };

    [MenuItem("Tools/E-stagiar.io/Supervisor (ronda)")]
    public static void Run()
    {
        AgentRadius(0.3f);
        BuildPrefab();
        ConfigureScene();
        Debug.Log("SupervisorSetup: pronto");
    }

    private static void AgentRadius(float r)
    {
        var so = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/NavMeshAreas.asset")[0]);
        SerializedProperty settings = so.FindProperty("m_Settings");
        SerializedProperty first = settings.GetArrayElementAtIndex(0);
        first.FindPropertyRelative("agentRadius").floatValue = r;
        first.FindPropertyRelative("agentHeight").floatValue = Height;
        first.FindPropertyRelative("agentClimb").floatValue = 0.15f;   // não sobe em móveis baixos (gaveteiro, base de vaso)
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void BuildPrefab()
    {
        var root = new GameObject("NPC_Supervisor");
        var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath));
        model.name = "Visual";
        model.transform.SetParent(root.transform, false);
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++) mats[i] = mat;
            r.sharedMaterials = mats;
        }

        // tamanho e posição: ~1,75 m de altura, pés no chão (y 0) e centrado no root
        model.transform.localRotation = Quaternion.Euler(0f, VisualYaw, 0f) * model.transform.localRotation;
        Bounds b = Bounds(model);
        model.transform.localScale *= Height / b.size.y;
        b = Bounds(model);
        model.transform.localPosition += new Vector3(-b.center.x, -b.min.y, -b.center.z);

        var agent = root.AddComponent<NavMeshAgent>();
        agent.radius = 0.28f;
        agent.height = Height;
        agent.speed = 1.3f;
        agent.angularSpeed = 220f;
        agent.acceleration = 6f;
        agent.stoppingDistance = 0.1f;
        agent.autoBraking = true;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
        var col = root.AddComponent<CapsuleCollider>();
        col.radius = 0.28f;
        col.height = Height;
        col.center = new Vector3(0f, Height * 0.5f, 0f);
        root.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
        var patrol = root.AddComponent<SupervisorPatrol>();
        root.AddComponent<SupervisorPerception>();
        var so = new SerializedObject(patrol);
        so.FindProperty("visual").objectReferenceValue = model.transform;
        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
    }

    private static Bounds Bounds(GameObject g)
    {
        Renderer[] rs = g.GetComponentsInChildren<Renderer>();
        Bounds b = rs[0].bounds;
        foreach (Renderer r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    private static void ConfigureScene()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath);

        GameObject player = GameObject.Find("Player");
        if (player.GetComponent<NavMeshModifier>() == null) player.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
        // o jogador "recorta" o NavMesh em volta de si enquanto anda: o supervisor recalcula o caminho e o contorna com
        // folga (raio 0,75 m + raio do agente 0,3 m), em vez de esbarrar nele
        var obstacle = player.GetComponent<NavMeshObstacle>();
        if (obstacle == null) obstacle = player.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Capsule;
        obstacle.center = Vector3.zero;
        obstacle.radius = 0.75f / player.transform.lossyScale.x;   // ~1 m de folga entre os centros (as cabeças são largas)
        obstacle.height = 2f;
        obstacle.carving = true;
        obstacle.carveOnlyStationary = false;
        obstacle.carvingMoveThreshold = 0.1f;
        obstacle.carvingTimeToStationary = 0.2f;
        GameObject old = GameObject.Find("NPC_Supervisor");
        if (old != null) Object.DestroyImmediate(old);                      // recriado abaixo (depois de assar)

        // NavMesh da sala
        GameObject navGo = GameObject.Find("NavMesh_Escritorio");
        if (navGo == null) navGo = new GameObject("NavMesh_Escritorio");
        var surface = navGo.GetComponent<NavMeshSurface>();
        if (surface == null) surface = navGo.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.All;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.layerMask = ~((1 << 5) | (1 << 8) | (1 << LayerMask.NameToLayer("ItemPreview")));
        surface.BuildNavMesh();
        System.IO.Directory.CreateDirectory("Assets/Scenes/Office_Placeholder");
        if (AssetDatabase.LoadAssetAtPath<NavMeshData>(NavDataPath) != null) AssetDatabase.DeleteAsset(NavDataPath);
        AssetDatabase.CreateAsset(surface.navMeshData, NavDataPath);

        // rota (pontos ajustados ao NavMesh)
        GameObject routeGo = GameObject.Find("Ronda_Supervisor");
        if (routeGo == null) routeGo = new GameObject("Ronda_Supervisor");
        for (int i = routeGo.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(routeGo.transform.GetChild(i).gameObject);
        for (int i = 0; i < Route.Length; i++)
        {
            Vector3 p = Route[i];
            if (NavMesh.SamplePosition(p, out NavMeshHit hit, 1f, NavMesh.AllAreas)) p = hit.position;
            else Debug.LogWarning("SupervisorSetup: ponto fora do NavMesh: " + Route[i]);
            var point = new GameObject("Ponto_" + (i + 1));
            point.transform.SetParent(routeGo.transform, false);
            point.transform.position = p;
        }

        // NPC no fundo da sala, olhando para as estações
        var npc = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
        npc.transform.position = routeGo.transform.GetChild(6).position;
        npc.transform.rotation = Quaternion.LookRotation(Vector3.forward);
        var so = new SerializedObject(npc.GetComponent<SupervisorPatrol>());
        so.FindProperty("route").objectReferenceValue = routeGo.transform;
        so.FindProperty("player").objectReferenceValue = player.transform;
        // pontos de interesse para olhar em volta nas pausas: as estações, a bancada e os gaveteiros
        string[] interest = { "Estacao_1", "Estacao_2", "Bancada", "Gaveteiro_Hardware" };
        SerializedProperty ip = so.FindProperty("interestPoints");
        ip.arraySize = interest.Length;
        for (int i = 0; i < interest.Length; i++) ip.GetArrayElementAtIndex(i).objectReferenceValue = GameObject.Find(interest[i]).transform;
        so.ApplyModifiedPropertiesWithoutUndo();

        // infrações: quem detecta o que acontece (Conduta) e o aviso ao jogador (HUD)
        GameObject conduta = GameObject.Find("Conduta");
        if (conduta == null) conduta = new GameObject("Conduta");
        if (conduta.GetComponent<ConductMonitor>() == null) conduta.AddComponent<ConductMonitor>();
        GameObject hud = GameObject.Find("HUD");
        var alert = hud.GetComponent<SupervisorAlertHUD>();
        if (alert == null) alert = hud.AddComponent<SupervisorAlertHUD>();
        var al = new SerializedObject(alert);
        al.FindProperty("playerCamera").objectReferenceValue = player.GetComponentInChildren<Camera>();
        al.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }
}
