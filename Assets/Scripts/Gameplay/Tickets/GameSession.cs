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

    private static bool menuRequested, startRequested;

    /// <summary>Cena do expediente (onde ficam o menu inicial e a fase) e cena do tutorial. As duas estão no Build.</summary>
    public const string CenaPrincipal = "Assets/Scenes/Office_Placeholder.unity";
    public const string CenaTutorial = "Assets/Scenes/Tutorial_Montagem.unity";

    /// <summary>A cena ativa é a do tutorial?</summary>
    public static bool NoTutorial => SceneManager.GetActiveScene().path == CenaTutorial;

    /// <summary>A próxima carga da cena principal deve começar o expediente direto, sem o menu? Consome o pedido.</summary>
    public static bool ConsumeStartRequest()
    {
        bool r = startRequested;
        startRequested = false;
        return r;
    }

    /// <summary>Menu inicial → tutorial: carrega a cena do tutorial do zero.</summary>
    public static void StartTutorial()
    {
        Mode = GameMode.Normal;
        InfractionLog.ResetAll();
        Time.timeScale = 1f;
        LoadScene(CenaTutorial);
    }

    /// <summary>Fim do tutorial → "Começar o expediente": carrega a cena principal e começa a fase sem passar pelo menu.</summary>
    public static void StartWorkday()
    {
        startRequested = true;
        Mode = GameMode.Normal;
        InfractionLog.ResetAll();
        Time.timeScale = 1f;
        LoadScene(CenaPrincipal);
    }

    /// <summary>A próxima carga de cena deve abrir no menu inicial? Consome o pedido.</summary>
    public static bool ConsumeMenuRequest()
    {
        bool r = menuRequested;
        menuRequested = false;
        return r;
    }

    /// <summary>
    /// "Ir para o menu inicial": limpa o estado global da partida e carrega a cena PRINCIPAL do zero (de qualquer cena,
    /// inclusive do tutorial: nada da partida anterior sobrevive); ela abre no menu inicial.
    /// </summary>
    public static void GoToMainMenu()
    {
        menuRequested = true;
        startRequested = false;
        Mode = GameMode.Normal;
        InfractionLog.ResetAll();
        Time.timeScale = 1f;
        LoadScene(CenaPrincipal);
    }

    /// <summary>Recarrega a cena ativa (no Editor também funciona com cenas fora do Build Settings).</summary>
    public static void ReloadActiveScene() => LoadScene(SceneManager.GetActiveScene().path);

    /// <summary>
    /// Carrega uma cena pelo caminho. No jogo exportado ela precisa estar no Build Settings (carrega pelo índice); no
    /// Editor, uma cena fora do Build também carrega.
    /// </summary>
    public static void LoadScene(string caminho)
    {
        int indice = SceneUtility.GetBuildIndexByScenePath(caminho);
        if (indice >= 0) { SceneManager.LoadScene(indice); return; }
#if UNITY_EDITOR
        UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(caminho, new LoadSceneParameters(LoadSceneMode.Single));
#else
        Debug.LogError("GameSession: a cena " + caminho + " não está no Build Settings.");
#endif
    }
}
