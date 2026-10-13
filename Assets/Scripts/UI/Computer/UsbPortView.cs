using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Apresentação de UMA porta USB da traseira do gabinete na tela 2D do PC: área clicável + pendrive visível quando
/// conectado. Sem lógica: avisa o clique e mostra o estado que o <see cref="ComputerScreenController"/> mandar.
/// </summary>
public class UsbPortView : MonoBehaviour
{
    [Tooltip("Índice da USB (0 a ComputerPower.UsbPortCount - 1).")]
    [SerializeField] private int index;
    [SerializeField] private Button button;
    [Tooltip("Pendrive desenhado sobre a porta quando conectado.")]
    [SerializeField] private Graphic pluggedIndicator;

    public int Index => index;

    public void Init(Action<int> onClick)
    {
        if (button == null) button = GetComponent<Button>();
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClick?.Invoke(index));
    }

    public void SetPlugged(bool plugged)
    {
        if (pluggedIndicator != null) pluggedIndicator.gameObject.SetActive(plugged);
    }
}
