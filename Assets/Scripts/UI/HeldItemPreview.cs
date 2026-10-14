using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Mostra, nas telas 2D (manutenção do PC etc.), o objeto que o jogador está segurando: na 1ª pessoa o item aparece na
/// mão, mas a tela 2D cobre a visão. É uma MINIATURA do próprio objeto da mão (o mesmo <see cref="ItemHolder.Current"/>),
/// renderizada por uma câmera própria só com ele: não cria cópia no mundo, não tem collider e não recebe cliques.
/// Atualiza na hora em que o item da mão muda; some com a mão vazia ou sem tela aberta. Sem texto.
/// Fica num painel do Canvas das telas (ex.: <c>ScreenCanvas/MaoAtual</c>), desenhado por cima da tela aberta.
/// Trocar por outra representação no futuro (ícone por item, modelo final) = trocar este componente/painel; a lógica de
/// pegar/soltar não depende dele.
/// </summary>
public class HeldItemPreview : MonoBehaviour
{
    [SerializeField] private ItemHolder holder;
    [SerializeField] private UIScreenManager screens;
    [Tooltip("Painel mostrado/oculto (moldura). Vazio = este objeto.")]
    [SerializeField] private GameObject panel;
    [SerializeField] private RawImage image;

    [Header("Renderização da miniatura")]
    [Tooltip("Camada usada SÓ durante a renderização da miniatura (o item volta às camadas dele em seguida).")]
    [SerializeField] private string previewLayer = "ItemPreview";
    [SerializeField] private int resolution = 256;
    [Tooltip("Ligado: a miniatura olha o item como o jogador o vê na mão (dos olhos até o item). Desligado: usa 'View Direction'.")]
    [SerializeField] private bool fromPlayerEyes = true;
    [Tooltip("Direção do olhar da miniatura no espaço da câmera do jogador (Z = para a frente), se 'From Player Eyes' estiver desligado.")]
    [SerializeField] private Vector3 viewDirection = new Vector3(0.2f, -0.4f, 1f);
    [Tooltip("Folga em volta do item (1 = a maior medida do item encosta na borda).")]
    [Min(1f)] [SerializeField] private float padding = 1.2f;
    [SerializeField] private Color background = new Color(0.86f, 0.9f, 0.92f, 1f);

    private Camera previewCamera;
    private RenderTexture texture;
    private PickupInteractable rendered;

    /// <summary>Item mostrado agora (null = painel oculto). Para testes.</summary>
    public PickupInteractable Shown => Visible ? rendered : null;
    public bool Visible => (panel != null ? panel : gameObject).activeSelf && rendered != null;

    private GameObject Panel => panel != null ? panel : gameObject;

    private void Awake()
    {
        if (holder == null) holder = FindAnyObjectByType<ItemHolder>();
        if (screens == null && holder != null) screens = holder.GetComponent<UIScreenManager>();
        if (image != null) image.raycastTarget = false;
    }

    private void LateUpdate()
    {
        PickupInteractable item = holder != null && holder.IsHolding && screens != null && screens.IsOpen ? holder.Current : null;
        if (item == null)
        {
            rendered = null;
            if (panel != null && panel.activeSelf) panel.SetActive(false);
            return;
        }

        if (!Panel.activeSelf) Panel.SetActive(true);
        PlaceInSlot();
        // por cima da tela aberta (as telas vão para o fim da lista do Canvas ao abrir)
        Transform top = transform;
        if (top.parent != null && top.GetSiblingIndex() != top.parent.childCount - 1) top.SetAsLastSibling();
        if (item != rendered) Render(item);
    }

    private bool hasDefault;
    private Vector2 defMin, defMax, defPivot, defPos, defSize;

