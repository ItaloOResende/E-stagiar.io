using UnityEngine;

/// <summary>
/// Onde um item pegável está agora (base para o supervisor avaliar organização no futuro; nada é pontuado aqui).
/// </summary>
public enum ItemPlacement
{
    /// <summary>Posição inicial da cena (nunca foi mexido).</summary>
    Initial,
    /// <summary>Na mão do jogador.</summary>
    Held,
    /// <summary>Guardado numa gaveta (<see cref="PickupInteractable.Drawer"/>).</summary>
    StoredInDrawer,
    /// <summary>Colocado de propósito numa superfície mirada (mesa, chão...).</summary>
    PlacedOnSurface,
    /// <summary>Solto sem destino: caiu pela física a partir da mão.</summary>
    Dropped,
    /// <summary>Conectado/instalado num equipamento (guardado, desativado).</summary>
    InEquipment
}

/// <summary>
/// Objeto que o jogador pode pegar, carregar e soltar.
/// Estados: solto (física normal) / seguro (cinemático, colliders desligados, preso ao ponto de mão).
/// Funciona em qualquer GameObject, mesmo filho de hierarquias com escala/rotação herdadas (ex.: nós de FBX):
/// guarda a escala e a rotação MUNDIAIS de repouso e as restaura ao soltar.
/// Posição na mão e altura de soltura são calculadas pelos bounds dos renderers.
/// Preferível ainda: usar num root lógico (escala 1) com um filho "Visual".
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PickupInteractable : Interactable
{
    [Header("Na mão")]
    [Tooltip("Deslocamento do CENTRO do objeto em relação ao ponto de mão (espaço da mão).")]
    [SerializeField] private Vector3 holdPositionOffset = Vector3.zero;
    [Tooltip("Rotação extra do objeto em relação ao ponto de mão (graus).")]
    [SerializeField] private Vector3 holdEulerOffset = Vector3.zero;

    [Header("Física")]
    [Tooltip("Cinemático até ser pego pela primeira vez (evita peças soltas caindo/brigando na cena).")]
    [SerializeField] private bool startsFrozen = true;

    private Rigidbody body;
    private Collider[] colliders;
    private Renderer[] renderers;

    private Vector3 restWorldScale;
    private Quaternion restWorldRotation;
    private Vector3 restCenterOffset; // centro dos renderers - pivô, em mundo, na pose de repouso
    private float horizontalRadius;

    public bool IsHeld { get; private set; }
    /// <summary>Onde o item está (gaveta, superfície, caiu...). Ver <see cref="ItemPlacement"/>.</summary>
    public ItemPlacement Placement { get; private set; } = ItemPlacement.Initial;
    /// <summary>Gaveta onde está guardado (só com <see cref="ItemPlacement.StoredInDrawer"/>).</summary>
    public DrawerContents Drawer { get; private set; }
    /// <summary>Disparado quando o item sai da mão (guardado, colocado, solto ou conectado).</summary>
    public event System.Action<PickupInteractable, ItemPlacement> Released;

    /// <summary>Disparado quando o jogador pega o item (ex.: gaveta atualiza o que ainda contém).</summary>
    public event System.Action<PickupInteractable> PickedUp;
    public float HoldYaw => holdEulerOffset.y;

    /// <summary>Raio horizontal aproximado, usado para não soltar dentro de paredes.</summary>
    public float HorizontalRadius => horizontalRadius;

    private void Awake() => Init();

    private void Init()
    {
        if (body != null) return;
        body = GetComponent<Rigidbody>();
        renderers = GetComponentsInChildren<Renderer>(true);

        restWorldScale = transform.lossyScale;
        restWorldRotation = transform.rotation;

        Bounds b = RenderBounds();
        restCenterOffset = b.center - transform.position;
        horizontalRadius = Mathf.Max(b.extents.x, b.extents.z);

        colliders = GetComponentsInChildren<Collider>(true);
        if (colliders.Length == 0)
        {
            var box = gameObject.AddComponent<BoxCollider>();
            box.center = transform.InverseTransformPoint(b.center);
            box.size = new Vector3(
                b.size.x / Mathf.Abs(restWorldScale.x),
                b.size.y / Mathf.Abs(restWorldScale.y),
                b.size.z / Mathf.Abs(restWorldScale.z));
            colliders = new Collider[] { box };
        }

        if (startsFrozen) body.isKinematic = true;
    }

    private Bounds RenderBounds()
    {
        if (renderers.Length == 0) return new Bounds(transform.position, Vector3.one * 0.1f);
        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
        return b;
    }

    // Mensagem não personalizada no Inspector = "Pegar".
    public override string PromptText =>
        string.IsNullOrEmpty(interactionMessage) || interactionMessage == "Interagir" ? "Pegar" : interactionMessage;

    public override bool CanInteract(InteractionDetector interactor)
        => base.CanInteract(interactor) && !IsHeld && interactor.Holder != null && !interactor.Holder.IsHolding;

    public override void Interact(InteractionDetector interactor) => interactor.Holder.Pick(this);

    public void OnPickedUp(Transform holdPoint)
    {
        Init();
        IsHeld = true;
        if (!body.isKinematic)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
        body.isKinematic = true;
        foreach (Collider c in colliders) c.enabled = false;

        StopIgnoring();
        transform.SetParent(holdPoint, true);
        Placement = ItemPlacement.Held;
        Drawer = null;

        // Escala mundial original preservada, qualquer que seja o pai antigo.
        Vector3 hs = holdPoint.lossyScale;
        transform.localScale = new Vector3(restWorldScale.x / hs.x, restWorldScale.y / hs.y, restWorldScale.z / hs.z);

        Quaternion rot = holdPoint.rotation * Quaternion.Euler(holdEulerOffset) * restWorldRotation;
        transform.rotation = rot;

        // Centro visual (e não o pivô) fica no ponto de mão.
        Vector3 centerOffset = (rot * Quaternion.Inverse(restWorldRotation)) * restCenterOffset;
        transform.position = holdPoint.TransformPoint(holdPositionOffset) - centerOffset;

        PickedUp?.Invoke(this);
    }

    /// <summary>Coloca numa superfície mirada: base centrada em <paramref name="basePosition"/>, sem atravessar paredes.</summary>
    /// <param name="ignore">Colliders do jogador: o item não colide com eles até se afastar (não fica apoiado no jogador).</param>
    public void OnPlaced(Vector3 basePosition, float yaw, Collider[] ignore = null)
    {
        OnDropped(basePosition, yaw);
        IgnoreUntilClear(ignore);
        ResolveOverlaps();
        SetPlacement(ItemPlacement.PlacedOnSurface, null);
    }

    /// <summary>
    /// Solta sem destino: o item sai da mão onde está (sem teletransporte) e cai pela física. Se a mão estiver
    /// dentro/atrás de algo (parede, móvel), recua até a frente do obstáculo, ao longo do olhar do jogador.
    /// </summary>
    /// <param name="releaseCenter">Onde o centro do item começa a cair (ex.: um pouco à frente do jogador). Vazio = onde está na mão.</param>
    public void OnReleased(Vector3 eye, Collider[] ignore = null, Vector3? releaseCenter = null)
    {
        Init();
        Quaternion rot = transform.rotation;
        transform.SetParent(null, true);
        transform.localScale = restWorldScale;
        Vector3 center = RenderBounds().center;
        if (releaseCenter.HasValue)
        {
            transform.position += releaseCenter.Value - center;
            center = releaseCenter.Value;
        }
        Vector3 pos = transform.position;
        Vector3 toItem = center - eye;
        if (Physics.Raycast(eye, toItem.normalized, out RaycastHit block, toItem.magnitude + horizontalRadius,
                ~0, QueryTriggerInteraction.Ignore) && !block.collider.transform.IsChildOf(transform))
            pos -= toItem.normalized * (toItem.magnitude + horizontalRadius - block.distance + 0.02f);
        transform.SetPositionAndRotation(pos, rot);

        foreach (Collider c in colliders) c.enabled = true;
        IgnoreUntilClear(ignore);
        ResolveOverlaps();
        body.isKinematic = false;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative; // não atravessa o chão ao cair
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.WakeUp();
        IsHeld = false;
        SetPlacement(ItemPlacement.Dropped, null);
    }

    /// <summary>Guardado na gaveta: preso à parte móvel, parado (cinemático), ainda pegável.</summary>
    public void OnStored(DrawerContents drawer, Transform parent, Vector3 position, Quaternion rotation)
    {
        Init();
        transform.SetParent(parent, true);
        transform.SetPositionAndRotation(position, rotation);
        Vector3 ps = parent.lossyScale;
        transform.localScale = new Vector3(restWorldScale.x / ps.x, restWorldScale.y / ps.y, restWorldScale.z / ps.z);
        body.isKinematic = true;
        foreach (Collider c in colliders) c.enabled = true;
        IsHeld = false;
        SetPlacement(ItemPlacement.StoredInDrawer, drawer);
    }

    /// <summary>Marca o item como guardado numa gaveta (itens criados pela própria gaveta).</summary>
    public void MarkStored(DrawerContents drawer)
    {
        Placement = ItemPlacement.StoredInDrawer;
        Drawer = drawer;
    }

    /// <summary>Marca o item como conectado/instalado num equipamento (cabo, peça, pendrive).</summary>
    public void MarkInEquipment() => SetPlacement(ItemPlacement.InEquipment, null);

    private void SetPlacement(ItemPlacement placement, DrawerContents drawer)
    {
        Placement = placement;
        Drawer = drawer;
        Released?.Invoke(this, placement);
    }

    /// <summary>Bounds dos renderers (mundo). Usado para acomodar o item (gaveta, superfície).</summary>
    public Bounds WorldBounds
    {
        get { Init(); return RenderBounds(); }
    }

    // Colliders do jogador ignorados por este item até ele se afastar (soltar perto do corpo/cabeça).
    private Collider[] ignoring;
    private Coroutine ignoringRoutine;

    /// <summary>O item está ignorando a colisão com este collider (o jogador, logo depois de soltar).</summary>
    public bool IsIgnoring(Collider other) => ignoring != null && System.Array.IndexOf(ignoring, other) >= 0;

    private void IgnoreUntilClear(Collider[] others)
    {
        StopIgnoring();
        if (others == null || others.Length == 0) return;
        ignoring = others;
        SetIgnore(true);
        if (isActiveAndEnabled) ignoringRoutine = StartCoroutine(RestoreWhenClear());
    }

    private void SetIgnore(bool value)
    {
        foreach (Collider mine in colliders)
            foreach (Collider other in ignoring)
                if (mine != null && other != null) Physics.IgnoreCollision(mine, other, value);
    }

    private void StopIgnoring()
    {
        if (ignoringRoutine != null) StopCoroutine(ignoringRoutine);
        ignoringRoutine = null;
        if (ignoring != null) SetIgnore(false);
        ignoring = null;
    }

    // Volta a colidir com o jogador só quando os volumes não se tocam mais (evita ser empurrado para cima dele).
    private System.Collections.IEnumerator RestoreWhenClear()
    {
        var wait = new WaitForFixedUpdate();
        for (float t = 0f; t < 10f; t += Time.fixedDeltaTime)
        {
            yield return wait;
            if (IsHeld || ignoring == null) yield break;
            bool touching = false;
            Bounds mine = RenderBounds();
            mine.Expand(0.05f);
            foreach (Collider other in ignoring)
                if (other != null && other.enabled && mine.Intersects(other.bounds)) { touching = true; break; }
            if (!touching) break;
        }
        ignoringRoutine = null;
        if (ignoring != null) SetIgnore(false);
        ignoring = null;
    }

    /// <summary>Empurra o item para fora de colliders (não-gatilho) em que tenha ficado enfiado (parede, móvel).</summary>
    private void ResolveOverlaps()
    {
        for (int iteration = 0; iteration < 4; iteration++)
        {
            bool moved = false;
            foreach (Collider mine in colliders)
            {
                if (mine == null || !mine.enabled) continue;
                Bounds b = mine.bounds;
                foreach (Collider other in Physics.OverlapBox(b.center, b.extents, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (other.transform.IsChildOf(transform) || IsIgnoring(other) ||
                        other.attachedRigidbody != null && !other.attachedRigidbody.isKinematic) continue;
                    if (Physics.ComputePenetration(mine, mine.transform.position, mine.transform.rotation,
                            other, other.transform.position, other.transform.rotation, out Vector3 dir, out float dist) && dist > 0.001f)
                    {
                        transform.position += dir * (dist + 0.002f);
                        moved = true;
                    }
                }
            }
            if (!moved) return;
        }
    }

    /// <summary>Solta com a base centrada em <paramref name="basePosition"/>, virado para <paramref name="yaw"/>.</summary>
    public void OnDropped(Vector3 basePosition, float yaw)
    {
        Init();
        transform.SetParent(null, true);
        transform.localScale = restWorldScale;
        // Só a inclinação de repouso é restaurada (não a rotação usada na mão); o yaw vem do jogador.
        Quaternion restTilt = Quaternion.Euler(0f, -restWorldRotation.eulerAngles.y, 0f) * restWorldRotation;
        transform.rotation = Quaternion.Euler(0f, yaw, 0f) * restTilt;
        transform.position = basePosition;

        Bounds b = RenderBounds();
        transform.position += new Vector3(basePosition.x - b.center.x,
                                          basePosition.y + 0.01f - b.min.y,
                                          basePosition.z - b.center.z);

        foreach (Collider c in colliders) c.enabled = true;
        body.isKinematic = false;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        IsHeld = false;
    }
}
