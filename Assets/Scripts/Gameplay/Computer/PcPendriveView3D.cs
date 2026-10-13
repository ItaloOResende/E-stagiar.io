using UnityEngine;

/// <summary>
/// Pendrive encaixado numa USB do gabinete no mundo 3D (só visual; a lógica está no <see cref="ComputerPower"/>).
/// Vai no root lógico do gabinete. <see cref="plugged"/> é um objeto filho com o modelo do pendrive (desativado);
/// quando há pendrive conectado ele é mostrado na âncora da USB correspondente (posição = boca da porta,
/// eixo Z = para FORA do gabinete; o conector do modelo aponta para -Z e entra na porta).
/// </summary>
[RequireComponent(typeof(ComputerPower))]
public class PcPendriveView3D : MonoBehaviour
{
    [Tooltip("Uma âncora por USB, na ordem dos índices (0 a ComputerPower.UsbPortCount - 1).")]
    [SerializeField] private Transform[] usbAnchors = new Transform[ComputerPower.UsbPortCount];
    [Tooltip("Modelo do pendrive encaixado (filho do gabinete, desativado no prefab).")]
    [SerializeField] private Transform plugged;

    private ComputerPower power;

    private void OnEnable()
    {
        power = GetComponent<ComputerPower>();
        power.ConnectionsChanged += Apply;
        Apply();
    }

    private void OnDisable()
    {
        if (power != null) power.ConnectionsChanged -= Apply;
    }

    private void Apply()
    {
        if (plugged == null) return;
        int i = power.PendrivePort;
        bool show = i >= 0 && i < usbAnchors.Length && usbAnchors[i] != null;
        if (show) plugged.SetPositionAndRotation(usbAnchors[i].position, usbAnchors[i].rotation);
        if (plugged.gameObject.activeSelf != show) plugged.gameObject.SetActive(show);
    }
}
