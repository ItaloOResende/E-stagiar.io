using UnityEngine;

/// <summary>
/// Marca, dentro de uma tela 2D, o lugar onde a miniatura do item da mão (<see cref="HeldItemPreview"/>) deve aparecer
/// enquanto aquela parte da tela está ativa (ex.: Traseira do PC: embaixo, no centro). Sem marcador ativo, a miniatura
/// fica na posição padrão do painel <c>MaoAtual</c>. Só um RectTransform vazio com este componente; não desenha nada.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class HeldItemSlot : MonoBehaviour { }
