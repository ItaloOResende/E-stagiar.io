using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Biblioteca de consulta exclusiva do tutorial. Nao altera o tablet nem seus prefabs.
/// Exibe um botao auxiliar enquanto o tablet estiver aberto e uma janela propria de leitura.
/// A biblioteca nao conclui chamados nem altera minigames.
/// </summary>
[DefaultExecutionOrder(9000)]
public sealed class TutorialKnowledgeBase : MonoBehaviour
{
    [Serializable]
    private sealed class Entry
    {
        public readonly string Name, ImageId, What, Where, How, Tip;
        public Entry(string name, string imageId, string what, string where, string how, string tip)
        { Name = name; ImageId = imageId; What = what; Where = where; How = how; Tip = tip; }
    }
    private sealed class Category
    {
        public readonly string Name;
        public readonly Entry[] Entries;
        public Category(string name, params Entry[] entries) { Name = name; Entries = entries; }
    }

    private static readonly Category[] Categories = {
        new Category("Pecas do computador",
            new Entry("Gabinete", "gabinete", "Caixa (chassi) que abriga e protege as pecas internas do computador: placa-mae, memoria RAM, HD, fonte e cooler. Na frente fica o botao Power; na traseira, as portas onde se ligam os cabos.", "Na mesa de cada estacao, ao lado do monitor. Gabinetes de reserva ficam no armario da sala de TI.", "Clique direito abre a manutencao, com as vistas Frente, Traseira e Interior. Na Traseira ficam a entrada de energia da fonte, as saidas de video VGA e HDMI, a porta de rede (RJ45) e as portas USB. Para chegar ao Interior, apoie o gabinete numa mesa ou bancada e retire os parafusos da tampa com a chave de fenda.", "Chamado entregue com o gabinete aberto e recusado: recoloque os parafusos antes de finalizar. Quedas fortes podem quebrar o gabinete."),
            new Entry("Placa-mae", "placa-mae", "Placa de circuito principal do computador. Todas as outras pecas se encaixam ou se ligam nela, e e por ela que se comunicam. No jogo, o processador ja faz parte da placa.", "Gaveteiro de hardware da sala de TI, na mesma gaveta da RAM e do cooler.", "Com o gabinete aberto, va ao Interior e, com a placa na mao, clique no encaixe da placa-mae. Ela e a base da montagem: a RAM e o cooler so podem ser instalados depois dela e precisam sair antes que ela seja retirada.", "Sem placa-mae o PC nao liga. E fragil: quebra na primeira queda, e peca quebrada nao pode ser instalada."),
            new Entry("Fonte de alimentacao", "fonte", "Tambem chamada de PSU. Recebe a energia da tomada e a converte nas tensoes que as pecas do computador usam.", "Gaveteiro de hardware da sala de TI, na mesma gaveta do HD.", "Instale pelo encaixe da fonte, no Interior do gabinete aberto. O cabo de energia que vem da tomada e ligado na entrada da fonte, na vista Traseira.", "Sem fonte o PC nao liga, mesmo com o cabo de energia conectado. Quebra na segunda queda."),
            new Entry("Memoria RAM", "ram", "Memoria de trabalho do computador: rapida e temporaria, guarda os programas que estao em uso. O conteudo dela se apaga quando o PC desliga; os arquivos ficam no HD.", "Gaveteiro de hardware da sala de TI, na mesma gaveta da placa-mae e do cooler.", "No Interior do gabinete, com o pente na mao, clique no slot de memoria da placa-mae. A placa-mae precisa estar instalada antes.", "Sem RAM o PC nao liga. Nao confunda com o pente do notebook, que e menor. E fragil: quebra na primeira queda."),
            new Entry("HD (armazenamento)", "ssd", "Disco rigido: armazenamento permanente do computador. Guarda o sistema operacional e os arquivos, que continuam la mesmo com o PC desligado.", "Gaveteiro de hardware da sala de TI, na mesma gaveta da fonte.", "Instale pelo encaixe do HD no Interior do gabinete aberto. Com o HD instalado, mas sem sistema, o PC para na BIOS avisando que nao ha dispositivo de inicializacao; nesse caso, instale o sistema (veja Pendrive bootavel).", "Sem HD o PC chega a BIOS, mas nao ao sistema. Quebra na segunda queda."),
            new Entry("Cooler", "cooler", "Cooler do processador: dissipador de metal com uma ventoinha que retira o calor do processador e evita o superaquecimento.", "Gaveteiro de hardware da sala de TI, na mesma gaveta da placa-mae e da RAM.", "No Interior do gabinete, clique no encaixe do cooler, que fica sobre o processador da placa-mae. A placa-mae precisa estar instalada antes.", "Sem cooler o PC nao liga (na vida real, o processador superaqueceria). Quebra na segunda queda."),
            new Entry("Monitor", "monitor", "Tela que exibe a imagem gerada pelo computador. O monitor so mostra a imagem: quem processa tudo e o gabinete.", "Na mesa de cada estacao. Monitores de reserva ficam no estoque da sala de TI.", "Na traseira do monitor ficam a entrada de energia e as entradas de video VGA e HDMI. Ligue o cabo de energia do monitor numa tomada e um cabo de video entre o monitor e a saida do mesmo tipo no gabinete. O monitor precisa estar perto do gabinete para aparecer na tela de manutencao.", "O PC so liga com o monitor tambem recebendo energia. Ligado sem cabo de video, o monitor mostra \"Sem sinal\". Quebra na primeira queda."),
            new Entry("Pendrive bootavel", "sistema", "Pendrive preparado para dar boot: o computador consegue iniciar por ele em vez de pelo HD. Este guarda o instalador do sistema operacional, o programa principal (como Windows ou Linux) que controla o hardware e permite usar os demais programas. O sistema instalado fica gravado no HD (no notebook, no SSD).", "Gaveteiros de hardware e de suprimentos da sala de TI.", "Com o PC montado e um HD sem sistema, encaixe o pendrive numa USB da traseira do gabinete e ligue o PC. A BIOS inicia pelo pendrive e a instalacao comeca sozinha, avancando enquanto voce olha para o monitor. No notebook, use a USB lateral.", "Ele so instala o sistema se houver um HD ou SSD sem sistema. Tirar o pendrive, o HD ou outra peca necessaria durante a instalacao a cancela.")),
        new Category("Cabos e conexoes",
            new Entry("Cabo de energia do PC", "energia", "Leva a eletricidade da tomada ate a fonte de alimentacao do computador.", "Gaveteiro de cabos da sala de TI, na gaveta de energia.", "Com o cabo na mao, na vista Traseira, clique na entrada de energia da fonte e depois numa das tomadas do ponto eletrico. O ponto eletrico precisa estar perto do gabinete.", "As duas tomadas aceitam qualquer cabo de energia: o que define a ligacao e a ponta no equipamento. Sem energia nas duas pontas, o PC nao liga."),
            new Entry("Cabo de energia do monitor", "energia-monitor", "Alimenta o monitor com a eletricidade da tomada. E o mesmo tipo de cabo usado no PC; muda so o equipamento onde a ponta e ligada.", "Gaveteiro de cabos da sala de TI, na gaveta de energia.", "Na vista Traseira, ligue uma ponta na entrada de energia do monitor e a outra na tomada livre do ponto eletrico.", "Energia e video sao ligacoes diferentes: este cabo faz o monitor ligar, mas nao leva imagem."),
            new Entry("Cabo HDMI", "hdmi", "Cabo de video digital: leva a imagem do computador ao monitor (na vida real, tambem leva som). O conector e achatado, com um lado mais largo que o outro.", "Gaveteiro de cabos da sala de TI, na gaveta de video (junto com o VGA).", "Na vista Traseira, ligue uma ponta na saida HDMI do gabinete e a outra na entrada HDMI do monitor.", "Basta um cabo de video: HDMI ou VGA. Cada ponta so entra em porta do mesmo tipo."),
            new Entry("Cabo VGA", "vga", "Cabo de video analogico, mais antigo que o HDMI. O conector tem formato de trapezio, 15 pinos e parafusos laterais.", "Gaveteiro de cabos da sala de TI, na gaveta de video (junto com o HDMI).", "Na vista Traseira, ligue uma ponta na saida VGA do gabinete e a outra na entrada VGA do monitor.", "Basta um cabo de video: VGA ou HDMI. Ele nao leva energia ao monitor."),
            new Entry("Cabo de rede RJ45", "rede", "Cabo de par trancado com conectores RJ45 nas pontas. Liga o computador a rede do escritorio e, por ela, a internet.", "Gaveteiro de cabos da sala de TI, na gaveta de rede.", "Na vista Traseira, ligue uma ponta na porta de rede (RJ45) do gabinete e a outra no ponto de rede da parede. O ponto da parede chega ate o rack da sala de TI.", "Sem rede o PC liga normalmente, mas fica desconectado. Um cabo pode parecer bom e ter defeito: na duvida, use o testador.")),
        new Category("Ferramentas",
            new Entry("Chave de fenda", "chave", "Ferramenta para apertar e soltar parafusos.", "Gaveteiro de hardware da sala de TI, na gaveta de ferramentas.", "Com a chave na mao, abra a manutencao do gabinete ou a vista de baixo do notebook: o cursor vira a chave e cada clique num parafuso o retira. Recolocar os parafusos fecha a tampa.", "Sem a chave aparece \"Falta ferramenta\". O gabinete so abre apoiado numa mesa ou bancada."),
            new Entry("Alicate de crimpar", "alicate", "Ferramenta que corta e descasca o cabo de rede e prensa o conector RJ45 na ponta, prendendo os contatos aos fios.", "Gaveteiro de hardware da sala de TI, na gaveta de ferramentas.", "Com o alicate na mao, clique direito na caixa de cabo de rede da sala de TI para abrir a crimpagem (veja Crimpagem RJ45, no app Rede e rack).", "Sem o alicate na mao, a crimpagem nao comeca (\"Falta ferramenta\").")),
        new Category("Impressora",
            new Entry("Impressora", "impressora", "Equipamento que passa documentos digitais para o papel. A do escritorio e de rede: os computadores a encontram pelo endereco IP.", "Area de impressoras do escritorio.", "Clique direito no corpo ou no painel abre a tela da impressora. O painel mostra o que falta (toner, papel ou IP) ou \"PRONTA\". Depois de corrigir, faca o teste de impressao.", "O teste de impressao so sai com toner, papel e IP corretos. Leia o painel antes de trocar qualquer item."),
            new Entry("Toner", "toner", "Cartucho com o po de tinta usado pelas impressoras a laser para formar o texto e as imagens no papel.", "Gaveteiro de suprimentos da sala de TI, na gaveta do toner.", "Com o toner na mao, abra a tela da impressora e clique no encaixe do toner.", "Nem toda falha de impressao e falta de toner: confira o painel primeiro."),
            new Entry("Resma de papel", "resma", "Pacote de folhas de papel para impressao (uma resma tem 500 folhas).", "Gaveteiro de suprimentos da sala de TI, na gaveta do papel.", "Com a resma na mao, abra a tela da impressora e clique na bandeja de papel.", "Toner e papel tem encaixes diferentes: cada item so entra no seu."),
            new Entry("Configuracao de rede (IP)", "impressora-rede", "O endereco IP identifica a impressora na rede: sao quatro numeros de 0 a 255 separados por pontos. Com o IP errado, os computadores nao a encontram.", "O IP que vale esta no cartaz da sala de TI.", "No painel da tela da impressora, digite so os numeros: os pontos entram sozinhos. Confirme e faca o teste de impressao.", "O IP muda a cada chamado que o apaga: consulte o cartaz sempre, em vez de repetir um IP antigo.")),
        new Category("Notebook",
            new Entry("Notebook", "notebook", "Computador portatil: tela, teclado e pecas internas reunidos num so aparelho.", "Nas mesas dos funcionarios que usam notebook.", "Clique direito abre a manutencao, com as vistas de baixo, interior, lateral e frente. A tampa de baixo sai com a chave de fenda e da acesso a memoria e ao SSD. A USB fica na lateral e o botao de energia, no teclado.", "As pecas do notebook sao diferentes das do PC de mesa. Recoloque a tampa de baixo antes de finalizar o chamado."),
            new Entry("RAM do notebook", "ram-notebook", "Memoria RAM no formato SO-DIMM: um pente menor que o do PC de mesa, feito para notebooks.", "Gaveteiro de suprimentos da sala de TI, na gaveta com o SSD e o pendrive.", "Com a tampa de baixo aberta, va ao interior e, com o pente na mao, clique no slot de memoria.", "Nao confunda com o pente do PC de mesa, que e maior."),
            new Entry("SSD do notebook", "ssd-notebook", "Armazenamento sem partes moveis, mais rapido que o HD. Este e um SSD M.2: uma placa fina encaixada direto na placa do notebook. Guarda o sistema e os arquivos.", "Gaveteiro de suprimentos da sala de TI, na gaveta com a RAM do notebook e o pendrive.", "Com a tampa de baixo aberta, va ao interior e, com o SSD na mao, clique no slot do SSD.", "Um SSD sem sistema precisa receber a instalacao depois (veja Pendrive bootavel)."),
            new Entry("Pendrive bootavel", "sistema-notebook", "Pendrive preparado para dar boot, com o instalador do sistema operacional. No notebook, ele grava o sistema no SSD; sem sistema, o notebook liga, mas nao passa da BIOS.", "Gaveteiros de hardware e de suprimentos da sala de TI.", "Com o SSD instalado, encaixe o pendrive na USB lateral do notebook e ligue pelo botao do teclado. A BIOS inicia pelo pendrive e a instalacao acontece como no PC.", "Se o notebook nao inicia, descubra a causa antes: pode faltar memoria, SSD ou apenas o sistema.")),
        new Category("Rede e rack",
            new Entry("Rack de rede", "rack", "Armario que concentra os equipamentos de rede. Tem o patch panel (portas P, ligadas aos pontos de rede das mesas) e o switch (portas S, que distribuem a rede).", "Sala de TI.", "Clique direito no rack abre a tela. Com um cabo de rede na mao, ligue uma porta do patch panel a uma porta do switch. A rede de uma mesa so funciona se a porta do patch panel dela estiver ligada ao switch com um cabo bom.", "Descubra qual porta do patch panel atende a mesa do chamado antes de mexer nas ligacoes."),
            new Entry("Crimpagem RJ45", "crimpagem", "Montagem de um cabo de rede: o conector RJ45 e prensado na ponta do cabo, com os 8 fios na ordem certa.", "Caixa de cabo de rede da sala de TI, perto do rack.", "Com o alicate de crimpar na mao, clique direito na caixa. A sequencia e: cortar o cabo, tirar a capa, organizar os 8 fios no padrao T568B, encaixar o RJ45 e crimpar. A aba CORES do tablet ajuda a conferir os fios.", "Fio fora de ordem gera um cabo com defeito, que nao passa sinal. O chamado de cabeamento so aceita um cabo feito na crimpagem."),
            new Entry("Testador de cabo", "testador", "Aparelho com duas unidades que verifica, fio a fio, se um cabo de rede conduz sinal de uma ponta a outra.", "Sala de TI, perto do rack.", "Com o cabo de rede na mao, clique direito no testador, encaixe uma ponta em cada unidade e toque em TESTAR. Os LEDs de 1 a 8 acendem verde quando o fio esta bom e vermelho quando falha.", "O teste e opcional, mas ajuda a encontrar o cabo com defeito e vale bonus. Ele nao liga nada na rede; um cabo reprovado pode ser descartado."))
    };

