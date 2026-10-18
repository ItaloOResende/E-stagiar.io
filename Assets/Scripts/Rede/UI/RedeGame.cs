using EstagiarIo.Rede;
using UnityEngine;

/// <summary>
/// Estado do minigame de rede durante a partida: quantos cabos já foram crimpados e a TOPOLOGIA real do rack
/// (<see cref="RedeRackTopologia"/>: qualquer cabo entre quaisquer duas portas, inclusive ligações erradas — P N ↔ S M,
/// P ↔ P, S ↔ S). Fica num objeto da própria cena (criado pelo setup) para o estado ser por partida.
/// A tela de crimpagem e a tela do rack leem/escrevem aqui; os PCs (<see cref="RedeConectividade"/>) e os chamados leem
/// <see cref="PortaFunciona"/> — uma conexão física em portas incompatíveis não dá rede.
/// </summary>
public class RedeGame : MonoBehaviour
{
    [Tooltip("Quantas portas tem o rack (patch panel e switch).")]
    [SerializeField] private int portasDoRack = 6;

    private int cabosCrimpados;
    private RedeRackTopologia topologia;

    public static RedeGame Instance { get; private set; }

    public int PortasDoRack => portasDoRack;
    public int CabosDisponiveis => cabosCrimpados;
    /// <summary>Topologia atual do rack (todas as ligações, certas e erradas).</summary>
    public RedeRackTopologia Topologia => topologia ??= new RedeRackTopologia(portasDoRack);
    /// <summary>Total de cabos crimpados com sucesso e de pares CERTOS ligados no rack desde o início (só crescem).</summary>
    public int TotalCrimpados { get; private set; }
    public int TotalParesLigados { get; private set; }
    /// <summary>Pares CERTOS (P N ↔ S N) ligados com um cabo BOM feito na crimpagem (<see cref="CaboCrimpado"/>).</summary>
    public int TotalParesComCaboCrimpado { get; private set; }

    /// <summary>O rack mudou (cabo ligado ou desligado).</summary>
    public event System.Action Mudou;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Acha o estado na cena; cria um se ainda não existir (não deveria acontecer: o setup cria).</summary>
    public static RedeGame Garantir()
    {
        if (Instance != null) return Instance;
        Instance = FindFirstObjectByType<RedeGame>();
        if (Instance == null) Instance = new GameObject("RedeGame").AddComponent<RedeGame>();
        return Instance;
    }

    /// <summary>
    /// Um cabo crimpado com sucesso vira um cabo disponível para ligar no rack. Só conta com a crimpagem CONCLUÍDA na
    /// sequência (cortar → descascar → ordenar → RJ45 → crimpar): qualquer outro estado é recusado (false).
    /// </summary>
    public bool RegistrarCabo(RedeCrimpSession sessao)
    {
        if (sessao == null || sessao.Etapa != RedeCrimpEtapa.Concluido)
        {
            Debug.LogWarning("RedeGame: cabo recusado — a crimpagem não terminou na sequência correta.", this);
            return false;
        }
        cabosCrimpados++;
        TotalCrimpados++;
        return true;
    }

    // ------------------------------------------------------------------ ligações

    /// <summary>
    /// Liga o cabo <paramref name="cabo"/> entre duas portas quaisquer (o jogador pode errar: nada é forçado a casar).
    /// Recusa só o fisicamente impossível (mesma porta, porta ocupada). Devolve a ligação criada (null = recusada).
    /// </summary>
    public LigacaoRack Ligar(PortaRack a, PortaRack b, PickupInteractable cabo)
    {
        bool crimpado = cabo != null && cabo.GetComponentInParent<CaboCrimpado>(true) != null;
        bool defeituoso = cabo != null && CaboRedeEstado.EhDefeituoso(cabo.gameObject);
        LigacaoRack l = Topologia.Ligar(a, b, crimpado, defeituoso, cabo, Time.time);
        if (l == null) return null;
        if (l.Correta)
        {
            TotalParesLigados++;
            if (crimpado && !defeituoso) TotalParesComCaboCrimpado++;
        }
        Debug.Log($"[Rack] cabo ligado {l}" + (l.Correta ? "" : " (fora da topologia)") + (defeituoso ? " com defeito" : "") + ".", this);
        Avisar();
        return l;
    }

