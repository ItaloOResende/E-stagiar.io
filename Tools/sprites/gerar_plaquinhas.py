"""
Gera as plaquinhas de identificação dos gaveteiros (pixel art, mesmo estilo das telas do monitor):
desenho em grade pequena e ampliação 4x sem suavização. Fonte DejaVu Sans Mono (licença livre,
a cópia que acompanha o Blender), sem antialiasing.

Uso:  python Tools/sprites/gerar_plaquinhas.py <pasta_saida>
Saída: plaquinha_<id>.png (gavetas, 160x40 -> 640x160) e plaquinha_titulo_<id>.png (topo, 200x36 -> 800x144).
"""
import os, sys
from PIL import Image, ImageDraw, ImageFont

FONT = os.environ.get("TELAS_FONTE", "C:/Program Files/Blender Foundation/Blender 5.2/5.2/datafiles/fonts/DejaVuSansMono.woff2")
out = sys.argv[1]
os.makedirs(out, exist_ok=True)

VERDE, AZUL = (70, 160, 90), (60, 120, 200)   # hardware / cabos
GAVETAS = {
    # id: (linha 1, linha 2, cor da categoria)
    "ram_cooler": ("MEMÓRIA RAM", "E COOLER", VERDE),
    "placa_mae": ("PLACA-MÃE", "", VERDE),
    "hd_fonte": ("HD E FONTE", "", VERDE),
    "ferramentas": ("FERRAMENTAS", "", VERDE),
    "energia": ("CABOS DE", "ENERGIA", AZUL),
    "vga": ("CABO VGA", "", AZUL),
    "hdmi": ("CABO HDMI", "", AZUL),
    "rede": ("CABOS DE", "REDE", AZUL),
}
TITULOS = {"hardware": ("HARDWARE", VERDE), "cabos": ("CABOS", AZUL)}


def plate(w, h, cor):
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.fontmode = "1"
    d.rectangle([0, 0, w - 1, h - 1], fill=(28, 28, 32, 255))                 # contorno
    d.rectangle([1, 1, w - 2, h - 2], fill=(238, 234, 220, 255))              # placa creme
    d.line([(2, 2), (w - 3, 2)], fill=(255, 252, 242, 255))                   # brilho
    d.line([(2, h - 3), (w - 3, h - 3)], fill=(196, 190, 174, 255))           # sombra
    d.rectangle([3, 3, 8, h - 4], fill=cor + (255,))                          # faixa da categoria
    for x in (12, w - 6):                                                     # rebites
        d.rectangle([x, 4, x + 1, 5], fill=(150, 150, 150, 255))
        d.rectangle([x, h - 6, x + 1, h - 5], fill=(150, 150, 150, 255))
    return img, d


def centered(d, w, y, text, size, x0=10):
    f = ImageFont.truetype(FONT, size)
    tw = d.textlength(text, font=f)
    for dx in (0, 1):  # negrito (traço de 2 px): legível de longe
        d.text((x0 + (w - x0 - tw) / 2 + dx, y), text, font=f, fill=(24, 24, 28, 255))


for key, (l1, l2, cor) in GAVETAS.items():
    w, h = 160, 40
    img, d = plate(w, h, cor)
    if l2:
        centered(d, w, 6, l1, 12); centered(d, w, 21, l2, 12)
    else:
        centered(d, w, 13, l1, 13)
    img.resize((w * 4, h * 4), Image.NEAREST).save(os.path.join(out, f"plaquinha_{key}.png"))
    print("gerado", key)

for key, (t, cor) in TITULOS.items():
    w, h = 200, 36
    img, d = plate(w, h, cor)
    centered(d, w, 8, t, 18)
    img.resize((w * 4, h * 4), Image.NEAREST).save(os.path.join(out, f"plaquinha_titulo_{key}.png"))
    print("gerado titulo", key)
