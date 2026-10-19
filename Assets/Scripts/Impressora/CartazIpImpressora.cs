using UnityEngine;

/// <summary>
/// Cartaz da sala de TI com o endereço IP que a impressora deve usar nesta rodada (<see cref="ImpressoraRede"/>):
/// o mesmo valor que o painel da impressora aceita. Escreve no <see cref="TextMesh"/> do IP sempre que a rodada muda.
/// Vai no root do cartaz; o texto fixo (título e instrução) fica nos outros TextMesh do próprio objeto.
/// </summary>
public class CartazIpImpressora : MonoBehaviour
{
    [Tooltip("Texto que mostra o IP da rodada.")]
    [SerializeField] private TextMesh textoIp;
    [Tooltip("Mostrado enquanto a rodada ainda não definiu o IP.")]
    [SerializeField] private string semIp = "---.---.---.---";

    private void OnEnable() { ImpressoraRede.Mudou += Mostrar; Mostrar(ImpressoraRede.IpDaRodada); }
    private void OnDisable() => ImpressoraRede.Mudou -= Mostrar;
    private void Start() => Mostrar(ImpressoraRede.IpDaRodada);

    private void Mostrar(string ip)
    {
        if (textoIp != null) textoIp.text = string.IsNullOrEmpty(ip) ? semIp : ip;
    }
}
