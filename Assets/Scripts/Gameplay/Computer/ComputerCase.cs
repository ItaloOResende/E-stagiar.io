using System;
using System.Collections.Generic;
using UnityEngine;

public enum CaseState { Closed, Open }

/// <summary>Valores novos sempre no fim. NotOnWorkSurface: gabinete fora de mesa/bancada (nada muda).</summary>
public enum ScrewRemovalResult { Removed, WrongTool, AlreadyRemoved, InvalidScrew, CaseAlreadyOpen, NotOnWorkSurface }

/// <summary>Resultado de recolocar um parafuso (<see cref="ComputerCase.TryInstallScrew"/>).</summary>
public enum ScrewInstallResult { Installed, WrongTool, AlreadyInstalled, InvalidScrew, NotOnWorkSurface }

/// <summary>Resultado de abrir a tampa (<see cref="ComputerCase.TryOpen"/>). Valores novos sempre no fim.</summary>
public enum CaseOpenResult
{
    Opened,
    ScrewsRemaining,
    AlreadyOpen,
    /// <summary>O gabinete não está apoiado numa superfície de trabalho (<see cref="WorkSurface"/>): chão, gaveteiro, mão...</summary>
    NotOnWorkSurface
}

/// <summary>Resultado de clicar num encaixe de peça do interior (<see cref="ComputerCase.UsePartSlot"/>).</summary>
public enum PartSlotResult
{
    /// <summary>A peça da mão foi instalada (o chamador tira o item da mão e o guarda).</summary>
    Installed,
    /// <summary>A peça foi retirada; o item volta para a mão.</summary>
    Removed,
    /// <summary>O gabinete está fechado.</summary>
    CaseClosed,
    /// <summary>Encaixe vazio e nada (ou não uma peça) na mão.</summary>
    NeedsPart,
    /// <summary>Encaixe vazio e a peça da mão é de outro tipo.</summary>
    WrongPart,
    /// <summary>Para retirar uma peça, a mão precisa estar livre.</summary>
    HandsFull,
    /// <summary>A peça veio no gabinete e não há prefab configurado para devolvê-la à mão.</summary>
    NoItemToReturn,
    /// <summary>A peça encaixa em outra que ainda não está instalada (RAM e cooler vão na placa-mãe).</summary>
    MissingBase,
    /// <summary>Há peças montadas sobre esta (tire a RAM e o cooler antes da placa-mãe).</summary>
    BlockedByParts,
    Invalid,
    /// <summary>A peça da mão está quebrada (caiu demais): não é instalada. Valor novo no fim (prefabs guardam o número).</summary>
    BrokenPart,
    /// <summary>O gabinete não está numa mesa/bancada (<see cref="WorkSurface"/>): nada muda.</summary>
    NotOnWorkSurface
}

/// <summary>Componentes internos que um gabinete pode ter (cada um presente ou ausente de forma independente).</summary>
[Flags]
public enum PcComponent { None = 0, Motherboard = 1, Ram = 2, Hdd = 4, Psu = 8, CpuCooler = 16 }

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
/// Fechar: recolocando os parafusos (<see cref="TryInstallScrew"/>); com todos os obrigatórios de volta, o gabinete
/// aberto volta a ficar fechado (os opcionais também não contam para fechar).
/// Os componentes internos são sorteados uma única vez por instância (ou fixados no Inspector).
/// Aberto ou sem parafusos, o gabinete continua podendo ser carregado; concluir um chamado assim conta como gabinete
/// incompleto (<see cref="IsIncomplete"/>), o que a pontuação pode penalizar.
/// Abrir também exige o gabinete APOIADO numa superfície de trabalho marcada com <see cref="WorkSurface"/> (mesa,
/// bancada): no chão ou em outro móvel não há manutenção nenhuma (<see cref="IsOnWorkSurface"/>): a tela 2D não abre
/// (<see cref="IScreenBlocker"/>) e parafusos, tampa e peças recusam (<c>NotOnWorkSurface</c>). Fechado, pode ser carregado.
/// Sem dependência de modelo, UI, tag ou nome.
/// </summary>
public class ComputerCase : MonoBehaviour, IScreenBlocker
{
    [SerializeField] private List<ScrewData> screws = new List<ScrewData>();
    [SerializeField] private CaseState state = CaseState.Closed;

