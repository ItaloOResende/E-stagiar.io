using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif
using TMPro;

[ExecuteAlways, DisallowMultipleComponent]
public class RedeCrimpMinigame : MonoBehaviour
{
    [Header("Cena independente")]
    public bool abrirAoIniciar = true;
    public RedeRackMinigame rack;
    [Header("Sprites — campos vazios usam retângulos")]
    public Arte fundo = new Arte(new Color(.045f,.065f,.10f));
    public Arte painel = new Arte(new Color(.12f,.16f,.23f));
    public Arte botao = new Arte(new Color(.20f,.29f,.42f));
    public Arte caixa = new Arte(new Color(.51f,.35f,.21f));
    public Arte capaCabo = new Arte(new Color(.15f,.42f,.90f));
    public Arte marcaCorte = new Arte(new Color(1f,.82f,.25f));
    public Arte slotVazio = new Arte(new Color(.24f,.29f,.36f));
    public Arte conectorRj45 = new Arte(new Color(.7f,.78f,.85f));
    public Arte efeito = new Arte(Color.white);
    [Tooltip("Ordem: branco-laranja, laranja, branco-verde, azul, branco-azul, verde, branco-marrom, marrom.")]
    public Arte[] fios = {
        new Arte(new Color(.98f,.74f,.55f)), new Arte(new Color(.95f,.51f,.13f)),
        new Arte(new Color(.68f,.90f,.62f)), new Arte(new Color(.20f,.38f,.82f)),
        new Arte(new Color(.62f,.80f,.96f)), new Arte(new Color(.20f,.66f,.32f)),
        new Arte(new Color(.82f,.68f,.55f)), new Arte(new Color(.47f,.31f,.19f)) };
    public AudioClip somCorte, somDescascar, somEncaixe;

    [Serializable]
    public class Arte
    {
        public Sprite sprite;
        public Color corPlaceholder = Color.white;
        [Tooltip("Desative para esticar fundos, fios e painéis.")]
        public bool preservarProporcao;
        public Arte() { }
        public Arte(Color cor) { corPlaceholder = cor; }
    }
    [Header("Estilo dos textos — igual em TODOS os textos (só o tamanho é individual)")]
    [Tooltip("TMP Font Asset (SDF) usado em todos os textos. Vazio = fonte padrão do TMP Settings.")]
    [FormerlySerializedAs("fonteTextoTMP")] public TMP_FontAsset fonteTMP;
    public Color corTexto = Color.white;
    [Tooltip("Cor usada apenas nas mensagens de erro (feedback).")]
    public Color corTextoErro = new Color(1f,.5f,.45f);
    [Range(0f,6f)] [FormerlySerializedAs("outlineTexto")] public float espessuraOutlineTexto = 1f;
    public Color corOutlineTexto = Color.black;
    [Tooltip("Sombra feita com o Underlay do TextMeshPro (shader SDF).")]
    public bool usarSombraTexto = false;
    public Color corSombraTexto = new Color(0f,0f,0f,.6f);
    [Tooltip("Deslocamento da sombra: X positivo = direita, Y negativo = para baixo.")]
    [Range(-1f,1f)] public float sombraX = .4f;
    [Range(-1f,1f)] public float sombraY = -.4f;
    [Range(0f,1f)] public float sombraSuavidade = .3f;
    [Tooltip("Engrossa (+) ou afina (−) a sombra.")]
    [Range(-1f,1f)] public float sombraExpansao = 0f;
    [Header("Game juice — 0 desliga o efeito")]
    [Range(0f, 2f)] public float intensidadeGeral = 1f;
    [Range(0f, 2f)] public float bounce = 1f;
    [Range(0f, 2f)] public float tremor = 0.6f;
    [Range(0f, 2f)] public float zoom = 0.5f;
    [Range(0f, 2f)] public float brilho = 0.6f;
    [Range(0f, 2f)] public float pulsacao = 0.6f;
    [Range(0f, 2f)] public float particulas = 1f;
    [Range(0f, 1f)] public float volume = 0.7f;
    [Header("Sons opcionais")]
    public AudioClip somClique, somAcerto, somErro;
    [Header("Eventos")]
    public UnityEvent aoConcluir = new UnityEvent();


    [Header("Números das posições — edição centralizada")]
    [Tooltip("Texto das oito posições. Também atualiza os números correspondentes na tabela T568B. Não altera a ordem correta dos fios.")]
    public string[] textosNumeros = { "1", "2", "3", "4", "5", "6", "7", "8" };
    [Tooltip("Só o tamanho é próprio dos números; o resto vem do Estilo dos textos.")]
    [Range(8,80)] public int tamanhoNumeros = 21;
    [Header("Outline do conector RJ45")]
    public bool usarOutlineConector = true;
    [Range(0f,8f)] public float espessuraOutlineConector = 2f;
    public Color corOutlineConector = Color.black;
    [Tooltip("Trava e contatos são placeholders. Normalmente ficam ocultos quando você usa um sprite próprio do RJ45.")]
    public bool mostrarDetalhesConectorComSprite = false;

    [Header("Fundos por etapa — vazio usa Fundo original")]
    public Sprite fundoParte01, fundoParte02, fundoParte03;
    [Header("Alicate no cursor — parte 01")]
    public Sprite alicateAberto, alicateFechado;
    public Vector2 tamanhoAlicate = new Vector2(150,180);
    public Vector2 offsetAlicate = new Vector2(25,-45);
    [Range(30f,500f)] public float molaAlicate = 180f;
    [Range(.2f,2f)] public float amortecimentoAlicate = .65f;
    [Range(0f,2f)] public float giroAlicate = 1f;
    [Header("Transições")]
    public Sprite spriteWipe;
    [Range(0f,2f)] public float duracaoQuedaCaixa = .75f;
    [Range(0f,2f)] public float duracaoWipe = .35f;
    [Range(0f,2f)] public float duracaoSubidaCabo = .6f;
    [Range(0f,2f)] public float duracaoEntradaRJ45 = .5f;
    [Range(0f,2f)] public float bounceCaixa = 1f;
    [Header("Fios listrados — cores alternadas com branco")]
    public Color laranjaListra = new Color(.95f,.43f,.08f);
    public Color verdeListra = new Color(.12f,.65f,.22f);
    public Color azulListra = new Color(.13f,.36f,.87f);
    public Color marromListra = new Color(.43f,.24f,.10f);
    [Range(2,12)] public int paresDeListras = 5;

    [SerializeField,HideInInspector] private Image fundoEtapa, alicateImagem, wipeImagem;
    [SerializeField,HideInInspector] private RectTransform capaBase, camadaCurva;
    [SerializeField,HideInInspector] private Image[] bordaCurva;
    private readonly Dictionary<Image,List<Image>> faixas = new Dictionary<Image,List<Image>>();
    private Coroutine transicao;
    private bool animando, basesCapturadas, cursorSobControle, cursorAnterior;
    private Vector2 baseCorte, baseCabo, baseRJ, velocidadeAlicate;
    private float fechaAte, anguloAlicate, velocidadeAngular;

    public enum Etapa { Cortar, Descascar, Ordenar, Concluido }
    [Header("Scene View — fora do Play")]
    public bool mostrarPrevia = true;
    public Etapa etapaPrevia = Etapa.Ordenar;
    [Header("Outline preta das imagens")]
    [Range(0f,8f)] public float espessuraOutline = 2f;
    [Header("Texto — máquina de escrever por letra")]
    public bool animarTextos = true;
    [Range(5f,100f)] public float letrasPorSegundo = 40f;
    [Range(0f,2f)] public float bounceTexto = .7f;
    [Range(.05f,.6f)] public float duracaoBounceTexto = .22f;
    [Range(0f,2f)] public float movimentoEtapas = 1f;
    [Range(0f,2f)] public float balancoFio = .7f;
    public Arte contato = new Arte(new Color(.83f,.72f,.45f));

