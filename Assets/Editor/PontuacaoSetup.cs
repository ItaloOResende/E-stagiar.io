using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Coloca o <see cref="ScoreKeeper"/> nas cenas que têm chamados (<c>Escritorio_Teste</c> e <c>Fase_01</c>): no mesmo
/// GameObject do <see cref="TicketManager"/>, ligado a ele e ao <see cref="StageController"/>, e liga o campo
/// <c>Score Keeper</c> de cada <see cref="ComputerCaseTicketBridge"/> da cena. NÃO define valores de pontuação (ficam
/// em 0/vazios até a equipe decidir). Idempotente; só mexe nesses componentes.
/// </summary>
public static class PontuacaoSetup
{
    private static readonly string[] Scenes = { "Assets/Scenes/Escritorio_Teste.unity", "Assets/Scenes/Fase_01.unity" };

    [MenuItem("Tools/E-stagiar.io/Pontuação nas cenas (ScoreKeeper)")]
    public static void Run()
    {
        foreach (string path in Scenes)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            TicketManager tickets = Object.FindAnyObjectByType<TicketManager>(FindObjectsInactive.Include);
            if (tickets == null)
            {
                Debug.LogWarning($"PontuacaoSetup: {path} não tem TicketManager; nada feito.");
                continue;
            }
            ScoreKeeper score = tickets.GetComponent<ScoreKeeper>();
            if (score == null) score = tickets.gameObject.AddComponent<ScoreKeeper>();
            var so = new SerializedObject(score);
            so.FindProperty("ticketManager").objectReferenceValue = tickets;
            StageController stage = tickets.GetComponent<StageController>();
            if (stage == null) stage = Object.FindAnyObjectByType<StageController>(FindObjectsInactive.Include);
            so.FindProperty("stageController").objectReferenceValue = stage;
            so.ApplyModifiedPropertiesWithoutUndo();

            int bridges = 0;
            foreach (ComputerCaseTicketBridge b in Object.FindObjectsByType<ComputerCaseTicketBridge>(FindObjectsInactive.Include))
            {
                var bso = new SerializedObject(b);
                bso.FindProperty("scoreKeeper").objectReferenceValue = score;
                bso.ApplyModifiedPropertiesWithoutUndo();
                bridges++;
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"PontuacaoSetup: {path} — ScoreKeeper em '{tickets.name}', {bridges} ponte(s) ligada(s).");
        }
    }
}
