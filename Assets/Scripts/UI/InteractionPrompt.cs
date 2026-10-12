using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Prompt do HUD no centro da tela: mostra, lado a lado, as interações possíveis com o objeto sob a mira.
///  - Esquerda: ícone da mão + ação do clique ESQUERDO (ex.: "Pegar").
///  - Direita: ícone da ação do clique DIREITO (ex.: chave de fenda para a manutenção do PC) + texto.
/// Cada lado só aparece se houver alvo para aquele clique. O ícone de cada lado vem de
/// <see cref="Interactable.promptIcon"/> do alvo, ou do ícone padrão deste componente.
/// Só lê o <see cref="InteractionDetector"/>; não processa cliques.
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

    [Header("Clique direito")]
    [SerializeField] private GameObject secondaryRoot;
    [SerializeField] private Image secondaryIcon;
    [SerializeField] private Text secondaryLabel;
    [SerializeField] private Sprite defaultSecondaryIcon;
    [SerializeField] private string secondaryFormat = "Dir.: {0}";

    private void Awake()
    {
        if (detector == null) detector = FindAnyObjectByType<InteractionDetector>();
    }

    private void LateUpdate()
    {
        Interactable primary = detector != null && detector.isActiveAndEnabled ? detector.CurrentTarget : null;
        Interactable secondary = detector != null && detector.isActiveAndEnabled ? detector.CurrentSecondaryTarget : null;
        Apply(primary, primaryRoot, primaryIcon, primaryLabel, defaultPrimaryIcon, primaryFormat);
        Apply(secondary, secondaryRoot, secondaryIcon, secondaryLabel, defaultSecondaryIcon, secondaryFormat);
    }

    private static void Apply(Interactable target, GameObject root, Image icon, Text label, Sprite fallback, string format)
    {
        if (root != null && root.activeSelf != (target != null)) root.SetActive(target != null);
        if (target == null) return;
        if (icon != null)
        {
            Sprite s = target.promptIcon != null ? target.promptIcon : fallback;
            icon.sprite = s;
            icon.enabled = s != null;
        }
        if (label != null)
        {
            string text = string.Format(format, target.PromptText); // o texto pode mudar sem trocar de alvo (abrir/fechar)
            if (label.text != text) label.text = text;
        }
    }
}
