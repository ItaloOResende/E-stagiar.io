using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Toca os efeitos sonoros das ações de interação (<see cref="InteractionSounds"/>). Um por cena (ex.: objeto
/// <c>Sons_Interacao</c>). Cada linha da lista liga uma ação (<see cref="InteractionSound"/>) a um ou mais clipes
/// (sorteado um por vez). Linha sem clipe, ou ação sem linha = silêncio, sem erro: dá para preparar a lista antes de ter
/// os áudios. Ações da interface 2D tocam sem posição (<see cref="uiSource"/>); as do mundo, no ponto onde aconteceram.
/// Só apresentação: nenhuma mecânica depende deste componente.
/// </summary>
public class InteractionSoundPlayer : MonoBehaviour
{
    [Serializable]
    public class Entry
    {
        public InteractionSound sound;
        [Tooltip("Um ou mais clipes (um é sorteado a cada vez). Vazio = sem som para esta ação.")]
        public AudioClip[] clips = new AudioClip[0];
        [Range(0f, 1f)] public float volume = 1f;
        [Tooltip("Variação aleatória de tom (0 = sempre igual).")]
        [Range(0f, 0.5f)] public float pitchVariation = 0.05f;
    }

    [SerializeField] private List<Entry> entries = new List<Entry>();
    [Tooltip("Fonte 2D para as ações da interface (parafusos, cabos, peças na tela de manutenção). Vazio = cria uma.")]
    [SerializeField] private AudioSource uiSource;
    [Tooltip("Fonte 3D usada como modelo para as ações no mundo (alcance, rolloff). Vazio = som 3D padrão.")]
    [SerializeField] private AudioSource worldSourceTemplate;

    public IReadOnlyList<Entry> Entries => entries;

    private void OnEnable() => InteractionSounds.Raised += Play;
    private void OnDisable() => InteractionSounds.Raised -= Play;

    /// <summary>Clipe que tocaria para a ação (null = sem som configurado).</summary>
    public AudioClip ClipFor(InteractionSound sound, out Entry entry)
    {
        entry = entries.Find(e => e.sound == sound);
        if (entry == null || entry.clips == null || entry.clips.Length == 0) return null;
        AudioClip clip = entry.clips[UnityEngine.Random.Range(0, entry.clips.Length)];
        return clip;
    }

    private void Play(InteractionSound sound, Vector3? at)
    {
        AudioClip clip = ClipFor(sound, out Entry entry);
        if (clip == null) return;
        float pitch = 1f + UnityEngine.Random.Range(-entry.pitchVariation, entry.pitchVariation);

        if (at.HasValue)
        {
            var go = new GameObject("Som_" + sound);
            go.transform.position = at.Value;
            AudioSource src = go.AddComponent<AudioSource>();
            if (worldSourceTemplate != null)
            {
                src.outputAudioMixerGroup = worldSourceTemplate.outputAudioMixerGroup;
                src.rolloffMode = worldSourceTemplate.rolloffMode;
                src.minDistance = worldSourceTemplate.minDistance;
                src.maxDistance = worldSourceTemplate.maxDistance;
            }
            src.spatialBlend = 1f;
            src.pitch = pitch;
            src.PlayOneShot(clip, entry.volume);
            Destroy(go, clip.length / Mathf.Max(0.1f, pitch) + 0.1f);
            return;
        }

        if (uiSource == null)
        {
            uiSource = gameObject.AddComponent<AudioSource>();
            uiSource.playOnAwake = false;
            uiSource.spatialBlend = 0f;
        }
        uiSource.pitch = pitch;
        uiSource.PlayOneShot(clip, entry.volume);
    }
}
