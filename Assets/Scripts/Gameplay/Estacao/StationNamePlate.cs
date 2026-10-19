using UnityEngine;

/// <summary>
/// Plaquinha de mesa (prefab <c>Placa_Mesa</c>): escreve nos <see cref="TextMesh"/> (as duas faces) o nome do
/// <see cref="StationEmployee"/> da estação em que está (procura subindo na hierarquia). Sem estação, mostra
/// <see cref="fallback"/>. O nome vem sempre do funcionário: mudar o nome na estação muda a placa.
/// </summary>
public class StationNamePlate : MonoBehaviour
{
    [Tooltip("Textos do nome (um por face da placa).")]
    [SerializeField] private TextMesh[] nameTexts = new TextMesh[0];
    [SerializeField] private TextMesh[] departmentTexts = new TextMesh[0];
    [SerializeField] private string fallback = "";
    [Tooltip("Renderers do detalhe colorido (faixas). Vazio = os filhos chamados \"Faixa\". A cor vem de " +
             "StationEmployee.corDaPlaca, por MaterialPropertyBlock (o material compartilhado não muda).")]
    [SerializeField] private Renderer[] faixas = new Renderer[0];

    private static readonly int MainColor = Shader.PropertyToID("_MainColor");   // RealToon
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");   // URP/Lit e Toon

    private void Start() => Refresh();

    public void Refresh()
    {
        StationEmployee e = StationEmployee.Of(this);
        foreach (TextMesh t in nameTexts) if (t != null) t.text = e != null ? e.DisplayName : fallback;
        foreach (TextMesh t in departmentTexts) if (t != null) t.text = e != null && e.Occupied ? e.department.ToUpperInvariant() : "";
        if (e != null && e.corDaPlaca.a > 0f) Pintar(e.corDaPlaca);
    }

    private void Pintar(Color cor)
    {
        Renderer[] alvos = faixas != null && faixas.Length > 0 ? faixas
            : System.Array.FindAll(GetComponentsInChildren<Renderer>(true), r => r.name == "Faixa");
        var bloco = new MaterialPropertyBlock();
        foreach (Renderer r in alvos)
        {
            if (r == null) continue;
            r.GetPropertyBlock(bloco);
            bloco.SetColor(MainColor, cor);
            bloco.SetColor(BaseColor, cor);
            r.SetPropertyBlock(bloco);
        }
    }
}
