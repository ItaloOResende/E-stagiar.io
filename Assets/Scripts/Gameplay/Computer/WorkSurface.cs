using UnityEngine;

/// <summary>
/// Marca um móvel como superfície de trabalho (mesa, bancada): o gabinete só pode ser ABERTO apoiado sobre um
/// collider deste objeto ou de um filho dele (<see cref="ComputerCase.IsOnWorkSurface"/>). Vai no root lógico do móvel,
/// junto do collider do tampo. Cadastrar uma mesa nova = adicionar este componente; a mecânica não muda.
/// Só marca: não depende de nome, tag, altura nem do modelo 3D.
/// </summary>
[DisallowMultipleComponent]
public class WorkSurface : MonoBehaviour
{
    [Tooltip("Desligado = o móvel continua na cena, mas deixa de valer como superfície para abrir o gabinete.")]
    [SerializeField] private bool allowsCaseMaintenance = true;

    public bool AllowsCaseMaintenance
    {
        get => allowsCaseMaintenance && enabled && gameObject.activeInHierarchy;
        set => allowsCaseMaintenance = value;
    }
}
