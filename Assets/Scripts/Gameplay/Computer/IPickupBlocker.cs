/// <summary>
/// Componente que pode impedir, por enquanto, que o objeto em que está seja pego e carregado
/// (o <c>PickupInteractable</c> do mesmo GameObject consulta todos antes de oferecer "Pegar").
/// Ex.: <see cref="ComputerCase"/> aberto. Fica neste assembly (o mais baixo do projeto) para que a lógica do
/// gameplay possa implementá-la sem depender do código de interação.
/// </summary>
public interface IPickupBlocker
{
    /// <summary>Verdadeiro enquanto o objeto não pode ser pego.</summary>
    bool BlocksPickup { get; }
}

/// <summary>
/// Componente que pode impedir que a tela 2D do objeto seja aberta (o <c>UIScreenInteractable</c> do mesmo GameObject
/// consulta todos antes de oferecer o clique direito). Ex.: <see cref="ComputerCase"/> fora de uma mesa/bancada.
/// </summary>
public interface IScreenBlocker
{
    /// <summary>Verdadeiro enquanto a tela do objeto não pode ser aberta.</summary>
    bool BlocksScreen { get; }
}
