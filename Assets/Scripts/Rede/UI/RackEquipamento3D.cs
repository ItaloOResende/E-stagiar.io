using EstagiarIo.Rede;
using UnityEngine;

/// <summary>
/// Equipamentos 3D dentro do rack (switch, patch panel) refletindo o estado real da rede (<see cref="RedeGame"/>):
///  - <see cref="cabos"/>[n-1] (patch cord da porta n do patch panel) aparece quando a porta P n tem um cabo ligado;
///  - <see cref="leds"/>[n-1] (LED da porta n do switch) fica verde quando o par n funciona (P n ↔ S n, cabo bom, sem
///    laço), âmbar quando há cabo no switch mas sem rede, apagado sem cabo.
/// Só apresentação (sem colisores): a interação continua no root do rack (<c>UIScreenInteractable</c> → UI_Screen_Rack).
/// Vai no objeto dos equipamentos internos (filho do RackInternet).
/// </summary>
public class RackEquipamento3D : MonoBehaviour
{
    [SerializeField] private GameObject[] cabos = new GameObject[0];
    [SerializeField] private Renderer[] leds = new Renderer[0];
    [SerializeField] private Color ledOk = new Color(0.2f, 1f, 0.35f);
    [SerializeField] private Color ledSemRede = new Color(1f, 0.65f, 0.1f);
    [SerializeField] private Color ledApagado = new Color(0.08f, 0.09f, 0.08f);

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
    private MaterialPropertyBlock block;
    private RedeGame rede;

    private void OnEnable()
    {
        rede = RedeGame.Garantir();
        rede.Mudou += Atualizar;
        Atualizar();
    }

    private void OnDisable()
    {
        if (rede != null) rede.Mudou -= Atualizar;
    }

    /// <summary>Relê o rack (chamado sozinho quando a rede muda).</summary>
    public void Atualizar()
    {
        if (rede == null) return;
        RedeRackTopologia topo = rede.Topologia;
        block ??= new MaterialPropertyBlock();
        for (int i = 0; i < cabos.Length; i++)
        {
            int n = i + 1;
            bool valida = topo.Valida(new PortaRack(true, n));
            if (cabos[i] != null) cabos[i].SetActive(valida && topo.Ocupada(new PortaRack(true, n)));
        }
        for (int i = 0; i < leds.Length; i++)
        {
            if (leds[i] == null) continue;
            int n = i + 1;
            bool valida = topo.Valida(new PortaRack(false, n));
            Color c = !valida ? ledApagado : rede.PortaFunciona(n) ? ledOk : topo.Ocupada(new PortaRack(false, n)) ? ledSemRede : ledApagado;
            leds[i].GetPropertyBlock(block);
            block.SetColor(BaseColorId, c);
            block.SetColor(EmissionId, c * 1.5f);
            leds[i].SetPropertyBlock(block);
        }
    }
}
