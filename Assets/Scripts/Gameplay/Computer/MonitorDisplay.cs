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
    [Tooltip("BIOS sem nenhum HD.")]
    [SerializeField] private Texture biosTexture;
    [Tooltip("BIOS com HD sem sistema ('No bootable device'). Vazio = usa a BIOS.")]
    [SerializeField] private Texture noBootableTexture;
    [Tooltip("Instalação pelo pendrive: uma textura por etapa. Vazio = usa a BIOS.")]
    [SerializeField] private Texture[] installTextures = new Texture[0];
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
            source.DisplayChanged += Refresh;
        }
        Refresh();
    }

    private void OnDisable()
    {
        if (source == null) return;
        source.PowerStateChanged -= Show;
        source.ConnectionsChanged -= Refresh;
        source.DisplayChanged -= Refresh;
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
        if (state == PcPowerState.Bios && source != null)
        {
            if (source.Screen == PcScreen.NoBootableDevice && noBootableTexture != null) tex = noBootableTexture;
            else if (source.Screen == PcScreen.Installing && installTextures.Length > 0)
                tex = installTextures[Mathf.Clamp(source.InstallStage, 0, installTextures.Length - 1)];
        }
        block.SetTexture(BaseMapId, tex != null ? tex : Texture2D.whiteTexture);
        block.SetColor(BaseColorId, tex != null ? Color.white : offColor);
        screenRenderer.SetPropertyBlock(block);
    }
}