    [SerializeField, HideInInspector] private RectTransform raiz, palco, corteUI, capaUI, ordenarUI, tabela, caboVertical;
    [SerializeField, HideInInspector] private Canvas canvas;
    [SerializeField, HideInInspector] private Image flash, fioMao;
    [SerializeField, HideInInspector] private TMP_Text mensagemTMP, etapaTMP;
    [SerializeField, HideInInspector] private AudioSource audioUI;
    [SerializeField, HideInInspector] private Image[] imagensFios, pontas, slots, curva, encaixados;
    [SerializeField, HideInInspector] private RectTransform[] origens, destinos;
    [SerializeField, HideInInspector] private Button cortarBtn, descascarBtn, rj, rackBtn, novoBtn;
    [SerializeField, HideInInspector] private List<Visual> visuais = new List<Visual>();
    [SerializeField, HideInInspector] private List<Rotulo> rotulosTMP = new List<Rotulo>();
    [SerializeField, HideInInspector] private List<Button> botoes = new List<Button>();
    [Serializable] private class Visual { public Image imagem; public int arte; }
    [Serializable] private class Rotulo { public TMP_Text texto; public bool titulo; }
    private class Letreiro { public TMP_Text texto; public string original; public float inicio; public bool visivel, concluido; public Action<TMP_TextInfo> render; }
    private class Particula { public Image imagem; public float vida; public Vector2 velocidade; public Color cor; }
    private readonly Dictionary<TMP_Text,Letreiro> letreiros = new Dictionary<TMP_Text,Letreiro>();
    private readonly List<Particula> poeira = new List<Particula>();
    private readonly int[] fila = new int[8], soltos = new int[8];
    private readonly float[] pop = new float[8];
    private readonly Dictionary<Transform,Vector3> escalas = new Dictionary<Transform,Vector3>();
    private Etapa etapa;
    private int arrastando=-1, origem=-1;
    private bool origemSlot, pronto, erroEfeito, mensagemErro;
    private float idadeEfeito, impulso;
    private Vector2 posPalco;
    private Vector3 escalaPalco;
    private TMP_FontAsset fallback;
    private static readonly string[] Nomes = { "Branco-laranja", "Laranja", "Branco-verde", "Azul", "Branco-azul", "Verde", "Branco-marrom", "Marrom" };
    private const string NomeUI = "UI_RedeCrimpMinigame";

    private void OnEnable()
    {
        if (Application.IsPlaying(gameObject)) Iniciar();
#if UNITY_EDITOR
        else { pronto=false; LimparLetreiros(); escalas.Clear(); Agendar(); }
#endif
    }
    private void OnValidate()
    {
#if UNITY_EDITOR
        Agendar();
#endif
    }
#if UNITY_EDITOR
    private void Agendar() { EditorApplication.delayCall -= AtualizarEditor; EditorApplication.delayCall += AtualizarEditor; }
    private void AtualizarEditor()
    {
        if (this==null || !isActiveAndEnabled || EditorUtility.IsPersistent(this) || !gameObject.scene.IsValid()) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode && !Application.IsPlaying(gameObject)) return;
        if (Application.IsPlaying(gameObject)) { if(pronto) AplicarVisual(); return; }
        GerarInterface(); if(raiz==null)return; AplicarVisual(); Previa();
        EditorUtility.SetDirty(this); EditorSceneManager.MarkSceneDirty(gameObject.scene); SceneView.RepaintAll();
    }
