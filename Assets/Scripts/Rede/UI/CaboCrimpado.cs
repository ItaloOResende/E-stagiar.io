using UnityEngine;

/// <summary>
/// Marca um cabo de rede FEITO na crimpagem (<see cref="RedeCrimpScreen"/>), para diferenciá-lo dos cabos prontos das
/// gavetas. O rack registra se o par foi ligado com um cabo assim (<see cref="RedeGame.TotalParesComCaboCrimpado"/>),
/// e o chamado de cabeamento só aceita esse. Os cabos prontos continuam valendo nos outros atendimentos (ex.: PC sem rede).
/// </summary>
public class CaboCrimpado : MonoBehaviour
{
}
