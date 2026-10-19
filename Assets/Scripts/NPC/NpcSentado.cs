using UnityEngine;

/// <summary>
/// NPC sentado numa cadeira (os modelos do Matheus não têm pernas): no Start, desce ou sobe o modelo para que a parte
/// mais baixa dele fique na altura do assento. Vai no ROOT do modelo, que fica posicionado sobre a cadeira e virado
/// para a mesa. Funciona com qualquer pivô do FBX (usa os bounds dos renderers). Sem NavMesh, sem animação.
/// Também cria um colisor do corpo (<c>Colisor_&lt;nome&gt;</c>, irmão do modelo, sem a escala/rotação do FBX) para o
/// jogador não atravessar o NPC sentado.
/// </summary>
[DisallowMultipleComponent]
public class NpcSentado : MonoBehaviour
{
    [Tooltip("Altura do assento (m, no mundo). A base do modelo é colocada aqui.")]
    [SerializeField] private float alturaAssento = 0.48f;

    public float AlturaAssento { get => alturaAssento; set => alturaAssento = value; }

    [Tooltip("Cria o colisor do corpo (cápsula do tamanho do modelo sentado).")]
    [SerializeField] private bool criarColisor = true;

    private CapsuleCollider colisor;

    private void Start()
    {
        Assentar();
        if (criarColisor) CriarColisor();
    }

    // Cápsula vertical nos limites do modelo, num objeto à parte: o modelo do FBX tem escala negativa e rotação de
    // 270° em X, e um colisor nele ficaria torto (ou com aviso de escala negativa).
    private void CriarColisor()
    {
        Renderer[] rs = GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return;
        Bounds b = rs[0].bounds;
        foreach (Renderer r in rs) b.Encapsulate(r.bounds);
        if (colisor == null)
        {
            var go = new GameObject("Colisor_" + name);
            go.transform.SetParent(transform.parent, false);
            go.layer = gameObject.layer;
            colisor = go.AddComponent<CapsuleCollider>();
        }
        colisor.transform.position = new Vector3(b.center.x, b.min.y, b.center.z);
        colisor.transform.rotation = Quaternion.identity;
        // o tronco (não os braços abertos do modelo): raio pela menor medida horizontal
        colisor.radius = Mathf.Clamp(Mathf.Min(b.size.x, b.size.z) * 0.3f, 0.2f, 0.35f);
        colisor.height = Mathf.Max(b.size.y, colisor.radius * 2f);
        colisor.center = new Vector3(0f, colisor.height / 2f, 0f);
    }

    /// <summary>Encosta a base do modelo no assento (pode ser chamado de novo depois de mover o NPC).</summary>
    public void Assentar()
    {
        Renderer[] rs = GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return;
        float baseY = float.MaxValue;
        foreach (Renderer r in rs) if (r.enabled) baseY = Mathf.Min(baseY, r.bounds.min.y);
        if (baseY == float.MaxValue) return;
        transform.position += Vector3.up * (alturaAssento - baseY);
    }
}
