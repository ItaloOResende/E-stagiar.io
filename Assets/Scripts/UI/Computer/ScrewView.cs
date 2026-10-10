using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Apresentação de UM parafuso sobre a imagem da traseira do gabinete: botão clicável + feedback visual.
/// Sem lógica de jogo: só avisa o clique (<see cref="Init"/>) e mostra os estados que o
/// <see cref="ComputerScreenController"/> mandar. Toda a aparência (sprites, cores, tamanho, animação)
/// vem do prefab, então pode ser trocada sem tocar neste script.
/// </summary>
public class ScrewView : MonoBehaviour
{
    [SerializeField] private Button button;
    [Tooltip("Imagem que cobre o parafuso depois de removido (o furo).")]
    [SerializeField] private Graphic hole;
    [Tooltip("Anel que pisca no momento da remoção.")]
    [SerializeField] private Graphic flash;
    [SerializeField] private float removeDuration = 0.35f;
    [SerializeField] private float rejectDuration = 0.3f;
    [SerializeField] private float rejectShake = 7f;

    private int index;
    private Action<int> clicked;
    private RectTransform rect;
    private Coroutine running;

    public int Index => index;
    public bool Removed { get; private set; }

    /// <summary>Liga a vista ao parafuso de índice <paramref name="screwIndex"/> e ao callback de clique.</summary>
    public void Init(int screwIndex, Action<int> onClick)
    {
        index = screwIndex;
        clicked = onClick;
        rect = (RectTransform)transform;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => clicked?.Invoke(index));
    }

    /// <summary>Mostra o parafuso removido (furo) ou presente. <paramref name="animate"/>: só quando acabou de ser removido.</summary>
    public void SetRemoved(bool removed, bool animate)
    {
        Removed = removed;
        button.interactable = !removed;
        StopRunning();
        ResetPose();

        if (hole != null) hole.gameObject.SetActive(removed);
        if (flash != null) flash.gameObject.SetActive(false);
        if (removed && animate && isActiveAndEnabled) running = StartCoroutine(RemoveRoutine());
    }

    /// <summary>Tremida de recusa (ferramenta errada ou ausente).</summary>
    public void PlayRejected()
    {
        if (Removed || !isActiveAndEnabled) return;
        StopRunning();
        ResetPose();
        running = StartCoroutine(RejectRoutine());
    }

    private void OnDisable()
    {
        StopRunning();
        ResetPose();
    }

    private void StopRunning()
    {
        if (running != null) StopCoroutine(running);
        running = null;
    }

    private void ResetPose()
    {
        if (rect == null) return;
        // O parafuso é posicionado por âncoras; o offset em relação à âncora é sempre zero em repouso.
        rect.anchoredPosition = Vector2.zero;
        rect.localScale = Vector3.one;
        if (hole != null)
        {
            Color c = hole.color; c.a = 1f; hole.color = c;
            hole.rectTransform.localScale = Vector3.one;
        }
    }

    private IEnumerator RemoveRoutine()
    {
        float t = 0f;
        if (flash != null) flash.gameObject.SetActive(true);
        while (t < removeDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / removeDuration);
            // O furo "estoura" de grande para o tamanho normal, enquanto o anel de brilho cresce e some.
            if (hole != null)
            {
                float s = Mathf.Lerp(2.2f, 1f, 1f - (1f - k) * (1f - k));
                hole.rectTransform.localScale = new Vector3(s, s, 1f);
                Color c = hole.color; c.a = Mathf.Clamp01(k * 3f); hole.color = c;
            }
            if (flash != null)
            {
                float s = Mathf.Lerp(1f, 2.4f, k);
                flash.rectTransform.localScale = new Vector3(s, s, 1f);
                Color c = flash.color; c.a = 1f - k; flash.color = c;
            }
            yield return null;
        }
        if (flash != null) flash.gameObject.SetActive(false);
        if (hole != null)
        {
            hole.rectTransform.localScale = Vector3.one;
            Color c = hole.color; c.a = 1f; hole.color = c;
        }
        running = null;
    }

    private IEnumerator RejectRoutine()
    {
        float t = 0f;
        while (t < rejectDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / rejectDuration);
            float x = Mathf.Sin(k * Mathf.PI * 6f) * rejectShake * (1f - k);
            rect.anchoredPosition = new Vector2(x, 0f);
            yield return null;
        }
        rect.anchoredPosition = Vector2.zero;
        running = null;
    }
}
