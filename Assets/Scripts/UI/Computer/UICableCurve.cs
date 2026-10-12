using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Faixa curva de UI (cabo desenhado na tela 2D): uma tira de espessura constante ao longo de pontos dados no
/// espaço local deste RectTransform, com uma borda escura (contorno) e um brilho no meio para dar volume.
/// Só desenho; quem decide os pontos é o <see cref="CableLineView"/>.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class UICableCurve : MaskableGraphic
{
    [SerializeField] private float thickness = 10f;
    [Tooltip("Cor da borda (contorno) do cabo.")]
    [SerializeField] private Color outline = new Color(0f, 0f, 0f, 0.85f);
    [Range(0f, 1f)] [SerializeField] private float highlight = 0.25f;

    private readonly List<Vector2> points = new List<Vector2>();

    public float Thickness { get => thickness; set { thickness = value; SetVerticesDirty(); } }

    public void SetPoints(List<Vector2> pts)
    {
        points.Clear();
        points.AddRange(pts);
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (points.Count < 2) return;
        Strip(vh, thickness + 3f, outline);                                     // contorno
        Strip(vh, thickness, color);                                            // corpo
        Strip(vh, thickness * 0.3f, Color.Lerp(color, Color.white, highlight), 0.18f * thickness); // brilho
    }

    private void Strip(VertexHelper vh, float width, Color c, float offset = 0f)
    {
        int start = vh.currentVertCount;
        for (int i = 0; i < points.Count; i++)
        {
            Vector2 a = points[Mathf.Max(i - 1, 0)], b = points[Mathf.Min(i + 1, points.Count - 1)];
            Vector2 dir = (b - a).sqrMagnitude > 1e-6f ? (b - a).normalized : Vector2.right;
            Vector2 n = new Vector2(-dir.y, dir.x);
            Vector2 p = points[i] + n * offset;
            vh.AddVert(p + n * width * 0.5f, c, Vector2.zero);
            vh.AddVert(p - n * width * 0.5f, c, Vector2.zero);
        }
        for (int i = 0; i < points.Count - 1; i++)
        {
            int k = start + i * 2;
            vh.AddTriangle(k, k + 1, k + 2);
            vh.AddTriangle(k + 1, k + 3, k + 2);
        }
    }
}
