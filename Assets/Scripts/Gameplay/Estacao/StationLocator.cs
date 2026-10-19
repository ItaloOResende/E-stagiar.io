using System.Collections.Generic;
using UnityEngine;

/// <summary>Sprite billboard dos chamados ativos. Aparência configurada no TicketManager.</summary>
public class StationLocator : MonoBehaviour
{
    public const float Scale = 1.4f; // Compatibilidade com ferramentas antigas.
    public const float Clearance = 0.3f;
    private static readonly Dictionary<Object, StationLocator> shown = new Dictionary<Object, StationLocator>();
    private Object owner;
    private Transform target;
    private TicketManager settings;
    private SpriteRenderer visual;
    private float heightOffset;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry() => shown.Clear();

    public static void Show(Object owner, Transform station, TicketManager settings = null)
    {
        if (owner == null || station == null) return;
        Hide(owner);
        if (settings == null) settings = Object.FindFirstObjectByType<TicketManager>();
        var go = new GameObject("Indicador_" + station.name);
        var loc = go.AddComponent<StationLocator>();
        loc.owner = owner;
        loc.target = station;
        loc.settings = settings;
        loc.visual = go.AddComponent<SpriteRenderer>();
        loc.heightOffset = Mathf.Max(TopOf(station), DividerTop(station)) - station.position.y;
        shown[owner] = loc;
        loc.LateUpdate();
    }

    public static void Hide(Object owner)
    {
        if (ReferenceEquals(owner, null) || !shown.TryGetValue(owner, out var loc)) return;
        shown.Remove(owner);
        if (loc != null) { loc.gameObject.SetActive(false); Destroy(loc.gameObject); }
    }
    public static int Count => shown.Count;
    public static StationLocator Of(Object owner) => owner != null && shown.TryGetValue(owner, out var loc) ? loc : null;

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
        if (target == null || owner == null) { Destroy(gameObject); return; }
        var camera = settings != null && settings.chamadoCamera != null ? settings.chamadoCamera : Camera.main;
        Sprite sprite = settings != null ? settings.chamadoSprite : null;
        visual.enabled = sprite != null && camera != null;
        if (!visual.enabled) return;
        visual.sprite = sprite;
        visual.color = settings.chamadoCor;
        float size = Mathf.Max(0.05f, settings.chamadoTamanho);
        float spriteHeight = Mathf.Max(0.001f, sprite.bounds.size.y);
        float scale = size / spriteHeight;
        transform.localScale = Vector3.one * scale;
        transform.rotation = camera.transform.rotation;
        Vector3 center = target.position + Vector3.up * (heightOffset + size * 0.5f) + settings.chamadoOffset;
        center.y += Mathf.Sin(Time.time * settings.chamadoVelocidade) * settings.chamadoFlutuacao;
        transform.position = center - transform.rotation * (sprite.bounds.center * scale);
    }

    private void OnDestroy()
    {
        if (!ReferenceEquals(owner, null) && shown.TryGetValue(owner, out var loc) && loc == this)
            shown.Remove(owner);
    }
}
