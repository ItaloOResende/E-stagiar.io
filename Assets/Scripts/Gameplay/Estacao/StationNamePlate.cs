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

    private void Start() => Refresh();

    public void Refresh()
    {
        StationEmployee e = StationEmployee.Of(this);
        foreach (TextMesh t in nameTexts) if (t != null) t.text = e != null ? e.employeeName : fallback;
        foreach (TextMesh t in departmentTexts) if (t != null) t.text = e != null ? e.department.ToUpperInvariant() : "";
    }
}
