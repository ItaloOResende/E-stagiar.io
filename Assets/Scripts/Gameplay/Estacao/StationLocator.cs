using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Seta discreta sobre a estação de um chamado EM ATENDIMENTO. Mostrada pelo <see cref="TicketDeviceBridge"/> ao
/// reservar o equipamento e removida ao liberar (chamado concluído, troca de equipamento). Uma seta por equipamento.
/// O modelo vem de <c>Resources/Indicador_Chamado</c> (gerado pelo menu de correções); sem ele, nada aparece.
/// A seta flutua um pouco acima do ponto mais alto da estação e balança devagar; não tem colisor (não atrapalha a mira).
/// </summary>
public class StationLocator : MonoBehaviour
{
    private const string PrefabPath = "Indicador_Chamado";
    private static GameObject prefab;
    private static bool loaded;
    private static readonly Dictionary<Object, StationLocator> shown = new Dictionary<Object, StationLocator>();

    private Transform target;
    private float baseHeight;

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
        loc.baseHeight = TopOf(station) + 0.45f;
        shown[owner] = loc;
    }

    public static void Hide(Object owner)
    {
        if (owner == null || !shown.TryGetValue(owner, out StationLocator loc)) return;
        shown.Remove(owner);
        if (loc != null) Destroy(loc.gameObject);
    }

    public static int Count => shown.Count;

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
        transform.position = new Vector3(target.position.x, baseHeight + Mathf.Sin(t * 2.2f) * 0.06f, target.position.z);
        transform.rotation = Quaternion.Euler(0f, t * 60f, 0f);
    }
}
