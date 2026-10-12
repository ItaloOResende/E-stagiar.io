"""
Gera os sprites em pixel art da manutenção do PC (mesmo estilo de placa_mae/memoria_ram/hd/tomada_*):
desenho numa grade pequena e ampliação sem suavização (vizinho mais próximo).

Uso:  python Tools/sprites/gerar_sprites_pc.py <pasta_saida> <pasta_placeholders_sprites> <pasta_placeholders_textures>

Saída (PNG com transparência):
  ui_fonte.png              fonte de alimentação (vista lateral, como fica no gabinete aberto)
  ui_cooler.png             cooler do processador (vista de cima)
  ui_hd_deitado.png         hd.png girado 90° (HD deitado na baia), recortado ao desenho
  ui_memoria_ram_vertical.png  memoria_ram.png girada 90° (pente no slot vertical da placa-mãe)
  ui_plugue_energia.png / ui_plugue_vga.png / ui_plugue_hdmi.png / ui_plugue_rj45.png / ui_plugue_tomada.png
                            plugues vistos de trás (encaixados), desenhados sobre a porta quando conectada
  ui_tomada_energia.png     tomada NBR 14136 (2P+T) com espelho, parafusos e sombra
  ui_ponto_rede.png         ponto de rede de parede com 1 RJ45
Autoria própria do projeto (código), sem assets de terceiros.
"""
import sys, os
from PIL import Image, ImageDraw

out, ph_sprites, ph_tex = sys.argv[1:4]
os.makedirs(out, exist_ok=True)

OUT = (26, 26, 30, 255)        # contorno
T = (0, 0, 0, 0)


class G:
    """Grade de pixels; desenha em coordenadas inteiras e exporta ampliada."""
    def __init__(self, w, h):
        self.im = Image.new("RGBA", (w, h), T)
        self.d = ImageDraw.Draw(self.im)

    def rect(self, x0, y0, x1, y1, fill, outline=None):
        self.d.rectangle((x0, y0, x1, y1), fill=fill, outline=outline)

    def px(self, x, y, c):
        self.d.point((x, y), fill=c)

    def line(self, pts, c):
        self.d.line(pts, fill=c)

    def ellipse(self, x0, y0, x1, y1, fill, outline=None):
        self.d.ellipse((x0, y0, x1, y1), fill=fill, outline=outline)

    def poly(self, pts, fill, outline=None):
        self.d.polygon(pts, fill=fill, outline=outline)

    def save(self, name, scale):
        self.im.resize((self.im.width * scale, self.im.height * scale), Image.NEAREST).save(os.path.join(out, name))
        print("SPRITE", name, self.im.width * scale, "x", self.im.height * scale)


def bevel(g, x0, y0, x1, y1, base, light, dark):
    g.rect(x0, y0, x1, y1, base, OUT)
    g.line([(x0 + 1, y0 + 1), (x1 - 1, y0 + 1)], light)
    g.line([(x0 + 1, y0 + 1), (x0 + 1, y1 - 1)], light)
    g.line([(x0 + 1, y1 - 1), (x1 - 1, y1 - 1)], dark)
    g.line([(x1 - 1, y0 + 1), (x1 - 1, y1 - 1)], dark)


def screw(g, x, y):
    g.rect(x - 1, y - 1, x + 1, y + 1, (200, 202, 208, 255), OUT)
    g.px(x, y, (90, 92, 98, 255))


# ---------------- fonte (70 x 40) ----------------
g = G(70, 40)
bevel(g, 0, 0, 69, 39, (128, 132, 140, 255), (176, 180, 188, 255), (84, 88, 96, 255))
g.rect(2, 33, 67, 37, (98, 102, 110, 255))                       # dobra inferior da chapa
for y in range(5, 31, 4):                                          # grade de ventilação (lado traseiro)
    for x in range(4, 17, 4):
        g.rect(x, y, x + 1, y + 1, (40, 42, 48, 255))
g.rect(22, 7, 52, 28, (232, 222, 150, 255), OUT)                   # etiqueta
g.rect(23, 8, 51, 11, (214, 70, 54, 255))
for i, y in enumerate((14, 17, 20, 23, 26)):
    g.line([(25, y), (25 + (22 if i % 2 == 0 else 15), y)], (70, 70, 74, 255))