    // A vista aberta pode ter um lugar próprio para a miniatura (HeldItemSlot ativo); senão, a posição do prefab.
    private void PlaceInSlot()
    {
        var frame = Panel.transform as RectTransform;
        if (frame == null || frame == transform) return;
        if (!hasDefault)
        {
            hasDefault = true;
            defMin = frame.anchorMin; defMax = frame.anchorMax; defPivot = frame.pivot; defPos = frame.anchoredPosition; defSize = frame.sizeDelta;
        }
        HeldItemSlot slot = null;
        if (screens.Current != null)
            foreach (HeldItemSlot s in screens.Current.GetComponentsInChildren<HeldItemSlot>())
                if (s.isActiveAndEnabled) { slot = s; break; }
        if (slot == null)
        {
            frame.anchorMin = defMin; frame.anchorMax = defMax; frame.pivot = defPivot;
            frame.anchoredPosition = defPos; frame.sizeDelta = defSize;
            return;
        }
        var corners = new Vector3[4];
        ((RectTransform)slot.transform).GetWorldCorners(corners);
        var parent = (RectTransform)frame.parent;
        Vector2 a = parent.InverseTransformPoint(corners[0]), b = parent.InverseTransformPoint(corners[2]);
        frame.anchorMin = frame.anchorMax = new Vector2(0.5f, 0.5f);
        frame.pivot = new Vector2(0.5f, 0.5f);
        frame.sizeDelta = b - a;
        frame.anchoredPosition = (a + b) * 0.5f - parent.rect.center;
    }

    /// <summary>Renderiza a miniatura de <paramref name="item"/> (o próprio objeto, onde ele está).</summary>
    public void Render(PickupInteractable item)
    {
        rendered = item;
        if (image == null || item == null) return;
        EnsureCamera();

        Bounds b = item.WorldBounds;
        Transform eye = holder.HoldPoint != null && holder.HoldPoint.parent != null ? holder.HoldPoint.parent : item.transform;
        Vector3 dir = fromPlayerEyes && (b.center - eye.position).sqrMagnitude > 1e-4f
            ? (b.center - eye.position).normalized
            : (eye.rotation * viewDirection).normalized;
        float radius = Mathf.Max(0.01f, Mathf.Max(b.extents.x, Mathf.Max(b.extents.y, b.extents.z))) * padding;
        float distance = radius / Mathf.Sin(previewCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        previewCamera.transform.position = b.center - dir * distance;
        previewCamera.transform.rotation = Quaternion.LookRotation(dir, eye.up);
        float depth = b.extents.magnitude * 1.5f;
        previewCamera.nearClipPlane = Mathf.Max(0.005f, distance - depth);
        previewCamera.farClipPlane = distance + depth;

        int layer = LayerMask.NameToLayer(previewLayer);
        if (layer < 0) layer = 31;   // camada livre de reserva (sem nome no projeto)
        Renderer[] renderers = item.GetComponentsInChildren<Renderer>();
        var old = new int[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            old[i] = renderers[i].gameObject.layer;
            renderers[i].gameObject.layer = layer;
        }
        previewCamera.cullingMask = 1 << layer;
        previewCamera.backgroundColor = background;
        previewCamera.targetTexture = texture;
        previewCamera.Render();
        for (int i = 0; i < renderers.Length; i++) renderers[i].gameObject.layer = old[i];

        image.texture = texture;
        image.color = Color.white;
        image.raycastTarget = false;   // só mostra: nunca bloqueia cliques na tela
    }

    private void EnsureCamera()
    {
        if (texture == null)
        {
            texture = new RenderTexture(resolution, resolution, 24, RenderTextureFormat.ARGB32) { name = "MaoAtual_Miniatura" };
            texture.Create();
        }
        if (previewCamera != null) return;
        var go = new GameObject("MaoAtual_Camera") { hideFlags = HideFlags.HideAndDontSave };
        previewCamera = go.AddComponent<Camera>();
        previewCamera.enabled = false;               // só renderiza quando chamada
        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        previewCamera.fieldOfView = 30f;
    }

    private void OnDestroy()
    {
        if (previewCamera != null) Destroy(previewCamera.gameObject);
        if (texture != null) { texture.Release(); Destroy(texture); }
    }
}
