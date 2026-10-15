"""
Textura de rachadura (autoria própria do projeto, gerada por código): linhas escuras com borda clara, irradiando de
um ponto de impacto, em fundo transparente. Usada como camada "quebrado" por cima de equipamentos (tela do monitor,
laterais do gabinete) pelo PartFallDamage (Broken Visual). Determinística (semente fixa).

Uso: python Tools/textures/gerar_rachadura.py Assets/Art/Textures/T_Rachadura.png
"""
import math
import random
import sys

from PIL import Image, ImageDraw, ImageFilter

TAM = 512


def galho(d, x, y, ang, comp, larg, rnd, nivel):
    pts = [(x, y)]
    passos = max(4, int(comp / 18))
    for _ in range(passos):
        ang += rnd.uniform(-0.35, 0.35)
        x += math.cos(ang) * comp / passos
        y += math.sin(ang) * comp / passos
        pts.append((x, y))
        if nivel < 2 and rnd.random() < 0.18:
            galho(d, x, y, ang + rnd.choice((-1, 1)) * rnd.uniform(0.5, 1.1), comp * 0.45, max(1, larg - 1), rnd, nivel + 1)
    d.line(pts, fill=(255, 255, 255, 200), width=larg + 3, joint="curve")   # borda clara (vidro lascado)
    d.line(pts, fill=(20, 22, 26, 255), width=larg, joint="curve")          # fenda escura


def main(saida):
    rnd = random.Random(42)
    img = Image.new("RGBA", (TAM, TAM), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    cx, cy = TAM * 0.42, TAM * 0.38
    for i in range(9):                                                     # raios a partir do impacto
        ang = i * 2 * math.pi / 9 + rnd.uniform(-0.2, 0.2)
        galho(d, cx, cy, ang, rnd.uniform(TAM * 0.35, TAM * 0.7), 3, rnd, 0)
    for r in (34, 70):                                                     # anéis concêntricos (teia)
        pts = []
        for k in range(19):
            a = k * 2 * math.pi / 18
            rr = r * rnd.uniform(0.85, 1.15)
            pts.append((cx + math.cos(a) * rr, cy + math.sin(a) * rr))
        d.line(pts, fill=(255, 255, 255, 170), width=4, joint="curve")
        d.line(pts, fill=(20, 22, 26, 235), width=2, joint="curve")
    d.ellipse((cx - 10, cy - 10, cx + 10, cy + 10), fill=(235, 238, 242, 230))  # ponto do impacto
    img = img.filter(ImageFilter.SMOOTH)
    img.save(saida)
    print("GERADO", saida)


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else "T_Rachadura.png")
