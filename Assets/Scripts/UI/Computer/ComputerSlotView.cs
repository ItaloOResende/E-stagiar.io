using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Apresentação de UM encaixe de peça no interior do PC (placa-mãe, RAM ou HD): área clicável sobre a peça +
/// contorno "vazio" quando a peça não está instalada. A peça em si é a imagem já existente da vista do interior
/// (mostrada/escondida pelo <see cref="ComputerScreenController"/>). Sem lógica de jogo.
/// </summary>
public class ComputerSlotView : MonoBehaviour
{
    [Tooltip("Um único componente.")]
    [SerializeField] private PcComponent slot = PcComponent.Ram;
    [SerializeField] private Button button;
    [Tooltip("Contorno mostrado quando o encaixe está vazio.")]
    [SerializeField] private Graphic emptyGhost;

    public PcComponent Slot => slot;

    public void Init(Action<PcComponent> onClick)
    {
        if (button == null) button = GetComponent<Button>();
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClick?.Invoke(slot));
    }

    public void SetInstalled(bool installed)
    {
        if (emptyGhost != null) emptyGhost.gameObject.SetActive(!installed);
    }
}
