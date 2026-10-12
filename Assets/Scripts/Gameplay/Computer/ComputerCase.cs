using System;
using System.Collections.Generic;
using UnityEngine;

public enum CaseState { Closed, Open }

public enum ScrewRemovalResult { Removed, WrongTool, AlreadyRemoved, InvalidScrew, CaseAlreadyOpen }

public enum CaseOpenResult { Opened, ScrewsRemaining, AlreadyOpen }

/// <summary>Componentes internos que um gabinete pode ter (cada um presente ou ausente de forma independente).</summary>
[Flags]
public enum PcComponent { None = 0, Motherboard = 1, Ram = 2, Hdd = 4 }

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
    [Tooltip("Obrigatório para abrir a tampa. Os opcionais podem ser removidos, mas não são exigidos.")]
    public bool required = true;
    [Tooltip("Ferramenta exigida para remover.")]
    public ToolType requiredTool = ToolType.Screwdriver;
    [Tooltip("Estado atual (persiste enquanto o objeto existir na cena).")]
    public bool removed;

    public ScrewData() { }

    public ScrewData(string id, float x, float y, bool required = true, ToolType requiredTool = ToolType.Screwdriver)
    {
        this.id = id;
        normalizedPosition = new Vector2(x, y);
        this.required = required;
        this.requiredTool = requiredTool;
    }
}

/// <summary>
/// Estado lógico de um gabinete de PC: parafusos da traseira, se está aberto e quais componentes há dentro.
/// Vai no ROOT LÓGICO do gabinete (nunca no Visual nem na tela 2D): o Canvas só lê e comanda este
/// componente, por isso o estado sobrevive a fechar e reabrir a tela. O interior só é liberado
/// (<see cref="TryOpen"/>) com todos os parafusos marcados como obrigatórios removidos; os opcionais não contam.
/// Os componentes internos são sorteados uma única vez por instância (ou fixados no Inspector).
/// Sem dependência de modelo, UI, tag ou nome.
/// </summary>
public class ComputerCase : MonoBehaviour
{
    [SerializeField] private List<ScrewData> screws = new List<ScrewData>();
    [SerializeField] private CaseState state = CaseState.Closed;

    [Header("Componentes internos")]
    [Tooltip("Ligado: sorteia, uma vez por instância, se cada componente está presente. Desligado: usa 'Fixed Components'.")]
    [SerializeField] private bool randomizeComponents = true;
    [Tooltip("Chance (0 a 1) de cada componente estar presente, sorteada de forma independente.")]
    [Range(0f, 1f)] [SerializeField] private float componentChance = 0.5f;
    [Tooltip("Semente do sorteio. 0 = semente nova por instância (cada PC diferente). Outro valor = resultado reproduzível.")]
    [SerializeField] private int componentSeed;
    [Tooltip("Componentes usados quando o sorteio está desligado.")]
    [SerializeField] private PcComponent fixedComponents = PcComponent.Motherboard | PcComponent.Ram | PcComponent.Hdd;

    private PcComponent components;
    private bool componentsResolved;

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

    /// <summary>Todos os parafusos obrigatórios foram removidos (os opcionais não importam).</summary>
    public bool RequiredScrewsRemoved
    {
        get
        {
            for (int i = 0; i < screws.Count; i++)
                if (screws[i].required && !screws[i].removed) return false;
            return true;
        }
    }

    public bool CanOpen => state == CaseState.Closed && RequiredScrewsRemoved;

    /// <summary>
    /// Componentes presentes. Resolvidos uma vez (no Awake ou na primeira consulta) e estáveis daí em diante:
    /// abrir/fechar a tela, navegar entre as vistas ou pegar o gabinete não sorteia de novo.
    /// </summary>
    public PcComponent Components
    {
        get
        {
            if (!componentsResolved) ResolveComponents();
            return components;
        }
    }

    public bool HasComponent(PcComponent component) => component != PcComponent.None && (Components & component) == component;

    private void Reset() => Configure(CreateDefaultTowerBackScrews());

    private void Awake()
    {
        if (!componentsResolved) ResolveComponents();
    }

    private void ResolveComponents()
    {
        components = randomizeComponents
            ? RollComponents(componentSeed != 0 ? componentSeed : UnityEngine.Random.Range(1, int.MaxValue), componentChance)
            : fixedComponents;
        componentsResolved = true;
    }

    /// <summary>Fixa os componentes desta instância (ex.: configuração de um chamado), sem sorteio.</summary>
    public void SetComponents(PcComponent value)
    {
        components = value;
        componentsResolved = true;
    }

    /// <summary>Sorteio independente de cada componente; mesma semente = mesmo resultado.</summary>
    public static PcComponent RollComponents(int seed, float chance)
    {
        var rng = new System.Random(seed);
        PcComponent result = PcComponent.None;
        if (rng.NextDouble() < chance) result |= PcComponent.Motherboard;
        if (rng.NextDouble() < chance) result |= PcComponent.Ram;
        if (rng.NextDouble() < chance) result |= PcComponent.Hdd;
        return result;
    }

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

    /// <summary>Libera o interior. Só funciona com todos os parafusos obrigatórios removidos.</summary>
    public CaseOpenResult TryOpen()
    {
        if (state == CaseState.Open) return CaseOpenResult.AlreadyOpen;
        if (!RequiredScrewsRemoved) return CaseOpenResult.ScrewsRemaining;

        state = CaseState.Open;
        StateChanged?.Invoke(state);
        return CaseOpenResult.Opened;
    }

    /// <summary>
    /// Os 8 parafusos reais da traseira do modelo placeholder (<c>pc_back</c>, 710×1580 px): 2 no topo, 2 laterais
    /// altos, 2 laterais no meio e 2 na base. Centros medidos na imagem (px ÷ tamanho; Y a partir do topo).
    /// Só os 3 do lado DIREITO (que prendem a tampa) são obrigatórios; os do topo e do lado esquerdo são opcionais.
    /// Outro modelo de gabinete = outra lista no Inspector, sem mudar código.
    /// </summary>
    public static List<ScrewData> CreateDefaultTowerBackScrews()
    {
        const float w = 710f, h = 1580f;
        return new List<ScrewData>
        {
            new ScrewData("topo-esquerda",  118.4f / w,   33.2f / h, required: false),
            new ScrewData("topo-direita",   600.6f / w,   36.4f / h, required: false),
            new ScrewData("alto-esquerda",   36.9f / w,  149.8f / h, required: false),
            new ScrewData("alto-direita",   680.9f / w,  149.8f / h, required: true),
            new ScrewData("meio-esquerda",   35.6f / w,  780.3f / h, required: false),
            new ScrewData("meio-direita",   679.7f / w,  780.3f / h, required: true),
            new ScrewData("base-esquerda",   38.9f / w, 1456.8f / h, required: false),
            new ScrewData("base-direita",   679.8f / w, 1456.8f / h, required: true),
        };
    }
}