g.poly([(46, 13), (42, 19), (45, 19), (43, 25), (49, 17), (46, 17), (48, 13)], (250, 196, 40, 255), OUT)  # raio
for y in (3, 36):
    for x in (3, 66):
        screw(g, x, y)
g.rect(56, 10, 66, 30, (30, 30, 34, 255), OUT)                     # chicote de cabos saindo para dentro
for i, c in enumerate(((220, 60, 50), (240, 200, 40), (40, 40, 44), (240, 200, 40), (220, 60, 50))):
    g.line([(58 + i * 2, 11), (58 + i * 2, 29)], c + (255,))
g.rect(55, 13, 58, 27, (236, 236, 228, 255), OUT)                  # conector ATX
g.save("ui_fonte.png", 16)

# ---------------- cooler (40 x 40) ----------------
g = G(40, 40)
bevel(g, 0, 0, 39, 39, (150, 154, 162, 255), (196, 200, 208, 255), (96, 100, 108, 255))
for x in range(3, 37, 3):                                          # aletas do dissipador
    g.line([(x, 2), (x, 37)], (112, 116, 124, 255))
g.ellipse(4, 4, 35, 35, (44, 46, 52, 255), OUT)                    # moldura da ventoinha
g.ellipse(7, 7, 32, 32, (30, 32, 36, 255))
import math
for k in range(7):                                                 # pás
    a = k * 2 * math.pi / 7
    pts = []
    for r, da in ((5, -0.25), (13, -0.05), (13, 0.35), (5, 0.25)):
        pts.append((19.5 + r * math.cos(a + da), 19.5 + r * math.sin(a + da)))
    g.poly(pts, (88, 150, 210, 255), (52, 96, 150, 255))
g.ellipse(15, 15, 24, 24, (214, 218, 226, 255), OUT)               # cubo
g.ellipse(18, 18, 21, 21, (214, 70, 54, 255))
for x, y in ((2, 2), (37, 2), (2, 37), (37, 37)):
    screw(g, x, y)
g.save("ui_cooler.png", 24)

# ---------------- HD deitado e RAM vertical (reaproveitam os sprites existentes) ----------------
hd = Image.open(os.path.join(ph_sprites, "hd.png")).convert("RGBA")
hd = hd.crop(hd.getbbox()).rotate(90, expand=True)
hd.save(os.path.join(out, "ui_hd_deitado.png")); print("SPRITE ui_hd_deitado.png", hd.size)
ram = Image.open(os.path.join(ph_sprites, "memoria_ram.png")).convert("RGBA")
ram = ram.crop(ram.getbbox()).rotate(90, expand=True)
ram.save(os.path.join(out, "ui_memoria_ram_vertical.png")); print("SPRITE ui_memoria_ram_vertical.png", ram.size)

# ---------------- plugues (vistos de trás, encaixados) ----------------
BLK, BLK_L, BLK_D = (40, 40, 46, 255), (78, 80, 88, 255), (22, 22, 26, 255)
# energia IEC C13: corpo preto com alívio de tensão e cabo saindo para o observador
g = G(24, 18)
bevel(g, 0, 0, 23, 17, BLK, BLK_L, BLK_D)
g.rect(3, 3, 20, 14, (54, 54, 60, 255))
for x in range(5, 19, 3):
    g.line([(x, 4), (x, 13)], BLK_D)                               # frisos de pega
g.ellipse(8, 5, 15, 12, (30, 30, 34, 255), OUT); g.ellipse(10, 7, 13, 10, (64, 64, 70, 255))
g.save("ui_plugue_energia.png", 16)
# VGA: capa azul trapezoidal com 2 parafusos borboleta
g = G(34, 14)
g.poly([(5, 1), (28, 1), (26, 12), (7, 12)], (52, 96, 210, 255), OUT)
g.line([(7, 2), (26, 2)], (110, 150, 240, 255))
g.ellipse(13, 3, 20, 10, (34, 64, 150, 255), OUT); g.ellipse(15, 5, 18, 8, (64, 64, 70, 255))
for x in (2, 31):
    g.ellipse(x - 2, 4, x + 2, 9, (210, 212, 218, 255), OUT); g.px(x, 6, (120, 122, 128, 255))
