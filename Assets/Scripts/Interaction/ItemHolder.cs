using UnityEngine;

/// <summary>
/// Segura um <see cref="PickupInteractable"/> no ponto de mão do Player e o solta de forma controlada.
/// </summary>
public class ItemHolder : MonoBehaviour
{
    [Tooltip("Ponto de mão (filho da câmera). O item carregado fica preso aqui.")]
    [SerializeField] private Transform holdPoint;
    [Tooltip("Visual do braço em 1ª pessoa (filho da câmera); opcional.")]
    [SerializeField] private GameObject armVisual;
    [SerializeField] private bool showArmOnlyWhileHolding = false;

    [Header("Soltar (clique esquerdo com item na mão)")]
    [Tooltip("Alcance (m) da mira para guardar numa gaveta ou colocar numa superfície, SÓ sem InteractionDetector no " +
             "mesmo objeto. Com ele (o Player), vale o alcance único do InteractionDetector.")]
    [SerializeField] private float placeReach = 2.5f;
    [Tooltip("Inclinação mínima da superfície mirada (normal.y) para colocar o item nela (1 = plana).")]
    [Range(0f, 1f)] [SerializeField] private float minSurfaceUp = 0.6f;
    [SerializeField] private LayerMask placementMask = ~0;
    [Tooltip("Soltar sem destino: distância horizontal (m) à frente dos olhos onde o item começa a cair (+ o raio do item).")]
    [SerializeField] private float releaseAhead = 0.6f;
    [Tooltip("Soltar sem destino: quanto abaixo dos olhos (m) o item começa a cair.")]
    [SerializeField] private float releaseBelowEyes = 0.3f;

    private InteractionDetector detector;

    /// <summary>Alcance para guardar/colocar: o mesmo de todas as interações (InteractionDetector), senão o próprio.</summary>
    public float PlaceReach
    {
        get
        {
            if (detector == null) detector = GetComponent<InteractionDetector>();
            return detector != null ? detector.InteractionDistance : placeReach;
        }
    }

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

    /// <summary>
    /// Clique esquerdo com item na mão, conforme a MIRA (centro da câmera):
    ///  - gaveta aberta e compatível → guarda dentro dela (<see cref="DrawerContents.TryStore"/>); gaveta cheia ou
    ///    de outro tipo de item → não solta (o item continua na mão);
    ///  - superfície horizontal ao alcance (mesa, chão...) → coloca ali;
    ///  - nada disso → o item sai da mão e cai pela física (sem ir parar num lugar arbitrário).
    /// </summary>
    public void Drop()
    {
        if (!IsHolding) return;
        PickupInteractable item = Current;
        Transform cam = holdPoint.parent != null ? holdPoint.parent : holdPoint;

        Target target = Aim(cam, out RaycastHit hit, out DrawerContents drawer);
        if (target == Target.Drawer)
        {
            if (!drawer.TryStore(item, hit.point)) return;   // incompatível ou sem espaço: continua na mão
            Current = null;
            InteractionSounds.Raise(InteractionSound.ItemStoredInDrawer, hit.point);
        }
        else if (target == Target.Surface)
        {
            Current = null;
            item.OnPlaced(hit.point, cam.eulerAngles.y + item.PlacedYawOffset, PlayerColliders);
            InteractionSounds.Raise(InteractionSound.ItemPlaced, hit.point);
        }
        else
        {
            Current = null;
            item.OnReleased(cam.position, PlayerColliders, ReleasePoint(cam, item));
            InteractionSounds.Raise(InteractionSound.ItemDropped, item.transform.position);
        }
        RefreshArm();
    }

    private enum Target { None, Drawer, Surface }

