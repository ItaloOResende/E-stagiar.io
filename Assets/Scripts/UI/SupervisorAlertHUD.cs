using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Aviso ao jogador quando o supervisor PRESENCIA uma infração (<see cref="InfractionLog.Witnessed"/>). Fica no Canvas do HUD.
///  - borda vermelha breve e discreta (não cobre o centro, não recebe cliques);
///  - indicador direcional: seta na borda da tela apontando para o supervisor quando ele está fora da visão (inclusive
///    atrás), ou um "!" sobre a cabeça dele quando está na tela. A câmera do jogador nunca é girada;
///  - cartão no alto: ícone, título e a mensagem do motivo (queda, dano, desorganização), com entrada/saída animadas;
///  - som curto de alerta (<see cref="InteractionSound.SupervisorAlert"/>, sem posição: ouvido mesmo de costas).
/// Só informa o que foi visto; nenhuma penalidade é mostrada (não existe ainda). Monta os próprios elementos (formas
/// geradas por código, fonte padrão da UI do projeto): sem assets novos.
/// </summary>
public class SupervisorAlertHUD : MonoBehaviour
{
    [Tooltip("Câmera do jogador (para o indicador). Vazio = Camera.main.")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Font font;

    [Header("Textos ({0} = item, ex.: 'memória RAM')")]
    [SerializeField] private string title = "O supervisor viu!";
    [SerializeField] private string droppedMessage = "Você deixou cair: {0}.";
    [SerializeField] private string brokenMessage = "Na queda, quebrou: {0}.";
    [SerializeField] private string floorMessage = "Item largado no chão: {0}.";

    [Header("Tempos e cores")]
    [Min(0.5f)] [SerializeField] private float cardDuration = 4f;
    [Min(0.5f)] [SerializeField] private float indicatorDuration = 4.5f;
    [Min(0.1f)] [SerializeField] private float vignetteDuration = 1.2f;
    [Range(0f, 1f)] [SerializeField] private float vignetteAlpha = 0.35f;
    [SerializeField] private Color alertColor = new Color(0.9f, 0.18f, 0.16f);
    [SerializeField] private Color cardColor = new Color(1f, 0.97f, 0.9f);
    [SerializeField] private Color textColor = new Color(0.16f, 0.13f, 0.12f);

    private RectTransform root, card, indicator, arrow, bang;
    private Image vignette;
    private Text titleText, messageText;
    private Transform supervisor;
    private float shownAt = -100f, indicatorUntil = -100f, vignetteAt = -100f;

    /// <summary>Cartão visível agora.</summary>
    public bool IsShowing => card != null && card.gameObject.activeSelf;
    public string CurrentMessage => messageText != null ? messageText.text : "";
    public bool IndicatorVisible => indicator != null && indicator.gameObject.activeSelf;
    /// <summary>Indicador em modo seta (supervisor fora da tela).</summary>
    public bool IndicatorOffscreen => IndicatorVisible && arrow.gameObject.activeSelf;
    public float VignetteAlpha => vignette != null ? vignette.color.a : 0f;

    private void Awake() => Build();
    private void OnEnable() => InfractionLog.Witnessed += OnWitnessed;
    private void OnDisable() => InfractionLog.Witnessed -= OnWitnessed;

    /// <summary>Mostra o aviso de uma infração presenciada por <paramref name="witness"/>.</summary>
    public void Show(Infraction inf, Transform witness)
    {
        Build();
        supervisor = witness;
        string fmt = inf.Kind == InfractionKind.ComponentBroken ? brokenMessage
                   : inf.Kind == InfractionKind.ItemLeftOnFloor ? floorMessage : droppedMessage;
        titleText.text = title;
        messageText.text = string.Format(fmt, inf.ItemName);
        shownAt = vignetteAt = Time.unscaledTime;
        indicatorUntil = Time.unscaledTime + indicatorDuration;
        card.gameObject.SetActive(true);
        InteractionSounds.Raise(InteractionSound.SupervisorAlert);
    }

    private void OnWitnessed(Infraction inf, Transform witness) => Show(inf, witness);

    private void LateUpdate()
    {
        if (root == null) return;
        float now = Time.unscaledTime;

        // borda vermelha: sobe rápido e some
        float v = (now - vignetteAt) / vignetteDuration;
        float a = v < 0f || v > 1f ? 0f : vignetteAlpha * (v < 0.15f ? v / 0.15f : 1f - (v - 0.15f) / 0.85f);
        vignette.color = new Color(alertColor.r, alertColor.g, alertColor.b, a);
        vignette.enabled = a > 0.001f;

        // cartão: desce do alto, fica, sobe
        float c = now - shownAt;
        if (card.gameObject.activeSelf)
        {
            if (c > cardDuration) card.gameObject.SetActive(false);
            else
            {
                float inK = Mathf.Clamp01(c / 0.3f), outK = Mathf.Clamp01((cardDuration - c) / 0.3f);
                float k = Mathf.Min(EaseOut(inK), EaseOut(outK));
                card.anchoredPosition = new Vector2(0f, Mathf.Lerp(160f, -28f, k));
                card.localScale = Vector3.one * Mathf.Lerp(0.9f, 1f, k);
            }
        }

        UpdateIndicator(now);
    }

    private static float EaseOut(float x) => 1f - (1f - x) * (1f - x);

    private void UpdateIndicator(float now)
    {
        Camera cam = playerCamera != null ? playerCamera : Camera.main;
        bool show = supervisor != null && cam != null && now < indicatorUntil;
        if (indicator.gameObject.activeSelf != show) indicator.gameObject.SetActive(show);
        if (!show) return;

        Vector3 head = supervisor.position + Vector3.up * 2.0f;
        Vector3 vp = cam.WorldToViewportPoint(head);
        bool behind = vp.z < 0f;
        if (behind) { vp.x = 1f - vp.x; vp.y = 1f - vp.y; }
        bool onScreen = !behind && vp.x > 0.05f && vp.x < 0.95f && vp.y > 0.05f && vp.y < 0.95f;
        arrow.gameObject.SetActive(!onScreen);
        bang.gameObject.SetActive(onScreen);
        Vector2 size = root.rect.size;
        float pulse = 1f + 0.12f * Mathf.Sin(now * 10f);
        if (onScreen)
        {
            indicator.anchoredPosition = new Vector2((vp.x - 0.5f) * size.x, (vp.y - 0.5f) * size.y);
            indicator.localRotation = Quaternion.identity;
            indicator.localScale = Vector3.one * pulse;
            return;
        }
        // seta na borda, na direção do supervisor (atrás: direção espelhada, aponta para os lados/baixo)
        Vector2 dir = new Vector2(vp.x - 0.5f, vp.y - 0.5f);
        if (behind) dir = new Vector2(dir.x, -Mathf.Abs(dir.y) - 0.6f);   // atrás: seta embaixo, puxando para o lado dele
        dir.Normalize();
        Vector2 half = size * 0.5f - new Vector2(70f, 70f);
        float t = Mathf.Min(Mathf.Abs(half.x / (dir.x == 0f ? 1e-4f : dir.x)), Mathf.Abs(half.y / (dir.y == 0f ? 1e-4f : dir.y)));
        indicator.anchoredPosition = dir * t;
        indicator.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);
        indicator.localScale = Vector3.one * pulse;
    }

