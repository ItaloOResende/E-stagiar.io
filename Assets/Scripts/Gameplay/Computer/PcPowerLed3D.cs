using UnityEngine;

/// <summary>
/// LED do gabinete no mundo 3D: troca o material dos LEDs (botão Power e rede) entre aceso e apagado conforme o
/// <see cref="ComputerPower"/>. Só apresentação. Vai no root lógico do gabinete.
/// </summary>
[RequireComponent(typeof(ComputerPower))]
public class PcPowerLed3D : MonoBehaviour
{
    [SerializeField] private Renderer target;
    [Tooltip("Material do LED aceso (o que está no modelo).")]
    [SerializeField] private Material ledOn;
    [SerializeField] private Material ledOff;

    private ComputerPower power;

    private void OnEnable()
    {
        power = GetComponent<ComputerPower>();
        power.PowerStateChanged += Apply;
        Apply(power.State);
    }

    private void OnDisable()
    {
        if (power != null) power.PowerStateChanged -= Apply;
    }

    private void Apply(PcPowerState state)
    {
        if (target == null || ledOn == null || ledOff == null) return;
        Material[] mats = target.sharedMaterials;
        Material want = state != PcPowerState.Off ? ledOn : ledOff;
        bool changed = false;
        for (int i = 0; i < mats.Length; i++)
            if ((mats[i] == ledOn || mats[i] == ledOff) && mats[i] != want) { mats[i] = want; changed = true; }
        if (changed) target.sharedMaterials = mats;
    }
}
