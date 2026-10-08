# Art / Official — assets oficiais

Pasta destinada aos assets finais (3D cartunizado) produzidos por **Matheus**: escritório, paredes, mesas, PCs, gavetas, personagens etc. Será atualizada por ele.

## Contrato para substituir placeholders

A lógica de gameplay **não depende** dos modelos. Prefabs de gameplay (em `Assets/Prefabs/Gameplay`) têm um **root lógico** (colliders, `Interactable`, `Pickable`, `Socket`, dados) e um filho **`Visual`** com o mesh/material. Para trocar um placeholder pelo oficial, substitua o `Visual` (ou crie uma Prefab Variant); não é preciso alterar scripts.

Ao adicionar assets: manter nomes sem espaços/acentos, informar origem e licença, e usar materiais URP/Lit (ou Shader Graph do projeto).
