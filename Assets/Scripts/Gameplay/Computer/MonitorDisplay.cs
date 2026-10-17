using UnityEngine;

/// <summary>
/// Tela de um monitor no mundo 3D: mostra preto, "Sem sinal", BIOS ou sistema conforme o <see cref="ComputerPower"/>
/// ligado a ele (<see cref="ComputerPower.Screen"/>). Vai num objeto da tela do monitor (ex.: um Quad "Tela" filho do
/// monitor, com material URP/Unlit). Ao ser ativado, registra-se no PC (<see cref="ComputerPower.AttachMonitor"/>):
/// é por ele que o gabinete sabe qual é o SEU monitor e a que distância está (longe do gabinete o monitor não aparece na
/// tela de manutenção e as portas dele não são usadas; a imagem continua, ela só depende do cabo).
/// Só apresentação: troca a textura/cor do renderer por MaterialPropertyBlock (o material compartilhado não muda).
/// </summary>
public class MonitorDisplay : MonoBehaviour, IPickupBlocker
{
    [Tooltip("PC (root lógico do gabinete) que alimenta este monitor.")]
    [SerializeField] private ComputerPower source;
    [Tooltip("Renderer da tela. Vazio = o deste objeto.")]
    [SerializeField] private Renderer screenRenderer;
    [Tooltip("BIOS sem nenhum HD.")]
    [SerializeField] private Texture biosTexture;
    [Tooltip("BIOS com HD sem sistema ('No bootable device'). Vazio = usa a BIOS.")]
    [SerializeField] private Texture noBootableTexture;
    [Tooltip("Instalação pelo pendrive: uma textura por etapa. Vazio = usa a BIOS.")]
    [SerializeField] private Texture[] installTextures = new Texture[0];
    [SerializeField] private Texture operatingSystemTexture;
    [Tooltip("Sistema com o cabo de rede ligado. Vazio = usa a textura do sistema.")]
    [SerializeField] private Texture onlineTexture;
    [Tooltip("PC ligado sem sinal de vídeo (sem cabo VGA/HDMI ou monitor longe do gabinete). Vazio = tela preta.")]
    [SerializeField] private Texture noSignalTexture;
    [Tooltip("Cor da tela desligada.")]
    [SerializeField] private Color offColor = Color.black;

    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private MaterialPropertyBlock block;

    public ComputerPower Source => source;

    /// <summary>
    /// O monitor não é carregado com cabo de energia na tomada ou vídeo ligado ao gabinete
    /// (<see cref="ComputerPower.MonitorTetheredLinks"/>). Fica no filho da tela; o <c>PickupInteractable</c> do monitor
    /// consulta os bloqueadores dos filhos.
    /// </summary>
    public bool BlocksPickup => source != null && source.MonitorTetheredLinks().Count > 0;
    public string BlockReason => source != null ? source.MonitorBlockReason : "";

    private void Awake()
    {
        if (screenRenderer == null) screenRenderer = GetComponent<Renderer>();
    }

    private void OnEnable()
    {
        if (source != null)
        {
            source.AttachMonitor(transform);
            source.PowerStateChanged += Show;
            source.ConnectionsChanged += Refresh;
            source.DisplayChanged += Refresh;
        }
        Refresh();
    }

    private void OnDisable()
    {
        if (source == null) return;
        source.DetachMonitor(transform);
        source.PowerStateChanged -= Show;
        source.ConnectionsChanged -= Refresh;
        source.DisplayChanged -= Refresh;
    }

    private void Refresh() => Show(source != null ? source.State : PcPowerState.Off);

    /// <summary>Atualiza a tela (o que aparece vem de <see cref="ComputerPower.Screen"/>; o estado dado só importa sem PC).</summary>
    public void Show(PcPowerState state)
    {
        if (screenRenderer == null) return;
        block ??= new MaterialPropertyBlock();
        screenRenderer.GetPropertyBlock(block);

        Texture tex = null;
        switch (source != null ? source.Screen : PcScreen.Off)
        {
            case PcScreen.NoSignal: tex = noSignalTexture; break;
            case PcScreen.NoStorage: tex = biosTexture; break;
            case PcScreen.NoBootableDevice: tex = noBootableTexture != null ? noBootableTexture : biosTexture; break;
            case PcScreen.Installing:
                tex = installTextures.Length > 0 ? installTextures[Mathf.Clamp(source.InstallStage, 0, installTextures.Length - 1)] : biosTexture;
                break;
            case PcScreen.OperatingSystem:
                tex = onlineTexture != null && source.NetworkConnected ? onlineTexture : operatingSystemTexture;
                break;
        }
        block.SetTexture(BaseMapId, tex != null ? tex : Texture2D.whiteTexture);
        block.SetColor(BaseColorId, tex != null ? Color.white : offColor);
        screenRenderer.SetPropertyBlock(block);
    }
}
