using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Camera livre para a cena duplicada do trailer.
/// Reutiliza SupervisorPatrol e SupervisorFeedback existentes no projeto.
/// Nao adiciona infracoes ao log nem altera pontuacao.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class TrailerNoclipCamera : MonoBehaviour
{
    [Header("Movimento")]
    [SerializeField, Min(0.01f)] private float moveSpeed = 4f;
    [SerializeField, Min(1f)] private float fastMultiplier = 3f;
    [SerializeField, Min(0.01f)] private float minimumSpeed = 0.2f;
    [SerializeField, Min(0.01f)] private float maximumSpeed = 40f;
    [SerializeField, Min(0f)] private float scrollSensitivity = 0.2f;
    [Tooltip("M alterna este modo. Desligado: movimento imediato.")]
    [SerializeField] private bool accelerationEnabled;
    [SerializeField, Min(0.01f)] private float acceleration = 8f;
    [SerializeField, Min(0.01f)] private float deceleration = 10f;

    [Header("Mouse")]
    [Tooltip("Graus por pixel no novo Input System.")]
    [SerializeField, Min(0.001f)] private float mouseSensitivity = 0.12f;
    [Tooltip("Multiplicador para os eixos do Input Manager antigo.")]
    [SerializeField, Min(0.01f)] private float legacyMouseSensitivity = 2f;
    [SerializeField] private bool invertY;
    [SerializeField, Range(1f, 89.9f)] private float pitchLimit = 89f;
    [SerializeField] private bool captureMouseOnEnable = true;

    [Header("FOV - segurar [ aumenta / ] diminui")]
    [SerializeField, Range(1f, 179f)] private float minimumFov = 15f;
    [SerializeField, Range(1f, 179f)] private float maximumFov = 110f;
    [SerializeField, Min(0.1f)] private float fovDegreesPerSecond = 30f;

    [Header("Chefe - tecla 1")]
    [Tooltip("Arraste o SupervisorPatrol do chefe da cena.")]
    [SerializeField] private SupervisorPatrol supervisor;
    [Tooltip("Opcional. Vazio: usa o SupervisorFeedback no mesmo objeto do Patrol.")]
    [SerializeField] private SupervisorFeedback supervisorFeedback;
    [SerializeField] private bool triggerInfractionFeedback = true;

    [Header("Interface - H mostra/oculta")]
    [SerializeField] private bool showOverlay = true;
    [SerializeField] private Vector2 overlayPosition = new Vector2(16f, 16f);

    private Camera controlledCamera;
    private Vector3 velocity;
    private float yaw, pitch;
    private bool mouseCaptured;
    private CursorLockMode previousLock;
    private bool previousCursorVisible;
    private SupervisorPatrol reactingSupervisor;
    private Transform previousSupervisorTarget;

    private struct Controls
    {
        public Vector3 movement;
        public Vector2 look;
        public float scroll, fov;
        public bool fast, toggleAcceleration, toggleOverlay, toggleCursor, react;
    }

    private void Awake()
    {
        controlledCamera = GetComponent<Camera>();
    }

    private void OnEnable()
    {
        previousLock = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        Vector3 angles = transform.eulerAngles;
        yaw = angles.y;
        pitch = Mathf.Clamp(Mathf.DeltaAngle(0f, angles.x), -pitchLimit, pitchLimit);
        velocity = Vector3.zero;
        CaptureMouse(captureMouseOnEnable);
    }

    private void Update()
    {
        if (!Application.isFocused) return;
        Controls input = ReadControls();
        if (input.toggleOverlay) showOverlay = !showOverlay;
        if (input.toggleCursor) CaptureMouse(!mouseCaptured);
        if (input.toggleAcceleration) SetAcceleration(!accelerationEnabled);
        if (!mouseCaptured || Cursor.lockState != CursorLockMode.Locked)
        {
            velocity = Vector3.zero;
            return;
        }

        float dt = Time.unscaledDeltaTime;
        yaw = Mathf.Repeat(yaw + input.look.x, 360f);
        pitch = Mathf.Clamp(pitch + input.look.y * (invertY ? 1f : -1f), -pitchLimit, pitchLimit);
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);

        moveSpeed = Mathf.Clamp(moveSpeed * Mathf.Exp(input.scroll * scrollSensitivity),
            minimumSpeed, Mathf.Max(minimumSpeed, maximumSpeed));
        Vector3 local = Vector3.ClampMagnitude(input.movement, 1f);
        // W/S segue a mira; Q/E usa o eixo vertical do mundo.
        Vector3 direction = transform.right * local.x + Vector3.up * local.y + transform.forward * local.z;
        direction = Vector3.ClampMagnitude(direction, 1f);
        Vector3 desired = direction * moveSpeed * (input.fast ? fastMultiplier : 1f);
        if (accelerationEnabled)
        {
            float rate = local.sqrMagnitude > 0f ? acceleration : deceleration;
            velocity = Vector3.Lerp(velocity, desired, 1f - Mathf.Exp(-rate * dt));
            if (desired == Vector3.zero && velocity.sqrMagnitude < 0.000001f)
                velocity = Vector3.zero;
        }
        else velocity = desired;
        transform.position += velocity * dt;

        if (input.fov != 0f)
            controlledCamera.fieldOfView = Mathf.Clamp(
                controlledCamera.fieldOfView + input.fov * fovDegreesPerSecond * dt,
                minimumFov, Mathf.Max(minimumFov, maximumFov));
        if (input.react) TriggerSupervisorReaction();
    }

    private void LateUpdate()
    {
        if (reactingSupervisor != null &&
            (!reactingSupervisor.isActiveAndEnabled ||
             reactingSupervisor.State != SupervisorPatrol.PatrolState.Reacting))
            RestoreSupervisorTarget();
    }

    public void TriggerSupervisorReaction()
    {
        if (supervisor == null || !supervisor.isActiveAndEnabled)
        {
            Debug.LogWarning("TrailerNoclipCamera: atribua um SupervisorPatrol ativo no Inspector.", this);
            return;
        }
        // A reacao original ignora novos disparos enquanto esta em andamento.
        if (supervisor.State == SupervisorPatrol.PatrolState.Reacting) return;
        RestoreSupervisorTarget();
        reactingSupervisor = supervisor;
        previousSupervisorTarget = supervisor.Player;
        supervisor.Player = transform;
        supervisor.React(transform.position);
        SupervisorFeedback feedback = supervisorFeedback != null
            ? supervisorFeedback : supervisor.GetComponent<SupervisorFeedback>();
        if (triggerInfractionFeedback && feedback != null)
            feedback.NotifyInfraction();
    }

    private void RestoreSupervisorTarget()
    {
        if (reactingSupervisor != null && reactingSupervisor.Player == transform)
            reactingSupervisor.Player = previousSupervisorTarget;
        reactingSupervisor = null;
        previousSupervisorTarget = null;
    }

    private void SetAcceleration(bool enabledValue)
    {
        accelerationEnabled = enabledValue;
        if (!enabledValue) velocity = Vector3.zero;
    }

    private void CaptureMouse(bool capture)
    {
        mouseCaptured = capture;
        Cursor.lockState = capture ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !capture;
        velocity = Vector3.zero;
    }

    private void OnApplicationFocus(bool focused)
    {
        if (!focused) CaptureMouse(false);
    }

    private void OnDisable()
    {
        RestoreSupervisorTarget();
        velocity = Vector3.zero;
        Cursor.lockState = previousLock;
        Cursor.visible = previousCursorVisible;
    }

    private void OnGUI()
    {
        if (!showOverlay) return;
        GUILayout.BeginArea(new Rect(overlayPosition.x, overlayPosition.y, 370f, 160f), GUI.skin.box);
        GUILayout.Label("CAMERA DO TRAILER");
        bool next = GUILayout.Toggle(accelerationEnabled, "Aceleracao suave [M]");
        if (next != accelerationEnabled) SetAcceleration(next);
        GUILayout.Label("WASD mover | Q/E descer/subir | Shift acelerar");
        GUILayout.Label("Scroll velocidade | [ +FOV | ] -FOV | 1 chefe");
        GUILayout.Label("Tab liberar/capturar mouse | H ocultar painel");
        GUILayout.Label("Velocidade: " + moveSpeed.ToString("0.0") +
                        " | FOV: " + (controlledCamera != null ? controlledCamera.fieldOfView.ToString("0.0") : "-"));
        GUILayout.EndArea();
    }

    private Controls ReadControls()
    {
        Controls c = new Controls();
#if ENABLE_INPUT_SYSTEM
        Keyboard k = Keyboard.current;
        Mouse m = Mouse.current;
        if (k != null)
        {
            c.movement = new Vector3(
                (k.dKey.isPressed ? 1f : 0f) - (k.aKey.isPressed ? 1f : 0f),
                (k.eKey.isPressed ? 1f : 0f) - (k.qKey.isPressed ? 1f : 0f),
                (k.wKey.isPressed ? 1f : 0f) - (k.sKey.isPressed ? 1f : 0f));
            c.fast = k.leftShiftKey.isPressed || k.rightShiftKey.isPressed;
            c.fov = (k.leftBracketKey.isPressed ? 1f : 0f) - (k.rightBracketKey.isPressed ? 1f : 0f);
            c.toggleAcceleration = k.mKey.wasPressedThisFrame;
            c.toggleOverlay = k.hKey.wasPressedThisFrame;
            c.toggleCursor = k.tabKey.wasPressedThisFrame;
            c.react = k.digit1Key.wasPressedThisFrame;
        }
        if (m != null)
        {
            c.look = m.delta.ReadValue() * mouseSensitivity;
            c.scroll = Mathf.Clamp(m.scroll.ReadValue().y / 120f, -4f, 4f);
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        c.movement = new Vector3(
            (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f),
            (Input.GetKey(KeyCode.E) ? 1f : 0f) - (Input.GetKey(KeyCode.Q) ? 1f : 0f),
            (Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.S) ? 1f : 0f));
        c.fast = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        c.look = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * legacyMouseSensitivity;
        c.scroll = Input.mouseScrollDelta.y;
        c.fov = (Input.GetKey(KeyCode.LeftBracket) ? 1f : 0f) - (Input.GetKey(KeyCode.RightBracket) ? 1f : 0f);
        c.toggleAcceleration = Input.GetKeyDown(KeyCode.M);
        c.toggleOverlay = Input.GetKeyDown(KeyCode.H);
        c.toggleCursor = Input.GetKeyDown(KeyCode.Tab);
        c.react = Input.GetKeyDown(KeyCode.Alpha1);
#endif
        return c;
    }
}
