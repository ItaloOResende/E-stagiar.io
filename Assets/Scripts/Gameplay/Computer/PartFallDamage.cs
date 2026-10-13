using UnityEngine;

/// <summary>
/// Quedas de uma peça (<see cref="ComputerPartItem"/>) no mundo. Vai no ROOT LÓGICO do item, junto do Rigidbody.
///
/// Queda = impacto físico REAL: colisão com velocidade relativa de pelo menos <see cref="minImpactSpeed"/>
/// (~2,2 m/s = cair de uns 25 cm). Por isso não contam: colocar a peça numa superfície (ela é apoiada 1 cm acima),
/// guardar/tirar da gaveta e estar na mão (cinemática, sem colisões), nem empurrar uma peça parada.
/// Uma queda conta UMA vez (os quiques seguintes não), até a peça ser pega de novo ou parar.
///
/// <see cref="dropsToBreak"/> &gt; 0: na N-ésima queda a peça quebra (troca para <see cref="brokenVisual"/> e toca um
/// estalo): placa-mãe e RAM = 1 (frágeis), HD, fonte e cooler = 2 (a 1ª queda só é registrada). 0 = nunca quebra.
/// </summary>
[RequireComponent(typeof(ComputerPartItem), typeof(Rigidbody))]
public class PartFallDamage : MonoBehaviour
{
    [Tooltip("Quedas até quebrar (placa-mãe e RAM: 1; HD, fonte e cooler: 2). 0 = nunca quebra.")]
    [Min(0)] [SerializeField] private int dropsToBreak = 2;
    [Tooltip("Velocidade mínima do impacto (m/s) para contar como queda.")]
    [SerializeField] private float minImpactSpeed = 2.2f;
    [Tooltip("Visual da peça inteira (filho 'Visual').")]
    [SerializeField] private GameObject intactVisual;
    [Tooltip("Visual da peça quebrada (filho desativado, ex.: 'Visual_Quebrado').")]
    [SerializeField] private GameObject brokenVisual;
    [Tooltip("Som da quebra. Vazio = um estalo curto gerado por código.")]
    [SerializeField] private AudioClip breakSound;

    private ComputerPartItem part;
    private Rigidbody body;
    private bool countedThisFall;
    private float restingTime;

    public int DropsToBreak => dropsToBreak;

    private void Awake()
    {
        part = GetComponent<ComputerPartItem>();
        body = GetComponent<Rigidbody>();
        ApplyVisual();
    }

    private void OnEnable() => part.ConditionChanged += OnConditionChanged;
    private void OnDisable() => part.ConditionChanged -= OnConditionChanged;

    private void OnCollisionEnter(Collision collision)
    {
        if (body.isKinematic || countedThisFall) return;
        if (collision.relativeVelocity.magnitude < minImpactSpeed) return;
        countedThisFall = true;
        restingTime = 0f;
        part.RegisterDrop(dropsToBreak);
    }

    private void FixedUpdate()
    {
        // nova queda só depois de a peça ser pega/guardada (cinemática) ou ficar parada um instante
        if (!countedThisFall) return;
        if (body.isKinematic) { countedThisFall = false; return; }
        restingTime = body.linearVelocity.sqrMagnitude < 0.01f ? restingTime + Time.fixedDeltaTime : 0f;
        if (restingTime > 0.3f) countedThisFall = false;
    }

    private void OnConditionChanged(ComputerPartItem p)
    {
        ApplyVisual();
        if (p.IsBroken) PlayBreakSound();
    }

    private void ApplyVisual()
    {
        bool broken = part != null && part.IsBroken;
        if (brokenVisual != null && brokenVisual.activeSelf != broken) brokenVisual.SetActive(broken);
        if (intactVisual != null && brokenVisual != null && intactVisual.activeSelf == broken) intactVisual.SetActive(!broken);
    }

    private void PlayBreakSound()
    {
        AudioClip clip = breakSound != null ? breakSound : CrackClip();
        AudioSource.PlayClipAtPoint(clip, transform.position, 0.8f);
    }

    private static AudioClip crack;

    // Estalo curto (ruído com decaimento rápido + segundo estalo), gerado uma vez; sem asset de áudio no projeto.
    private static AudioClip CrackClip()
    {
        if (crack != null) return crack;
        const int rate = 22050;
        int n = rate / 4;
        var data = new float[n];
        var rnd = new System.Random(7);
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float env = Mathf.Exp(-t * 45f) + 0.6f * (t > 0.05f ? Mathf.Exp(-(t - 0.05f) * 60f) : 0f);
            data[i] = ((float)rnd.NextDouble() * 2f - 1f) * env * 0.7f;
        }
        crack = AudioClip.Create("estalo_quebra", n, 1, rate, false);
        crack.SetData(data, 0);
        return crack;
    }
}