#endif
    private void Iniciar()
    {
        if(!pronto)
        {
            GerarInterface(); if(raiz==null)return;
            CapturarBases();
            pronto=true; posPalco=palco.anchoredPosition; escalaPalco=palco.localScale;
            foreach(var b in botoes) if(b!=null)escalas[b.transform]=b.transform.localScale;
            foreach(var s in slots)escalas[s.transform]=s.transform.localScale;
            ConfigurarInput(); GarantirEventos(); AplicarVisual(); Reiniciar();
        }
        raiz.gameObject.SetActive(abrirAoIniciar);
        if(abrirAoIniciar)AnimarTodos();
    }
    [ContextMenu("Gerar interface editável")]
    public void GerarInterface()
    {
        if(raiz!=null){AtualizarEstrutura();return;}
        if(transform.Find(NomeUI)!=null){Debug.LogWarning("UI sem referência. Use Reconstruir interface.",this);return;}
        if(fios==null || fios.Length!=8){Debug.LogError("Fios precisa de exatamente 8 entradas.",this);return;}
#if UNITY_EDITOR
        if(!Application.IsPlaying(gameObject))Undo.RecordObject(this,"Gerar crimpagem");
#endif
        visuais.Clear();rotulosTMP.Clear();botoes.Clear();
        var go=new GameObject(NomeUI,typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        go.transform.SetParent(transform,false);raiz=go.GetComponent<RectTransform>();canvas=go.GetComponent<Canvas>();
        canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=50;
        var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution=new Vector2(1600,900);scaler.matchWidthOrHeight=.5f;
        audioUI=go.AddComponent<AudioSource>();audioUI.playOnAwake=false;
        var bg=Imagem(raiz,"Fundo",0,Vector2.zero,Vector2.zero);Esticar(bg.rectTransform);bg.raycastTarget=true;
        palco=Rect(raiz,"Palco",Vector2.zero,new Vector2(1600,900));
        etapaTMP=Texto(palco,"Etapa","3 / 3 — Ordenar e crimpar",new Vector2(0,390),new Vector2(650,55),25,true);
        mensagemTMP=Texto(palco,"Mensagem","Arraste cada fio para uma posição numerada acima.",new Vector2(0,-380),new Vector2(1100,55),22);
        corteUI=Rect(palco,"01_Cortar",Vector2.zero,new Vector2(1400,650));
        Imagem(corteUI,"Caixa",3,new Vector2(-150,-30),new Vector2(360,300));
        Imagem(corteUI,"Cabo",4,new Vector2(210,-30),new Vector2(380,55));
        cortarBtn=Botao(corteUI,"Corte","",5,new Vector2(240,-30),new Vector2(32,120));
        caboVertical=Rect(palco,"CaboVertical",Vector2.zero,new Vector2(500,700));
        Imagem(caboVertical,"CapaBase",4,new Vector2(0,-205),new Vector2(290,290));
        capaUI=Rect(caboVertical,"02_Descascar",Vector2.zero,new Vector2(400,500));
        descascarBtn=Botao(capaUI,"PuxarCapa","",4,new Vector2(0,30),new Vector2(290,180));
        Imagem(capaUI,"MarcaCapa",5,new Vector2(0,-55),new Vector2(290,12));
        ordenarUI=Rect(caboVertical,"03_Ordenar",Vector2.zero,new Vector2(500,700));
        rj=Botao(ordenarUI,"RJ45_Crimpar","",7,new Vector2(0,285),new Vector2(240,65));
        Imagem(rj.transform,"Trava",7,new Vector2(0,42),new Vector2(65,20));
        Texto(ordenarUI,"LegendaRJ45","RJ45",new Vector2(0,340),new Vector2(250,35),18);
        encaixados=new Image[8];imagensFios=new Image[8];pontas=new Image[8];slots=new Image[8];origens=new RectTransform[8];destinos=new RectTransform[8];
        for(int i=0;i<8;i++)
        {
            float x=(i-3.5f)*36;
            Imagem(rj.transform,"Contato_"+(i+1),6,new Vector2(x*.82f,-35),new Vector2(12,25));
            destinos[i]=Area(ordenarUI,"Destino_"+(i+1),new Vector2(x,150),new Vector2(35,85));
            Texto(destinos[i],"Numero",(i+1).ToString(),new Vector2(0,50),new Vector2(32,35),21);
            slots[i]=Imagem(destinos[i],"Slot",6,new Vector2(0,15),new Vector2(28,26));
            encaixados[i]=Imagem(destinos[i],"FioEncaixado",20+i,new Vector2(0,-100),new Vector2(17,220));
            encaixados[i].enabled=false;
            origens[i]=Area(ordenarUI,"Origem_"+(i+1),new Vector2(x,-15),new Vector2(34,100));
            imagensFios[i]=Imagem(origens[i],"Fio",20+i,Vector2.zero,new Vector2(17,85));
            pontas[i]=Imagem(origens[i],"Ponta",9,new Vector2(0,40),new Vector2(17,12));
        }
        tabela=Imagem(palco,"TabelaT568B",1,new Vector2(-590,200),new Vector2(340,400)).rectTransform;
        tabela.GetComponent<Image>().raycastTarget=true;
        Texto(tabela,"TituloTabela","T568B",new Vector2(0,165),new Vector2(300,36),23,true);
        for(int i=0;i<8;i++)
        {
            float y=120-i*40;
            Texto(tabela,"NumeroTabela_"+i,(i+1).ToString(),new Vector2(-140,y),new Vector2(25,30),20);
            Imagem(tabela,"Amostra_"+i,20+i,new Vector2(-105,y),new Vector2(28,22));
            Texto(tabela,"Nome_"+i,Nomes[i],new Vector2(35,y),new Vector2(220,32),19).alignment=TextAlignmentOptions.MidlineLeft;
        }
        rackBtn=Botao(palco,"IrAoRack","Ir ao rack",2,new Vector2(620,-415),new Vector2(230,48));
        novoBtn=Botao(palco,"NovoCabo","Novo cabo",2,new Vector2(-620,-415),new Vector2(230,48));
        curva=new Image[24];for(int i=0;i<curva.Length;i++)curva[i]=Imagem(palco,"FioArrastado_"+i,20,Vector2.zero,new Vector2(10,10));
        fioMao=Imagem(palco,"PontaNaMao",9,Vector2.zero,new Vector2(18,16));
        flash=Imagem(raiz,"Flash",8,Vector2.zero,Vector2.zero);Esticar(flash.rectTransform);flash.color=Color.clear;
        AtualizarEstrutura();OcultarArraste();if(!Application.IsPlaying(gameObject))Previa();
#if UNITY_EDITOR
        if(!Application.IsPlaying(gameObject)){Undo.RegisterCreatedObjectUndo(go,"Gerar crimpagem");EditorUtility.SetDirty(this);EditorSceneManager.MarkSceneDirty(gameObject.scene);}
#endif
    }
    [ContextMenu("Reconstruir interface (redefine layout)")]
    public void ReconstruirInterface()
    {
        if(Application.IsPlaying(gameObject)){Debug.LogWarning("Reconstrua fora do Play.",this);return;}
        var antiga=raiz!=null?raiz.gameObject:transform.Find(NomeUI)!=null?transform.Find(NomeUI).gameObject:null;
#if UNITY_EDITOR
        Undo.RecordObject(this,"Reconstruir crimpagem");if(antiga!=null)Undo.DestroyObjectImmediate(antiga);
#else
        if(antiga!=null)DestroyImmediate(antiga);
#endif
        raiz=null;GerarInterface();
    }
    private void Previa()
    {
        if(raiz==null)return;raiz.gameObject.SetActive(mostrarPrevia);MostrarEtapa(etapaPrevia);
        etapaTMP.text=NomeEtapa(etapaPrevia);mensagemErro=false;mensagemTMP.color=corTexto;mensagemTMP.text="Arraste cada fio para uma posição numerada acima.";
        int[] exemplo={3,5,4,1,6,0,7,2};
        for(int i=0;i<8;i++){Aplicar(imagensFios[i],ArteId(20+exemplo[i]));imagensFios[i].enabled=pontas[i].enabled=true;Aplicar(slots[i],slotVazio);encaixados[i].enabled=false;}
        SincronizarListras();OcultarArraste();flash.color=Color.clear;
        if(wipeImagem!=null)wipeImagem.gameObject.SetActive(false);
        if(alicateImagem!=null)alicateImagem.gameObject.SetActive(false);
    }
    private void GarantirEventos()
    {
        if(EventSystem.current!=null || FindObjectOfType<EventSystem>()!=null)return;
        var es=new GameObject("EventSystem",typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
        es.AddComponent<InputSystemUIInputModule>();
#else
        es.AddComponent<StandaloneInputModule>();
#endif
    }
    private void ConfigurarInput()
    {
        cortarBtn.onClick.RemoveAllListeners();Acao(descascarBtn,Descascar);Acao(rj,Crimpar);Acao(novoBtn,Reiniciar);
        Acao(rackBtn,()=>{if(rack!=null){Fechar();rack.Abrir();}else Feedback("Associe o rack no Inspector.",true);});
        for(int i=0;i<8;i++){int k=i;Arraste(origens[i],()=>Comecar(k,false));Arraste(destinos[i],()=>Comecar(k,true));}
    }
    private void Acao(Button b,UnityAction acao)
    {
        b.onClick.RemoveAllListeners();b.onClick.AddListener(()=>{Som(somClique);acao();});
    }
    private void Arraste(RectTransform rt,Action comecar)
    {
        var trigger=rt.GetComponent<EventTrigger>();if(trigger==null)trigger=rt.gameObject.AddComponent<EventTrigger>();
        trigger.triggers.Clear();
        Evento(trigger,EventTriggerType.BeginDrag,e=>{if(e.button==PointerEventData.InputButton.Left)comecar();});
        Evento(trigger,EventTriggerType.Drag,e=>{if(arrastando>=0)DesenharArraste(e.position);});
        Evento(trigger,EventTriggerType.EndDrag,e=>{if(e.button==PointerEventData.InputButton.Left)Terminar(e.position);});
    }
    private void Evento(EventTrigger trigger,EventTriggerType tipo,Action<PointerEventData> acao)
    {
        var entry=new EventTrigger.Entry{eventID=tipo};entry.callback.AddListener(data=>{var e=data as PointerEventData;if(e!=null)acao(e);});trigger.triggers.Add(entry);
    }
    public void Abrir()
    {
        if(!Application.IsPlaying(gameObject)){mostrarPrevia=true;GerarInterface();Previa();return;}
        if(!pronto)Iniciar();if(raiz==null)return;
        if(rack!=null)rack.Fechar();raiz.gameObject.SetActive(true);
        if(etapa==Etapa.Concluido)Reiniciar();AnimarTodos();
    }
    public void Fechar(){PararTransicoes();RestaurarCursor();if(pronto)Cancelar();if(raiz!=null)raiz.gameObject.SetActive(false);}
    public void Reiniciar()
    {
        if(!Application.IsPlaying(gameObject)){Previa();return;}
        if(!pronto){Iniciar();return;}
        PararTransicoes();fechaAte=0;Cancelar();etapa=Etapa.Cortar;Embaralhar();Atualizar();Feedback("Clique na marca amarela para cortar o cabo.");
        transicao=StartCoroutine(IntroCaixa());
    }
    private void Embaralhar()
    {
        for(int i=0;i<8;i++){fila[i]=-1;soltos[i]=i;}
        for(int i=7;i>0;i--){int j=UnityEngine.Random.Range(0,i+1);int tmp=soltos[i];soltos[i]=soltos[j];soltos[j]=tmp;}
    }
    private void Cortar()
    {
        if(etapa!=Etapa.Cortar || animando)return;
        Som(somCorte);fechaAte=Time.unscaledTime+.18f;velocidadeAngular-=180f*bounce;
        transicao=StartCoroutine(SairDoCorte());
    }
    private void Descascar()
    {
        if(etapa!=Etapa.Descascar || animando)return;
        Fragmento(capaCabo,Local(descascarBtn.transform.position),((RectTransform)descascarBtn.transform).sizeDelta,new Vector2(250,380));
        etapa=Etapa.Ordenar;Som(somDescascar);Atualizar();Feedback("Arraste cada fio para uma posição numerada acima.",false,true);
        transicao=StartCoroutine(IntroRJ());
    }
    private void Comecar(int i,bool encaixado)
    {
        if(etapa!=Etapa.Ordenar || arrastando>=0 || animando)return;
        int fio=encaixado?fila[i]:soltos[i];if(fio<0)return;
        arrastando=fio;origem=i;origemSlot=encaixado;
        // A origem só é alterada no drop válido. Cancelar nunca perde um fio.
        Atualizar();foreach(var seg in curva){Aplicar(seg,ArteId(20+fio));seg.gameObject.SetActive(true);}
        foreach(var b in bordaCurva)b.gameObject.SetActive(true);
        fioMao.gameObject.SetActive(true);Som(somClique);
        Feedback(Nomes[fio]+": solte em uma posição numerada.");DesenharArraste(Ponteiro());
    }
    private void Terminar(Vector2 tela)
    {
        if(arrastando<0)return;
        int destino=-1;
        for(int i=0;i<8;i++)if(RectTransformUtility.RectangleContainsScreenPoint(destinos[i],tela,CameraUI())){destino=i;break;}
        if(destino<0){Cancelar();Feedback("Fio devolvido à origem. Solte sobre uma posição numerada.");return;}
        int anterior=fila[destino];
        if(origemSlot)fila[origem]=anterior;else soltos[origem]=anterior;
        fila[destino]=arrastando;arrastando=-1;origem=-1;OcultarArraste();Atualizar();
        pop[destino]=.25f;Som(somEncaixe);Feedback("Fio encaixado. A ordem será conferida ao clicar no RJ45.");
    }
    private void Cancelar(){arrastando=-1;origem=-1;OcultarArraste();if(pronto && raiz!=null)Atualizar();}
    private void OcultarArraste()
    {
        if(fioMao!=null)fioMao.gameObject.SetActive(false);
        if(curva!=null)foreach(var s in curva)if(s!=null)s.gameObject.SetActive(false);
        if(bordaCurva!=null)foreach(var s in bordaCurva)if(s!=null)s.gameObject.SetActive(false);
    }
    private void Crimpar()
    {
        if(etapa!=Etapa.Ordenar || arrastando>=0 || animando)return;
        for(int i=0;i<8;i++)if(fila[i]<0){Feedback("Encaixe os 8 fios antes de clicar no RJ45.",true);return;}
        int erros=0;for(int i=0;i<8;i++)if(fila[i]!=i)erros++;
        if(erros>0)
        {
            etapa=Etapa.Descascar;Embaralhar();Atualizar();
            Feedback("Ordem errada ("+erros+" fios). Descasque outro cabo e tente novamente.",true,true);return;
        }
        etapa=Etapa.Concluido;Atualizar();if(rack!=null)rack.ReceberCabo();
        Feedback(rack!=null?"Cabo pronto! Disponível no rack.":"Cabo pronto!",false,true);aoConcluir.Invoke();
    }
    private string NomeEtapa(Etapa e)
    {
        return e==Etapa.Cortar?"1 / 3 — Cortar":e==Etapa.Descascar?"2 / 3 — Descascar":e==Etapa.Ordenar?"3 / 3 — Ordenar e crimpar":"CABO CONCLUÍDO";
    }
    private void MostrarEtapa(Etapa e)
    {
        corteUI.gameObject.SetActive(e==Etapa.Cortar);caboVertical.gameObject.SetActive(e!=Etapa.Cortar);
        capaUI.gameObject.SetActive(e==Etapa.Descascar);ordenarUI.gameObject.SetActive(e==Etapa.Ordenar || e==Etapa.Concluido);
        tabela.gameObject.SetActive(true);rj.interactable=e==Etapa.Ordenar;
        AtualizarFundo(e);OrdenarCamadas();
    }
    private void Atualizar()
    {
        MostrarEtapa(etapa);DefinirTexto(etapaTMP,NomeEtapa(etapa));
        for(int i=0;i<8;i++)
        {
            bool ocultarSolto=arrastando>=0 && !origemSlot && origem==i;
            bool ocultarSlot=arrastando>=0 && origemSlot && origem==i;
            imagensFios[i].enabled=pontas[i].enabled=soltos[i]>=0 && !ocultarSolto;
            origens[i].GetComponent<Image>().raycastTarget=soltos[i]>=0;
            if(soltos[i]>=0)Aplicar(imagensFios[i],ArteId(20+soltos[i]));
            Aplicar(slots[i],fila[i]>=0 && !ocultarSlot?ArteId(20+fila[i]):slotVazio);
            encaixados[i].enabled=fila[i]>=0 && !ocultarSlot;
            if(fila[i]>=0)Aplicar(encaixados[i],ArteId(20+fila[i]));
        }
        SincronizarListras();
    }
    private Camera CameraUI(){return canvas!=null && canvas.renderMode!=RenderMode.ScreenSpaceOverlay?canvas.worldCamera:null;}
    private Vector2 Local(Vector3 world){return palco.InverseTransformPoint(world);}
    private void DesenharArraste(Vector2 tela)
    {
        if(arrastando<0)return;
        Vector2 fim;RectTransformUtility.ScreenPointToLocalPointInRectangle(camadaCurva,tela,CameraUI(),out fim);
        var fonte=origemSlot?destinos[origem]:origens[origem];
        Vector2 inicio=camadaCurva.InverseTransformPoint(fonte.TransformPoint(new Vector3(0,-fonte.rect.height*.5f,0)));
        Vector2 c1=inicio+Vector2.up*80;
        Vector2 c2=fim+Vector2.down*65+Vector2.right*(Mathf.Sin(Time.unscaledTime*6)*12*balancoFio*intensidadeGeral);
        Vector2 anterior=inicio;
        for(int i=0;i<curva.Length;i++)
        {
            float t=(i+1f)/curva.Length,u=1-t;
            Vector2 ponto=u*u*u*inicio+3*u*u*t*c1+3*u*t*t*c2+t*t*t*fim;
            Vector2 d=ponto-anterior;var rt=curva[i].rectTransform;
            rt.anchoredPosition=(ponto+anterior)*.5f;rt.sizeDelta=new Vector2(d.magnitude+2,13);
            rt.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(d.y,d.x)*Mathf.Rad2Deg);
            var br=bordaCurva[i].rectTransform;br.anchoredPosition=rt.anchoredPosition;
            br.sizeDelta=new Vector2(d.magnitude+2+espessuraOutline*2,13+espessuraOutline*2);br.localRotation=rt.localRotation;
            bordaCurva[i].enabled=espessuraOutline>0;
            // Todas as bordas ficam atrás de TODOS os interiores: nenhuma emenda preta interna.
            curva[i].sprite=null;curva[i].color=CorDoFio(arrastando);
            if(Listrado(arrastando))curva[i].color=(i/2)%2==0?Color.white:CorListra(arrastando);
            anterior=ponto;
        }
        fioMao.rectTransform.anchoredPosition=fim;
    }
    private Vector2 Ponteiro()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current!=null?Mouse.current.position.ReadValue():new Vector2(-10000,-10000);
#else
        return Input.mousePosition;
#endif
    }
    private void Update()
    {
        if(!Application.IsPlaying(gameObject) || !pronto || raiz==null || !raiz.gameObject.activeInHierarchy)return;
        if(arrastando>=0)DesenharArraste(Ponteiro());Animar();AnimarAlicate();
    }
    private void LateUpdate(){if(Application.IsPlaying(gameObject) && pronto && raiz!=null && raiz.gameObject.activeInHierarchy)AnimarLetras();}
    private void OnApplicationFocus(bool foco){if(!foco && pronto)Cancelar();}
    private void OnDisable()
    {
#if UNITY_EDITOR
        EditorApplication.delayCall-=AtualizarEditor;
#endif
        RestaurarCursor();
        if(!Application.IsPlaying(gameObject))return;
        Fechar();if(palco!=null){palco.localScale=escalaPalco;palco.anchoredPosition=posPalco;}
        foreach(var p in poeira)if(p.imagem!=null)Destroy(p.imagem.gameObject);poeira.Clear();
    }
    private RectTransform Rect(Transform pai,string nome,Vector2 pos,Vector2 tamanho)
    {
        var rt=new GameObject(nome,typeof(RectTransform)).GetComponent<RectTransform>();rt.SetParent(pai,false);
        rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(.5f,.5f);rt.anchoredPosition=pos;rt.sizeDelta=tamanho;return rt;
    }
    private void Esticar(RectTransform rt){rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.offsetMin=rt.offsetMax=Vector2.zero;}
    private RectTransform Area(Transform pai,string nome,Vector2 pos,Vector2 tamanho)
    {
        var rt=Rect(pai,nome,pos,tamanho);var img=rt.gameObject.AddComponent<Image>();img.color=Color.clear;img.raycastTarget=true;return rt;
    }
    private Image Imagem(Transform pai,string nome,int arte,Vector2 pos,Vector2 tamanho)
    {
        var img=Rect(pai,nome,pos,tamanho).gameObject.AddComponent<Image>();img.raycastTarget=false;
        visuais.Add(new Visual{imagem=img,arte=arte});Aplicar(img,ArteId(arte));Contorno(img,false);return img;
    }
    private void Contorno(Graphic g,bool texto)
    {
        var tmp=g as TMP_Text;
        if(tmp!=null){EstilizarTexto(tmp);return;}
        var outline=g.GetComponent<Outline>();if(outline==null)outline=g.gameObject.AddComponent<Outline>();
        if(g.name.StartsWith("FioArrastado_")){outline.enabled=false;return;}
        if(!texto && rj!=null && (g.transform==rj.transform || g.transform.IsChildOf(rj.transform)))
        {
            ConfigurarOutlineConector(outline);return;
        }
        float e=texto?espessuraOutlineTexto:espessuraOutline;
        outline.effectColor=Color.black;outline.effectDistance=new Vector2(e,-e);outline.useGraphicAlpha=true;outline.enabled=e>0;
    }
    private Arte ArteId(int id)
    {
        if(id>=20)return fios!=null && id-20<fios.Length?fios[id-20]:null;
        switch(id)
        {
            case 0:return fundo;case 1:return painel;case 2:return botao;case 3:return caixa;
            case 4:return capaCabo;case 5:return marcaCorte;case 6:return slotVazio;
            case 7:return conectorRj45;case 8:return efeito;case 9:return contato;default:return null;
        }
    }
    private void Aplicar(Image img,Arte arte)
    {
        if(img==null || arte==null)return;img.sprite=arte.sprite;img.color=arte.sprite!=null?Color.white:arte.corPlaceholder;img.preserveAspect=arte.preservarProporcao;
        int fio=IndiceArte(arte);
        // A curva tem sua própria silhueta e bandas; não recebe listras-filhas.
        bool curvaImg=img.name.StartsWith("FioArrastado_");
        AplicarListras(img, Listrado(fio) && !curvaImg, fio);
        if(curvaImg){var o=img.GetComponent<Outline>();if(o!=null)o.enabled=false;}
    }
    private TMP_FontAsset Fonte()
    {
        if(fonteTMP!=null)return fonteTMP;
        if(fallback==null)fallback=TMP_Settings.defaultFontAsset;return fallback;
    }
    private TMP_Text Texto(Transform pai,string nome,string valor,Vector2 pos,Vector2 tamanho,int size,bool titulo=false)
    {
        var t=Rect(pai,nome,pos,tamanho).gameObject.AddComponent<TextMeshProUGUI>();t.font=Fonte();t.fontSize=size;
        t.text=valor;t.alignment=TextAlignmentOptions.Center;t.color=Color.white;t.raycastTarget=false;t.richText=true;
        t.textWrappingMode=TextWrappingModes.Normal;t.overflowMode=TextOverflowModes.Overflow;t.enableAutoSizing=false;
        Contorno(t,true);rotulosTMP.Add(new Rotulo{texto=t,titulo=titulo});return t;
    }
    private Button Botao(Transform pai,string nome,string rotulo,int arte,Vector2 pos,Vector2 tamanho)
    {
        var img=Imagem(pai,nome,arte,pos,tamanho);img.raycastTarget=true;
        var b=img.gameObject.AddComponent<Button>();b.targetGraphic=img;
        var nav=b.navigation;nav.mode=Navigation.Mode.None;b.navigation=nav;
        if(!string.IsNullOrEmpty(rotulo))Texto(img.transform,"Rotulo",rotulo,Vector2.zero,tamanho-new Vector2(18,8),21);
        botoes.Add(b);return b;
    }
    [ContextMenu("Aplicar sprites, fontes e outline")]
    public void AplicarVisual()
    {
        if(raiz==null)return;
        foreach(var v in visuais)if(v.imagem!=null){Aplicar(v.imagem,ArteId(v.arte));Contorno(v.imagem,false);}
        foreach(var r in rotulosTMP)if(r.texto!=null)EstilizarTexto(r.texto);
        AplicarNumeros();AplicarConector();
        flash.color=Color.clear;AtualizarFerramenta();
        if(Application.IsPlaying(gameObject) && pronto)
        {
            Atualizar();if(arrastando>=0)foreach(var s in curva)Aplicar(s,ArteId(20+arrastando));
        }
        else Previa();
    }
    // TextMeshPro: layout estável; o bounce transforma os vértices, nunca o fontSize.
    private void DefinirTexto(TMP_Text t,string valor,bool forcar=false)
    {
        if(t==null)return;
        if(!Application.IsPlaying(gameObject)){t.text=valor;t.maxVisibleCharacters=int.MaxValue;return;}
        Letreiro l;
        if(letreiros.TryGetValue(t,out l) && l.original==valor && !forcar)return;
        if(l==null)
        {
            l=new Letreiro{texto=t};letreiros[t]=l;
            Letreiro capturado=l;l.render=info=>AnimarVertices(capturado,info);
            t.OnPreRenderText+=l.render;
        }
        l.original=valor;l.inicio=Time.unscaledTime;l.visivel=t.gameObject.activeInHierarchy;l.concluido=false;
        t.text=valor;t.maxVisibleCharacters=animarTextos && intensidadeGeral>0?0:int.MaxValue;
    }
    private void LimparLetreiros()
    {
        foreach(var l in letreiros.Values)if(l.texto!=null && l.render!=null)l.texto.OnPreRenderText-=l.render;
        letreiros.Clear();
    }
    private void AnimarTodos()
    {
        foreach(var r in rotulosTMP)
        {
            if(r.texto==null)continue;Letreiro l;
            string original=letreiros.TryGetValue(r.texto,out l)?l.original:r.texto.text;DefinirTexto(r.texto,original,true);
        }
    }
    private void AnimarVertices(Letreiro l,TMP_TextInfo info)
    {
        if(!animarTextos || intensidadeGeral<=0)return;
        float tempo=Time.unscaledTime-l.inicio;
        for(int i=0;i<info.characterCount;i++)
        {
            var ch=info.characterInfo[i];if(!ch.isVisible || i>=l.texto.maxVisibleCharacters)continue;
            float idade=tempo-i/Mathf.Max(1f,letrasPorSegundo);
            if(idade<0 || idade>=duracaoBounceTexto)continue;
            float t=Mathf.Clamp01(idade/Mathf.Max(.01f,duracaoBounceTexto));
            float onda=Mathf.Sin(t*Mathf.PI*2f)*(1f-t)*bounceTexto*intensidadeGeral;
            Vector3[] vertices=info.meshInfo[ch.materialReferenceIndex].vertices;int v=ch.vertexIndex;
            Vector3 centro=(vertices[v]+vertices[v+2])*.5f;
            for(int j=0;j<4;j++)vertices[v+j]=centro+(vertices[v+j]-centro)*(1f+onda*.3f)+Vector3.up*(onda*l.texto.fontSize*.2f);
        }
    }
    private void AnimarLetras()
    {
        foreach(var l in letreiros.Values)
        {
            if(l.texto==null)continue;
            bool ativa=l.texto.gameObject.activeInHierarchy;
            if(ativa && !l.visivel){l.inicio=Time.unscaledTime;l.concluido=false;}
            l.visivel=ativa;if(!ativa)continue;
            if(!animarTextos || intensidadeGeral<=0)
            {
                if(!l.concluido || l.texto.maxVisibleCharacters!=int.MaxValue)
                {l.texto.maxVisibleCharacters=int.MaxValue;l.texto.ForceMeshUpdate();l.concluido=true;}
                continue;
            }
            if(l.concluido)continue;
            float tempo=Time.unscaledTime-l.inicio;
            l.texto.maxVisibleCharacters=Mathf.Max(0,Mathf.FloorToInt(tempo*Mathf.Max(1f,letrasPorSegundo))+1);
            // Regenera a geometria-base antes de OnPreRenderText: nenhuma deformação acumulada.
            l.texto.ForceMeshUpdate();
            int total=l.texto.textInfo.characterCount;
            if(tempo>=total/Mathf.Max(1f,letrasPorSegundo)+duracaoBounceTexto)
            {l.texto.maxVisibleCharacters=int.MaxValue;l.texto.ForceMeshUpdate();l.concluido=true;}
        }
    }
    private void Som(AudioClip clip){if(clip!=null && audioUI!=null)audioUI.PlayOneShot(clip,volume);}
    private void Feedback(string texto,bool erro=false,bool forte=false)
    {
        DefinirTexto(mensagemTMP,texto);mensagemErro=erro;mensagemTMP.color=erro?corTextoErro:corTexto;
        impulso=forte?1:.4f;idadeEfeito=0;erroEfeito=erro;
        if(erro)Som(somErro);else if(forte)Som(somAcerto);
        if(forte && !erro)
            for(int i=0;i<Mathf.RoundToInt(16*particulas*intensidadeGeral);i++)
                NovaParticula(efeito,Vector2.zero,new Vector2(8,8),UnityEngine.Random.insideUnitCircle*450,new Color(.4f,1,.8f));
    }
    private void NovaParticula(Arte arte,Vector2 pos,Vector2 size,Vector2 velocidade,Color cor)
    {
        var img=Rect(palco,"FeedbackTemporario",pos,size).gameObject.AddComponent<Image>();img.raycastTarget=false;
        Aplicar(img,arte);img.color=cor;Contorno(img,false);poeira.Add(new Particula{imagem=img,vida=.65f,velocidade=velocidade,cor=cor});
    }
    private void Fragmento(Arte arte,Vector2 pos,Vector2 size,Vector2 velocidade)
    {
        if(intensidadeGeral<=0 || movimentoEtapas<=0)return;
        NovaParticula(arte,pos,size,velocidade*movimentoEtapas*intensidadeGeral,arte.sprite!=null?Color.white:arte.corPlaceholder);
    }
    private void Animar()
    {
        float dt=Time.unscaledDeltaTime;idadeEfeito+=dt;
        float decay=Mathf.Max(0,1-idadeEfeito/.38f)*impulso*intensidadeGeral;
        palco.anchoredPosition=posPalco+(erroEfeito?new Vector2(Mathf.Sin(idadeEfeito*95)*12*tremor*decay,0):Vector2.zero);
        palco.localScale=escalaPalco*(1+Mathf.Sin(Mathf.Min(1,idadeEfeito/.38f)*Mathf.PI)*.035f*zoom*decay);
        flash.color=new Color(erroEfeito?1:.3f,erroEfeito ? .2f : 1f,.6f,decay*.13f*brilho);
        foreach(var b in botoes)
        {
            if(b==null || !b.gameObject.activeInHierarchy)continue;
            bool hover=b.interactable && RectTransformUtility.RectangleContainsScreenPoint((RectTransform)b.transform,Ponteiro(),CameraUI());
            Vector3 escalaBase; if(!escalas.TryGetValue(b.transform,out escalaBase))escalaBase=Vector3.one;
            float alvo=hover?1+.04f*bounce*intensidadeGeral:1;
            b.transform.localScale=Vector3.Lerp(b.transform.localScale,escalaBase*alvo,1-Mathf.Exp(-20*dt));
        }
        for(int i=0;i<8;i++)
        {
            pop[i]=Mathf.Max(0,pop[i]-dt);Vector3 escalaBase;
            if(!escalas.TryGetValue(slots[i].transform,out escalaBase))escalaBase=Vector3.one;
            slots[i].transform.localScale=escalaBase*(1+Mathf.Sin(pop[i]/.25f*Mathf.PI)*.18f*bounce*intensidadeGeral);
        }
        Image alvoImg=etapa==Etapa.Cortar?cortarBtn.image:etapa==Etapa.Descascar?descascarBtn.image:null;
        if(alvoImg!=null)
        {
            Aplicar(alvoImg,etapa==Etapa.Cortar?marcaCorte:capaCabo);
            float p=(.5f+.5f*Mathf.Sin(Time.unscaledTime*5))*pulsacao*intensidadeGeral;
            alvoImg.color=Color.Lerp(alvoImg.color,new Color(1,.85f,.35f),p*.3f);
        }
        for(int i=poeira.Count-1;i>=0;i--)
        {
            var p=poeira[i];p.vida-=dt;
            if(p.vida<=0 || p.imagem==null){if(p.imagem!=null)Destroy(p.imagem.gameObject);poeira.RemoveAt(i);continue;}
            p.velocidade+=Vector2.down*600*dt;p.imagem.rectTransform.anchoredPosition+=p.velocidade*dt;
            Color cor=p.cor;cor.a*=p.vida/.65f;p.imagem.color=cor;
        }
    }
    // Migração incremental: preserva os RectTransforms já editados na cena.
    private void AtualizarEstrutura()
    {
        if(raiz==null || palco==null)return;
        MigrarTextosTMP();
        if(fundoEtapa==null)
        {
            fundoEtapa=Rect(raiz,"FundoPorEtapa",Vector2.zero,Vector2.zero).gameObject.AddComponent<Image>();
            Esticar(fundoEtapa.rectTransform);fundoEtapa.raycastTarget=false;fundoEtapa.transform.SetSiblingIndex(1);
        }
        if(capaBase==null)capaBase=caboVertical.Find("CapaBase") as RectTransform;
        if(camadaCurva==null)camadaCurva=Rect(caboVertical,"CurvaCompleta",Vector2.zero,new Vector2(1600,900));
        if(bordaCurva==null || bordaCurva.Length!=curva.Length)bordaCurva=new Image[curva.Length];
        for(int i=0;i<curva.Length;i++)
        {
            if(bordaCurva[i]==null)
            {
                bordaCurva[i]=Rect(camadaCurva,"Silhueta_"+i,Vector2.zero,Vector2.one).gameObject.AddComponent<Image>();
                bordaCurva[i].color=Color.black;bordaCurva[i].raycastTarget=false;
            }
            bordaCurva[i].transform.SetSiblingIndex(i);
            curva[i].transform.SetParent(camadaCurva,false);
            var o=curva[i].GetComponent<Outline>();if(o!=null)o.enabled=false;
        }
        foreach(var c in curva)c.transform.SetAsLastSibling();
        fioMao.transform.SetParent(camadaCurva,false);fioMao.transform.SetAsLastSibling();
        if(alicateImagem==null)
        {
            alicateImagem=Rect(raiz,"AlicateCursor",Vector2.zero,tamanhoAlicate).gameObject.AddComponent<Image>();
            alicateImagem.raycastTarget=false;Contorno(alicateImagem,false);
        }
        if(wipeImagem==null)
        {
            wipeImagem=Rect(raiz,"Wipe",Vector2.zero,new Vector2(1800,1100)).gameObject.AddComponent<Image>();
            wipeImagem.raycastTarget=false;wipeImagem.gameObject.SetActive(false);
        }
        AtualizarFerramenta();OrdenarCamadas();AplicarNumeros();AplicarConector();
        if(!Application.IsPlaying(gameObject)){alicateImagem.gameObject.SetActive(false);OcultarArraste();}
    }
    private void AplicarNumeros()
    {
        for(int i=0;i<8;i++)
        {
            string valor=textosNumeros!=null && i<textosNumeros.Length ? textosNumeros[i]??"" : (i+1).ToString();
            if(destinos!=null && i<destinos.Length && destinos[i]!=null)
            {
                Transform numero=destinos[i].Find("Numero");
                if(numero!=null)ConfigurarNumero(numero.GetComponentInChildren<TMP_Text>(true),valor);
            }
            if(tabela!=null)
            {
                Transform numero=tabela.Find("NumeroTabela_"+i);
                if(numero!=null)ConfigurarNumero(numero.GetComponentInChildren<TMP_Text>(true),valor);
            }
        }
    }
    private void ConfigurarNumero(TMP_Text texto,string valor)
    {
        if(texto==null)return;
        EstilizarTexto(texto);
        texto.fontSize=tamanhoNumeros;
        DefinirTexto(texto,valor);
    }
    private void ConfigurarOutlineConector(Outline o)
    {
        o.enabled=usarOutlineConector && espessuraOutlineConector>0;
        o.effectColor=corOutlineConector;
        o.effectDistance=new Vector2(espessuraOutlineConector,-espessuraOutlineConector);
        o.useGraphicAlpha=true;
    }
    private void AplicarConector()
    {
        if(rj==null)return;
        bool detalhes=conectorRj45==null || conectorRj45.sprite==null || mostrarDetalhesConectorComSprite;
        for(int i=0;i<rj.transform.childCount;i++)
        {
            Transform filho=rj.transform.GetChild(i);
            if(filho.name=="Trava" || filho.name.StartsWith("Contato_"))filho.gameObject.SetActive(detalhes);
        }
        foreach(var img in rj.GetComponentsInChildren<Image>(true))
        {
            var o=img.GetComponent<Outline>();if(o==null)o=img.gameObject.AddComponent<Outline>();
            ConfigurarOutlineConector(o);
        }
    }
    private void OrdenarCamadas()
    {
        if(capaBase!=null)capaBase.SetAsLastSibling();
        if(alicateImagem!=null)alicateImagem.transform.SetAsLastSibling();
        if(wipeImagem!=null)wipeImagem.transform.SetAsLastSibling();
        GarantirHUD();
    }
    private void AtualizarFundo(Etapa e)
    {
        if(fundoEtapa==null)return;
        Sprite sprite=e==Etapa.Cortar?fundoParte01:e==Etapa.Descascar?fundoParte02:fundoParte03;
        fundoEtapa.sprite=sprite;fundoEtapa.color=Color.white;fundoEtapa.enabled=sprite!=null;
    }
    private void AtualizarFerramenta()
    {
        if(alicateImagem!=null)
        {
            alicateImagem.rectTransform.sizeDelta=tamanhoAlicate;
            alicateImagem.sprite=alicateAberto;alicateImagem.color=alicateAberto!=null?Color.white:new Color(.7f,.73f,.78f);
            alicateImagem.preserveAspect=true;Contorno(alicateImagem,false);
        }
        if(wipeImagem!=null){wipeImagem.sprite=spriteWipe;wipeImagem.color=spriteWipe!=null?Color.white:new Color(.12f,.16f,.23f);}
    }
    private int IndiceArte(Arte arte)
    {
        if(fios==null)return -1;for(int i=0;i<fios.Length;i++)if(ReferenceEquals(fios[i],arte))return i;return -1;
    }
    private bool Listrado(int i){return i==0 || i==2 || i==4 || i==6;}
    private Color CorListra(int i){return i==0?laranjaListra:i==2?verdeListra:i==4?azulListra:marromListra;}
    private Color CorDoFio(int i){return Listrado(i)?CorListra(i):fios[i].corPlaceholder;}
    private void AplicarListras(Image img,bool ativo,int fio)
    {
        List<Image> lista;
        if(!faixas.TryGetValue(img,out lista))
        {
            lista=new List<Image>();
            for(int i=0;i<img.transform.childCount;i++)
            {
                var child=img.transform.GetChild(i);if(child.name.StartsWith("Listra_"))lista.Add(child.GetComponent<Image>());
            }
            faixas[img]=lista;
        }
        int n=Mathf.Clamp(paresDeListras,2,12);
        // Sprites fornecidos pelo artista continuam sendo usados; listras são o padrão sem sprite.
        bool gerar=ativo && fios[fio].sprite==null;
        while(gerar && lista.Count<n)
        {
            var banda=Rect(img.transform,"Listra_"+lista.Count,Vector2.zero,Vector2.zero).gameObject.AddComponent<Image>();
            banda.color=Color.white;banda.raycastTarget=false;lista.Add(banda);
        }
        if(gerar){img.sprite=null;img.color=CorListra(fio);}
        for(int i=0;i<lista.Count;i++)
        {
            var banda=lista[i];if(banda==null)continue;
            // enabled segue o pai: Image.enabled=false não esconde seus filhos.
            banda.gameObject.SetActive(gerar && i<n && img.enabled);
            if(!gerar || i>=n)continue;
            var rt=banda.rectTransform;rt.anchorMin=new Vector2(0,i/(float)n);rt.anchorMax=new Vector2(1,(i+.5f)/n);
            rt.offsetMin=rt.offsetMax=Vector2.zero;
        }
    }
    private void SincronizarListras()
    {
        foreach(var pair in faixas)
        {
            if(pair.Key==null)continue;
            if(!pair.Key.enabled)foreach(var faixa in pair.Value)if(faixa!=null)faixa.gameObject.SetActive(false);
        }
    }
    private void CapturarBases()
    {
        baseCorte=corteUI.anchoredPosition;baseCabo=caboVertical.anchoredPosition;
        baseRJ=((RectTransform)rj.transform).anchoredPosition;basesCapturadas=true;
    }
    private void PararTransicoes()
    {
        if(transicao!=null){StopCoroutine(transicao);transicao=null;}animando=false;
        if(wipeImagem!=null)wipeImagem.gameObject.SetActive(false);
        if(basesCapturadas)
        {
            corteUI.anchoredPosition=baseCorte;caboVertical.anchoredPosition=baseCabo;
            ((RectTransform)rj.transform).anchoredPosition=baseRJ;
        }
    }
    private float Duracao(float valor){return intensidadeGeral<=0?0:valor;}
    private IEnumerator Mover(RectTransform rt,Vector2 inicio,Vector2 fim,float duracao,bool quicar)
    {
        rt.anchoredPosition=inicio;
        if(duracao>0)
        {
            for(float t=0;t<duracao;t+=Time.unscaledDeltaTime)
            {
                float u=Mathf.Clamp01(t/duracao);
                float f=quicar?Mathf.Lerp(u*u*(3-2*u),BounceOut(u),Mathf.Clamp01(bounceCaixa*bounce*intensidadeGeral)):u*u*(3-2*u);
                rt.anchoredPosition=Vector2.LerpUnclamped(inicio,fim,f);yield return null;
            }
        }
        rt.anchoredPosition=fim;
    }
    private float BounceOut(float x)
    {
        const float n=7.5625f,d=2.75f;
        if(x<1/d)return n*x*x;
        if(x<2/d){x-=1.5f/d;return n*x*x+.75f;}
        if(x<2.5f/d){x-=2.25f/d;return n*x*x+.9375f;}
        x-=2.625f/d;return n*x*x+.984375f;
    }
    private IEnumerator IntroCaixa()
    {
        animando=true;
        yield return Mover(corteUI,baseCorte+Vector2.up*1000,baseCorte,Duracao(duracaoQuedaCaixa),true);
        animando=false;transicao=null;
    }
    private IEnumerator SairDoCorte()
    {
        animando=true;
        // Dá tempo para enxergar o alicate fechado antes da passagem do wipe.
        float espera=Duracao(.16f);for(float t=0;t<espera;t+=Time.unscaledDeltaTime)yield return null;
        var wr=wipeImagem.rectTransform;
        float largura=raiz.rect.width+160,altura=raiz.rect.height+160;
        wr.sizeDelta=new Vector2(largura,altura);wipeImagem.gameObject.SetActive(true);
        yield return Mover(wr,new Vector2(-largura,0),Vector2.zero,Duracao(duracaoWipe),false);
        etapa=Etapa.Descascar;Atualizar();RestaurarCursor();alicateImagem.gameObject.SetActive(false);
        caboVertical.anchoredPosition=baseCabo+Vector2.down*1000;
        Feedback("Clique na capa azul para descascar.",false,true);
        // O cabo entra enquanto a transição deixa a tela; sem bounce.
        float dw=Duracao(duracaoWipe),dc=Duracao(duracaoSubidaCabo),total=Mathf.Max(dw,dc);
        for(float t=0;t<total;t+=Time.unscaledDeltaTime)
        {
            float w=dw<=0?1:Mathf.Clamp01(t/dw),c=dc<=0?1:Mathf.Clamp01(t/dc);
            wr.anchoredPosition=Vector2.right*(largura*w*w*(3-2*w));
            caboVertical.anchoredPosition=Vector2.Lerp(baseCabo+Vector2.down*1000,baseCabo,c*c*(3-2*c));yield return null;
        }
        caboVertical.anchoredPosition=baseCabo;wipeImagem.gameObject.SetActive(false);
        animando=false;transicao=null;
    }
    private IEnumerator IntroRJ()
    {
        animando=true;
        // O cabo não se move na passagem 02 -> 03; apenas o conector entra.
        yield return Mover((RectTransform)rj.transform,baseRJ+Vector2.up*800,baseRJ,Duracao(duracaoEntradaRJ45),false);
        animando=false;transicao=null;
    }
    private bool CliquePressionado()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current!=null && Mouse.current.leftButton.wasPressedThisFrame;
