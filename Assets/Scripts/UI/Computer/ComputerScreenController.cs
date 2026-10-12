using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>Vistas da tela de manutenção do PC, na ordem de navegação.</summary>
public enum ComputerView { Front, Rear, Interior }

/// <summary>
/// Controla a tela 2D de manutenção do PC: três vistas em tela cheia (FRENTE → TRASEIRA → INTERIOR) com
/// botões Voltar/Avançar (navegação linear, sem dar a volta) e Fechar. Fica no GameObject raiz do prefab da tela,
/// ao lado do <see cref="UIScreen"/>.
///
/// Separação lógica × apresentação: o estado vive no <see cref="ComputerCase"/> do objeto do mundo (descoberto a
/// partir de <see cref="UIScreen.Source"/> ao abrir); aqui só se lê esse estado e se comanda o ComputerCase.
/// Como a mesma instância de tela é reaproveitada por vários gabinetes, a tela é revinculada a cada abertura.
///  - Traseira: parafusos clicáveis; o cursor vira a chave de fenda só nesta vista.
///  - Avançar da traseira abre a tampa (<see cref="ComputerCase.TryOpen"/>) e só passa ao interior se os parafusos
///    OBRIGATÓRIOS tiverem sido removidos; senão fica na traseira e mostra a dica.
///  - Interior: mostra/esconde cada componente conforme <see cref="ComputerCase.HasComponent"/>.
/// Textos saem por UnityEvents (<c>UnityEvent&lt;string&gt;</c>): ligue-os a qualquer componente de texto.
/// </summary>
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

    [Header("Navegação")]
    [SerializeField] private Button backButton;
    [SerializeField] private Button nextButton;

    [Header("Cursor da traseira")]
    [Tooltip("Textura do cursor (Texture Type = Cursor). Vazio = cursor padrão em todas as vistas.")]
    [SerializeField] private Texture2D rearCursor;
    [Tooltip("Ponto de clique em pixels a partir do canto SUPERIOR ESQUERDO da textura (a ponta da chave).")]
    [SerializeField] private Vector2 rearCursorHotspot;
    [Tooltip("Desenho decorativo da ferramenta na lateral da traseira (não é o cursor). Fica opaco com a chave na mão.")]
    [SerializeField] private Graphic toolDecoration;

    [Header("Saídas (ligue a qualquer texto)")]
    public UnityEvent<string> onTitleText;
    public UnityEvent<string> onToolText;
    public UnityEvent<string> onHintText;

    [Header("Mensagens (português; edite aqui)")]
    [SerializeField] private string titleFront = "Computador - frente";
    [SerializeField] private string titleRear = "Computador - traseira";
    [SerializeField] private string titleInterior = "Gabinete - interior";
    [Tooltip("{0} = nome da ferramenta.")]
    [SerializeField] private string toolHoldingFormat = "Na mão: {0}";
    [SerializeField] private string toolEmpty = "Na mão: nada";
    [SerializeField] private string toolOther = "Na mão: um item que não é ferramenta";
    [SerializeField] private string hintFront = "Frente do monitor e do gabinete. Avance para ver a traseira.";
    [SerializeField] private string hintStart = "Retire os parafusos do lado direito da tampa para abrir o gabinete.";
    [SerializeField] private string hintNoTool = "Você precisa de uma chave de fenda na mão. Pegue uma no armário, segure-a e abra esta tela de novo.";
    [SerializeField] private string hintWrongTool = "Esta ferramenta não serve para parafusos. Use uma chave de fenda.";
    [SerializeField] private string hintRemoved = "Parafuso removido.";
    [SerializeField] private string hintLocked = "A tampa ainda está presa: faltam parafusos do lado direito.";
    [SerializeField] private string hintUnlocked = "Tampa solta. Avance para ver o interior.";
    [Tooltip("{0} = lista de componentes presentes.")]
    [SerializeField] private string hintInteriorFormat = "Componentes encontrados: {0}.";
    [SerializeField] private string hintInteriorEmpty = "O gabinete está vazio: sem placa-mãe, memória RAM ou HD.";

    private UIScreen screen;
    private ComputerCase boundCase;
    private bool subscribed;
    private ComputerView current = ComputerView.Front;
    private readonly List<ScrewView> views = new List<ScrewView>();

    public ComputerView CurrentView => current;

    private void Awake()
    {
        screen = GetComponent<UIScreen>();
        if (backButton != null) backButton.onClick.AddListener(Back);
        if (nextButton != null) nextButton.onClick.AddListener(Next);
    }

    private void OnEnable()
    {
        ComputerCase found = screen.Source != null ? screen.Source.GetComponentInParent<ComputerCase>() : null;
        if (found == null)
        {
            Debug.LogError("ComputerScreenController: o objeto que abriu a tela não tem ComputerCase (ponha-o no root lógico do gabinete).", this);
            return;
        }

        if (found != boundCase)
        {
            Unsubscribe();
            boundCase = found;
            BuildScrewViews();
        }
        Subscribe();
        ShowView(ComputerView.Front);
    }

    private void OnDisable()
    {
        Unsubscribe();
        ApplyCursor(false);
    }

    private void Subscribe()
    {
        if (subscribed || boundCase == null) return;
        boundCase.ScrewRemoved += HandleScrewRemoved;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed || boundCase == null) { subscribed = false; return; }
        boundCase.ScrewRemoved -= HandleScrewRemoved;
        subscribed = false;
    }

    // ---- construção ----------------------------------------------------------------------------------------------

    private void BuildScrewViews()
    {
        foreach (ScrewView v in views)
            if (v != null) Destroy(v.gameObject);
        views.Clear();

        if (screwPrefab == null || screwLayer == null)
        {
            Debug.LogError("ComputerScreenController: defina 'Screw Prefab' e 'Screw Layer'.", this);
            return;
        }

        for (int i = 0; i < boundCase.Screws.Count; i++)
        {
            Vector2 p = boundCase.Screws[i].normalizedPosition;
            ScrewView view = Instantiate(screwPrefab, screwLayer, false);
            view.name = "Parafuso_" + boundCase.Screws[i].id;
            var rt = (RectTransform)view.transform;
            // Y do dado é a partir do topo da imagem; âncora de UI é a partir de baixo.
            rt.anchorMin = rt.anchorMax = new Vector2(p.x, 1f - p.y);
            rt.anchoredPosition = Vector2.zero;
            view.Init(i, OnScrewClicked);
            views.Add(view);
        }
    }

    // ---- navegação -----------------------------------------------------------------------------------------------

    /// <summary>Botão Avançar: Frente → Traseira → Interior. Da traseira só passa com os obrigatórios removidos.</summary>
    public void Next()
    {
        if (boundCase == null) return;
        switch (current)
        {
            case ComputerView.Front:
                ShowView(ComputerView.Rear);
                break;
            case ComputerView.Rear:
                if (boundCase.TryOpen() == CaseOpenResult.ScrewsRemaining) onHintText.Invoke(hintLocked);
                else ShowView(ComputerView.Interior);
                break;
        }
    }

    /// <summary>Botão Voltar: Interior → Traseira → Frente.</summary>
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

        if (frontView != null) frontView.SetActive(view == ComputerView.Front);
        if (rearView != null) rearView.SetActive(view == ComputerView.Rear);
        if (interiorView != null) interiorView.SetActive(view == ComputerView.Interior);
        if (backButton != null) backButton.interactable = view != ComputerView.Front;
        if (nextButton != null) nextButton.interactable = view != ComputerView.Interior;
        ApplyCursor(view == ComputerView.Rear);

        for (int i = 0; i < views.Count; i++)
            views[i].SetRemoved(boundCase.Screws[i].removed, false);
        if (motherboardView != null) motherboardView.SetActive(boundCase.HasComponent(PcComponent.Motherboard));
        if (ramView != null) ramView.SetActive(boundCase.HasComponent(PcComponent.Ram));
        if (hddView != null) hddView.SetActive(boundCase.HasComponent(PcComponent.Hdd));

        UpdateTool(out ToolType held);
        switch (view)
        {
            case ComputerView.Front:
                onTitleText.Invoke(titleFront);
                onHintText.Invoke(hintFront);
                break;
            case ComputerView.Rear:
                onTitleText.Invoke(titleRear);
                if (boundCase.State == CaseState.Open || boundCase.RequiredScrewsRemoved) onHintText.Invoke(hintUnlocked);
                else onHintText.Invoke(held == ToolType.Screwdriver ? hintStart : hintNoTool);
                break;
            case ComputerView.Interior:
                onTitleText.Invoke(titleInterior);
                onHintText.Invoke(InteriorHint());
                break;
        }
    }

    private string InteriorHint()
    {
        var names = new List<string>();
        if (boundCase.HasComponent(PcComponent.Motherboard)) names.Add("placa-mãe");
        if (boundCase.HasComponent(PcComponent.Ram)) names.Add("memória RAM");
        if (boundCase.HasComponent(PcComponent.Hdd)) names.Add("HD");
        return names.Count == 0 ? hintInteriorEmpty : string.Format(hintInteriorFormat, string.Join(", ", names));
    }

    private void ApplyCursor(bool screwdriver)
    {
        if (screwdriver && rearCursor != null) Cursor.SetCursor(rearCursor, rearCursorHotspot, CursorMode.Auto);
        else Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }

    // ---- ferramenta ----------------------------------------------------------------------------------------------

    private void UpdateTool(out ToolType held)
    {
        ToolItem tool = GetHeldToolItem(out bool holdingSomething);
        held = tool != null ? tool.Type : ToolType.None;

        if (tool != null) onToolText.Invoke(string.Format(toolHoldingFormat, tool.DisplayName));
        else onToolText.Invoke(holdingSomething ? toolOther : toolEmpty);

        if (toolDecoration != null)
        {
            Color c = toolDecoration.color;
            c.a = held == ToolType.Screwdriver ? 1f : 0.35f;
            toolDecoration.color = c;
        }
    }

    private ToolItem GetHeldToolItem(out bool holdingSomething)
    {
        holdingSomething = false;
        ItemHolder holder = screen.Interactor != null ? screen.Interactor.Holder : null;
        if (holder == null || !holder.IsHolding) return null;
        holdingSomething = true;
        return holder.Current.GetComponentInParent<ToolItem>();
    }

    // ---- ações do jogador ----------------------------------------------------------------------------------------

    private void OnScrewClicked(int index)
    {
        if (boundCase == null) return;

        ToolItem tool = GetHeldToolItem(out _);
        ToolType held = tool != null ? tool.Type : ToolType.None;

        switch (boundCase.TryRemoveScrew(index, held))
        {
            case ScrewRemovalResult.Removed:
                // Feedback e dica chegam por HandleScrewRemoved.
                break;
            case ScrewRemovalResult.WrongTool:
                views[index].PlayRejected();
                onHintText.Invoke(held == ToolType.None ? hintNoTool : hintWrongTool);
                break;
        }
    }

    private void HandleScrewRemoved(int index)
    {
        if (index >= 0 && index < views.Count) views[index].SetRemoved(true, true);
        onHintText.Invoke(boundCase.RequiredScrewsRemoved ? hintUnlocked : hintRemoved);
    }
}
