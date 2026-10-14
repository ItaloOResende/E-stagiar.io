using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Vistas do notebook (a mesma tela, trocando de vista pelos botões Anterior/Próxima).</summary>
public enum NotebookVista { Baixo, Aberto, Lado, Frente }

/// <summary>
/// Tela 2D do notebook. Monta a própria interface por código (só primitivas de UI: retângulos, textos,
/// botões e um círculo gerado em execução), então não depende de nenhuma arte do projeto — quando houver
/// arte de verdade, basta trocar as formas por sprites aqui.
///
/// Quatro vistas, como o PC:
///  - <b>Baixo</b>: a tampa de baixo com os parafusos (chave de fenda) para abrir o notebook.
///  - <b>Aberto</b>: os encaixes da memória SO-DIMM e do SSD.
///  - <b>Lado</b>: a porta USB para o pendrive.
///  - <b>Frente</b>: a tela do notebook e o botão Power (BIOS, instalação pelo pendrive, sistema).
///
/// O estado NÃO fica aqui: fica no <see cref="NotebookEstacao"/> do objeto do mundo (<c>UIScreen.Source</c>),
/// então fechar e reabrir a tela não perde nada.
/// </summary>
[RequireComponent(typeof(UIScreen))]
public class NotebookTela : MonoBehaviour
{
    private static readonly Color CorPainel = new Color(0.09f, 0.11f, 0.14f);
    private static readonly Color CorChassi = new Color(0.17f, 0.2f, 0.24f);
    private static readonly Color CorChassiClaro = new Color(0.23f, 0.27f, 0.32f);
    private static readonly Color CorBorda = new Color(0.3f, 0.35f, 0.42f);
    private static readonly Color CorVazio = new Color(0.11f, 0.13f, 0.16f);
    private static readonly Color CorMetal = new Color(0.63f, 0.67f, 0.73f);
    private static readonly Color CorPlaca = new Color(0.16f, 0.42f, 0.24f);
    private static readonly Color CorSsd = new Color(0.2f, 0.23f, 0.28f);
    private static readonly Color CorTela = new Color(0.05f, 0.06f, 0.08f);
    private static readonly Color TextoClaro = new Color(0.9f, 0.93f, 0.97f);
    private static readonly Color TextoFraco = new Color(0.6f, 0.66f, 0.74f);
    private static readonly Color Verde = new Color(0.36f, 0.85f, 0.45f);
    private static readonly Color Amarelo = new Color(1f, 0.82f, 0.25f);
    private static readonly Color Vermelho = new Color(0.95f, 0.4f, 0.4f);
    private static readonly Color Azul = new Color(0.42f, 0.7f, 1f);

    // Posições dos parafusos da tampa de baixo (fração da tampa; 0 = centro).
    private static readonly Vector2[] PosicaoParafusos =
    {
        new Vector2(-0.43f, 0.33f), new Vector2(0.43f, 0.33f),
        new Vector2(-0.43f, 0.0f), new Vector2(0.43f, 0.0f),
        new Vector2(-0.43f, -0.33f), new Vector2(0.43f, -0.33f)
    };

    private static readonly Vector2 TamanhoTampa = new Vector2(1150f, 640f);
    private static readonly Vector2 TamanhoFuroRam = new Vector2(430f, 120f);
    private static readonly Vector2 TamanhoFuroSsd = new Vector2(400f, 150f);

    private bool construido;
    private UIScreen tela;

    private NotebookVista vista = NotebookVista.Baixo;
    private RectTransform[] vistas;
    private Text msgTxt;

    // Vista "Baixo".
    private Image[] parafusoImg;
    private RectTransform[] parafusoFenda;
    private float[] parafusoFlash;

    // Vista "Aberto".
    private Image ramMiolo, ramPeca, ssdMiolo, ssdPeca;

    // Vista "Lado".
    private Image usbMiolo, pendriveImg;

    // Vista "Frente".
    private Image telaFundo, telaPower, barraFill;
    private Text telaTxt, etapaTxt;

