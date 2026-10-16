using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public enum ComputerView { Front, Rear, Interior }

[RequireComponent(typeof(UIScreen))]
public class ComputerScreenController : MonoBehaviour
{
    [Header("Vistas (cada uma ocupa a tela inteira)")]
    [SerializeField] private GameObject frontView;
    [Tooltip("Vista da traseira com os parafusos.")]
    [SerializeField] private GameObject rearView;
    [Tooltip("Vista do interior (só depois de remover os parafusos obrigatórios).")]
    [SerializeField] private GameObject interiorView;

    [Header("Parafusos")]
    [Tooltip("Contêiner com a MESMA proporção da imagem da traseira; os parafusos são ancorados nele (0–1).")]
    [SerializeField] private RectTransform screwLayer;
    [SerializeField] private ScrewView screwPrefab;

    [Header("Componentes internos (presença vem do ComputerCase)")]
    [SerializeField] private GameObject motherboardView;
    [SerializeField] private GameObject ramView;
    [SerializeField] private GameObject hddView;
    [SerializeField] private GameObject psuView;
    [SerializeField] private GameObject coolerView;

    [Header("Energia e monitor (opcional: exige ComputerPower no gabinete)")]
    [SerializeField] private Button powerButton;
    [Tooltip("LED aceso do botão Power (sobre o desenho apagado); só aparece com o PC ligado.")]
    [SerializeField] private GameObject powerLed;
    [Tooltip("Imagem sobre a área da tela do monitor, na vista da frente.")]
    [SerializeField] private Image monitorScreen;
    [Tooltip("BIOS sem nenhum HD (nenhum dispositivo de armazenamento).")]
    [SerializeField] private Sprite biosScreen;
    [Tooltip("BIOS com HD detectado mas sem sistema ('No bootable device found'). Vazio = usa a BIOS.")]
    [SerializeField] private Sprite noBootableScreen;
    [Tooltip("Instalação do sistema pelo pendrive: uma imagem por etapa (ComputerPower.InstallStages).")]
    [SerializeField] private Sprite[] installScreens = new Sprite[0];
    [SerializeField] private Sprite operatingSystemScreen;
    [Tooltip("Sistema operacional com o cabo de rede ligado. Vazio = usa a tela do sistema.")]
    [SerializeField] private Sprite onlineScreen;
    [Tooltip("PC ligado sem sinal de vídeo (sem cabo VGA/HDMI nas duas pontas ou monitor longe). Vazio = tela preta.")]
    [SerializeField] private Sprite noSignalScreen;
    [SerializeField] private Color screenOffColor = Color.black;

    [Header("Monitor (perto do gabinete: ComputerPower.MonitorInRange)")]
    [Tooltip("Partes da tela que mostram o monitor (Frente/Monitor, Traseira/Monitor). Somem com o monitor longe do gabinete.")]
    [SerializeField] private List<GameObject> monitorViews = new List<GameObject>();

    [Header("Lado de cada equipamento (segue a posição física do monitor e do gabinete)")]
    [Tooltip("Partes que trocam de lado (espelhadas no eixo X, a imagem não vira) quando, no mundo, o gabinete está do " +
             "outro lado do monitor: Frente/Monitor e Frente/Gabinete, Traseira/Monitor, Traseira/ContainerTraseira_Area e " +
             "Traseira/PainelConexoes (o ponto elétrico acompanha o lado do gabinete). O layout do prefab é o da estação " +
             "padrão (vista de frente: gabinete à esquerda do monitor).")]
    [SerializeField] private List<RectTransform> sideViews = new List<RectTransform>();
    [Tooltip("Eixo (0-1, horizontal) do espelhamento de cada item de 'Side Views' (mesma ordem). Faltando: 'Mirror Center X'.")]
    [SerializeField] private List<float> sideMirrorCenters = new List<float>();
    [Tooltip("Eixo padrão do espelhamento.")]
    [Range(0f, 1f)] [SerializeField] private float mirrorCenterX = 0.5f;

    [Header("Ponto elétrico (perto do gabinete: ComputerPower.IsWallPointInRange)")]
    [Tooltip("Painel das tomadas e do ponto de rede (Traseira/PainelConexoes). Some com o gabinete longe do ponto elétrico " +
             "(Case Range no WallConnectionPoint).")]
    [SerializeField] private List<GameObject> wallViews = new List<GameObject>();