    // ------------------------------------------------------------------ montagem (uma vez)
    private void Build()
    {
        if (root != null) return;
        if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        root = New("AlertaSupervisor", transform);
        Stretch(root);

        vignette = New("BordaVermelha", root).gameObject.AddComponent<Image>();
        Stretch(vignette.rectTransform);
        vignette.sprite = Sprite.Create(VignetteTexture(), new Rect(0, 0, 128, 128), new Vector2(0.5f, 0.5f));
        vignette.raycastTarget = false;
        vignette.enabled = false;

        // indicador: seta (fora da tela) ou "!" (na tela)
        indicator = New("Indicador", root);
        indicator.sizeDelta = new Vector2(70f, 70f);
        var arrowImg = New("Seta", indicator).gameObject.AddComponent<Image>();
        arrow = arrowImg.rectTransform;
        Stretch(arrow);
        arrowImg.sprite = Sprite.Create(TriangleTexture(), new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f));
        arrowImg.color = alertColor;
        arrowImg.raycastTarget = false;
        bang = Circle("Exclamacao", indicator, alertColor, 56f);
        Label(bang, "!", 40, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        indicator.gameObject.SetActive(false);

        // cartão no alto
        card = New("Cartao", root);
        card.anchorMin = card.anchorMax = new Vector2(0.5f, 1f);
        card.pivot = new Vector2(0.5f, 1f);
        card.sizeDelta = new Vector2(640f, 120f);
        var bg = card.gameObject.AddComponent<Image>();
        bg.sprite = Rounded(alertColor, cardColor);
        bg.type = Image.Type.Sliced;
        bg.raycastTarget = false;
        RectTransform icon = Circle("Icone", card, alertColor, 78f);
        icon.anchorMin = icon.anchorMax = new Vector2(0f, 0.5f);
        icon.anchoredPosition = new Vector2(62f, 0f);
        Label(icon, "!", 54, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        titleText = Label(Area(card, 118f, 16f, 36f), "", 28, FontStyle.Bold, alertColor, TextAnchor.UpperLeft);
        messageText = Label(Area(card, 118f, 56f, 52f), "", 26, FontStyle.Normal, textColor, TextAnchor.UpperLeft);
        card.gameObject.SetActive(false);
    }

    private static RectTransform New(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    // faixa de texto no topo do cartão: a partir de 'top' px abaixo da borda, com 'height' px
    private static RectTransform Area(RectTransform parent, float left, float top, float height)
    {
        RectTransform rt = New("Texto", parent);
        rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(left, -top - height); rt.offsetMax = new Vector2(-24f, -top);
        return rt;
    }

    private Text Label(RectTransform parent, string text, int size, FontStyle style, Color color, TextAnchor anchor)
    {
        RectTransform rt = New("Rotulo", parent);
        Stretch(rt);
        var t = rt.gameObject.AddComponent<Text>();
        t.font = font; t.text = text; t.fontSize = size; t.fontStyle = style; t.color = color; t.alignment = anchor;
        t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    private static RectTransform Circle(string name, Transform parent, Color color, float size)
    {
        RectTransform rt = New(name, parent);
        rt.sizeDelta = new Vector2(size, size);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = Sprite.Create(CircleTexture(), new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f));
        img.color = color;
        img.raycastTarget = false;
        return rt;
    }

    // ------------------------------------------------------------------ formas geradas (sem assets)
    private static Texture2D VignetteTexture()
    {
        const int n = 128;
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = Mathf.Abs(x / (n - 1f) - 0.5f) * 2f, dy = Mathf.Abs(y / (n - 1f) - 0.5f) * 2f;
                float e = Mathf.Max(dx, dy);
                float a = Mathf.Clamp01((e - 0.62f) / 0.38f);
                t.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
        t.Apply();
        return t;
    }

    private static Texture2D TriangleTexture()
    {
        const int n = 64;
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float fy = y / (n - 1f), half = 0.5f * (1f - fy);
                bool inside = Mathf.Abs(x / (n - 1f) - 0.5f) <= half && fy > 0.05f;
                t.SetPixel(x, y, inside ? Color.white : new Color(1f, 1f, 1f, 0f));   // aponta para cima (+Y)
            }
        t.Apply();
        return t;
    }

    private static Texture2D CircleTexture()
    {
        const int n = 64;
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f));
                t.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(31.5f - d)));
            }
        t.Apply();
        return t;
    }

    // Retângulo arredondado com borda colorida (9-slice), no estilo cartunizado dos botões.
    private static Sprite Rounded(Color border, Color fill)
    {
        const int n = 64, r = 20, bw = 5;
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float cx = Mathf.Clamp(x, r, n - 1 - r), cy = Mathf.Clamp(y, r, n - 1 - r);
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
                float outer = Mathf.Clamp01(r - d + 0.5f);
                Color c = d > r - bw ? border : fill;
                c.a *= outer;
                t.SetPixel(x, y, c);
            }
        t.Apply();
        return Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
    }
}
