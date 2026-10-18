using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float speed = 5f;
    [SerializeField] private float gravity = -9.81f;

    [Header("Resposta do movimento")]
    [SerializeField, Min(0f)] private float acceleration = 30f;
    [SerializeField, Min(0f)] private float deceleration = 50f;
    [SerializeField] private bool movementEnabled = true;
    [SerializeField] private bool requireLockedCursor = true;
    [SerializeField, Min(0f)] private float groundStickSpeed = 2f;
    [SerializeField, Min(0f)] private float maximumFallSpeed = 40f;

    [Header("Passos por distancia")]
    [SerializeField] private bool footstepsEnabled = true;
    [SerializeField] private AudioSource footstepSource;
    [SerializeField] private AudioClip[] defaultFootsteps;
    [SerializeField, Min(0.1f)] private float stepDistance = 1.8f;
    [SerializeField, Range(0f, 1f)] private float footstepVolume = 0.65f;
    [SerializeField] private Vector2 pitchRange = new Vector2(0.94f, 1.06f);
    [SerializeField] private LayerMask surfaceMask = ~0;
    [SerializeField, Min(0.01f)] private float surfaceProbeDistance = 0.35f;
    [SerializeField, Min(0f)] private float minimumStepSpeed = 0.15f;
    [SerializeField] private SurfaceFootsteps[] surfaces;

    [System.Serializable]
    public class SurfaceFootsteps
    {
        [Tooltip("Tag do collider do chao. Sons vazios usam os sons padrao.")]
        public string surfaceTag = "Untagged";
        public AudioClip[] clips;
        [Range(0f, 1f)] public float volumeMultiplier = 1f;
    }

    public Vector3 PlanarVelocity { get; private set; }
    public bool IsGrounded => controller != null && controller.enabled && controller.isGrounded;
    public bool CanMove => movementEnabled && Application.isFocused &&
        (!requireLockedCursor || Cursor.lockState == CursorLockMode.Locked);
    private Vector3 currentMove;
    private float stepTravel;
    private AudioClip lastStep;
    private CharacterController controller;
    private Vector3 velocity;
    private Vector3 slipDirection;
    private float slipElapsed;
    private float slipDuration;
    private float slipSpeed;
    private float slipSpin;
    private float slipMinControl;
    private float slipBlendIn;

    public bool IsSlipping { get; private set; }

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (footstepSource == null)
        {
            footstepSource = gameObject.AddComponent<AudioSource>();
            footstepSource.playOnAwake = false;
            footstepSource.spatialBlend = 0f;
        }
    }

    // Called by the puddle; overlapping obstacles cannot stack the effect.
    public bool TryStartSlip(Vector3 direction, float duration, float slideSpeed,
        float spinSpeed, float minimumControl, float blendIn)
    {
        if (!isActiveAndEnabled || !controller.enabled || IsSlipping)
            return false;

        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f || duration <= 0f)
            return false;

        slipDirection = direction.normalized;
        slipDuration = Mathf.Max(0.1f, duration);
        slipSpeed = Mathf.Max(0f, slideSpeed);
        slipSpin = spinSpeed;
        slipMinControl = Mathf.Clamp(minimumControl, 0.05f, 1f);
        slipBlendIn = Mathf.Clamp(blendIn, 0.01f, slipDuration * 0.25f);
        slipElapsed = 0f;
        IsSlipping = true;
        return true;
    }

    // Mantem o impulso vertical no mesmo CharacterController usado pela poca.
    public bool TryStartShove(Vector3 direction, float duration, float slideSpeed,
        float spinSpeed, float minimumControl, float blendIn, float upwardSpeed)
    {
        if (!TryStartSlip(direction, duration, slideSpeed, spinSpeed, minimumControl, blendIn)) return false;
        if (upwardSpeed > 0f) velocity.y = upwardSpeed;
        return true;
    }

    private void Update()
    {
        if (!controller.enabled)
        {
            currentMove = Vector3.zero;
            PlanarVelocity = Vector3.zero;
            stepTravel = 0f;
            return;
        }

        float dt = Time.deltaTime;
        float control = 1f;
        Vector3 slide = Vector3.zero;

        if (IsSlipping)
        {
            // Midpoint sampling keeps the fade smooth across different frame rates.
            float step = Mathf.Min(dt, slipDuration - slipElapsed);
            float sample = slipElapsed + step * 0.5f;
            float entry = Mathf.SmoothStep(0f, 1f, sample / slipBlendIn);
            float recovery = Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(slipBlendIn, slipDuration, sample));
            float strength = entry * (1f - recovery);
            float frameStrength = dt > 0f ? strength * step / dt : 0f;

            control = Mathf.Lerp(1f, slipMinControl, frameStrength);
            slide = slipDirection * (slipSpeed * frameStrength);
            transform.Rotate(Vector3.up, slipSpin * strength * step, Space.World);

            slipElapsed += step;
            if (slipElapsed >= slipDuration) IsSlipping = false;
        }

        Vector2 input = Vector2.zero;
        Keyboard keyboard = Keyboard.current;
        if (CanMove && keyboard != null)
        {
            if (keyboard.wKey.isPressed) input.y += 1;
            if (keyboard.sKey.isPressed) input.y -= 1;
            if (keyboard.aKey.isPressed) input.x -= 1;
            if (keyboard.dKey.isPressed) input.x += 1;
        }

        input = Vector2.ClampMagnitude(input, 1f);
        Vector3 target = (transform.right * input.x + transform.forward * input.y) * speed;
        float rate = input.sqrMagnitude > 0f ? acceleration : deceleration;
        currentMove = rate <= 0f ? target : Vector3.MoveTowards(currentMove, target, rate * dt);
        if (!CanMove) currentMove = Vector3.zero;
        if (controller.isGrounded && velocity.y < 0f) velocity.y = -groundStickSpeed;
        velocity.y = Mathf.Max(velocity.y + gravity * dt, -maximumFallSpeed);

        Vector3 before = transform.position;
        // Preserve one collision-aware Move, including the existing puddle effect.
        controller.Move((currentMove * control + slide + velocity) * dt);
        Vector3 travelled = transform.position - before;
        travelled.y = 0f;
        PlanarVelocity = dt > 0f ? travelled / dt : Vector3.zero;
        UpdateFootsteps(travelled.magnitude);
    }

    public void SetMovementEnabled(bool value)
    {
        movementEnabled = value;
        if (!value)
        {
            currentMove = Vector3.zero;
            stepTravel = 0f;
        }
    }

    private void UpdateFootsteps(float distance)
    {
        if (!footstepsEnabled || !CanMove || IsSlipping || !IsGrounded ||
            PlanarVelocity.magnitude < minimumStepSpeed)
        {
            stepTravel = 0f;
            return;
        }
        stepTravel += distance;
        if (stepTravel < Mathf.Max(0.1f, stepDistance)) return;
        stepTravel %= Mathf.Max(0.1f, stepDistance);
        AudioClip[] clips = defaultFootsteps;
        float volume = footstepVolume;
        Bounds bounds = controller.bounds;
        Vector3 origin = new Vector3(bounds.center.x, bounds.min.y + 0.1f, bounds.center.z);
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit,
            0.1f + surfaceProbeDistance, surfaceMask, QueryTriggerInteraction.Ignore) && surfaces != null)
        {
            foreach (SurfaceFootsteps surface in surfaces)
            {
                if (surface == null || hit.collider.tag != surface.surfaceTag) continue;
                if (surface.clips != null && surface.clips.Length > 0) clips = surface.clips;
                volume *= surface.volumeMultiplier;
                break;
            }
        }
        if (footstepSource == null || clips == null || clips.Length == 0) return;
        int index = Random.Range(0, clips.Length);
        if (clips.Length > 1 && clips[index] == lastStep)
            index = (index + Random.Range(1, clips.Length)) % clips.Length;
        AudioClip clip = clips[index];
        if (clip == null) return;
        lastStep = clip;
        footstepSource.pitch = Random.Range(Mathf.Max(0.01f, Mathf.Min(pitchRange.x, pitchRange.y)),
            Mathf.Max(0.01f, Mathf.Max(pitchRange.x, pitchRange.y)));
        footstepSource.PlayOneShot(clip, volume);
    }

    private void OnDisable()
    {
        IsSlipping = false;
        slipElapsed = 0f;
        velocity = Vector3.zero;
        currentMove = Vector3.zero;
        PlanarVelocity = Vector3.zero;
        stepTravel = 0f;
    }
}
