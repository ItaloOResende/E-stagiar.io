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

    [Header("Navegação")]
    [SerializeField] private Button backButton;
    [SerializeField] private Button nextButton;

    [Header("Configuração de Cursores")]
    [Tooltip("Cursor de mão para as telas de Frente e Interior.")]
    [SerializeField] private Texture2D handCursor;
    [SerializeField] private Vector2 handCursorHotspot;

    [Tooltip("Cursor de chave de fenda para a tela Traseira.")]
    [SerializeField] private Texture2D rearCursor;
    [SerializeField] private Vector2 rearCursorHotspot;

    [Header("Tamanho dos Cursores")]
    [Tooltip("Deseja redimensionar os cursores por script para deixá-los maiores?")]
    [SerializeField] private bool enableCustomCursorSize = true;
    [SerializeField] private int cursorWidth = 64;
    [SerializeField] private int cursorHeight = 64;

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
            Debug.LogError("ComputerScreenController: o objeto que abriu a tela não tem ComputerCase.", this);
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
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
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

        if (frontView != null) frontView.SetActive(view == ComputerView.Front);
        if (rearView != null) rearView.SetActive(view == ComputerView.Rear);
        if (interiorView != null) interiorView.SetActive(view == ComputerView.Interior);
        if (backButton != null) backButton.interactable = view != ComputerView.Front;
        if (nextButton != null) nextButton.interactable = view != ComputerView.Interior;

        // Aplica o cursor correspondente à vista atual (já redimensionado)
        ApplyViewCursor(view);

        for (int i = 0; i < views.Count; i++)
            views[i].SetRemoved(boundCase.Screws[i].removed, false);
        if (motherboardView != null) motherboardView.SetActive(boundCase.HasComponent(PcComponent.Motherboard));
        if (ramView != null) ramView.SetActive(boundCase.HasComponent(PcComponent.Ram));
        if (hddView != null) hddView.SetActive(boundCase.HasComponent(PcComponent.Hdd));
    }

    private void ApplyViewCursor(ComputerView view)
    {
        Texture2D targetTexture = null;
        Vector2 targetHotspot = Vector2.zero;

        // Traseira usa chave de fenda; Frente e Interior usam a mão
        if (view == ComputerView.Rear)
        {
            targetTexture = rearCursor;
            targetHotspot = rearCursorHotspot;
        }
        else
        {
            targetTexture = handCursor;
            targetHotspot = handCursorHotspot;
        }

        if (targetTexture != null)
        {
            if (enableCustomCursorSize)
            {
                Texture2D resized = ResizeTexture(targetTexture, cursorWidth, cursorHeight);
                Cursor.SetCursor(resized, targetHotspot, CursorMode.Auto);
            }
            else
            {
                Cursor.SetCursor(targetTexture, targetHotspot, CursorMode.Auto);
            }
        }
        else
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }
    }

    private Texture2D ResizeTexture(Texture2D source, int targetWidth, int targetHeight)
    {
        RenderTexture rt = RenderTexture.GetTemporary(targetWidth, targetHeight);
        RenderTexture.active = rt;
        Graphics.Blit(source, rt);

        Texture2D result = new Texture2D(targetWidth, targetHeight, TextureFormat.RGBA32, false);
        result.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
        result.Apply();

        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rt);
        return result;
    }

    private ToolItem GetHeldToolItem()
    {
        ItemHolder holder = screen.Interactor != null ? screen.Interactor.Holder : null;
        if (holder == null || !holder.IsHolding) return null;
        return holder.Current.GetComponentInParent<ToolItem>();
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