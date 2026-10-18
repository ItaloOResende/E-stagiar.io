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
    [Tooltip("Outras partes que abrem/fecham junto com esta (ex.: a outra porta de um armário de duas portas).")]
    [SerializeField] private OpenableInteractable[] linked = new OpenableInteractable[0];

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

    public override string PromptText
    {
        get
        {
            string motivo = LockReason;
            if (motivo != null) return motivo;
            return string.IsNullOrEmpty(interactionMessage) || interactionMessage == "Interagir" ? (IsOpen ? "Fechar" : "Abrir") : interactionMessage;
        }
    }

    /// <summary>Trancado agora por um <see cref="IOpenLock"/> do objeto (só impede ABRIR; fechar sempre pode).</summary>
    public override bool IsBlocked => LockReason != null;

    private string LockReason
    {
        get
        {
            if (targetOpen) return null;
            foreach (IOpenLock l in GetComponents<IOpenLock>())
                if (l as Object != null && l.IsLocked) return string.IsNullOrEmpty(l.LockReason) ? "Trancada" : l.LockReason;
            return null;
        }
    }

    public override bool CanInteract(InteractionDetector interactor)
        => base.CanInteract(interactor) && !IsMoving
           && (interactor.Holder == null || !interactor.Holder.IsHolding);

    public override void Interact(InteractionDetector interactor)
    {
        if (IsBlocked)
        {
            // trancada: não abre; som de recusa e um tranco curto na gaveta
            InteractionSounds.Raise(InteractionSound.PickupBlocked, MovingPart.position);
            StartCoroutine(Tranco());
            return;
        }
        Toggle();
        if (targetOpen) OpenedBy?.Invoke(interactor);
    }

    public void Toggle() => SetOpen(!targetOpen);

    public void SetOpen(bool open)
    {
        if (open == targetOpen) return;
        targetOpen = open;
        InteractionSounds.Raise(open ? InteractionSound.DrawerOpened : InteractionSound.DrawerClosed, MovingPart.position);
        (open ? onOpened : onClosed).Invoke();
        if (open) Opened?.Invoke();
        foreach (OpenableInteractable l in linked) if (l != null && l != this) l.SetOpen(open);
    }

    private System.Collections.IEnumerator Tranco()
    {
        Vector3 axis = closedLocalRot * localAxis.normalized;
        for (float t = 0f; t < 0.25f; t += Time.deltaTime)
        {
            if (!IsMoving && !targetOpen && mode == Mode.Slide)
                movingPart.localPosition = closedLocalPos + axis * (Mathf.Sin(t / 0.25f * Mathf.PI) * 0.012f);
            yield return null;
        }
        if (!targetOpen && !IsMoving) Apply();
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

/// <summary>Tranca um <see cref="OpenableInteractable"/> do mesmo objeto (ex.: gaveta bloqueada durante um chamado).</summary>
public interface IOpenLock
{
    bool IsLocked { get; }
    /// <summary>Texto curto do HUD (ex.: "Gaveta trancada").</summary>
    string LockReason { get; }
}
