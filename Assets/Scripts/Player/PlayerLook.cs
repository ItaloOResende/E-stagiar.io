using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(100)]
public class PlayerLook : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private PlayerMovement movement;
    [Tooltip("Objeto visual da mao ou conjunto de maos/itens, filho direto ou indireto do Player. Nao use o Player, a camera ou um ancestral da camera.")]
    [SerializeField] private Transform handTransform;
    [Tooltip("Suporte/empty onde o sistema de pegar itens coloca os objetos. Ele acompanhara exatamente o delay e bob da mao. Se ja estiver dentro do visual da mao, nao precisa preencher.")]
    [SerializeField] private Transform heldItemRoot;

    private Transform originalItemParent;
    private Vector3 originalItemPosition, originalItemScale;
    private Quaternion originalItemRotation;
    private bool itemRootReparented;

    [Header("Mouse")]
    [SerializeField, Min(0f)] private float sensitivity = 0.1f;
    [SerializeField, Min(0f)] private float verticalSensitivityMultiplier = 1f;
    [SerializeField] private bool invertY;
    [SerializeField, Range(1f, 89.9f)] private float maxLookAngle = 85f;
    [SerializeField] private bool lookEnabled = true;
    [SerializeField] private bool requireLockedCursor = true;
    [SerializeField] private bool lockCursorOnStart = true;
    [SerializeField] private bool escapeReleasesCursor = true;
    [Tooltip("Opcional. Deixe desligado se sua interface usa clique esquerdo.")]
    [SerializeField] private bool clickCapturesCursor;

    [Header("Balanco da camera")]
    [SerializeField] private bool cameraBobEnabled = true;
    [SerializeField, Min(0.1f)] private float bobCycleDistance = 3.6f;
    [SerializeField] private Vector2 bobPosition = new Vector2(0.012f, 0.018f);
    [SerializeField, Min(0f)] private float bobBlendSpeed = 10f;
    [SerializeField, Min(0f)] private float minimumBobSpeed = 0.15f;
    [SerializeField, Min(0.01f)] private float fullBobSpeed = 5f;

    [Header("Mao: atraso e bounce")]
    [SerializeField] private bool handSwayEnabled = true;
    [Tooltip("Frequencia da mola. Menor = mais atraso.")]
    [SerializeField, Min(0.1f)] private float handFrequency = 4f;
    [Tooltip("Abaixo de 1 ha bounce. Maior = menos oscilacao.")]
    [SerializeField, Range(0.05f, 2f)] private float handDamping = 0.65f;
    [Tooltip("Deslocamento por velocidade angular do olhar (graus/segundo).")]
    [SerializeField, Min(0f)] private float lookPositionAmount = 0.00015f;
    [SerializeField, Min(0f)] private float lookRotationAmount = 0.018f;
    [SerializeField, Min(0f)] private float maximumHandOffset = 0.06f;
    [SerializeField, Min(0f)] private float maximumHandAngle = 8f;
    [Tooltip("Deslocamento ao andar lateralmente e para frente, por unidade/segundo.")]
    [SerializeField] private Vector3 movementHandOffset = new Vector3(0.004f, 0.002f, 0.006f);
    [SerializeField] private Vector2 handBobPosition = new Vector2(0.005f, 0.008f);

    private Transform pitchPivot, effectsPivot, handPivot;
    private Transform originalCameraParent, originalHandParent;
    private Vector3 originalCameraPosition, originalHandPosition;
    private Quaternion originalCameraRotation, originalHandRotation;
    private float xRotation, phase, bobWeight;
    private Vector2 lookRate;
    private Vector3 handOffset, handOffsetVelocity, handAngles, handAngleVelocity;
    private bool wasLocked;
    private Transform focusTarget;
    private Vector3 focusOffset;
    private float focusRemaining, focusSpeed;
    public void FocusOn(Transform target, Vector3 worldOffset, float duration, float degreesPerSecond)
    {
        if (!isActiveAndEnabled || !CanLook || target == null) return;
        focusTarget = target; focusOffset = worldOffset;
        focusRemaining = Mathf.Max(0f, duration); focusSpeed = Mathf.Max(1f, degreesPerSecond);
    }

    private bool CanLook => lookEnabled && Application.isFocused &&
        (!requireLockedCursor || Cursor.lockState == CursorLockMode.Locked);

    private void Awake()
    {
        if (movement == null) movement = GetComponent<PlayerMovement>();
        if (cameraTransform == null)
        {
            Camera childCamera = GetComponentInChildren<Camera>();
            if (childCamera != null) cameraTransform = childCamera.transform;
        }
        if (cameraTransform == null || cameraTransform == transform || !cameraTransform.IsChildOf(transform))
        {
            Debug.LogError("PlayerLook: indique uma camera filha do Player.", this);
            enabled = false;
            return;
        }
        originalCameraParent = cameraTransform.parent;
        originalCameraPosition = cameraTransform.localPosition;
        originalCameraRotation = cameraTransform.localRotation;
        pitchPivot = Wrap(cameraTransform, "Look Pitch (runtime)");
        effectsPivot = Wrap(cameraTransform, "Camera Effects (runtime)");
        if (handTransform != null)
        {
            if (handTransform == transform || handTransform == cameraTransform ||
                !handTransform.IsChildOf(transform) || cameraTransform.IsChildOf(handTransform))
                Debug.LogWarning("PlayerLook: Hand Transform precisa ser um objeto visual filho do Player, sem conter a camera; efeito da mao ignorado.", this);
            else
            {
                originalHandParent = handTransform.parent;
                originalHandPosition = handTransform.localPosition;
                originalHandRotation = handTransform.localRotation;
                handPivot = Wrap(handTransform, "Hand Spring (runtime)");
            }
        }
        ConnectHeldItemRoot();
    }

    private void ConnectHeldItemRoot()
    {
        if (heldItemRoot == null) return;
        if (handPivot == null)
        {
            Debug.LogWarning("PlayerLook: configure Hand Transform para os itens acompanharem a mao.", this);
            return;
        }
        // Already following the hand: do not apply a second copy of the effect.
        if (heldItemRoot == handPivot || heldItemRoot.IsChildOf(handPivot)) return;
        if (heldItemRoot == transform || !heldItemRoot.IsChildOf(transform) ||
            heldItemRoot == cameraTransform || cameraTransform.IsChildOf(heldItemRoot) ||
            handPivot.IsChildOf(heldItemRoot))
        {
            Debug.LogWarning("PlayerLook: Held Item Root deve ser o suporte dos itens, filho do Player, sem conter a camera ou a mao.", this);
            return;
        }
        originalItemParent = heldItemRoot.parent;
        originalItemPosition = heldItemRoot.localPosition;
        originalItemRotation = heldItemRoot.localRotation;
        originalItemScale = heldItemRoot.localScale;
        // Keep the anchor reference and its current world pose. Future pickups
        // parented to that same anchor automatically inherit the hand spring.
        heldItemRoot.SetParent(handPivot, true);
        itemRootReparented = true;
    }

    private static Transform Wrap(Transform child, string objectName)
    {
        Transform pivot = new GameObject(objectName).transform;
        pivot.SetParent(child.parent, false);
        pivot.localPosition = child.localPosition;
        pivot.localRotation = child.localRotation;
        child.SetParent(pivot, false);
        child.localPosition = Vector3.zero;
        child.localRotation = Quaternion.identity;
        return pivot;
    }

    private void Start()
    {
        if (lockCursorOnStart) SetCursorLocked(true);
    }

    private void Update()
    {
        bool released = escapeReleasesCursor && Keyboard.current != null &&
            Keyboard.current.escapeKey.wasPressedThisFrame;
        if (released) SetCursorLocked(false);
        else if (clickCapturesCursor && lookEnabled && Application.isFocused &&
            Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            SetCursorLocked(true);

        bool locked = Cursor.lockState == CursorLockMode.Locked;
        lookRate = Vector2.zero;
        // Ignore the first delta after capturing the cursor to avoid a camera jump.
        if (focusTarget != null && focusRemaining > 0f && CanLook && Time.deltaTime > 0f)
        {
            Vector3 d = focusTarget.position + focusOffset - cameraTransform.position;
            Vector3 flat = Vector3.ProjectOnPlane(d, Vector3.up);
            if (flat.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(flat), focusSpeed * Time.deltaTime);
            Vector3 local = Quaternion.Inverse(pitchPivot.parent.rotation * originalCameraRotation) * d;
            float desired = -Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg;
            xRotation = Mathf.MoveTowardsAngle(xRotation, Mathf.Clamp(desired, -maxLookAngle, maxLookAngle), focusSpeed * Time.deltaTime);
            focusRemaining -= Time.deltaTime;
            wasLocked = locked;
            return;
        }
        if (!CanLook) focusRemaining = 0f;
        if (CanLook && (!locked || wasLocked) && Mouse.current != null && Time.deltaTime > 0f)
        {
            Vector2 delta = Mouse.current.delta.ReadValue();
            float yaw = delta.x * sensitivity;
            float pitch = delta.y * sensitivity * verticalSensitivityMultiplier * (invertY ? -1f : 1f);
            transform.Rotate(Vector3.up, yaw, Space.World);
            float previousPitch = xRotation;
            xRotation = Mathf.Clamp(xRotation - pitch, -maxLookAngle, maxLookAngle);
            lookRate = Vector2.ClampMagnitude(new Vector2(yaw, previousPitch - xRotation) / Time.deltaTime, 720f);
        }
        wasLocked = locked;
    }

    private void LateUpdate()
    {
        if (pitchPivot == null) return;
        float dt = Time.deltaTime;
        pitchPivot.localRotation = originalCameraRotation * Quaternion.Euler(xRotation, 0f, 0f);
        Vector3 speed = movement != null ? movement.PlanarVelocity : Vector3.zero;
        float magnitude = speed.magnitude;
        bool walking = CanLook && movement != null && movement.CanMove && movement.IsGrounded &&
            !movement.IsSlipping && magnitude > minimumBobSpeed;
        float targetWeight = walking ? Mathf.Clamp01(magnitude / Mathf.Max(0.01f, fullBobSpeed)) : 0f;
        bobWeight = Mathf.Lerp(bobWeight, targetWeight, 1f - Mathf.Exp(-bobBlendSpeed * dt));
        if (walking) phase = (phase + magnitude * dt * Mathf.PI * 2f / Mathf.Max(0.1f, bobCycleDistance)) % (Mathf.PI * 2f);
        float side = Mathf.Sin(phase) * bobWeight;
        float vertical = Mathf.Cos(phase * 2f) * bobWeight;
        effectsPivot.localPosition = cameraBobEnabled ? new Vector3(side * bobPosition.x, vertical * bobPosition.y, 0f) : Vector3.zero;
        effectsPivot.localRotation = Quaternion.identity;

        if (handPivot == null) return;
        Vector3 localSpeed = CanLook && movement != null && movement.CanMove ? transform.InverseTransformDirection(speed) : Vector3.zero;
        Vector3 targetPosition = Vector3.zero;
        Vector3 targetAngles = Vector3.zero;
        if (handSwayEnabled && CanLook)
        {
            targetPosition = new Vector3(-lookRate.x * lookPositionAmount - localSpeed.x * movementHandOffset.x,
                -lookRate.y * lookPositionAmount - Mathf.Abs(localSpeed.z) * movementHandOffset.y,
                -localSpeed.z * movementHandOffset.z);
            targetPosition += new Vector3(side * handBobPosition.x, vertical * handBobPosition.y, 0f);
            targetPosition = Vector3.ClampMagnitude(targetPosition, maximumHandOffset);
            targetAngles = Vector3.ClampMagnitude(new Vector3(lookRate.y * lookRotationAmount,
                -lookRate.x * lookRotationAmount, 0f), maximumHandAngle);
        }
        Spring(ref handOffset, ref handOffsetVelocity, targetPosition, dt);
        Spring(ref handAngles, ref handAngleVelocity, targetAngles, dt);
        // Hard bounds include spring overshoot, not just the target.
        handOffset = Vector3.ClampMagnitude(handOffset, maximumHandOffset);
        handAngles = Vector3.ClampMagnitude(handAngles, maximumHandAngle);
        handPivot.localPosition = originalHandPosition + originalHandRotation * handOffset;
        handPivot.localRotation = originalHandRotation * Quaternion.Euler(handAngles);
    }

    private void Spring(ref Vector3 value, ref Vector3 velocity, Vector3 target, float dt)
    {
        // Small substeps keep the underdamped spring stable at low frame rates.
        float remaining = Mathf.Min(dt, 0.1f);
        float omega = Mathf.PI * 2f * Mathf.Clamp(handFrequency, 0.1f, 20f);
        while (remaining > 0f)
        {
            float step = Mathf.Min(remaining, 1f / 240f);
            velocity = (velocity + (target - value) * omega * omega * step) /
                (1f + 2f * handDamping * omega * step + omega * omega * step * step);
            value += velocity * step;
            remaining -= step;
        }
    }

    public void SetLookEnabled(bool value)
    {
        lookEnabled = value;
        if (!value) focusRemaining = 0f;
        lookRate = Vector2.zero;
        wasLocked = false;
    }

    public void SetCursorLocked(bool value)
    {
        Cursor.lockState = value ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !value;
        wasLocked = false;
    }

    private void OnDisable()
    {
        focusTarget = null; focusRemaining = 0f;
        lookRate = Vector2.zero;
        wasLocked = false;
        bobWeight = 0f;
        handOffset = handOffsetVelocity = handAngles = handAngleVelocity = Vector3.zero;
        if (effectsPivot != null)
        {
            effectsPivot.localPosition = Vector3.zero;
            effectsPivot.localRotation = Quaternion.identity;
        }
        if (handPivot != null)
        {
            handPivot.localPosition = originalHandPosition;
            handPivot.localRotation = originalHandRotation;
        }
    }

    private void OnDestroy()
    {
        // Restore the item anchor before destroying the hand pivot.
        if (itemRootReparented && heldItemRoot != null)
        {
            heldItemRoot.SetParent(originalItemParent, false);
            heldItemRoot.localPosition = originalItemPosition;
            heldItemRoot.localRotation = originalItemRotation;
            heldItemRoot.localScale = originalItemScale;
        }
        // Restore visual children if this component is removed at runtime.
        if (handPivot != null && handTransform != null)
        {
            handTransform.SetParent(originalHandParent, false);
            handTransform.localPosition = originalHandPosition;
            handTransform.localRotation = originalHandRotation;
            Destroy(handPivot.gameObject);
        }
        if (pitchPivot != null && cameraTransform != null)
        {
            cameraTransform.SetParent(originalCameraParent, false);
            cameraTransform.localPosition = originalCameraPosition;
            cameraTransform.localRotation = originalCameraRotation;
            Destroy(pitchPivot.gameObject);
        }
    }
}
