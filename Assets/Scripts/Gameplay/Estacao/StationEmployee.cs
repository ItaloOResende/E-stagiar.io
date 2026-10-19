using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Funcionário dono de uma estação de trabalho. Fica no ROOT da estação (Estacao_N); os equipamentos dela (gabinete,
/// notebook) acham este componente subindo na hierarquia, então reorganizar ou mover a estação não quebra a ligação.
/// Ao reservar um equipamento da estação para um chamado, o chamado passa a ser DESTE funcionário
/// (<see cref="TicketInstance.Requester"/>); a reserva prefere a estação cujo funcionário é o solicitante da ficha.
/// A plaquinha da mesa (<see cref="StationNamePlate"/>) mostra o nome.
/// </summary>
[DisallowMultipleComponent]
public class StationEmployee : MonoBehaviour
{
    private static readonly List<StationEmployee> all = new List<StationEmployee>();

    [Tooltip("Nome do funcionário (o mesmo usado como solicitante nas fichas de chamado).")]
    public string employeeName = "Funcionário";
    [Tooltip("Setor (mostrado no celular junto do nome).")]
    public string department = "Escritório";
    [Tooltip("Identificação da estação (ex.: \"Estação 03\"). Estação SEM funcionário (nome vazio, sem NPC) usa isto " +
             "na plaquinha e no local do chamado.")]
    public string stationLabel = "";
    [Tooltip("Nome mostrado SÓ na plaquinha quando a estação não tem funcionário (nome vazio): a mesa não recebe " +
             "chamados por isso — só não fica com \"Estação NN\" escrito. Vazio = usa a identificação da estação.")]
    public string nomeNaPlaca = "";
    [Tooltip("Cor do detalhe (faixa) da plaquinha desta mesa. Alfa 0 = a cor do material (azul).")]
    public Color corDaPlaca = new Color(0f, 0f, 0f, 0f);

    /// <summary>Estação ocupada por um funcionário (NPC). Vazia = só a identificação da estação.</summary>
    public bool Occupied => !string.IsNullOrEmpty(employeeName);
    /// <summary>O que a plaquinha mostra: o nome do funcionário ou, sem funcionário, a identificação da estação.</summary>
    public string DisplayName => Occupied ? employeeName : !string.IsNullOrEmpty(nomeNaPlaca) ? nomeNaPlaca : stationLabel;

    public static IReadOnlyList<StationEmployee> All => all;

    /// <summary>Estação (funcionário) de um objeto qualquer dela (gabinete, notebook...). Null = fora de estação.</summary>
    public static StationEmployee Of(Component c) => c != null ? c.GetComponentInParent<StationEmployee>() : null;

    private void OnEnable() { if (!all.Contains(this)) all.Add(this); }
    private void OnDisable() => all.Remove(this);
}
