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
/// Dentro de um grupo, <see cref="Group.subgroups"/> (opcional) faz o mesmo rodízio um nível abaixo: cada vez que o grupo
/// sai, vem um subgrupo diferente, sem repetir até todos terem saído e nunca o mesmo duas vezes seguidas (ex.: Rede =
/// crimpagem → rack → cabo defeituoso; PC = peças ↔ cabos). Os pesos entre os grupos não mudam.
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
        [Tooltip("Opcional: subdivisões do grupo, em rodízio sem repetição. Vazio = sorteio entre os Ticket Ids do grupo.")]
        public List<Subgroup> subgroups = new List<Subgroup>();
    }

    [Serializable]
    public class Subgroup
    {
        public string name = "Subgrupo";
        public List<string> ticketIds = new List<string>();
    }

    [SerializeField] private List<Group> groups = new List<Group>();

    private readonly List<Group> deck = new List<Group>();
    private readonly Dictionary<Group, List<Subgroup>> subDecks = new Dictionary<Group, List<Subgroup>>();
    private readonly Dictionary<Group, Subgroup> lastSub = new Dictionary<Group, Subgroup>();
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
            Subgroup sub = NextSubgroup(g, eligible);
            if (sub != null) options = options.FindAll(d => sub.ticketIds.Contains(d.ticketId));
            if (options.Count > 1) options.Remove(previous);
            return options[UnityEngine.Random.Range(0, options.Count)];
        }
        return null;   // nenhum grupo serve: sorteio padrão do TicketManager
    }

    // Próximo subgrupo do grupo (só os que têm chamado elegível). Baralho sem repetição; ao reembaralhar, o primeiro
    // nunca é o último que saiu.
    private Subgroup NextSubgroup(Group g, List<TicketDefinition> eligible)
    {
        if (g.subgroups == null || g.subgroups.Count == 0) return null;
        var validos = g.subgroups.FindAll(s => s != null && eligible.Exists(d => s.ticketIds.Contains(d.ticketId)));
        if (validos.Count == 0) return null;
        if (!subDecks.TryGetValue(g, out List<Subgroup> sd)) subDecks[g] = sd = new List<Subgroup>();
        sd.RemoveAll(s => !validos.Contains(s));
        if (sd.Count == 0)
        {
            sd.AddRange(validos);
            Shuffle(sd);
            // o baralho é tirado do fim: o próximo a sair não pode repetir o anterior
            if (sd.Count > 1 && lastSub.TryGetValue(g, out Subgroup ultimo) && sd[sd.Count - 1] == ultimo)
                (sd[0], sd[sd.Count - 1]) = (sd[sd.Count - 1], sd[0]);
        }
        Subgroup escolhido = sd[sd.Count - 1];
        sd.RemoveAt(sd.Count - 1);
        lastSub[g] = escolhido;
        return escolhido;
    }

    private static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private void Refill()
    {
        deck.AddRange(groups);
        Shuffle(deck);
    }
}
