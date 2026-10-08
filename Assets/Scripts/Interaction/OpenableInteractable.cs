using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Objeto que abre/fecha por interpolação suave: gaveta (deslizar), porta/armário (girar).
/// Eixo, distância/ângulo e duração configuráveis. Base para gaveteiros e containers:
/// o conteúdo pode ser filho de <see cref="movingPart"/> e se mover junto.
/// </summary>
public class OpenableInteractable : Interactable
{
    public enum Mode { Slide, Rotate }

    [Tooltip("Parte que se move. Vazio = este objeto.")]
    [SerializeField] private Transform movingPart;
    [SerializeField] private Mode mode = Mode.Slide;
    [Tooltip("Eixo no espaço LOCAL da parte móvel (deslize: direção; giro: eixo da dobradiça).")]
    [SerializeField] private Vector3 localAxis = Vector3.forward;
    [Tooltip("Slide: metros. Rotate: graus.")]
    [SerializeField] private float openAmount = 0.5f;
    [SerializeField] private float duration = 0.6f;
    [SerializeField] private AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private bool startsOpen = false;

    public UnityEvent onOpened;
    public UnityEvent onClosed;

    private Vector3 closedLocalPos;
    private Quaternion closedLocalRot;
    private float progress; // 0 fechado .. 1 aberto
    private bool targetOpen;

    /// <summary>Quem abriu (clique do jogador). Usado por gavetas que entregam item direto à mão.</summary>
    public event System.Action<InteractionDetector> OpenedBy;
    public event System.Action Opened;
    public event System.Action FinishedClosing;

    public Transform MovingPart => movingPart != null ? movingPart : transform;
    public bool IsOpen => targetOpen;
    public bool IsMoving => !Mathf.Approximately(progress, targetOpen ? 1f : 0f);

    private void Awake()
    {
        if (movingPart == null) movingPart = transform;
        closedLocalPos = movingPart.localPosition;
        closedLocalRot = movingPart.localRotation;
        targetOpen = startsOpen;
        progress = startsOpen ? 1f : 0f;
        Apply();
    }

    public override bool CanInteract(InteractionDetector interactor)
        => base.CanInteract(interactor) && !IsMoving
           && (interactor.Holder == null || !interactor.Holder.IsHolding);

    public override void Interact(InteractionDetector interactor)
    {
        Toggle();
        if (targetOpen) OpenedBy?.Invoke(interactor);
    }

    public void Toggle() => SetOpen(!targetOpen);

    public void SetOpen(bool open)
    {
        if (open == targetOpen) return;
        targetOpen = open;
        (open ? onOpened : onClosed).Invoke();
        if (open) Opened?.Invoke();
    }

    private void Update()
    {
        if (!IsMoving) return;
        float step = Time.deltaTime / Mathf.Max(0.01f, duration);
        progress = Mathf.MoveTowards(progress, targetOpen ? 1f : 0f, step);
        Apply();
        if (!targetOpen && !IsMoving) FinishedClosing?.Invoke();
    }

    private void Apply()
    {
        float t = ease.Evaluate(progress);
        Vector3 axis = localAxis.normalized;
        if (mode == Mode.Slide)
            movingPart.localPosition = closedLocalPos + closedLocalRot * (axis * (openAmount * t));
        else
            movingPart.localRotation = closedLocalRot * Quaternion.AngleAxis(openAmount * t, axis);
    }
}
