using UnityEngine;

/// <summary>
/// HUD provisoria da fase, no topo central. Usa o tempo real do StageController.
/// Nao cria um segundo temporizador nem modifica os minigames.
/// </summary>
[DisallowMultipleComponent]
public class StageTimerHUD : MonoBehaviour
{
    [SerializeField] private StageController stageController;
    [SerializeField] private TicketManager ticketManager;
    [Header("Visual")]
    [SerializeField, Min(220f)] private float width = 410f;
    [SerializeField, Min(10f)] private float barHeight = 16f;
    [SerializeField, Min(0f)] private float topMargin = 12f;

    private static readonly Color Green = new Color(0.13f, 0.72f, 0.35f);
    private static readonly Color Yellow = new Color(0.95f, 0.80f, 0.15f);
    private static readonly Color Orange = new Color(1f, 0.50f, 0.10f);
    private static readonly Color Red = new Color(0.85f, 0.18f, 0.18f);
    private static readonly Color Track = new Color(0.05f, 0.07f, 0.09f, 0.95f);
    private static readonly Color Backdrop = new Color(0.08f, 0.10f, 0.14f, 0.87f);

    private GUIStyle labelStyle;

    private void Awake()
    {
        if (stageController == null) stageController = GetComponent<StageController>();
        if (ticketManager == null) ticketManager = GetComponent<TicketManager>();
    }

    private void OnGUI()
    {
        if (stageController == null || ticketManager == null) return;
        int stage = ticketManager.StageNumber;
        if (stage == 0) return; // Tutorial sem cronometro.

        float duration = Mathf.Max(1f, stageController.DurationSeconds);
        float remaining = Mathf.Clamp(stageController.SecondsRemaining, 0f, duration);
        float fraction = Mathf.Clamp01(remaining / duration);
        int seconds = Mathf.CeilToInt(remaining);
        string text = $"FASE {stage}   |   {seconds / 60:00}:{seconds % 60:00}";

        float w = Mathf.Min(width, Mathf.Max(220f, Screen.width - 24f));
        float x = (Screen.width - w) * 0.5f;
        float y = topMargin;
        float totalHeight = 58f + barHeight;

        DrawRect(new Rect(x, y, w, totalHeight), Backdrop);
        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18,
                fontStyle = FontStyle.Bold
            };
            labelStyle.normal.textColor = Color.white;
        }
        GUI.Label(new Rect(x + 8f, y + 6f, w - 16f, 32f), text, labelStyle);

        Rect track = new Rect(x + 14f, y + 41f, w - 28f, barHeight);
        DrawRect(track, Track);
        if (fraction > 0f)
        {
            Color fill = fraction > 0.65f ? Green : fraction > 0.40f ? Yellow : fraction > 0.20f ? Orange : Red;
            DrawRect(new Rect(track.x, track.y, track.width * fraction, track.height), fill);
        }
    }

    private static void DrawRect(Rect rect, Color color)
    {
        Color previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previous;
    }
}
