using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Detecta as infrações que ACONTECEM (sem saber se alguém viu) e as registra no <see cref="InfractionLog"/>. Um por cena
/// (objeto <c>Conduta</c>). Reaproveita o que já existe:
///  - queda/quebra: o evento de dano de QUALQUER quebrável (<see cref="Breakables.AnyConditionChanged"/>: peças do PC e
///    equipamentos como monitor e gabinete, disparado pelo <see cref="PartFallDamage"/> só em impactos reais). Queda sem
///    quebra = <see cref="InfractionKind.ComponentDropped"/> (continua funcionando); quebra =
///    <see cref="InfractionKind.ComponentBroken"/> (estado de dano mantido).
///    São instantâneas: o supervisor só pode percebê-las por <see cref="witnessWindow"/> segundos; depois, despercebidas.
///  - item largado no chão: peça (do PC, do notebook ou da impressora), ferramenta, cabo ou pendrive solto
///    (<see cref="PickupInteractable.Placement"/> caído ou colocado), parado, com a base perto do chão e sem mesa/bancada
///    (<see cref="WorkSurface"/>) embaixo, por mais de <see cref="abandonDelay"/> segundos. Uma infração por "abandono":
///    só volta a contar depois de o item sair do chão. Monitor, teclado, mouse e gabinete NÃO entram (são equipamentos
///    carregados e apoiados durante o trabalho; a queda deles conta pelo dano).
/// Itens que nunca saíram do lugar inicial (<see cref="ItemPlacement.Initial"/>: cabos e caixas que a cena já começa no
/// chão, itens criados pelo jogo e postos no lugar por ele) não contam; o que o JOGADOR soltou ou largou, conta.
/// </summary>
public class ConductMonitor : MonoBehaviour
{
    [Header("Queda e quebra")]
    [Tooltip("Tempo (s) depois da queda em que o supervisor ainda pode percebê-la (ex.: virar-se com o barulho).")]
    [Min(0f)] [SerializeField] private float witnessWindow = 1.2f;

    [Header("Item largado no chão")]
    [Tooltip("Tolerância (s) no chão antes de virar 'largado' (dá tempo de soltar, reposicionar e recolher).")]
    [Min(0f)] [SerializeField] private float abandonDelay = 20f;
    [Tooltip("Altura máxima (m) da base do item para contar como 'no chão'.")]
    [SerializeField] private float floorHeight = 0.25f;
    [Tooltip("Velocidade (m/s) abaixo da qual o item é considerado parado.")]
    [SerializeField] private float restSpeed = 0.1f;
    [SerializeField] private bool trackParts = true, trackTools = true, trackCables = true, trackPendrives = true;
    [Tooltip("Peças e consumíveis dos minigames: memória SO-DIMM e SSD do notebook, toner e papel da impressora.")]
    [SerializeField] private bool trackMinigameParts = true;
    [Tooltip("Intervalo (s) entre verificações do chão.")]
    [Min(0.05f)] [SerializeField] private float scanInterval = 0.25f;

    private class FloorState { public float since = -1f; public Infraction flagged; }
    private readonly Dictionary<PickupInteractable, FloorState> floor = new Dictionary<PickupInteractable, FloorState>();
    private readonly List<PickupInteractable> scratch = new List<PickupInteractable>();
    // Desempenho (WebGL): a varredura roda 4x por segundo sobre todos os itens; nada de alocar a cada passada.
    private readonly List<PickupInteractable> gone = new List<PickupInteractable>();
    private readonly Dictionary<PickupInteractable, bool> tracked = new Dictionary<PickupInteractable, bool>();
    private static readonly RaycastHit[] hits = new RaycastHit[16];
    private float nextScan;

    public float AbandonDelay { get => abandonDelay; set => abandonDelay = Mathf.Max(0f, value); }
    public float WitnessWindow { get => witnessWindow; set => witnessWindow = Mathf.Max(0f, value); }

    private void OnEnable() => Breakables.AnyConditionChanged += OnPartCondition;
    private void OnDisable() => Breakables.AnyConditionChanged -= OnPartCondition;

    private void OnPartCondition(IBreakable part)
    {
        InfractionKind kind = part.IsBroken ? InfractionKind.ComponentBroken : InfractionKind.ComponentDropped;
        InfractionLog.Report(kind, part.gameObject, part.DisplayName, part.transform.position);
    }