    private void Awake() => Construir();

    private void OnEnable()
    {
        if (tela == null) tela = GetComponent<UIScreen>();
        // Começa na vista útil: se o notebook já está aberto, vai direto ao interior.
        NotebookEstacao est = Estacao();
        vista = est != null && est.Sessao.Estado == NotebookEstado.Aberto ? NotebookVista.Aberto : NotebookVista.Baixo;
        Mensagem("", TextoFraco);
        Atualizar();
    }

    private void Update()
    {
        if (!construido || gameObject.activeInHierarchy == false) return;

        // As setas do teclado fazem o MESMO que os botões ◄ ► (que continuam funcionando).
        Keyboard teclado = Keyboard.current;
        if (teclado != null)
        {
            if (teclado.leftArrowKey.wasPressedThisFrame) TrocarVista(-1);
            else if (teclado.rightArrowKey.wasPressedThisFrame) TrocarVista(1);
        }

        bool precisa = false;
        for (int i = 0; i < parafusoFlash.Length; i++)
        {
            if (parafusoFlash[i] > 0f)
            {
                parafusoFlash[i] = Mathf.Max(0f, parafusoFlash[i] - Time.deltaTime);
                precisa = true;
            }
        }
        if (vista == NotebookVista.Frente || vista == NotebookVista.Aberto || precisa) Atualizar();
    }

    private NotebookEstacao Estacao()
    {
        if (tela == null) tela = GetComponent<UIScreen>();
        return tela != null && tela.Source != null ? tela.Source.GetComponent<NotebookEstacao>() : null;
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

        RectTransform area = RedeUi.Cheio(painel, "Vistas", Color.clear);
        vistas = new RectTransform[4];
        for (int i = 0; i < 4; i++) vistas[i] = RedeUi.Cheio(area, "Vista" + (NotebookVista)i, Color.clear);

        ConstruirBaixo(vistas[(int)NotebookVista.Baixo]);
        ConstruirAberto(vistas[(int)NotebookVista.Aberto]);
        ConstruirLado(vistas[(int)NotebookVista.Lado]);
        ConstruirFrente(vistas[(int)NotebookVista.Frente]);

        // Setas IGUAIS às do canvas do PC (UI_Screen_Gabinete): os cantos de baixo, sem título de vista e sem
        // dica escrita — a própria tela mostra o que fazer.
        RectTransform anterior = RedeUi.Botao(painel, "Anterior", "\u25C4", new Color(0.22f, 0.25f, 0.3f, 0.95f), () => TrocarVista(-1), 40);
        RedeUi.Ancorar(anterior, new Vector2(0f, 0f), new Vector2(110f, 90f), new Vector2(0f, 0f));
        anterior.anchoredPosition = new Vector2(24f, 24f);

        RectTransform proxima = RedeUi.Botao(painel, "Proxima", "\u25BA", new Color(0.22f, 0.25f, 0.3f, 0.95f), () => TrocarVista(1), 40);
        RedeUi.Ancorar(proxima, new Vector2(1f, 0f), new Vector2(110f, 90f), new Vector2(1f, 0f));
        proxima.anchoredPosition = new Vector2(-24f, 24f);

        msgTxt = RedeUi.Texto(painel, "Mensagem", "", 26, TextAnchor.MiddleCenter, TextoFraco);
        RedeUi.Ancorar((RectTransform)msgTxt.transform, new Vector2(0.5f, 0.5f), new Vector2(1300f, 40f), new Vector2(0.5f, 0.5f));
        ((RectTransform)msgTxt.transform).anchoredPosition = new Vector2(0f, -498f);
        msgTxt.fontStyle = FontStyle.Bold;
    }

