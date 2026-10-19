using UnityEngine;

/// <summary>
/// Ícones cartoon das interações do HUD (sprites em <c>Resources/Interacao</c>, gerados por
/// <c>Tools/icons/gerar_icones_interacao.py</c>). A mãozinha continua a de pixel art (<c>mao_pixel</c>, cópia do
/// <c>PH_ui_mao</c>), como o cursor das telas. O ícone depende da AÇÃO e do EQUIPAMENTO mirado, não só do clique:
///  - esquerdo: pegar/usar (mãozinha), abrir (gaveta/porta), crimpar (alicate, caixa de cabo com o alicate na mão),
///    rack, testador (a impressora não tem clique esquerdo: o componente dele está desligado no prefab);
///  - direito: manutenção do gabinete e do notebook = chave de fenda; impressora = impressora;
///    caixa de cabo = alicate;
///    monitor = monitor; testador = testador.
/// Sem regra para o alvo, vale o <see cref="Interactable.promptIcon"/> dele (o campo continua servindo para objetos
/// novos). Carregados uma vez, sob demanda.
/// </summary>
public static class IconesInteracao
{
    private const string Pasta = "Interacao/";

    private static Sprite Carregar(string nome) => Resources.Load<Sprite>(Pasta + nome);

    private static Sprite mao, abrir, chave, crimpador, impressora, monitor, rack, testador, bloqueado;
    public static Sprite Pegar => mao != null ? mao : mao = Carregar("mao_pixel");
    public static Sprite Usar => Pegar;
    public static Sprite Bloqueado => bloqueado != null ? bloqueado : bloqueado = Carregar("icone_bloqueado");
    private static Sprite Abrir => abrir != null ? abrir : abrir = Carregar("icone_abrir");
    /// <summary>Desenho da chave de fenda (aviso de ferramenta que falta, como o alicate na caixa de cabo).</summary>
    public static Sprite ChaveDeFenda => chave != null ? chave : chave = Carregar("icone_chave_fenda");
    /// <summary>Desenho do alicate de crimpar (aviso de ferramenta que falta na crimpagem).</summary>
    public static Sprite Crimpador => crimpador != null ? crimpador : crimpador = Carregar("icone_crimpador");
    private static Sprite Impressora => impressora != null ? impressora : impressora = Carregar("icone_impressora");
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
                // impressora (se o clique esquerdo dela for religado no prefab): ícone da impressora, nunca o alicate
                if (rede.GetComponentInParent<ImpressoraEstacao>() != null) return Impressora;
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
        // notebook: a manutenção é com a chave de fenda (parafusos da tampa de baixo). Vem ANTES da caixa de cabo: o
        // prefab do notebook tem um RedeEstacaoInteractable DESLIGADO no mesmo objeto, que fazia aparecer o alicate
        // (GetComponent acha componente desligado), e sem checar .Enabled ele roubava o ícone do notebook.
        if (alvo.GetComponentInParent<NotebookEstacao>() != null) return ChaveDeFenda;
        // caixa de cabo de rede: o clique direito abre a crimpagem (o esquerdo pega a caixa, ou crimpa com o alicate)
        RedeEstacaoInteractable caixa = alvo.GetComponent<RedeEstacaoInteractable>();
        if (caixa != null && caixa.enabled && alvo.GetComponentInParent<ImpressoraEstacao>() == null
            && (caixa.screen == null || caixa.screen.GetComponentInChildren<RedeRackScreen>(true) == null)) return Crimpador;
        if (alvo.GetComponentInParent<ImpressoraEstacao>() != null) return Impressora;
        if (alvo.GetComponentInParent<MonitorDisplay>() != null) return Monitor;
        return null;
    }
}
