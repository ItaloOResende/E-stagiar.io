using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Reposição TEMPORÁRIA de estoque (um por cena). Quando não sobra NENHUMA unidade utilizável de um tipo de peça
/// (<see cref="ItemKind"/>) no mundo, devolve a quantidade inicial desse tipo aos lugares de origem:
///  - entradas das gavetas (<see cref="DrawerContents.Restock"/>, nos mesmos lugares do começo);
///  - pontos de peças soltas (<see cref="pontosSoltos"/>, ex.: toner e SSD sobre a bancada), preenchidos pelo setup.
/// "Utilizável" = item ativo no mundo (solto, na mão ou numa gaveta aberta), guardado numa gaveta fechada ou entrega
/// direta restante. NÃO conta: peça instalada em equipamento (desativada), peça quebrada (sem <see cref="ItemKind"/>)
/// e cabo de rede defeituoso. Equipamentos nunca são alterados.
/// Sem duplicação: uma verificação por intervalo, num único componente; repõe só tipos com zero unidades.
/// </summary>
public class ReposicaoEstoque : MonoBehaviour
{
    [Serializable]
    public class PontoSolto
    {
        public PickupInteractable prefab;
        [Tooltip("Pai do item reposto (ex.: a bancada). Vazio = raiz da cena.")]
        public Transform pai;
        public Vector3 posicao;
        public Vector3 rotacao;
    }

    [Tooltip("Intervalo (s) entre as verificações.")]
    [Min(0.2f)] [SerializeField] private float intervalo = 1f;
    [Tooltip("Peças soltas no cenário (fora de gavetas) que também são repostas no lugar original.")]
    [SerializeField] private List<PontoSolto> pontosSoltos = new List<PontoSolto>();

    private readonly Dictionary<string, int> disponiveis = new Dictionary<string, int>();
    private readonly HashSet<PickupInteractable> contados = new HashSet<PickupInteractable>();
    private float proxima;

    /// <summary>Quantas reposições já aconteceram (por tipo), para conferência.</summary>
    public readonly Dictionary<string, int> Reposicoes = new Dictionary<string, int>();

    public IList<PontoSolto> PontosSoltos => pontosSoltos;

    private void Update()
    {
        if (Time.time < proxima) return;
        proxima = Time.time + intervalo;
        Verificar();
    }

    /// <summary>Uma verificação completa (público para testes).</summary>
    public void Verificar()
    {
        DrawerContents[] gavetas = FindObjectsByType<DrawerContents>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        Contar(gavetas);

        var repostos = new HashSet<string>();
        foreach (DrawerContents g in gavetas)
            for (int e = 0; e < g.EntryCount; e++)
            {
                string tipo = ItemKind.Of(g.EntryPrefab(e));
                if (!Repoe(tipo) || Disponivel(tipo) > 0) continue;
                g.Restock(e);
                repostos.Add(tipo);
            }
        foreach (PontoSolto p in pontosSoltos)
        {
            string tipo = p != null ? ItemKind.Of(p.prefab) : null;
            if (!Repoe(tipo) || Disponivel(tipo) > 0) continue;
            Quaternion rot = Quaternion.Euler(p.rotacao);
            PickupInteractable item = p.pai != null
                ? Instantiate(p.prefab, p.pai.TransformPoint(p.posicao), p.pai.rotation * rot, p.pai)
                : Instantiate(p.prefab, p.posicao, rot);
            item.name = p.prefab.name;
            repostos.Add(tipo);
        }
        // só depois de passar por todas as origens: as do mesmo tipo repõem juntas (a quantidade inicial inteira)
        foreach (string tipo in repostos)
        {
            Reposicoes[tipo] = (Reposicoes.TryGetValue(tipo, out int n) ? n : 0) + 1;
            Debug.Log($"[Estoque] '{tipo}' esgotado: quantidade inicial reposta.", this);
        }
    }

    // Só componentes de manutenção (peças, cabos, peças de notebook e impressora). Ferramentas e pendrive não são
    // consumidos: um pendrive preso num PC instalando o sistema não pode gerar outro.
    private static bool Repoe(string tipo) => tipo != null && !tipo.StartsWith("ferramenta:") && tipo != "pendrive";

    private int Disponivel(string tipo) => disponiveis.TryGetValue(tipo, out int n) ? n : 0;

    private void Contar(DrawerContents[] gavetas)
    {
        disponiveis.Clear();
        contados.Clear();
        foreach (PickupInteractable item in PickupInteractable.All) Somar(item);
        foreach (DrawerContents g in gavetas)
        {
            foreach (PickupInteractable item in g.Inside) Somar(item);   // gaveta fechada: itens desativados
            for (int e = 0; e < g.EntryCount; e++)
            {
                string tipo = ItemKind.Of(g.EntryPrefab(e));
                if (tipo != null && g.DirectRemaining(e) > 0) disponiveis[tipo] = Disponivel(tipo) + g.DirectRemaining(e);
            }
        }
    }

    private void Somar(PickupInteractable item)
    {
        if (item == null || !contados.Add(item)) return;
        string tipo = ItemKind.Of(item);
        if (tipo == null || CaboRedeEstado.EhDefeituoso(item.gameObject)) return;
        disponiveis[tipo] = Disponivel(tipo) + 1;
    }
}