    [Header("Onde pode ser aberto")]
    [Tooltip("Ligado: a tampa só abre com o gabinete apoiado sobre um objeto com WorkSurface (mesa, bancada). " +
             "Desligado: abre em qualquer lugar (como antes; só para cenários/testes).")]
    [SerializeField] private bool requiresWorkSurface = true;
    [Tooltip("Folga (m) abaixo da base do gabinete em que o apoio ainda conta (o gabinete pousado fica a ~1 cm do tampo).")]
    [Min(0.005f)] [SerializeField] private float surfaceTolerance = 0.05f;
    [Tooltip("Inclinação mínima do apoio (normal.y; 1 = plano). Abaixo disso não conta como apoiado.")]
    [Range(0f, 1f)] [SerializeField] private float minSurfaceUp = 0.7f;

    [Header("Componentes internos")]
    [Tooltip("Ligado: sorteia, uma vez por instância, se cada componente está presente. Desligado: usa 'Fixed Components'.")]
    [SerializeField] private bool randomizeComponents = true;
    [Tooltip("Chance (0 a 1) de cada componente estar presente, sorteada de forma independente.")]
    [Range(0f, 1f)] [SerializeField] private float componentChance = 0.5f;
    [Tooltip("Semente do sorteio. 0 = semente nova por instância (cada PC diferente). Outro valor = resultado reproduzível.")]
    [SerializeField] private int componentSeed;
    [Tooltip("Componentes usados quando o sorteio está desligado.")]
    [SerializeField] private PcComponent fixedComponents = AllComponents;
    [Tooltip("O HD que já vem no gabinete (sorteado/fixo) tem o sistema operacional instalado. Ligado = PC pronto para usar; " +
             "desligado = o jogador precisa instalar o sistema pelo pendrive. HDs novos (gaveta) sempre vêm vazios.")]
    [SerializeField] private bool startingHddHasOperatingSystem = true;

    [Header("Itens devolvidos ao retirar peças que já vieram no gabinete")]
    [Tooltip("Prefab (Pickup + ComputerPartItem) criado UMA vez quando o jogador retira a placa-mãe sorteada/fixa. Vazio = não dá para retirar.")]
    [SerializeField] private GameObject motherboardItemPrefab;
    [SerializeField] private GameObject ramItemPrefab;
    [SerializeField] private GameObject hddItemPrefab;
    [SerializeField] private GameObject psuItemPrefab;
    [SerializeField] private GameObject coolerItemPrefab;

    /// <summary>Todos os componentes instaláveis (um encaixe para cada).</summary>
    public const PcComponent AllComponents =
        PcComponent.Motherboard | PcComponent.Ram | PcComponent.Hdd | PcComponent.Psu | PcComponent.CpuCooler;

    private PcComponent components;
    private bool componentsResolved;
    // Objeto físico de cada peça instalada pelo jogador (desativado enquanto está no gabinete).
    private readonly Dictionary<PcComponent, GameObject> installedItems = new Dictionary<PcComponent, GameObject>();
    // Sistema no HD que veio no gabinete (sem objeto até ser retirado).
    private bool builtInHddHasOs;
    private bool builtInHddResolved;

    /// <summary>Disparado com o índice do parafuso removido.</summary>
    public event Action<int> ScrewRemoved;
    /// <summary>Disparado com o índice do parafuso recolocado.</summary>
    public event Action<int> ScrewInstalled;
    /// <summary>Disparado quando o estado do gabinete muda (fechado → aberto, ou aberto → fechado ao recolocar os parafusos).</summary>
    public event Action<CaseState> StateChanged;
    /// <summary>Disparado quando uma peça é instalada ou retirada (ou os componentes são redefinidos).</summary>
    public event Action ComponentsChanged;

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

    /// <summary>Todos os parafusos obrigatórios estão no lugar (os opcionais não importam).</summary>
    public bool RequiredScrewsInstalled
    {
        get
        {
            for (int i = 0; i < screws.Count; i++)
                if (screws[i].required && screws[i].removed) return false;
            return true;
        }
    }

    public bool CanOpen => state == CaseState.Closed && RequiredScrewsRemoved && (!requiresWorkSurface || IsOnWorkSurface);

    /// <summary>Ligado: abrir exige <see cref="IsOnWorkSurface"/>.</summary>
    public bool RequiresWorkSurface
    {
        get => requiresWorkSurface;
        set => requiresWorkSurface = value;
    }

