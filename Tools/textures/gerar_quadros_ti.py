"""Artes de parede da sala de TI (autoria própria, geradas por código).

Saída em Assets/Art/Textures/Quadros/:
  cartaz_seguranca.png   cartaz "Segurança na manutenção" (retrato, para o quadro_retrato)
  quadro_rede.png        diagrama da rede do escritório (paisagem, para o quadro_paisagem)
  calendario.png         calendário de outubro/2026 com a entrega no dia 21 (para o calendario)
  placa_sala_ti.png      placa externa "SALA DE TI" (para a placa)
  aviso_bancada.png      aviso da área de manutenção (para a placa)

Fonte: Inter (SIL Open Font License), a que vem com o Blender 5.2. Proporções iguais às faces de imagem dos modelos
de Tools/blender/sala_ti_complementos_build.py.

Uso: python Tools/textures/gerar_quadros_ti.py [pasta_saida]
"""
import calendar
import os
import sys

from PIL import Image, ImageDraw, ImageFont

FONTE = os.environ.get("QUADROS_FONTE", "C:/Program Files/Blender Foundation/Blender 5.2/5.2/datafiles/fonts/Inter.woff2")
SAIDA = sys.argv[1] if len(sys.argv) > 1 else "Assets/Art/Textures/Quadros"

AZUL = (52, 78, 160)
AZUL_CLARO = (120, 150, 230)
LILAS = (178, 176, 245)        # mesmo tom das divisórias do Matheus
CREME = (246, 241, 230)
ESCURO = (40, 42, 52)
VERMELHO = (214, 64, 64)
AMARELO = (250, 204, 64)
VERDE = (70, 170, 110)


def fonte(tam, peso="Regular"):
    f = ImageFont.truetype(FONTE, tam)
    try:
        f.set_variation_by_name(peso)
    except Exception:
        pass
    return f


def centro(d, xy, texto, f, cor):
    x, y = xy
    l, t, r, b = d.textbbox((0, 0), texto, font=f)
    d.text((x - (r - l) / 2 - l, y - (b - t) / 2 - t), texto, font=f, fill=cor)


def cartaz_seguranca():
    W, H = 840, 1200
    im = Image.new("RGB", (W, H), CREME)
    d = ImageDraw.Draw(im)
    d.rectangle((0, 0, W, 230), fill=AZUL)
    centro(d, (W / 2, 80), "SEGURANÇA NA", fonte(64, "Bold"), CREME)
    centro(d, (W / 2, 160), "MANUTENÇÃO", fonte(76, "Black"), AMARELO)
    itens = [("Desligue o PC e tire", "da tomada", VERMELHO, "plug"),
             ("Use a pulseira", "antiestática", AZUL, "pulseira"),
             ("Guarde os parafusos", "no potinho", VERDE, "parafuso"),
             ("Teste tudo antes", "de devolver", AZUL_CLARO, "check")]
    y = 300
    for i, (l1, l2, cor, icone) in enumerate(itens):
        cx, cy = 140, y + 95
        d.ellipse((cx - 85, cy - 85, cx + 85, cy + 85), fill=cor)
        if icone == "plug":
            d.rectangle((cx - 30, cy - 20, cx + 30, cy + 40), fill=CREME)
            d.rectangle((cx - 20, cy - 55, cx - 8, cy - 20), fill=CREME)
            d.rectangle((cx + 8, cy - 55, cx + 20, cy - 20), fill=CREME)
            d.line((cx - 60, cy + 60, cx + 60, cy - 60), fill=CREME, width=12)
        elif icone == "pulseira":
            d.ellipse((cx - 50, cy - 35, cx + 50, cy + 35), outline=CREME, width=14)
            d.line((cx + 45, cy, cx + 70, cy + 55), fill=CREME, width=8)
        elif icone == "parafuso":
            d.ellipse((cx - 45, cy - 45, cx + 45, cy + 45), fill=CREME)
            d.line((cx - 28, cy, cx + 28, cy), fill=cor, width=12)
            d.line((cx, cy - 28, cx, cy + 28), fill=cor, width=12)
        else:
            d.line((cx - 45, cy, cx - 10, cy + 38, cx + 50, cy - 40), fill=CREME, width=18, joint="curve")
        d.text((260, y + 30), f"{i + 1}. {l1}", font=fonte(46, "Bold"), fill=ESCURO)
        d.text((300, y + 95), l2, font=fonte(46), fill=ESCURO)
        y += 215
    d.rectangle((0, H - 70, W, H), fill=LILAS)
    centro(d, (W / 2, H - 35), "Equipe de Suporte de TI", fonte(34, "SemiBold"), ESCURO)
    return im


