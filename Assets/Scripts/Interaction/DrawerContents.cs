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
///  - Quando o jogador pega um item, ele sai definitivamente do conteúdo: abrir de novo não cria cópias.
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
        inside.Remove(item); // fica no mundo; nunca volta ao conteúdo
        CheckEmptied();
    }

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
