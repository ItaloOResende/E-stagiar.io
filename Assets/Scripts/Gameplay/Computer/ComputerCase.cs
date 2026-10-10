using System;
using System.Collections.Generic;
using UnityEngine;

public enum CaseState { Closed, Open }

public enum ScrewRemovalResult { Removed, WrongTool, AlreadyRemoved, InvalidScrew, CaseAlreadyOpen }

public enum CaseOpenResult { Opened, ScrewsRemaining, AlreadyOpen }

/// <summary>
/// Um parafuso da traseira do gabinete. Dado puro (serializado no Inspector do <see cref="ComputerCase"/>).
/// </summary>
[Serializable]
public class ScrewData
{
    [Tooltip("Identificador estável (ex.: 'topo-esq'). Só para depuração/dados de chamado.")]
    public string id;
    [Tooltip("Posição na IMAGEM da traseira, de 0 a 1. X: 0 = esquerda. Y: 0 = TOPO da imagem.")]
    public Vector2 normalizedPosition;
    [Tooltip("Ferramenta exigida para remover.")]
    public ToolType requiredTool = ToolType.Screwdriver;
    [Tooltip("Estado atual (persiste enquanto o objeto existir na cena).")]
    public bool removed;

    public ScrewData() { }

    public ScrewData(string id, float x, float y, ToolType requiredTool = ToolType.Screwdriver)
    {
        this.id = id;
        normalizedPosition = new Vector2(x, y);
        this.requiredTool = requiredTool;
    }
}

/// <summary>
/// Estado lógico de um gabinete de PC: parafusos da traseira e se está aberto.
/// Vai no ROOT LÓGICO do gabinete (nunca no Visual nem na tela 2D): o Canvas só lê e comanda este
/// componente, por isso o estado sobrevive a fechar e reabrir a tela. Todos os parafusos listados são
/// obrigatórios; o interior só é liberado (<see cref="TryOpen"/>) com todos removidos.
/// Sem dependência de modelo, UI, tag ou nome.
/// </summary>
public class ComputerCase : MonoBehaviour
{
    [SerializeField] private List<ScrewData> screws = new List<ScrewData>();
    [SerializeField] private CaseState state = CaseState.Closed;

    /// <summary>Disparado com o índice do parafuso removido.</summary>
    public event Action<int> ScrewRemoved;
    /// <summary>Disparado quando o estado do gabinete muda (fechado → aberto).</summary>
    public event Action<CaseState> StateChanged;

    public CaseState State => state;
    public IReadOnlyList<ScrewData> Screws => screws;
    public int TotalCount => screws.Count;

    public int RemovedCount
    {
        get
        {
            int n = 0;
            for (int i = 0; i < screws.Count; i++) if (screws[i].removed) n++;
            return n;
        }
    }

    public bool AllScrewsRemoved => RemovedCount == screws.Count;
    public bool CanOpen => state == CaseState.Closed && AllScrewsRemoved;

    private void Reset() => Configure(CreateDefaultTowerBackScrews());

    /// <summary>Substitui a lista de parafusos (estado de abertura volta a fechado).</summary>
    public void Configure(IEnumerable<ScrewData> newScrews)
    {
        screws = new List<ScrewData>(newScrews);
        state = CaseState.Closed;
    }

    /// <summary>
    /// Tenta remover um parafuso com a ferramenta que o jogador segura
    /// (<see cref="ToolType.None"/> = mãos vazias ou item sem <see cref="ToolItem"/>).
    /// </summary>
    public ScrewRemovalResult TryRemoveScrew(int index, ToolType heldTool)
    {
        if (index < 0 || index >= screws.Count) return ScrewRemovalResult.InvalidScrew;
        if (state == CaseState.Open) return ScrewRemovalResult.CaseAlreadyOpen;

        ScrewData screw = screws[index];
        if (screw.removed) return ScrewRemovalResult.AlreadyRemoved;
        if (heldTool != screw.requiredTool) return ScrewRemovalResult.WrongTool;

        screw.removed = true;
        ScrewRemoved?.Invoke(index);
        return ScrewRemovalResult.Removed;
    }

    /// <summary>Libera o interior. Só funciona com todos os parafusos removidos.</summary>
    public CaseOpenResult TryOpen()
    {
        if (state == CaseState.Open) return CaseOpenResult.AlreadyOpen;
        if (!AllScrewsRemoved) return CaseOpenResult.ScrewsRemaining;

        state = CaseState.Open;
        StateChanged?.Invoke(state);
        return CaseOpenResult.Opened;
    }

    /// <summary>
    /// Os 8 parafusos reais da traseira do modelo placeholder (<c>pc_back</c>, 710×1580 px): 2 no topo, 2 laterais
    /// altos, 2 laterais no meio e 2 na base. Centros medidos na imagem (px ÷ tamanho; Y a partir do topo).
    /// Outro modelo de gabinete = outra lista no Inspector, sem mudar código.
    /// </summary>
    public static List<ScrewData> CreateDefaultTowerBackScrews()
    {
        const float w = 710f, h = 1580f;
        return new List<ScrewData>
        {
            new ScrewData("topo-esquerda",  118.4f / w,   33.2f / h),
            new ScrewData("topo-direita",   600.6f / w,   36.4f / h),
            new ScrewData("alto-esquerda",   36.9f / w,  149.8f / h),
            new ScrewData("alto-direita",   680.9f / w,  149.8f / h),
            new ScrewData("meio-esquerda",   35.6f / w,  780.3f / h),
            new ScrewData("meio-direita",   679.7f / w,  780.3f / h),
            new ScrewData("base-esquerda",   38.9f / w, 1456.8f / h),
            new ScrewData("base-direita",   679.8f / w, 1456.8f / h),
        };
    }
}