#else
        return Input.GetMouseButtonDown(0);
#endif
    }
    private void RestaurarCursor()
    {
        if(!cursorSobControle)return;Cursor.visible=cursorAnterior;cursorSobControle=false;
    }
    private void AnimarAlicate()
    {
        bool mostrar=etapa==Etapa.Cortar && Application.isFocused;
        alicateImagem.gameObject.SetActive(mostrar);
        if(!mostrar){RestaurarCursor();return;}
        Vector2 alvo;RectTransformUtility.ScreenPointToLocalPointInRectangle(raiz,Ponteiro(),CameraUI(),out alvo);alvo+=offsetAlicate;
        var rt=alicateImagem.rectTransform;
        if(!cursorSobControle)
        {
            cursorAnterior=Cursor.visible;cursorSobControle=true;Cursor.visible=false;
            rt.anchoredPosition=alvo;velocidadeAlicate=Vector2.zero;anguloAlicate=velocidadeAngular=0;
        }
        if(CliquePressionado()){fechaAte=Time.unscaledTime+.15f;velocidadeAngular-=200*bounce*intensidadeGeral;}
        float dt=Mathf.Min(Time.unscaledDeltaTime,.05f),k=Mathf.Max(30,molaAlicate);
        int passos=Mathf.Max(1,Mathf.CeilToInt(dt/.008f));float h=dt/passos;
        Vector2 pos=rt.anchoredPosition;
        for(int i=0;i<passos;i++)
        {
            velocidadeAlicate+=((alvo-pos)*k-velocidadeAlicate*(2*Mathf.Sqrt(k)*amortecimentoAlicate))*h;
            pos+=velocidadeAlicate*h;
        }
        rt.anchoredPosition=intensidadeGeral>0?pos:alvo;
        float giro=Mathf.Clamp(-velocidadeAlicate.x*.02f,-30,30)*giroAlicate*intensidadeGeral;
        anguloAlicate=Mathf.SmoothDampAngle(anguloAlicate,giro,ref velocidadeAngular,.12f,Mathf.Infinity,dt);
        rt.localRotation=Quaternion.Euler(0,0,anguloAlicate);
        bool fechado=Time.unscaledTime<fechaAte;
        alicateImagem.sprite=fechado && alicateFechado!=null?alicateFechado:alicateAberto;
        alicateImagem.color=alicateImagem.sprite!=null?Color.white:fechado?new Color(.4f,.43f,.48f):new Color(.7f,.73f,.78f);
        if(CliquePressionado() && !animando && SobrepoeAlicate() && !CliqueEmOutroBotao())Cortar();
    }

    [SerializeField,HideInInspector] private RectTransform hudTextos;
    private readonly Vector3[] cantosA=new Vector3[4],cantosB=new Vector3[4];
    private readonly List<RaycastResult> hitsUI=new List<RaycastResult>();

    // Estilo único: fonte, cor, outline e sombra vêm dos campos do Inspector. O tamanho nunca é alterado aqui.
    private void EstilizarTexto(TMP_Text t)
    {
        if(t==null)return;
        // Outline SDF nativo, sem duplicar a malha com o Outline legado do uGUI.
        var antigo=t.GetComponent<Outline>();if(antigo!=null)antigo.enabled=false;
        t.font=Fonte();
        t.color=(t==mensagemTMP && mensagemErro)?corTextoErro:corTexto;
        if(t.font==null)return;
        ShaderUtilities.GetShaderPropertyIDs();
        // A fonte nova recria o material; por isso as propriedades são gravadas direto na instância.
        Material m=t.fontMaterial;if(m==null)return;
        float largura=Mathf.Clamp(espessuraOutlineTexto*.06f,0f,.35f);
        if(largura>0)m.EnableKeyword(ShaderUtilities.Keyword_Outline);else m.DisableKeyword(ShaderUtilities.Keyword_Outline);
        m.SetFloat(ShaderUtilities.ID_OutlineWidth,largura);
        m.SetColor(ShaderUtilities.ID_OutlineColor,corOutlineTexto);
        if(usarSombraTexto && corSombraTexto.a>0)
        {
            m.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            m.SetColor(ShaderUtilities.ID_UnderlayColor,corSombraTexto);
            m.SetFloat(ShaderUtilities.ID_UnderlayOffsetX,sombraX);
            m.SetFloat(ShaderUtilities.ID_UnderlayOffsetY,sombraY);
            m.SetFloat(ShaderUtilities.ID_UnderlayDilate,sombraExpansao);
            m.SetFloat(ShaderUtilities.ID_UnderlaySoftness,sombraSuavidade);
        }
        else m.DisableKeyword(ShaderUtilities.Keyword_Underlay);
        t.UpdateMeshPadding();
    }
    private void MigrarTextosTMP()
    {
        // Mantém o RectTransform original. O componente legado fica desativado;
        // o novo texto ocupa o mesmo retângulo e pode ser editado normalmente.
        foreach(var antigo in raiz.GetComponentsInChildren<UnityEngine.UI.Text>(true))
        {
            var filho=antigo.transform.Find("TextoTMP");
            TMP_Text tmp=filho!=null?filho.GetComponent<TMP_Text>():null;
            bool titulo=antigo.name=="Etapa" || antigo.name.StartsWith("Titulo");
            if(tmp==null)
            {
                var rt=Rect(antigo.transform,"TextoTMP",Vector2.zero,Vector2.zero);Esticar(rt);
                tmp=rt.gameObject.AddComponent<TextMeshProUGUI>();
                tmp.text=antigo.text;tmp.font=Fonte();tmp.fontSize=antigo.fontSize;
                tmp.fontStyle=antigo.fontStyle==FontStyle.Bold?FontStyles.Bold:
                    antigo.fontStyle==FontStyle.Italic?FontStyles.Italic:
                    antigo.fontStyle==FontStyle.BoldAndItalic?FontStyles.Bold|FontStyles.Italic:FontStyles.Normal;
                tmp.color=antigo.color;tmp.raycastTarget=false;tmp.enableAutoSizing=false;
                tmp.richText=true;tmp.textWrappingMode=TextWrappingModes.Normal;tmp.overflowMode=TextOverflowModes.Overflow;
                tmp.alignment=ConverterAlinhamento(antigo.alignment);
                tmp.maxVisibleCharacters=int.MaxValue;
            }
            antigo.enabled=false;
            var contorno=antigo.GetComponent<Outline>();if(contorno!=null)contorno.enabled=false;
            if(!rotulosTMP.Exists(r=>r.texto==tmp))rotulosTMP.Add(new Rotulo{texto=tmp,titulo=titulo});
            if(antigo.name=="Etapa")etapaTMP=tmp;
            if(antigo.name=="Mensagem")mensagemTMP=tmp;
        }
        // Também recupera referências caso a cena já tenha os componentes TMP.
        foreach(var t in raiz.GetComponentsInChildren<TMP_Text>(true))
        {
            if(!rotulosTMP.Exists(r=>r.texto==t))rotulosTMP.Add(new Rotulo{texto=t,titulo=t.name=="Etapa" || t.name.StartsWith("Titulo")});
            if(t.name=="Etapa")etapaTMP=t;
            if(t.name=="Mensagem")mensagemTMP=t;
        }
    }
    private TextAlignmentOptions ConverterAlinhamento(TextAnchor a)
    {
        switch(a)
        {
            case TextAnchor.UpperLeft:return TextAlignmentOptions.TopLeft;
            case TextAnchor.UpperCenter:return TextAlignmentOptions.Top;
            case TextAnchor.UpperRight:return TextAlignmentOptions.TopRight;
            case TextAnchor.MiddleLeft:return TextAlignmentOptions.MidlineLeft;
            case TextAnchor.MiddleRight:return TextAlignmentOptions.MidlineRight;
            case TextAnchor.LowerLeft:return TextAlignmentOptions.BottomLeft;
            case TextAnchor.LowerCenter:return TextAlignmentOptions.Bottom;
            case TextAnchor.LowerRight:return TextAlignmentOptions.BottomRight;
            default:return TextAlignmentOptions.Center;
        }
    }
    private void GarantirHUD()
    {
        if(raiz==null)return;
        if(hudTextos==null)
        {
            hudTextos=Rect(raiz,"HUD_TextosPrimeiroPlano",Vector2.zero,Vector2.zero);Esticar(hudTextos);
        }
        var hudCanvas=hudTextos.GetComponent<Canvas>();if(hudCanvas==null)hudCanvas=hudTextos.gameObject.AddComponent<Canvas>();
        hudCanvas.overrideSorting=true;hudCanvas.sortingLayerID=canvas.sortingLayerID;
        hudCanvas.sortingOrder=Mathf.Min(32767,canvas.sortingOrder+100);
        MoverTextoHUD(etapaTMP);MoverTextoHUD(mensagemTMP);
        hudTextos.SetAsLastSibling();
    }
    private void MoverTextoHUD(TMP_Text t)
    {
        if(t==null)return;
        // Na migração, move o contêiner legado inteiro para preservar o tamanho e a posição.
        Transform mover=t.name=="TextoTMP"?t.transform.parent:t.transform;
        if(mover.parent!=hudTextos)mover.SetParent(hudTextos,true);
        t.raycastTarget=false;
    }
    private bool SobrepoeAlicate()
    {
        if(alicateImagem==null || cortarBtn==null || !cortarBtn.gameObject.activeInHierarchy)return false;
        alicateImagem.rectTransform.GetWorldCorners(cantosA);
        ((RectTransform)cortarBtn.transform).GetWorldCorners(cantosB);
        for(int i=0;i<4;i++){cantosA[i]=raiz.InverseTransformPoint(cantosA[i]);cantosB[i]=raiz.InverseTransformPoint(cantosB[i]);}
        // Separating-axis test: usa o retângulo inteiro e acompanha a rotação da ferramenta.
        for(int shape=0;shape<2;shape++)
        {
            Vector3[] pontos=shape==0?cantosA:cantosB;
            for(int i=0;i<4;i++)
            {
                Vector2 aresta=pontos[(i+1)%4]-pontos[i];Vector2 eixo=new Vector2(-aresta.y,aresta.x);
                if(eixo.sqrMagnitude<.000001f)continue;
                float minA=float.PositiveInfinity,maxA=float.NegativeInfinity,minB=float.PositiveInfinity,maxB=float.NegativeInfinity;
                for(int j=0;j<4;j++)
                {
                    float a=Vector2.Dot(cantosA[j],eixo),b=Vector2.Dot(cantosB[j],eixo);
                    minA=Mathf.Min(minA,a);maxA=Mathf.Max(maxA,a);minB=Mathf.Min(minB,b);maxB=Mathf.Max(maxB,b);
                }
                if(maxA<minB || maxB<minA)return false;
            }
        }
        return true;
    }
    private bool CliqueEmOutroBotao()
    {
        if(EventSystem.current==null)return false;
        var evento=new PointerEventData(EventSystem.current){position=Ponteiro()};hitsUI.Clear();
        EventSystem.current.RaycastAll(evento,hitsUI);
        if(hitsUI.Count==0)return false;
        var b=hitsUI[0].gameObject.GetComponentInParent<Button>();return b!=null && b!=cortarBtn;
    }
    private void OnDestroy(){LimparLetreiros();RestaurarCursor();}

}
#if UNITY_EDITOR
[CustomEditor(typeof(RedeCrimpMinigame))]
public class RedeCrimpMinigameInspector : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();var alvo=(RedeCrimpMinigame)target;EditorGUILayout.Space();
        EditorGUILayout.HelpBox("A UI fica salva na hierarquia. Selecione UI_RedeCrimpMinigame e pressione F para enquadrar na Scene View. Reconstruir redefine o layout gerado.",MessageType.Info);
        if(GUILayout.Button("Gerar interface / mostrar prévia")){Undo.RecordObject(alvo,"Mostrar prévia");alvo.Abrir();EditorUtility.SetDirty(alvo);}
        if(GUILayout.Button("Aplicar sprites e estilo dos textos"))alvo.AplicarVisual();
        using(new EditorGUI.DisabledScope(Application.isPlaying))
            if(GUILayout.Button("Reconstruir interface (redefine layout)"))alvo.ReconstruirInterface();
    }
}
#endif