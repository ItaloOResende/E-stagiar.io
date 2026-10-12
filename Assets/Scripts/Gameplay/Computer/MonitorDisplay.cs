using UnityEngine;

/// <summary>
/// Tela de um monitor no mundo 3D: mostra preto, BIOS ou sistema conforme o <see cref="ComputerPower"/> ligado a ele.
/// Vai num objeto da tela do monitor (ex.: um Quad "Tela" filho do monitor, com material URP/Unlit).
/// Só apresentação: troca a textura/cor do renderer por MaterialPropertyBlock (o material compartilhado não muda).
/// </summary>
public class MonitorDisplay : MonoBehaviour
{
    [Tooltip("PC (root lógico do gabinete) que alimenta este monitor.")]
    [SerializeField] private ComputerPower source;
    [Tooltip("Renderer da tela. Vazio = o deste objeto.")]
    [SerializeField] private Renderer screenRenderer;
    [SerializeField] private Texture biosTexture;
    [SerializeField] private Texture operatingSystemTexture;
    [Tooltip("Sistema com o cabo de rede ligado. Vazio = usa a textura do sistema.")]
    [SerializeField] private Texture onlineTexture;
    [Tooltip("Cor da tela desligada.")]
    [SerializeField] private Color offColor = Color.black;

    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private MaterialPropertyBlock block;

    public ComputerPower Source => source;

    private void Awake()
    {
        if (screenRenderer == null) screenRenderer = GetComponent<Renderer>();
    }

    private void OnEnable()
    {
        if (source != null)
        {
            source.PowerStateChanged += Show;
            source.ConnectionsChanged += Refresh;
        }
        Refresh();
    }

    private void OnDisable()
    {
        if (source == null) return;
        source.PowerStateChanged -= Show;
        source.ConnectionsChanged -= Refresh;
    }

    private void Refresh() => Show(source != null ? source.State : PcPowerState.Off);

    /// <summary>Atualiza a tela para o estado dado.</summary>
    public void Show(PcPowerState state)
    {
        if (screenRenderer == null) return;
        block ??= new MaterialPropertyBlock();
        screenRenderer.GetPropertyBlock(block);

        Texture tex = state == PcPowerState.Bios ? biosTexture : state == PcPowerState.OperatingSystem ? operatingSystemTexture : null;
        if (state == PcPowerState.OperatingSystem && onlineTexture != null && source != null && source.NetworkConnected) tex = onlineTexture;
        block.SetTexture(BaseMapId, tex != null ? tex : Texture2D.whiteTexture);
        block.SetColor(BaseColorId, tex != null ? Color.white : offColor);
        screenRenderer.SetPropertyBlock(block);
    }
}
