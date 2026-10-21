using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Teclado ou mouse de uma estação (vai no ROOT pegável do item, ao lado do <see cref="PickupInteractable"/>). Só marca
/// o tipo do item, sem nome nem tag: o chamado de estação nova (<see cref="PcTicketBridge"/>) confere por ele se a mesa
/// tem um teclado e um mouse. Qualquer teclado/mouse serve (os da estação, os das estantes da sala de TI).
/// </summary>
[RequireComponent(typeof(PickupInteractable))]
public class Periferico : MonoBehaviour
{
    public enum Tipo { Teclado, Mouse }

    [SerializeField] private Tipo tipo;

    private static readonly List<Periferico> ativos = new List<Periferico>();

    /// <summary>Teclados e mouses ativos na cena.</summary>
    public static IReadOnlyList<Periferico> Ativos => ativos;

    public Tipo Qual { get => tipo; set => tipo = value; }
    public PickupInteractable Item => GetComponent<PickupInteractable>();

    private void OnEnable() { if (!ativos.Contains(this)) ativos.Add(this); }
    private void OnDisable() => ativos.Remove(this);
}
