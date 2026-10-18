using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Visor do painel 3D da impressora (no modelo do mundo), como a <c>Tela</c> do monitor (<see cref="MonitorDisplay"/>):
/// mostra AO VIVO o estado do <see cref="ImpressoraEstacao"/> do objeto (o mesmo que a tela 2D do Ítalo comanda) —
/// o que falta (toner, papel, IP) ou "PRONTA", e o IP confirmado. Vai no filho <c>Painel</c> do root lógico da
/// impressora, junto do <see cref="ImpressoraPainelInteractable"/> (clicar no painel abre a tela no teclado do IP).
/// Só apresentação: não guarda estado e não depende de nome, tag nem do modelo 3D.
/// </summary>
public class ImpressoraPainel3D : MonoBehaviour
{
    [Tooltip("Estado da impressora. Vazio = o ImpressoraEstacao do pai.")]
    [SerializeField] private ImpressoraEstacao estacao;
    [Tooltip("Linha de cima do visor (status).")]
    [SerializeField] private Text statusText;
    [Tooltip("Linha de baixo do visor (IP).")]
    [SerializeField] private Text ipText;
    [Tooltip("LED de status (opcional): verde quando pronta, âmbar quando falta algo.")]
    [SerializeField] private Renderer led;
    [SerializeField] private Color corPronta = new Color(0.36f, 0.95f, 0.45f);
    [SerializeField] private Color corPendente = new Color(1f, 0.72f, 0.2f);

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
    private MaterialPropertyBlock block;
    private string ultimoStatus, ultimoIp;

    public ImpressoraEstacao Estacao => estacao;

    private void Awake()
    {
        if (estacao == null) estacao = GetComponentInParent<ImpressoraEstacao>();
    }

    private void OnEnable() => Atualizar(true);

    private void Update() => Atualizar(false);

    /// <summary>Texto do status para uma sessão (público para testes).</summary>
    public static string Status(ImpressoraSession s)
    {
        if (s == null) return "";
        // Aviso genérico: o painel não diz o que está faltando (o técnico investiga a impressora).
        return s.Pronta ? "PRONTA" : "ERRO - VERIFICAR";
    }

    /// <summary>Linha do IP para uma sessão (público para testes).</summary>
    public static string LinhaIp(ImpressoraSession s) =>
        s != null && s.TemIp ? "IP " + s.IpSalvo : "IP ---.---.---.---";

    private void Atualizar(bool forcar)
    {
        ImpressoraSession s = estacao != null ? estacao.Sessao : null;
        string status = Status(s), ip = LinhaIp(s);
        if (!forcar && status == ultimoStatus && ip == ultimoIp) return;
        ultimoStatus = status;
        ultimoIp = ip;

        bool pronta = s != null && s.Pronta;
        if (statusText != null)
        {
            statusText.text = status;
            statusText.color = pronta ? corPronta : corPendente;
        }
        if (ipText != null) ipText.text = ip;
        if (led != null)
        {
            block ??= new MaterialPropertyBlock();
            led.GetPropertyBlock(block);
            Color c = pronta ? corPronta : corPendente;
            block.SetColor(BaseColorId, c);
            block.SetColor(EmissionId, c * 1.5f);
            led.SetPropertyBlock(block);
        }
    }
}
