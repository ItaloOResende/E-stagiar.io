using UnityEngine;

/// <summary>
/// Faz o MODELO 3D do notebook (Prefabs/Props/Notebook_Toon) mostrar o estado real do <see cref="NotebookEstacao"/>:
///  - tela: desligada (escura), BIOS, instalando (por etapa) ou "Sistema iniciado com sucesso" (as mesmas imagens da
///    tela 2D); LED de energia aceso com o notebook ligado; piscada âmbar quando o Power não liga (<see cref="PiscarFalha"/>);
///  - tampa de baixo e parafusos (somem quando o parafuso sai; a tampa some com todos fora);
///  - RAM e SSD aparecem ENCAIXADOS nos slots quando instalados; o pendrive aparece na USB quando conectado.
/// Vale para o notebook do mundo (acha a estação no pai) e para a cópia mostrada na tela 2D (<see cref="Vincular"/>).
/// Só muda visibilidade e cor/textura por MaterialPropertyBlock: os materiais compartilhados não mudam.
/// </summary>
public class NotebookModeloEstado : MonoBehaviour
{
    private static readonly int MainTex = Shader.PropertyToID("_MainTex");
    private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
    private static readonly int ShadeMap1 = Shader.PropertyToID("_1st_ShadeMap");
    private static readonly int ShadeMap2 = Shader.PropertyToID("_2nd_ShadeMap");
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int Shade1 = Shader.PropertyToID("_1st_ShadeColor");
    private static readonly int Shade2 = Shader.PropertyToID("_2nd_ShadeColor");
    private static readonly int Emissive = Shader.PropertyToID("_Emissive_Color");

    [Header("Imagens da tela (as mesmas da tela 2D)")]
    [SerializeField] private Texture telaSistema;
    [SerializeField] private Texture telaBios;
    [SerializeField] private Texture telaBiosSemSistema;
    [SerializeField] private Texture[] telaInstalando = new Texture[0];
    [SerializeField] private Color telaDesligada = new Color(0.03f, 0.035f, 0.05f);
    [SerializeField] private Color ledLigado = new Color(0.35f, 1f, 0.45f);
    [SerializeField] private Color ledDesligado = new Color(0.12f, 0.14f, 0.13f);
    [SerializeField] private Color ledFalha = new Color(1f, 0.6f, 0.15f);

    private NotebookEstacao estacao;
    private Renderer tela, led;
    private GameObject tampaInferior, ram, ssd, pendrive;
    private readonly GameObject[] parafusos = new GameObject[6];
    private MaterialPropertyBlock bloco;
    private float falhaAte = -1f;
    private Texture ultimaTela;
    private bool primeira = true;
    private bool tinhaRam, tinhaSsd;
    private Vector3 restoRam, restoSsd;
    private float encaixeRam = -1f, encaixeSsd = -1f;   // Time.unscaledTime em que a peça começou a encaixar

    /// <summary>
    /// Mostra as cabeças dos parafusos do modelo. A tela 2D de baixo desliga: lá a cabeça e o furo são desenhados pela
    /// própria tela (como na traseira do gabinete) e o ScrewView anima.
    /// </summary>
    public bool MostrarParafusos { get; set; } = true;

    /// <summary>
    /// A tampa de baixo continua à vista mesmo com o notebook aberto. A vista de baixo da tela 2D liga: é onde os
    /// parafusos saem e voltam, e o interior fica só na vista "Aberto" (senão as duas mostrariam o notebook aberto).
    /// </summary>
    public bool ManterTampa { get; set; }

    /// <summary>Segundos da animação de encaixe da RAM/SSD.</summary>
    public const float DuracaoEncaixe = 0.35f;

    public NotebookEstacao Estacao => estacao;

    public void Configurar(Texture sistema, Texture bios, Texture biosSemSistema, Texture[] instalando)
    {
        telaSistema = sistema; telaBios = bios; telaBiosSemSistema = biosSemSistema; telaInstalando = instalando;
    }

    /// <summary>Liga o modelo a um notebook (a cópia da tela 2D usa o notebook que abriu a tela).</summary>
    public void Vincular(NotebookEstacao est)
    {
        estacao = est;
        primeira = true;
        Atualizar();
    }

    /// <summary>O Power foi apertado e o notebook não ligou: o LED pisca âmbar por um instante.</summary>
    public void PiscarFalha() => falhaAte = Time.unscaledTime + 0.6f;

    /// <summary>Peça do modelo pelo nome (para a tela 2D pôr os pontos clicáveis em cima).</summary>
    public Transform Parte(string nome) => Achar(transform, nome);

    private void Awake()
    {
        tela = Componente<Renderer>("Tela");
        led = Componente<Renderer>("LedEnergia");
        tampaInferior = Objeto("TampaInferior");
        ram = Objeto("RAM_Instalada");
        ssd = Objeto("SSD_Instalado");
        pendrive = Objeto("Pendrive");
        for (int i = 0; i < parafusos.Length; i++) parafusos[i] = Objeto("Parafuso_" + i);
        if (ram != null) restoRam = ram.transform.localPosition;
        if (ssd != null) restoSsd = ssd.transform.localPosition;
        if (estacao == null) estacao = GetComponentInParent<NotebookEstacao>();
    }

