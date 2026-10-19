using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// O que o supervisor consegue PERCEBER das infrações abertas no <see cref="InfractionLog"/> (não é câmera de segurança):
///  - VISÃO: até <see cref="viewDistance"/> m, dentro do cone de <see cref="fieldOfView"/> graus à frente do corpo e com
///    linha de visão livre dos olhos até o ponto (paredes, móveis, gaveteiros bloqueiam; o jogador e o próprio item não).
///  - AUDIÇÃO (só quedas e quebras, que fazem barulho): até <see cref="hearingRadius"/> m em qualquer direção, também
///    sem parede no meio. Ele "ouve" e se vira — a percepção acontece de fato, não é descoberta depois.
///  - Item largado no chão: só por visão e até <see cref="floorNoticeDistance"/> m (precisa passar perto para reparar).
/// Cada infração é marcada vista UMA vez (o log ignora repetições). A reação (parar, olhar, reprovar) é do
/// <see cref="SupervisorPatrol"/>; o aviso ao jogador é do HUD; pontuação/vidas não existem aqui.
/// </summary>
public class SupervisorPerception : MonoBehaviour
{
    [Tooltip("Alcance da visão (m).")]
    [Min(0.5f)] [SerializeField] private float viewDistance = 8f;
    [Tooltip("Abertura do campo de visão (graus, total). 110 ≈ visão de quem anda olhando em frente, sem a periferia.")]
    [Range(10f, 360f)] [SerializeField] private float fieldOfView = 110f;
    [Tooltip("Ouve quedas/quebras até esta distância (m), em qualquer direção. 0 = não ouve.")]
    [Min(0f)] [SerializeField] private float hearingRadius = 3f;
    [Tooltip("Distância máxima (m) para reparar num item largado no chão.")]
    [Min(0.5f)] [SerializeField] private float floorNoticeDistance = 5f;
    [Tooltip("Altura dos olhos (m) acima do root.")]
    [SerializeField] private float eyeHeight = 1.5f;
    [Tooltip("Camadas que bloqueiam a visão.")]
    [SerializeField] private LayerMask occluders = ~0;
    [Tooltip("Jogador (o corpo dele não bloqueia a visão). Vazio = o do SupervisorPatrol.")]
    [SerializeField] private Transform player;
    [Tooltip("Reagir (parar, olhar, reprovar) ao perceber. Desligado = só registra.")]
    [SerializeField] private bool react = true;

    [Header("Desempenho e audicao")]
    [SerializeField, Min(0.02f)] private float perceptionInterval = 0.1f;
    [SerializeField, Min(0f)] private float hearingWindow = 0.6f;
    private float nextScan;
    private readonly RaycastHit[] hits = new RaycastHit[64];
    private readonly Dictionary<GameObject, Renderer[]> rendererCache = new Dictionary<GameObject, Renderer[]>();
    private SupervisorFeedback feedback;
    private SupervisorPatrol patrol;
    private void OnDisable() { rendererCache.Clear(); nextScan = 0f; }


    public float ViewDistance { get => viewDistance; set => viewDistance = value; }
    public float FieldOfView { get => fieldOfView; set => fieldOfView = value; }
    public float HearingRadius { get => hearingRadius; set => hearingRadius = value; }
    public float FloorNoticeDistance { get => floorNoticeDistance; set => floorNoticeDistance = value; }

    private void Awake() { patrol = GetComponent<SupervisorPatrol>(); feedback = GetComponent<SupervisorFeedback>(); }

    private void Update()
    {
        if (Time.time < nextScan || Time.timeScale <= 0f) return;
        nextScan = Time.time + Mathf.Max(0.02f, perceptionInterval);
        if (rendererCache.Count > 256) rendererCache.Clear();
        var open = InfractionLog.Open;
        for (int i = open.Count - 1; i >= 0; i--)
        {
            if (i >= open.Count) continue;
            Infraction inf = open[i];
            if (!CanPerceive(inf)) continue;
            if (!InfractionLog.MarkWitnessed(inf, transform)) continue;
            if (feedback != null) feedback.NotifyInfraction();
            if (react && patrol != null) patrol.React(CurrentPosition(inf));
        }
    }

    private Vector3 CurrentPosition(Infraction inf) =>
        inf.Item != null && inf.Item.activeInHierarchy ? Center(inf.Item) : inf.Position;

    private Vector3 Center(GameObject g)
    {
        if (!rendererCache.TryGetValue(g, out Renderer[] rs))
        { rs = g.GetComponentsInChildren<Renderer>(); rendererCache[g] = rs; }
        if (rs.Length == 0) return g.transform.position;
        Bounds b = new Bounds(g.transform.position, Vector3.zero);
        bool found = false;
        foreach (Renderer r in rs) if (r != null && r.enabled) { if (!found) { b = r.bounds; found = true; } else b.Encapsulate(r.bounds); }
        return b.center;
    }

    /// <summary>O supervisor percebe esta infração agora (visão, ou audição para quedas/quebras)?</summary>
    public bool CanPerceive(Infraction inf)
    {
        if (inf == null || inf.Resolved) return false;
        Vector3 target = CurrentPosition(inf);
        Vector3 flat = target - transform.position; flat.y = 0f;
        float dist = flat.magnitude;
        bool audible = !inf.IsOngoing && hearingRadius > 0f &&
            Time.time - inf.Time <= hearingWindow && dist <= hearingRadius;
        float range = inf.IsOngoing ? Mathf.Min(viewDistance, floorNoticeDistance) : viewDistance;
        bool visible = dist <= range && (dist <= 0.3f || Vector3.Angle(transform.forward, flat) <= fieldOfView * 0.5f);
        return (audible || visible) && LineOfSight(transform.position + Vector3.up * eyeHeight, target, inf.Item);
    }

    private bool LineOfSight(Vector3 eye, Vector3 target, GameObject item)
    {
        Transform p = player != null ? player : patrol != null ? patrol.Player : null;
        Vector3 d = target - eye;
        int count = Physics.RaycastNonAlloc(eye, d.normalized, hits, d.magnitude, occluders, QueryTriggerInteraction.Ignore);
        if (count == hits.Length) return false; // Conservador quando o buffer enche.
        for (int i = 0; i < count; i++)
        {
            RaycastHit h = hits[i];
            Transform t = h.collider.transform;
            if (t.IsChildOf(transform) || item != null && t.IsChildOf(item.transform) || p != null && t.IsChildOf(p)) continue;
            if (h.collider.attachedRigidbody != null && !h.collider.attachedRigidbody.isKinematic) continue;   // outros itens soltos
            return false;
        }
        return true;
    }
}
