using UnityEngine;

/// <summary>
/// Quedas de algo quebrável (<see cref="IBreakable"/>: peça <see cref="ComputerPartItem"/> ou equipamento
/// <see cref="BreakableEquipment"/>, como monitor e gabinete) no mundo. Vai no ROOT LÓGICO do item, junto do Rigidbody
/// e do componente que implementa <see cref="IBreakable"/>.
///
/// Queda = impacto físico REAL: colisão com velocidade relativa de pelo menos <see cref="minImpactSpeed"/>
/// (~2,2 m/s = cair de uns 25 cm). Por isso não contam: colocar a peça numa superfície (ela é apoiada 1 cm acima),
/// guardar/tirar da gaveta e estar na mão (cinemática, sem colisões), nem empurrar uma peça parada.
/// Uma queda conta UMA vez (os quiques seguintes não), até a peça ser pega de novo ou parar.
///
/// <see cref="dropsToBreak"/> &gt; 0: na N-ésima queda a peça quebra (troca para <see cref="brokenVisual"/> e toca um
/// estalo): placa-mãe e RAM = 1 (frágeis), HD, fonte e cooler = 2 (a 1ª queda só é registrada); monitor = 1 (tela),
/// gabinete = 2. 0 = nunca quebra.
/// <para>
/// Visual quebrado: com <see cref="intactVisual"/> e <see cref="brokenVisual"/> os dois se alternam (peças: malha
/// partida). Só com <see cref="brokenVisual"/> ele é uma CAMADA por cima do visual normal (equipamentos: rachaduras
/// sobre o modelo oficial, sem trocar a malha nem mexer nas âncoras de cabos).
/// </para>
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PartFallDamage : MonoBehaviour
{
    [Tooltip("Quedas até quebrar (placa-mãe, RAM e monitor: 1; HD, fonte, cooler e gabinete: 2). 0 = nunca quebra.")]
    [Min(0)] [SerializeField] private int dropsToBreak = 2;
    [Tooltip("Velocidade mínima do impacto (m/s) para contar como queda.")]
    [SerializeField] private float minImpactSpeed = 2.2f;
    [Tooltip("Visual inteiro (filho 'Visual'), trocado pelo quebrado. Vazio = o quebrado é só uma camada por cima.")]
    [SerializeField] private GameObject intactVisual;
    [Tooltip("Visual quebrado (filho desativado, ex.: 'Visual_Quebrado').")]
    [SerializeField] private GameObject brokenVisual;
    [Tooltip("Som da quebra. Vazio = um estalo curto gerado por código.")]
    [SerializeField] private AudioClip breakSound;

    private IBreakable part;
    private Rigidbody body;
    private bool countedThisFall;
    private float restingTime;

    public int DropsToBreak => dropsToBreak;

    private void Awake()
    {
        part = GetComponent<IBreakable>();
        body = GetComponent<Rigidbody>();
        if (part == null)
            Debug.LogWarning("PartFallDamage: falta um componente quebrável (ComputerPartItem ou BreakableEquipment) " +
                             "no mesmo objeto; as quedas não serão registradas.", this);
        ApplyVisual();
    }

    private void OnEnable() { if (part != null) part.BreakableChanged += OnConditionChanged; }
    private void OnDisable() { if (part != null) part.BreakableChanged -= OnConditionChanged; }

    private void OnCollisionEnter(Collision collision)
    {
        if (part == null || body.isKinematic || countedThisFall) return;
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

    private void OnConditionChanged(IBreakable p)
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
