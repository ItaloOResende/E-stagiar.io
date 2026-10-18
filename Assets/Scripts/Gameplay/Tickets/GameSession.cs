using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Modo da partida. Valores novos no fim.</summary>
public enum GameMode
{
    /// <summary>Expediente: relógio da fase, chamados param de chegar perto do fim, encerra cedo sem nada pendente.</summary>
    Normal
}

/// <summary>
/// Estado da sessão de jogo que sobrevive à troca/recarga de cena: o modo escolhido no menu inicial e o pedido de abrir
/// o menu na próxima carga. Tudo o mais (pontuação, chamados, rack, equipamentos) vive na cena e é refeito ao recarregar.
/// </summary>
public static class GameSession
{
    /// <summary>Modo da partida atual (definido no menu inicial; padrão: Normal).</summary>
    public static GameMode Mode { get; set; } = GameMode.Normal;

    private static bool menuRequested;

    /// <summary>A próxima carga de cena deve abrir no menu inicial? Consome o pedido.</summary>
    public static bool ConsumeMenuRequest()
    {
        bool r = menuRequested;
        menuRequested = false;
        return r;
    }

    /// <summary>
    /// "Ir para o menu inicial": limpa o estado global da partida e RECARREGA a cena do zero (nada da partida anterior
    /// sobrevive: chamados, pontos, rack e equipamentos voltam ao estado inicial); a cena abre no menu inicial.
    /// </summary>
    public static void GoToMainMenu()
    {
        menuRequested = true;
        Mode = GameMode.Normal;
        InfractionLog.ResetAll();
        Time.timeScale = 1f;
        ReloadActiveScene();
    }

    /// <summary>Recarrega a cena ativa (no Editor também funciona com cenas fora do Build Settings).</summary>
    public static void ReloadActiveScene()
    {
        Scene cena = SceneManager.GetActiveScene();
        if (cena.buildIndex >= 0) { SceneManager.LoadScene(cena.buildIndex); return; }
#if UNITY_EDITOR
        UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(cena.path, new LoadSceneParameters(LoadSceneMode.Single));
#else
        SceneManager.LoadScene(cena.name);
#endif
    }
}
