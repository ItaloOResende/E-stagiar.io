using UnityEngine;

/// <summary>
/// Estado inicial de um PC de estação (vai no <c>Gabinete</c>, junto do <see cref="ComputerPower"/>): ao começar a fase,
/// liga os cabos (energia do PC, energia do monitor, vídeo e, opcionalmente, rede) e aperta o Power — o PC já aparece
/// montado e ligado. Usa só a API pública do <see cref="ComputerPower"/> (as mesmas jogadas do jogador), então tudo
/// continua interativo: os cabos são itens reais (soltando as duas pontas o cabo volta para a mão) e o PC desliga se
/// algo for retirado.
/// Variante "com problema": <see cref="chanceCaboSolto"/> deixa cada cabo de fora com essa chance (o jogador pega
/// outro no gaveteiro); as peças sorteadas continuam vindo do <see cref="ComputerCase"/> (Randomize Components).
/// Sem requisitos (peça faltando, cabo solto), o Power não liga — o PC fica desligado, como na vida real.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(ComputerPower))]
public class PcEstadoInicial : MonoBehaviour
{
    [Header("Cabos já ligados ao começar")]
    [Tooltip("Pickup_CaboEnergia (usado nos dois cabos de energia: PC e monitor).")]
    [SerializeField] private GameObject caboEnergiaPrefab;
    [Tooltip("Cabo de vídeo (Pickup_CaboHDMI ou Pickup_CaboVideo). Vazio = sem vídeo (monitor mostra 'Sem sinal').")]
    [SerializeField] private GameObject caboVideoPrefab;
    [Tooltip("Pickup_CaboRede (opcional). Vazio = sem cabo de rede.")]
    [SerializeField] private GameObject caboRedePrefab;

    [Header("Problema sorteado")]
    [Tooltip("Chance (0 a 1) de cada cabo começar fora (não ligado). 0 = PC 100% montado.")]
    [Range(0f, 1f)] [SerializeField] private float chanceCaboSolto;
    [Tooltip("Semente do sorteio dos cabos (0 = aleatória a cada partida).")]
    [SerializeField] private int seed;

    [Header("Energia")]
    [Tooltip("Aperta o Power depois de ligar os cabos (só liga se tudo estiver certo).")]
    [SerializeField] private bool ligarAoIniciar = true;

    public float ChanceCaboSolto { get => chanceCaboSolto; set => chanceCaboSolto = Mathf.Clamp01(value); }
    public bool LigarAoIniciar { get => ligarAoIniciar; set => ligarAoIniciar = value; }

    /// <summary>Já aplicado (roda uma vez só).</summary>
    public bool Aplicado { get; private set; }

    // Start: o monitor e o ponto elétrico já se registraram no ComputerPower (OnEnable deles).
    private void Start() => Aplicar();

    /// <summary>Liga os cabos configurados e, se pedido, o PC. Roda uma vez (público para testes).</summary>
    public void Aplicar()
    {
        if (Aplicado) return;
        Aplicado = true;
        var power = GetComponent<ComputerPower>();
        var rng = seed != 0 ? new System.Random(seed) : new System.Random(Random.Range(1, int.MaxValue));

        Ligar(power, rng, caboEnergiaPrefab, PcPort.CasePower, PcPort.Outlet);
        Ligar(power, rng, caboEnergiaPrefab, PcPort.MonitorPower, PcPort.MonitorOutlet);
        if (caboVideoPrefab != null)
        {
            CableItem video = caboVideoPrefab.GetComponent<CableItem>();
            bool hdmi = video != null && video.Type == CableType.Hdmi;
            Ligar(power, rng, caboVideoPrefab, hdmi ? PcPort.CaseHdmi : PcPort.CaseVideo, hdmi ? PcPort.MonitorHdmi : PcPort.MonitorVideo);
        }
        Ligar(power, rng, caboRedePrefab, PcPort.CaseNetwork, PcPort.NetworkJack);

        if (ligarAoIniciar && power.State == PcPowerState.Off) power.PressPower();
    }

    // Cria o cabo, liga a ponta do equipamento "da mão" (o item fica guardado no PC, desativado, como o ItemHolder.Stow
    // faz) e depois a outra ponta.
    private void Ligar(ComputerPower power, System.Random rng, GameObject prefab, PcPort pontaEquipamento, PcPort outraPonta)
    {
        if (prefab == null) return;
        if (chanceCaboSolto > 0f && rng.NextDouble() < chanceCaboSolto) return;
        GameObject item = Instantiate(prefab, transform.position, transform.rotation);
        item.name = prefab.name;
        var cable = item.GetComponent<CableItem>();
        if (cable == null || power.UsePort(pontaEquipamento, cable, true, out _) != PortResult.PluggedFromHand)
        {
            Debug.LogWarning($"PcEstadoInicial: não foi possível ligar {prefab.name} em {pontaEquipamento}.", this);
            Destroy(item);
            return;
        }
        var pickup = item.GetComponent<PickupInteractable>();
        if (pickup != null) pickup.MarkInEquipment();
        item.SetActive(false);
        power.UsePort(outraPonta, null, false, out _);
    }
}
