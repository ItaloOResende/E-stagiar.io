using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Tipos de infração da 1ª versão. Valores novos sempre no fim.</summary>
public enum InfractionKind
{
    /// <summary>Uma peça caiu de verdade (impacto) e NÃO quebrou (continua funcionando).</summary>
    ComponentDropped,
    /// <summary>Uma peça quebrou na queda (inutilizada; estado mantido pelo <see cref="ComputerPartItem"/>).</summary>
    ComponentBroken,
    /// <summary>Peça/ferramenta/cabo largado no chão além do tempo de tolerância (desorganização).</summary>
    ItemLeftOnFloor
}

/// <summary>Uma infração que ACONTECEU (independe de alguém ter visto). Imutável depois de criada, exceto o desfecho.</summary>
public class Infraction
{
    public int Id { get; internal set; }
    public InfractionKind Kind { get; internal set; }
    /// <summary>Objeto envolvido (a peça, a ferramenta).</summary>
    public GameObject Item { get; internal set; }
    /// <summary>Nome do item para mensagens (ex.: "memória RAM").</summary>
    public string ItemName { get; internal set; }
    /// <summary>Onde aconteceu (para o supervisor olhar).</summary>
    public Vector3 Position { get; internal set; }
    public float Time { get; internal set; }
    /// <summary>Instantânea (queda/quebra: só pode ser presenciada logo depois) ou um estado que continua (item no chão).</summary>
    public bool IsOngoing => Kind == InfractionKind.ItemLeftOnFloor;
    public bool Witnessed { get; internal set; }
    /// <summary>Encerrada sem ninguém ver (janela de percepção passou, ou o item saiu do chão antes de ser visto).</summary>
    public bool Missed { get; internal set; }
    public bool Resolved => Witnessed || Missed;
    /// <summary>Quem viu (null se ninguém).</summary>
    public Transform Witness { get; internal set; }
}

/// <summary>
/// Registro central de infrações, em três camadas separadas:
///  1. <see cref="Occurred"/>: a infração aconteceu (fontes: <see cref="ConductMonitor"/>);
///  2. <see cref="Witnessed"/>: um supervisor a percebeu (<see cref="SupervisorPerception"/>), uma única vez por infração;
///     ou <see cref="Missed"/>: passou despercebida;
///  3. consequência: quem quiser (reação do NPC, HUD, e no futuro a pontuação/vidas) assina <see cref="Witnessed"/>.
///     Nenhuma penalidade é aplicada aqui.
/// Também guarda métricas (quantas aconteceram, quantas foram vistas/perdidas por tipo) para testes e ajuste de jogo.
/// </summary>
public static class InfractionLog
{
    public static event Action<Infraction> Occurred;
    /// <summary>Uma infração foi percebida (uma vez só). O Transform é o supervisor que viu.</summary>
    public static event Action<Infraction, Transform> Witnessed;
    public static event Action<Infraction> Missed;

    private static readonly List<Infraction> open = new List<Infraction>();
    private static readonly int kinds = Enum.GetValues(typeof(InfractionKind)).Length;
    private static int[] occurred = new int[kinds], witnessed = new int[kinds], missed = new int[kinds];
    private static int nextId = 1;

    /// <summary>Infrações ainda sem desfecho (o supervisor pode percebê-las).</summary>
    public static IReadOnlyList<Infraction> Open => open;

    public static int OccurredCount(InfractionKind k) => occurred[(int)k];
    public static int WitnessedCount(InfractionKind k) => witnessed[(int)k];
    public static int MissedCount(InfractionKind k) => missed[(int)k];
    public static int TotalOccurred => Sum(occurred);
    public static int TotalWitnessed => Sum(witnessed);
    public static int TotalMissed => Sum(missed);

    private static int Sum(int[] a) { int s = 0; foreach (int v in a) s += v; return s; }

    /// <summary>Zera tudo (início de fase, testes).</summary>
    public static void ResetAll()
    {
        open.Clear();
        occurred = new int[kinds]; witnessed = new int[kinds]; missed = new int[kinds];
        nextId = 1;
    }

    /// <summary>Registra que uma infração aconteceu. Retorna o registro (para quem a criou acompanhar).</summary>
    public static Infraction Report(InfractionKind kind, GameObject item, string itemName, Vector3 position)
    {
        var inf = new Infraction
        {
            Id = nextId++, Kind = kind, Item = item, ItemName = itemName, Position = position, Time = UnityEngine.Time.time
        };
        open.Add(inf);
        occurred[(int)kind]++;
        Occurred?.Invoke(inf);
        return inf;
    }

    /// <summary>Um supervisor percebeu a infração. Ignora se já tinha desfecho (nunca registra duas vezes).</summary>
    public static bool MarkWitnessed(Infraction inf, Transform witness)
    {
        if (inf == null || inf.Resolved) return false;
        inf.Witnessed = true;
        inf.Witness = witness;
        open.Remove(inf);
        witnessed[(int)inf.Kind]++;
        Witnessed?.Invoke(inf, witness);
        return true;
    }

    /// <summary>A infração deixou de poder ser percebida sem ninguém ter visto.</summary>
    public static void MarkMissed(Infraction inf)
    {
        if (inf == null || inf.Resolved) return;
        inf.Missed = true;
        open.Remove(inf);
        missed[(int)inf.Kind]++;
        Missed?.Invoke(inf);
    }

    /// <summary>Resumo das métricas (log de teste/ajuste).</summary>
    public static string Summary()
    {
        var sb = new System.Text.StringBuilder();
        foreach (InfractionKind k in Enum.GetValues(typeof(InfractionKind)))
            sb.Append($"{k}: {OccurredCount(k)} aconteceram, {WitnessedCount(k)} vistas, {MissedCount(k)} despercebidas; ");
        sb.Append($"total {TotalWitnessed}/{TotalOccurred} vistas");
        return sb.ToString();
    }
}
