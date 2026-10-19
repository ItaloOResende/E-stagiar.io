using UnityEngine;

/// <summary>
/// Painel 3D da impressora (filho <c>Painel</c>/<c>PainelControle</c>, com collider próprio): é PARTE da impressora, não
/// um alvo separado. Os dois cliques ficam indisponíveis aqui, então o <see cref="InteractionDetector"/> (que sobe pelos
/// pais do collider) chega ao interativo da própria impressora (<see cref="UIScreenInteractable"/> no root do
/// <c>Impressora_Estacao</c>): mirar no corpo ou no painel é o MESMO objeto, com o mesmo ícone e a mesma tela.
/// O componente continua no prefab só para não quebrar referências; o visor ao vivo é o <see cref="ImpressoraPainel3D"/>.
/// </summary>
public class ImpressoraPainelInteractable : Interactable
{
    [Tooltip("Sem uso (o painel abre a tela da impressora pelo interativo do root). Mantido por compatibilidade.")]
    public UIScreen screen;

    public override bool CanInteract(InteractionDetector interactor) => false;

    public override bool CanInteractSecondary(InteractionDetector interactor) => false;
}
