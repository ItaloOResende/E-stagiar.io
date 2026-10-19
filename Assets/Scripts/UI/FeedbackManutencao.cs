using UnityEngine;

/// <summary>Tipo do recado da manutenção: muda o ícone e a cor da borda do cartão.</summary>
public enum TipoFeedback { Sucesso, Diagnostico, Alerta, Info, Erro }

/// <summary>
/// Recados curtos de TODOS os minigames (PC, notebook, impressora, crimpagem): um só padrão visual — o cartão do
/// <see cref="AvisoRodape"/> (arredondado, ícone, sobe do rodapé e some sozinho) na raiz da tela 2D. Tipos:
/// Sucesso (verde), Alerta = requisito não atendido (amarelo), Erro = ação recusada (vermelho), Diagnóstico (azul),
/// Info (cinza). Regra de texto: diz o QUE falta ou falhou, nunca a solução (qual ferramenta, qual valor, qual ordem).
/// Só para ações relevantes: não é para narrar cada clique.
/// </summary>
public static class FeedbackManutencao
{
    private static readonly Color Verde = new Color(0.35f, 0.82f, 0.47f);
    private static readonly Color Azul = new Color(0.45f, 0.72f, 1f);
    private static readonly Color Amarelo = new Color(0.98f, 0.78f, 0.24f);
    private static readonly Color Cinza = new Color(0.7f, 0.74f, 0.8f);
    private static readonly Color Vermelho = new Color(0.95f, 0.4f, 0.4f);

    /// <summary>Tipo pelas cores antigas das telas: verde = sucesso, vermelho = erro, amarelo = requisito.</summary>
    public static TipoFeedback TipoDaCor(bool verde, bool vermelho) =>
        verde ? TipoFeedback.Sucesso : vermelho ? TipoFeedback.Erro : TipoFeedback.Alerta;

    public static void Mostrar(Component tela, string mensagem, TipoFeedback tipo)
    {
        if (tela == null || string.IsNullOrEmpty(mensagem)) return;
        AvisoRodape aviso = tela.GetComponent<AvisoRodape>();
        if (aviso == null) aviso = tela.gameObject.AddComponent<AvisoRodape>();
        aviso.Mostrar(mensagem, Icone(tipo), Cor(tipo));
    }

    private static Color Cor(TipoFeedback t) =>
        t == TipoFeedback.Sucesso ? Verde : t == TipoFeedback.Diagnostico ? Azul : t == TipoFeedback.Alerta ? Amarelo
        : t == TipoFeedback.Erro ? Vermelho : Cinza;

    private static Sprite Icone(TipoFeedback t)
    {
        string nome = t == TipoFeedback.Sucesso ? "feedback_ok" : t == TipoFeedback.Alerta || t == TipoFeedback.Erro ? "feedback_alerta"
                    : t == TipoFeedback.Diagnostico ? "feedback_diagnostico" : null;
        return nome != null ? Resources.Load<Sprite>("Interacao/" + nome) : null;
    }

    /// <summary>Nome da peça do PC para os recados.</summary>
    public static string Nome(PcComponent c)
    {
        switch (c)
        {
            case PcComponent.Motherboard: return "Placa-mãe";
            case PcComponent.Ram: return "Memória RAM";
            case PcComponent.Hdd: return "HD";
            case PcComponent.Psu: return "Fonte";
            case PcComponent.CpuCooler: return "Cooler";
            default: return c.ToString();
        }
    }
}
