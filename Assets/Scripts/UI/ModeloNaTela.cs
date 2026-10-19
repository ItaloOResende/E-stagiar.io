using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Mostra o MODELO 3D real de um equipamento dentro de uma tela 2D, num ângulo fixo de manutenção (a mesma ideia dos
/// renders do gabinete, mas ao vivo: peças instaladas, parafusos, tela ligada e LEDs aparecem como estão no mundo).
/// Uma CÓPIA só visual do modelo fica longe da cena (<see cref="Distante"/>); uma câmera própria a desenha numa
/// RenderTexture mostrada por esta <see cref="RawImage"/>. A câmera só fica ligada com a tela visível.
/// Os pontos clicáveis da tela ficam sobre as peças com <see cref="Posicao"/>/<see cref="Tamanho"/> (coordenadas
/// locais desta RawImage, centro = 0,0).
/// </summary>
[RequireComponent(typeof(RawImage))]
public class ModeloNaTela : MonoBehaviour
{
    private static int proximo;

    private RawImage imagem;
    private Camera cam;
    private RenderTexture rt;
    private Transform raiz;          // a cópia do modelo
    private Vector3 olharLocal = Vector3.forward, cimaLocal = Vector3.up;
    private Bounds alvoLocal;
    private float margem = 0.08f;
    private Vector2Int resolucao = new Vector2Int(1600, 1000);

    /// <summary>Ponto bem longe de tudo onde as cópias ficam (cada tela num lugar diferente).</summary>
    public static Vector3 Distante(int i) => new Vector3(8000f + i * 40f, -6000f, 8000f);

    public Transform Modelo => raiz;

    private Light luz;
    private float luzDaVista;

    /// <summary>
    /// Luz própria da vista (0 = sem luz própria): uma luz pontual na câmera, com alcance só até o modelo. A cópia fica
    /// longe da cena e só recebe a luz direcional, que em algumas vistas vem de TRÁS do que a câmera mostra (o modelo
    /// saía escuro, como se houvesse um véu preto). Por estar a quilômetros da cena, não ilumina mais nada.
    /// </summary>
    public float LuzDaVista
    {
        get => luzDaVista;
        set { luzDaVista = Mathf.Max(0f, value); AjustarCamera(); }
    }

    /// <summary>Cria a RawImage (esticada no pai) com o componente.</summary>
    public static ModeloNaTela Criar(Transform pai, string nome, Vector2Int resolucao)
    {
        var go = new GameObject(nome, typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(pai, false);
        var rtf = (RectTransform)go.transform;
        rtf.anchorMin = Vector2.zero; rtf.anchorMax = Vector2.one; rtf.offsetMin = rtf.offsetMax = Vector2.zero;
        var m = go.AddComponent<ModeloNaTela>();
        m.resolucao = resolucao;
        m.GetComponent<RawImage>().raycastTarget = false;
        return m;
    }

    /// <summary>
    /// Copia <paramref name="origem"/> (só o visual: colisores, física e scripts saem; ficam os tipos em
    /// <paramref name="manter"/>) e a põe longe da cena. Devolve a cópia.
    /// </summary>
    public Transform Copiar(GameObject origem, params Type[] manter)
    {
        Limpar();
        if (origem == null) return null;
        var holder = new GameObject("ModeloNaTela_" + origem.name);
        holder.SetActive(false);   // a cópia nasce desativada: nenhum Awake/OnEnable roda antes da limpeza
        holder.transform.position = Distante(proximo++);
        GameObject copia = Instantiate(origem, holder.transform, false);
        copia.transform.localPosition = Vector3.zero;
        copia.transform.localRotation = Quaternion.identity;
        copia.SetActive(true);
        // scripts primeiro (de trás para frente: quem depende de outro componente costuma ter sido adicionado depois),
        // depois a física: assim nenhum [RequireComponent] impede a remoção
        MonoBehaviour[] scripts = copia.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = scripts.Length - 1; i >= 0; i--)
        {
            MonoBehaviour mb = scripts[i];
            if (mb == null) continue;
            bool fica = false;
            foreach (Type t in manter) if (t.IsInstanceOfType(mb)) { fica = true; break; }
            if (!fica) DestroyImmediate(mb);
        }
        foreach (Collider c in copia.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);
        foreach (Rigidbody b in copia.GetComponentsInChildren<Rigidbody>(true)) DestroyImmediate(b);
        raiz = holder.transform;
        holder.SetActive(true);
        CriarCamera();
        return copia.transform;
    }

    /// <summary>
    /// Ângulo de câmera: olha na direção <paramref name="olhar"/> (no espaço da cópia) enquadrando
    /// <paramref name="enquadrar"/> (bounds no espaço da cópia), com <paramref name="borda"/> de folga (fração).
    /// </summary>
    public void Enquadrar(Vector3 olhar, Vector3 cima, Bounds enquadrar, float borda = 0.08f)
    {
        olharLocal = olhar.normalized;
        cimaLocal = cima.normalized;
        alvoLocal = enquadrar;
        margem = borda;
        AjustarCamera();
    }

    /// <summary>Bounds (no espaço da cópia) dos renderers ativos dos objetos dados (null = a cópia toda).</summary>
    public Bounds BoundsLocal(params Transform[] alvos)
    {
        var b = new Bounds();
        bool tem = false;
        if (raiz == null) return b;
        Transform[] lista = alvos != null && alvos.Length > 0 ? alvos : new[] { raiz };
        foreach (Transform a in lista)
        {
            if (a == null) continue;
            foreach (Vector3 c in Cantos(a, false))
            {
                Vector3 l = raiz.InverseTransformPoint(c);
                if (!tem) { b = new Bounds(l, Vector3.zero); tem = true; } else b.Encapsulate(l);
            }
        }
        return b;
    }

    /// <summary>Posição do ponto (mundo) na RawImage (local, centro = 0,0).</summary>
    public Vector2 Posicao(Vector3 mundo)
    {
        if (cam == null) return Vector2.zero;
        Vector3 v = cam.WorldToViewportPoint(mundo);
        Rect r = ((RectTransform)transform).rect;
        return new Vector2((v.x - 0.5f) * r.width, (v.y - 0.5f) * r.height);
    }

    /// <summary>
    /// Posição do ponto (mundo) como fração da RawImage (0..1, origem embaixo à esquerda): é a ÂNCORA para pôr um
    /// elemento em cima dele (ex.: ScrewView, que volta à âncora ao animar).
    /// </summary>
    public Vector2 Ancora(Vector3 mundo)
    {
        if (cam == null) return new Vector2(0.5f, 0.5f);
        Vector3 v = cam.WorldToViewportPoint(mundo);
        return new Vector2(v.x, v.y);
    }

    /// <summary>Retângulo (centro e tamanho, local da RawImage) que cobre os renderers do objeto na tela.</summary>
    public Rect Retangulo(Transform alvo, float folga = 0f)
    {
        var min = new Vector2(float.MaxValue, float.MaxValue);
        var max = new Vector2(float.MinValue, float.MinValue);
        bool tem = false;
        if (alvo != null)
            foreach (Vector3 c in Cantos(alvo, true))
            {
                Vector2 p = Posicao(c);
                min = Vector2.Min(min, p); max = Vector2.Max(max, p); tem = true;
            }
        if (!tem) return new Rect(0f, 0f, 0f, 0f);
        return Rect.MinMaxRect(min.x - folga, min.y - folga, max.x + folga, max.y + folga);
    }

    // Cantos (mundo) das malhas do objeto e dos filhos, pela malha (vale também para peças ocultas, ex.: encaixe vazio).
    private static System.Collections.Generic.IEnumerable<Vector3> Cantos(Transform alvo, bool incluirOcultos)
    {
        foreach (MeshFilter mf in alvo.GetComponentsInChildren<MeshFilter>(incluirOcultos))
        {
            if (mf.sharedMesh == null) continue;
            Bounds mb = mf.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
                yield return mf.transform.TransformPoint(mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) * 2 - 1, (i & 2) - 1, (i & 4) / 2 - 1)));
        }
    }

    private void CriarCamera()
    {
        imagem = GetComponent<RawImage>();
        var go = new GameObject("Camera");
        go.transform.SetParent(raiz, false);
        cam = go.AddComponent<Camera>();
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = 20f;
        cam.allowHDR = false;
        cam.enabled = isActiveAndEnabled;
        imagem.color = Color.white;
        AjustarCamera();
    }

    private void AjustarCamera()
    {
        if (cam == null || raiz == null) return;
        Transform modelo = raiz;
        Vector3 olhar = modelo.TransformDirection(olharLocal);
        Vector3 cima = modelo.TransformDirection(cimaLocal);
        Vector3 centro = modelo.TransformPoint(alvoLocal.center);
        float dist = alvoLocal.extents.magnitude * 3f + 0.5f;
        cam.transform.position = centro - olhar * dist;
        cam.transform.rotation = Quaternion.LookRotation(olhar, cima);
        cam.farClipPlane = dist * 2f + 2f;
        AjustarLuz(dist);

        // tamanho ortográfico: os 8 cantos do alvo no espaço da câmera cabem na imagem (no aspecto dela)
        Vector2 ext = Vector2.zero;
        for (int i = 0; i < 8; i++)
        {
            Vector3 c = alvoLocal.center + Vector3.Scale(alvoLocal.extents, new Vector3((i & 1) * 2 - 1, (i & 2) - 1, (i & 4) / 2 - 1));
            Vector3 p = cam.transform.InverseTransformPoint(modelo.TransformPoint(c));
            Vector3 o = cam.transform.InverseTransformPoint(centro);
            ext = Vector2.Max(ext, new Vector2(Mathf.Abs(p.x - o.x), Mathf.Abs(p.y - o.y)));
        }
        Rect r = ((RectTransform)transform).rect;
        float aspecto = r.height > 1f ? r.width / r.height : (float)resolucao.x / resolucao.y;
        AjustarTextura(r);
        cam.aspect = aspecto;
        cam.orthographicSize = Mathf.Max(ext.y, ext.x / aspecto) * (1f + margem);
    }

    /// <summary>
    /// A RenderTexture tem o tamanho EM PIXELS da imagem na tela (área × escala do Canvas), na mesma proporção: com
    /// tamanho fixo ela era esticada para a área (imagem borrada e deformada). <see cref="resolucao"/> vale só enquanto
    /// o layout ainda não tem tamanho.
    /// </summary>
    private void AjustarTextura(Rect r)
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        float escala = canvas != null ? canvas.rootCanvas.scaleFactor : 1f;
        Vector2Int px = r.width > 1f && r.height > 1f
            ? new Vector2Int(Mathf.Clamp(Mathf.RoundToInt(r.width * escala), 64, 4096), Mathf.Clamp(Mathf.RoundToInt(r.height * escala), 64, 4096))
            : resolucao;
        if (rt == null || rt.width != px.x || rt.height != px.y)
        {
            if (rt != null) { if (cam != null) cam.targetTexture = null; rt.Release(); Destroy(rt); }
            rt = new RenderTexture(px.x, px.y, 24, RenderTextureFormat.ARGB32)
                 { antiAliasing = 8, name = "RT_" + name, useMipMap = false, filterMode = FilterMode.Bilinear };
            rt.Create();
        }
        cam.targetTexture = rt;
        if (imagem == null) imagem = GetComponent<RawImage>();
        imagem.texture = rt;
        imagem.uvRect = new Rect(0f, 0f, 1f, 1f);
    }

    private void AjustarLuz(float dist)
    {
        if (luzDaVista <= 0f) { if (luz != null) luz.enabled = false; return; }
        if (luz == null)
        {
            luz = new GameObject("LuzDaVista").AddComponent<Light>();
            luz.transform.SetParent(cam.transform, false);
            luz.type = LightType.Point;
            luz.shadows = LightShadows.None;
            luz.renderMode = LightRenderMode.ForcePixel;
            luz.color = Color.white;
        }
        luz.enabled = cam.enabled;
        luz.intensity = luzDaVista;
        luz.range = dist * 2f + 1f;
    }

    private void OnRectTransformDimensionsChange() => AjustarCamera();

    private void OnEnable()
    {
        if (cam != null) { cam.enabled = true; AjustarCamera(); }
        if (luz != null) luz.enabled = luzDaVista > 0f;
    }

    private void OnDisable()
    {
        if (cam != null) cam.enabled = false;
        if (luz != null) luz.enabled = false;
    }

    private void Limpar()
    {
        if (raiz != null) Destroy(raiz.gameObject);
        raiz = null;
        cam = null;
        luz = null;   // filha da câmera, destruída com a cópia
    }

    private void OnDestroy()
    {
        Limpar();
        if (rt != null) { rt.Release(); Destroy(rt); }
    }
}
