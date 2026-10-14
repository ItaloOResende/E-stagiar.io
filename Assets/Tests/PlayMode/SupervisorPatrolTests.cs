using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

/// <summary>
/// Ronda do supervisor (SupervisorPatrol): percorre os pontos na ordem, para entre eles e segue; às vezes para e vira
/// para o jogador quando passa perto (nunca com chance 0; com chance 1, uma vez por passagem) e retoma a ronda.
/// Cena: NPC, rota e NavMesh configurados. Sem detecção de infrações (não existe ainda).
/// </summary>
public class SupervisorPatrolTests
{
    private readonly List<GameObject> created = new List<GameObject>();

    private static Type T(string name) => Type.GetType(name + ", Assembly-CSharp", true);
    private static void Set(object o, string f, object v) => o.GetType().GetField(f, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(o, v);
    private static object Get(object o, string p) => o.GetType().GetProperty(p).GetValue(o);

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject g in created) if (g != null) Object.Destroy(g);
        created.Clear();
    }

    private GameObject New(string name) { var g = new GameObject(name); created.Add(g); return g; }

    /// <summary>Chão 20 x 20 com NavMesh, rota com pontos e o NPC (rápido, pausas curtas).</summary>
    private Component Build(Vector3[] points, Transform player, float chance)
    {
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        created.Add(floor);
        floor.transform.localScale = new Vector3(20f, 0.2f, 20f);
        floor.transform.position = new Vector3(0f, -0.1f, 0f);
        var surface = floor.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Children;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.BuildNavMesh();

        Transform route = New("Rota").transform;
        foreach (Vector3 p in points) { var c = new GameObject("P").transform; c.SetParent(route, false); c.position = p; }

        GameObject npc = New("Supervisor");
        npc.transform.position = points[points.Length - 1];
        var agent = npc.AddComponent<NavMeshAgent>();
        agent.speed = 6f; agent.acceleration = 30f; agent.angularSpeed = 720f; agent.radius = 0.3f;
        Component patrol = npc.AddComponent(T("SupervisorPatrol"));
        Set(patrol, "route", route);
        Set(patrol, "waitRange", new Vector2(0.2f, 0.3f));
        Set(patrol, "player", player);
        Set(patrol, "watchChance", chance);
        Set(patrol, "watchCooldown", 0f);
        Set(patrol, "watchDuration", new Vector2(0.8f, 0.8f));
        Set(patrol, "requireLineOfSight", false);
        return patrol;
    }

    [UnityTest]
    public IEnumerator Ronda_PercorreOsPontosNaOrdem_ComPausas_ERecomeca()
    {
        Transform far = New("JogadorLonge").transform;
        far.position = new Vector3(50f, 0f, 50f);
        var pts = new[] { new Vector3(-4f, 0f, -4f), new Vector3(4f, 0f, -4f), new Vector3(4f, 0f, 4f) };
        Component patrol = Build(pts, far, 1f);
        var visited = new List<int>();
        bool sawWaiting = false;
        for (float t = 0f; t < 12f && visited.Count < 5; t += Time.deltaTime)
        {
            if (Get(patrol, "State").ToString() == "Waiting")
            {
                sawWaiting = true;
                int i = (int)Get(patrol, "CurrentIndex");
                if (visited.Count == 0 || visited[visited.Count - 1] != i) visited.Add(i);
                Assert.Less(Vector3.Distance(((Component)patrol).transform.position, pts[i]), 0.5f, "parou no ponto");
            }
            yield return null;
        }
        Assert.IsTrue(sawWaiting, "faz pausas nos pontos");
        CollectionAssert.AreEqual(new[] { 0, 1, 2, 0, 1 }, visited.Take(5).ToArray(), "segue a ordem e recomeça");
        Assert.AreEqual(0, (int)Get(patrol, "WatchCount"), "jogador longe: não para para olhar");
    }

    [UnityTest]
    public IEnumerator PertoDoJogador_ComChance1_ParaEOlha_DepoisRetoma_ComChance0_NuncaOlha()
    {
        Transform player = New("Jogador").transform;
        player.position = new Vector3(0f, 0f, -2.5f);
        var pts = new[] { new Vector3(-6f, 0f, -4f), new Vector3(6f, 0f, -4f) };
        Component patrol = Build(pts, player, 1f);
        float t = 0f;
        while (t < 6f && Get(patrol, "State").ToString() != "Watching") { t += Time.deltaTime; yield return null; }
        Assert.AreEqual("Watching", Get(patrol, "State").ToString(), "passou perto: parou para olhar");
        Assert.IsTrue(((Component)patrol).GetComponent<NavMeshAgent>().isStopped);
        yield return new WaitForSeconds(0.6f);
        Assert.Less((float)patrol.GetType().GetMethod("AngleToPlayer").Invoke(patrol, null), 20f, "virou (suavemente) para o jogador");
        yield return new WaitForSeconds(0.6f);
        Assert.AreNotEqual("Watching", Get(patrol, "State").ToString(), "retomou a ronda");
        Assert.AreEqual(1, (int)Get(patrol, "WatchCount"), "uma vez por passagem (o jogador continuou perto)");

        foreach (GameObject g in created) if (g != null) Object.Destroy(g);
        created.Clear();
        yield return null;
        player = New("Jogador").transform;
        player.position = new Vector3(0f, 0f, -2.5f);
        patrol = Build(pts, player, 0f);
        for (t = 0f; t < 6f; t += Time.deltaTime)
        {
            Assert.AreNotEqual("Watching", Get(patrol, "State").ToString(), "chance 0: só passa");
            yield return null;
        }
        Assert.Greater((int)Get(patrol, "Arrivals"), 0, "continuou andando");
    }

    [UnityTest]
    public IEnumerator JogadorNoCaminho_SupervisorMantemDistancia_ContornaENaoFicaPreso()
    {
        // jogador parado no meio da linha entre dois pontos, com o NavMeshObstacle (carving) como o Player da cena
        GameObject player = New("Jogador");
        player.transform.position = new Vector3(0f, 1f, -4f);
        var obstacle = player.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Capsule;
        obstacle.radius = 0.75f;
        obstacle.height = 2f;
        obstacle.carving = true;
        obstacle.carveOnlyStationary = false;
        var pts = new[] { new Vector3(-6f, 0f, -4f), new Vector3(6f, 0f, -4f) };
        Component patrol = Build(pts, player.transform, 0f);
        ((Component)patrol).GetComponent<NavMeshAgent>().speed = 3f;
        float min = float.MaxValue;
        for (float t = 0f; t < 30f && (int)Get(patrol, "Arrivals") < 3; t += Time.deltaTime)
        {
            Vector3 d = ((Component)patrol).transform.position - player.transform.position;
            d.y = 0f;
            min = Mathf.Min(min, d.magnitude);
            yield return null;
        }
        Assert.GreaterOrEqual((int)Get(patrol, "Arrivals"), 3, "passou pelo jogador e seguiu a ronda");
        Assert.GreaterOrEqual(min, 0.9f, "não encosta no jogador (mínimo {0:F2} m)", min);
        Assert.AreEqual(0, (int)Get(patrol, "SkippedPoints"), "não ficou preso");
    }

    [UnityTest]
    public IEnumerator Cena_SupervisorComRotaNoNavMesh()
    {
        Type esm = Type.GetType("UnityEditor.SceneManagement.EditorSceneManager, UnityEditor", true);
        esm.GetMethod("LoadSceneInPlayMode").Invoke(null, new object[] { "Assets/Scenes/Office_Placeholder.unity", new LoadSceneParameters(LoadSceneMode.Additive) });
        yield return null; yield return null;
        Scene scene = SceneManager.GetSceneByPath("Assets/Scenes/Office_Placeholder.unity");
        try
        {
            GameObject[] roots = scene.GetRootGameObjects();
            GameObject npc = roots.Single(r => r.name == "NPC_Supervisor");
            Component patrol = npc.GetComponent(T("SupervisorPatrol"));
            Assert.GreaterOrEqual((int)Get(patrol, "PointCount"), 4, "rota com pontos pelo escritório");
            Assert.IsTrue(npc.GetComponent<NavMeshAgent>().isOnNavMesh);
            Assert.IsNotNull(npc.transform.Find("Visual"), "modelo no filho Visual");
            Transform route = roots.Single(r => r.name == "Ronda_Supervisor").transform;
            GameObject playerGo = roots.Single(r => r.name == "Player");
            var obstacle = playerGo.GetComponent<NavMeshObstacle>();
            Assert.IsNotNull(obstacle, "o jogador recorta o NavMesh (o supervisor o contorna)");
            Assert.IsTrue(obstacle.carving);
            Assert.GreaterOrEqual(obstacle.radius * playerGo.transform.lossyScale.x, 0.6f, "folga confortável");
            Bounds floor = roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)).First(t => t.name == "Floor").GetComponent<Collider>().bounds;
            Assert.GreaterOrEqual(floor.size.x, 14f, "escritório de teste ampliado");
            Assert.GreaterOrEqual(floor.size.z, 8f);
            float e1 = roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)).First(t => t.name == "Estacao_1").position.x;
            float e2 = roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)).First(t => t.name == "Estacao_2").position.x;
            Assert.GreaterOrEqual(Mathf.Abs(e2 - e1), 6.5f, "estações mais afastadas");
            var path = new NavMeshPath();
            for (int i = 0; i < route.childCount; i++)
            {
                Vector3 a = route.GetChild(i).position, b = route.GetChild((i + 1) % route.childCount).position;
                Assert.IsTrue(NavMesh.SamplePosition(a, out _, 0.2f, NavMesh.AllAreas), "ponto no NavMesh: " + route.GetChild(i).name);
                Assert.IsTrue(NavMesh.CalculatePath(a, b, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete,
                    "caminho entre " + route.GetChild(i).name + " e o próximo");
            }
        }
        finally { SceneManager.UnloadSceneAsync(scene); }
        yield return null;
    }
}
