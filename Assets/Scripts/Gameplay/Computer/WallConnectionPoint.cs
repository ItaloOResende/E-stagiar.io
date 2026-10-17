using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Ponto elétrico da parede (as duas tomadas e o ponto de rede). Vai no root lógico do <c>PontoEletrico</c>.
/// Qualquer gabinete serve-se do ponto ativo mais próximo dentro de <see cref="caseRange"/> (distância HORIZONTAL até o
/// GABINETE, nunca a do jogador): <see cref="Nearest"/>, usado pelo <see cref="ComputerPower.CurrentWall"/>. Assim, um
/// gabinete levado para outra estação usa as tomadas e a rede de lá.
/// <see cref="source"/> = PC "da casa": recebe o registro (<see cref="ComputerPower.AttachWallPoint"/>) e empresta as
/// âncoras 3D das portas (<see cref="PcCableView3D"/>) para os cabos de qualquer PC ligados aqui.
/// A ocupação de cada tomada/ponto de rede fica AQUI (<see cref="OccupantOf"/>): dois PCs nunca usam a mesma porta.
/// Só lógica: o modelo fica no filho <c>Visual</c>.
/// </summary>
[DisallowMultipleComponent]
public class WallConnectionPoint : MonoBehaviour
{
    [Tooltip("PC da estação deste ponto (empresta as âncoras 3D das portas). Gabinetes de outras estações também podem usar este ponto.")]
    [SerializeField] private ComputerPower source;
    [Tooltip("Distância HORIZONTAL máxima (m) entre o gabinete e este ponto para as tomadas e a rede ficarem disponíveis. 0 = sem limite.")]
    [FormerlySerializedAs("playerReach")]
    [Min(0f)] [SerializeField] private float caseRange = 2f;

    private static readonly List<WallConnectionPoint> active = new List<WallConnectionPoint>();
    private readonly Dictionary<PcPort, ComputerPower> occupants = new Dictionary<PcPort, ComputerPower>();

    public ComputerPower Source => source;

    /// <summary>Pontos elétricos ativos na cena (para testes).</summary>
    public static IReadOnlyList<WallConnectionPoint> Active => active;

    public float CaseRange
    {
        get => caseRange;
        set
        {
            caseRange = Mathf.Max(0f, value);
            if (isActiveAndEnabled && source != null) source.AttachWallPoint(transform, caseRange);
        }
    }

    private void OnEnable()
    {
        if (!active.Contains(this)) active.Add(this);
        if (source != null) source.AttachWallPoint(transform, caseRange);
    }

    private void OnDisable()
    {
        active.Remove(this);
        if (source != null) source.DetachWallPoint(transform);
    }

    /// <summary>Há algum ponto elétrico ativo (com nenhum, os PCs não têm limite de alcance, como antes).</summary>
    public static bool AnyActive
    {
        get
        {
            active.RemoveAll(p => p == null);
            return active.Count > 0;
        }
    }

    /// <summary>Ponto ativo mais próximo (horizontal) de <paramref name="casePosition"/> dentro do alcance de cada um; null = nenhum.</summary>
    public static WallConnectionPoint Nearest(Vector3 casePosition)
    {
        WallConnectionPoint best = null;
        float bestSq = float.MaxValue;
        foreach (WallConnectionPoint p in active)
        {
            if (p == null || !ComputerPower.WithinReach(p.transform, p.caseRange, casePosition)) continue;
            Vector3 d = p.transform.position - casePosition;
            d.y = 0f;
            if (d.sqrMagnitude < bestSq) { bestSq = d.sqrMagnitude; best = p; }
        }
        return best;
    }

    /// <summary>PC cujo cabo está nesta porta (tomada 1/2 ou rede); null = livre.</summary>
    public ComputerPower OccupantOf(PcPort port)
    {
        if (occupants.TryGetValue(port, out ComputerPower pc) && pc != null) return pc;
        occupants.Remove(port);
        return null;
    }

    /// <summary>Marca a porta como usada por <paramref name="pc"/>. Falso se outro PC já está nela.</summary>
    public bool TryOccupy(PcPort port, ComputerPower pc)
    {
        ComputerPower current = OccupantOf(port);
        if (current != null && current != pc) return false;
        occupants[port] = pc;
        return true;
    }

    /// <summary>Libera a porta (só se ela era de <paramref name="pc"/>).</summary>
    public void Release(PcPort port, ComputerPower pc)
    {
        if (OccupantOf(port) == pc) occupants.Remove(port);
    }

    /// <summary>Âncora 3D da porta deste ponto (emprestada do <see cref="PcCableView3D"/> do PC da estação).</summary>
    public Transform AnchorFor(PcPort port)
    {
        PcCableView3D view = source != null ? source.GetComponent<PcCableView3D>() : null;
        return view != null ? view.LocalAnchor(port) : null;
    }
}