    private void ConstruirBaixo(RectTransform vista)
    {
        RectTransform tampa = RedeUi.Imagem(vista, "Tampa", CorChassi);
        RedeUi.Ancorar(tampa, new Vector2(0.5f, 0.5f), TamanhoTampa, new Vector2(0.5f, 0.5f));
        tampa.anchoredPosition = new Vector2(0f, 20f);

        RectTransform dentro = RedeUi.Imagem(tampa, "Dentro", CorChassiClaro);
        RedeUi.Esticar(dentro, 10f);

        parafusoImg = new Image[PosicaoParafusos.Length];
        parafusoFenda = new RectTransform[PosicaoParafusos.Length];
        parafusoFlash = new float[PosicaoParafusos.Length];

        for (int i = 0; i < PosicaoParafusos.Length; i++)
        {
            int indice = i;
            Image parafuso = MiniUi.BotaoCircular(dentro, "Parafuso" + i, CorMetal, 82f, () => ClicarParafuso(indice));
            RectTransform rt = (RectTransform)parafuso.transform;
            rt.anchoredPosition = new Vector2(PosicaoParafusos[i].x * TamanhoTampa.x, PosicaoParafusos[i].y * TamanhoTampa.y);

            // Fenda do parafuso (só decorativa, não clicável).
            RectTransform fenda = RedeUi.Imagem(rt, "Fenda", new Color(0.16f, 0.18f, 0.21f));
            RedeUi.Ancorar(fenda, new Vector2(0.5f, 0.5f), new Vector2(52f, 9f), new Vector2(0.5f, 0.5f));
            fenda.localRotation = Quaternion.Euler(0f, 0f, 32f);

            parafusoImg[i] = parafuso;
            parafusoFenda[i] = fenda;
        }
    }

    private void ConstruirAberto(RectTransform vista)
    {
        RectTransform chassi = RedeUi.Imagem(vista, "Chassi", CorChassi);
        RedeUi.Ancorar(chassi, new Vector2(0.5f, 0.5f), new Vector2(1250f, 660f), new Vector2(0.5f, 0.5f));
        chassi.anchoredPosition = new Vector2(0f, 20f);

        RectTransform placa = RedeUi.Imagem(chassi, "Placa", new Color(0.12f, 0.26f, 0.18f));
        RedeUi.Ancorar(placa, new Vector2(0.5f, 0.5f), new Vector2(1130f, 560f), new Vector2(0.5f, 0.5f));

        // ---- encaixe da memória SO-DIMM (em cima, deitada)
        RectTransform furoRam = RedeUi.Imagem(placa, "FuroRam", CorBorda);
        RedeUi.Ancorar(furoRam, new Vector2(0.5f, 0.5f), TamanhoFuroRam, new Vector2(0.5f, 0.5f));
        furoRam.anchoredPosition = new Vector2(-60f, 150f);
        ramMiolo = RedeUi.Imagem(furoRam, "Miolo", CorVazio).GetComponent<Image>();
        RedeUi.Esticar((RectTransform)ramMiolo.transform, 6f);
        ramPeca = RedeUi.Imagem(furoRam, "Peca", CorPlaca).GetComponent<Image>();
        RedeUi.Esticar((RectTransform)ramPeca.transform, 6f);
        Rotulo(placa, "RAM (SO-DIMM)", new Vector2(-60f, 232f));
        TornarClicavel(furoRam, () => ClicarEncaixe(NotebookParte.Ram));

        // ---- encaixe do SSD (embaixo)
        RectTransform furoSsd = RedeUi.Imagem(placa, "FuroSsd", CorBorda);
        RedeUi.Ancorar(furoSsd, new Vector2(0.5f, 0.5f), TamanhoFuroSsd, new Vector2(0.5f, 0.5f));
        furoSsd.anchoredPosition = new Vector2(60f, -150f);
        ssdMiolo = RedeUi.Imagem(furoSsd, "Miolo", CorVazio).GetComponent<Image>();
        RedeUi.Esticar((RectTransform)ssdMiolo.transform, 6f);
        ssdPeca = RedeUi.Imagem(furoSsd, "Peca", CorSsd).GetComponent<Image>();
        RedeUi.Esticar((RectTransform)ssdPeca.transform, 6f);
        Rotulo(placa, "SSD (M.2)", new Vector2(60f, -240f));
        TornarClicavel(furoSsd, () => ClicarEncaixe(NotebookParte.Ssd));
    }

