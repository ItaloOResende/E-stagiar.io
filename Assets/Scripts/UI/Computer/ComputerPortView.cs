using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Apresentação de UMA porta (tomada, entrada de energia, VGA…) na tela 2D do PC: área clicável + plugue visível
/// quando conectada. Sem lógica: avisa o clique e mostra o estado que o <see cref="ComputerScreenController"/>
/// mandar. A porta lógica é escolhida em <see cref="Port"/>; a aparência vem do prefab/cena.
/// </summary>
public class ComputerPortView : MonoBehaviour
{
    [SerializeField] private PcPort port;
    [SerializeField] private Button button;
    [Tooltip("Plugue desenhado sobre a porta quando há cabo conectado.")]
    [SerializeField] private Graphic pluggedIndicator;

    public PcPort Port => port;
    /// <summary>
    /// Ponto onde o cabo encosta (para a linha do cabo): o centro do plugue desenhado (que nem sempre fica no centro da
    /// área clicável da porta); sem plugue, a própria porta.
    /// </summary>
    public RectTransform Anchor => pluggedIndicator != null ? pluggedIndicator.rectTransform : (RectTransform)transform;

    public void Init(Action<PcPort> onClick)
    {
        if (button == null) button = GetComponent<Button>();
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClick?.Invoke(port));
    }

    public void SetPlugged(bool plugged)
    {
        if (pluggedIndicator != null) pluggedIndicator.gameObject.SetActive(plugged);
    }
}
