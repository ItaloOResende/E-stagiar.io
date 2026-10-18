using UnityEngine;

/// <summary>
/// Diz ao <see cref="ComputerPower"/> se o jogador está VENDO o monitor daquele PC (<see cref="ComputerPower.WatchCheck"/>):
/// a instalação pelo pendrive e o boot depois de instalar o HD começam sozinhos, mas a tela só muda diante do jogador.
///  - Tela 2D aberta: só a manutenção DESTE PC conta (a vista Frente é decidida pelo <see cref="ComputerPower.InstallPaused"/>).
///  - No 3D: o monitor está a até <see cref="Distancia"/> m da câmera, dentro de um cone de <see cref="Angulo"/>° da mira
///    e sem parede/divisória no caminho.
/// Registrado sozinho ao carregar o jogo; não precisa estar na cena.
/// </summary>
public static class MonitorWatchCheck
{
    public const float Distancia = 4.5f;
    public const float Angulo = 30f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Registrar() => ComputerPower.WatchCheck = Vendo;

    public static bool Vendo(ComputerPower pc)
    {
        if (pc == null) return false;
        // o PC_Escritorio (pai do gabinete) tem monitor, gabinete e periféricos: bater neles não bloqueia a visão
        Transform conjunto = pc.transform.parent != null ? pc.transform.parent : pc.transform;
        return Vendo(pc.Monitor != null ? pc.Monitor : pc.transform, conjunto,
                     fonte => fonte.GetComponentInParent<ComputerPower>() == pc || MonitorDe(fonte) == pc);
    }

    /// <summary>Notebook (ou outro equipamento com tela própria): a tela 2D dele aberta, ou ele à vista no 3D.</summary>
    public static bool Vendo(Transform alvo, GameObject equipamento) =>
        Vendo(alvo, equipamento.transform, fonte => fonte == equipamento || fonte.transform.IsChildOf(equipamento.transform)
                             || equipamento.transform.IsChildOf(fonte.transform));

    private static bool Vendo(Transform alvo, Transform conjunto, System.Func<GameObject, bool> telaDele)
    {
        if (alvo == null) return false;
        UIScreenManager telas = UIScreenManager.Active;
        if (telas != null && telas.IsOpen)
        {
            GameObject fonte = telas.Current != null ? telas.Current.Source : null;
            return fonte != null && telaDele(fonte);
        }
        Camera cam = Camera.main;
        if (cam == null) return true;   // sem câmera (testes): não segura a transição
        Vector3 olho = cam.transform.position, para = alvo.position - olho;
        if (para.sqrMagnitude > Distancia * Distancia) return false;
        if (Vector3.Angle(cam.transform.forward, para) > Angulo) return false;
        // linha de visão: o primeiro obstáculo sólido precisa ser o próprio equipamento (ou nada)
        if (Physics.Linecast(olho, alvo.position, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore))
        {
            Transform t = hit.collider.transform;
            bool proprio = t.IsChildOf(conjunto) || t.IsChildOf(alvo) || hit.collider.GetComponentInParent<CharacterController>() != null;
            if (!proprio && (hit.point - alvo.position).sqrMagnitude > 0.35f * 0.35f) return false;
        }
        return true;
    }

    // PC do monitor aberto pela tela 2D (MonitorScreenInteractable abre a manutenção do PC dele).
    private static ComputerPower MonitorDe(GameObject fonte)
    {
        MonitorDisplay m = fonte.GetComponentInParent<MonitorDisplay>();
        return m != null ? m.Source : null;
    }
}
