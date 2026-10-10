using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Raycast a partir do centro da câmera, mostra a mãozinha quando há um <see cref="Interactable"/>
/// válido ao alcance e despacha os cliques (Input System): esquerdo = <see cref="Interactable.Interact"/>,
/// direito = <see cref="Interactable.InteractSecondary"/>.
/// Com um item na mão, o clique esquerdo solta o item; o direito segue abrindo telas (o item continua na mão).
/// Enquanto uma tela de UI está aberta (<see cref="UIScreenManager"/>), nada aqui processa cliques.
/// Um mesmo objeto pode ter vários Interactable (ex.: Pickup + UIScreenInteractable): o primeiro que aceitar
/// cada tipo de clique é usado.
/// </summary>
public class InteractionDetector : MonoBehaviour
{
    [SerializeField] private float interactionDistance = 3f;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private LayerMask interactionMask = ~0;
    [Tooltip("Mãozinha 2D mostrada quando há algo interagível sob a mira.")]
    [SerializeField] private GameObject handCursor;
    [SerializeField] private ItemHolder holder;
    [Tooltip("Opcional: se houver tela de UI aberta, o detector não age. Padrão: busca no mesmo objeto.")]
    [SerializeField] private UIScreenManager screens;

    private Interactable primary;
    private Interactable secondary;

    public ItemHolder Holder => holder;
    public Camera PlayerCamera => playerCamera;
    public UIScreenManager Screens => screens;
    /// <summary>Alvo do clique esquerdo sob a mira (null se não houver).</summary>
    public Interactable CurrentTarget => primary;

    private void Awake()
    {
        if (holder == null) holder = GetComponent<ItemHolder>();
        if (screens == null) screens = GetComponent<UIScreenManager>();
        if (playerCamera == null) playerCamera = Camera.main;
    }

    private void Update()
    {
        if (screens != null && screens.BlocksGameplayInput)
        {
            primary = secondary = null;
            SetHand(false);
            return;
        }

        bool holding = holder != null && holder.IsHolding;
        FindTargets(holding);
        SetHand(primary != null || secondary != null);

        var mouse = Mouse.current;
        if (mouse == null) return;

        if (mouse.leftButton.wasPressedThisFrame)
        {
            if (holding) holder.Drop();
            else if (primary != null) primary.Interact(this);
        }
        else if (mouse.rightButton.wasPressedThisFrame && secondary != null)
        {
            secondary.InteractSecondary(this);
        }
    }

    private void SetHand(bool visible)
    {
        if (handCursor != null && handCursor.activeSelf != visible) handCursor.SetActive(visible);
    }

    private void FindTargets(bool holding)
    {
        primary = secondary = null;

        Transform cam = playerCamera.transform;
        if (!Physics.Raycast(cam.position, cam.forward, out RaycastHit hit, interactionDistance,
                interactionMask, QueryTriggerInteraction.Ignore))
            return;

        foreach (Interactable candidate in hit.collider.GetComponentsInParent<Interactable>())
        {
            // Com item na mão o clique esquerdo só solta o item, mas o direito (telas) continua valendo:
            // é assim que a ferramenta na mão pode ser usada dentro de uma tela 2D.
            if (!holding && primary == null && candidate.CanInteract(this)) primary = candidate;
            if (secondary == null && candidate.CanInteractSecondary(this)) secondary = candidate;
        }
    }
}
