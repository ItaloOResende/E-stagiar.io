using UnityEngine;

/// <summary>
/// Segura um <see cref="PickupInteractable"/> no ponto de mão do Player e o solta de forma controlada.
/// </summary>
public class ItemHolder : MonoBehaviour
{
    [Tooltip("Ponto de mão (filho da câmera). O item carregado fica preso aqui.")]
    [SerializeField] private Transform holdPoint;
    [Tooltip("Visual placeholder do braço; opcional.")]
    [SerializeField] private GameObject armVisual;
    [SerializeField] private bool showArmOnlyWhileHolding = false;

    [Header("Soltar")]
    [SerializeField] private float dropDistance = 1.5f;
    [SerializeField] private float minDropDistance = 0.6f;
    [SerializeField] private float maxGroundSearch = 6f;
    [SerializeField] private LayerMask placementMask = ~0;

    public PickupInteractable Current { get; private set; }
    public bool IsHolding => Current != null;
    public Transform HoldPoint => holdPoint;

    private void Start() => RefreshArm();

    public bool Pick(PickupInteractable item)
    {
        if (IsHolding || item == null) return false;
        Current = item;
        item.OnPickedUp(holdPoint);
        RefreshArm();
        return true;
    }

    public void Drop()
    {
        if (!IsHolding) return;
        PickupInteractable item = Current;
        Current = null;

        Transform cam = holdPoint.parent != null ? holdPoint.parent : holdPoint;
        Vector3 forward = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.01f) forward = Vector3.ProjectOnPlane(cam.up, Vector3.up).normalized;

        // Distância à frente, sem atravessar paredes.
        Vector3 origin = cam.position;
        float radius = item.HorizontalRadius;
        float distance = dropDistance;
        if (Physics.Raycast(origin, forward, out RaycastHit wall, dropDistance + radius, placementMask,
                QueryTriggerInteraction.Ignore))
            distance = Mathf.Clamp(wall.distance - radius, minDropDistance, dropDistance);

        Vector3 spot = origin + forward * distance;

        // Apoia no chão/superfície abaixo.
        float groundY = spot.y - maxGroundSearch;
        if (Physics.Raycast(spot, Vector3.down, out RaycastHit ground, maxGroundSearch, placementMask,
                QueryTriggerInteraction.Ignore))
            groundY = ground.point.y;
        spot.y = groundY;

        item.OnDropped(spot, cam.eulerAngles.y);
        RefreshArm();
    }

    private void RefreshArm()
    {
        if (armVisual != null) armVisual.SetActive(!showArmOnlyWhileHolding || IsHolding);
    }
}
