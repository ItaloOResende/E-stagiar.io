using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

/// <summary>Rack independente: um cabo pronto por par, números iguais, sem inventário 3D.</summary>
[DisallowMultipleComponent]
public class RedeRackMinigame : MonoBehaviour
{
    [Header("Cena independente")]
    public bool abrirAoIniciar;
    public RedeCrimpMinigame crimpagem;
    [Min(1)] public int numeroPortas = 6;
    [Tooltip("Para testar o rack isolado. Na cena completa deixe 0.")]
    [Min(0)] public int cabosIniciais = 0;
    [Header("Sprites — campos vazios usam retângulos")]
    public Arte fundo = new Arte(new Color(.045f,.065f,.10f));
    public Arte patchPanel = new Arte(new Color(.14f,.19f,.27f));
    public Arte switchPainel = new Arte(new Color(.14f,.19f,.27f));
    public Arte portaPatch = new Arte(new Color(.30f,.37f,.47f));
    public Arte portaSwitch = new Arte(new Color(.30f,.37f,.47f));
    public Arte plugue = new Arte(new Color(.8f,.86f,.92f));
    public Arte cabo = new Arte(new Color(.2f,.5f,.95f));
    public Arte ledDesligado = new Arte(new Color(.1f,.2f,.18f));
    public Arte ledLigado = new Arte(new Color(.2f,1f,.6f));
    public Arte botao = new Arte(new Color(.20f,.29f,.42f));
    public Arte efeito = new Arte(Color.white);
    public AudioClip somEncaixe, somRetirar;
    [Range(0f, 2f)] public float balancoCabo = .7f;
    [Range(3f, 20f)] public float espessuraCabo = 8f;
    public int CabosDisponiveis { get { return disponiveis; } }
    public int ParesLigados { get { return ligados == null ? 0 : ContarLigados(); } }

    private bool pronto;
    private int n, disponiveis, ponta = -1;
    private bool pontaPatch;
    private bool[] ligados;
    private Button[] portasP, portasS;
    private Image[] ledsP, ledsS, plugsP, plugsS;
    private Vector2[] posP,posS;
    private Image[][] curvas;
    private Image[] curvaLivre;
    private Image plugLivre, alvo;
    private Text status;

