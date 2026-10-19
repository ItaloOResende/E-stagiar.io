using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Impede que NPCs (agentes do NavMesh) se atravessem. A desvio da Unity (RVO) só ajusta a velocidade de quem está
/// andando e é "macio": em cruzamentos de frente num corredor estreito, com um NPC parado no caminho ou entre agentes
/// de MESMA prioridade (os caminhantes eram todos 55), os dois cedem pela metade e acabam passando um por dentro do
/// outro. Aqui, a cada quadro, quem estiver sobreposto é afastado pelo próprio <see cref="NavMeshAgent.Move"/> (respeita
/// as bordas do NavMesh), e quem tem prioridade MENOR (número maior) é quem sai da frente.
/// Também desempata prioridades iguais ao registrar, para o RVO saber quem cede. Sem componente na cena: o
/// <see cref="SupervisorPatrol"/> e o <see cref="NpcCaminhada"/> registram o agente e chamam <see cref="Aplicar"/>.
/// </summary>
public static class NpcSeparacao
{
    /// <summary>Folga (m) além da soma dos raios.</summary>
    public const float Folga = 0.08f;
    /// <summary>Fração da sobreposição corrigida por quadro (suaviza o empurrão).</summary>
    private const float Correcao = 0.5f;
    /// <summary>Velocidade máxima (m/s) do afastamento: sem limite, NPCs sobrepostos "pulavam" de um quadro para o outro.</summary>
    private const float VelocidadeMaxima = 0.8f;

    private static readonly List<NavMeshAgent> agentes = new List<NavMeshAgent>();

    public static IReadOnlyList<NavMeshAgent> Agentes => agentes;

    public static void Registrar(NavMeshAgent agent)
    {
        if (agent == null || agentes.Contains(agent)) return;
        // prioridades iguais: o RVO divide o desvio meio a meio e nenhum cede de fato
        while (agentes.Exists(a => a != null && a.avoidancePriority == agent.avoidancePriority) && agent.avoidancePriority < 99)
            agent.avoidancePriority++;
        agentes.Add(agent);
    }

    public static void Remover(NavMeshAgent agent) => agentes.Remove(agent);

    /// <summary>Raio real do agente no mundo (o raio do NavMeshAgent é multiplicado pela escala).</summary>
    public static float Raio(NavMeshAgent a)
    {
        Vector3 s = a.transform.lossyScale;
        return a.radius * Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.z));
    }

    /// <summary>Afasta este agente dos que estiverem sobrepostos a ele. Chamar no Update de quem controla o agente.</summary>
    public static void Aplicar(NavMeshAgent self)
    {
        if (self == null || !self.isActiveAndEnabled || !self.isOnNavMesh) return;
        Vector3 p = self.transform.position;
        Vector3 empurrao = Vector3.zero;
        foreach (NavMeshAgent o in agentes)
        {
            if (o == null || o == self || !o.isActiveAndEnabled || !o.isOnNavMesh) continue;
            Vector3 d = p - o.transform.position;
            d.y = 0f;
            if (Mathf.Abs(p.y - o.transform.position.y) > 1f) continue;   // outro andar
            float min = Raio(self) + Raio(o) + Folga;
            float dist = d.magnitude;
            if (dist >= min) continue;
            // quem tem prioridade menor (número maior) sai da frente; empate (não deveria haver) = meio a meio
            float parte = self.avoidancePriority > o.avoidancePriority ? 1f
                        : self.avoidancePriority < o.avoidancePriority ? 0f : 0.5f;
            if (parte <= 0f) continue;
            Vector3 dir = dist > 0.001f ? d / dist
                        : Quaternion.Euler(0f, agentes.IndexOf(self) * 137f, 0f) * Vector3.forward;   // exatamente juntos
            empurrao += dir * ((min - dist) * parte);
        }
        if (empurrao.sqrMagnitude > 1e-8f) self.Move(Vector3.ClampMagnitude(empurrao * Correcao, VelocidadeMaxima * Time.deltaTime));
    }
}
