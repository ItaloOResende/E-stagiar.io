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
    }

    public bool RegistrarParLigado(int porta)
    {
        if (porta < 1 || porta > portasDoRack) return false;
        return ParesLigados.Add(porta);
    }

    /// <summary>Zera tudo (botão de "reiniciar" no Editor/Console ou debug).</summary>
    public void Reiniciar()
    {
        cabosCrimpados = 0;
        ParesLigados.Clear();
    }
}
