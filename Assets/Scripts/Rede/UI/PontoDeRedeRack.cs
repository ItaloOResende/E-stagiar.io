using UnityEngine;

/// <summary>
/// Liga o ponto de rede da parede de uma estação (o objeto com <see cref="WallConnectionPoint"/>) à porta do rack:
/// patch panel N ↔ switch N (<see cref="RedeGame"/>, mesma regra da tela do rack). A rede do PC ligado neste ponto
/// só funciona com o par N ligado no rack com um cabo bom (<see cref="RedeConectividade"/>).
/// Ao começar, o par N já vem ligado (a estação começa com rede). Sem este componente, o ponto não depende do rack.
/// </summary>
[RequireComponent(typeof(WallConnectionPoint))]
public class PontoDeRedeRack : MonoBehaviour
{
    [Tooltip("Porta do patch panel/switch que atende este ponto (1..portas do rack). 0 = não depende do rack.")]
    [Min(0)] [SerializeField] private int portaRack;

    public int PortaRack => portaRack;

    private void Start()
    {
        RedeConectividade.Garantir();
        if (portaRack > 0) RedeGame.Garantir().PreLigar(portaRack);
    }

    /// <summary>Porta do rack do ponto (0 = ponto sem vínculo ou nenhum ponto).</summary>
    public static int PortaDe(WallConnectionPoint ponto)
    {
        PontoDeRedeRack p = ponto != null ? ponto.GetComponent<PontoDeRedeRack>() : null;
        return p != null ? p.portaRack : 0;
    }
}
