using UnityEngine;

/// <summary>
/// Desenha um cabo na tela 2D como uma faixa reta entre duas âncoras de UI (ex.: as duas portas do cabo).
/// Vai num Image filho de um contêiner que cobre a tela inteira. Só apresentação.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class CableLineView : MonoBehaviour
{
    [SerializeField] private RectTransform from;
    [SerializeField] private RectTransform to;
    [SerializeField] private float thickness = 10f;

    /// <summary>Mostra a faixa entre as âncoras (se as duas estiverem ativas) ou a esconde.</summary>
    public void Show(bool visible)
    {
        bool ok = visible && from != null && to != null && from.gameObject.activeInHierarchy && to.gameObject.activeInHierarchy;
        gameObject.SetActive(ok);
        if (ok) Layout();
    }

    private void LateUpdate() => Layout(); // acompanha mudanças de resolução

    private void Layout()
    {
        var rt = (RectTransform)transform;
        var parent = (RectTransform)rt.parent;
        Vector2 a = parent.InverseTransformPoint(from.TransformPoint(from.rect.center));
        Vector2 b = parent.InverseTransformPoint(to.TransformPoint(to.rect.center));
        Vector2 d = b - a;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = (a + b) * 0.5f - parent.rect.center;
        rt.sizeDelta = new Vector2(d.magnitude, thickness);
        rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
    }
}
