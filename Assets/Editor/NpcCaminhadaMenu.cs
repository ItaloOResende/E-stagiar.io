using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Liga a caminhada independente (<see cref="NpcCaminhada"/>) SÓ nos NPCs selecionados na Hierarchy:
/// Tools &gt; E-stagiar.io &gt; NPC: ativar caminhada nos selecionados. Acrescenta um <see cref="NavMeshAgent"/>
/// (raio 0,3 m, altura 1,65 m, como o supervisor) se não houver, tira o NavMeshObstacle (um agente não pode ser
/// obstáculo de si mesmo) e aponta os destinos para <c>Navegacao_NPCs/Pontos_Circulacao</c>. Recusa NPCs que
/// trabalham numa estação (dentro de um StationEmployee) e o supervisor (que já tem a ronda dele).
/// </summary>
public static class NpcCaminhadaMenu
{
    [MenuItem("Tools/E-stagiar.io/NPC: ativar caminhada nos selecionados")]
    public static void Ativar()
    {
        GameObject pontos = GameObject.Find("Pontos_Circulacao");
        foreach (GameObject go in Selection.gameObjects)
        {
            if (go.GetComponentInParent<StationEmployee>() != null) { Debug.LogWarning($"{go.name}: trabalha numa estação; não vai andar.", go); continue; }
            if (go.GetComponent<SupervisorPatrol>() != null) { Debug.LogWarning($"{go.name}: é o supervisor (já tem ronda).", go); continue; }
            Undo.RegisterFullObjectHierarchyUndo(go, "Ativar caminhada");
            var obs = go.GetComponent<NavMeshObstacle>();
            if (obs != null) Undo.DestroyObjectImmediate(obs);
            var agent = go.GetComponent<NavMeshAgent>();
            if (agent == null)
            {
                agent = Undo.AddComponent<NavMeshAgent>(go);
                agent.radius = 0.3f;
                agent.height = 1.65f;
                agent.speed = 1.1f;
                agent.angularSpeed = 360f;
                agent.acceleration = 8f;
                agent.stoppingDistance = 0.3f;
                agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
                agent.avoidancePriority = Random.Range(40, 60);
            }
            // colisor do tamanho do agente + Rigidbody cinemático: o jogador não atravessa nem empurra o NPC
            var cap = go.GetComponent<CapsuleCollider>();
            if (cap == null) { cap = Undo.AddComponent<CapsuleCollider>(go); cap.radius = agent.radius; cap.height = agent.height; cap.center = Vector3.up * agent.height / 2f; }
            var rb = go.GetComponent<Rigidbody>();
            if (rb == null) { rb = Undo.AddComponent<Rigidbody>(go); rb.isKinematic = true; rb.useGravity = false; }
            var c = go.GetComponent<NpcCaminhada>();
            if (c == null) c = Undo.AddComponent<NpcCaminhada>(go);
            if (pontos != null) c.Destinos = pontos.transform;
            EditorUtility.SetDirty(go);
            Debug.Log($"{go.name}: caminhada ligada ({(pontos != null ? "destinos = Pontos_Circulacao" : "destinos aleatórios no NavMesh")}).", go);
        }
    }

    [MenuItem("Tools/E-stagiar.io/NPC: desativar caminhada nos selecionados")]
    public static void Desativar()
    {
        foreach (GameObject go in Selection.gameObjects)
        {
            var c = go.GetComponent<NpcCaminhada>();
            if (c == null) continue;
            Undo.RecordObject(c, "Desativar caminhada");
            c.enabled = false;
            EditorUtility.SetDirty(c);
        }
    }
}
