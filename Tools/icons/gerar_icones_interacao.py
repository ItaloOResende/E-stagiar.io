"""Gera os ícones de interação (HUD) em estilo cartoon (contorno grosso, cel-shading com
sombra e brilho), sem pixel art. Saída: Assets/Resources/Interacao/*.png (+ .meta de sprite legível, só na 1ª vez).

    python Tools/icons/gerar_icones_interacao.py

Desenhado em 4x e reduzido (antialias). Paleta própria do jogo; nenhum asset de terceiros.
"""
import os
import uuid
from PIL import Image, ImageDraw, ImageFilter, ImageChops

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "Resources", "Interacao")
S = 1024            # tela de desenho
FINAL = 256         # tamanho salvo
INK = (43, 33, 64, 255)
OUTLINE = 34        # contorno (px na tela de desenho)


def mask_new():
    return Image.new("L", (S, S), 0)


def offset(m, dx, dy):
    return ImageChops.offset(m, dx, dy)


def dilate(m, r):
    # dilatação em passos (MaxFilter grande é lento): 3 passos de r/3
    k = max(3, (r // 3) * 2 + 1)
    for _ in range(3):
        m = m.filter(ImageFilter.MaxFilter(k))
    return m


class Icon:
    def __init__(self):
        self.img = Image.new("RGBA", (S, S), (0, 0, 0, 0))

    def part(self, m, base, shade=None, light=None, outline=OUTLINE):
        """Peça com contorno, sombra (baixo/direita) e brilho (cima/esquerda), por cima do que já foi desenhado."""
        shade = shade or tuple(int(c * 0.72) for c in base[:3]) + (255,)
        light = light or tuple(min(255, int(c + (255 - c) * 0.55)) for c in base[:3]) + (255,)
        if outline:
            self.img.paste(INK, (0, 0), dilate(m, outline))
        self.img.paste(base, (0, 0), m)
        sh = ImageChops.subtract(m, offset(m, -40, -46))
        self.img.paste(shade, (0, 0), sh)
        li = ImageChops.subtract(m, offset(m, 22, 26))
        li = ImageChops.multiply(li, offset(m, -14, -14))   # só a borda de cima/esquerda, sem vazar
        self.img.paste(light, (0, 0), li)

    def dot(self, xy, r, color=(255, 255, 255, 230)):
        d = ImageDraw.Draw(self.img)
        x, y = xy
        d.ellipse((x - r, y - r, x + r, y + r), fill=color)

    def save(self, name):
        os.makedirs(OUT, exist_ok=True)
        small = self.img.resize((FINAL, FINAL), Image.LANCZOS)
        path = os.path.join(OUT, name + ".png")
        small.save(path)
        write_meta(path)
        print("ok", os.path.relpath(path, ROOT))


def rrect(box, r):
    m = mask_new()
    ImageDraw.Draw(m).rounded_rectangle(box, r, fill=255)
    return m


def ellipse(box):
    m = mask_new()
    ImageDraw.Draw(m).ellipse(box, fill=255)
    return m


def poly(pts):
    m = mask_new()
    ImageDraw.Draw(m).polygon(pts, fill=255)
    return m


def rotated(m, deg, center=None):
    return m.rotate(deg, resample=Image.BICUBIC, center=center)


def union(*ms):
    out = mask_new()
    for m in ms:
        out = ImageChops.lighter(out, m)
    return out


SKIN = (255, 210, 166, 255)
SKIN_SH = (232, 158, 118, 255)
CUFF = (90, 170, 235, 255)
METAL = (196, 206, 218, 255)
RED = (235, 72, 62, 255)
YELLOW = (255, 200, 60, 255)
BLUE = (70, 140, 230, 255)
GREEN = (90, 200, 120, 255)
DARK = (70, 74, 96, 255)
WHITE = (250, 250, 252, 255)


def screwdriver(ic, cx=512, cy=512, scale=1.0, ang=45):
    lay = Icon()
    s = scale
    shaft = rrect((512 - 34 * s, 80, 512 + 34 * s, 560), 20)
    tip = poly([(512 - 34 * s, 80), (512 + 34 * s, 80), (512 + 12 * s, 20), (512 - 12 * s, 20)])
    lay.part(union(shaft, tip), METAL, (140, 150, 170, 255))
    lay.part(rrect((512 - 60 * s, 530, 512 + 60 * s, 600), 20), DARK)
    handle = rrect((512 - 110 * s, 580, 512 + 110 * s, 960), 100 * s)
    lay.part(handle, RED, (180, 40, 40, 255))
    d = ImageDraw.Draw(lay.img)
    for x in (-50, 0, 50):
        d.rounded_rectangle((512 + x * s - 12, 630, 512 + x * s + 12, 900), 12, fill=(160, 36, 36, 255))
    lay.dot((512 - 60 * s, 660), 20)
    r = lay.img.rotate(ang, resample=Image.BICUBIC, center=(512, 512))
    if (cx, cy) != (512, 512) or scale != 1.0:
        r = r.resize((int(S * scale), int(S * scale)), Image.LANCZOS)
        canvas = Image.new("RGBA", (S, S), (0, 0, 0, 0))
        canvas.alpha_composite(r, (int(cx - S * scale / 2), int(cy - S * scale / 2)))
        r = canvas
    ic.img.alpha_composite(r)


def make_abrir():
    ic = Icon()
    ic.part(rrect((150, 330, 870, 900), 50), (200, 150, 110, 255))          # móvel
    ic.part(rrect((210, 520, 810, 860), 30), (230, 185, 140, 255))          # gaveta puxada
    ic.part(rrect((420, 640, 600, 710), 30), DARK, outline=20)              # puxador
    arrow = poly([(512, 60), (700, 250), (590, 250), (590, 380), (434, 380), (434, 250), (324, 250)])
    ic.part(arrow, GREEN)
    ic.save("icone_abrir")


def make_chave_fenda():
    ic = Icon(); screwdriver(ic); ic.save("icone_chave_fenda")


def make_crimpador():
    ic = Icon()
    lay = Icon()
    h1 = rotated(rrect((300, 430, 420, 960), 60), 14, (360, 450))
    h2 = rotated(rrect((604, 430, 724, 960), 60), -14, (664, 450))
    lay.part(h1, BLUE, (40, 90, 170, 255))
    lay.part(h2, BLUE, (40, 90, 170, 255))
    head = poly([(330, 470), (694, 470), (640, 120), (384, 120)])
    lay.part(head, METAL, (140, 150, 170, 255))
    lay.part(rrect((440, 210, 584, 330), 16), DARK, outline=18)              # boca do RJ45
    lay.part(ellipse((470, 380, 554, 464)), (120, 128, 150, 255), outline=18)  # pino
    ic.img.alpha_composite(lay.img.rotate(-18, resample=Image.BICUBIC, center=(512, 512)))
    # conector RJ45 transparente
    ic.part(rrect((690, 120, 860, 270), 24), (200, 235, 255, 255), outline=22)
    d = ImageDraw.Draw(ic.img)
    for i in range(4):
        d.rectangle((715 + i * 36, 140, 735 + i * 36, 190), fill=(230, 180, 60, 255))
    ic.save("icone_crimpador")


def make_impressora():
    ic = Icon()
    ic.part(rrect((300, 90, 724, 380), 20), WHITE, (210, 214, 224, 255))     # papel saindo atrás
    ic.part(rrect((130, 330, 894, 760), 80), (120, 130, 160, 255))           # corpo
    ic.part(rrect((250, 640, 774, 940), 20), WHITE, (210, 214, 224, 255))    # folha impressa
    d = ImageDraw.Draw(ic.img)
    for y in (720, 790, 860):
        d.rounded_rectangle((310, y, 714, y + 26), 12, fill=(150, 160, 190, 255))
    ic.part(ellipse((740, 400, 820, 480)), GREEN, outline=18)                # LED
    ic.save("icone_impressora")


def make_notebook():
    ic = Icon()
    ic.part(poly([(130, 760), (894, 760), (980, 900), (44, 900)]), (150, 160, 185, 255))   # base
    ic.part(rrect((200, 180, 824, 770), 40), DARK)                                          # tampa
    ic.part(rrect((250, 230, 774, 720), 20), (110, 190, 255, 255), outline=0)              # tela
    ic.dot((330, 320), 30)
    screwdriver(ic, cx=700, cy=420, scale=0.55, ang=40)
    ic.save("icone_notebook")


def make_monitor():
    ic = Icon()
    ic.part(rrect((380, 720, 644, 900), 30), DARK)                           # pé
    ic.part(rrect((260, 860, 764, 960), 40), DARK)
    ic.part(rrect((90, 130, 934, 760), 60), DARK)
    ic.part(rrect((150, 190, 874, 700), 30), (110, 190, 255, 255), outline=0)
    ic.dot((250, 290), 34)
    ic.save("icone_monitor")


def make_rack():
    ic = Icon()
    ic.part(rrect((200, 60, 824, 980), 50), DARK)
    for i, y in enumerate((150, 380, 610)):
        ic.part(rrect((260, y, 764, y + 180), 24), (100, 110, 140, 255), outline=18)
        d = ImageDraw.Draw(ic.img)
        for k in range(5):
            x = 300 + k * 80
            d.rounded_rectangle((x, y + 60, x + 50, y + 120), 8, fill=(30, 30, 40, 255))
            d.ellipse((x + 15, y + 20, x + 35, y + 40), fill=(GREEN if (k + i) % 3 else YELLOW))
    # cabo azul saindo
    d = ImageDraw.Draw(ic.img)
    d.line([(355, 470), (300, 860), (120, 940)], fill=INK, width=70, joint="curve")
    d.line([(355, 470), (300, 860), (120, 940)], fill=BLUE, width=40, joint="curve")
    ic.save("icone_rack")


def make_testador():
    ic = Icon()
    ic.part(rrect((250, 120, 774, 940), 80), (255, 170, 50, 255))
    ic.part(rrect((320, 200, 704, 520), 30), DARK, outline=20)
    d = ImageDraw.Draw(ic.img)
    for k in range(4):
        for r in range(2):
            x, y = 370 + k * 85, 250 + r * 130
            d.ellipse((x, y, x + 60, y + 60), fill=(GREEN if (k + r) % 4 else RED))
    ic.part(ellipse((430, 620, 594, 784)), RED, outline=22)                  # botão TEST
    ic.part(rrect((380, 830, 644, 960), 20), DARK, outline=18)               # entrada RJ45
    ic.save("icone_testador")


def make_bloqueado():
    ic = Icon()
    ring = ImageChops.subtract(ellipse((110, 110, 914, 914)), ellipse((230, 230, 794, 794)))
    bar = rotated(rrect((170, 452, 854, 572), 50), -45)
    ic.part(union(ring, ImageChops.multiply(bar, ellipse((150, 150, 874, 874)))), RED, (180, 40, 40, 255))
    ic.save("icone_bloqueado")


def make_ok():
    ic = Icon()
    ic.part(ellipse((110, 110, 914, 914)), GREEN, (50, 150, 85, 255))
    d = ImageDraw.Draw(ic.img)
    d.line([(300, 530), (450, 680), (730, 380)], fill=WHITE, width=110, joint="curve")
    ic.save("feedback_ok")


def make_info():
    ic = Icon()
    lay = Icon()
    lay.part(rrect((470, 560, 580, 960), 50), DARK)                              # cabo da lupa
    lay.part(ellipse((180, 80, 760, 660)), (230, 240, 250, 255))
    lay.part(ellipse((270, 170, 670, 570)), (130, 200, 255, 255), outline=24)
    lay.dot((380, 280), 40)
    ic.img.alpha_composite(lay.img.rotate(35, resample=Image.BICUBIC, center=(512, 512)))
    ic.save("feedback_diagnostico")


def make_alerta():
    ic = Icon()
    ic.part(poly([(512, 90), (940, 880), (84, 880)]), YELLOW, (220, 160, 30, 255))
    ic.part(rrect((462, 330, 562, 660), 50), INK, outline=0)
    ic.part(ellipse((457, 710, 567, 820)), INK, outline=0)
    ic.save("feedback_alerta")


META = """fileFormatVersion: 2
guid: {guid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
    linearTexture: 0
  isReadable: 1
  textureFormat: 1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  spriteMode: 1
  spriteExtrude: 1
  spriteMeshType: 0
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: 100
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  alphaUsage: 1
  alphaIsTransparency: 1
  textureType: 8
  textureShape: 1
  platformSettings:
  - serializedVersion: 4
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: 4
    textureCompression: 0
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    physicsShape: []
    bones: []
    spriteID: 5e97eb03825dee720800000000000000
    internalID: 0
    vertices: []
    indices:
    edges: []
    weights: []
  spritePackingTag:
  pSDRemoveMatte: 0
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def write_meta(png):
    meta = png + ".meta"
    if os.path.exists(meta):
        return   # mantém o GUID (e ajustes feitos no Editor)
    with open(meta, "w", encoding="utf-8", newline="\n") as f:
        f.write(META.format(guid=uuid.uuid4().hex))


if __name__ == "__main__":
    for fn in (make_abrir, make_chave_fenda, make_crimpador, make_impressora,
               make_notebook, make_monitor, make_rack, make_testador, make_bloqueado, make_ok, make_info, make_alerta):
        fn()
