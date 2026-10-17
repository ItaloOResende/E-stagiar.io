using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Defeito REAL de um cabo de rede (item com <see cref="CableItem"/> do tipo rede). Cabo sem este componente está bom.
/// Origens: crimpagem na ordem errada (<see cref="RedeCrimpScreen"/>, com os pinos errados) ou o defeito criado por um
/// chamado no cabo de uma estação (<see cref="PcTicketBridge"/>, <see cref="Chamado"/> = número do chamado).
/// Quem lê: a conectividade (<see cref="RedeConectividade"/>: cabo defeituoso não dá rede, no PC ou no rack) e o
/// testador de cabos (<see cref="TestadorCaboScreen"/>).
/// </summary>
public class CaboRedeEstado : MonoBehaviour
{
    [Tooltip("Somente leitura em jogo: pinos (0..7) que não fecham o circuito.")]
    [SerializeField] private List<int> pinosComFalha = new List<int>();
    [Tooltip("Somente leitura em jogo: número do chamado que criou o defeito (0 = crimpagem errada).")]
    [SerializeField] private int chamado;

    public bool Defeituoso => pinosComFalha.Count > 0;
    public IReadOnlyList<int> PinosComFalha => pinosComFalha;
    public int Chamado => chamado;

    /// <summary>Marca o cabo como defeituoso nos pinos indicados (vazio = todos). Retorna o estado.</summary>
    public static CaboRedeEstado Marcar(GameObject cabo, IEnumerable<int> pinos, int numeroChamado)
    {
        if (cabo == null) return null;
        CaboRedeEstado e = cabo.GetComponent<CaboRedeEstado>();
        if (e == null) e = cabo.AddComponent<CaboRedeEstado>();
        e.pinosComFalha.Clear();
        if (pinos != null) foreach (int p in pinos) if (p >= 0 && p < 8 && !e.pinosComFalha.Contains(p)) e.pinosComFalha.Add(p);
        if (e.pinosComFalha.Count == 0) for (int p = 0; p < 8; p++) e.pinosComFalha.Add(p);
        e.chamado = numeroChamado;
        return e;
    }

    /// <summary>O objeto (ou um pai dele) é um cabo com defeito?</summary>
    public static bool EhDefeituoso(GameObject cabo)
    {
        if (cabo == null) return false;
        CaboRedeEstado e = cabo.GetComponentInParent<CaboRedeEstado>(true);
        return e != null && e.Defeituoso;
    }
}
