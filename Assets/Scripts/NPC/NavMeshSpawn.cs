using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// NPC que nasce fora do NavMesh (posto um pouco acima do piso, dentro de um móvel ou na borda da área assada) não
/// anda: a Unity avisa "Failed to create agent because there is no valid NavMesh" e o agente fica parado. Aqui ele é
/// levado ao ponto do NavMesh mais próximo (<see cref="NavMeshAgent.Warp"/>), sem mexer na cena.
/// </summary>
public static class NavMeshSpawn
{
    /// <summary>Põe o agente no NavMesh se ele ainda não estiver. Devolve se ficou no NavMesh.</summary>
    public static bool Garantir(NavMeshAgent agent, float raioMaximo = 6f)
    {
        if (agent == null || !agent.enabled) return false;
        if (agent.isOnNavMesh) return true;
        Vector3 p = agent.transform.position;
        // procura perto primeiro (evita pular para o outro lado de uma divisória), depois mais longe
        for (float r = 0.5f; r <= raioMaximo; r *= 2f)
        {
            if (!NavMesh.SamplePosition(p, out NavMeshHit hit, r, agent.areaMask)) continue;
            if (agent.Warp(hit.position))
            {
                Debug.Log($"[NavMesh] {agent.name} nasceu fora do NavMesh; levado a {hit.position} ({Vector3.Distance(p, hit.position):0.00} m).", agent);
                return true;
            }
        }
        Debug.LogWarning($"[NavMesh] {agent.name}: nenhum NavMesh a {raioMaximo} m de {p}; o NPC fica parado.", agent);
        return false;
    }
}
