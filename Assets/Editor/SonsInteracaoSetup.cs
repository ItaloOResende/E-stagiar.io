using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Deixa a cena ABERTA com as ações de som que o jogo já dispara por <see cref="InteractionSounds"/>. Hoje: o som
/// de erro das ações recusadas (peça/cabo no lugar errado, ferramenta errada, ordem errada). Sem a linha no
/// <see cref="InteractionSoundPlayer"/>, a ação simplesmente fica em silêncio — nada quebra, mas o jogador não
/// recebe o aviso sonoro.
///
/// É para a cena que está ABERTA no editor: o arquivo .unity já vem com a linha, mas a cena carregada em memória
/// (que é quem manda no Play) não pega a mudança do arquivo. Idempotente: rodar de novo não duplica nem mexe nas
/// outras linhas — mesmo protocolo dos outros menus do projeto.
/// </summary>
public static class SonsInteracaoSetup
{
    private const string CaminhoDoSomDeErro = "Assets/Art/Sounds/erro.mp3";

    [MenuItem("Tools/E-stagiar.io/Ajustar sons da cena (som de erro) (cena aberta)")]
    public static void Ajustar()
    {
        AudioClip erro = AssetDatabase.LoadAssetAtPath<AudioClip>(CaminhoDoSomDeErro);
        if (erro == null)
        {
            Debug.LogWarning("SonsInteracaoSetup: não achei " + CaminhoDoSomDeErro + " — o som de erro ficaria vazio.");
            return;
        }

        InteractionSoundPlayer[] players =
            Object.FindObjectsByType<InteractionSoundPlayer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (players.Length == 0)
        {
            Debug.LogWarning("SonsInteracaoSetup: não há InteractionSoundPlayer (objeto 'Sons_Interacao') na cena aberta.");
            return;
        }

        int mexidos = 0;
        foreach (InteractionSoundPlayer player in players)
            if (GarantirSomDeErro(player, erro)) mexidos++;

        if (mexidos == 0)
        {
            Debug.Log("SonsInteracaoSetup: os sons da cena já estavam certos — nada a fazer.");
            return;
        }

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene cena = SceneManager.GetSceneAt(i);
            if (cena.isLoaded) EditorSceneManager.MarkSceneDirty(cena);
        }
        EditorSceneManager.SaveOpenScenes();

        Debug.Log("SonsInteracaoSetup: som de erro configurado em " + mexidos + " objeto(s) de sons (cena salva).");
    }

    /// <summary>Garante a linha do som de erro na lista. Devolve true se mexeu em algo.</summary>
    private static bool GarantirSomDeErro(InteractionSoundPlayer player, AudioClip erro)
    {
        var so = new SerializedObject(player);
        SerializedProperty entries = so.FindProperty("entries");
        if (entries == null) return false;

        SerializedProperty entrada = null;
        for (int i = 0; i < entries.arraySize; i++)
        {
            SerializedProperty candidata = entries.GetArrayElementAtIndex(i);
            if (candidata.FindPropertyRelative("sound").enumValueIndex == (int)InteractionSound.Error)
            {
                entrada = candidata;
                break;
            }
        }

        bool nova = entrada == null;
        if (nova)
        {
            entries.arraySize++;
            entrada = entries.GetArrayElementAtIndex(entries.arraySize - 1);
            entrada.FindPropertyRelative("sound").enumValueIndex = (int)InteractionSound.Error;
            entrada.FindPropertyRelative("volume").floatValue = 0.45f;
            SerializedProperty variacao = entrada.FindPropertyRelative("pitchVariation");
            if (variacao != null) variacao.floatValue = 0.05f;
        }

        SerializedProperty clips = entrada.FindPropertyRelative("clips");
        bool semSom = clips.arraySize == 0;
        if (semSom)
        {
            clips.arraySize = 1;
            clips.GetArrayElementAtIndex(0).objectReferenceValue = erro;
        }

        if (!nova && !semSom) return false;   // já estava certa: não suja a cena à toa

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(player);
        return true;
    }
}
