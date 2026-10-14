"""Gera os efeitos sonoros das interações (autoria própria, síntese procedural; sem amostras de terceiros).

Uso: python gerar_sfx.py <pasta de saída>   (ex.: ../../Assets/Audio/SFX)
Cada som é feito de ruído filtrado e senoides com envelope; semente fixa (o resultado é sempre o mesmo).
WAV 16 bits mono, 44,1 kHz. Mudou algo aqui: rode de novo e o Unity reimporta.
"""
import os
import sys
import wave

import numpy as np

SR = 44100
rng = np.random.default_rng(20261014)


def t(dur):
    return np.arange(int(SR * dur)) / SR


def env(n, attack=0.002, decay=0.05):
    x = np.arange(n) / SR
    a = np.clip(x / max(attack, 1e-4), 0, 1)
    return a * np.exp(-x / decay)


def lowpass(x, cutoff):
    # filtro de 1 polo (suave), suficiente para dar "corpo" ao ruído
    k = np.exp(-2 * np.pi * cutoff / SR)
    y = np.empty_like(x)
    acc = 0.0
    for i, v in enumerate(x):
        acc = (1 - k) * v + k * acc
        y[i] = acc
    return y


def highpass(x, cutoff):
    return x - lowpass(x, cutoff)


def click(freq=3200, dur=0.03, decay=0.006, noise=0.6):
    n = int(SR * dur)
    s = np.sin(2 * np.pi * freq * t(dur)) * (1 - noise) + highpass(rng.standard_normal(n), 1500) * noise
    return s * env(n, 0.0005, decay)


def thud(freq=110, dur=0.18, decay=0.05):
    n = int(SR * dur)
    f = freq * (1 + 0.6 * np.exp(-t(dur) / 0.02))            # cai um pouco de tom (impacto)
    s = np.sin(2 * np.pi * np.cumsum(f) / SR) + 0.3 * lowpass(rng.standard_normal(n), 600)
    return s * env(n, 0.001, decay)


def slide(dur=0.35, lo=900, hi=2500):
    n = int(SR * dur)
    s = highpass(lowpass(rng.standard_normal(n), hi), lo)
    shape = np.sin(np.pi * np.arange(n) / n) ** 1.5
    return s * shape


def place(out, sig, at):
    i = int(SR * at)
    out[i:i + len(sig)] += sig[:max(0, len(out) - i)]


def mix(dur, parts):
    out = np.zeros(int(SR * dur))
    for at, sig in parts:
        place(out, sig, at)
    return out


def normalize(x, peak=0.8):
    m = np.max(np.abs(x))
    return x * (peak / m) if m > 0 else x


def save(folder, name, x):
    x = normalize(x)
    fade = min(len(x), int(SR * 0.005))
    x[-fade:] *= np.linspace(1, 0, fade)
    data = (x * 32767).astype(np.int16)
    with wave.open(os.path.join(folder, name + ".wav"), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(data.tobytes())


def main(folder):
    os.makedirs(folder, exist_ok=True)
    # parafuso: catraca da chave (cliques) e, ao sair, um "tic" metálico agudo; ao voltar, cliques e um aperto seco
    save(folder, "sfx_parafuso_retirar", mix(0.5, [(0.00 + i * 0.07, click(2600 + i * 150, decay=0.005)) for i in range(5)]
                                               + [(0.38, click(5200, 0.08, 0.03, noise=0.2))]))
    save(folder, "sfx_parafuso_recolocar", mix(0.45, [(0.00 + i * 0.065, click(3000 - i * 150, decay=0.005)) for i in range(5)]
                                                + [(0.34, thud(380, 0.1, 0.02) * 0.6 + 0)]))
    # tampa do gabinete: chapa deslizando + batida
    save(folder, "sfx_gabinete_abrir", mix(0.6, [(0.0, click(1800, 0.05, 0.01)), (0.05, slide(0.45, 700, 3000) * 0.7)]))
    save(folder, "sfx_gabinete_fechar", mix(0.55, [(0.0, slide(0.3, 700, 3000) * 0.6), (0.3, thud(140, 0.22, 0.05)),
                                                   (0.3, click(2200, 0.05, 0.01) * 0.5)]))
    # pegar / colocar / soltar / guardar
    save(folder, "sfx_pegar", mix(0.18, [(0.0, slide(0.14, 400, 2000) * 0.6), (0.02, thud(260, 0.1, 0.02) * 0.5)]))
    save(folder, "sfx_colocar", mix(0.22, [(0.0, thud(170, 0.2, 0.035))]))
    save(folder, "sfx_soltar", mix(0.3, [(0.0, slide(0.12, 400, 2000) * 0.4), (0.1, thud(120, 0.2, 0.05))]))
    # cabos (também usados para peças e pendrive)
    save(folder, "sfx_cabo_conectar", mix(0.16, [(0.0, slide(0.06, 1500, 5000) * 0.4), (0.05, click(1900, 0.06, 0.012, noise=0.4))]))
    save(folder, "sfx_cabo_desconectar", mix(0.18, [(0.0, click(1400, 0.05, 0.01, noise=0.5)), (0.03, slide(0.12, 1200, 4500) * 0.5)]))
    # gaveta
    save(folder, "sfx_gaveta_abrir", mix(0.5, [(0.0, slide(0.45, 300, 1600) * 0.8)]))
    # alerta do supervisor: "opa!" em dois tons descendentes (curto, sem susto)
    tt = t(0.22)
    tone1 = np.sin(2 * np.pi * 880 * tt + 0.4 * np.sin(2 * np.pi * 7 * tt)) * env(len(tt), 0.01, 0.12)
    tone2 = np.sin(2 * np.pi * 622 * tt + 0.4 * np.sin(2 * np.pi * 7 * tt)) * env(len(tt), 0.01, 0.16)
    save(folder, "sfx_supervisor_alerta", mix(0.5, [(0.0, tone1 * 0.8), (0.16, tone2)]))
    save(folder, "sfx_gaveta_fechar", mix(0.5, [(0.0, slide(0.35, 300, 1600) * 0.7), (0.33, thud(150, 0.15, 0.03))]))


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else "../../Assets/Audio/SFX")