    private void Update()
    {
        // quedas/quebras: janela de percepção encerrada
        IReadOnlyList<Infraction> open = InfractionLog.Open;
        for (int i = open.Count - 1; i >= 0; i--)
        {
            Infraction inf = open[i];
            if (!inf.IsOngoing && Time.time - inf.Time > witnessWindow) InfractionLog.MarkMissed(inf);
        }

        if (Time.time < nextScan) return;
        nextScan = Time.time + scanInterval;
        ScanFloor();
    }

    private void ScanFloor()
    {
        scratch.Clear();
        scratch.AddRange(PickupInteractable.All);
        foreach (PickupInteractable item in scratch)
        {
            if (!Tracked(item)) continue;
            if (!floor.TryGetValue(item, out FloorState st)) floor[item] = st = new FloorState();
            if (OnFloor(item))
            {
                if (st.since < 0f) st.since = Time.time;
                if (st.flagged == null && Time.time - st.since >= abandonDelay)
                {
                    Bounds b = item.WorldBounds;
                    st.flagged = InfractionLog.Report(InfractionKind.ItemLeftOnFloor, item.gameObject, NameOf(item), b.center);
                }
            }
            else
            {
                // saiu do chão: nova chance de abandono no futuro; se ninguém tinha visto, ficou despercebida
                if (st.flagged != null && !st.flagged.Resolved) InfractionLog.MarkMissed(st.flagged);
                st.since = -1f;
                st.flagged = null;
            }
        }
        // itens desativados (instalados, guardados por Stow) ou destruídos
        gone.Clear();
        foreach (var kv in floor) if (kv.Key == null || !kv.Key.isActiveAndEnabled) gone.Add(kv.Key);
        foreach (PickupInteractable p in gone)
        {
            if (floor[p].flagged != null && !floor[p].flagged.Resolved) InfractionLog.MarkMissed(floor[p].flagged);
            floor.Remove(p);
            tracked.Remove(p);
        }
    }

    // Os componentes de um item não mudam: o resultado fica guardado por item.
    private bool Tracked(PickupInteractable item)
    {
        if (tracked.TryGetValue(item, out bool t)) return t;
        GameObject g = item.gameObject;
        t = trackParts && g.TryGetComponent(out ComputerPartItem _) || trackTools && g.TryGetComponent(out ToolItem _) ||
            trackCables && g.TryGetComponent(out CableItem _) || trackPendrives && g.TryGetComponent(out PendriveItem _) ||
            trackMinigameParts && (g.TryGetComponent(out NotebookPartItem _) || g.TryGetComponent(out ImpressoraPartItem _));
        tracked[item] = t;
        return t;
    }

    /// <summary>O item está largado no chão agora (parado, base baixa, sem mesa/bancada embaixo, fora da mão e de gavetas).</summary>
    public bool OnFloor(PickupInteractable item)
    {
        if (item == null || item.IsHeld) return false;
        if (item.Placement != ItemPlacement.Dropped && item.Placement != ItemPlacement.PlacedOnSurface) return false;
        var body = item.GetComponent<Rigidbody>();
        if (body != null && !body.isKinematic && body.linearVelocity.magnitude > restSpeed) return false;
        Bounds b = item.WorldBounds;
        if (b.min.y > floorHeight) return false;
        int n = Physics.RaycastNonAlloc(b.center + Vector3.up * 0.05f, Vector3.down, hits, b.extents.y + 0.3f, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
            if (!hits[i].collider.transform.IsChildOf(item.transform) && hits[i].collider.GetComponentInParent<WorkSurface>() != null) return false;
        return true;
    }

    /// <summary>Nome para mensagens (peça, ferramenta, cabo, pendrive), sem depender do nome do GameObject.</summary>
    public static string NameOf(Component item)
    {
        GameObject g = item.gameObject;
        if (g.TryGetComponent(out ComputerPartItem p)) return p.DisplayName;
        if (g.TryGetComponent(out ToolItem t)) return t.DisplayName;
        if (g.TryGetComponent(out CableItem c)) return c.DisplayName;
        if (g.TryGetComponent(out PendriveItem pen)) return pen.DisplayName;
        if (g.TryGetComponent(out NotebookPartItem n)) return n.DisplayName;
        if (g.TryGetComponent(out ImpressoraPartItem i)) return i.DisplayName;
        return "objeto";
    }
}