def quadro_rede():
    W, H = 1200, 840
    im = Image.new("RGB", (W, H), (250, 250, 252))
    d = ImageDraw.Draw(im)
    d.rectangle((0, 0, W, 120), fill=ESCURO)
    centro(d, (W / 2, 60), "REDE DO ESCRITÓRIO", fonte(60, "Black"), CREME)

    def caixa(x, y, w, h, cor, t1, t2=None):
        d.rounded_rectangle((x - w / 2, y - h / 2, x + w / 2, y + h / 2), 22, fill=cor, outline=ESCURO, width=5)
        centro(d, (x, y - (16 if t2 else 0)), t1, fonte(38, "Bold"), CREME)
        if t2:
            centro(d, (x, y + 26), t2, fonte(28), CREME)

    estacoes = [(180 + i * 210, 690) for i in range(5)]
    for (x, y) in estacoes:
        d.line((600, 470, x, y - 55), fill=AZUL, width=8)
    d.line((600, 245, 600, 400), fill=VERMELHO, width=10)
    caixa(600, 210, 360, 110, VERMELHO, "INTERNET")
    caixa(600, 440, 420, 120, AZUL, "RACK", "patch panel + switch")
    nomes = ["Est. A", "Est. B", "Est. C", "Est. D", "Est. E"]
    for (x, y), n in zip(estacoes, nomes):
        caixa(x, y, 180, 100, VERDE, n)
    d.text((40, 770), "Cabo de rede: padrão T568B nas duas pontas", font=fonte(26, "SemiBold"), fill=ESCURO)
    return im


def calendario_img():
    W, H = 720, 1080
    im = Image.new("RGB", (W, H), (252, 252, 250))
    d = ImageDraw.Draw(im)
    # ilustração de cima: monitor cartunizado
    d.rectangle((0, 0, W, 430), fill=AZUL_CLARO)
    d.rounded_rectangle((200, 70, 520, 300), 24, fill=ESCURO)
    d.rounded_rectangle((225, 95, 495, 275), 10, fill=(90, 200, 250))
    d.ellipse((300, 140, 340, 180), fill=ESCURO); d.ellipse((380, 140, 420, 180), fill=ESCURO)
    d.arc((310, 170, 410, 240), 20, 160, fill=ESCURO, width=8)
    d.rectangle((340, 300, 380, 350), fill=ESCURO); d.rounded_rectangle((270, 345, 450, 370), 8, fill=ESCURO)
    centro(d, (W / 2, 400), "OUTUBRO 2026", fonte(52, "Black"), CREME)
    # grade
    dias = ["D", "S", "T", "Q", "Q", "S", "S"]
    x0, y0, cw, ch = 30, 470, (W - 60) / 7, 95
    for i, nome in enumerate(dias):
        centro(d, (x0 + cw * i + cw / 2, y0), nome, fonte(34, "Bold"), VERMELHO if i == 0 else ESCURO)
    cal = calendar.Calendar(firstweekday=6).monthdayscalendar(2026, 10)
    for r, semana in enumerate(cal):
        for c, dia in enumerate(semana):
            if not dia:
                continue
            cx, cy = x0 + cw * c + cw / 2, y0 + 70 + r * ch
            if dia == 21:
                d.ellipse((cx - 40, cy - 40, cx + 40, cy + 40), outline=VERMELHO, width=7)
                centro(d, (cx, cy + 56), "ENTREGA!", fonte(20, "Black"), VERMELHO)
            centro(d, (cx, cy), str(dia), fonte(40, "SemiBold"), VERMELHO if c == 0 else ESCURO)
    return im


def placa(texto1, texto2, fundo, cor1, cor2, icone):
    W, H = 1200, 400
    im = Image.new("RGB", (W, H), fundo)
    d = ImageDraw.Draw(im)
    d.rectangle((12, 12, W - 12, H - 12), outline=cor2, width=10)
    cx, cy = 190, H / 2
    if icone == "pc":
        d.rounded_rectangle((cx - 110, cy - 90, cx + 110, cy + 60), 16, fill=cor1)
        d.rounded_rectangle((cx - 90, cy - 70, cx + 90, cy + 40), 8, fill=fundo)
        d.rectangle((cx - 20, cy + 60, cx + 20, cy + 95), fill=cor1)
        d.rounded_rectangle((cx - 70, cy + 90, cx + 70, cy + 110), 6, fill=cor1)
        d.line((cx - 40, cy - 15, cx - 10, cy + 15, cx + 45, cy - 40), fill=cor2, width=14)
    else:
        d.polygon([(cx, cy - 110), (cx + 120, cy + 95), (cx - 120, cy + 95)], fill=cor1)
        d.rectangle((cx - 10, cy - 45, cx + 10, cy + 35), fill=fundo)
        d.ellipse((cx - 13, cy + 50, cx + 13, cy + 76), fill=fundo)
    d.text((350, 90 if len(texto1) < 12 else 110), texto1, font=fonte(108 if len(texto1) < 12 else 62, "Black"), fill=cor1)
    d.text((355, 250), texto2, font=fonte(48, "SemiBold"), fill=cor2)
    return im


def main():
    os.makedirs(SAIDA, exist_ok=True)
    arts = {
        "cartaz_seguranca": cartaz_seguranca(),
        "quadro_rede": quadro_rede(),
        "calendario": calendario_img(),
        "placa_sala_ti": placa("SALA DE TI", "Suporte técnico · Manutenção", AZUL, CREME, AMARELO, "pc"),
        "aviso_bancada": placa("ÁREA DE MANUTENÇÃO", "Use a pulseira antiestática", AMARELO, ESCURO, ESCURO, "aviso"),
    }
    for nome, im in arts.items():
        p = os.path.join(SAIDA, nome + ".png")
        im.save(p)
        print("GERADO", p, im.size)


main()