    private void ConstruirLado(RectTransform vista)
    {
        RectTransform corpo = RedeUi.Imagem(vista, "Corpo", CorChassi);
        RedeUi.Ancorar(corpo, new Vector2(0.5f, 0.5f), new Vector2(1150f, 470f), new Vector2(0.5f, 0.5f));
        corpo.anchoredPosition = new Vector2(0f, 10f);

        RectTransform dentro = RedeUi.Imagem(corpo, "Dentro", CorChassiClaro);
        RedeUi.Esticar(dentro, 10f);

        Rotulo(dentro, "PERFIL DIREITO DO NOTEBOOK", new Vector2(0f, 160f));

        RectTransform furoUsb = RedeUi.Imagem(dentro, "Usb", CorBorda);
        RedeUi.Ancorar(furoUsb, new Vector2(0.5f, 0.5f), new Vector2(190f, 90f), new Vector2(0.5f, 0.5f));
        furoUsb.anchoredPosition = new Vector2(-350f, 0f);
        usbMiolo = RedeUi.Imagem(furoUsb, "Miolo", CorVazio).GetComponent<Image>();
        RedeUi.Esticar((RectTransform)usbMiolo.transform, 7f);
        Rotulo(dentro, "USB", new Vector2(-350f, -80f));
        TornarClicavel(furoUsb, ClicarUsb);

        // O pendrive "entrando" pela esquerda da porta.
        pendriveImg = RedeUi.Imagem(dentro, "Pendrive", new Color(0.25f, 0.3f, 0.36f)).GetComponent<Image>();
        RedeUi.Ancorar((RectTransform)pendriveImg.transform, new Vector2(0.5f, 0.5f), new Vector2(210f, 74f), new Vector2(0.5f, 0.5f));
        ((RectTransform)pendriveImg.transform).anchoredPosition = new Vector2(-560f, 0f);

        RectTransform led = RedeUi.Imagem((RectTransform)pendriveImg.transform, "Led", Verde);
        RedeUi.Ancorar(led, new Vector2(1f, 0.5f), new Vector2(14f, 14f), new Vector2(0.5f, 0.5f));
        led.anchoredPosition = new Vector2(10f, 0f);
    }

    private void ConstruirFrente(RectTransform vista)
    {
        // Tela do notebook (aberto, de frente).
        RectTransform tampa = RedeUi.Imagem(vista, "Tampa", CorChassi);
        RedeUi.Ancorar(tampa, new Vector2(0.5f, 0.5f), new Vector2(1040f, 540f), new Vector2(0.5f, 0.5f));
        tampa.anchoredPosition = new Vector2(0f, 150f);

        telaFundo = RedeUi.Imagem(tampa, "Display", CorTela).GetComponent<Image>();
        RedeUi.Esticar((RectTransform)telaFundo.transform, 16f);

        telaTxt = RedeUi.Texto(telaFundo.transform, "Conteudo", "", 30, TextAnchor.MiddleCenter, TextoClaro);
        RedeUi.Esticar((RectTransform)telaTxt.transform, 40f);
        telaTxt.fontStyle = FontStyle.Bold;

        // Base com o teclado e o botão Power.
        RectTransform baseNote = RedeUi.Imagem(vista, "Base", CorChassi);
        RedeUi.Ancorar(baseNote, new Vector2(0.5f, 0.5f), new Vector2(1080f, 200f), new Vector2(0.5f, 0.5f));
        baseNote.anchoredPosition = new Vector2(0f, -230f);

        RectTransform teclado = RedeUi.Imagem(baseNote, "Teclado", new Color(0.28f, 0.32f, 0.37f));
        RedeUi.Ancorar(teclado, new Vector2(0.5f, 0.5f), new Vector2(760f, 140f), new Vector2(0.5f, 0.5f));
        teclado.anchoredPosition = new Vector2(-60f, 0f);

        telaPower = MiniUi.BotaoCircular(baseNote, "Power", CorMetal, 84f, ClicarPower);
        ((RectTransform)telaPower.transform).anchoredPosition = new Vector2(420f, 0f);

        RectTransform simbolo = RedeUi.Imagem(telaPower.transform, "Simbolo", new Color(0.14f, 0.16f, 0.19f));
        RedeUi.Ancorar((RectTransform)simbolo.transform, new Vector2(0.5f, 0.5f), new Vector2(10f, 32f), new Vector2(0.5f, 0.5f));
        simbolo.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);

