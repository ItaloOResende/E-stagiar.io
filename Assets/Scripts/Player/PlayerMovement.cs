using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float speed = 5f;
    [SerializeField] private float gravity = -9.81f;

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

    private void Update()
    {
        if (!controller.enabled) return;

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
        if (keyboard != null)
        {
            if (keyboard.wKey.isPressed) input.y += 1;
            if (keyboard.sKey.isPressed) input.y -= 1;
            if (keyboard.aKey.isPressed) input.x -= 1;
            if (keyboard.dKey.isPressed) input.x += 1;
        }

        Vector3 direction = transform.right * input.x + transform.forward * input.y;
        if (controller.isGrounded && velocity.y < 0f) velocity.y = -2f;
        velocity.y += gravity * dt;

        // A single collision-aware movement: input + horizontal slide + gravity.
        controller.Move((direction.normalized * speed * control + slide + velocity) * dt);
    }

    private void OnDisable()
    {
        IsSlipping = false;
        slipElapsed = 0f;
        velocity = Vector3.zero;
    }
}