    [Header("Cabos (traseira)")]
    [SerializeField] private List<ComputerPortView> ports = new List<ComputerPortView>();
    [Tooltip("Uma faixa por cabo (energia do PC, energia do monitor, VGA, HDMI, rede); cada uma diz o seu cabo.")]
    [SerializeField] private List<CableLineView> cableLines = new List<CableLineView>();
    [Tooltip("Portas USB da Traseira (pendrive).")]
    [SerializeField] private List<UsbPortView> usbPorts = new List<UsbPortView>();

    [Header("Encaixes de peças (interior)")]
    [SerializeField] private List<ComputerSlotView> slots = new List<ComputerSlotView>();

    [Header("Navegação")]
    [SerializeField] private Button backButton;
    [SerializeField] private Button nextButton;

    [Header("Abertura do gabinete (Avançar na Traseira)")]
    [Tooltip("Opacidade do Avançar na Traseira enquanto a tampa não pode abrir (parafusos obrigatórios no lugar ou " +
             "gabinete fora de uma superfície de trabalho). Continua clicável: balança, sem texto.")]
    [Range(0f, 1f)] [SerializeField] private float lockedAlpha = 0.4f;

    [Header("Cursores")]
    [Tooltip("Mãozinha, usada só se a tela não tiver UIScreenManager com cursor padrão (o normal é o do gerenciador).")]
    [SerializeField] private Texture2D handCursor;
    [SerializeField] private Vector2 handCursorHotspot;
    [SerializeField] private int handCursorHeight = 48;

    [Tooltip("Cursor de chave de fenda: só na Traseira e só com a chave de fenda na mão do jogador.")]
    [SerializeField] private Texture2D rearCursor;
    [Tooltip("Ponta da chave, em pixels da textura (origem no canto superior esquerdo).")]
    [SerializeField] private Vector2 rearCursorHotspot;
    [Tooltip("Altura do cursor de chave de fenda em relação ao parafuso na tela (ex.: 4 = 4x o diâmetro do parafuso).")]
    [SerializeField] private float screwdriverToScrewRatio = 4f;

    private UIScreen screen;
    private ComputerCase boundCase;
    private ComputerPower boundPower;
    private bool subscribed;
    private ComputerView current = ComputerView.Front;
    // Cabo cuja 1ª ponta acabou de ser conectada: a ponta livre segue o mouse até a outra porta.
    private PcLink? carriedLink;
    private readonly List<ScrewView> views = new List<ScrewView>();
    // Última vista de cada gabinete: ao reabrir a tela, volta onde o jogador parou (só durante a execução).
    private readonly Dictionary<ComputerCase, ComputerView> lastViews = new Dictionary<ComputerCase, ComputerView>();
    private CanvasGroup nextGroup;
    private RectTransform nextRect;
    private Vector2 nextRestPosition;
    private bool nextLocked;
    private Coroutine nextRoutine;
    private readonly List<(Vector2 min, Vector2 max)> sideAnchors = new List<(Vector2, Vector2)>();
    private bool mirrored;

    public ComputerView CurrentView => current;
    /// <summary>
    /// O Avançar está bloqueado agora (na Traseira, gabinete fechado e a tampa não pode abrir: faltam parafusos
    /// obrigatórios ou o gabinete não está numa superfície de trabalho).
    /// </summary>
    public bool NextLocked => nextLocked;

    // Interior bloqueado: o gabinete está fechado e a tampa não pode abrir agora (estado real dos parafusos e do apoio).
    private bool InteriorLocked => boundCase != null && boundCase.State == CaseState.Closed && !boundCase.CanOpen;

    /// <summary>O monitor deste PC aparece na tela agora (perto do gabinete; a distância do jogador não importa).</summary>
    public bool MonitorVisible => boundPower == null || boundPower.MonitorAvailable;   // perto e não quebrado

    /// <summary>Vista de frente (pelo lado da tela do monitor): o gabinete está à esquerda do monitor no mundo.</summary>
    public bool CaseLeftOfMonitor
    {
        get
        {
            if (boundPower == null || boundPower.Monitor == null) return true;
            Transform mon = boundPower.Monitor.parent != null ? boundPower.Monitor.parent : boundPower.Monitor;
            // olhando a tela de frente, a direita de quem olha é -right do monitor
            return Vector3.Dot(boundCase.transform.position - mon.position, mon.right) >= 0f;
        }
    }

