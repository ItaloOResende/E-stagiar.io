using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Ponto elétrico da parede (as duas tomadas e o ponto de rede) de UM PC. Vai no root lógico do <c>PontoEletrico</c>;
/// ao ser ativado, registra-se no <see cref="ComputerPower"/> do gabinete correspondente
/// (<see cref="ComputerPower.AttachWallPoint"/>), como o <see cref="MonitorDisplay"/> faz com o monitor.
/// A distância até o GABINETE (<see cref="caseRange"/>, nunca a do jogador) decide se o painel de conexões aparece na tela
/// de manutenção e se as tomadas e a rede podem ser usadas (<see cref="ComputerPower.CanReachPort"/>). Cada estação aponta para o seu PC:
/// o estado dos cabos nunca se mistura entre computadores. Só lógica: o modelo fica no filho <c>Visual</c>.
/// </summary>
[DisallowMultipleComponent]
public class WallConnectionPoint : MonoBehaviour
{
    [Tooltip("PC (root lógico do gabinete) ligado a estas tomadas e a este ponto de rede.")]
    [SerializeField] private ComputerPower source;
    [Tooltip("Distância HORIZONTAL máxima (m) entre o gabinete e este ponto para as tomadas e a rede ficarem disponíveis. 0 = sem limite.")]
    [FormerlySerializedAs("playerReach")]
    [Min(0f)] [SerializeField] private float caseRange = 2f;

    public ComputerPower Source => source;

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
        if (source != null) source.AttachWallPoint(transform, caseRange);
    }

    private void OnDisable()
    {
        if (source != null) source.DetachWallPoint(transform);
    }
}
