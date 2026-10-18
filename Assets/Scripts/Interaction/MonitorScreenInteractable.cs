using UnityEngine;

/// <summary>
/// Clique DIREITO no monitor abre a manutenção do PC dele: a mesma tela 2D do gabinete (<see cref="UIScreenInteractable"/>
/// do gabinete que alimenta este monitor, <see cref="MonitorDisplay.Source"/>), com o gabinete como origem. Vai no ROOT
/// pegável do monitor, ao lado do <see cref="PickupInteractable"/> (clique esquerdo). Respeita os bloqueios do gabinete
/// (ex.: fora de uma superfície de trabalho) e não faz nada com monitor avulso (sem PC) ou quebrado.
/// </summary>
public class MonitorScreenInteractable : Interactable
{
    private MonitorDisplay display;

    private void Awake() => display = GetComponentInChildren<MonitorDisplay>(true);

    private void Reset() => interactionMessage = "Manutenção";

    public override bool CanInteract(InteractionDetector interactor) => false;

    public override bool CanInteractSecondary(InteractionDetector interactor)
    {
        UIScreenInteractable pc = Target();
        return isActiveAndEnabled && pc != null && pc.CanInteractSecondary(interactor);
    }

    public override void InteractSecondary(InteractionDetector interactor)
    {
        UIScreenInteractable pc = Target();
        if (pc != null && pc.CanInteractSecondary(interactor)) pc.InteractSecondary(interactor);
    }

    // a tela do PC deste monitor (null = monitor avulso, quebrado ou longe do gabinete)
    private UIScreenInteractable Target()
    {
        if (display == null) display = GetComponentInChildren<MonitorDisplay>(true);
        ComputerPower pc = display != null ? display.Source : null;
        if (pc == null || pc.Monitor != display.transform || !pc.MonitorAvailable) return null;
        return pc.GetComponent<UIScreenInteractable>();
    }
}
