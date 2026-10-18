using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Seta discreta sobre a estação de um chamado EM ATENDIMENTO. Mostrada pelo <see cref="TicketDeviceBridge"/> ao
/// reservar o equipamento e removida ao liberar (chamado concluído, troca de equipamento). Uma seta por equipamento.
/// O modelo vem de <c>Resources/Indicador_Chamado</c> (gerado pelo menu de correções); sem ele, nada aparece.
/// A seta (<see cref="Scale"/> × o modelo) flutua um pouco acima do ponto mais alto da estação e das divisórias em volta
/// (<see cref="Clearance"/>) e balança devagar; não tem colisor (não atrapalha a mira).
/// </summary>
public class StationLocator : MonoBehaviour
{
    private const string PrefabPath = "Indicador_Chamado";
    private static GameObject prefab;
    private static bool loaded;
    private static readonly Dictionary<Object, StationLocator> shown = new Dictionary<Object, StationLocator>();

    /// <summary>Escala da seta sobre o modelo do Resources (2026-10-17: um pouco maior para ser vista de longe).</summary>
    public const float Scale = 1.4f;
    /// <summary>Folga (m) entre o ponto mais alto da estação/divisória e a base da seta.</summary>
    public const float Clearance = 0.45f;

    private Transform target;
    private float baseHeight;
    private float nextRefresh;

    /// <summary>Seta ligada para este dono (equipamento), sobre <paramref name="station"/>.</summary>
    public static void Show(Object owner, Transform station)
    {
        if (owner == null || station == null) return;
        Hide(owner);
        if (!loaded) { prefab = Resources.Load<GameObject>(PrefabPath); loaded = true; }
        if (prefab == null) return;
        GameObject go = Instantiate(prefab);
        go.name = "Indicador_" + station.name;
        foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) Destroy(c);
        var loc = go.AddComponent<StationLocator>();
        loc.target = station;
        go.transform.localScale = prefab.transform.localScale * Scale;
        loc.baseHeight = Mathf.Max(TopOf(station), DividerTop(station)) + Clearance;
        shown[owner] = loc;
    }

    public static void Hide(Object owner)
    {
        if (owner == null || !shown.TryGetValue(owner, out StationLocator loc)) return;
        shown.Remove(owner);
        if (loc != null) Destroy(loc.gameObject);
    }

    public static int Count => shown.Count;

    /// <summary>Seta mostrada para um dono (testes).</summary>
    public static StationLocator Of(Object owner) => owner != null && shown.TryGetValue(owner, out StationLocator l) ? l : null;

    /// <summary>
    /// Topo das divisórias em volta da estação: o mais alto entre os colisores ESTÁTICOS (sem Rigidbody) num raio de 1,6 m
    /// e os renderers estáticos (divisórias sem colisor) cujo volume chega a 1,6 m do ponto da seta, até 2,3 m acima do
    /// chão da estação (ignora paredes, teto, NPCs e objetos soltos). Assim a seta fica ACIMA da divisória e é vista do
    /// corredor.
    /// </summary>
    private static float DividerTop(Transform station)
    {
        float chao = station.position.y, top = chao;
        foreach (Collider c in Physics.OverlapSphere(station.position + Vector3.up, 1.6f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (c.attachedRigidbody != null) continue;
            float y = c.bounds.max.y;
            if (y > chao + 2.3f || c.bounds.size.y > 2.6f) continue;   // parede/teto
            top = Mathf.Max(top, y);
        }
        Vector3 p = station.position;
        foreach (MeshRenderer r in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            Bounds b = r.bounds;
            if (b.max.y > chao + 2.3f || b.size.y > 2.6f || b.max.y <= top || r.GetComponentInParent<Rigidbody>() != null) continue;
            Vector3 q = b.ClosestPoint(new Vector3(p.x, b.center.y, p.z));
            if (new Vector2(q.x - p.x, q.z - p.z).sqrMagnitude <= 1.6f * 1.6f) top = b.max.y;
        }
        return top;
    }

    private static float TopOf(Transform t)
    {
        float top = t.position.y + 1f;
        foreach (Renderer r in t.GetComponentsInChildren<Renderer>())
            if (r.enabled && r.bounds.max.y < t.position.y + 2.2f) top = Mathf.Max(top, r.bounds.max.y);   // ignora NPC alto
        return top;
    }

    private void LateUpdate()
    {
        if (target == null) { Destroy(gameObject); return; }
        float t = Time.time;
        if (t >= nextRefresh)   // o alvo pode ter sido movido: refaz a altura (topo da estação e das divisórias em volta)
        {
            nextRefresh = t + 0.5f;
            baseHeight = Mathf.Max(TopOf(target), DividerTop(target)) + Clearance;
        }
        transform.position = new Vector3(target.position.x, baseHeight + Mathf.Sin(t * 2.2f) * 0.06f, target.position.z);
        transform.rotation = Quaternion.Euler(0f, t * 60f, 0f);
    }
}
