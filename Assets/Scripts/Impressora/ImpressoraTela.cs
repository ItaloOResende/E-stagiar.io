using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>Vistas da impressora (a mesma tela, trocando de vista pelos botões Anterior/Próxima).</summary>
public enum ImpressoraVista { Toner, Papel, Painel }

/// <summary>
/// Tela 2D da impressora. Monta a própria interface por código (retângulos, textos e botões), então não
/// depende de nenhuma arte do projeto — quando houver arte de verdade, basta trocar as formas por sprites.
///
/// Três vistas:
///  - <b>Toner</b>: o compartimento do cartucho.
///  - <b>Papel</b>: a bandeja de papel.
///  - <b>Painel</b>: o painel da impressora (visor + teclado numérico) para configurar o IP.
///
/// O estado NÃO fica aqui: fica no <see cref="ImpressoraEstacao"/> do objeto do mundo (<c>UIScreen.Source</c>).
/// </summary>
[RequireComponent(typeof(UIScreen))]
public class ImpressoraTela : MonoBehaviour
{
    private static readonly Color CorPainel = new Color(0.09f, 0.11f, 0.14f);
    private static readonly Color CorCorpo = new Color(0.2f, 0.22f, 0.26f);
    private static readonly Color CorCorpoClaro = new Color(0.26f, 0.29f, 0.34f);
    private static readonly Color CorBorda = new Color(0.3f, 0.35f, 0.42f);
    private static readonly Color CorVazio = new Color(0.1f, 0.12f, 0.15f);
    private static readonly Color CorLcd = new Color(0.06f, 0.14f, 0.1f);
    private static readonly Color CorToner = new Color(0.25f, 0.28f, 0.34f);
    private static readonly Color CorPapel = new Color(0.85f, 0.86f, 0.82f);
    private static readonly Color CorTecla = new Color(0.3f, 0.34f, 0.4f);
    private static readonly Color TextoClaro = new Color(0.9f, 0.93f, 0.97f);
    private static readonly Color TextoFraco = new Color(0.6f, 0.66f, 0.74f);
    private static readonly Color TextoLcd = new Color(0.55f, 0.95f, 0.6f);
    private static readonly Color Verde = new Color(0.36f, 0.85f, 0.45f);
    private static readonly Color Amarelo = new Color(1f, 0.82f, 0.25f);
    private static readonly Color Vermelho = new Color(0.95f, 0.4f, 0.4f);

    private static readonly string[] Teclas = { "1", "2", "3", "4", "5", "6", "7", "8", "9", ".", "0", "Apagar" };

    private bool construido;
    private UIScreen tela;

    private ImpressoraVista vista = ImpressoraVista.Toner;
    private RectTransform[] vistas;
    private Text tituloTxt, instrTxt, msgTxt;

    // Vista "Toner".
    private Image tonerMiolo, tonerCartucho;

    // Vista "Papel".
    private Image papelMiolo;
    private RectTransform pilhaPapel;

    // Vista "Painel".
    private Text lcdStatus, lcdIp;

    private void Awake() => Construir();

    private void OnEnable()
    {
        if (tela == null) tela = GetComponent<UIScreen>();
        ImpressoraEstacao est = Estacao();
        if (est != null)
        {
            // Abre na vista que ainda falta resolver; se estiver tudo pronto, vai direto ao painel.
            if (!est.Sessao.TemToner) vista = ImpressoraVista.Toner;
            else if (!est.Sessao.TemPapel) vista = ImpressoraVista.Papel;
            else vista = ImpressoraVista.Painel;
        }
        else vista = ImpressoraVista.Toner;

        Mensagem("", TextoFraco);
        Atualizar();
    }

    private void Update()
    {
        if (construido && vista == ImpressoraVista.Painel) Atualizar();
    }

    private ImpressoraEstacao Estacao()
    {
        if (tela == null) tela = GetComponent<UIScreen>();
        return tela != null && tela.Source != null ? tela.Source.GetComponent<ImpressoraEstacao>() : null;
    }

    private ItemHolder Mao()
    {
        if (tela == null) tela = GetComponent<UIScreen>();
        return tela != null && tela.Interactor != null ? tela.Interactor.Holder : null;
    }

