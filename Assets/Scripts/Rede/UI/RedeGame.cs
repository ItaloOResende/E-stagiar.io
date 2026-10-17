using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Estado do minigame de rede durante a partida: quantos cabos já foram crimpados e quais portas do rack
/// já estão ligadas. Fica num objeto da própria cena (criado pelo setup) para o estado ser por partida.
/// A tela de crimpagem e a tela do rack leem/escrevem aqui.
/// </summary>
public class RedeGame : MonoBehaviour
{
    [Tooltip("Quantas portas tem o rack (patch panel e switch).")]
    [SerializeField] private int portasDoRack = 6;

    private int cabosCrimpados;

    public static RedeGame Instance { get; private set; }

    public int PortasDoRack => portasDoRack;
    public int CabosDisponiveis => cabosCrimpados;
    public readonly HashSet<int> ParesLigados = new HashSet<int>();
    /// <summary>Total de cabos crimpados com sucesso e de pares ligados no rack desde o início (só crescem; chamados comparam com o valor de quando começaram).</summary>
    public int TotalCrimpados { get; private set; }
    public int TotalParesLigados { get; private set; }

    /// <summary>
    /// O cabo físico que ficou ligado em cada porta do rack. Fica AQUI (e não na tela) porque o jogador pode fechar
    /// e reabrir a tela: o cabo continua sendo dele, então ele pode tirá-lo de qualquer porta e ligar de novo.
    /// </summary>
    public readonly Dictionary<int, PickupInteractable> CabosLigados = new Dictionary<int, PickupInteractable>();

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

    /// <summary>Um cabo crimpado com sucesso vira um cabo disponível para ligar no rack.</summary>
    public void RegistrarCabo()
    {
        cabosCrimpados++;
        TotalCrimpados++;
    }

    /// <summary>Pares ligados com um cabo BOM feito na crimpagem (<see cref="CaboCrimpado"/>), não com um cabo pronto.</summary>
    public int TotalParesComCaboCrimpado { get; private set; }

    // pares ligados com cabo defeituoso (contam como ligados no rack, mas não dão rede; o cabo físico de cada porta
    // fica em CabosLigados)
    private readonly HashSet<int> paresDefeituosos = new HashSet<int>();

    /// <summary>O rack mudou (par ligado ou desligado).</summary>
    public event System.Action Mudou;

    public bool RegistrarParLigado(int porta) => RegistrarParLigado(porta, false);

    public bool RegistrarParLigado(int porta, bool caboCrimpado) => RegistrarParLigado(porta, caboCrimpado, null);

    /// <summary>Par N↔N ligado com o cabo <paramref name="cabo"/> (a tela guarda o objeto com <see cref="GuardarCaboLigado"/>).</summary>
    public bool RegistrarParLigado(int porta, bool caboCrimpado, GameObject cabo)
    {
        if (porta < 1 || porta > portasDoRack) return false;
        if (!ParesLigados.Add(porta)) return false;
        bool defeituoso = CaboRedeEstado.EhDefeituoso(cabo);
        TotalParesLigados++;
        if (caboCrimpado && !defeituoso) TotalParesComCaboCrimpado++;
        if (defeituoso) paresDefeituosos.Add(porta);
        Avisar();
        return true;
    }

    /// <summary>Estado inicial: o par da porta de uma estação já vem ligado (sem cabo físico, sem contar nos totais).</summary>
    public void PreLigar(int porta)
    {
        if (porta < 1 || porta > portasDoRack || !ParesLigados.Add(porta)) return;
        Avisar();
    }

    /// <summary>
    /// Desliga o par (defeito de chamado ou o jogador tirando o cabo no rack). Devolve o cabo físico do par, se houver
    /// (o par pré-ligado do começo não tem: o cabo dele é descartado).
    /// </summary>
    public bool Desligar(int porta, out GameObject cabo)
    {
        PickupInteractable item = RetirarCaboLigado(porta);
        cabo = item != null ? item.gameObject : null;
        return RemoverParLigado(porta);
    }

    /// <summary>O par N está ligado no rack com um cabo bom.</summary>
    public bool PortaFunciona(int porta) => ParesLigados.Contains(porta) && !paresDefeituosos.Contains(porta);

    private void Avisar()
    {
        Mudou?.Invoke();
        RedeConectividade.NotificarPcs();
    }

    /// <summary>Tira a porta da lista de ligadas (o jogador tirou o cabo): ela volta a aceitar cabo.</summary>
    public bool RemoverParLigado(int porta)
    {
        if (porta < 1 || porta > portasDoRack) return false;
        paresDefeituosos.Remove(porta);
        if (!ParesLigados.Remove(porta)) return false;
        Avisar();   // a estação dessa porta perde a rede
        return true;
    }

    /// <summary>Guarda o cabo que ficou ligado numa porta, para poder devolvê-lo à mão se o jogador desligar.</summary>
    public void GuardarCaboLigado(int porta, PickupInteractable cabo)
    {
        if (porta < 1 || porta > portasDoRack) return;
        if (cabo == null) CabosLigados.Remove(porta);
        else CabosLigados[porta] = cabo;
    }

    /// <summary>Pega (e esquece) o cabo que estava ligado numa porta. Null se não havia cabo físico ali.</summary>
    public PickupInteractable RetirarCaboLigado(int porta)
    {
        if (porta < 1 || porta > portasDoRack) return null;
        if (!CabosLigados.TryGetValue(porta, out PickupInteractable cabo)) return null;
        CabosLigados.Remove(porta);
        return cabo;
    }

    /// <summary>Zera tudo (botão de "reiniciar" no Editor/Console ou debug).</summary>
    public void Reiniciar()
    {
        cabosCrimpados = 0;
        ParesLigados.Clear();
        CabosLigados.Clear();
        paresDefeituosos.Clear();
        Avisar();
    }
}
