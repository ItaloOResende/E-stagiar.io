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
///
/// Mira estável: se o raio exato não acerta nada interagível, uma esfera fina (<see cref="aimAssistRadius"/>)
/// ao longo do mesmo raio procura um alvo próximo (sem passar do primeiro obstáculo). E um alvo perdido por um
/// instante (<see cref="targetGraceTime"/>) continua valendo enquanto a mira ainda passa rente a ele — itens finos
/// e bordas de gavetas não fazem o cursor piscar. Roda depois do PlayerLook (a mira já está atualizada no frame).
/// </summary>
[DefaultExecutionOrder(50)]
public class InteractionDetector : MonoBehaviour
{
    [SerializeField] private float interactionDistance = 3f;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private LayerMask interactionMask = ~0;
    [Tooltip("Opcional (legado): mãozinha 2D mostrada quando há algo interagível sob a mira. " +
             "Com um InteractionPrompt no HUD, deixe vazio: o prompt mostra as duas interações.")]
    [SerializeField] private GameObject handCursor;
    [SerializeField] private ItemHolder holder;
    [Tooltip("Opcional: se houver tela de UI aberta, o detector não age. Padrão: busca no mesmo objeto.")]
    [SerializeField] private UIScreenManager screens;

    [Header("Estabilidade da mira")]
    [Tooltip("Raio (m) da ajuda de mira quando o raio exato não acerta nada interagível. 0 = desligada.")]
    [SerializeField] private float aimAssistRadius = 0.035f;
    [Tooltip("Tempo (s) em que um alvo perdido continua valendo se a mira ainda passa rente a ele.")]
    [SerializeField] private float targetGraceTime = 0.15f;
    [Tooltip("Folga (m) em volta do alvo anterior para a tolerância acima.")]
    [SerializeField] private float graceMargin = 0.05f;

    private Interactable primary;
    private Interactable secondary;
    private Interactable lastPrimary, lastSecondary;
    private float lastSeenTime = float.NegativeInfinity;
    private readonly RaycastHit[] sphereHits = new RaycastHit[16];

    public ItemHolder Holder => holder;
    public Camera PlayerCamera => playerCamera;
    public UIScreenManager Screens => screens;
    /// <summary>Alvo do clique esquerdo sob a mira (null se não houver).</summary>
    public Interactable CurrentTarget => primary;
    /// <summary>Alvo do clique direito sob a mira (null se não houver).</summary>
    public Interactable CurrentSecondaryTarget => secondary;
    /// <summary>Com item na mão, o clique esquerdo solta o item (mesmo sem alvo).</summary>
    public bool LeftClickDrops => holder != null && holder.IsHolding;

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
        var ray = new Ray(cam.position, cam.forward);
        bool hitSomething = RaycastIgnoringSelf(ray, out RaycastHit hit);
        if (hitSomething) Collect(hit.collider, holding);

        // Ajuda de mira: só se o raio exato não achou nada, e sem ir além do que ele atingiu (paredes bloqueiam).
        if (primary == null && secondary == null && aimAssistRadius > 0f)
        {
            float maxDist = hitSomething ? Mathf.Min(interactionDistance, hit.distance + aimAssistRadius * 2f) : interactionDistance;
            int n = Physics.SphereCastNonAlloc(ray, aimAssistRadius, sphereHits, maxDist, interactionMask, QueryTriggerInteraction.Ignore);
            float best = float.PositiveInfinity;
            for (int i = 0; i < n; i++)
            {
                RaycastHit h = sphereHits[i];
                if (h.distance <= 0f || h.distance >= best) continue; // distância 0 = começou dentro do collider
                if (IsSelf(h.collider)) continue;
                if (h.collider.GetComponentInParent<Interactable>() == null) continue;
                best = h.distance;
                primary = secondary = null;
                Collect(h.collider, holding);
            }
        }

        if (primary != null || secondary != null)
        {
            lastPrimary = primary;
            lastSecondary = secondary;
            lastSeenTime = Time.time;
            return;
        }

        // Tolerância: o alvo anterior continua por um instante se a mira ainda passa rente a ele.
        if (Time.time - lastSeenTime <= targetGraceTime && (StillAimed(lastPrimary, ray) || StillAimed(lastSecondary, ray)))
        {
            if (!holding && lastPrimary != null && lastPrimary.CanInteract(this)) primary = lastPrimary;
            if (lastSecondary != null && lastSecondary.CanInteractSecondary(this)) secondary = lastSecondary;
        }
    }

    private readonly RaycastHit[] rayHits = new RaycastHit[16];

    // O próprio jogador (CharacterController, cápsula) não bloqueia a mira: a câmera fica na borda do topo da cápsula
    // e, olhando para baixo, o raio saía por ela e parava a poucos centímetros do olho.
    private bool IsSelf(Collider c) => c.transform.IsChildOf(transform);

    private bool RaycastIgnoringSelf(Ray ray, out RaycastHit hit)
    {
        hit = default;
        int n = Physics.RaycastNonAlloc(ray, rayHits, interactionDistance, interactionMask, QueryTriggerInteraction.Ignore);
        float best = float.PositiveInfinity;
        for (int i = 0; i < n; i++)
        {
            if (rayHits[i].distance >= best || IsSelf(rayHits[i].collider)) continue;
            best = rayHits[i].distance;
            hit = rayHits[i];
        }
        return best < float.PositiveInfinity;
    }

    private void Collect(Collider collider, bool holding)
    {
        foreach (Interactable candidate in collider.GetComponentsInParent<Interactable>())
        {
            // Com item na mão o clique esquerdo só solta o item, mas o direito (telas) continua valendo:
            // é assim que a ferramenta na mão pode ser usada dentro de uma tela 2D.
            if (!holding && primary == null && candidate.CanInteract(this)) primary = candidate;
            if (secondary == null && candidate.CanInteractSecondary(this)) secondary = candidate;
        }
    }

    private bool StillAimed(Interactable target, Ray ray)
    {
        if (target == null || !target.isActiveAndEnabled) return false;
        foreach (Collider c in target.GetComponentsInChildren<Collider>())
        {
            if (!c.enabled || c.isTrigger) continue;
            Bounds b = c.bounds;
            b.Expand(graceMargin * 2f);
            if (b.IntersectRay(ray, out float d) && d <= interactionDistance) return true;
        }
        return false;
    }
}
