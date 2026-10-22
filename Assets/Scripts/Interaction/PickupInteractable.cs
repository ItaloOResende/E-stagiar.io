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
    [Tooltip("Escala do objeto SÓ enquanto está na mão (1 = tamanho real). Para objetos grandes (monitor, gabinete) não " +
             "cobrirem a visão. Ao soltar, volta ao tamanho real.")]
    [Range(0.2f, 1.5f)] [SerializeField] private float holdScale = 1f;
    [Tooltip("Ligado: quem fica no ponto de mão é a EMPUNHADURA (Grip Local Point), não o centro do objeto, e a " +
             "rotação Hold Euler Offset passa a ser em relação aos eixos locais do item (não depende do giro em que ele " +
             "estava). Para ferramentas: segurar pelo cabo, nunca pela ponta.")]
    [SerializeField] private bool useGripPoint;
    [Tooltip("Ponto de pega no espaço LOCAL deste objeto (root lógico), ex.: o meio do cabo da chave de fenda. " +
             "Hold Position Offset continua somado a ele.")]
    [SerializeField] private Vector3 gripLocalPoint = Vector3.zero;

    [Header("Ao colocar numa superfície")]
    [Tooltip("Giro (graus) somado à direção do jogador ao colocar o item numa superfície mirada. 0 = frente do item para " +
             "onde o jogador olha; 180 = virado para o jogador (monitor, teclado, mouse).")]
    [SerializeField] private float placedYawOffset = 0f;

    [Header("Física")]
    [Tooltip("Cinemático até ser pego pela primeira vez (evita peças soltas caindo/brigando na cena).")]
    [SerializeField] private bool startsFrozen = true;

    private Rigidbody body;
    private Collider[] colliders;
    private Renderer[] renderers;
    private IPickupBlocker[] blockers;

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
    public Vector3 HoldPositionOffset => holdPositionOffset;
    public Vector3 HoldEulerOffset => holdEulerOffset;
    public float HoldScale => holdScale;
    /// <summary>Giro somado à direção do jogador ao colocar numa superfície (ver Inspector).</summary>
    public float PlacedYawOffset
    {
        get => placedYawOffset;
        set => placedYawOffset = value;
    }

    /// <summary>Ajusta como o objeto fica na mão (usado por scripts de configuração; vale na próxima vez que for pego).</summary>
    public bool UsesGripPoint => useGripPoint;
    public Vector3 GripLocalPoint => gripLocalPoint;

    /// <summary>Configura a empunhadura (ver <see cref="useGripPoint"/>). Usado pelos scripts de configuração.</summary>
    public void SetGrip(bool use, Vector3 localPoint)
    {
        useGripPoint = use;
        gripLocalPoint = localPoint;
    }

    public void SetHoldPose(Vector3 positionOffset, Vector3 eulerOffset, float scale)
    {
        holdPositionOffset = positionOffset;
        holdEulerOffset = eulerOffset;
        holdScale = Mathf.Clamp(scale, 0.2f, 1.5f);
    }

    /// <summary>Raio horizontal aproximado, usado para não soltar dentro de paredes.</summary>
    public float HorizontalRadius => horizontalRadius;

    private static readonly System.Collections.Generic.HashSet<PickupInteractable> all = new System.Collections.Generic.HashSet<PickupInteractable>();
    /// <summary>Itens pegáveis ativos na cena (registro; evita procurar a cena inteira toda hora).</summary>
    public static System.Collections.Generic.IReadOnlyCollection<PickupInteractable> All => all;

    private void Awake() => Init();
    private void OnEnable() => all.Add(this);
    private void OnDisable() => all.Remove(this);

    private void Init()
    {
        if (body != null) return;
        body = GetComponent<Rigidbody>();
        renderers = GetComponentsInChildren<Renderer>(true);
        blockers = GetComponentsInChildren<IPickupBlocker>(true);   // ex.: MonitorDisplay fica no filho da tela

        restWorldScale = transform.lossyScale;
        restWorldRotation = transform.rotation;

        Bounds b = RenderBounds();
        restCenterOffset = b.center - transform.position;
        horizontalRadius = Mathf.Max(b.extents.x, b.extents.z);

        // Só os colliders LIGADOS em repouso: os desligados de propósito (ex.: visual antigo trocado por outro modelo)
        // não podem voltar a ligar quando o item é solto.
        colliders = System.Array.FindAll(GetComponentsInChildren<Collider>(true), c => c.enabled);
        AjustarCaixaAoVisual();
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

    // Peças pequenas (do PC, notebook, impressora, pendrive, teclado, mouse): a caixa de colisão do prefab não
    // acompanhava o modelo, que descia abaixo dela, e o item apoiado parecia entrar na mesa/bancada. Com uma única
    // BoxCollider na raiz, ela passa a ser exatamente o volume dos renderers visíveis.
    private void AjustarCaixaAoVisual()
    {
        if (colliders.Length != 1 || !(colliders[0] is BoxCollider caixa) || caixa.transform != transform) return;
        if (GetComponent<ComputerPartItem>() == null && GetComponent<NotebookPartItem>() == null && GetComponent<ImpressoraPartItem>() == null
            && GetComponent<PendriveItem>() == null && GetComponent<Periferico>() == null) return;
        bool tem = false;
        Bounds local = default;
        Matrix4x4 paraMim = transform.worldToLocalMatrix;
        foreach (Renderer r in renderers)
        {
            if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
            Bounds lb = r.localBounds;
            Matrix4x4 m = paraMim * r.transform.localToWorldMatrix;
            for (int i = 0; i < 8; i++)
            {
                Vector3 canto = lb.center + Vector3.Scale(lb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 p = m.MultiplyPoint3x4(canto);
                if (!tem) { local = new Bounds(p, Vector3.zero); tem = true; } else local.Encapsulate(p);
            }
        }
        if (!tem) return;
        caixa.center = local.center;
        caixa.size = Vector3.Max(local.size, Vector3.one * 0.004f);   // nunca uma caixa achatada a zero
    }

    private Bounds RenderBounds()
    {
        // Renderers desligados (visual antigo escondido) não contam: o centro na mão é o do modelo que aparece.
        bool any = false;
        Bounds b = new Bounds(transform.position, Vector3.one * 0.1f);
        foreach (Renderer r in renderers)
        {
            if (r == null || !r.enabled) continue;
            if (!any) { b = r.bounds; any = true; }
            else b.Encapsulate(r.bounds);
        }
        return b;
    }

    // Mensagem não personalizada no Inspector = "Pegar".
    // Bloqueado: o motivo do bloqueador (ex.: "Desconecte o cabo de energia").
    public override string PromptText
    {
        get
        {
            string reason = BlockReason;
            if (reason != null) return reason;
            return string.IsNullOrEmpty(interactionMessage) || interactionMessage == "Interagir" ? "Pegar" : interactionMessage;
        }
    }

    public override bool IsBlocked => IsPickupBlocked;

    /// <summary>Motivo do bloqueio (null = não está bloqueado; "Não pode ser pego" se o bloqueador não disser).</summary>
    public string BlockReason
    {
        get
        {
            Init();
            foreach (IPickupBlocker b in blockers)
                if (b as Object != null && b.BlocksPickup)
                    return string.IsNullOrEmpty(b.BlockReason) ? "Não pode ser pego" : b.BlockReason;
            return null;
        }
    }

    /// <summary>
    /// Algum <see cref="IPickupBlocker"/> do objeto impede pegá-lo agora (ex.: gabinete com cabos presos à estação). Só o pegar é
    /// bloqueado: colliders e as outras interações do objeto (como abrir a tela de manutenção) continuam valendo.
    /// </summary>
    public bool IsPickupBlocked
    {
        get
        {
            Init();
            foreach (IPickupBlocker b in blockers)
                if (b as Object != null && b.BlocksPickup) return true;
            return false;
        }
    }

    /// <summary>
    /// Bloqueado (<see cref="IsPickupBlocked"/>) continua sendo alvo, para o HUD explicar o motivo; o clique só toca o
    /// som de bloqueio e não pega.
    /// </summary>
    public override bool CanInteract(InteractionDetector interactor)
        => base.CanInteract(interactor) && !IsHeld && interactor.Holder != null && !interactor.Holder.IsHolding;

    public override void Interact(InteractionDetector interactor)
    {
        if (IsPickupBlocked)
        {
            InteractionSounds.Raise(InteractionSound.PickupBlocked, transform.position);
            return;
        }
        if (interactor.Holder.Pick(this)) InteractionSounds.Raise(InteractionSound.ItemPickedUp, transform.position);
    }

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

        // Escala mundial original preservada (vezes a escala de mão), qualquer que seja o pai antigo.
        Vector3 hs = holdPoint.lossyScale;
        transform.localScale = new Vector3(restWorldScale.x / hs.x, restWorldScale.y / hs.y, restWorldScale.z / hs.z) * holdScale;

        // Com empunhadura a pose é a mesma qualquer que seja o giro em que o item estava (gaveta, mesa): rotação
        // relativa aos eixos LOCAIS do item. Sem ela, a rotação de repouso entra (comportamento antigo).
        Quaternion rot = useGripPoint
            ? holdPoint.rotation * Quaternion.Euler(holdEulerOffset)
            : holdPoint.rotation * Quaternion.Euler(holdEulerOffset) * restWorldRotation;
        transform.rotation = rot;

        if (useGripPoint)
        {
            // A empunhadura (ponto local configurado) fica no ponto de mão: a mão segura o cabo, não o meio do item.
            Vector3 grip = transform.TransformPoint(gripLocalPoint) - transform.position;
            transform.position = holdPoint.TransformPoint(holdPositionOffset) - grip;
        }
        else
        {
            // Centro visual (e não o pivô) fica no ponto de mão.
            Vector3 centerOffset = (rot * Quaternion.Inverse(restWorldRotation)) * restCenterOffset * holdScale;
            transform.position = holdPoint.TransformPoint(holdPositionOffset) - centerOffset;
        }

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
