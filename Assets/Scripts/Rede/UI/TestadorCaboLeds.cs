using UnityEngine;

/// <summary>
/// LEDs do modelo 3D do testador de cabos. A tela 2D (<see cref="TestadorCaboScreen"/>) acende os mesmos LEDs aqui
/// durante o teste. Troca só a cor por <see cref="MaterialPropertyBlock"/> (o material compartilhado não muda).
/// </summary>
public class TestadorCaboLeds : MonoBehaviour
{
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int Shade1 = Shader.PropertyToID("_1st_ShadeColor");
    private static readonly int Shade2 = Shader.PropertyToID("_2nd_ShadeColor");
    private static readonly int Emissive = Shader.PropertyToID("_Emissive_Color");

    [Tooltip("LEDs 1..8 da unidade principal.")]
    [SerializeField] private Renderer[] principal = new Renderer[8];
    [Tooltip("LEDs 1..8 da unidade remota.")]
    [SerializeField] private Renderer[] remota = new Renderer[8];
    [SerializeField] private Color apagado = new Color(0.16f, 0.2f, 0.17f);

    private MaterialPropertyBlock block;

    public void Configurar(Renderer[] ledsPrincipal, Renderer[] ledsRemota)
    {
        principal = ledsPrincipal;
        remota = ledsRemota;
    }

    private void OnEnable() => Apagar();

    public void Apagar()
    {
        for (int i = 0; i < 8; i++) { Acender(false, i, apagado, false); Acender(true, i, apagado, false); }
    }

    /// <summary>Pinta o LED <paramref name="pino"/> (0..7) da unidade remota ou da principal.</summary>
    public void Acender(bool naRemota, int pino, Color cor, bool aceso = true)
    {
        Renderer[] leds = naRemota ? remota : principal;
        if (leds == null || pino < 0 || pino >= leds.Length || leds[pino] == null) return;
        block ??= new MaterialPropertyBlock();
        leds[pino].GetPropertyBlock(block);
        block.SetColor(BaseColor, cor);
        block.SetColor(Shade1, cor * 0.85f);
        block.SetColor(Shade2, cor * 0.7f);
        block.SetColor(Emissive, aceso ? cor : Color.black);
        leds[pino].SetPropertyBlock(block);
    }
}
