using UnityEngine;

/// <summary>
/// Tela de um monitor no mundo 3D: mostra preto, "Sem sinal", BIOS ou sistema conforme o <see cref="ComputerPower"/>
/// ligado a ele (<see cref="ComputerPower.Screen"/>). Vai num objeto da tela do monitor (ex.: um Quad "Tela" filho do
/// monitor, com material URP/Unlit). Ao ser ativado, registra-se no PC (<see cref="ComputerPower.AttachMonitor"/>):
/// é por ele que o gabinete sabe qual é o SEU monitor e a que distância está (longe do gabinete o monitor não aparece na
/// tela de manutenção e as portas dele não são usadas; a imagem continua, ela só depende do cabo).
/// Só apresentação: troca a textura/cor do renderer por MaterialPropertyBlock (o material compartilhado não muda).
/// Monitor AVULSO (<see cref="connectWhenPlaced"/>, ex.: estoque da sala de TI): sem PC em <see cref="source"/>, passa a
/// ser o monitor do gabinete mais próximo que está sem monitor utilizável ao ser colocado perto dele (ver
/// <see cref="CanTake"/>); o monitor substituído fica avulso.
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
    [Tooltip("Monitor avulso (estoque): ao ser colocado perto de um gabinete sem monitor utilizável (nenhum, longe ou " +
             "quebrado, sem cabo na traseira do monitor), passa a ser o monitor dele. Monitores das estações: desligado.")]
    [SerializeField] private bool connectWhenPlaced;
    [Tooltip("Portas deste monitor para os cabos 3D (Z para fora). Vazio = as âncoras do PcCableView3D do gabinete.")]
    [SerializeField] private PcCableView3D.PortAnchor[] portAnchors = new PcCableView3D.PortAnchor[0];

    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private MaterialPropertyBlock block;

    public ComputerPower Source => source;
    public bool ConnectWhenPlaced => connectWhenPlaced;

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
        Bind();
        Refresh();
    }

    private void OnDisable()
    {
        Unbind();
    }

    private void Bind()
    {
        if (source == null) return;
        source.AttachMonitor(transform);
        source.PowerStateChanged += Show;
        source.ConnectionsChanged += Refresh;
        source.DisplayChanged += Refresh;
    }

    private void Unbind()
    {
        if (source == null) return;
        source.DetachMonitor(transform);
        source.PowerStateChanged -= Show;
        source.ConnectionsChanged -= Refresh;
        source.DisplayChanged -= Refresh;
    }

    /// <summary>Troca o PC deste monitor (null = avulso). O PC novo passa a usar este monitor.</summary>
    public void SetSource(ComputerPower pc)
    {
        if (pc == source) return;
        if (isActiveAndEnabled) Unbind();
        source = pc;
        if (isActiveAndEnabled) Bind();
        Refresh();
    }

    /// <summary>Âncora 3D da porta deste monitor (null = não configurada).</summary>
    public Transform AnchorFor(PcPort port)
    {
        foreach (PcCableView3D.PortAnchor a in portAnchors)
            if (a != null && a.port == port && a.anchor != null) return a.anchor;
        return null;
    }

    /// <summary>
    /// O PC pode receber outro monitor: não tem monitor, o dele está longe do gabinete ou quebrado, e não há nenhum
    /// cabo ligado do lado do monitor (energia, VGA ou HDMI); o estado desses cabos não passa de um monitor para outro.
    /// </summary>
    public static bool CanTake(ComputerPower pc)
    {
        if (pc == null) return false;
        if (pc.Monitor != null && pc.MonitorInRange && !pc.MonitorBroken) return false;
        foreach (PcLink link in new[] { PcLink.MonitorPower, PcLink.Vga, PcLink.Hdmi })
            if (pc.IsEndPlugged(link, false)) return false;
        return true;
    }

    /// <summary>
    /// Monitor avulso colocado (chamado pelo MonitorAvulso no root pegável do monitor, ou direto em testes): vira o monitor do gabinete mais próximo que o aceite
    /// (<see cref="CanTake"/>, dentro do <see cref="ComputerPower.MonitorRange"/> dele). Devolve o PC escolhido ou null.
    /// </summary>
    public ComputerPower TryConnectToNearby()
    {
        if (!connectWhenPlaced) return null;
        if (source != null && source.Monitor == transform && source.MonitorInRange) return null;   // continua no seu PC
        ComputerPower best = null;
        float bestDist = float.MaxValue;
        foreach (Collider c in Physics.OverlapSphere(transform.position, 3f, ~0, QueryTriggerInteraction.Ignore))
        {
            ComputerPower pc = c.GetComponentInParent<ComputerPower>();
            if (pc == null || pc == source || !CanTake(pc)) continue;
            float d = (pc.transform.position - transform.position).sqrMagnitude;
            if (d <= pc.MonitorRange * pc.MonitorRange && d < bestDist) { bestDist = d; best = pc; }
        }
        if (best == null) return null;
        MonitorDisplay old = best.Monitor != null ? best.Monitor.GetComponent<MonitorDisplay>() : null;
        if (old != null && old != this)
        {
            old.SetSource(null);
            old.connectWhenPlaced = true;   // o substituído vira avulso (pode ir para outra estação)
        }
        SetSource(best);
        return best;
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