    private void LateUpdate() => Atualizar();

    private void Atualizar()
    {
        NotebookSession s = estacao != null ? estacao.Sessao : null;
        if (s == null) return;

        for (int i = 0; i < parafusos.Length; i++) Mostrar(parafusos[i], MostrarParafusos && !s.ParafusoRemovido(i));
        Mostrar(tampaInferior, ManterTampa || s.Estado != NotebookEstado.Aberto);
        // peça recém-instalada: aparece fora do slot e desliza até encaixar (como as peças do gabinete)
        if (!primeira && s.TemRam && !tinhaRam) encaixeRam = Time.unscaledTime;
        if (!primeira && s.TemSsd && !tinhaSsd) encaixeSsd = Time.unscaledTime;
        tinhaRam = s.TemRam; tinhaSsd = s.TemSsd;
        Mostrar(ram, s.TemRam);
        Mostrar(ssd, s.TemSsd);
        Encaixar(ram, restoRam, ref encaixeRam, new Vector3(0f, -0.004f, 0.022f));
        Encaixar(ssd, restoSsd, ref encaixeSsd, new Vector3(0f, -0.004f, 0.03f));
        Mostrar(pendrive, s.PendriveConectado);

        Texture img = Imagem(s);
        if (primeira || img != ultimaTela)
        {
            Pintar(tela, img, img != null ? Color.white : telaDesligada, img != null);
            ultimaTela = img;
        }
        bool falha = Time.unscaledTime < falhaAte && Mathf.Repeat(Time.unscaledTime * 6f, 1f) < 0.5f;
        Pintar(led, null, falha ? ledFalha : s.Ligado ? ledLigado : ledDesligado, falha || s.Ligado);
        primeira = false;
    }

    // Desliza a peça do ponto de entrada (resto + desvio, inclinada) até o resto, com uma pequena desaceleração.
    private static void Encaixar(GameObject peca, Vector3 resto, ref float inicio, Vector3 desvio)
    {
        if (peca == null) return;
        if (inicio < 0f) { peca.transform.localPosition = resto; peca.transform.localRotation = Quaternion.identity; return; }
        float t = Mathf.Clamp01((Time.unscaledTime - inicio) / DuracaoEncaixe);
        float e = 1f - (1f - t) * (1f - t) * (1f - t);
        peca.transform.localPosition = resto + desvio * (1f - e);
        peca.transform.localRotation = Quaternion.Euler(-18f * (1f - e), 0f, 0f);
        if (t >= 1f) inicio = -1f;
    }

    private Texture Imagem(NotebookSession s)
    {
        switch (s.Tela)
        {
            case NotebookEstadoTela.SemArmazenamento: return telaBios;
            case NotebookEstadoTela.SemSistema: return telaBiosSemSistema != null ? telaBiosSemSistema : telaBios;
            case NotebookEstadoTela.Instalando:
                return telaInstalando != null && telaInstalando.Length > 0
                    ? telaInstalando[Mathf.Clamp(s.EtapaInstalacao, 0, telaInstalando.Length - 1)] : telaBios;
            case NotebookEstadoTela.Sistema: return telaSistema;
            default: return null;
        }
    }

    // Tela e LED "acesos" ficam com a mesma cor nas áreas de sombra (sem degrau do toon) e com emissão.
    private void Pintar(Renderer r, Texture tex, Color cor, bool aceso)
    {
        if (r == null) return;
        bloco ??= new MaterialPropertyBlock();
        r.GetPropertyBlock(bloco);
        Texture t = tex != null ? tex : Texture2D.whiteTexture;
        bloco.SetTexture(MainTex, t);
        bloco.SetTexture(BaseMap, t);
        bloco.SetTexture(ShadeMap1, t);
        bloco.SetTexture(ShadeMap2, t);
        bloco.SetColor(BaseColor, cor);
        bloco.SetColor(Shade1, aceso ? cor : cor * 0.8f);
        bloco.SetColor(Shade2, aceso ? cor : cor * 0.65f);
        bloco.SetColor(Emissive, aceso && tex == null ? cor : Color.black);
        r.SetPropertyBlock(bloco);
    }

    private static void Mostrar(GameObject go, bool ativo)
    {
        if (go != null && go.activeSelf != ativo) go.SetActive(ativo);
    }

    private GameObject Objeto(string nome)
    {
        Transform t = Achar(transform, nome);
        return t != null ? t.gameObject : null;
    }

    private T Componente<T>(string nome) where T : Component
    {
        Transform t = Achar(transform, nome);
        return t != null ? t.GetComponentInChildren<T>(true) : null;   // peças redondas têm o desenho no filho "Forma"
    }

    private static Transform Achar(Transform raiz, string nome)
    {
        if (raiz.name == nome) return raiz;
        foreach (Transform f in raiz)
        {
            Transform r = Achar(f, nome);
            if (r != null) return r;
        }
        return null;
    }
}
