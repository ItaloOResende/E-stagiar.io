using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Liga a ação <see cref="InteractionSound.PickupBlocked"/> (pegar algo preso, ex.: gabinete com cabos na tomada) ao
/// clipe <c>sfx_bloqueado</c> (<c>Tools/audio/gerar_sfx.py</c>) no <see cref="InteractionSoundPlayer"/> das cenas com
/// gameplay (<c>Escritorio_Teste</c>, <c>Fase_01</c>). Só acrescenta a linha que falta; idempotente.
/// </summary>
public static class SomBloqueioSetup
{
    private static readonly string[] Scenes = { "Assets/Scenes/Escritorio_Teste.unity", "Assets/Scenes/Fase_01.unity" };

    [MenuItem("Tools/E-stagiar.io/Som de pegar bloqueado nas cenas")]
    public static void Run()
    {
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/sfx_bloqueado.wav");
        if (clip == null) { Debug.LogError("SomBloqueioSetup: falta Assets/Audio/SFX/sfx_bloqueado.wav"); return; }
        foreach (string path in Scenes)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var player = Object.FindAnyObjectByType<InteractionSoundPlayer>(FindObjectsInactive.Include);
            if (player == null) { Debug.LogWarning($"SomBloqueioSetup: {path} sem InteractionSoundPlayer."); continue; }
            var so = new SerializedObject(player);
            SerializedProperty entries = so.FindProperty("entries");
            bool exists = false;
            for (int i = 0; i < entries.arraySize; i++)
                if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("sound").intValue == (int)InteractionSound.PickupBlocked) exists = true;
            if (!exists)
            {
                entries.arraySize++;
                SerializedProperty e = entries.GetArrayElementAtIndex(entries.arraySize - 1);
                e.FindPropertyRelative("sound").intValue = (int)InteractionSound.PickupBlocked;
                SerializedProperty clips = e.FindPropertyRelative("clips");
                clips.arraySize = 1;
                clips.GetArrayElementAtIndex(0).objectReferenceValue = clip;
                e.FindPropertyRelative("volume").floatValue = 0.45f;
                e.FindPropertyRelative("pitchVariation").floatValue = 0.05f;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            Debug.Log($"SomBloqueioSetup: {path} — PickupBlocked {(exists ? "já estava" : "ligado")}.");
        }
    }
}
