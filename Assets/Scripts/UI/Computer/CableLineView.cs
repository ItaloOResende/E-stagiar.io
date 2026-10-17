using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Desenha UM cabo na tela 2D (Traseira) com um <see cref="UICableCurve"/> no mesmo objeto, que cobre o
/// contêiner dos cabos inteiro. Só apresentação:
///  - as duas pontas conectadas: curva com "barriga" entre as portas (<see cref="from"/> = ponta A, <see cref="to"/> = B);
///  - só uma ponta: o cabo sai da porta e fica pendente, com o plugue solto (<see cref="looseEnd"/>) na ponta livre;
///    se <c>followPointer</c>, a ponta livre acompanha o mouse suavemente (o jogador está levando o cabo até a outra porta);
///  - nenhuma: escondido.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class CableLineView : MonoBehaviour
{
    [Tooltip("Cabo que esta faixa representa.")]
    [SerializeField] private PcLink link;
    [Tooltip("Porta da ponta A (tomada, saída do gabinete, ponto de rede).")]
    [SerializeField] private RectTransform from;
    [Tooltip("Porta da ponta B (gabinete/monitor).")]
    [SerializeField] private RectTransform to;
    [SerializeField] private float thickness = 10f;
    [Tooltip("Barriga do cabo completo (px), somada a 12% da distância entre as portas.")]
    [SerializeField] private float sag = 40f;
    [Tooltip("Quanto o cabo com uma ponta só desce abaixo da porta (px).")]
    [SerializeField] private float dangle = 150f;
    [Tooltip("Plugue desenhado na ponta solta (filho). Sprites da ponta A e da ponta B abaixo.")]
    [SerializeField] private Image looseEnd;
    [SerializeField] private Sprite looseSpriteA;
    [SerializeField] private Sprite looseSpriteB;
    [SerializeField] private float followSpeed = 18f;

    private UICableCurve curve;
    private bool plugA, plugB, follow;
    private Vector2 freeEnd;
    private bool freeEndValid;
    private readonly List<Vector2> pts = new List<Vector2>();

    public PcLink Link => link;
    /// <summary>Porta da ponta A configurada no Inspector (a padrão do cabo).</summary>
    public RectTransform DefaultFrom { get; private set; }

    /// <summary>
    /// Troca a porta da ponta A (ex.: cabo de energia ligado na outra tomada da parede). Null = volta à configurada.
    /// </summary>
    public void SetFrom(RectTransform port)
    {
        if (DefaultFrom == null) DefaultFrom = from;
        RectTransform target = port != null ? port : DefaultFrom;
        if (from == target) return;
        from = target;
        freeEndValid = false;
    }

    private void Awake()
    {
        curve = GetComponent<UICableCurve>();
        if (curve != null) curve.Thickness = thickness;
    }

    /// <summary>Compatível com a versão anterior: visível = as duas pontas conectadas.</summary>
    public void Show(bool visible) => SetState(visible, visible, visible, false);

    /// <summary>Atualiza o cabo: <paramref name="visible"/> (vista certa) e quais pontas estão conectadas.</summary>
    public void SetState(bool visible, bool aPlugged, bool bPlugged, bool followPointer)
    {
        bool show = visible && (aPlugged || bPlugged) && from != null && to != null;
        if (plugA != aPlugged || plugB != bPlugged) freeEndValid = false;
        plugA = aPlugged; plugB = bPlugged; follow = followPointer;
        if (gameObject.activeSelf != show) gameObject.SetActive(show);
        if (show) Layout(true);
    }

    private void LateUpdate() => Layout(false);

    private Vector2 Local(RectTransform t)
    {
        var parent = (RectTransform)transform;
        return parent.InverseTransformPoint(t.TransformPoint(t.rect.center));
    }

    private void Layout(bool snap)
    {
        if (curve == null) curve = GetComponent<UICableCurve>();
        if (curve == null || from == null || to == null) return;
        var rt = (RectTransform)transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        rt.localRotation = Quaternion.identity;

        pts.Clear();
        Vector2 a = Local(from), b = Local(to);
        if (plugA && plugB)
        {
            float s = sag + Vector2.Distance(a, b) * 0.12f;
            Vector2 c1 = a + Vector2.down * s, c2 = b + Vector2.down * s;
            Bezier(a, c1, c2, b);
            if (looseEnd != null) looseEnd.gameObject.SetActive(false);
        }
        else
        {
            Vector2 start = plugA ? a : b;
            Vector2 rest = start + new Vector2(plugA ? 30f : -30f, -dangle);
            Vector2 target = rest;
            if (follow && TryPointer(out Vector2 mouse)) target = mouse;
            if (!freeEndValid || snap && !follow) { freeEnd = target; freeEndValid = true; }
            else freeEnd = Vector2.Lerp(freeEnd, target, 1f - Mathf.Exp(-followSpeed * Time.unscaledDeltaTime));
            float drop = Mathf.Max(40f, Vector2.Distance(start, freeEnd) * 0.35f);
            Bezier(start, start + Vector2.down * drop, freeEnd + Vector2.down * drop * 0.6f, freeEnd);
            if (looseEnd != null)
            {
                looseEnd.gameObject.SetActive(true);
                Sprite sp = plugA ? looseSpriteB : looseSpriteA;
                if (sp != null) looseEnd.sprite = sp;
                looseEnd.rectTransform.anchorMin = looseEnd.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                looseEnd.rectTransform.anchoredPosition = freeEnd - rt.rect.center;
            }
        }
        curve.SetPoints(pts);
    }

    /// <summary>O ponto de tela está sobre o plugue solto deste cabo (visível)?</summary>
    public bool LooseEndContains(Vector2 screenPosition)
    {
        if (looseEnd == null || !looseEnd.gameObject.activeInHierarchy) return false;
        Canvas canvas = GetComponentInParent<Canvas>();
        Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        return RectTransformUtility.RectangleContainsScreenPoint(looseEnd.rectTransform, screenPosition, cam);
    }

    private bool TryPointer(out Vector2 local)
    {
        local = default;
        var mouse = Mouse.current;
        if (mouse == null) return false;
        Canvas canvas = GetComponentInParent<Canvas>();
        Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        return RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, mouse.position.ReadValue(), cam, out local);
    }

    private void Bezier(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
    {
        const int n = 24;
        for (int i = 0; i <= n; i++)
        {
            float t = i / (float)n, u = 1f - t;
            pts.Add(u * u * u * p0 + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * p3);
        }
    }
}
