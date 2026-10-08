using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Raycast a partir do centro da câmera, mostra a mãozinha quando há um <see cref="Interactable"/>
/// válido ao alcance e despacha o clique esquerdo (Input System).
/// Com um item na mão, qualquer clique solta o item.
/// </summary>
public class InteractionDetector : MonoBehaviour
{
    [SerializeField] private float interactionDistance = 3f;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private LayerMask interactionMask = ~0;
    [Tooltip("Mãozinha 2D mostrada quando há algo interagível sob a mira.")]
    [SerializeField] private GameObject handCursor;
    [SerializeField] private ItemHolder holder;

    public ItemHolder Holder => holder;
    public Camera PlayerCamera => playerCamera;
    public Interactable CurrentTarget { get; private set; }

    private void Awake()
    {
        if (holder == null) holder = GetComponent<ItemHolder>();
        if (playerCamera == null) playerCamera = Camera.main;
    }

    private void Update()
    {
        bool holding = holder != null && holder.IsHolding;
        CurrentTarget = holding ? null : FindTarget();

        if (handCursor != null && handCursor.activeSelf != (CurrentTarget != null))
            handCursor.SetActive(CurrentTarget != null);

        var mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;

        if (holding) holder.Drop();
        else if (CurrentTarget != null) CurrentTarget.Interact(this);
    }

    private Interactable FindTarget()
    {
        Transform cam = playerCamera.transform;
        if (!Physics.Raycast(cam.position, cam.forward, out RaycastHit hit, interactionDistance,
                interactionMask, QueryTriggerInteraction.Ignore))
            return null;

        Interactable target = hit.collider.GetComponentInParent<Interactable>();
        return target != null && target.CanInteract(this) ? target : null;
    }
}