    /// <summary>A tela está espelhada agora (gabinete à direita do monitor, vista de frente).</summary>
    public bool Mirrored => mirrored;

    private void ApplySides()
    {
        mirrored = !CaseLeftOfMonitor;
        for (int i = 0; i < sideViews.Count; i++)
        {
            RectTransform r = sideViews[i];
            if (r == null) continue;
            (Vector2 min, Vector2 max) = sideAnchors[i];
            float c = i < sideMirrorCenters.Count ? sideMirrorCenters[i] : mirrorCenterX;
            if (mirrored) { float a = 2f * c - max.x, b = 2f * c - min.x; min.x = a; max.x = b; }
            r.anchorMin = min;
            r.anchorMax = max;
        }
    }

    /// <summary>O painel do ponto elétrico (tomadas e rede) aparece na tela agora (ponto perto do gabinete deste PC).</summary>
    public bool WallPointVisible => boundPower == null || boundPower.IsWallPointInRange;

    private void Awake()
    {
        screen = GetComponent<UIScreen>();
        if (backButton != null) backButton.onClick.AddListener(Back);
        if (nextButton != null)
        {
            nextButton.onClick.AddListener(Next);
            nextGroup = nextButton.GetComponent<CanvasGroup>();
            if (nextGroup == null) nextGroup = nextButton.gameObject.AddComponent<CanvasGroup>();
            nextRect = (RectTransform)nextButton.transform;
            nextRestPosition = nextRect.anchoredPosition;
        }
        foreach (RectTransform r in sideViews) sideAnchors.Add(r != null ? (r.anchorMin, r.anchorMax) : (Vector2.zero, Vector2.zero));
        if (powerButton != null) powerButton.onClick.AddListener(PressPower);
        foreach (ComputerPortView p in ports) if (p != null) p.Init(OnPortClicked);
        foreach (ComputerSlotView v in slots) if (v != null) v.Init(OnSlotClicked);
        foreach (UsbPortView u in usbPorts) if (u != null) u.Init(OnUsbClicked);
    }

    private void OnEnable()
    {
        ComputerCase found = screen.Source != null ? screen.Source.GetComponentInParent<ComputerCase>() : null;
        if (found == null)
        {
            Debug.LogError("ComputerScreenController: o objeto que abriu a tela não tem ComputerCase.", this);
            return;
        }

        if (found != boundCase)
        {
            Unsubscribe();
            boundCase = found;
            boundPower = found.GetComponent<ComputerPower>();
            BuildScrewViews();
        }
        Subscribe();
        // Interior só se a tampa ainda estiver aberta (ShowView cai para a Traseira se não estiver).
        ShowView(lastViews.TryGetValue(found, out ComputerView last) ? last : ComputerView.Front);
    }

    private void OnDisable()
    {
        if (boundPower != null) boundPower.InstallPaused = false;   // tela fechada: segue no monitor 3D
        Unsubscribe();
        UICursor.Reset();
        nextRoutine = null;                                        // corrotinas param junto com o objeto
        if (nextRect != null) { nextRect.anchoredPosition = nextRestPosition; nextRect.localScale = Vector3.one; }
    }

    /// <summary>As setas do teclado fazem o MESMO que os botões ◄ ► da tela (Voltar/Avançar).</summary>
    private void Update()
    {
        Keyboard teclado = Keyboard.current;
        if (teclado == null || boundCase == null) return;
        if (teclado.leftArrowKey.wasPressedThisFrame) Back();
        else if (teclado.rightArrowKey.wasPressedThisFrame) Next();
    }

