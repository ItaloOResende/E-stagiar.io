using System.Collections.Generic;
using UnityEngine;
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
    [SerializeField] private Color screenOffColor = Color.black;

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

    public ComputerView CurrentView => current;

    private void Awake()
    {
        screen = GetComponent<UIScreen>();
        if (backButton != null) backButton.onClick.AddListener(Back);
        if (nextButton != null) nextButton.onClick.AddListener(Next);
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
    }

    private void Subscribe()
    {
        if (subscribed || boundCase == null) return;
        boundCase.ScrewRemoved += HandleScrewRemoved;
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
                if (boundCase.TryOpen() == CaseOpenResult.ScrewsRemaining) 
                {
                    // Aguarda remover os parafusos
                }
                else ShowView(ComputerView.Interior);
                break;
        }
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

        foreach (UsbPortView u in usbPorts)
            if (u != null) u.SetPlugged(boundPower != null && boundPower.PendrivePort == u.Index);
        foreach (ComputerPortView p in ports)
            if (p != null) p.SetPlugged(boundPower != null && boundPower.IsPlugged(p.Port));
        foreach (CableLineView line in cableLines)
        {
            if (line == null) continue;
            bool a = false, b = false;
            if (boundPower != null)
            {
                PcCableView3D.Ports(line.Link, out PcPort pa, out PcPort pb);
                a = boundPower.IsPlugged(pa);
                b = boundPower.IsPlugged(pb);
            }
            if (a == b && carriedLink == line.Link) carriedLink = null;  // completo ou solto: ninguém está levando
            line.SetState(current == ComputerView.Rear, a, b, carriedLink == line.Link);
        }

        ShowScreen(boundPower != null ? boundPower.State : PcPowerState.Off);
        if (isActiveAndEnabled) ApplyViewCursor(current);   // item na mão mudou (guardou/pegou algo)
    }

    private void HandlePowerState(PcPowerState state) => ShowScreen(state);

    private void RefreshScreen() => ShowScreen(boundPower != null ? boundPower.State : PcPowerState.Off);

    private void ShowScreen(PcPowerState state)
    {
        if (powerLed != null && powerLed.activeSelf != (state != PcPowerState.Off)) powerLed.SetActive(state != PcPowerState.Off);
        if (monitorScreen == null) return;
        Sprite s = null;
        switch (boundPower != null ? boundPower.Screen : PcScreen.Off)
        {
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
    public int ScrewdriverCursorHeight()
    {
        float screwPx = 24f;
        ScrewView v = views.Count > 0 ? views[0] : null;
        if (v != null)
        {
            Canvas.ForceUpdateCanvases();
            var corners = new Vector3[4];
            ((RectTransform)v.transform).GetWorldCorners(corners);
            Canvas canvas = v.GetComponentInParent<Canvas>();
            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            float h = (RectTransformUtility.WorldToScreenPoint(cam, corners[1]) - RectTransformUtility.WorldToScreenPoint(cam, corners[0])).magnitude;
            if (h > 1f) screwPx = h;
        }
        return Mathf.Clamp(Mathf.RoundToInt(screwPx * screwdriverToScrewRatio), 48, 320);
    }

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
        if (boundPower != null) boundPower.PressPower();
    }

    // Itens conectados/instalados saem da mão e ficam guardados no objeto do mundo (nunca destruídos).
    private void OnPortClicked(PcPort port)
    {
        if (boundPower == null) return;
        ItemHolder holder = Holder;
        PickupInteractable held = holder != null ? holder.Current : null;
        CableItem cable = held != null ? held.GetComponentInParent<CableItem>() : null;

        PortResult result = boundPower.UsePort(port, cable, held != null, out GameObject back);
        if (result == PortResult.PluggedFromHand) carriedLink = ComputerPower.LinkFor(port, out _);
        else if (result == PortResult.Unplugged) carriedLink = ComputerPower.LinkFor(port, out _); // segue a ponta que soltou
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
        if (result == PartSlotResult.Installed) holder.Stow(boundCase.transform.position);
        else if (result == PartSlotResult.Removed && holder != null) holder.Retrieve(back);
    }

    private void OnScrewClicked(int index)
    {
        if (boundCase == null) return;

        ToolItem tool = GetHeldToolItem();
        ToolType held = tool != null ? tool.Type : ToolType.None;

        switch (boundCase.TryRemoveScrew(index, held))
        {
            case ScrewRemovalResult.Removed:
                break;
            case ScrewRemovalResult.WrongTool:
                views[index].PlayRejected();
                break;
        }
    }

    private void HandleScrewRemoved(int index)
    {
        if (index >= 0 && index < views.Count) views[index].SetRemoved(true, true);
    }
}