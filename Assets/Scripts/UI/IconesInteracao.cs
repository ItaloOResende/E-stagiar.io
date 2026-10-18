using UnityEngine;

/// <summary>
/// Ícones cartoon das interações do HUD (sprites em <c>Resources/Interacao</c>, gerados por
/// <c>Tools/icons/gerar_icones_interacao.py</c>). A mãozinha continua a de pixel art (<c>mao_pixel</c>, cópia do
/// <c>PH_ui_mao</c>), como o cursor das telas. O ícone depende da AÇÃO e do EQUIPAMENTO mirado, não só do clique:
///  - esquerdo: pegar/usar (mãozinha), abrir (gaveta/porta), crimpar (alicate, caixa de cabo com o alicate na mão),
///    rack, testador;
///  - direito: manutenção do gabinete = chave de fenda; notebook = notebook com chave; impressora = impressora;
///    monitor = monitor; testador = testador.
/// Sem regra para o alvo, vale o <see cref="Interactable.promptIcon"/> dele (o campo continua servindo para objetos
/// novos). Carregados uma vez, sob demanda.
/// </summary>
public static class IconesInteracao
{
    private const string Pasta = "Interacao/";

    private static Sprite Carregar(string nome) => Resources.Load<Sprite>(Pasta + nome);

    private static Sprite mao, abrir, chave, crimpador, impressora, notebook, monitor, rack, testador, bloqueado;
    public static Sprite Pegar => mao != null ? mao : mao = Carregar("mao_pixel");
    public static Sprite Usar => Pegar;
    public static Sprite Bloqueado => bloqueado != null ? bloqueado : bloqueado = Carregar("icone_bloqueado");
    private static Sprite Abrir => abrir != null ? abrir : abrir = Carregar("icone_abrir");
    private static Sprite ChaveDeFenda => chave != null ? chave : chave = Carregar("icone_chave_fenda");
    private static Sprite Crimpador => crimpador != null ? crimpador : crimpador = Carregar("icone_crimpador");
    private static Sprite Impressora => impressora != null ? impressora : impressora = Carregar("icone_impressora");
    private static Sprite Notebook => notebook != null ? notebook : notebook = Carregar("icone_notebook");
    private static Sprite Monitor => monitor != null ? monitor : monitor = Carregar("icone_monitor");
    private static Sprite Rack => rack != null ? rack : rack = Carregar("icone_rack");
    private static Sprite Testador => testador != null ? testador : testador = Carregar("icone_testador");

    /// <summary>Ícone da ação do alvo: <paramref name="esquerdo"/> = clique esquerdo; senão, direito.</summary>
    public static Sprite Para(Interactable alvo, bool esquerdo)
    {
        if (alvo == null) return null;
        Sprite s = esquerdo ? Esquerdo(alvo) : Direito(alvo);
        return s != null ? s : alvo.promptIcon;
    }

    private static Sprite Esquerdo(Interactable alvo)
    {
        switch (alvo)
        {
            case PickupInteractable _: return Pegar;
            case OpenableInteractable _: return Abrir;
            case TestadorCaboInteractable _: return Testador;
            case RedeEstacaoInteractable rede:
                if (rede.screen != null && rede.screen.GetComponentInChildren<RedeRackScreen>(true) != null) return Rack;
                return Crimpador;   // a caixa de cabo de rede (crimpagem)
            case ImpressoraPainelInteractable _: return Impressora;
            default: return alvo.promptIcon != null ? null : Usar;
        }
    }

    private static Sprite Direito(Interactable alvo)
    {
        switch (alvo)
        {
            case MonitorScreenInteractable _: return Monitor;
            case ImpressoraPainelInteractable _: return Impressora;
            case TestadorCaboInteractable _: return Testador;
        }
        // UIScreenInteractable e outros: pelo equipamento ao qual pertencem
        if (alvo.GetComponentInParent<ComputerCase>() != null) return ChaveDeFenda;
        if (alvo.GetComponentInParent<NotebookEstacao>() != null) return Notebook;
        if (alvo.GetComponentInParent<ImpressoraEstacao>() != null) return Impressora;
        if (alvo.GetComponentInParent<MonitorDisplay>() != null) return Monitor;
        return null;
    }
}
