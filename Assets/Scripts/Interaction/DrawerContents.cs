using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Conteúdo configurável (pelo Inspector) de uma gaveta/armário com <see cref="OpenableInteractable"/>.
/// Cada entrada define o prefab (um <see cref="PickupInteractable"/>), a quantidade, onde aparece e se fica
/// fisicamente na gaveta ou é entregue direto à mão ao abrir.
///
/// Regras (sem duplicação):
///  - Itens físicos são criados UMA vez (Awake) e acompanham a gaveta. Ficam ocultos com ela fechada.
///  - Quando o jogador pega um item, ele sai do conteúdo: abrir de novo não cria cópias.
///  - Guardar de volta: com a gaveta aberta, soltar (mirando a área de guardar) um item do MESMO tipo de algum
///    dos itens desta gaveta (<see cref="ItemKind"/>) acomoda-o dentro dela, no espaço livre (<see cref="TryStore"/>).
///  - Entregas diretas consomem o estoque (1 por abertura, se a mão estiver livre).
///  - Gaveta sem nada restante abre/fecha normalmente (vazia) e dispara <see cref="onEmptied"/> uma vez.
/// </summary>
[RequireComponent(typeof(OpenableInteractable))]
public class DrawerContents : MonoBehaviour
{
    public enum Delivery { PhysicalInDrawer, DirectToHand }

    [Serializable]
    public class Entry
    {
        [Tooltip("Prefab com PickupInteractable (root lógico + Visual).")]
        public PickupInteractable prefab;
        [Min(1)] public int quantity = 1;
        public Delivery delivery = Delivery.PhysicalInDrawer;
        [Tooltip("Posição da 1ª unidade, no espaço local da parte móvel da gaveta (só PhysicalInDrawer).")]
        public Vector3 localPosition = new Vector3(0f, 0.1f, 0.15f);
        public Vector3 localEuler = Vector3.zero;
        [Tooltip("Deslocamento entre unidades (só PhysicalInDrawer), no espaço local da gaveta.")]
        public Vector3 step = new Vector3(0.15f, 0f, 0f);
    }

    [SerializeField] private List<Entry> entries = new List<Entry>();
    [Tooltip("Itens físicos só aparecem com a gaveta aberta (o modelo provisório é maciço).")]
    [SerializeField] private bool hideContentsWhenClosed = true;
    [Tooltip("Gatilho (BoxCollider 'Is Trigger', filho da parte móvel) com o espaço interno onde itens são guardados. " +
             "Vazio = esta gaveta não aceita guardar.")]
    [SerializeField] private BoxCollider storageArea;
    public UnityEvent onEmptied;

    private OpenableInteractable openable;
    private readonly List<PickupInteractable> inside = new List<PickupInteractable>();
    private int[] directRemaining;
    private bool emptiedRaised;

    public int RemainingCount
    {
        get
        {
            int n = inside.Count;
            if (directRemaining != null) foreach (int d in directRemaining) n += d;
            return n;
        }
    }

    public bool IsEmpty => RemainingCount == 0;
    public BoxCollider StorageArea => storageArea;
    /// <summary>Aberta e com área de guardar: dá para guardar itens agora.</summary>
    public bool IsOpenForStorage => storageArea != null && openable != null && openable.IsOpen;
    /// <summary>Itens guardados agora (criados aqui ou devolvidos pelo jogador).</summary>
    public IReadOnlyList<PickupInteractable> Inside => inside;

    private void Awake()
    {
        openable = GetComponent<OpenableInteractable>();
        directRemaining = new int[entries.Count];

        for (int e = 0; e < entries.Count; e++)
        {
            Entry entry = entries[e];
            if (entry.prefab == null) continue;

            if (entry.delivery == Delivery.DirectToHand)
            {
                directRemaining[e] = entry.quantity;
                continue;
            }

            Transform parent = openable.MovingPart;
            for (int i = 0; i < entry.quantity; i++)
            {
                Vector3 pos = parent.TransformPoint(entry.localPosition + entry.step * i);
                Quaternion rot = parent.rotation * Quaternion.Euler(entry.localEuler);
                PickupInteractable item = Instantiate(entry.prefab, pos, rot, parent);
                item.PickedUp += OnItemTaken;
                item.MarkStored(this);
                inside.Add(item);
            }
        }

        SetContentsVisible(openable.IsOpen || !hideContentsWhenClosed);
    }

    private void OnEnable()
    {
        openable = GetComponent<OpenableInteractable>();
        openable.Opened += OnOpened;
        openable.OpenedBy += OnOpenedBy;
        openable.FinishedClosing += OnFinishedClosing;
    }

    private void OnDisable()
    {
        openable.Opened -= OnOpened;
        openable.OpenedBy -= OnOpenedBy;
        openable.FinishedClosing -= OnFinishedClosing;
    }

    private void OnOpened() => SetContentsVisible(true);

    private void OnFinishedClosing()
    {
        if (hideContentsWhenClosed) SetContentsVisible(false);
    }

    private void SetContentsVisible(bool visible)
    {
        foreach (PickupInteractable item in inside)
            if (item != null && item.gameObject.activeSelf != visible) item.gameObject.SetActive(visible);
    }

    private void OnItemTaken(PickupInteractable item)
    {
        item.PickedUp -= OnItemTaken;
        inside.Remove(item); // fica no mundo; volta ao conteúdo só se o jogador guardar de novo (TryStore)
        CheckEmptied();
    }

    /// <summary>A gaveta guarda itens deste tipo (algum item configurado nela é do mesmo <see cref="ItemKind"/>).</summary>
    public bool Accepts(PickupInteractable item)
    {
        string kind = ItemKind.Of(item);
        if (kind == null) return false;
        foreach (Entry e in entries)
            if (e.prefab != null && ItemKind.Of(e.prefab) == kind) return true;
        return false;
    }