    /// <summary>
    /// Onde o item solto sem destino começa a cair: à frente do jogador na horizontal (fora da cápsula, mais longe
    /// para peças grandes), um pouco abaixo dos olhos. Obstáculos no caminho são tratados por OnReleased (recua).
    /// </summary>
    private Vector3 ReleasePoint(Transform cam, PickupInteractable item)
    {
        Vector3 flat = Vector3.ProjectOnPlane(cam.forward, Vector3.up);
        if (flat.sqrMagnitude < 0.01f) flat = Vector3.ProjectOnPlane(cam.up, Vector3.up);
        float ahead = releaseAhead + item.HorizontalRadius;
        return cam.position + flat.normalized * ahead + Vector3.down * releaseBelowEyes;
    }

    // Colliders do próprio jogador (CharacterController...): o item solto não colide com eles até se afastar.
    private Collider[] PlayerColliders =>
        System.Array.FindAll(GetComponentsInChildren<Collider>(), c => !c.transform.IsChildOf(holdPoint));

    // O ponto atingido é de um móvel com WorkSurface, mas bem acima do tampo dele (o colisor da raiz).
    private static bool AcimaDoTampo(RaycastHit h)
    {
        WorkSurface mesa = h.collider.GetComponentInParent<WorkSurface>();
        if (mesa == null || h.collider.gameObject == mesa.gameObject || !mesa.TryGetComponent(out Collider tampo)) return false;
        return h.point.y > tampo.bounds.max.y + 0.15f;
    }

    /// <summary>O que está na mira: área de guardar de uma gaveta aberta, uma superfície horizontal, ou nada.</summary>
    private Target Aim(Transform cam, out RaycastHit hit, out DrawerContents drawer)
    {
        drawer = null;
        hit = default;
        RaycastHit[] hits = Physics.RaycastAll(cam.position, cam.forward, PlaceReach, placementMask, QueryTriggerInteraction.Collide);
        System.Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));
        foreach (RaycastHit h in hits)
        {
            if (h.collider.transform.IsChildOf(transform)) continue;                 // o próprio jogador
            DrawerContents d = h.collider.GetComponentInParent<DrawerContents>();
            if (h.collider.isTrigger)
            {
                if (d == null || d.StorageArea != h.collider || !d.IsOpenForStorage) continue;  // outros gatilhos não contam
                hit = h; drawer = d;
                return Target.Drawer;
            }
            // mirando um item que já está dentro de uma gaveta aberta = a gaveta
            if (d != null && d.IsOpenForStorage && d.StorageContains(h.point))
            {
                hit = h; drawer = d;
                return Target.Drawer;
            }
            // partes altas de uma mesa/bancada (ex.: a prateleira do alto da bancada de manutenção) não são apoio: a mira
            // segue até o tampo de baixo, onde o item fica
            if (AcimaDoTampo(h)) continue;
            if (h.normal.y >= minSurfaceUp) { hit = h; return Target.Surface; }
            return Target.None;                                                      // parede/lado de móvel: cai
        }
        return Target.None;
    }

    /// <summary>
    /// Tira o item da mão e o GUARDA (desativado, sem física) num ponto do mundo — ex.: cabo conectado ao PC,
    /// peça instalada no gabinete. O objeto não é destruído; <see cref="Retrieve"/> o devolve à mão.
    /// </summary>
    public PickupInteractable Stow(Vector3 storePosition)
    {
        if (!IsHolding) return null;
        PickupInteractable item = Current;
        Current = null;
        item.OnDropped(storePosition, 0f);
        item.MarkInEquipment();
        item.gameObject.SetActive(false);
        RefreshArm();
        return item;
    }

    /// <summary>Devolve à mão um item guardado por <see cref="Stow"/> (ou recém-criado). Falha com a mão ocupada.</summary>
    public bool Retrieve(GameObject stored)
    {
        if (stored == null || IsHolding) return false;
        PickupInteractable item = stored.GetComponentInParent<PickupInteractable>(true);
        if (item == null) return false;
        item.gameObject.SetActive(true);
        return Pick(item);
    }

    private void RefreshArm()
    {
        if (armVisual != null) armVisual.SetActive(!showArmOnlyWhileHolding || IsHolding);
    }
}