    // ================================================================== construção

    private void Construir()
    {
        if (construido) return;
        construido = true;

        RedeUi.Esticar((RectTransform)transform);
        RectTransform painel = RedeUi.Cheio(transform, "Painel", CorPainel);
        RedeUi.BotaoFechar(painel, Fechar);

        tituloTxt = RedeUi.Texto(painel, "TituloVista", "", 34, TextAnchor.MiddleCenter, TextoClaro);
        RedeUi.Ancorar((RectTransform)tituloTxt.transform, new Vector2(0.5f, 0.5f), new Vector2(900f, 52f), new Vector2(0.5f, 0.5f));
        ((RectTransform)tituloTxt.transform).anchoredPosition = new Vector2(0f, 486f);
        tituloTxt.fontStyle = FontStyle.Bold;

        RectTransform anterior = RedeUi.Botao(painel, "Anterior", "«  Anterior", new Color(0.22f, 0.25f, 0.3f, 0.95f), () => TrocarVista(-1), 26);
        RedeUi.Ancorar(anterior, new Vector2(0.5f, 0.5f), new Vector2(220f, 56f), new Vector2(0.5f, 0.5f));
        anterior.anchoredPosition = new Vector2(-720f, 486f);

        RectTransform proxima = RedeUi.Botao(painel, "Proxima", "Próxima  »", new Color(0.22f, 0.25f, 0.3f, 0.95f), () => TrocarVista(1), 26);
        RedeUi.Ancorar(proxima, new Vector2(0.5f, 0.5f), new Vector2(220f, 56f), new Vector2(0.5f, 0.5f));
        proxima.anchoredPosition = new Vector2(720f, 486f);

        instrTxt = RedeUi.Texto(painel, "Instrucao", "", 26, TextAnchor.MiddleCenter, TextoFraco);
        RedeUi.Ancorar((RectTransform)instrTxt.transform, new Vector2(0.5f, 0.5f), new Vector2(1600f, 40f), new Vector2(0.5f, 0.5f));
        ((RectTransform)instrTxt.transform).anchoredPosition = new Vector2(0f, -444f);

        RectTransform area = RedeUi.Cheio(painel, "Vistas", Color.clear);
        vistas = new RectTransform[3];
        for (int i = 0; i < 3; i++) vistas[i] = RedeUi.Cheio(area, "Vista" + (ImpressoraVista)i, Color.clear);

        ConstruirToner(vistas[(int)ImpressoraVista.Toner]);
        ConstruirPapel(vistas[(int)ImpressoraVista.Papel]);
        ConstruirPainel(vistas[(int)ImpressoraVista.Painel]);

        msgTxt = RedeUi.Texto(painel, "Mensagem", "", 26, TextAnchor.MiddleCenter, TextoFraco);
        RedeUi.Ancorar((RectTransform)msgTxt.transform, new Vector2(0.5f, 0.5f), new Vector2(1600f, 40f), new Vector2(0.5f, 0.5f));
        ((RectTransform)msgTxt.transform).anchoredPosition = new Vector2(0f, -498f);
        msgTxt.fontStyle = FontStyle.Bold;
    }

