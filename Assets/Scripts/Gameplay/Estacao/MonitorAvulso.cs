using UnityEngine;

/// <summary>
/// Monitor avulso (ex.: estoque da sala de TI): vai no ROOT pegável do monitor, ao lado do <see cref="PickupInteractable"/>.
/// Quando o jogador coloca o monitor (fora de gaveta), pede ao <see cref="MonitorDisplay"/> da tela que se ligue ao
/// gabinete mais próximo que esteja sem monitor utilizável (<see cref="MonitorDisplay.TryConnectToNearby"/>; a regra
/// fica no MonitorDisplay, que é do assembly do PC e não enxerga a interação). O MonitorDisplay precisa estar com
/// "Connect When Placed" ligado.
/// </summary>
[RequireComponent(typeof(PickupInteractable))]
public class MonitorAvulso : MonoBehaviour
{
    private PickupInteractable item;
    private MonitorDisplay display;

    private void OnEnable()
    {
        item = GetComponent<PickupInteractable>();
        display = GetComponentInChildren<MonitorDisplay>(true);
        item.Released += OnReleased;
    }

    private void OnDisable() => item.Released -= OnReleased;

    private void OnReleased(PickupInteractable _, ItemPlacement placement)
    {
        if (display != null && placement != ItemPlacement.StoredInDrawer) display.TryConnectToNearby();
    }
}
