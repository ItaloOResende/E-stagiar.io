using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ponto de retirada dos equipamentos de uma estação NOVA (chamado <c>pc-montagem-completa</c>): fica sobre a bancada da
/// sala de TI. Ao iniciar o chamado, o <see cref="PcTicketBridge"/> tira da mesa da estação o gabinete (vazio), o monitor,
/// o teclado, o mouse e os cabos e os deixa AQUI, lado a lado ao longo do eixo X (vermelho) deste objeto; o jogador
/// leva cada um até a mesa. O objeto deve ficar na altura do tampo (os itens são apoiados pela base).
/// Só marca o lugar: sem modelo, sem colisor. Com vários pontos na cena, vale o mais próximo do equipamento.
/// </summary>
public class AssemblyPickupPoint : MonoBehaviour
{
    private static readonly List<AssemblyPickupPoint> all = new List<AssemblyPickupPoint>();

    [Tooltip("Largura útil (m) ao longo do eixo X. Itens que não cabem começam uma segunda fileira (eixo Z).")]
    [Min(0.3f)] [SerializeField] private float usableWidth = 1.4f;
    [Tooltip("Distância (m) entre fileiras quando os itens não cabem numa só.")]
    [Min(0.1f)] [SerializeField] private float rowSpacing = 0.35f;
    [Tooltip("Folga (m) entre um item e o seguinte.")]
    [Min(0f)] [SerializeField] private float gap = 0.06f;

    private readonly List<PickupInteractable> placed = new List<PickupInteractable>();

    private void OnEnable() { if (!all.Contains(this)) all.Add(this); }
    private void OnDisable() => all.Remove(this);

    /// <summary>O ponto mais próximo de <paramref name="from"/> (null se a cena não tem nenhum).</summary>
    public static AssemblyPickupPoint Nearest(Vector3 from)
    {
        AssemblyPickupPoint best = null;
        float d = float.MaxValue;
        foreach (AssemblyPickupPoint p in all)
        {
            if (p == null || !p.isActiveAndEnabled) continue;
            float dd = (p.transform.position - from).sqrMagnitude;
            if (dd < d) { d = dd; best = p; }
        }
        return best;
    }

    /// <summary>Apoia os itens lado a lado sobre o ponto (nunca destrói nem duplica nada).</summary>
    public void Place(IList<PickupInteractable> items)
    {
        // itens que já saíram daqui (o jogador levou) liberam o espaço
        placed.RemoveAll(p => p == null || p.IsHeld || (p.transform.position - transform.position).sqrMagnitude > 4f);
        float x = -usableWidth * 0.5f;
        int row = 0;
        foreach (PickupInteractable p in placed) x += Width(p) + gap;
        foreach (PickupInteractable item in items)
        {
            if (item == null) continue;
            item.gameObject.SetActive(true);
            float w = Width(item);
            if (x + w > usableWidth * 0.5f && x > -usableWidth * 0.5f) { row++; x = -usableWidth * 0.5f; }
            Vector3 pos = transform.TransformPoint(new Vector3(x + w * 0.5f, 0f, -row * rowSpacing));
            item.OnDropped(pos, transform.eulerAngles.y);
            placed.Add(item);
            x += w + gap;
        }
    }

    // largura do item ao longo do eixo X do ponto (bounds dos renderers)
    private float Width(PickupInteractable p)
    {
        Bounds b = p.WorldBounds;
        Vector3 r = transform.right;
        return Mathf.Abs(r.x) * b.size.x + Mathf.Abs(r.z) * b.size.z;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.8f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(new Vector3(0f, 0.02f, -rowSpacing * 0.5f), new Vector3(usableWidth, 0.04f, rowSpacing * 2f));
    }
}
