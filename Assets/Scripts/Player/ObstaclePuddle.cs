using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider), typeof(Rigidbody), typeof(AudioSource))]
public class ObstaclePuddle : MonoBehaviour
{
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
        if (!isActiveAndEnabled) return;
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
