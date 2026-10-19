using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>
/// Ícones de aplicativo do tablet, DESENHADOS COMO VETORES (sem imagem, pacote ou shader externo).
/// Cada ícone é um pequeno "SVG de texto": uma lista de formas (retângulo arredondado, círculo, anel, linha, arco,
/// polígono) num quadro de 0 a 100. Na primeira vez que o ícone é pedido, as formas viram um sprite com anti-aliasing
/// calculado pela distância exata até cada contorno, por isso as bordas ficam nítidas em qualquer tamanho.
/// O visual segue o de um app de verdade: fundo "squircle" com gradiente, brilho no alto, borda de luz e
/// sombra projetada tanto no aparelho quanto no desenho.
///
/// Para criar ou editar um ícone, mexa só no texto em <see cref="Definicoes"/>. Formato de cada linha
/// (coordenadas de 0 a 100, y cresce para baixo; a cor é #RRGGBB ou #RRGGBBAA; cor "-" APAGA o que já foi desenhado):
///   r x y w h raio cor              retângulo arredondado (x, y = canto superior esquerdo)
///   c cx cy r cor                   círculo
///   o cx cy r espessura cor         anel
///   l x1 y1 x2 y2 largura cor       linha com pontas redondas
///   a cx cy r ang0 ang1 largura cor arco em graus (0 = direita, cresce no sentido horário)
///   p x y x y x y ... cor           polígono
/// Linhas começando com // são comentários.
/// </summary>
public static class TabletIcones
{
    // id -> { cor do topo, cor da base, formas }
    private static readonly Dictionary<string, string[]> Definicoes = new Dictionary<string, string[]>
    {
        // Peças do computador: chip/processador com pinos.
        { "pecas", new[] { "#3BE3C3", "#0B7F8C", @"
// pinos do chip
l 18 37 29 37 4 #FFFFFFE6
l 18 47 29 47 4 #FFFFFFE6
l 18 57 29 57 4 #FFFFFFE6
l 18 67 29 67 4 #FFFFFFE6
l 71 37 82 37 4 #FFFFFFE6
l 71 47 82 47 4 #FFFFFFE6
l 71 57 82 57 4 #FFFFFFE6
l 71 67 82 67 4 #FFFFFFE6
l 33 18 33 29 4 #FFFFFFE6
l 43 18 43 29 4 #FFFFFFE6
l 53 18 53 29 4 #FFFFFFE6
l 63 18 63 29 4 #FFFFFFE6
l 33 71 33 82 4 #FFFFFFE6
l 43 71 43 82 4 #FFFFFFE6
l 53 71 53 82 4 #FFFFFFE6
l 63 71 63 82 4 #FFFFFFE6
// corpo e núcleo
r 26 26 48 48 9 #FFFFFF
r 36 36 28 28 5 #0B7F8C
r 43 43 14 14 3 #3BE3C3
c 31.5 31.5 2 #0B7F8C
" } },
        // Cabos e conexões: plugue com o cabo saindo.
        { "cabos", new[] { "#B197FF", "#5A3CCB", @"
// cabo saindo do plugue
l 50 58 50 66 7 #FFFFFF
a 66 66 16 90 180 7 #FFFFFF
l 66 82 80 82 7 #FFFFFF
// pinos
l 40 14 40 30 7 #FFFFFF
l 60 14 60 30 7 #FFFFFF
// corpo do plugue
r 28 28 44 32 10 #FFFFFF
r 36 36 28 6 3 #5A3CCB40
c 50 49 3.2 #5A3CCB
" } },
        // Ferramentas: chave inglesa cruzada com chave de fenda.
        { "ferramentas", new[] { "#FFC15E", "#E5541B", @"
// chave inglesa
l 63 37 28 72 11 #FFFFFF
c 28 72 3.6 #E5541B
c 71 29 16 #FFFFFF
p 68 26 90 4 97 11 74 33 -
c 71 29 5.5 -
// chave de fenda (com folga em volta)
l 18 18 52 52 11 -
l 20 20 55 55 4.6 #FFFFFF
p 14 14 18 22 22 18 #FFFFFF
l 56 56 79 79 15 #FFE6C2
l 63 63 72 72 15 #E5541B55
" } },
        // Impressora: impressora com papel entrando e saindo.
        { "impressora", new[] { "#62B6FF", "#1D55D4", @"
// papel de trás
r 32 12 36 30 3 #FFFFFF
l 39 20 61 20 3 #9DB8F2
l 39 28 55 28 3 #9DB8F2
// corpo
r 12 34 76 36 10 #DCE8FF
r 12 52 76 18 9 #B9CFF8
// papel saindo
r 28 58 44 28 3 #FFFFFF
r 24 56 52 5 2.5 #1D55D4
l 35 68 65 68 3 #9DB8F2
l 35 76 58 76 3 #9DB8F2
c 76 43 3.4 #3BD97A
r 20 41 22 5 2.5 #8FA9DD
" } },
        // Notebook: laptop com um terminal na tela.
        { "notebook", new[] { "#6E7F99", "#262E3C", @"
// tampa
r 21 19 58 43 6 #EAF0F9
r 26 24 48 33 3 #12263D
// prompt > _
l 33 33 40 39 3.4 #5ED0FF
l 40 39 33 45 3.4 #5ED0FF
l 45 46 58 46 3.4 #5ED0FF
c 62 32 2.2 #3BD97A
c 68 32 2.2 #FFC15E
// base
r 9 61 82 10 5 #EAF0F9
r 40 61 20 4 2 #AEBBD0
" } },
        // Rede e rack: rack com switch, servidor e patch panel.
        { "rede", new[] { "#7DEB86", "#1B9650", @"
// montantes
r 12 12 5 76 2.5 #FFFFFFB0
r 83 12 5 76 2.5 #FFFFFFB0
// unidade 1: switch com portas
r 21 17 58 20 4 #FFFFFF
r 27 23 7 8 1.5 #1B9650
r 37 23 7 8 1.5 #1B9650
r 47 23 7 8 1.5 #1B9650
r 57 23 7 8 1.5 #1B9650
c 72 27 2.6 #FFB347
// unidade 2: servidor
r 21 40 58 20 4 #FFFFFF
l 27 46 52 46 3 #1B9650
l 27 54 44 54 3 #1B965080
c 66 50 2.6 #3BD97A
c 73 50 2.6 #3BD97A
// unidade 3: patch panel
r 21 63 58 20 4 #FFFFFF
r 27 69 5 8 1.2 #1B9650
r 34 69 5 8 1.2 #1B9650
r 41 69 5 8 1.2 #1B9650
r 48 69 5 8 1.2 #1B9650
r 55 69 5 8 1.2 #1B9650
r 62 69 5 8 1.2 #1B9650
r 69 69 5 8 1.2 #1B9650
" } },
    };

    /// <summary>Existe um ícone vetorial com este id?</summary>
    public static bool Existe(string id) { return id != null && Definicoes.ContainsKey(id); }

    /// <summary>
    /// Desenha o ícone e devolve um Sprite novo (quem chamou é dono dele: destrua o sprite e a textura quando não precisar).
    /// Devolve null se o id não existir.
    /// </summary>
    public static Sprite Criar(string id, int tamanho = 192)
    {
        if (!Existe(id)) return null;
        string[] def = Definicoes[id];
        tamanho = Mathf.Clamp(tamanho, 32, 512);
        List<Forma> formas = Interpretar(def[2]);
        Color topo = LerCor(def[0], Color.white), baseCor = LerCor(def[1], Color.gray);
        float k = 100f / tamanho;   // tamanho de um pixel no quadro 0..100
        var pixels = new Color32[tamanho * tamanho];

        for (int y = 0; y < tamanho; y++)
        {
            float vy = (tamanho - y - .5f) * k;   // a textura começa embaixo; o desenho começa em cima
            for (int x = 0; x < tamanho; x++)
            {
                float vx = (x + .5f) * k;
                float r = 0f, g = 0f, b = 0f;
                // sombra do aparelho (por baixo de tudo)
                float a = .38f * Mathf.Clamp01(.5f - SuperElipse(vx, vy - 2.5f) / 4.5f);
                float dFundo = SuperElipse(vx, vy);
                float cob = Mathf.Clamp01(.5f - dFundo / k);
                if (cob > 0f)
                {
                    // fundo com gradiente vertical
                    Color cor = Color.Lerp(topo, baseCor, Mathf.Clamp01((vy - 6f) / 88f));
                    Sobre(ref r, ref g, ref b, ref a, cor.r * cob, cor.g * cob, cor.b * cob, cob);
                    // brilho no alto, borda de luz e leve sombreado embaixo
                    float brilho = .22f * (1f - Suave(6f, 54f, vy)) * cob;
                    Sobre(ref r, ref g, ref b, ref a, brilho, brilho, brilho, brilho);
                    float borda = .28f * Mathf.Clamp01((dFundo + 1.6f) / 1.6f) * cob * (1f - Suave(6f, 60f, vy));
                    Sobre(ref r, ref g, ref b, ref a, borda, borda, borda, borda);
                    float escuro = .10f * Suave(62f, 94f, vy) * cob;
                    Sobre(ref r, ref g, ref b, ref a, 0f, 0f, 0f, escuro);

                    // sombra projetada do desenho (a silhueta de todas as formas, deslocada para baixo e suavizada)
                    float dUniao = 1000f;
                    for (int i = 0; i < formas.Count; i++)
                        if (!formas[i].Apagar && !Fora(formas[i], vx, vy - 2.2f, 2f)) dUniao = Mathf.Min(dUniao, Distancia(formas[i], vx, vy - 2.2f));
                    float sombra = .30f * Mathf.Clamp01(.5f - dUniao / 3.2f) * cob;
                    Sobre(ref r, ref g, ref b, ref a, 0f, 0f, 0f, sombra);

                    // o desenho, numa camada própria (para o "apagar" recortar só o desenho, não o fundo)
                    float lr = 0f, lg = 0f, lb = 0f, la = 0f;
                    for (int i = 0; i < formas.Count; i++)
                    {
                        Forma f = formas[i];
                        if (Fora(f, vx, vy, 1f)) continue;   // longe demais para tocar este pixel
                        float c = Mathf.Clamp01(.5f - Distancia(f, vx, vy) / k);
                        if (f.Apagar)
                        {
                            float m = 1f - c;
                            lr *= m; lg *= m; lb *= m; la *= m;
                        }
                        else
                        {
                            float al = c * f.Cor.a, m = 1f - al;
                            lr = f.Cor.r * al + lr * m; lg = f.Cor.g * al + lg * m; lb = f.Cor.b * al + lb * m; la = al + la * m;
                        }
                    }
                    Sobre(ref r, ref g, ref b, ref a, lr * cob, lg * cob, lb * cob, la * cob);
                }
                float inv = a > 1e-5f ? 1f / a : 0f;   // de alfa pré-multiplicado para alfa direto
                pixels[y * tamanho + x] = new Color32(Byte(r * inv), Byte(g * inv), Byte(b * inv), Byte(a));
            }
        }

        var textura = new Texture2D(tamanho, tamanho, TextureFormat.RGBA32, true)
        { name = "Tablet_Icone_" + id, hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        textura.SetPixels32(pixels);
        textura.Apply(true, true);
        var sprite = Sprite.Create(textura, new Rect(0, 0, tamanho, tamanho), Vector2.one * .5f, 100f, 0, SpriteMeshType.FullRect);
        sprite.name = "Tablet_Icone_" + id; sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    // ------------------------------------------------------------------ formas

    private sealed class Forma { public char Tipo; public float[] N; public Color Cor; public bool Apagar; public float X0, Y0, X1, Y1; }

    private static List<Forma> Interpretar(string vetor)
    {
        var lista = new List<Forma>();
        foreach (string bruto in vetor.Split('\n'))
        {
            string linha = bruto.Trim();
            if (linha.Length == 0 || linha.StartsWith("//")) continue;
            string[] t = linha.Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (t.Length < 3) continue;
            var f = new Forma { Tipo = t[0][0] };
            string cor = t[t.Length - 1];
            if (cor == "-") f.Apagar = true; else f.Cor = LerCor(cor, Color.magenta);
            var n = new float[t.Length - 2];
            bool ok = true;
            for (int i = 1; i < t.Length - 1; i++)
                if (!float.TryParse(t[i], NumberStyles.Float, CultureInfo.InvariantCulture, out n[i - 1])) ok = false;
            f.N = n;
            int minimo;
            switch (f.Tipo)
            {
                case 'r': minimo = 5; break;
                case 'c': minimo = 3; break;
                case 'o': minimo = 4; break;
                case 'l': minimo = 5; break;
                case 'a': minimo = 6; break;
                case 'p': minimo = 6; break;
                default: minimo = int.MaxValue; break;
            }
            if (!ok || n.Length < minimo || (f.Tipo == 'p' && n.Length % 2 != 0)) continue;
            CalcularCaixa(f);
            lista.Add(f);
        }
        return lista;
    }

    /// <summary>Retângulo que contém a forma: fora dele (e de uma margem) a forma não tem efeito, então nem é calculada.</summary>
    private static void CalcularCaixa(Forma f)
    {
        float[] n = f.N;
        switch (f.Tipo)
        {
            case 'r': f.X0 = n[0]; f.Y0 = n[1]; f.X1 = n[0] + n[2]; f.Y1 = n[1] + n[3]; break;
            case 'c': f.X0 = n[0] - n[2]; f.Y0 = n[1] - n[2]; f.X1 = n[0] + n[2]; f.Y1 = n[1] + n[2]; break;
            case 'o': { float m = n[2] + n[3] * .5f; f.X0 = n[0] - m; f.Y0 = n[1] - m; f.X1 = n[0] + m; f.Y1 = n[1] + m; break; }
            case 'a': { float m = n[2] + n[5] * .5f; f.X0 = n[0] - m; f.Y0 = n[1] - m; f.X1 = n[0] + m; f.Y1 = n[1] + m; break; }
            case 'l':
            {
                float m = n[4] * .5f;
                f.X0 = Mathf.Min(n[0], n[2]) - m; f.Y0 = Mathf.Min(n[1], n[3]) - m;
                f.X1 = Mathf.Max(n[0], n[2]) + m; f.Y1 = Mathf.Max(n[1], n[3]) + m;
                break;
            }
            default:
                f.X0 = f.Y0 = float.MaxValue; f.X1 = f.Y1 = float.MinValue;
                for (int i = 0; i + 1 < n.Length; i += 2)
                {
                    f.X0 = Mathf.Min(f.X0, n[i]); f.X1 = Mathf.Max(f.X1, n[i]);
                    f.Y0 = Mathf.Min(f.Y0, n[i + 1]); f.Y1 = Mathf.Max(f.Y1, n[i + 1]);
                }
                break;
        }
    }
    private static bool Fora(Forma f, float x, float y, float margem)
    { return x < f.X0 - margem || x > f.X1 + margem || y < f.Y0 - margem || y > f.Y1 + margem; }

    private static Color LerCor(string hex, Color padrao)
    {
        Color cor;
        return ColorUtility.TryParseHtmlString(hex, out cor) ? cor : padrao;
    }

    /// <summary>Distância com sinal até o contorno da forma: negativa dentro, positiva fora.</summary>
    private static float Distancia(Forma f, float x, float y)
    {
        float[] n = f.N;
        switch (f.Tipo)
        {
            case 'r':
            {
                float mw = n[2] * .5f, mh = n[3] * .5f, raio = Mathf.Min(n[4], Mathf.Min(mw, mh));
                float qx = Mathf.Abs(x - (n[0] + mw)) - (mw - raio), qy = Mathf.Abs(y - (n[1] + mh)) - (mh - raio);
                float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
                return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - raio;
            }
            case 'c':
                return Mathf.Sqrt((x - n[0]) * (x - n[0]) + (y - n[1]) * (y - n[1])) - n[2];
            case 'o':
                return Mathf.Abs(Mathf.Sqrt((x - n[0]) * (x - n[0]) + (y - n[1]) * (y - n[1])) - n[2]) - n[3] * .5f;
            case 'l':
            {
                float ex = n[2] - n[0], ey = n[3] - n[1], wx = x - n[0], wy = y - n[1];
                float t = Mathf.Clamp01((wx * ex + wy * ey) / (ex * ex + ey * ey + 1e-6f));
                float bx = wx - ex * t, by = wy - ey * t;
                return Mathf.Sqrt(bx * bx + by * by) - n[4] * .5f;
            }
            case 'a':
            {
                float px = x - n[0], py = y - n[1], raio = n[2];
                float angulo = Mathf.Atan2(py, px) * Mathf.Rad2Deg;
                float relativo = Mathf.Repeat(angulo - n[3], 360f);
                float d;
                if (relativo <= n[4] - n[3]) d = Mathf.Abs(Mathf.Sqrt(px * px + py * py) - raio);
                else
                {
                    float a0 = n[3] * Mathf.Deg2Rad, a1 = n[4] * Mathf.Deg2Rad;
                    float d0x = x - (n[0] + raio * Mathf.Cos(a0)), d0y = y - (n[1] + raio * Mathf.Sin(a0));
                    float d1x = x - (n[0] + raio * Mathf.Cos(a1)), d1y = y - (n[1] + raio * Mathf.Sin(a1));
                    d = Mathf.Sqrt(Mathf.Min(d0x * d0x + d0y * d0y, d1x * d1x + d1y * d1y));
                }
                return d - n[5] * .5f;
            }
            case 'p':
            {
                int cnt = n.Length / 2;
                float d = (x - n[0]) * (x - n[0]) + (y - n[1]) * (y - n[1]), s = 1f;
                for (int i = 0, j = cnt - 1; i < cnt; j = i, i++)
                {
                    float ex = n[j * 2] - n[i * 2], ey = n[j * 2 + 1] - n[i * 2 + 1];
                    float wx = x - n[i * 2], wy = y - n[i * 2 + 1];
                    float t = Mathf.Clamp01((wx * ex + wy * ey) / (ex * ex + ey * ey + 1e-6f));
                    float bx = wx - ex * t, by = wy - ey * t;
                    d = Mathf.Min(d, bx * bx + by * by);
                    bool c1 = y >= n[i * 2 + 1], c2 = y < n[j * 2 + 1], c3 = ex * wy > ey * wx;
                    if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) s = -s;
                }
                return s * Mathf.Sqrt(d);
            }
        }
        return 1000f;
    }

    // ------------------------------------------------------------------ utilidades

    /// <summary>Contorno "squircle" (curva dos ícones de aplicativo) centrado em 50,50, com meia-largura de 44.</summary>
    private static float SuperElipse(float x, float y)
    {
        float ax = Mathf.Abs(x - 50f) / 44f, ay = Mathf.Abs(y - 50f) / 44f;
        float ax5 = ax * ax * ax * ax * ax, ay5 = ay * ay * ay * ay * ay;
        return (Mathf.Pow(ax5 + ay5, .2f) - 1f) * 44f;
    }

    private static float Suave(float de, float ate, float v)
    {
        float t = Mathf.Clamp01((v - de) / (ate - de));
        return t * t * (3f - 2f * t);
    }

    /// <summary>Composição "source-over" com cores pré-multiplicadas.</summary>
    private static void Sobre(ref float r, ref float g, ref float b, ref float a, float sr, float sg, float sb, float sa)
    {
        float m = 1f - sa;
        r = sr + r * m; g = sg + g * m; b = sb + b * m; a = sa + a * m;
    }

    private static byte Byte(float v) { return (byte)(Mathf.Clamp01(v) * 255f + .5f); }
}
