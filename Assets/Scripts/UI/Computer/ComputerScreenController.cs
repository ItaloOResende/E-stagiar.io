using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Controla a tela 2D do gabinete: vista TRASEIRA (parafusos clicáveis) e vista do INTERIOR.
/// Fica no GameObject raiz do prefab da tela, ao lado do <see cref="UIScreen"/>.
///
/// Separação lógica × apresentação: o estado vive no <see cref="ComputerCase"/> do objeto do mundo (descoberto a
/// partir de <see cref="UIScreen.Source"/> ao abrir); aqui só se lê esse estado e se comanda o ComputerCase.
/// Como a mesma instância de tela é reaproveitada por vários gabinetes, a tela é revinculada a cada abertura.
/// Textos e valores saem por UnityEvents (<c>UnityEvent&lt;string&gt;</c>/<c>&lt;float&gt;</c>): ligue-os a qualquer
/// componente de texto (Text, TextMeshPro...) ou barra, então fontes, botões e fundos podem ser trocados no prefab
/// sem alterar este script.
/// </summary>
[RequireComponent(typeof(UIScreen))]
public class ComputerScreenController : MonoBehaviour
{
    [Header("Vistas")]
    [Tooltip("Vista da traseira (gabinete fechado) com os parafusos.")]
    [SerializeField] private GameObject rearView;
    [Tooltip("Vista do interior (só depois de remover todos os parafusos e abrir).")]
    [SerializeField] private GameObject interiorView;

    [Header("Parafusos")]
    [Tooltip("Contêiner com a MESMA proporção da imagem da traseira; os parafusos são ancorados nele (0–1).")]
    [SerializeField] private RectTransform screwLayer;
    [SerializeField] private ScrewView screwPrefab;

    [Header("Controles")]
    [SerializeField] private Button openButton;

    [Header("Saídas (ligue a qualquer texto/barra)")]
    public UnityEvent<string> onTitleText;
    public UnityEvent<string> onProgressText;
    public UnityEvent<float> onProgressValue;
    public UnityEvent<string> onToolText;
    public UnityEvent<string> onHintText;

    [Header("Mensagens (português; edite aqui)")]
    [SerializeField] private string titleRear = "Gabinete - traseira";
    [SerializeField] private string titleInterior = "Gabinete - interior";
    [Tooltip("{0} = removidos, {1} = total.")]
    [SerializeField] private string progressFormat = "Parafusos removidos: {0}/{1}";
    [Tooltip("{0} = nome da ferramenta.")]
    [SerializeField] private string toolHoldingFormat = "Na mão: {0}";
    [SerializeField] private string toolEmpty = "Na mão: nada";
    [SerializeField] private string toolOther = "Na mão: um item que não é ferramenta";
    [SerializeField] private string hintStart = "Clique em cada parafuso com a chave de fenda na mão para removê-lo.";
    [SerializeField] private string hintNoTool = "Você precisa de uma chave de fenda na mão. Pegue uma no armário, segure-a e abra esta tela de novo.";
    [SerializeField] private string hintWrongTool = "Esta ferramenta não serve para parafusos. Use uma chave de fenda.";
    [SerializeField] private string hintRemoved = "Parafuso removido.";
    [SerializeField] private string hintAllRemoved = "Todos os parafusos foram removidos. Já é possível abrir o gabinete.";
    [SerializeField] private string hintInterior = "Gabinete aberto. Interior liberado.";

    private UIScreen screen;
    private ComputerCase boundCase;
    private bool subscribed;
    private readonly List<ScrewView> views = new List<ScrewView>();

    private void Awake()
    {
        screen = GetComponent<UIScreen>();
        if (openButton != null) openButton.onClick.AddListener(OpenCase);
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
        Refresh();
    }

    private void OnDisable() => Unsubscribe();

    private void Subscribe()
    {
        if (subscribed || boundCase == null) return;
        boundCase.ScrewRemoved += HandleScrewRemoved;
        boundCase.StateChanged += HandleStateChanged;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed || boundCase == null) { subscribed = false; return; }
        boundCase.ScrewRemoved -= HandleScrewRemoved;
        boundCase.StateChanged -= HandleStateChanged;
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

    // ---- estado → tela -------------------------------------------------------------------------------------------

    private void Refresh()
    {
        if (boundCase == null) return;

        bool open = boundCase.State == CaseState.Open;
        if (rearView != null) rearView.SetActive(!open);
        if (interiorView != null) interiorView.SetActive(open);
        onTitleText.Invoke(open ? titleInterior : titleRear);

        for (int i = 0; i < views.Count; i++)
            views[i].SetRemoved(boundCase.Screws[i].removed, false);

        UpdateProgress();
        UpdateTool(out ToolType held);

        if (open) onHintText.Invoke(hintInterior);
        else if (boundCase.AllScrewsRemoved) onHintText.Invoke(hintAllRemoved);
        else onHintText.Invoke(held == ToolType.Screwdriver ? hintStart : hintNoTool);
    }

    private void UpdateProgress()
    {
        int total = boundCase.TotalCount;
        int removed = boundCase.RemovedCount;
        onProgressText.Invoke(string.Format(progressFormat, removed, total));
        onProgressValue.Invoke(total == 0 ? 1f : (float)removed / total);

        if (openButton != null)
        {
            openButton.gameObject.SetActive(boundCase.State == CaseState.Closed);
            openButton.interactable = boundCase.CanOpen;
        }
    }

    private void UpdateTool(out ToolType held)
    {
        ToolItem tool = GetHeldToolItem(out bool holdingSomething);
        held = tool != null ? tool.Type : ToolType.None;

        if (tool != null) onToolText.Invoke(string.Format(toolHoldingFormat, tool.DisplayName));
        else onToolText.Invoke(holdingSomething ? toolOther : toolEmpty);
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
                // Feedback, progresso e estado chegam por HandleScrewRemoved.
                break;
            case ScrewRemovalResult.WrongTool:
                views[index].PlayRejected();
                onHintText.Invoke(held == ToolType.None ? hintNoTool : hintWrongTool);
                break;
        }
    }

    /// <summary>Botão "Abrir gabinete": só funciona com todos os parafusos removidos.</summary>
    public void OpenCase()
    {
        if (boundCase != null) boundCase.TryOpen();
    }

    private void HandleScrewRemoved(int index)
    {
        if (index >= 0 && index < views.Count) views[index].SetRemoved(true, true);
        UpdateProgress();
        onHintText.Invoke(boundCase.AllScrewsRemoved ? hintAllRemoved : hintRemoved);
    }

    private void HandleStateChanged(CaseState state) => Refresh();
}
