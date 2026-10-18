using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Prompt do HUD no centro da tela: mostra, lado a lado, as interações possíveis com o objeto sob a mira.
///  - Esquerda: ícone da mão + ação do clique ESQUERDO (ex.: "Pegar").
///  - Direita: ícone da ação do clique DIREITO (ex.: chave de fenda para a manutenção do PC) + texto.
/// Cada lado só aparece se houver alvo para aquele clique. O ícone de cada lado depende da ação e do equipamento
/// (<see cref="IconesInteracao.Para"/>: chave de fenda no gabinete, alicate na crimpagem, impressora...); sem regra,
/// o <see cref="Interactable.promptIcon"/> do alvo, ou o ícone padrão deste componente.
/// Só lê o <see cref="InteractionDetector"/>; não processa cliques.
/// Cena com jogador (<see cref="InteractionDetector"/>) e SEM prompt: um prompt só de ícones é criado ao carregar a
/// cena (<see cref="Garantir"/>), para o HUD de interação nunca sumir do jogo.
/// </summary>
public class InteractionPrompt : MonoBehaviour
{
    [SerializeField] private InteractionDetector detector;

    [Header("Clique esquerdo")]
    [SerializeField] private GameObject primaryRoot;
    [SerializeField] private Image primaryIcon;
    [SerializeField] private Text primaryLabel;
    [SerializeField] private Sprite defaultPrimaryIcon;
    [Tooltip("{0} = ação (PromptText do alvo).")]
    [SerializeField] private string primaryFormat = "Esq.: {0}";
    [Tooltip("Ação bloqueada (ex.: gabinete com cabos na tomada): ícone no lugar do da ação. Vazio = mantém o ícone.")]
    [SerializeField] private Sprite blockedIcon;
    [Tooltip("Cor do texto quando a ação está bloqueada.")]
    [SerializeField] private Color blockedColor = new Color(0.9f, 0.25f, 0.2f);

    [Header("Clique direito")]
    [SerializeField] private GameObject secondaryRoot;
    [SerializeField] private Image secondaryIcon;
    [SerializeField] private Text secondaryLabel;
    [SerializeField] private Sprite defaultSecondaryIcon;
    [SerializeField] private string secondaryFormat = "Dir.: {0}";
    [Header("Textos")]
    [Tooltip("Desligado = só os ícones (mão, ferramenta, bloqueio em vermelho), sem frase ao lado. Os comandos ficam " +
             "na tela de comandos do menu de pausa.")]
    [SerializeField] private bool showLabels = true;

    private void Awake()
    {
        if (detector == null) detector = FindAnyObjectByType<InteractionDetector>();
    }

    private void LateUpdate()
    {
        Interactable primary = detector != null && detector.isActiveAndEnabled ? detector.CurrentTarget : null;
        Interactable secondary = detector != null && detector.isActiveAndEnabled ? detector.CurrentSecondaryTarget : null;
        Apply(primary, primaryRoot, primaryIcon, primaryLabel, defaultPrimaryIcon, primaryFormat, true);
        Apply(secondary, secondaryRoot, secondaryIcon, secondaryLabel, defaultSecondaryIcon, secondaryFormat, false);
    }

    private Color? normalColor;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AoIniciar()
    {
        SceneManager.sceneLoaded -= AoCarregarCena;
        SceneManager.sceneLoaded += AoCarregarCena;
        Garantir();
    }

    private static void AoCarregarCena(Scene cena, LoadSceneMode modo) => Garantir();

    /// <summary>
    /// Cria um prompt só de ícones (Canvas próprio, centro da tela, logo abaixo da mira) se a cena tem jogador e
    /// nenhum <see cref="InteractionPrompt"/>. Retorna o prompt da cena (o existente ou o criado), ou null sem jogador.
    /// </summary>
    public static InteractionPrompt Garantir()
    {
        InteractionPrompt existente = FindAnyObjectByType<InteractionPrompt>(FindObjectsInactive.Include);
        if (existente != null) return existente;
        InteractionDetector det = FindAnyObjectByType<InteractionDetector>();
        if (det == null) return null;

        var go = new GameObject("HUD_Interacao", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -10;   // abaixo das telas 2D, do celular e dos menus
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var p = go.AddComponent<InteractionPrompt>();
        p.detector = det;
        p.showLabels = false;
        p.defaultPrimaryIcon = IconesInteracao.Usar;
        p.primaryRoot = Icone(go.transform, "Esquerdo", -42f, out p.primaryIcon);
        p.secondaryRoot = Icone(go.transform, "Direito", 42f, out p.secondaryIcon);
        p.primaryRoot.SetActive(false);
        p.secondaryRoot.SetActive(false);
        return p;
    }

    // Ícone de 64 px (referência 1080p) abaixo do centro da tela; esquerdo = clique esquerdo, direito = direito.
    private static GameObject Icone(Transform pai, string nome, float x, out Image img)
    {
        var go = new GameObject(nome, typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(pai, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(64f, 64f);
        rt.anchoredPosition = new Vector2(x, -78f);
        img = go.GetComponent<Image>();
        img.preserveAspect = true;
        img.raycastTarget = false;
        return go;
    }

    private void Apply(Interactable target, GameObject root, Image icon, Text label, Sprite fallback, string format, bool primarySide)
    {
        if (root != null && root.activeSelf != (target != null)) root.SetActive(target != null);
        if (target == null) return;
        if (icon != null)
        {
            Sprite s = IconesInteracao.Para(target, primarySide);
            if (s == null) s = fallback;
            if (primarySide && target.IsBlocked)
            {
                Sprite b = blockedIcon != null ? blockedIcon : IconesInteracao.Bloqueado;
                if (b != null) s = b;
            }
            icon.sprite = s;
            icon.enabled = s != null;
        }
        if (label != null && label.gameObject.activeSelf != showLabels) label.gameObject.SetActive(showLabels);
        if (icon != null && primarySide && target.IsBlocked && !showLabels) icon.color = blockedColor;
        else if (icon != null && !showLabels && icon.color != Color.white) icon.color = Color.white;
        if (label != null && showLabels)
        {
            string text = string.Format(format, target.PromptText); // o texto pode mudar sem trocar de alvo (abrir/fechar)
            if (label.text != text) label.text = text;
            if (primarySide)
            {
                if (normalColor == null) normalColor = label.color;
                Color c = target.IsBlocked ? blockedColor : normalColor.Value;
                if (label.color != c) label.color = c;
            }
        }
    }
}