    private void ConstruirToner(RectTransform vista)
    {
        RectTransform corpo = RedeUi.Imagem(vista, "Corpo", CorCorpo);
        RedeUi.Ancorar(corpo, new Vector2(0.5f, 0.5f), new Vector2(1000f, 600f), new Vector2(0.5f, 0.5f));
        corpo.anchoredPosition = new Vector2(0f, 10f);

        RectTransform dentro = RedeUi.Imagem(corpo, "Dentro", CorCorpoClaro);
        RedeUi.Esticar(dentro, 12f);

        Rotulo(dentro, "COMPARTIMENTO DO TONER", new Vector2(0f, 200f));

        RectTransform furo = RedeUi.Imagem(dentro, "FuroToner", CorBorda);
        RedeUi.Ancorar(furo, new Vector2(0.5f, 0.5f), new Vector2(560f, 170f), new Vector2(0.5f, 0.5f));
        furo.anchoredPosition = new Vector2(0f, 40f);

        tonerMiolo = RedeUi.Imagem(furo, "Miolo", CorVazio).GetComponent<Image>();
        RedeUi.Esticar((RectTransform)tonerMiolo.transform, 7f);

        tonerCartucho = RedeUi.Imagem(furo, "Cartucho", CorToner).GetComponent<Image>();
        RedeUi.Esticar((RectTransform)tonerCartucho.transform, 7f);

        RectTransform visor = RedeUi.Imagem((RectTransform)tonerCartucho.transform, "Cilindro", new Color(0.55f, 0.5f, 0.45f));
        RedeUi.Ancorar(visor, new Vector2(0.5f, 0f), new Vector2(480f, 26f), new Vector2(0.5f, 0f));
        visor.anchoredPosition = new Vector2(0f, 18f);

        TornarClicavel(furo, () => ClicarEncaixe(ImpressoraParte.Toner));
    }

    private void ConstruirPapel(RectTransform vista)
    {
        RectTransform corpo = RedeUi.Imagem(vista, "Corpo", CorCorpo);
        RedeUi.Ancorar(corpo, new Vector2(0.5f, 0.5f), new Vector2(1000f, 560f), new Vector2(0.5f, 0.5f));
        corpo.anchoredPosition = new Vector2(0f, 30f);

        RectTransform dentro = RedeUi.Imagem(corpo, "Dentro", CorCorpoClaro);
        RedeUi.Esticar(dentro, 12f);

        Rotulo(dentro, "BANDEJA DE PAPEL", new Vector2(0f, 190f));

        RectTransform furo = RedeUi.Imagem(dentro, "FuroBandeja", CorBorda);
        RedeUi.Ancorar(furo, new Vector2(0.5f, 0.5f), new Vector2(700f, 240f), new Vector2(0.5f, 0.5f));
        furo.anchoredPosition = new Vector2(0f, -30f);

        papelMiolo = RedeUi.Imagem(furo, "Miolo", CorVazio).GetComponent<Image>();
        RedeUi.Esticar((RectTransform)papelMiolo.transform, 7f);

        pilhaPapel = RedeUi.Imagem(furo, "Pilha", Color.clear);
        RedeUi.Esticar(pilhaPapel, 10f);
        for (int i = 0; i < 5; i++)
        {
            RectTransform folha = RedeUi.Imagem(pilhaPapel, "Folha" + i, i % 2 == 0 ? CorPapel : new Color(0.78f, 0.79f, 0.75f));
            folha.anchorMin = new Vector2(0f, i * 0.18f);
            folha.anchorMax = new Vector2(1f, i * 0.18f + 0.14f);
            folha.offsetMin = Vector2.zero;
            folha.offsetMax = new Vector2(0f, 0f);
        }

        TornarClicavel(furo, () => ClicarEncaixe(ImpressoraParte.Papel));
    }

