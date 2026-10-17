using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Configuração de CENA (ex.: escritório de testes) para variar os tipos de chamado. Sem ela, o
/// <see cref="TicketManager"/> sorteia uniformemente entre os chamados elegíveis, e como 9 dos 12 do catálogo são de PC,
/// ~75% dos chamados são de PC e cada minigame aparece ~8% das vezes.
/// Com ela: cada ciclo embaralha os <see cref="groups"/> e entrega UM chamado de cada grupo (dentro do grupo, sorteio
/// evitando repetir o anterior). Assim, a cada N chamados (N = grupos com chamado elegível) todos os tipos aparecem.
/// Usa só o que o TicketManager já filtrou (fase, tutorial, etapas): as regras da Fernanda continuam valendo.
/// Remover o componente volta ao sorteio normal.
/// </summary>
[RequireComponent(typeof(TicketManager))]
public class TicketCategoryRotation : MonoBehaviour
{
    [Serializable]
    public class Group
    {
        public string name = "Grupo";
        [Tooltip("Ticket Ids deste tipo de atendimento.")]
        public List<string> ticketIds = new List<string>();
    }

    [SerializeField] private List<Group> groups = new List<Group>();

    private readonly List<Group> deck = new List<Group>();
    private TicketManager manager;

    private void OnEnable()
    {
        manager = GetComponent<TicketManager>();
        manager.SelectionOverride = Choose;
    }

    private void OnDisable()
    {
        if (manager != null && manager.SelectionOverride == (Func<List<TicketDefinition>, TicketDefinition, TicketDefinition>)Choose)
            manager.SelectionOverride = null;
    }

    private TicketDefinition Choose(List<TicketDefinition> eligible, TicketDefinition previous)
    {
        for (int attempt = 0; attempt <= groups.Count; attempt++)
        {
            if (deck.Count == 0) Refill();
            if (deck.Count == 0) return null;
            Group g = deck[deck.Count - 1];
            deck.RemoveAt(deck.Count - 1);
            var options = eligible.FindAll(d => g.ticketIds.Contains(d.ticketId));
            if (options.Count == 0) continue;   // grupo sem chamado elegível nesta fase: próximo
            if (options.Count > 1) options.Remove(previous);
            return options[UnityEngine.Random.Range(0, options.Count)];
        }
        return null;   // nenhum grupo serve: sorteio padrão do TicketManager
    }

    private void Refill()
    {
        deck.AddRange(groups);
        for (int i = deck.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (deck[i], deck[j]) = (deck[j], deck[i]);
        }
    }
}