    /// <summary>
    /// O gabinete está apoiado sobre uma superfície de trabalho (collider de um objeto com <see cref="WorkSurface"/>
    /// ativo, ou de um filho dele): lança a base do gabinete um pouco para baixo (<see cref="surfaceTolerance"/>) e
    /// olha o primeiro apoio que encontrar. Apoio de outro tipo (chão, gaveteiro, cadeira) = falso. Sem colliders
    /// ativos (ex.: na mão do jogador) = falso. Não depende de nome, tag nem altura.
    /// </summary>
    public bool IsOnWorkSurface => SupportingSurface() != null;

    /// <summary>Superfície de trabalho que apoia o gabinete agora (null = nenhuma). Ver <see cref="IsOnWorkSurface"/>.</summary>
    public WorkSurface SupportingSurface()
    {
        bool any = false;
        Bounds b = default;
        foreach (Collider c in GetComponentsInChildren<Collider>())
        {
            if (!c.enabled || c.isTrigger) continue;
            if (!any) { b = c.bounds; any = true; }
            else b.Encapsulate(c.bounds);
        }
        if (!any) return null;

        // caixa fina na base (um pouco menor que a pegada, para não pegar a parede ou o móvel vizinho), descendo
        Vector3 half = new Vector3(b.extents.x * 0.8f, 0.005f, b.extents.z * 0.8f);
        Vector3 origin = new Vector3(b.center.x, b.min.y + 0.02f, b.center.z);
        RaycastHit[] hits = Physics.BoxCastAll(origin, half, Vector3.down, Quaternion.identity, 0.02f + surfaceTolerance,
                                               ~0, QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));
        foreach (RaycastHit h in hits)
        {
            if (h.collider.transform.IsChildOf(transform)) continue;                 // o próprio gabinete
            // objetos móveis (monitor, teclado, cabos, peças: têm Rigidbody, parados ou não) não são apoio: encostar
            // neles (ex.: monitor ao lado, de qualquer lado) não pode esconder a mesa que está embaixo
            if (h.collider.attachedRigidbody != null) continue;
            WorkSurface surface = h.collider.GetComponentInParent<WorkSurface>();
            // começou encostado (distância 0): a normal não vale; conta o objeto que está embaixo
            bool flat = h.distance <= 0f || h.normal.y >= minSurfaceUp;
            if (surface != null && surface.AllowsCaseMaintenance && flat) return surface;
            if (h.distance > 0f) return null;                                        // apoiado em outra coisa (chão, gaveteiro)
        }
        return null;
    }

    /// <summary>Computador incompleto: tampa aberta ou algum parafuso obrigatório fora (os opcionais não contam).</summary>
    public bool IsIncomplete => state == CaseState.Open || !RequiredScrewsInstalled;

    /// <summary>Fora de uma mesa/bancada a tela de manutenção não abre (com <see cref="RequiresWorkSurface"/>).</summary>
    public bool BlocksScreen => !MaintenanceAllowed;

    /// <summary>Manutenção permitida agora: sem exigência de superfície, ou apoiado numa <see cref="WorkSurface"/>.</summary>
    public bool MaintenanceAllowed => !requiresWorkSurface || IsOnWorkSurface;

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

    /// <summary>Há um HD instalado e o sistema operacional está gravado nele.</summary>
    public bool HddHasOperatingSystem
    {
        get
        {
            if (!HasComponent(PcComponent.Hdd)) return false;
            if (installedItems.TryGetValue(PcComponent.Hdd, out GameObject item) && item != null)
                return item.TryGetComponent(out ComputerPartItem part) && part.HasOperatingSystem && !part.IsBroken;  // HD quebrado não dá boot
            return BuiltInHddHasOs;
        }
    }

    private bool BuiltInHddHasOs
    {
        get
        {
            if (!builtInHddResolved) { builtInHddHasOs = startingHddHasOperatingSystem; builtInHddResolved = true; }
            return builtInHddHasOs;
        }
    }

    /// <summary>Grava o sistema operacional no HD instalado. Falso se não houver HD.</summary>
    public bool InstallOperatingSystem()
    {
        if (!HasComponent(PcComponent.Hdd)) return false;
        if (installedItems.TryGetValue(PcComponent.Hdd, out GameObject item) && item != null)
        {
            if (!item.TryGetComponent(out ComputerPartItem part)) return false;
            part.HasOperatingSystem = true;
        }
        else
        {
            builtInHddHasOs = true;
            builtInHddResolved = true;
        }
        return true;
    }

    /// <summary>Um único componente instalável (não None nem combinação).</summary>
    public static bool IsSingleComponent(PcComponent c) =>
        c != PcComponent.None && (c & AllComponents) == c && (c & (c - 1)) == 0;

    /// <summary>Peça sobre a qual este componente é montado (RAM e cooler: placa-mãe). None = encaixe próprio no gabinete.</summary>
    public static PcComponent BaseOf(PcComponent c) =>
        c == PcComponent.Ram || c == PcComponent.CpuCooler ? PcComponent.Motherboard : PcComponent.None;

    /// <summary>Remove peças sem a base onde são montadas (ex.: RAM sem placa-mãe).</summary>
    public static PcComponent Sanitize(PcComponent value)
    {
        value &= AllComponents;
        if ((value & PcComponent.Motherboard) == 0) value &= ~(PcComponent.Ram | PcComponent.CpuCooler);
        return value;
    }

    /// <summary>O encaixe pode ser usado agora (a base dele, se houver, está instalada).</summary>
    public bool IsSlotAvailable(PcComponent slot) => BaseOf(slot) == PcComponent.None || HasComponent(BaseOf(slot));

    private void Reset() => Configure(CreateDefaultTowerBackScrews());

    private void Awake()
    {
        if (!componentsResolved) ResolveComponents();
    }

    private void ResolveComponents()
    {
        components = randomizeComponents
            ? RollComponents(componentSeed != 0 ? componentSeed : UnityEngine.Random.Range(1, int.MaxValue), componentChance)
            : Sanitize(fixedComponents);
        componentsResolved = true;
    }

    /// <summary>Fixa os componentes desta instância (ex.: configuração de um chamado), sem sorteio.</summary>
    public void SetComponents(PcComponent value) => SetComponents(value, startingHddHasOperatingSystem);

    /// <summary>Fixa os componentes e se o HD que vem no gabinete já tem o sistema operacional (cenário/chamado).</summary>
    public void SetComponents(PcComponent value, bool hddHasOperatingSystem)
    {
        builtInHddHasOs = hddHasOperatingSystem;
        builtInHddResolved = true;
        components = Sanitize(value);
        componentsResolved = true;
        installedItems.Clear();
        ComponentsChanged?.Invoke();
    }

    /// <summary>
    /// Clique num encaixe do interior (<paramref name="slot"/> = um único componente).
    /// Encaixe vazio + peça certa na mão → instala (o chamador tira <paramref name="heldPart"/> da mão; o objeto fica
    /// guardado aqui). Encaixe ocupado + mão livre → retira e devolve em <paramref name="itemForHand"/> o MESMO objeto
    /// que foi instalado (ou, para peças que vieram no gabinete, uma instância do prefab configurado).
    /// Só funciona com o gabinete aberto.
    /// </summary>
    public PartSlotResult UsePartSlot(PcComponent slot, ComputerPartItem heldPart, bool holdingSomething, out GameObject itemForHand)
    {
        itemForHand = null;
        if (!IsSingleComponent(slot)) return PartSlotResult.Invalid;
        if (state != CaseState.Open) return PartSlotResult.CaseClosed;
        if (!MaintenanceAllowed) return PartSlotResult.NotOnWorkSurface;

        if (HasComponent(slot))
        {
            if (holdingSomething) return PartSlotResult.HandsFull;
            if (slot == PcComponent.Motherboard && (HasComponent(PcComponent.Ram) || HasComponent(PcComponent.CpuCooler)))
                return PartSlotResult.BlockedByParts;
            if (!installedItems.TryGetValue(slot, out GameObject item) || item == null)
            {
                GameObject prefab = ItemPrefabFor(slot);
                if (prefab == null) return PartSlotResult.NoItemToReturn;
                item = Instantiate(prefab, transform.position, Quaternion.identity);
                // O HD que veio no gabinete leva o sistema com ele (o sistema é da unidade, não do PC).
                if (slot == PcComponent.Hdd && item.TryGetComponent(out ComputerPartItem part))
                    part.HasOperatingSystem = BuiltInHddHasOs;
            }
            installedItems.Remove(slot);
            components &= ~slot;
            itemForHand = item;
            ComponentsChanged?.Invoke();
            return PartSlotResult.Removed;
        }

        if (heldPart == null) return PartSlotResult.NeedsPart;
        if (heldPart.Component != slot) return PartSlotResult.WrongPart;
        if (heldPart.IsBroken) return PartSlotResult.BrokenPart;
        if (!IsSlotAvailable(slot)) return PartSlotResult.MissingBase;

        components |= slot;
        installedItems[slot] = heldPart.gameObject;
        ComponentsChanged?.Invoke();
        return PartSlotResult.Installed;
    }

    private GameObject ItemPrefabFor(PcComponent slot)
    {
        switch (slot)
        {
            case PcComponent.Motherboard: return motherboardItemPrefab;
            case PcComponent.Ram: return ramItemPrefab;
            case PcComponent.Hdd: return hddItemPrefab;
            case PcComponent.Psu: return psuItemPrefab;
            case PcComponent.CpuCooler: return coolerItemPrefab;
            default: return null;
        }
    }

    /// <summary>
    /// Sorteio independente de cada componente; mesma semente = mesmo resultado. Peças que dependem da placa-mãe
    /// (RAM, cooler) só ficam se ela também for sorteada.
    /// </summary>
    public static PcComponent RollComponents(int seed, float chance)
    {
        var rng = new System.Random(seed);
        PcComponent result = PcComponent.None;
        if (rng.NextDouble() < chance) result |= PcComponent.Motherboard;
        if (rng.NextDouble() < chance) result |= PcComponent.Ram;
        if (rng.NextDouble() < chance) result |= PcComponent.Hdd;
        if (rng.NextDouble() < chance) result |= PcComponent.Psu;
        if (rng.NextDouble() < chance) result |= PcComponent.CpuCooler;
        return Sanitize(result);
    }

    /// <summary>Substitui a lista de parafusos (estado de abertura volta a fechado).</summary>
    public void Configure(IEnumerable<ScrewData> newScrews)
    {
        screws = new List<ScrewData>(newScrews);
        state = CaseState.Closed;
    }

    /// <summary>
    /// Tenta remover um parafuso com a ferramenta que o jogador segura
    /// (<see cref="ToolType.None"/> = mãos vazias ou item sem <see cref="ToolItem"/>). Com o gabinete aberto nenhum
    /// parafuso sai (como antes); recolocá-los é <see cref="TryInstallScrew"/>.
    /// </summary>
    public ScrewRemovalResult TryRemoveScrew(int index, ToolType heldTool)
    {
        if (index < 0 || index >= screws.Count) return ScrewRemovalResult.InvalidScrew;
        if (state == CaseState.Open) return ScrewRemovalResult.CaseAlreadyOpen;
        if (!MaintenanceAllowed) return ScrewRemovalResult.NotOnWorkSurface;

        ScrewData screw = screws[index];
        if (screw.removed) return ScrewRemovalResult.AlreadyRemoved;
        if (heldTool != screw.requiredTool) return ScrewRemovalResult.WrongTool;

        screw.removed = true;
        ScrewRemoved?.Invoke(index);
        return ScrewRemovalResult.Removed;
    }

    /// <summary>
    /// Recoloca um parafuso já removido, com a ferramenta exigida por ele. Funciona com o gabinete aberto ou fechado.
    /// Aberto: quando o último obrigatório volta, o gabinete fecha (<see cref="StateChanged"/>) e pode ser carregado de novo.
    /// </summary>
    public ScrewInstallResult TryInstallScrew(int index, ToolType heldTool)
    {
        if (index < 0 || index >= screws.Count) return ScrewInstallResult.InvalidScrew;
        ScrewData screw = screws[index];
        if (!screw.removed) return ScrewInstallResult.AlreadyInstalled;
        if (!MaintenanceAllowed) return ScrewInstallResult.NotOnWorkSurface;
        if (heldTool != screw.requiredTool) return ScrewInstallResult.WrongTool;

        screw.removed = false;
        ScrewInstalled?.Invoke(index);
        if (state == CaseState.Open && RequiredScrewsInstalled)
        {
            state = CaseState.Closed;
            StateChanged?.Invoke(state);
        }
        return ScrewInstallResult.Installed;
    }

    /// <summary>
    /// Libera o interior. Só funciona com todos os parafusos obrigatórios removidos e (com
    /// <see cref="RequiresWorkSurface"/>) o gabinete apoiado numa superfície de trabalho.
    /// </summary>
    public CaseOpenResult TryOpen()
    {
        if (state == CaseState.Open) return CaseOpenResult.AlreadyOpen;
        if (!RequiredScrewsRemoved) return CaseOpenResult.ScrewsRemaining;
        if (requiresWorkSurface && !IsOnWorkSurface) return CaseOpenResult.NotOnWorkSurface;

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