        Rotulo(baseNote, "POWER", new Vector2(420f, -78f));

        // Barra de progresso da instalação (fica sobre o rodapé da tela).
        barraFill = MiniUi.BarraProgresso(vista, "Barra", new Color(0.16f, 0.19f, 0.23f), Verde,
            new Vector2(0f, -64f), new Vector2(700f, 26f), out RectTransform barraRaiz);
        barraRaiz.gameObject.SetActive(false);

        etapaTxt = RedeUi.Texto(vista, "Etapa", "", 24, TextAnchor.MiddleCenter, TextoClaro);
        RedeUi.Ancorar((RectTransform)etapaTxt.transform, new Vector2(0.5f, 0.5f), new Vector2(900f, 34f), new Vector2(0.5f, 0.5f));
        ((RectTransform)etapaTxt.transform).anchoredPosition = new Vector2(0f, -104f);
    }

    private void Rotulo(Transform pai, string texto, Vector2 pos)
    {
        Text t = RedeUi.Texto(pai, "Rotulo_" + texto, texto, 22, TextAnchor.MiddleCenter, TextoClaro);
        RedeUi.Ancorar((RectTransform)t.transform, new Vector2(0.5f, 0.5f), new Vector2(520f, 30f), new Vector2(0.5f, 0.5f));
        ((RectTransform)t.transform).anchoredPosition = pos;
    }

    /// <summary>Põe um quadrado transparente clicável por cima de um retângulo.</summary>
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
        int n = 4;
        vista = (NotebookVista)(((int)vista + passo % n + n) % n);
        Mensagem("", TextoFraco);
        Atualizar();
    }

    private void ClicarParafuso(int indice)
    {
        NotebookEstacao est = Estacao();
        if (est == null) return;

        ToolType ferramenta = FerramentaNaMao();

        // Furo vazio (parafuso já removido) = recolocar; parafuso no lugar = soltar (estado real de cada um).
        if (est.Sessao.ParafusoRemovido(indice))
        {
            switch (est.InstalarParafuso(indice, ferramenta))
            {
                case NotebookParafusoResultado.Instalado:
                    InteractionSounds.Raise(InteractionSound.ScrewInstalled);
                    Mensagem("Parafuso " + (indice + 1) + " de volta no lugar.", TextoFraco);
                    break;
                case NotebookParafusoResultado.FerramentaErrada:
                    Mensagem("Precisa de uma chave de fenda na mão.", Vermelho);
                    parafusoFlash[indice] = 0.45f;
                    break;
                case NotebookParafusoResultado.JaAberto:
                    Mensagem("A tampa de baixo já saiu — o notebook está aberto.", Amarelo);
                    break;
                default:
                    Mensagem("Não dá para recolocar esse parafuso agora.", Amarelo);
                    break;
            }
            Atualizar();
            return;
        }

        NotebookParafusoResultado r = est.RemoverParafuso(indice, ferramenta);
        switch (r)
        {
            case NotebookParafusoResultado.Removido:
                InteractionSounds.Raise(InteractionSound.ScrewRemoved);
                if (est.Sessao.TodosParafusosRemovidos)
                {
                    est.Abrir();
                    InteractionSounds.Raise(InteractionSound.CaseOpened);   // a tampa abriu (como o gabinete do PC)
                    Mensagem("Tampa solta — o notebook abriu. Use a seta \u25BA e instale a memória e o SSD.", Verde);
                }
                else Mensagem("Parafuso " + (indice + 1) + " removido.", TextoFraco);
                break;
            case NotebookParafusoResultado.FerramentaErrada:
                Mensagem("Precisa de uma chave de fenda na mão.", Vermelho);
                parafusoFlash[indice] = 0.45f;
                break;
            case NotebookParafusoResultado.JaRemovido:
                Mensagem("Esse parafuso já saiu.", Amarelo);
                break;
            default:
                Mensagem("Não dá para soltar esse parafuso agora.", Amarelo);
                break;
        }
        Atualizar();
    }

    private void ClicarEncaixe(NotebookParte encaixe)
    {
        NotebookEstacao est = Estacao();
        if (est == null) return;

        ItemHolder mao = Mao();
        PickupInteractable naMao = mao != null ? mao.Current : null;

        NotebookEncaixeResultado r = est.UsarEncaixe(encaixe, naMao, out GameObject devolver);
        switch (r)
        {
            case NotebookEncaixeResultado.Instalado:
                if (mao != null) mao.Stow(est.transform.position);
                InteractionSounds.Raise(InteractionSound.PartInstalled);
                Mensagem(encaixe == NotebookParte.Ram ? "Memória SO-DIMM instalada." : "SSD instalado.", Verde);
                break;
            case NotebookEncaixeResultado.Removido:
                if (mao != null && devolver != null) mao.Retrieve(devolver);
                InteractionSounds.Raise(InteractionSound.PartRemoved);
                Mensagem("Peça retirada.", TextoFraco);
                break;
            case NotebookEncaixeResultado.PrecisaPeca:
                Mensagem(encaixe == NotebookParte.Ram
                    ? "Ponha uma memória SO-DIMM na mão e clique no encaixe."
                    : "Ponha um SSD na mão e clique no encaixe.", Amarelo);
                break;
            case NotebookEncaixeResultado.PecaErrada:
                Mensagem("Essa peça não encaixa aqui.", Vermelho);
                break;
            case NotebookEncaixeResultado.MaoCheia:
                Mensagem("Tire a peça da mão para retirar esta.", Amarelo);
                break;
            case NotebookEncaixeResultado.Fechado:
                Mensagem("A tampa de baixo ainda está fechada.", Amarelo);
                break;
        }
        Atualizar();
    }

    private void ClicarUsb()
    {
        NotebookEstacao est = Estacao();
        if (est == null) return;

        ItemHolder mao = Mao();
        PickupInteractable naMao = mao != null ? mao.Current : null;

        NotebookUsbResultado r = est.UsarUsb(naMao, out GameObject devolver);
        switch (r)
        {
            case NotebookUsbResultado.Conectado:
                if (mao != null) mao.Stow(est.transform.position);
                InteractionSounds.Raise(InteractionSound.PendrivePlugged);
                Mensagem("Pendrive conectado. Ligue o notebook na tela da frente para instalar o sistema.", Verde);
                break;
            case NotebookUsbResultado.Devolvido:
                if (mao != null && devolver != null) mao.Retrieve(devolver);
                InteractionSounds.Raise(InteractionSound.PendriveUnplugged);
                Mensagem("Pendrive retirado.", TextoFraco);
                break;
            case NotebookUsbResultado.PrecisaPendrive:
                Mensagem("Ponha um pendrive na mão e clique na porta USB.", Amarelo);
                break;
            case NotebookUsbResultado.ItemErrado:
                Mensagem("Só um pendrive entra nessa porta.", Vermelho);
                break;
            case NotebookUsbResultado.MaoCheia:
                Mensagem("Tire o que está na mão para retirar o pendrive.", Amarelo);
                break;
        }
        Atualizar();
    }

    private void ClicarPower()
    {
        NotebookEstacao est = Estacao();
        if (est == null) return;

        NotebookBotaoResultado r = est.PressPower();
        InteractionSounds.Raise(InteractionSound.PowerButton);
        if (r == NotebookBotaoResultado.FaltamRequisitos)
            Mensagem("Não liga sem memória RAM instalada.", Vermelho);
        else if (r == NotebookBotaoResultado.Desligou)
            Mensagem("Notebook desligado.", TextoFraco);
        else if (est.Sessao.Instalando)
            Mensagem("Boot pelo pendrive: instalando o sistema...", Amarelo);
        else if (est.Sessao.TemSistema)
            Mensagem("Ligou no sistema instalado.", Verde);
        else
            Mensagem("Ligou na BIOS.", Amarelo);
        Atualizar();
    }

    private ToolType FerramentaNaMao()
    {
        ItemHolder mao = Mao();
        if (mao == null || mao.Current == null) return ToolType.None;
        ToolItem t = mao.Current.GetComponentInParent<ToolItem>();
        return t != null ? t.Type : ToolType.None;
    }

    // ================================================================== desenho

    private void Atualizar()
    {
        if (!construido) return;
        NotebookEstacao est = Estacao();

        for (int i = 0; i < 4; i++) vistas[i].gameObject.SetActive(i == (int)vista);

        if (est == null) return;

        if (vista == NotebookVista.Baixo)
        {
            for (int i = 0; i < parafusoImg.Length; i++)
            {
                bool removido = est.Sessao.ParafusoRemovido(i);
                Color cor = removido ? CorVazio : (parafusoFlash[i] > 0f ? Vermelho : CorMetal);
                parafusoImg[i].color = cor;
                // O furo vazio continua clicável: clicar nele recoloca o parafuso (com a chave de fenda na mão).
                parafusoImg[i].raycastTarget = true;
                parafusoFenda[i].gameObject.SetActive(!removido);
            }
        }
        else if (vista == NotebookVista.Aberto)
        {
            bool ram = est.Sessao.TemRam;
            bool ssd = est.Sessao.TemSsd;
            ramPeca.gameObject.SetActive(ram);
            ramMiolo.gameObject.SetActive(!ram);
            ssdPeca.gameObject.SetActive(ssd);
            ssdMiolo.gameObject.SetActive(!ssd);
        }
        else if (vista == NotebookVista.Lado)
        {
            bool usb = est.Sessao.PendriveConectado;
            pendriveImg.gameObject.SetActive(usb);
            usbMiolo.color = usb ? new Color(0.2f, 0.35f, 0.25f) : CorVazio;
        }
        else if (vista == NotebookVista.Frente) AtualizarFrente(est);
    }

    private void AtualizarFrente(NotebookEstacao est)
    {
        NotebookSession s = est.Sessao;
        bool ligado = s.Ligado;

        telaFundo.color = ligado ? new Color(0.08f, 0.1f, 0.14f) : CorTela;
        telaPower.color = ligado ? Verde : CorMetal;

        string texto;
        switch (s.Tela)
        {
            case NotebookEstadoTela.SemArmazenamento:
                texto = "BIOS\n\nNenhum disco detectado.\nInstale um SSD (vista \"Aberto\").";
                break;
            case NotebookEstadoTela.SemSistema:
                texto = "BIOS\n\nSSD detectado, sem sistema.\nConecte um pendrive na lateral e ligue de novo.";
                break;
            case NotebookEstadoTela.Instalando:
                texto = "INSTALANDO O SISTEMA...\n\nNão retire o pendrive nem o SSD.";
                break;
            case NotebookEstadoTela.Sistema:
                texto = "SISTEMA INSTALADO\n\nO notebook está pronto para usar.";
                break;
            default:
                texto = "Notebook desligado.\n\nClique no botão Power.";
                break;
        }
        telaTxt.text = texto;

        bool instalando = s.Instalando;
        ((RectTransform)barraFill.transform.parent).gameObject.SetActive(instalando);
        if (instalando)
        {
            barraFill.fillAmount = s.ProgressoInstalacao;
            etapaTxt.text = "Etapa " + (s.EtapaInstalacao + 1) + " de " + s.EtapasInstalacao;
        }
        else etapaTxt.text = "";
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
