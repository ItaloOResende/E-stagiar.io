using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Menu de pausa (tela 2D montada por código, como o <see cref="MainMenuScreen"/>). Vai no objeto da fase (o mesmo do
/// <see cref="StageController"/>). <b>Esc</b> durante a partida, no 3D:
///  - com uma tela de manutenção aberta, o Esc continua só fechando a tela (<see cref="UIScreenManager"/>);
///  - com o celular aberto, o Esc fecha o celular;
///  - senão, abre/fecha a pausa.
/// Na pausa: <c>Time.timeScale = 0</c> (relógio da fase, chamados, SLA, física e supervisor param), áudio pausado,
/// jogador, mira, celular, supervisor (ronda e percepção) e monitor de conduta desligados — nada é penalizado. Os
/// chamados, a pontuação, o item na mão e o estado dos minigames não mudam. Opções: Retomar, Comandos (lista dos
/// controles e do ciclo de atendimento) e Voltar ao menu inicial (<see cref="GameSession.GoToMainMenu"/>: recarrega a
/// cena do zero, como o botão do relatório).
/// </summary>
public class PauseMenuScreen : MonoBehaviour
{
    private static readonly Color Ink = new Color(0.13f, 0.11f, 0.16f);
    private static readonly Color Cream = new Color(1f, 0.97f, 0.9f);
    private static readonly Color Blue = new Color(0.16f, 0.45f, 0.86f);
    private static readonly Color Orange = new Color(0.93f, 0.48f, 0.12f);
    private static readonly Color Gray = new Color(0.45f, 0.43f, 0.5f);
    private static readonly Color Muted = new Color(0.42f, 0.39f, 0.44f);

    /// <summary>A pausa está na tela (o jogo está parado).</summary>
    public static bool Showing { get; private set; }

    [SerializeField] private StageController stage;

    private Canvas canvas;
    private RectTransform card, commands;
    private Font font;
    private float timeScaleBefore = 1f;
    private readonly List<Behaviour> disabled = new List<Behaviour>();

    private void Awake()
    {
        if (stage == null) stage = GetComponent<StageController>();
    }

    private void Update()
    {
        Keyboard k = Keyboard.current;
        if (k == null || !k.escapeKey.wasPressedThisFrame) return;
        if (Showing) { if (commands != null && commands.gameObject.activeSelf) ShowCommands(false); else Resume(); return; }
        if (!CanPause()) return;
        if (TicketTabletPrototype.CelularAberto)
        {
            TicketTabletPrototype tablet = FindAnyObjectByType<TicketTabletPrototype>();
            if (tablet != null) tablet.Fechar();
            return;
        }
        Pause();
    }

    // Só no 3D com a fase rodando: menu inicial, relatório e telas 2D (que o Esc já fecha) têm prioridade.
    private bool CanPause()
    {
        if (MainMenuScreen.Showing || StageReportScreen.Showing) return false;
        if (stage != null && !stage.IsRunning) return false;
        UIScreenManager screens = UIScreenManager.Active;
        return screens == null || !screens.BlocksGameplayInput;
    }

    /// <summary>Pausa o jogo e mostra o menu.</summary>
    public void Pause()
    {
        if (Showing) return;
        Showing = true;
        timeScaleBefore = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;
        AudioListener.pause = true;
        disabled.Clear();
        // o celular primeiro: ao ser desligado ele devolve os controles que tinha tirado (cursor liberado com Alt)
        foreach (var b in FindObjectsByType<TicketTabletPrototype>(FindObjectsSortMode.None)) Disable(b);
        foreach (var b in FindObjectsByType<PlayerLook>(FindObjectsSortMode.None)) Disable(b);
        foreach (var b in FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None)) Disable(b);
        foreach (var b in FindObjectsByType<InteractionDetector>(FindObjectsSortMode.None)) Disable(b);
        foreach (var b in FindObjectsByType<SupervisorPatrol>(FindObjectsSortMode.None)) Disable(b);
        foreach (var b in FindObjectsByType<SupervisorPerception>(FindObjectsSortMode.None)) Disable(b);
        foreach (var b in FindObjectsByType<ConductMonitor>(FindObjectsSortMode.None)) Disable(b);
        UICursor.Reset();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (canvas == null) Build();
        canvas.gameObject.SetActive(true);
        ShowCommands(false);
    }

    /// <summary>Volta ao jogo exatamente como estava.</summary>
    public void Resume()
    {
        if (!Showing) return;
        Showing = false;
        if (canvas != null) canvas.gameObject.SetActive(false);
        Time.timeScale = timeScaleBefore;
        AudioListener.pause = false;
        foreach (Behaviour b in disabled) if (b != null) b.enabled = true;
        disabled.Clear();
        UIScreenManager screens = UIScreenManager.Active;
        if (screens != null) screens.ApplyCursorForCurrentState();
        else { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
    }

    /// <summary>Encerra a partida e volta ao menu inicial (cena recarregada, sem estado antigo).</summary>
    public void BackToMenu()
    {
        Showing = false;
        AudioListener.pause = false;
        Time.timeScale = 1f;
        GameSession.GoToMainMenu();
    }

    private void Disable(Behaviour b)
    {
        if (b == null || !b.enabled) return;
        b.enabled = false;
        disabled.Add(b);
    }

    private void OnDestroy()
    {
        if (!Showing) return;
        Showing = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;
    }

    public void ShowCommands(bool show)
    {
        if (commands != null) commands.gameObject.SetActive(show);
        if (card != null) card.gameObject.SetActive(!show);
    }

    // ------------------------------------------------------------------ montagem

    private void Build()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var go = new GameObject("MenuPausa", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.transform.SetParent(transform, false);
        canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32590;   // acima do HUD e do celular; abaixo do menu inicial
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        var root = (RectTransform)go.transform;

        Image dim = Box(root, "Fundo", new Color(0.05f, 0.06f, 0.1f, 0.78f));
        Stretch(dim.rectTransform);
        dim.raycastTarget = true;

        Image c = Box(root, "Cartao", Cream);
        card = c.rectTransform;
        Place(card, new Vector2(760f, 640f), Vector2.zero);
        Label(card, "PAUSA", 72, FontStyle.Bold, Blue, new Vector2(700f, 100f), new Vector2(0f, 230f));
        Label(card, "O relógio e os chamados estão parados.", 26, FontStyle.Normal, Muted, new Vector2(700f, 40f), new Vector2(0f, 165f));
        Button(card, "RETOMAR", Blue, new Vector2(0f, 70f), Resume);
        Button(card, "COMANDOS", Gray, new Vector2(0f, -50f), () => ShowCommands(true));
        Button(card, "MENU INICIAL", Orange, new Vector2(0f, -170f), BackToMenu);
        Label(card, "Esc: retomar", 22, FontStyle.Italic, Muted, new Vector2(700f, 36f), new Vector2(0f, -275f));

        Image cm = Box(root, "Comandos", Cream);
        commands = cm.rectTransform;
        Place(commands, new Vector2(1240f, 860f), Vector2.zero);
        Label(commands, "COMANDOS", 56, FontStyle.Bold, Blue, new Vector2(1100f, 80f), new Vector2(0f, 360f));
        Text lista = Label(commands, Lista, 27, FontStyle.Normal, Ink, new Vector2(1120f, 600f), new Vector2(0f, 20f));
        lista.alignment = TextAnchor.UpperLeft;
        lista.lineSpacing = 1.15f;
        Button(commands, "VOLTAR", Gray, new Vector2(0f, -350f), () => ShowCommands(false));
        commands.gameObject.SetActive(false);
    }

    private const string Lista =
        "<b>W A S D</b>   andar          <b>Mouse</b>   olhar\n" +
        "<b>Clique esquerdo</b> (ícone da mão)   pegar, soltar, colocar sobre a mesa, abrir gavetas e armários\n" +
        "<b>Clique direito</b> (ícone da ferramenta)   manutenção do equipamento: gabinete, monitor, notebook,\n" +
        "      impressora, rack de rede, caixa de cabo e testador\n" +
        "<b>C</b>   celular: chamados, detalhes e entrega          <b>Alt + clique</b>   usar o celular com o mouse\n" +
        "<b>Esc</b>   fecha a tela aberta ou o celular; no escritório, pausa\n" +
        "<b>Setas</b>   trocam a vista nas telas de manutenção\n" +
        "Ícone vermelho = ação bloqueada (ex.: equipamento ainda preso por cabo)\n\n" +
        "<b>Atendimento</b>: leia o chamado no celular → vá até a estação indicada (seta) → diagnostique e conserte\n" +
        "(peças e cabos ficam nos gaveteiros e armários da sala de TI) → confira se funciona → entregue pelo celular.\n" +
        "Cuidado com o supervisor: quedas e itens largados pelo chão custam pontos.";

    private void Button(RectTransform parent, string text, Color color, Vector2 pos, UnityEngine.Events.UnityAction action)
    {
        Image bg = Box(parent, "Botao_" + text, color);
        Place(bg.rectTransform, new Vector2(520f, 96f), pos);
        bg.raycastTarget = true;
        var b = bg.gameObject.AddComponent<UnityEngine.UI.Button>();
        b.targetGraphic = bg;
        ColorBlock cb = b.colors;
        cb.highlightedColor = new Color(1.12f, 1.12f, 1.12f); cb.pressedColor = new Color(0.8f, 0.8f, 0.8f);
        b.colors = cb;
        b.onClick.AddListener(action);
        Text t = Label(bg.rectTransform, text, 40, FontStyle.Bold, Color.white, new Vector2(500f, 80f), Vector2.zero);
        t.gameObject.AddComponent<Outline>().effectColor = Ink;
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
        t.font = font; t.text = text; t.fontSize = size; t.fontStyle = style; t.color = color; t.supportRichText = true;
        t.alignment = TextAnchor.MiddleCenter; t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }
}