    private void Start(){Construir();if(abrirAoIniciar)Abrir();else Fechar();}
    private void Construir()
    {
        if(pronto)return;pronto=true;
        n=Mathf.Max(1,numeroPortas);disponiveis=Mathf.Max(0,cabosIniciais);ligados=new bool[n];
        BaseUI("RACK / CONEXÃO DE REDE");
        status=Texto(palco,"Status","",new Vector2(0,310),new Vector2(1300,50),24);
        Imagem(palco,"PatchPanel",patchPanel,new Vector2(0,160),new Vector2(1420,190));
        Imagem(palco,"Switch",switchPainel,new Vector2(0,-160),new Vector2(1420,190));
        Texto(palco,"PatchLabel","PATCH PANEL",new Vector2(0,235),new Vector2(1300,36),20);
        Texto(palco,"SwitchLabel","SWITCH",new Vector2(0,-85),new Vector2(1300,36),20);
        portasP=new Button[n];portasS=new Button[n];posP=new Vector2[n];posS=new Vector2[n];
        ledsP=new Image[n];ledsS=new Image[n];plugsP=new Image[n];plugsS=new Image[n];curvas=new Image[n][];
        float passo=1280f/n;float largura=Mathf.Min(90,passo*.65f);
        for(int i=0;i<n;i++)
        {
            int k=i;float x=(i-(n-1)*.5f)*passo;posP[i]=new Vector2(x,150);posS[i]=new Vector2(x,-170);
            portasP[i]=Botao(palco,"P"+(i+1),"",portaPatch,posP[i],new Vector2(largura,70),()=>Clicar(true,k));
            portasS[i]=Botao(palco,"S"+(i+1),"",portaSwitch,posS[i],new Vector2(largura,70),()=>Clicar(false,k));
            Texto(palco,"NumeroP"+i,"P"+(i+1),posP[i]+new Vector2(0,-55),new Vector2(passo,30),18);
            Texto(palco,"NumeroS"+i,"S"+(i+1),posS[i]+new Vector2(0,-55),new Vector2(passo,30),18);
            ledsP[i]=Imagem(palco,"LedP"+i,ledDesligado,posP[i]+new Vector2(largura*.5f+8,0),new Vector2(8,18));
            ledsS[i]=Imagem(palco,"LedS"+i,ledDesligado,posS[i]+new Vector2(largura*.5f+8,0),new Vector2(8,18));
        }
        for(int i=0;i<n;i++)curvas[i]=NovaCurva("Cabo_"+(i+1));
        curvaLivre=NovaCurva("CaboLivre");
        alvo=Imagem(palco,"Alvo",efeito,Vector2.zero,new Vector2(largura+14,84));
        for(int i=0;i<n;i++)
        {
            plugsP[i]=Imagem(palco,"PlugP"+i,plugue,posP[i],new Vector2(largura*.7f,48));
            plugsS[i]=Imagem(palco,"PlugS"+i,plugue,posS[i],new Vector2(largura*.7f,48));
        }
        plugLivre=Imagem(palco,"PlugLivre",plugue,Vector2.zero,new Vector2(50,60));
        Botao(palco,"Crimpagem","Crimpar cabo",botao,new Vector2(0,-410),new Vector2(300,50),()=>{if(crimpagem!=null){Fechar();crimpagem.Abrir();}else Feedback("Associe a crimpagem no Inspector.",true);});
        Atualizar();Feedback("Crimpe um cabo e conecte portas de mesmo número.");
    }
    public void Abrir(){Construir();if(crimpagem!=null)crimpagem.Fechar();raiz.gameObject.SetActive(true);Atualizar();}
    /// <summary>Cada chamada entrega exatamente um cabo. O vínculo da crimpagem já chama este método.</summary>
    public void ReceberCabo(){Construir();disponiveis++;Atualizar();}
    [ContextMenu("Reiniciar rack (durante Play)")]
    public void Reiniciar()
    {
        if(!Application.isPlaying)return;
        Construir();Array.Clear(ligados,0,n);ponta=-1;disponiveis=Mathf.Max(0,cabosIniciais);Atualizar();
    }
    private int ContarLigados(){int k=0;foreach(bool l in ligados)if(l)k++;return k;}
    private void Clicar(bool patch,int i)
    {
        if(ligados[i]){Feedback("Esta porta já está ligada.");return;}
        if(disponiveis<=0){Feedback("Você precisa de um cabo crimpado.",true);return;}
        if(ponta<0){ponta=i;pontaPatch=patch;Som(somEncaixe);Feedback("Conecte a outra ponta na porta "+(i+1)+" do outro lado.");}
        else if(ponta==i && pontaPatch==patch){ponta=-1;Som(somRetirar);Feedback("Cabo retirado. Escolha outra porta.");}
        else if(pontaPatch==patch){ponta=i;Som(somEncaixe);Feedback("Ponta movida para a porta "+(i+1)+".");}
        else if(ponta!=i){Feedback("Os números das portas precisam ser iguais.",true,true);}
        else
        {
            ligados[i]=true;ponta=-1;disponiveis--;Som(somEncaixe);
            Feedback("Par "+(i+1)+" conectado!",false,true);
            Atualizar();
            if(ContarLigados()==n){Feedback("Rede conectada! Todos os pares concluídos.",false,true);aoConcluir.Invoke();}
            return;
        }
        Atualizar();Pop((RectTransform)(patch?portasP[i]:portasS[i]).transform);
    }
    private void Atualizar()
    {
        status.text=ContarLigados()+" / "+n+" pares conectados     •     Cabos prontos: "+disponiveis;
        for(int i=0;i<n;i++)
        {
            Aplicar(ledsP[i],ligados[i]?ledLigado:ledDesligado);Aplicar(ledsS[i],ligados[i]?ledLigado:ledDesligado);
            plugsP[i].gameObject.SetActive(ligados[i] || ponta==i && pontaPatch);
            plugsS[i].gameObject.SetActive(ligados[i] || ponta==i && !pontaPatch);
            foreach(var seg in curvas[i])seg.gameObject.SetActive(ligados[i]);
            if(ligados[i])DesenharCurva(curvas[i],posP[i],posS[i],45f,0);
        }
        bool tem=disponiveis>0;
        plugLivre.gameObject.SetActive(tem);foreach(var seg in curvaLivre)seg.gameObject.SetActive(tem);
        alvo.gameObject.SetActive(ponta>=0);
    }
    private Image[] NovaCurva(string nome)
    {
        var segmentos=new Image[28];
        for(int i=0;i<segmentos.Length;i++)segmentos[i]=Imagem(palco,nome+"_"+i,cabo,Vector2.zero,Vector2.one);
        return segmentos;
    }
    private void DesenharCurva(Image[] segmentos,Vector2 inicio,Vector2 fim,float barriga,float onda)
    {
        Vector2 anterior=inicio;
        for(int i=0;i<segmentos.Length;i++)
        {
            float t=(i+1f)/segmentos.Length;
            Vector2 p=Vector2.Lerp(inicio,fim,t)+new Vector2(Mathf.Sin(t*Mathf.PI)*(barriga+onda),0);
            Vector2 d=p-anterior;var rt=segmentos[i].rectTransform;
            rt.anchoredPosition=(anterior+p)*.5f;rt.sizeDelta=new Vector2(d.magnitude+1,espessuraCabo);
            rt.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(d.y,d.x)*Mathf.Rad2Deg);anterior=p;
        }
    }
    private void Update()
    {
        if(!pronto || !raiz.gameObject.activeInHierarchy)return;Animar();
        if(disponiveis>0)
        {
            Vector2 destino=MouseLocal();plugLivre.rectTransform.anchoredPosition=destino;
            Vector2 inicio=ponta>=0?(pontaPatch?posP[ponta]:posS[ponta]):new Vector2(0,-480);
            DesenharCurva(curvaLivre,inicio,destino,60f,Mathf.Sin(Time.unscaledTime*5f)*12f*balancoCabo*intensidadeGeral);
        }
        if(ponta>=0)
        {
            alvo.rectTransform.anchoredPosition=pontaPatch?posS[ponta]:posP[ponta];
            alvo.color=new Color(1f,.84f,.3f,(.15f+.13f*Mathf.Sin(Time.unscaledTime*5f)*pulsacao*intensidadeGeral));
        }
        for(int i=0;i<n;i++)if(ligados[i])
        {
            float luz=1f-(Mathf.Sin(Time.unscaledTime*7f+i)+1f)*.1f*pulsacao*intensidadeGeral;
            Color cor=ledLigado.sprite!=null?Color.white:ledLigado.corPlaceholder;
            ledsP[i].color=ledsS[i].color=Color.Lerp(cor,Color.black,1f-luz);
        }
    }

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
    [Header("Fontes (Unity Font .ttf/.otf)")]
    public Font fonteTitulo;
    public Font fonteTexto;
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

    private RectTransform raiz, palco;
    private Canvas canvas;
    private Image flash;
    private Text mensagem;
    private AudioSource audioUI;
    private Font fallback;
    private float impulso, idadeEfeito;
    private bool efeitoErro;
    private readonly List<Image> alvos = new List<Image>();
    private readonly List<Button> botoes = new List<Button>();
    private readonly List<Arte> artesAlvos = new List<Arte>();
    private readonly List<Particula> poeira = new List<Particula>();
    private readonly List<Visual> visuais = new List<Visual>();
    private class Visual { public Image imagem; public Arte arte; }
    private class Particula { public Image imagem; public Vector2 velocidade; public float vida; }

    private void BaseUI(string titulo)
    {
        var go = new GameObject("UI_" + GetType().Name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.transform.SetParent(transform, false);
        canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600, 900);
        scaler.matchWidthOrHeight = 0.5f;
        raiz = go.GetComponent<RectTransform>();
        var bg = Imagem(raiz, "Fundo", fundo, Vector2.zero, Vector2.zero);
        bg.rectTransform.anchorMin = Vector2.zero; bg.rectTransform.anchorMax = Vector2.one;
        bg.rectTransform.offsetMin = bg.rectTransform.offsetMax = Vector2.zero;
        bg.raycastTarget = true;
        palco = Retangulo(raiz, "Palco", Vector2.zero, new Vector2(1600, 900));
        Texto(palco, "Titulo", titulo, new Vector2(0, 375), new Vector2(1300, 70), 40, true);
        mensagem = Texto(palco, "Mensagem", "", new Vector2(0, -345), new Vector2(1420, 70), 23);
        flash = Imagem(raiz, "Flash", efeito, Vector2.zero, Vector2.zero);
        flash.rectTransform.anchorMin = Vector2.zero; flash.rectTransform.anchorMax = Vector2.one;
        flash.rectTransform.offsetMin = flash.rectTransform.offsetMax = Vector2.zero;
        flash.color = Color.clear;
        audioUI = go.AddComponent<AudioSource>(); audioUI.playOnAwake = false;
        if (EventSystem.current == null && FindObjectOfType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            es.AddComponent<InputSystemUIInputModule>();
#else
            es.AddComponent<StandaloneInputModule>();
#endif
        }
    }
    private RectTransform Retangulo(Transform pai, string nome, Vector2 pos, Vector2 tamanho)
    {
        var rt = new GameObject(nome, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(pai, false); rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, .5f);
        rt.anchoredPosition = pos; rt.sizeDelta = tamanho;
        return rt;
    }
    private Image Imagem(Transform pai, string nome, Arte arte, Vector2 pos, Vector2 tamanho)
    {
        var img = Retangulo(pai, nome, pos, tamanho).gameObject.AddComponent<Image>();
        img.raycastTarget = false;
        visuais.Add(new Visual { imagem = img, arte = arte });
        Aplicar(img, arte);
        return img;
    }
    private void Aplicar(Image img, Arte arte)
    {
        if (arte == null) return;
        img.sprite = arte.sprite;
        img.color = arte.sprite != null ? Color.white : arte.corPlaceholder;
        img.preserveAspect = arte.preservarProporcao;
    }
    private Text Texto(Transform pai, string nome, string valor, Vector2 pos, Vector2 tamanho, int tamanhoFonte, bool titulo = false)
    {
        var t = Retangulo(pai, nome, pos, tamanho).gameObject.AddComponent<Text>();
        if (fallback == null) fallback = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.font = titulo && fonteTitulo != null ? fonteTitulo : fonteTexto != null ? fonteTexto : fallback;
        t.text = valor; t.fontSize = tamanhoFonte; t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white; t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        return t;
    }
    private Button Botao(Transform pai, string nome, string rotulo, Arte arte, Vector2 pos, Vector2 tamanho, UnityAction acao)
    {
        var img = Imagem(pai, nome, arte, pos, tamanho); img.raycastTarget = true;
        var b = img.gameObject.AddComponent<Button>(); b.targetGraphic = img;
        var nav = b.navigation; nav.mode = Navigation.Mode.None; b.navigation = nav;
        var c = b.colors; c.highlightedColor = new Color(1f, .93f, .7f); c.pressedColor = new Color(.65f,.72f,.85f); b.colors = c;
        b.onClick.AddListener(() => { Som(somClique); acao(); });
        if (!string.IsNullOrEmpty(rotulo)) Texto(img.transform, "Rotulo", rotulo, Vector2.zero, tamanho - new Vector2(12,8), 22);
        botoes.Add(b); return b;
    }
    private void Som(AudioClip clip) { if (clip != null && audioUI != null) audioUI.PlayOneShot(clip, volume); }
    private void Feedback(string texto, bool erro = false, bool forte = false)
    {
        mensagem.text = texto; mensagem.color = erro ? new Color(1f,.5f,.45f) : Color.white;
        impulso = forte ? 1f : .4f; idadeEfeito = 0f; efeitoErro = erro;
        if (erro) Som(somErro); else if (forte) Som(somAcerto);
        if (forte && !erro)
        {
            int n = Mathf.RoundToInt(16 * particulas * intensidadeGeral);
            for (int i = 0; i < n; i++)
            {
                var img = Imagem(palco, "Particula", efeito, Vector2.zero, new Vector2(9,9));
                // Partículas temporárias não entram na atualização de arte persistente.
                visuais.RemoveAt(visuais.Count - 1);
                img.color = new Color(.4f, 1f, .8f);
                poeira.Add(new Particula { imagem = img, vida = .65f, velocidade = UnityEngine.Random.insideUnitCircle * 550f });
            }
        }
    }
    private void Animar()
    {
        float dt = Time.unscaledDeltaTime;
        idadeEfeito += dt;
        float decay = Mathf.Max(0, 1f - idadeEfeito / .38f) * impulso * intensidadeGeral;
        palco.anchoredPosition = efeitoErro ? new Vector2(Mathf.Sin(idadeEfeito * 95f) * 12f * tremor * decay, 0) : Vector2.zero;
        palco.localScale = Vector3.one * (1f + Mathf.Sin(Mathf.Min(1f, idadeEfeito / .38f) * Mathf.PI) * .035f * zoom * decay);
        flash.color = new Color(efeitoErro ? 1f : .3f, efeitoErro ? .2f : 1f, .6f, decay * .13f * brilho);
        for (int i = 0; i < alvos.Count; i++)
        {
            if (alvos[i] == null || !alvos[i].gameObject.activeInHierarchy) continue;
            Aplicar(alvos[i], artesAlvos[i]);
            float p = (Mathf.Sin(Time.unscaledTime * 5f) + 1f) * .5f * pulsacao * intensidadeGeral;
            alvos[i].color = Color.Lerp(alvos[i].color, new Color(1f,.85f,.35f), p * .35f);
        }
        foreach (var b in botoes)
        {
            if (b == null || !b.gameObject.activeInHierarchy) continue;
            bool hover = b.interactable && RectTransformUtility.RectangleContainsScreenPoint((RectTransform)b.transform, Ponteiro(), null);
            float target = hover ? 1f + .04f * bounce * intensidadeGeral : 1f;
            b.transform.localScale = Vector3.Lerp(b.transform.localScale, Vector3.one * target, 1f - Mathf.Exp(-20f * dt));
        }
        for (int i = poeira.Count - 1; i >= 0; i--)
        {
            var p = poeira[i]; p.vida -= dt;
            if (p.vida <= 0) { Destroy(p.imagem.gameObject); poeira.RemoveAt(i); continue; }
            p.velocidade += Vector2.down * 600f * dt;
            p.imagem.rectTransform.anchoredPosition += p.velocidade * dt;
            var cor = p.imagem.color; cor.a = p.vida / .65f; p.imagem.color = cor;
        }
    }
    private void Pop(RectTransform rt)
    {
        rt.localScale = Vector3.one * (1f + .12f * bounce * intensidadeGeral);
    }
    private Vector2 Ponteiro()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null ? Mouse.current.position.ReadValue() : new Vector2(-10000,-10000);
#else
        return Input.mousePosition;
#endif
    }
    private Vector2 MouseLocal()
    {
        Vector2 p; RectTransformUtility.ScreenPointToLocalPointInRectangle(palco, Ponteiro(), null, out p); return p;
    }
    [ContextMenu("Aplicar sprites e fontes (durante Play)")]
    public void AplicarVisual()
    {
        foreach (var v in visuais) if (v.imagem != null) Aplicar(v.imagem, v.arte);
        if (raiz != null)
            foreach (var t in raiz.GetComponentsInChildren<Text>(true))
                t.font = t.name == "Titulo" && fonteTitulo != null ? fonteTitulo : fonteTexto != null ? fonteTexto : fallback;
        if (pronto) Atualizar();
    }
    public void Fechar() { if (raiz != null) raiz.gameObject.SetActive(false); }
    private void OnDisable()
    {
        Fechar();
        if (palco != null) { palco.localScale = Vector3.one; palco.anchoredPosition = Vector2.zero; }
        foreach (var p in poeira) if (p.imagem != null) Destroy(p.imagem.gameObject);
        poeira.Clear();
    }
}