    private void ConstruirPainel(RectTransform vista)
    {
        RectTransform lcd = RedeUi.Imagem(vista, "Lcd", CorLcd);
        RedeUi.Ancorar(lcd, new Vector2(0.5f, 0.5f), new Vector2(920f, 230f), new Vector2(0.5f, 0.5f));
        lcd.anchoredPosition = new Vector2(0f, 258f);

        lcdStatus = RedeUi.Texto(lcd, "Status", "", 40, TextAnchor.MiddleCenter, TextoLcd);
        RedeUi.Ancorar((RectTransform)lcdStatus.transform, new Vector2(0.5f, 1f), new Vector2(860f, 70f), new Vector2(0.5f, 1f));
        ((RectTransform)lcdStatus.transform).anchoredPosition = new Vector2(0f, -26f);
        lcdStatus.fontStyle = FontStyle.Bold;

        lcdIp = RedeUi.Texto(lcd, "Ip", "", 44, TextAnchor.MiddleCenter, TextoClaro);
        RedeUi.Ancorar((RectTransform)lcdIp.transform, new Vector2(0.5f, 0f), new Vector2(860f, 70f), new Vector2(0.5f, 0f));
        ((RectTransform)lcdIp.transform).anchoredPosition = new Vector2(0f, 34f);

        // Teclado numérico: 3 colunas x 4 linhas.
        for (int i = 0; i < Teclas.Length; i++)
        {
            string rotulo = Teclas[i];
            int coluna = i % 3;
            int linha = i / 3;
            RectTransform tecla = RedeUi.Botao(vista, "Tecla_" + rotulo, rotulo, CorTecla,
                () => ClicarTecla(rotulo), rotulo.Length > 1 ? 20 : 30);
            RedeUi.Ancorar(tecla, new Vector2(0.5f, 0.5f), rotulo.Length > 1 ? new Vector2(140f, 74f) : new Vector2(110f, 74f), new Vector2(0.5f, 0.5f));
            tecla.anchoredPosition = new Vector2(-160f + coluna * 160f, 20f - linha * 100f);
        }

        RectTransform limpar = RedeUi.Botao(vista, "Limpar", "Limpar", new Color(0.35f, 0.26f, 0.26f), () => { ImpressoraEstacao e = Estacao(); if (e != null) e.Sessao.Limpar(); Mensagem("Campo do IP limpo.", TextoFraco); Atualizar(); }, 24);
        RedeUi.Ancorar(limpar, new Vector2(0.5f, 0.5f), new Vector2(200f, 64f), new Vector2(0.5f, 0.5f));
        limpar.anchoredPosition = new Vector2(440f, -370f);

        RectTransform confirmar = RedeUi.Botao(vista, "Confirmar", "Confirmar IP", Verde, ClicarConfirmar, 26);
        RedeUi.Ancorar(confirmar, new Vector2(0.5f, 0.5f), new Vector2(420f, 64f), new Vector2(0.5f, 0.5f));
        Text rotuloConfirmar = confirmar.GetComponentInChildren<Text>();
        if (rotuloConfirmar != null) rotuloConfirmar.color = new Color(0.05f, 0.08f, 0.05f);
        confirmar.anchoredPosition = new Vector2(0f, -370f);
    }

    private void Rotulo(Transform pai, string texto, Vector2 pos)
    {
        Text t = RedeUi.Texto(pai, "Rotulo_" + texto, texto, 24, TextAnchor.MiddleCenter, TextoClaro);
        RedeUi.Ancorar((RectTransform)t.transform, new Vector2(0.5f, 0.5f), new Vector2(620f, 32f), new Vector2(0.5f, 0.5f));
        ((RectTransform)t.transform).anchoredPosition = pos;
    }

