using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Caminhada independente de um NPC pelo escritório (NavMesh): vai de um destino válido a outro, para um pouco e segue.
/// Não persegue nem olha para o jogador (o agente só gira para onde anda). Vai no ROOT do NPC, ao lado do
/// <see cref="NavMeshAgent"/>. Só anda quem tem este componente LIGADO: nada é habilitado sozinho (menu
/// <i>Tools &gt; E-stagiar.io &gt; NPC: ativar caminhada nos selecionados</i>, que recusa os NPCs das estações).
/// Destinos: os filhos de <see cref="destinos"/> (ex.: <c>Navegacao_NPCs/Pontos_Circulacao</c>); vazio = pontos
/// sorteados no NavMesh até <see cref="raioSemDestinos"/> do ponto de partida.
/// Sem aglomeração: um destino ocupado (reservado por outro NPC ou com alguém perto) não é escolhido; agente parado
/// por <see cref="tempoPreso"/> s troca de destino.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class NpcCaminhada : MonoBehaviour
{
    private static readonly List<NpcCaminhada> ativos = new List<NpcCaminhada>();

    [Tooltip("Pai dos pontos de destino (cada filho é um destino). Vazio = pontos aleatórios no NavMesh.")]
    [SerializeField] private Transform destinos;
    [Tooltip("Sem destinos: raio (m) em volta do ponto de partida para sortear pontos no NavMesh.")]
    [Min(1f)] [SerializeField] private float raioSemDestinos = 8f;
    [Tooltip("Pausa (s) em cada destino: mínimo e máximo.")]
    [SerializeField] private Vector2 pausa = new Vector2(2f, 6f);
    [Tooltip("Distância mínima (m) entre o destino escolhido e outros NPCs (e os destinos deles).")]
    [Min(0.5f)] [SerializeField] private float espacoPessoal = 1.5f;
    [Tooltip("Parado (sem conseguir andar) por este tempo (s) com caminho ativo: escolhe outro destino.")]
    [Min(0.5f)] [SerializeField] private float tempoPreso = 3f;

    [Header("Movimento natural")]
    [Tooltip("Giro máximo (graus/s). O corpo vira para onde o NPC REALMENTE anda (não para o rumo planejado): ao desviar " +
             "de alguém ele não anda de lado.")]
    [Min(30f)] [SerializeField] private float giro = 200f;
    [Tooltip("Em curva fechada (mais que este ângulo entre o corpo e o rumo) o NPC quase para e vira antes de seguir.")]
    [Range(10f, 120f)] [SerializeField] private float anguloParaDesacelerar = 50f;
    [Tooltip("Modelo (filho) que balança ao andar, como o chefe. Vazio = o primeiro filho com renderer.")]
    [SerializeField] private Transform visual;
    [SerializeField] private float alturaBalanco = 0.025f;
    [SerializeField] private float frequenciaBalanco = 1.8f;

    private NavMeshAgent agent;
    private Vector3 inicio;
    private Vector3? destino;
    private float esperarAte, paradoDesde = -1f;
    private int ultimoIndice = -1;
    private float velocidadeBase;
    private Vector3 visualRepouso;
    private float fasePasso;

    /// <summary>Destino atual (null = parado esperando).</summary>
    public Vector3? Destino => destino;
    public Transform Destinos { get => destinos; set => destinos = value; }

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        inicio = transform.position;
        velocidadeBase = agent.speed;
        agent.updateRotation = false;   // a rotação é feita aqui, pela velocidade real
        if (visual == null)
            foreach (Transform c in transform) if (c.GetComponentInChildren<Renderer>() != null) { visual = c; break; }
        if (visual != null) visualRepouso = visual.localPosition;
    }

    private void Start() => NavMeshSpawn.Garantir(agent);   // nasceu fora do NavMesh: vai ao ponto válido mais próximo

    private void OnEnable()
    {
        if (!ativos.Contains(this)) ativos.Add(this);
        NpcSeparacao.Registrar(agent);
        esperarAte = Time.time + Random.Range(0f, pausa.y);   // não saem todos juntos
    }

    private void OnDisable()
    {
        ativos.Remove(this);
        NpcSeparacao.Remover(agent);
        destino = null;
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh) agent.ResetPath();
    }

    private void Update()
    {
        if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh) return;
        NpcSeparacao.Aplicar(agent);   // não atravessar outros NPCs (parados ou andando)
        Girar();
        Balancar();
        if (destino == null)
        {
            if (Time.time >= esperarAte) Escolher();
            return;
        }
        if (agent.pathPending) return;
        if (agent.remainingDistance <= agent.stoppingDistance + 0.05f)
        {
            destino = null;
            agent.ResetPath();
            esperarAte = Time.time + Random.Range(pausa.x, pausa.y);
            return;
        }
        // preso (bloqueado por outro agente, porta, objeto): troca de destino
        if (agent.velocity.sqrMagnitude < 0.01f)
        {
            if (paradoDesde < 0f) paradoDesde = Time.time;
            else if (Time.time - paradoDesde > tempoPreso) { paradoDesde = -1f; Escolher(); }
        }
        else paradoDesde = -1f;
    }

    // Corpo virado para a velocidade REAL (com o desvio de outros agentes ela difere do rumo); em curva fechada a
    // velocidade cai, então o NPC vira quase parado em vez de deslizar de lado ou dar um giro brusco andando.
    private void Girar()
    {
        Vector3 rumo = agent.desiredVelocity.sqrMagnitude > 0.01f ? agent.desiredVelocity : agent.velocity;
        rumo.y = 0f;
        if (rumo.sqrMagnitude < 0.0025f) { agent.speed = velocidadeBase; return; }
        float angulo = Vector3.Angle(transform.forward, rumo);
        agent.speed = velocidadeBase * Mathf.Lerp(1f, 0.15f, Mathf.InverseLerp(anguloParaDesacelerar * 0.4f, anguloParaDesacelerar, angulo));
        Vector3 real = agent.velocity; real.y = 0f;
        Vector3 olhar = real.sqrMagnitude > 0.04f ? real : rumo;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(olhar), giro * Time.deltaTime);
    }

    // Balanço vertical leve do modelo ao andar (o mesmo do chefe, SupervisorPatrol.Bob): só o filho visual se move; o
    // agente e o colisor ficam no chão.
    private void Balancar()
    {
        if (visual == null) return;
        float v = agent.velocity.magnitude;
        if (v > 0.05f) fasePasso += Time.deltaTime * frequenciaBalanco * Mathf.PI * 2f;
        float h = v > 0.05f ? Mathf.Abs(Mathf.Sin(fasePasso)) * alturaBalanco * Mathf.Clamp01(v / Mathf.Max(0.1f, velocidadeBase)) : 0f;
        visual.localPosition = Vector3.Lerp(visual.localPosition, visualRepouso + Vector3.up * h, 1f - Mathf.Exp(-12f * Time.deltaTime));
    }

    /// <summary>Escolhe e segue um destino livre (público para testes).</summary>
    public bool Escolher()
    {
        for (int tentativa = 0; tentativa < 12; tentativa++)
        {
            Vector3 alvo;
            if (destinos != null && destinos.childCount > 0)
            {
                int i = Random.Range(0, destinos.childCount);
                if (i == ultimoIndice && destinos.childCount > 1) continue;
                alvo = destinos.GetChild(i).position;
                if (!Livre(alvo)) continue;
                ultimoIndice = i;
            }
            else
            {
                Vector2 r = Random.insideUnitCircle * raioSemDestinos;
                alvo = inicio + new Vector3(r.x, 0f, r.y);
                if (!Livre(alvo)) continue;
            }
            if (!NavMesh.SamplePosition(alvo, out NavMeshHit hit, 0.5f, agent.areaMask)) continue;
            var path = new NavMeshPath();
            if (!agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete) continue;
            agent.SetPath(path);
            destino = hit.position;
            paradoDesde = -1f;
            return true;
        }
        esperarAte = Time.time + 1f;
        return false;
    }

    // Ninguém (outro NPC andando, ou indo para lá) a menos do espaço pessoal.
    private bool Livre(Vector3 p)
    {
        foreach (NpcCaminhada o in ativos)
        {
            if (o == this) continue;
            if ((o.transform.position - p).sqrMagnitude < espacoPessoal * espacoPessoal) return false;
            if (o.destino.HasValue && (o.destino.Value - p).sqrMagnitude < espacoPessoal * espacoPessoal) return false;
        }
        return true;
    }
}
