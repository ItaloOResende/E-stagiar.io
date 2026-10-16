using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider), typeof(Rigidbody), typeof(AudioSource))]
public class ObstaclePuddle : MonoBehaviour
{
    [Header("Ciclo da poca")]
    [Tooltip("Tempo aleatorio de espera enquanto a poca esta oculta.")]
    [SerializeField, Range(0.1f, 60f)] private float minInactiveTime = 10f;
    [SerializeField, Range(0.1f, 60f)] private float maxInactiveTime = 15f;
    [Tooltip("Tempo aleatorio durante o qual a poca fica visivel e causa escorregao.")]
    [SerializeField, Range(0.1f, 60f)] private float minActiveTime = 5f;
    [SerializeField, Range(0.1f, 60f)] private float maxActiveTime = 10f;
    [Tooltip("Opcional: objeto filho que contem apenas o visual da poca. Nao coloque o objeto deste script aqui. Se vazio, controla os Renderers deste objeto e dos filhos.")]
    [SerializeField] private GameObject puddleVisual;

    [Header("Escorregao")]
    [SerializeField, Min(0.1f)] private float duration = 3f;
    [SerializeField, Min(0f)] private float slideSpeed = 7f;
    [Tooltip("Velocidade maxima do giro horizontal, em graus por segundo.")]
    [SerializeField, Min(0f)] private float spinSpeed = 540f;
    [Tooltip("Fracao do controle WASD no inicio. 0.25 = 25%. Recupera ate 100%.")]
    [SerializeField, Range(0.05f, 1f)] private float minimumControl = 0.25f;
    [Tooltip("Tempo de entrada suave no escorregao.")]
    [SerializeField, Min(0.01f)] private float blendIn = 0.15f;

    [Header("Som")]
    [SerializeField] private AudioClip slipSound;
    [SerializeField, Range(0f, 1f)] private float volume = 1f;

    private AudioSource audioSource;
    private BoxCollider puddleTrigger;
    private Renderer[] visualRenderers;
    private bool[] originalRendererStates;
    private bool isPuddleActive;
    private float timeRemaining;

    private void Reset()
    {
        ConfigurePhysics();
        AudioSource source = GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
    }

    private void Awake()
    {
        ConfigurePhysics();
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        puddleTrigger = GetComponent<BoxCollider>();

        if (puddleVisual != null &&
            (puddleVisual == gameObject || !puddleVisual.transform.IsChildOf(transform)))
        {
            Debug.LogWarning("ObstaclePuddle: Puddle Visual deve ser um objeto filho. Usando os Renderers da poca.", this);
            puddleVisual = null;
        }

        visualRenderers = GetComponentsInChildren<Renderer>(true);
        originalRendererStates = new bool[visualRenderers.Length];
        for (int i = 0; i < visualRenderers.Length; i++)
            originalRendererStates[i] = visualRenderers[i].enabled;
    }

    private void OnEnable()
    {
        SetPuddleActive(false);
        timeRemaining = RandomTime(minInactiveTime, maxInactiveTime);
    }

    private void Update()
    {
        timeRemaining -= Time.deltaTime;
        if (timeRemaining > 0f) return;

        SetPuddleActive(!isPuddleActive);
        timeRemaining = isPuddleActive
            ? RandomTime(minActiveTime, maxActiveTime)
            : RandomTime(minInactiveTime, maxInactiveTime);
    }

    private void OnDisable()
    {
        SetPuddleActive(false);
    }

    private static float RandomTime(float minimum, float maximum)
    {
        float low = Mathf.Max(0.1f, Mathf.Min(minimum, maximum));
        float high = Mathf.Max(low, Mathf.Max(minimum, maximum));
        return Random.Range(low, high);
    }

    private void SetPuddleActive(bool active)
    {
        isPuddleActive = active;
        if (puddleTrigger != null) puddleTrigger.enabled = active;

        // Keep the script's GameObject enabled so the timer can keep running.
        if (puddleVisual != null)
        {
            puddleVisual.SetActive(active);
        }
        else if (visualRenderers != null)
        {
            for (int i = 0; i < visualRenderers.Length; i++)
                if (visualRenderers[i] != null)
                    visualRenderers[i].enabled = active && originalRendererStates[i];
        }
    }

    private void ConfigurePhysics()
    {
        GetComponent<BoxCollider>().isTrigger = true;
        Rigidbody body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isActiveAndEnabled || !isPuddleActive) return;
        PlayerMovement player = other.GetComponentInParent<PlayerMovement>();
        if (player == null) return;

        float angle = Random.Range(0f, Mathf.PI * 2f);
        Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
        float spin = spinSpeed * (Random.value < 0.5f ? -1f : 1f);

        if (player.TryStartSlip(direction, duration, slideSpeed, spin,
            minimumControl, blendIn) && slipSound != null)
        {
            audioSource.PlayOneShot(slipSound, volume);
        }
    }
}
