using UnityEngine;

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

        transform.SetParent(holdPoint, true);

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
