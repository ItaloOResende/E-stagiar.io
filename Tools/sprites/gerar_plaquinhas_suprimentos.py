"""Plaquinhas do gaveteiro de suprimentos (notebook e impressora), no estilo das plaquinhas atuais:
cartão creme, borda arredondada na cor da categoria e o texto na mesma cor. Gera em Assets/Art/Textures/Plaquinhas.
Uso: python Tools/sprites/gerar_plaquinhas_suprimentos.py
"""
import os
from PIL import Image, ImageDraw, ImageFont

RAIZ = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SAIDA = os.path.join(RAIZ, "Assets", "Art", "Textures", "Plaquinhas")
FONTE = "C:/Windows/Fonts/comicbd.ttf"   # traço de marcador, parecido com o das plaquinhas atuais
CREME = (246, 242, 235, 255)
LARANJA = (236, 132, 38, 255)

PLAQUINHAS = {
    "titulo_suprimentos": "SUPRIMENTOS",
    "memoria_ssd": "MEMÓRIA E SSD",
    "pendrive": "PENDRIVES",
    "toner": "TONER",
    "papel": "PAPEL A4",
}


def plaquinha(texto, cor, w=640, h=160):
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([0, 0, w - 1, h - 1], radius=26, fill=CREME)
    d.rounded_rectangle([10, 10, w - 11, h - 11], radius=22, outline=cor, width=9)
    tam = 96
    while tam > 20:
        f = ImageFont.truetype(FONTE, tam)
        x0, y0, x1, y1 = d.textbbox((0, 0), texto, font=f)
        if x1 - x0 <= w - 80 and y1 - y0 <= h - 50:
            break
        tam -= 2
    d.text(((w - (x1 - x0)) / 2 - x0, (h - (y1 - y0)) / 2 - y0), texto, font=f, fill=cor)
    return img


if __name__ == "__main__":
    for chave, texto in PLAQUINHAS.items():
        plaquinha(texto, LARANJA).save(os.path.join(SAIDA, "plaquinha_" + chave + ".png"))
        print("plaquinha_" + chave + ".png")