    /// <summary>O ponto (mundo) está dentro do espaço de guardar.</summary>
    public bool StorageContains(Vector3 worldPoint)
    {
        if (storageArea == null) return false;
        Vector3 p = storageArea.transform.InverseTransformPoint(worldPoint) - storageArea.center;
        Vector3 h = storageArea.size * 0.5f + Vector3.one * 0.01f;
        return Mathf.Abs(p.x) <= h.x && Mathf.Abs(p.y) <= h.y && Mathf.Abs(p.z) <= h.z;
    }

    /// <summary>
    /// Guarda <paramref name="item"/> (vindo da mão) dentro da gaveta: deitado no fundo, no espaço livre mais perto
    /// de <paramref name="aimPoint"/>, sem sobrepor os outros itens nem sair pelas paredes. Falso (nada muda) se a
    /// gaveta estiver fechada, não aceitar o tipo do item ou não houver espaço.
    /// </summary>
    public bool TryStore(PickupInteractable item, Vector3 aimPoint)
    {
        if (item == null || !IsOpenForStorage || !Accepts(item)) return false;
        Transform area = storageArea.transform;
        Transform parent = openable.MovingPart;
        Quaternion rot = parent.rotation * Quaternion.Euler(EulerFor(item));

        // tamanho do item já virado como fica guardado, no espaço da área (alinhada com a gaveta)
        Transform t = item.transform;
        Quaternion oldRot = t.rotation;
        t.rotation = rot;
        Bounds wb = item.WorldBounds;
        Vector3 pivotToCenter = wb.center - t.position;
        float pivotAboveBottom = t.position.y - wb.min.y;
        t.rotation = oldRot;
        Vector3 size = Abs(Quaternion.Inverse(area.rotation) * wb.size);

        var occupied = new List<Bounds>();
        foreach (PickupInteractable other in inside)
            if (other != null && other != item && other.gameObject.activeInHierarchy) occupied.Add(LocalBounds(area, other.WorldBounds));

        Vector3 min = storageArea.center - storageArea.size * 0.5f, max = storageArea.center + storageArea.size * 0.5f;
        float hx = size.x * 0.5f + 0.005f, hz = size.z * 0.5f + 0.005f;
        if (min.x + hx > max.x - hx || min.z + hz > max.z - hz || size.y > max.y - min.y + 0.05f) return false;
        Vector3 aim = area.InverseTransformPoint(aimPoint);
        const float stepSize = 0.01f;
        bool found = false;
        Vector3 best = Vector3.zero;
        float bestDist = float.MaxValue;
        for (float x = min.x + hx; x <= max.x - hx + 1e-4f; x += stepSize)
            for (float z = min.z + hz; z <= max.z - hz + 1e-4f; z += stepSize)
            {
                var candidate = new Bounds(new Vector3(x, min.y + size.y * 0.5f, z), new Vector3(hx * 2f, size.y, hz * 2f));
                bool free = true;
                foreach (Bounds o in occupied)
                    if (candidate.min.x < o.max.x && candidate.max.x > o.min.x && candidate.min.z < o.max.z && candidate.max.z > o.min.z) { free = false; break; }
                if (!free) continue;
                float d = (x - aim.x) * (x - aim.x) + (z - aim.z) * (z - aim.z);
                if (d < bestDist) { bestDist = d; best = new Vector3(x, 0f, z); found = true; }
            }
        if (!found) return false;

        // centro do item sobre o ponto escolhido, base 2 mm acima do fundo
        Vector3 floorPoint = area.TransformPoint(new Vector3(best.x, min.y, best.z));
        Vector3 pos = floorPoint - new Vector3(pivotToCenter.x, 0f, pivotToCenter.z) + Vector3.up * (pivotAboveBottom + 0.002f);
        item.OnStored(this, parent, pos, rot);
        item.PickedUp += OnItemTaken;
        inside.Add(item);
        emptiedRaised = false;
        if (hideContentsWhenClosed && !openable.IsOpen) item.gameObject.SetActive(false);
        return true;
    }

    // Rotação de guardar: a mesma da entrada configurada para esse tipo de item (normalmente deitado, alinhado).
    private Vector3 EulerFor(PickupInteractable item)
    {
        string kind = ItemKind.Of(item);
        foreach (Entry e in entries)
            if (e.prefab != null && ItemKind.Of(e.prefab) == kind) return e.localEuler;
        return Vector3.zero;
    }

    private static Bounds LocalBounds(Transform space, Bounds world)
    {
        Vector3 c = world.center, e = world.extents;
        var b = new Bounds(space.InverseTransformPoint(c), Vector3.zero);
        for (int i = 0; i < 8; i++)
            b.Encapsulate(space.InverseTransformPoint(c + Vector3.Scale(e, new Vector3((i & 1) * 2 - 1, (i & 2) - 1, (i & 4) / 2 - 1))));
        return b;
    }

    private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

    private void OnOpenedBy(InteractionDetector interactor)
    {
        ItemHolder holder = interactor != null ? interactor.Holder : null;
        if (holder == null || holder.IsHolding) return;

        for (int e = 0; e < entries.Count; e++)
        {
            if (directRemaining[e] <= 0 || entries[e].prefab == null) continue;

            Transform hp = holder.HoldPoint;
            PickupInteractable item = Instantiate(entries[e].prefab, hp.position, hp.rotation);
            directRemaining[e]--;
            holder.Pick(item);
            CheckEmptied();
            return; // 1 item por abertura
        }
    }

    private void CheckEmptied()
    {
        if (emptiedRaised || !IsEmpty) return;
        emptiedRaised = true;
        onEmptied.Invoke();
    }
}
