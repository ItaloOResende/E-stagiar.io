using System;
using UnityEngine;

/// <summary>
/// Ações físicas que já têm animação/feedback e estão prontas para receber um efeito sonoro.
/// Valores novos sempre no FIM (o <see cref="InteractionSoundPlayer"/> guarda o número no Inspector).
/// </summary>
public enum InteractionSound
{
    ScrewRemoved,
    ScrewInstalled,
    DrawerOpened,
    DrawerClosed,
    CaseOpened,
    CaseClosed,
    CablePlugged,
    CableUnplugged,
    PartInstalled,
    PartRemoved,
    PendrivePlugged,
    PendriveUnplugged,
    PowerButton,
    ItemPickedUp,
    ItemPlaced,
    ItemDropped,
    ItemStoredInDrawer,
    /// <summary>O supervisor presenciou uma infração (alerta curto, sem posição: o jogador ouve mesmo de costas).</summary>
    SupervisorAlert
}

/// <summary>
/// Ponto único onde as mecânicas avisam "aconteceu uma ação que pode ter som". Não toca nada sozinho: quem toca é o
/// <see cref="InteractionSoundPlayer"/> da cena (se houver e se a ação tiver áudio configurado). Sem ouvintes, ou sem
/// áudio, nada acontece (nenhum erro). Posição vazia = ação da interface 2D (som sem posição no mundo).
/// </summary>
public static class InteractionSounds
{
    /// <summary>Ação e onde ela aconteceu no mundo (null = interface 2D).</summary>
    public static event Action<InteractionSound, Vector3?> Raised;

    public static void Raise(InteractionSound sound, Vector3? worldPosition = null) => Raised?.Invoke(sound, worldPosition);
}