    private void Subscribe()
    {
        if (subscribed || boundCase == null) return;
        boundCase.ScrewRemoved += HandleScrewRemoved;
        boundCase.ScrewInstalled += HandleScrewInstalled;
        boundCase.StateChanged += HandleCaseState;
        boundCase.ComponentsChanged += RefreshHardware;
        if (boundPower != null)
        {
            boundPower.PowerStateChanged += HandlePowerState;
            boundPower.ConnectionsChanged += RefreshHardware;
            boundPower.DisplayChanged += RefreshScreen;
        }
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed || boundCase == null) { subscribed = false; return; }
        boundCase.ScrewRemoved -= HandleScrewRemoved;
        boundCase.ScrewInstalled -= HandleScrewInstalled;
        boundCase.StateChanged -= HandleCaseState;
        boundCase.ComponentsChanged -= RefreshHardware;
        if (boundPower != null)
        {
            boundPower.PowerStateChanged -= HandlePowerState;
            boundPower.ConnectionsChanged -= RefreshHardware;
            boundPower.DisplayChanged -= RefreshScreen;
        }
        subscribed = false;
    }

    private void BuildScrewViews()
    {
        foreach (ScrewView v in views)
            if (v != null) Destroy(v.gameObject);
        views.Clear();

        if (screwPrefab == null || screwLayer == null) return;

        for (int i = 0; i < boundCase.Screws.Count; i++)
        {
            Vector2 p = boundCase.Screws[i].normalizedPosition;
            ScrewView view = Instantiate(screwPrefab, screwLayer, false);
            view.name = "Parafuso_" + boundCase.Screws[i].id;
            var rt = (RectTransform)view.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(p.x, 1f - p.y);
            rt.anchoredPosition = Vector2.zero;
            view.Init(i, OnScrewClicked);
            views.Add(view);
        }
    }

    public void Next()
    {
        if (boundCase == null) return;
        switch (current)
        {
            case ComputerView.Front:
                ShowView(ComputerView.Rear);
                break;
            case ComputerView.Rear:
                // Bloqueado (parafusos ou apoio): não abre; só o Avançar balança, sem texto e sem apontar a solução.
                CaseOpenResult result = boundCase.TryOpen();
                if (result == CaseOpenResult.ScrewsRemaining || result == CaseOpenResult.NotOnWorkSurface) ShowLockedFeedback();
                else
                {
                    if (result == CaseOpenResult.Opened) InteractionSounds.Raise(InteractionSound.CaseOpened);
                    ShowView(ComputerView.Interior);
                }
                break;
        }
    }

    private void ShowLockedFeedback()
    {
        if (nextRect != null && isActiveAndEnabled)
        {
            if (nextRoutine != null) StopCoroutine(nextRoutine);
            nextRoutine = StartCoroutine(ShakeNext());
        }
    }

    /// <summary>Atualiza a aparência do Avançar (opacidade reduzida quando bloqueado); devolve se acabou de desbloquear.</summary>
    private bool UpdateNextLock()
    {
        bool was = nextLocked;
        nextLocked = current == ComputerView.Rear && InteriorLocked;
        if (nextGroup != null && (nextRoutine == null || nextLocked)) nextGroup.alpha = nextLocked ? lockedAlpha : 1f;
        return was && !nextLocked;
    }

    // Balança o Avançar bloqueado (recusa), como o parafuso com a ferramenta errada.
    private IEnumerator ShakeNext()
    {
        for (float t = 0f; t < 0.35f; t += Time.unscaledDeltaTime)
        {
            nextRect.anchoredPosition = nextRestPosition + Vector2.right * Mathf.Sin(t * 60f) * 10f * (1f - t / 0.35f);
            yield return null;
        }
        nextRect.anchoredPosition = nextRestPosition;
        nextRoutine = null;
    }

    // Desbloqueio: o Avançar volta à opacidade total e "pulsa" (cresce e volta).
    private IEnumerator PulseUnlocked()
    {
        nextRect.anchoredPosition = nextRestPosition;
        for (float t = 0f; t < 0.6f; t += Time.unscaledDeltaTime)
        {
            float k = t / 0.6f;
            if (nextGroup != null) nextGroup.alpha = Mathf.Lerp(lockedAlpha, 1f, Mathf.Clamp01(k * 2f));
            nextRect.localScale = Vector3.one * (1f + 0.25f * Mathf.Sin(k * Mathf.PI));
            yield return null;
        }
        nextRect.localScale = Vector3.one;
        if (nextGroup != null) nextGroup.alpha = 1f;
        nextRoutine = null;
    }

    public void Back()
    {
        if (boundCase == null) return;
        if (current == ComputerView.Interior) ShowView(ComputerView.Rear);
        else if (current == ComputerView.Rear) ShowView(ComputerView.Front);
    }

    private void ShowView(ComputerView view)
    {
        if (view == ComputerView.Interior && boundCase.State != CaseState.Open) view = ComputerView.Rear;
        current = view;
        lastViews[boundCase] = view;
        if (boundPower != null) boundPower.InstallPaused = view != ComputerView.Front;

        if (frontView != null) frontView.SetActive(view == ComputerView.Front);
        if (rearView != null) rearView.SetActive(view == ComputerView.Rear);
        if (interiorView != null) interiorView.SetActive(view == ComputerView.Interior);
        if (backButton != null) backButton.interactable = view != ComputerView.Front;
        if (nextButton != null) nextButton.interactable = view != ComputerView.Interior;
        UpdateNextLock();

        // Aplica o cursor correspondente à vista atual (já redimensionado)
        ApplyViewCursor(view);

        for (int i = 0; i < views.Count; i++)
            views[i].SetRemoved(boundCase.Screws[i].removed, false);
        RefreshHardware();
    }

    /// <summary>Peças do interior, portas/cabos e tela do monitor, conforme o estado do objeto do mundo.</summary>
    private void RefreshHardware()
    {
        if (boundCase == null) return;
        if (motherboardView != null) motherboardView.SetActive(boundCase.HasComponent(PcComponent.Motherboard));
        if (ramView != null) ramView.SetActive(boundCase.HasComponent(PcComponent.Ram));
        if (hddView != null) hddView.SetActive(boundCase.HasComponent(PcComponent.Hdd));
        if (psuView != null) psuView.SetActive(boundCase.HasComponent(PcComponent.Psu));
        if (coolerView != null) coolerView.SetActive(boundCase.HasComponent(PcComponent.CpuCooler));
        foreach (ComputerSlotView v in slots)
        {
            if (v == null) continue;
            v.SetAvailable(boundCase.IsSlotAvailable(v.Slot) || boundCase.HasComponent(v.Slot));
            v.SetInstalled(boundCase.HasComponent(v.Slot));
        }

        ApplySides();   // monitor e gabinete no lado em que estão no mundo (atualiza ao abrir e ao mudarem de lugar)

        // monitor longe do gabinete ou do jogador / ponto elétrico longe do jogador: some da tela (e as portas dele
        // deixam de ser clicáveis; o clique também é validado em OnPortClicked)
        bool monitorNear = MonitorVisible;
        foreach (GameObject m in monitorViews)
            if (m != null && m.activeSelf != monitorNear) m.SetActive(monitorNear);
        bool wallNear = WallPointVisible;
        foreach (GameObject w in wallViews)
            if (w != null && w.activeSelf != wallNear) w.SetActive(wallNear);

        foreach (UsbPortView u in usbPorts)
            if (u != null) u.SetPlugged(boundPower != null && boundPower.PendrivePort == u.Index);
        foreach (ComputerPortView p in ports)
            if (p != null) p.SetPlugged(boundPower != null && boundPower.IsPlugged(p.Port));
        foreach (CableLineView line in cableLines)
        {
            if (line == null) continue;
            bool a = false, b = false, toMonitor = false, fromWall = false;
            if (boundPower != null)
            {
                // cabo de energia: a ponta A segue a tomada em que foi ligado (qualquer uma das duas)
                boundPower.PortsOf(line.Link, out PcPort pa, out PcPort pb);
                a = boundPower.IsEndPlugged(line.Link, true);
                b = boundPower.IsEndPlugged(line.Link, false);
                line.SetFrom(PortRect(pa));
                toMonitor = ComputerPower.IsMonitorPort(pb);
                fromWall = ComputerPower.IsWallPort(pa);
            }
            if (a == b && carriedLink == line.Link) carriedLink = null;  // completo ou solto: ninguém está levando
            bool visible = current == ComputerView.Rear && (monitorNear || !toMonitor) && (wallNear || !fromWall);
            line.SetState(visible, a, b, carriedLink == line.Link);
        }

        ShowScreen(boundPower != null ? boundPower.State : PcPowerState.Off);
        if (isActiveAndEnabled) ApplyViewCursor(current);   // item na mão mudou (guardou/pegou algo)
    }

    private RectTransform PortRect(PcPort port)
    {
        foreach (ComputerPortView p in ports) if (p != null && p.Port == port) return p.Anchor;
        return null;
    }

    private void HandlePowerState(PcPowerState state) => ShowScreen(state);

    // Tela, sinal de vídeo ou distância do monitor mudaram (o monitor pode ter entrado ou saído do alcance).
    private void RefreshScreen() => RefreshHardware();

    private void ShowScreen(PcPowerState state)
    {
        if (powerLed != null && powerLed.activeSelf != (state != PcPowerState.Off)) powerLed.SetActive(state != PcPowerState.Off);
        if (monitorScreen == null) return;
        Sprite s = null;
        switch (boundPower != null ? boundPower.Screen : PcScreen.Off)
        {
            case PcScreen.NoSignal: s = noSignalScreen; break;
            case PcScreen.NoStorage: s = biosScreen; break;
            case PcScreen.NoBootableDevice: s = noBootableScreen != null ? noBootableScreen : biosScreen; break;
            case PcScreen.Installing:
                int i = boundPower.InstallStage;
                s = installScreens.Length > 0 ? installScreens[Mathf.Clamp(i, 0, installScreens.Length - 1)] : biosScreen;
                break;
            case PcScreen.OperatingSystem:
                s = onlineScreen != null && boundPower.NetworkConnected ? onlineScreen : operatingSystemScreen;
                break;
        }
        monitorScreen.sprite = s;
        monitorScreen.color = s != null ? Color.white : screenOffColor;
    }

    /// <summary>
    /// Mãozinha (padrão das telas) em todas as vistas; a chave de fenda só na Traseira e só se o jogador estiver
    /// segurando a chave de fenda, no tamanho proporcional aos parafusos na tela.
    /// </summary>
    private void ApplyViewCursor(ComputerView view)
    {
        ToolItem tool = GetHeldToolItem();
        bool screwdriver = view == ComputerView.Rear && rearCursor != null && tool != null && tool.Type == ToolType.Screwdriver;
        if (screwdriver) UICursor.Set(rearCursor, rearCursorHotspot, ScrewdriverCursorHeight());
        else if (screen != null && screen.Owner != null) screen.Owner.ApplyDefaultCursor();
        else if (handCursor != null) UICursor.Set(handCursor, handCursorHotspot, handCursorHeight);
        else UICursor.Reset();
    }

    /// <summary>Altura (px) do cursor de chave de fenda: proporcional ao parafuso como aparece na tela agora.</summary>
    public int ScrewdriverCursorHeight() => ScrewView.CursorHeight(views.Count > 0 ? views[0] : null, screwdriverToScrewRatio);

    private ToolItem GetHeldToolItem()
    {
        ItemHolder holder = screen.Interactor != null ? screen.Interactor.Holder : null;
        if (holder == null || !holder.IsHolding) return null;
        return holder.Current.GetComponentInParent<ToolItem>();
    }

    private ItemHolder Holder => screen.Interactor != null ? screen.Interactor.Holder : null;

    /// <summary>Botão Power (frente do gabinete): liga só com os requisitos; ligado, desliga.</summary>
    public void PressPower()
    {
        if (boundPower == null) return;
        InteractionSounds.Raise(InteractionSound.PowerButton);
        boundPower.PressPower();
    }

    // Itens conectados/instalados saem da mão e ficam guardados no objeto do mundo (nunca destruídos).
    private void OnPortClicked(PcPort port)
    {
        if (boundPower == null) return;
        ItemHolder holder = Holder;
        PickupInteractable held = holder != null ? holder.Current : null;
        CableItem cable = held != null ? held.GetComponentInParent<CableItem>() : null;

        PortResult result = boundPower.UsePort(port, cable, held != null, out GameObject back);
        if (result == PortResult.OutOfReach) { RefreshHardware(); return; }   // interface antiga: esconde o que está longe
        if (result == PortResult.PluggedFromHand || result == PortResult.PluggedOtherEnd) InteractionSounds.Raise(InteractionSound.CablePlugged);
        else if (result == PortResult.Unplugged || result == PortResult.CableReturned) InteractionSounds.Raise(InteractionSound.CableUnplugged);
        else if (result == PortResult.WrongCable || result == PortResult.NeedsCable
              || result == PortResult.HandsFull || result == PortResult.Invalid)
            InteractionSounds.Raise(InteractionSound.Error);   // cabo errado / clique que não vale: som de erro
        // segue a ponta solta (cabo recém-ligado por uma ponta ou que acabou de soltar uma); tomada: o cabo que estava nela
        if (result == PortResult.PluggedFromHand || result == PortResult.Unplugged) carriedLink = boundPower.LastChangedLink;
        RefreshHardware(); // o UsePort já atualizou a tela; de novo, agora sabendo qual cabo segue o mouse
        if (result == PortResult.PluggedFromHand) holder.Stow(boundCase.transform.position);
        else if (result == PortResult.CableReturned && holder != null) holder.Retrieve(back);
    }

    private void OnUsbClicked(int index)
    {
        if (boundPower == null) return;
        ItemHolder holder = Holder;
        PickupInteractable held = holder != null ? holder.Current : null;
        PendriveItem pendrive = held != null ? held.GetComponentInParent<PendriveItem>() : null;

        PortResult result = boundPower.UseUsbPort(index, pendrive, held != null, out GameObject back);
        if (result == PortResult.PluggedFromHand) InteractionSounds.Raise(InteractionSound.PendrivePlugged);
        else if (result == PortResult.CableReturned) InteractionSounds.Raise(InteractionSound.PendriveUnplugged);
        else if (result == PortResult.WrongCable || result == PortResult.NeedsCable
              || result == PortResult.HandsFull || result == PortResult.Invalid)
            InteractionSounds.Raise(InteractionSound.Error);   // não é pendrive / clique que não vale
        if (result == PortResult.PluggedFromHand) holder.Stow(boundCase.transform.position);
        else if (result == PortResult.CableReturned && holder != null) holder.Retrieve(back);
    }

    private void OnSlotClicked(PcComponent slot)
    {
        if (boundCase == null) return;
        ItemHolder holder = Holder;
        PickupInteractable held = holder != null ? holder.Current : null;
        ComputerPartItem part = held != null ? held.GetComponentInParent<ComputerPartItem>() : null;

        PartSlotResult result = boundCase.UsePartSlot(slot, part, held != null, out GameObject back);
        if (result == PartSlotResult.Installed) InteractionSounds.Raise(InteractionSound.PartInstalled);
        else if (result == PartSlotResult.Removed) InteractionSounds.Raise(InteractionSound.PartRemoved);
        else if (result == PartSlotResult.WrongPart || result == PartSlotResult.HandsFull)
            InteractionSounds.Raise(InteractionSound.Error);   // peça no encaixe errado: som de erro
        if (result == PartSlotResult.Installed) holder.Stow(boundCase.transform.position);
        else if (result == PartSlotResult.Removed && holder != null) holder.Retrieve(back);
    }

    private void OnScrewClicked(int index)
    {
        if (boundCase == null) return;

        ToolItem tool = GetHeldToolItem();
        ToolType held = tool != null ? tool.Type : ToolType.None;

        // furo (parafuso removido) = recoloca; parafuso no lugar = remove (estado real de cada parafuso)
        if (boundCase.Screws[index].removed)
        {
            if (boundCase.TryInstallScrew(index, held) == ScrewInstallResult.WrongTool)
            {
                InteractionSounds.Raise(InteractionSound.Error);   // ferramenta errada: som de erro + parafuso recusando
                views[index].PlayRejected();
            }
            return;
        }

        switch (boundCase.TryRemoveScrew(index, held))
        {
            case ScrewRemovalResult.Removed:
                break;
            case ScrewRemovalResult.WrongTool:
                InteractionSounds.Raise(InteractionSound.Error);
                views[index].PlayRejected();
                break;
        }
    }

    private void HandleScrewRemoved(int index)
    {
        if (index >= 0 && index < views.Count) views[index].SetRemoved(true, true);
        InteractionSounds.Raise(InteractionSound.ScrewRemoved);
        // último parafuso obrigatório retirado (e gabinete na mesa/bancada): libera o Avançar com um pulso, sem texto
        if (!UpdateNextLock()) return;
        if (nextRect != null && isActiveAndEnabled)
        {
            if (nextRoutine != null) StopCoroutine(nextRoutine);
            nextRoutine = StartCoroutine(PulseUnlocked());
        }
    }

    private void HandleScrewInstalled(int index)
    {
        if (index >= 0 && index < views.Count) views[index].SetRemoved(false, true);
        InteractionSounds.Raise(InteractionSound.ScrewInstalled);
        UpdateNextLock();   // de volta no lugar: o Avançar pode voltar a ficar bloqueado (sem texto)
    }

    // Fechado ao recolocar os obrigatórios: o Interior deixa de valer (ShowView cai para a Traseira) e o Avançar
    // volta ao bloqueio. Aberto: nada a fazer aqui (a navegação já cuida).
    private void HandleCaseState(CaseState state)
    {
        if (state != CaseState.Closed) return;
        if (current == ComputerView.Interior) ShowView(ComputerView.Rear);   // (os parafusos só mudam na Traseira)
        UpdateNextLock();   // o Avançar volta a ficar apagado: é o sinal visual do fechamento (sem texto)
        InteractionSounds.Raise(InteractionSound.CaseClosed);
    }
}