    private enum ScreenMode { Categories, Items, Detail }
    private ScreenMode screen = ScreenMode.Categories;
    private TicketTabletPrototype tablet;
    private bool opened;
    private int categoryIndex, entryIndex;
    private Vector2 scroll;
    private readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
    private GUIStyle titleStyle, subTitleStyle, bodyStyle, captionStyle, buttonStyle, selectedButtonStyle, sectionStyle;
    private Texture2D backgroundTexture, cardTexture, accentTexture;

    private void Awake() { tablet = GetComponent<TicketTabletPrototype>(); }
    private bool TabletOpen() { return tablet != null && tablet.IsOpen; }

    private void Update()
    {
        if (!TabletOpen()) { opened = false; screen = ScreenMode.Categories; return; }
        if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
        { opened = !opened; if (opened) screen = ScreenMode.Categories; }
    }

    private Rect TabletRect()
    {
        if (tablet == null) return new Rect(0, 0, 0, 0);
        var f = typeof(TicketTabletPrototype).GetField("painel", BindingFlags.Instance | BindingFlags.NonPublic);
        RectTransform panel = f == null ? null : f.GetValue(tablet) as RectTransform;
        if (panel != null)
        {
            Vector3[] p = new Vector3[4]; panel.GetWorldCorners(p);
            Canvas canvas = panel.GetComponentInParent<Canvas>();
            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, p[0]);
            Vector2 b = RectTransformUtility.WorldToScreenPoint(cam, p[2]);
            Rect r = new Rect(a.x, Screen.height - b.y, b.x - a.x, b.y - a.y);
            if (r.width > 150 && r.height > 150) return r;
        }
        var m = typeof(TicketTabletPrototype).GetMethod("RetanguloAberto", BindingFlags.Instance | BindingFlags.NonPublic);
        if (m != null)
        {
            object r = m.Invoke(tablet, null);
            if (r is Rect) return (Rect)r;
        }
        return new Rect(Screen.width - 430, Screen.height - 670, 410, 640);
    }

    private Texture2D Picture(string id)
    {
        Texture2D cached;
        if (textures.TryGetValue(id, out cached)) return cached;
        cached = Resources.Load<Texture2D>("TutorialIcons/" + id);
        textures[id] = cached;
        return cached;
    }

    private static Texture2D Solid(Color color)
    {
        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }

    private void EnsureStyle()
    {
        if (bodyStyle != null) return;
        backgroundTexture = Solid(new Color(.075f, .105f, .16f));
        cardTexture = Solid(new Color(.13f, .175f, .24f));
        accentTexture = Solid(new Color(.22f, .42f, .64f));
        bodyStyle = new GUIStyle(GUI.skin.label) {
            fontSize = 13, wordWrap = true, richText = false,
            normal = { textColor = new Color(.93f, .95f, .99f) },
            padding = new RectOffset(2, 2, 2, 2)
        };
        titleStyle = new GUIStyle(bodyStyle) { fontSize = 19, fontStyle = FontStyle.Bold };
        subTitleStyle = new GUIStyle(bodyStyle) { fontSize = 15, fontStyle = FontStyle.Bold };
        captionStyle = new GUIStyle(bodyStyle) { fontSize = 11, normal = { textColor = new Color(.72f, .80f, .89f) } };
        sectionStyle = new GUIStyle(subTitleStyle) { fontSize = 13, normal = { textColor = new Color(.54f, .79f, 1f) } };
        buttonStyle = new GUIStyle(GUI.skin.button) {
            fontSize = 13, wordWrap = true, alignment = TextAnchor.MiddleLeft,
            padding = new RectOffset(12, 8, 8, 8),
            margin = new RectOffset(1, 1, 3, 3),
            normal = { background = cardTexture, textColor = Color.white },
            hover = { background = accentTexture, textColor = Color.white },
            active = { background = accentTexture, textColor = Color.white }
        };
        selectedButtonStyle = new GUIStyle(buttonStyle) { fontStyle = FontStyle.Bold,
            normal = { background = accentTexture, textColor = Color.white } };
    }

    private void OnDestroy()
    {
        if (backgroundTexture != null) Destroy(backgroundTexture);
        if (cardTexture != null) Destroy(cardTexture);
        if (accentTexture != null) Destroy(accentTexture);
    }

    private void OnGUI()
    {
        if (!TabletOpen()) return;
        Rect body = TabletRect();
        if (body.width <= 150 || body.height <= 150) return;
        GUI.depth = -9000;
        float inset = Mathf.Max(10f, body.width * .035f);
        Rect inner = new Rect(body.x + inset, body.y + inset * 1.5f,
            body.width - 2f * inset, body.height - 2.6f * inset);
        Rect launcher = new Rect(inner.x + 8f, inner.y + 6f, Mathf.Min(124f, inner.width * .35f), 30f);
        if (!opened)
        {
            if (GUI.Button(launcher, "APPS / GUIA [F1]")) { opened = true; screen = ScreenMode.Categories; }
            return;
        }
        EnsureStyle();
        GUI.DrawTexture(inner, backgroundTexture);
        Rect area = new Rect(inner.x + 13f, inner.y + 11f, inner.width - 26f, inner.height - 22f);
        GUILayout.BeginArea(area);
        try
        {
            GUILayout.BeginHorizontal(GUILayout.Height(32));
            GUILayout.Label("GUIA DO ESTAGIARIO", subTitleStyle, GUILayout.ExpandWidth(true));
            if (GUILayout.Button("CHAMADOS", buttonStyle, GUILayout.Width(94), GUILayout.Height(30)))
                opened = false;
            GUILayout.EndHorizontal();
            DrawDivider();
            if (screen == ScreenMode.Categories) DrawCategories();
            else if (screen == ScreenMode.Items) DrawItems();
            else DrawDetail();
        }
        finally { GUILayout.EndArea(); }
    }

    private void DrawDivider()
    {
        GUILayout.Space(6);
        Rect rect = GUILayoutUtility.GetRect(1, 1, GUILayout.ExpandWidth(true));
        GUI.DrawTexture(rect, accentTexture);
        GUILayout.Space(8);
    }

    private void DrawCategories()
    {
        GUILayout.Label("Escolha um aplicativo", titleStyle);
        GUILayout.Label("Consulte as pecas, conexoes e tarefas antes de executar o chamado.", captionStyle);
        GUILayout.Space(10);
        scroll = GUILayout.BeginScrollView(scroll);
        for (int i = 0; i < Categories.Length; i++)
        {
            string text = Categories[i].Name + "     (" + Categories[i].Entries.Length + " topicos)   >";
            if (GUILayout.Button(text, buttonStyle, GUILayout.Height(48)))
            { categoryIndex = i; screen = ScreenMode.Items; scroll = Vector2.zero; }
        }
        GUILayout.EndScrollView();
    }

    private void DrawItems()
    {
        if (GUILayout.Button("<  Todos os aplicativos", buttonStyle, GUILayout.Height(30)))
        { screen = ScreenMode.Categories; scroll = Vector2.zero; return; }
        GUILayout.Space(8);
        GUILayout.Label(Categories[categoryIndex].Name, titleStyle);
        GUILayout.Label("Selecione um item para ver onde encaixar e como usar no jogo.", captionStyle);
        GUILayout.Space(8);
        scroll = GUILayout.BeginScrollView(scroll);
        Entry[] entries = Categories[categoryIndex].Entries;
        for (int i = 0; i < entries.Length; i++)
        {
            if (GUILayout.Button(entries[i].Name + "    >", buttonStyle, GUILayout.Height(44)))
            { entryIndex = i; screen = ScreenMode.Detail; scroll = Vector2.zero; }
        }
        GUILayout.EndScrollView();
    }

    private void DrawDetail()
    {
        if (GUILayout.Button("<  " + Categories[categoryIndex].Name, buttonStyle, GUILayout.Height(30)))
        { screen = ScreenMode.Items; scroll = Vector2.zero; return; }
        Entry entry = Categories[categoryIndex].Entries[entryIndex];
        GUILayout.Space(8);
        GUILayout.Label(entry.Name, titleStyle);
        scroll = GUILayout.BeginScrollView(scroll);
        Texture2D picture = Picture(entry.ImageId);
        if (picture != null)
        {
            GUILayout.Space(7);
            Rect r = GUILayoutUtility.GetRect(1f, 122f, GUILayout.ExpandWidth(true));
            float width = Mathf.Min(210, r.width);
            GUI.DrawTexture(new Rect(r.x + (r.width - width) * .5f, r.y, width, r.height), picture, ScaleMode.ScaleToFit);
        }
        else GUILayout.Label("[Imagem do item ainda nao encontrada nos assets]", captionStyle);
        DrawSection("O QUE E", entry.What);
        DrawSection("ONDE ENCONTRAR", entry.Where);
        DrawSection("ONDE ENCAIXAR / CONECTAR E COMO FAZER", entry.How);
        DrawSection("ATENCAO", entry.Tip);
        GUILayout.Space(10);
        GUILayout.Label("As interacoes disponiveis dependem do minigame e do chamado aberto.", captionStyle);
        GUILayout.EndScrollView();
    }

    private void DrawSection(string heading, string content)
    {
        GUILayout.Space(13);
        GUILayout.Label(heading, sectionStyle);
        GUILayout.Space(3);
        GUILayout.Label(content, bodyStyle);
    }
}