    /// <summary>Estado inicial: o par certo P N ↔ S N de uma estação já vem ligado (sem cabo físico, sem contar nos totais).</summary>
    public void PreLigar(int porta)
    {
        if (Topologia.Ligar(PortaRack.P(porta), PortaRack.S(porta), false, false, null, Time.time, true) != null) Avisar();
    }

    /// <summary>
    /// Desfaz uma retirada: a ligação volta exatamente como era (mesmas portas, cabo, defeito, se era do estado inicial),
    /// sem contar como ligação nova do jogador nos totais. Usado quando o cabo puxado não tem para onde ir.
    /// </summary>
    public void Religar(LigacaoRack antes, PickupInteractable cabo)
    {
        if (antes == null) return;
        if (Topologia.Ligar(antes.A, antes.B, antes.Crimpado, antes.Defeituoso, cabo, antes.LigadaEm, antes.PreLigada) != null) Avisar();
    }

    /// <summary>
    /// Tira o cabo de uma porta (o jogador no rack, ou um defeito de chamado). Devolve o cabo físico, se houver
    /// (a ligação do estado inicial não tem: o cabo dela é descartado).
    /// </summary>
    public bool Desligar(PortaRack porta, out PickupInteractable cabo)
    {
        LigacaoRack l = Topologia.Desligar(porta);
        cabo = l != null ? l.Cabo as PickupInteractable : null;
        if (l == null) return false;
        Avisar();
        return true;
    }

    /// <summary>Compatível com os chamados antigos: tira o cabo da porta N do PATCH PANEL (esteja ligado onde estiver).</summary>
    public bool Desligar(int porta, out GameObject cabo)
    {
        bool ok = Desligar(PortaRack.P(porta), out PickupInteractable item);
        cabo = item != null ? item.gameObject : null;
        return ok;
    }

    /// <summary>
    /// Defeito de chamado: o cabo do patch panel N passa para uma porta ERRADA livre do switch (o par fica "ligado", mas
    /// na porta trocada). Sem porta livre, só desliga. Devolve a porta do switch usada (0 = só desligou).
    /// </summary>
    public int TrocarParaPortaErrada(int porta)
    {
        LigacaoRack antes = Topologia.Desligar(PortaRack.P(porta));
        int livre = 0;
        for (int i = 0; i < portasDoRack && livre == 0; i++)
        {
            int m = (porta + i) % portasDoRack + 1;   // a próxima depois de N, dando a volta
            if (m != porta && !Topologia.Ocupada(PortaRack.S(m))) livre = m;
        }
        if (livre > 0)
        {
            // o mesmo cabo, só que na porta trocada (mantém quando foi ligado: não conta como cabo novo do jogador)
            Topologia.Ligar(PortaRack.P(porta), PortaRack.S(livre), antes != null && antes.Crimpado,
                            antes != null && antes.Defeituoso, antes != null ? antes.Cabo : null,
                            antes != null ? antes.LigadaEm : -1f, antes == null || antes.PreLigada);
        }
        Avisar();
        return livre;
    }

    /// <summary>O ponto de parede da porta N tem rede: P N ↔ S N com cabo bom e nenhum laço no switch.</summary>
    public bool PortaFunciona(int porta) => Topologia.PortaFunciona(porta);

    /// <summary>
    /// Há um par CERTO (P N ↔ S N), com cabo BOM feito na crimpagem, ligado a partir de <paramref name="desde"/> e a
    /// rede do rack funcionando (sem laço). Estado real, não um contador: tirar o cabo desfaz.
    /// </summary>
    public bool ParCrimpadoFuncionandoDesde(float desde)
    {
        if (Topologia.LacoNoSwitch) return false;
        foreach (LigacaoRack l in Topologia.Ligacoes)
            if (l.Correta && l.Crimpado && !l.Defeituoso && l.LigadaEm >= desde) return true;
        return false;
    }

    private void Avisar()
    {
        Mudou?.Invoke();
        RedeConectividade.NotificarPcs();
    }

    /// <summary>Zera tudo (debug).</summary>
    public void Reiniciar()
    {
        cabosCrimpados = 0;
        Topologia.Limpar();
        Avisar();
    }
}