    private void TornarClicavel(RectTransform alvo, UnityAction acao)
    {
        var go = new GameObject("Clique", typeof(RectTransform));
        go.transform.SetParent(alvo, false);
        var img = go.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0f);
        img.raycastTarget = true;
        var b = go.AddComponent<Button>();
        b.targetGraphic = img;
        b.onClick.AddListener(acao);
        RedeUi.Esticar((RectTransform)go.transform);
    }

    // ================================================================== ações

    private void TrocarVista(int passo)
    {
        int n = 3;
        vista = (ImpressoraVista)(((int)vista + passo % n + n) % n);
        Mensagem("", TextoFraco);
        Atualizar();
    }

    private void ClicarEncaixe(ImpressoraParte encaixe)
    {
        ImpressoraEstacao est = Estacao();
        if (est == null) return;

        ItemHolder mao = Mao();
        PickupInteractable naMao = mao != null ? mao.Current : null;

        ImpressoraEncaixeResultado r = est.UsarEncaixe(encaixe, naMao, out GameObject devolver);
        switch (r)
        {
            case ImpressoraEncaixeResultado.Instalado:
                if (mao != null) mao.Stow(est.transform.position);
                Mensagem(encaixe == ImpressoraParte.Toner ? "Cartucho de toner instalado." : "Papel carregado na bandeja.", Verde);
                break;
            case ImpressoraEncaixeResultado.Removido:
                if (mao != null && devolver != null) mao.Retrieve(devolver);
                Mensagem("Peça retirada.", TextoFraco);
                break;
            case ImpressoraEncaixeResultado.PrecisaPeca:
                Mensagem(encaixe == ImpressoraParte.Toner
                    ? "Ponha um cartucho de toner na mão e clique no compartimento."
                    : "Ponha uma resma de papel na mão e clique na bandeja.", Amarelo);
                break;
            case ImpressoraEncaixeResultado.PecaErrada:
                Mensagem("Essa peça não entra aqui.", Vermelho);
                break;
            case ImpressoraEncaixeResultado.MaoCheia:
                Mensagem("Tire a peça da mão para retirar esta.", Amarelo);
                break;
        }
        Atualizar();
    }

    private void ClicarTecla(string tecla)
    {
        ImpressoraEstacao est = Estacao();
        if (est == null) return;

        if (tecla == "Apagar") est.Sessao.Apagar();
        else est.Sessao.Digitar(tecla);

        if (est.Sessao.TemIp) Mensagem("", TextoFraco);
        Atualizar();
    }

    private void ClicarConfirmar()
    {
        ImpressoraEstacao est = Estacao();
        if (est == null) return;

        if (est.Sessao.ConfirmarIp())
            Mensagem("IP " + est.Sessao.IpSalvo + " configurado.", Verde);
        else
            Mensagem("IP inválido. Use 4 números de 0 a 255 (ex.: 192.168.0.10).", Vermelho);
        Atualizar();
    }

    // ================================================================== desenho

    private void Atualizar()
    {
        if (!construido) return;
        ImpressoraEstacao est = Estacao();

        for (int i = 0; i < 3; i++) vistas[i].gameObject.SetActive(i == (int)vista);
        tituloTxt.text = NomeVista(vista);
        instrTxt.text = InstrucaoVista(vista);

        if (est == null) return;

        if (vista == ImpressoraVista.Toner)
        {
            bool tem = est.Sessao.TemToner;
            tonerCartucho.gameObject.SetActive(tem);
            tonerMiolo.gameObject.SetActive(!tem);
        }
        else if (vista == ImpressoraVista.Papel)
        {
            bool tem = est.Sessao.TemPapel;
            pilhaPapel.gameObject.SetActive(tem);
            papelMiolo.gameObject.SetActive(!tem);
        }
        else AtualizarPainel(est.Sessao);
    }

    private void AtualizarPainel(ImpressoraSession s)
    {
        string status;
        if (!s.TemToner) { status = "SEM TONER"; lcdStatus.color = Vermelho; }
        else if (!s.TemPapel) { status = "SEM PAPEL"; lcdStatus.color = Vermelho; }
        else if (!s.TemIp) { status = "CONFIGURE O IP"; lcdStatus.color = Amarelo; }
        else { status = "PRONTA"; lcdStatus.color = TextoLcd; }
        lcdStatus.text = status;

        string ip = s.IpDigitado;
        if (ip.Length == 0) ip = s.IpSalvo.Length > 0 ? s.IpSalvo : "---.---.---.---";
        lcdIp.text = "IP: " + ip;
        lcdIp.color = s.IpDigitado.Length > 0
            ? (ImpressoraSession.IpEhValido(s.IpDigitado) ? Verde : TextoClaro)
            : (s.TemIp ? Verde : TextoFraco);
    }

    private string NomeVista(ImpressoraVista v)
    {
        switch (v)
        {
            case ImpressoraVista.Toner: return "Toner";
            case ImpressoraVista.Papel: return "Papel";
            default: return "Painel";
        }
    }

    private string InstrucaoVista(ImpressoraVista v)
    {
        switch (v)
        {
            case ImpressoraVista.Toner:
                return "Com o cartucho de toner na mão, clique no compartimento para instalar.";
            case ImpressoraVista.Papel:
                return "Com a resma de papel na mão, clique na bandeja para carregar.";
            default:
                return "Digite o IP no teclado e clique em Confirmar IP.";
        }
    }

    private void Mensagem(string texto, Color cor)
    {
        if (msgTxt == null) return;
        msgTxt.text = texto;
        msgTxt.color = cor;
    }

    private void Fechar()
    {
        if (tela != null) tela.RequestClose();
    }
}
