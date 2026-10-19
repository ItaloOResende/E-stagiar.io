using UnityEngine;

/// <summary>
/// NPC sentado numa cadeira (os modelos do Matheus não têm pernas): no Start, desce ou sobe o modelo para que a parte
/// mais baixa dele fique na altura do assento. Vai no ROOT do modelo, que fica posicionado sobre a cadeira e virado
/// para a mesa. Funciona com qualquer pivô do FBX (usa os bounds dos renderers). Sem NavMesh, sem animação.
/// </summary>
[DisallowMultipleComponent]
public class NpcSentado : MonoBehaviour
{
    [Tooltip("Altura do assento (m, no mundo). A base do modelo é colocada aqui.")]
    [SerializeField] private float alturaAssento = 0.48f;

    public float AlturaAssento { get => alturaAssento; set => alturaAssento = value; }

    private void Start() => Assentar();

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
