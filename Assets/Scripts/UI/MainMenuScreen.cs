using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Menu inicial (tela 2D por cima da cena, montada por código como o relatório final). Aparece antes da fase começar
/// quando o <see cref="StageController"/> pede (<i>Show Start Menu</i>, ou o botão "Ir para o menu inicial" do relatório,
/// que recarrega a cena do zero com <see cref="GameSession.GoToMainMenu"/>). Escolhas:
///  - <b>Expediente</b> (<see cref="GameMode.Normal"/>): a fase de sempre;
///  - <b>Sair do jogo</b>: fecha o jogo (no Editor só registra no Console; não fecha o Editor nem para o Play Mode).
/// Enquanto aberto, o jogador não anda nem olha e o cursor fica livre; a fase (relógio, chamados) ainda não começou.
/// </summary>
public class MainMenuScreen : MonoBehaviour
{
    private static readonly Color Ink = new Color(0.13f, 0.11f, 0.16f);
    private static readonly Color Cream = new Color(1f, 0.97f, 0.9f);
    private static readonly Color Blue = new Color(0.16f, 0.45f, 0.86f);
    private static readonly Color Orange = new Color(0.93f, 0.48f, 0.12f);
    private static readonly Color Muted = new Color(0.42f, 0.39f, 0.44f);

    /// <summary>O menu inicial está na tela.</summary>
    public static bool Showing { get; private set; }

    private StageController stage;
    private Canvas canvas;
    private Font font;
    private readonly List<Behaviour> disabled = new List<Behaviour>();

    /// <summary>Abre o menu inicial para a fase <paramref name="stage"/> (cria o componente no mesmo objeto).</summary>
    public static MainMenuScreen Open(StageController stage)
    {
        MainMenuScreen m = stage.GetComponent<MainMenuScreen>();
        if (m == null) m = stage.gameObject.AddComponent<MainMenuScreen>();
        m.stage = stage;
        m.Show();
        return m;
    }

    private void Show()
    {
        if (canvas != null) Destroy(canvas.gameObject);
        Build();
        Showing = true;
        disabled.Clear();
        foreach (var b in FindObjectsByType<PlayerLook>(FindObjectsSortMode.None)) Disable(b);
        foreach (var b in FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None)) Disable(b);
        foreach (var b in FindObjectsByType<InteractionDetector>(FindObjectsSortMode.None)) Disable(b);
        UICursor.Reset();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Disable(Behaviour b)
    {
        if (b == null || !b.enabled) return;
        b.enabled = false;
        disabled.Add(b);
    }

    /// <summary>Escolha do jogador: fecha o menu, devolve os controles e começa a fase no modo escolhido.</summary>
    public void Choose(GameMode mode)
    {
        if (canvas != null) Destroy(canvas.gameObject);
        canvas = null;
        Showing = false;
        foreach (Behaviour b in disabled) if (b != null) b.enabled = true;
        disabled.Clear();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        InteractionSounds.Raise(InteractionSound.PowerButton);
        if (stage != null) stage.StartStageWithMode(mode);
    }

    /// <summary>Botão "Sair do jogo": encerra a aplicação. No Editor não fecha nada (seguro para testes): só avisa.</summary>
    public void SairDoJogo()
    {
        InteractionSounds.Raise(InteractionSound.PowerButton);
#if UNITY_EDITOR
        Debug.Log("Menu inicial: \"Sair do jogo\" (no Editor nada é fechado; na build o jogo encerra).", this);
#else
        Application.Quit();
#endif
    }

    private void OnDestroy()
    {
        if (canvas != null) Showing = false;
    }

    // ------------------------------------------------------------------ montagem

    private void Build()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var go = new GameObject("MenuInicial", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.transform.SetParent(transform, false);
        canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32600;   // acima de tudo (HUD, celular, relatório)
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        var root = (RectTransform)go.transform;

        Image dim = Box(root, "Fundo", new Color(0.05f, 0.06f, 0.1f, 0.86f));
        Stretch(dim.rectTransform);
        dim.raycastTarget = true;

        Image card = Box(root, "Cartao", Cream);
        Place(card.rectTransform, new Vector2(1180f, 760f), Vector2.zero);
        Text title = Label(card.rectTransform, "E-stagiar.io", 84, FontStyle.Bold, Blue, new Vector2(1100f, 110f), new Vector2(0f, 290f));
        title.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.2f);
        Label(card.rectTransform, "Estagiário de suporte de TI", 30, FontStyle.Normal, Muted,
              new Vector2(1100f, 50f), new Vector2(0f, 210f));

        ModeButton(card.rectTransform, "EXPEDIENTE", "A fase de sempre: atenda os chamados até o fim do expediente.",
                   Blue, new Vector2(0f, 70f), () => Choose(GameMode.Normal));
        ModeButton(card.rectTransform, "SAIR DO JOGO", "Fecha o jogo.",
                   Orange, new Vector2(0f, -110f), SairDoJogo);
        Label(card.rectTransform, "Celular: C (ou Alt + clique no aparelho)  ·  Esc fecha as telas", 22, FontStyle.Italic, Muted,
              new Vector2(1100f, 40f), new Vector2(0f, -300f));
    }

    private void ModeButton(RectTransform parent, string text, string desc, Color color, Vector2 pos, UnityEngine.Events.UnityAction action)
    {
        Image bg = Box(parent, "Botao_" + text, color);
        Place(bg.rectTransform, new Vector2(960f, 150f), pos);
        bg.raycastTarget = true;
        var b = bg.gameObject.AddComponent<Button>();
        b.targetGraphic = bg;
        ColorBlock cb = b.colors;
        cb.highlightedColor = new Color(1.12f, 1.12f, 1.12f); cb.pressedColor = new Color(0.8f, 0.8f, 0.8f);
        b.colors = cb;
        b.onClick.AddListener(action);
        Text t = Label(bg.rectTransform, text, 46, FontStyle.Bold, Color.white, new Vector2(900f, 60f), new Vector2(0f, 30f));
        t.gameObject.AddComponent<Outline>().effectColor = Ink;
        Text d = Label(bg.rectTransform, desc, 24, FontStyle.Normal, Color.white, new Vector2(900f, 50f), new Vector2(0f, -36f));
        d.resizeTextForBestFit = true; d.resizeTextMinSize = 14; d.resizeTextMaxSize = 24;
    }

    private static Image Box(Transform parent, string name, Color c)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = c;
        img.raycastTarget = false;
        return img;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private static void Place(RectTransform rt, Vector2 size, Vector2 pos)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size; rt.anchoredPosition = pos;
    }

    private Text Label(RectTransform parent, string text, int size, FontStyle style, Color color, Vector2 box, Vector2 pos)
    {
        var go = new GameObject("Texto", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Place((RectTransform)go.transform, box, pos);
        var t = go.AddComponent<Text>();
        t.font = font; t.text = text; t.fontSize = size; t.fontStyle = style; t.color = color;
        t.alignment = TextAnchor.MiddleCenter; t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }
}
