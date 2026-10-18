using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Ronda do supervisor (NPC): anda pelo NavMesh entre os pontos de uma rota, para um tempo variável em cada ponto e
/// segue. Às vezes, quando passa perto do estagiário, para por alguns segundos e vira-se (suavemente) para olhá-lo;
/// na maioria das passagens só continua andando. Depois retoma a ronda de onde estava.
/// Só movimento e observação: NÃO detecta infrações, não tira vidas nem pontua (etapas futuras).
///
/// Rota = filhos de <see cref="route"/>, na ordem da hierarquia (mover/adicionar/remover pontos não exige código).
/// Vai no root lógico do NPC, com um <see cref="NavMeshAgent"/>; o modelo fica no filho <see cref="visual"/>.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class SupervisorPatrol : MonoBehaviour
{
    /// <summary>Reacting: presenciou uma infração (ver <see cref="React"/>). Valores novos no fim.</summary>
    public enum PatrolState { Walking, Waiting, Watching, Reacting }

    [Header("Ronda")]
    [Tooltip("Pai dos pontos da ronda: cada filho é um ponto, na ordem da hierarquia.")]
    [SerializeField] private Transform route;
    [Tooltip("Ligado: escolhe o próximo ponto ao acaso (nunca o mesmo duas vezes seguidas). Desligado: segue a ordem.")]
    [SerializeField] private bool randomOrder;
    [Tooltip("Tempo parado em cada ponto (s): sorteado entre X e Y.")]
    [SerializeField] private Vector2 waitRange = new Vector2(2f, 5f);
    [Tooltip("Distância (m) para considerar o ponto alcançado.")]
    [Min(0.05f)] [SerializeField] private float arriveDistance = 0.25f;

    [Header("Olhar para o estagiário")]
    [Tooltip("Jogador observado. Vazio = procura o PlayerMovement da cena.")]
    [SerializeField] private Transform player;
    [Tooltip("Distância (m) em que o supervisor 'passa perto' do jogador.")]
    [Min(0.5f)] [SerializeField] private float noticeDistance = 3f;
    [Tooltip("Chance (0-1) de parar para olhar em cada passagem perto do jogador (decidida uma vez por passagem).")]
    [Range(0f, 1f)] [SerializeField] private float watchChance = 0.35f;
    [Tooltip("Quanto tempo (s) fica olhando: sorteado entre X e Y.")]
    [SerializeField] private Vector2 watchDuration = new Vector2(2f, 4f);
    [Tooltip("Tempo mínimo (s) entre duas paradas para olhar.")]
    [Min(0f)] [SerializeField] private float watchCooldown = 10f;
    [Tooltip("Só olha se enxergar o jogador (nada bloqueando entre os dois).")]
    [SerializeField] private bool requireLineOfSight = true;
    [Tooltip("Velocidade máxima de giro (graus/s) ao virar para o jogador e ao voltar a andar.")]
    [Min(10f)] [SerializeField] private float turnSpeed = 150f;
    [Tooltip("Altura (m) dos olhos do supervisor e do jogador para a linha de visão.")]
    [SerializeField] private float eyeHeight = 1.5f;

    [Header("Espaço pessoal e travamento")]
    [Tooltip("Se o jogador estiver mais perto que isto (m) à frente do supervisor enquanto ele anda, ele para e espera " +
             "(o caminho em volta do jogador é recalculado pelo NavMeshObstacle do jogador).")]
    [Min(0.3f)] [SerializeField] private float personalSpace = 1.5f;
    [Tooltip("Tempo máximo (s) esperando o jogador sair da frente antes de seguir contornando.")]
    [Min(0f)] [SerializeField] private float yieldTime = 1.5f;
    [Tooltip("Parado sem conseguir andar por este tempo (s): desiste do ponto e vai para o próximo.")]
    [Min(0.5f)] [SerializeField] private float stuckTime = 4f;

    [Header("Olhar em volta nas pausas")]
    [Tooltip("Pontos de interesse (estações, bancada...). Em cada pausa ele se vira para um deles, ao acaso: é o que dá " +
             "oportunidades reais de perceber o que acontece ali, sem perseguir o jogador. Vazio = não olha em volta.")]
    [SerializeField] private Transform[] interestPoints = new Transform[0];
    [Tooltip("Velocidade (°/s) ao virar para o ponto de interesse.")]
    [Min(10f)] [SerializeField] private float scanTurnSpeed = 90f;

    [Header("Reação a infração (modelo sem animação: só giros do corpo)")]
    [Tooltip("Tempo (s) virando para o local da infração.")]
    [Min(0.1f)] [SerializeField] private float reactFaceTime = 0.8f;
    [Tooltip("Tempo (s) virando para o jogador.")]
    [Min(0f)] [SerializeField] private float reactPlayerTime = 0.9f;
    [Tooltip("Tempo (s) do 'balançar a cabeça' (o corpo gira de um lado para o outro, reprovando).")]
    [Min(0f)] [SerializeField] private float shakeTime = 1.1f;
    [SerializeField] private float shakeAngle = 14f;
    [SerializeField] private int shakeCycles = 3;

    [Header("Visual (modelo sem animação)")]
    [Tooltip("Modelo (filho). Balança levemente ao andar. Vazio = sem balanço.")]
    [SerializeField] private Transform visual;
    [SerializeField] private float bobHeight = 0.025f;
    [SerializeField] private float bobFrequency = 1.8f;

    private NavMeshAgent agent;
    private PatrolState state = PatrolState.Walking;
    private PatrolState resumeState;
    private int index = -1;
    private float timer;
    private float lastWatchTime = float.NegativeInfinity;
    private bool playerWasNear;
    private Vector3 visualRest;
    private float bobPhase;
    private bool yielding;
    private float waitTimer;
    private Transform scanTarget;
    private Vector3 reactPoint;
    private float reactStart, reactBaseYaw;
    private bool reactShakeStarted;
    private float yieldTimer, yieldIgnoreUntil, stuckTimer;

    public PatrolState State => state;
    /// <summary>Índice (na rota) do ponto atual/destino.</summary>
    public int CurrentIndex => index;
    public int PointCount => route != null ? route.childCount : 0;
    /// <summary>Quantas vezes chegou a um ponto (para testes/depuração).</summary>
    public int Arrivals { get; private set; }
    /// <summary>Está parado esperando o jogador sair da frente.</summary>
    public bool Yielding => yielding;
    /// <summary>Quantas vezes desistiu de um ponto por ficar preso.</summary>
    public int SkippedPoints { get; private set; }
    /// <summary>Quantas vezes reagiu a uma infração.</summary>
    public int ReactionCount { get; private set; }
    /// <summary>Disparado quando começa a reagir a uma infração (posição do que ele viu).</summary>
    public event System.Action<Vector3> Reacted;
    /// <summary>Quantas vezes parou para olhar o jogador.</summary>
    public int WatchCount { get; private set; }
    public Transform Player { get => player; set => player = value; }

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (visual != null) visualRest = visual.localPosition;
    }

    private void Start()
    {
        if (player == null)
        {
            var p = FindAnyObjectByType<PlayerMovement>();
            if (p != null) player = p.transform;
        }
        NavMeshSpawn.Garantir(agent);   // nasceu fora do NavMesh: vai ao ponto válido mais próximo
        GoToNext();
    }

    private void Update()
    {
        if (agent == null || !agent.isOnNavMesh || PointCount == 0) return;

        if (state != PatrolState.Watching && state != PatrolState.Reacting) CheckPlayer();

        switch (state)
        {
            case PatrolState.Walking:
                if (UpdateYield()) break;
                if (!agent.pathPending && agent.remainingDistance > arriveDistance && agent.velocity.sqrMagnitude < 0.0025f)
                {
                    stuckTimer += Time.deltaTime;
                    if (stuckTimer >= stuckTime) { SkippedPoints++; GoToNext(); break; }
                }
                else stuckTimer = 0f;
                if (!agent.pathPending && agent.remainingDistance <= arriveDistance)
                {
                    Arrivals++;
                    state = PatrolState.Waiting;
                    waitTimer = Random.Range(waitRange.x, Mathf.Max(waitRange.x, waitRange.y));
                    scanTarget = interestPoints.Length > 0 ? interestPoints[Random.Range(0, interestPoints.Length)] : null;
                    agent.updateRotation = scanTarget == null;
                }
                break;
            case PatrolState.Waiting:
                if (scanTarget != null) TurnTowards(scanTarget.position, scanTurnSpeed);
                waitTimer -= Time.deltaTime;
                if (waitTimer <= 0f) GoToNext();
                break;
            case PatrolState.Reacting:
                UpdateReaction();
                break;
            case PatrolState.Watching:
                FacePlayer();
                timer -= Time.deltaTime;
                if (timer <= 0f) StopWatching();
                break;
        }
        Bob();
    }

    // Jogador perto e à frente: para e espera (sem empurrar nem atravessar); depois de 'yieldTime' segue contornando.
    private bool UpdateYield()
    {
        bool blocking = PlayerInPath();
        if (!yielding)
        {
            if (!blocking || Time.time < yieldIgnoreUntil) return false;
            yielding = true;
            yieldTimer = yieldTime;
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
            return true;
        }
        yieldTimer -= Time.deltaTime;
        if (blocking && yieldTimer > 0f) return true;
        yielding = false;
        if (blocking) yieldIgnoreUntil = Time.time + 2f;           // o jogador ficou: contorna pelo caminho recalculado
        agent.isStopped = false;
        if (index >= 0) agent.SetDestination(route.GetChild(index).position);
        return false;
    }

    private bool PlayerInPath()
    {
        if (player == null) return false;
        Vector3 d = player.position - transform.position;
        d.y = 0f;
        if (d.sqrMagnitude > personalSpace * personalSpace) return false;
        Vector3 heading = agent.desiredVelocity.sqrMagnitude > 0.01f ? agent.desiredVelocity : transform.forward;
        heading.y = 0f;
        return Vector3.Dot(heading.normalized, d.normalized) > 0.2f;
    }

    private void GoToNext()
    {
        stuckTimer = 0f;
        yielding = false;
        int n = PointCount;
        if (n == 0) return;
        if (randomOrder && n > 1)
        {
            int next;
            do next = Random.Range(0, n); while (next == index);
            index = next;
        }
        else index = (index + 1) % n;
        state = PatrolState.Walking;
        agent.updateRotation = true;
        if (agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.SetDestination(route.GetChild(index).position);
        }
    }

    // Uma decisão por passagem: ao entrar no raio, sorteia se vai parar para olhar (respeitando o intervalo mínimo).
    private void CheckPlayer()
    {
        if (player == null) return;
        Vector3 d = player.position - transform.position;
        d.y = 0f;
        float dist = d.magnitude;
        bool near = dist <= noticeDistance;
        if (near && !playerWasNear && Time.time - lastWatchTime >= watchCooldown && Random.value < watchChance && CanSeePlayer())
            StartWatching();
        if (near) playerWasNear = true;
        else if (dist > noticeDistance + 0.5f) playerWasNear = false;   // folga: não sorteia de novo na borda do raio
    }

    private bool CanSeePlayer()
    {
        if (!requireLineOfSight) return true;
        Vector3 eye = transform.position + Vector3.up * eyeHeight;
        Vector3 target = player.position + Vector3.up * 0.2f;
        foreach (RaycastHit h in Physics.RaycastAll(eye, (target - eye).normalized, Vector3.Distance(eye, target), ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.transform.IsChildOf(transform) || h.collider.transform.IsChildOf(player)) continue;
            if (h.collider.attachedRigidbody != null && !h.collider.attachedRigidbody.isKinematic) continue;  // itens soltos
            return false;
        }
        return true;
    }

    private void StartWatching()
    {
        resumeState = state;
        state = PatrolState.Watching;
        timer = Random.Range(watchDuration.x, Mathf.Max(watchDuration.x, watchDuration.y));
        lastWatchTime = Time.time;
        WatchCount++;
        agent.isStopped = true;
        agent.velocity = Vector3.zero;
        agent.updateRotation = false;
    }

    private void StopWatching()
    {
        agent.updateRotation = true;
        state = resumeState;
        Resume();
        lastWatchTime = Time.time;
    }

    // Volta ao que fazia (andando: segue o caminho; na pausa: termina a pausa, ou uma curta se já tinha acabado).
    private void Resume()
    {
        if (state == PatrolState.Walking) agent.isStopped = false;
        else if (waitTimer <= 0f) waitTimer = Random.Range(waitRange.x * 0.5f, Mathf.Max(waitRange.x, waitRange.y) * 0.5f);
        agent.updateRotation = state == PatrolState.Walking || scanTarget == null;
    }

    /// <summary>
    /// Presenciou uma infração em <paramref name="point"/>: interrompe a ronda, vira para o local, depois para o jogador,
    /// "balança a cabeça" (giro curto do corpo, já que o modelo não tem animações) e retoma de onde estava.
    /// Ignorado se já estiver reagindo (a infração continua registrada como vista).
    /// </summary>
    public void React(Vector3 point)
    {
        if (state == PatrolState.Reacting) return;
        if (state != PatrolState.Watching) resumeState = state;   // olhando: volta ao que fazia antes de olhar
        state = PatrolState.Reacting;
        reactPoint = point;
        reactStart = Time.time;
        reactShakeStarted = false;
        ReactionCount++;
        agent.isStopped = true;
        agent.velocity = Vector3.zero;
        agent.updateRotation = false;
        Reacted?.Invoke(point);
    }

    private void UpdateReaction()
    {
        float t = Time.time - reactStart;
        if (t < reactFaceTime) { TurnTowards(reactPoint, turnSpeed * 1.5f); return; }
        t -= reactFaceTime;
        if (t < reactPlayerTime) { if (player != null) TurnTowards(player.position, turnSpeed * 1.5f); return; }
        t -= reactPlayerTime;
        if (t < shakeTime)
        {
            if (!reactShakeStarted) { reactShakeStarted = true; reactBaseYaw = transform.eulerAngles.y; }
            float k = t / shakeTime;
            float yaw = reactBaseYaw + Mathf.Sin(k * Mathf.PI * 2f * shakeCycles) * shakeAngle * (1f - k);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            return;
        }
        if (reactShakeStarted) transform.rotation = Quaternion.Euler(0f, reactBaseYaw, 0f);
        state = resumeState;
        Resume();
    }

    private void TurnTowards(Vector3 target, float speed)
    {
        Vector3 d = target - transform.position;
        d.y = 0f;
        if (d.sqrMagnitude < 1e-4f) return;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), speed * Time.deltaTime);
    }

    private void FacePlayer()
    {
        if (player == null) return;
        Vector3 d = player.position - transform.position;
        d.y = 0f;
        if (d.sqrMagnitude < 1e-4f) return;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), turnSpeed * Time.deltaTime);
    }

    /// <summary>Ângulo (graus) entre a frente do supervisor e a direção do jogador (0 = olhando direto).</summary>
    public float AngleToPlayer()
    {
        if (player == null) return 180f;
        Vector3 d = player.position - transform.position;
        d.y = 0f;
        return Vector3.Angle(transform.forward, d);
    }

    private void Bob()
    {
        if (visual == null || bobHeight <= 0f) return;
        float speed = agent.velocity.magnitude;
        if (speed > 0.05f) bobPhase += Time.deltaTime * bobFrequency * Mathf.PI * 2f;
        else bobPhase = Mathf.MoveTowards(bobPhase, Mathf.Round(bobPhase / Mathf.PI) * Mathf.PI, Time.deltaTime * 4f);
        visual.localPosition = visualRest + Vector3.up * Mathf.Abs(Mathf.Sin(bobPhase)) * bobHeight;
    }
}