g.save("ui_plugue_vga.png", 12)
# HDMI: corpo preto com ponta metálica visível
g = G(26, 12)
bevel(g, 2, 1, 23, 10, BLK, BLK_L, BLK_D)
g.poly([(0, 3), (25, 3), (25, 7), (22, 10), (3, 10), (0, 7)], None, (170, 172, 180, 255))
g.rect(8, 4, 17, 7, (30, 30, 34, 255), OUT); g.rect(11, 5, 14, 6, (64, 64, 70, 255))
g.save("ui_plugue_hdmi.png", 12)
# RJ45: corpo transparente com fios coloridos e capa azul
g = G(18, 18)
g.rect(1, 1, 16, 16, (70, 130, 200, 255), OUT)                      # capa (bota) azul
g.rect(3, 3, 14, 12, (206, 226, 236, 255), OUT)                     # corpo transparente
for i, c in enumerate(((240, 140, 40), (250, 250, 250), (60, 160, 80), (60, 90, 200))):
    g.rect(4 + i * 3, 4, 5 + i * 3, 9, c + (255,))
g.rect(6, 13, 11, 15, (40, 90, 160, 255), OUT)                      # trava
g.save("ui_plugue_rj45.png", 14)
# plugue de tomada (redondo, NBR 14136) visto de trás
g = G(22, 22)
g.ellipse(0, 0, 21, 21, BLK, OUT); g.ellipse(2, 2, 19, 19, (58, 58, 64, 255))
g.line([(4, 6), (8, 3)], BLK_L)
g.ellipse(7, 7, 14, 14, (30, 30, 34, 255), OUT); g.ellipse(9, 9, 12, 12, (64, 64, 70, 255))
g.save("ui_plugue_tomada.png", 14)

# ---------------- tomada de energia e ponto de rede ----------------
WHITE, WHITE_L, WHITE_D = (236, 236, 232, 255), (252, 252, 250, 255), (186, 186, 182, 255)
g = G(30, 38)
g.rect(1, 2, 29, 37, (0, 0, 0, 60))                                 # sombra
bevel(g, 0, 0, 27, 35, WHITE, WHITE_L, WHITE_D)                     # espelho
screw(g, 13, 3); screw(g, 13, 32)
g.poly([(4, 17), (8, 11), (19, 11), (23, 17), (19, 23), (8, 23)], (210, 210, 206, 255), OUT)  # rebaixo sextavado
for x, y in ((9, 15), (18, 15), (13, 20)):
    g.ellipse(x - 2, y - 2, x + 2, y + 2, (30, 30, 34, 255))
g.save("ui_tomada_energia.png", 16)

g = G(30, 38)
g.rect(1, 2, 29, 37, (0, 0, 0, 60))
bevel(g, 0, 0, 27, 35, WHITE, WHITE_L, WHITE_D)
screw(g, 13, 3); screw(g, 13, 32)
g.rect(6, 9, 21, 27, (50, 52, 58, 255), OUT)                         # módulo keystone
g.rect(8, 11, 19, 13, (90, 160, 90, 255))                            # etiqueta "REDE"
g.rect(9, 15, 18, 24, (240, 240, 236, 255), OUT)                     # abertura RJ45
g.rect(11, 17, 16, 23, (30, 30, 34, 255))
g.rect(12, 23, 15, 24, (30, 30, 34, 255))
g.save("ui_ponto_rede.png", 16)

# ---------------- destaque de hover (anel branco suave; não usa verde para não parecer "ligado") ----------------
from PIL import ImageFilter
N = 256
ring = Image.new("L", (N, N), 0)
dr = ImageDraw.Draw(ring)
dr.ellipse((14, 14, N - 15, N - 15), outline=255, width=14)
ring = ring.filter(ImageFilter.GaussianBlur(5))
hover = Image.new("RGBA", (N, N), (255, 255, 255, 0))
hover.putalpha(ring)
hover.save(os.path.join(out, "ui_hover_anel.png"))
print("SPRITE ui_hover_anel.png", N, "x", N)
