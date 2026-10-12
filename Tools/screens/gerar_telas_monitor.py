"""
Gera as telas do monitor (BIOS e sistema operacional) no mesmo formato da tela_no_signal do legado:
640 x 320, pixel art (desenhado em 320 x 160 e ampliado 2x sem filtro).

Criadas para o projeto, sem arte de terceiros. Fonte: DejaVu Sans Mono (licença livre Bitstream Vera/DejaVu,
permite uso e redistribuição), a cópia que acompanha o Blender; só os pixels do texto vão para a imagem. Marcas e nomes são fictícios (sem logotipos reais de BIOS ou sistema operacional).

Uso:  python Tools/screens/gerar_telas_monitor.py Assets/Art/Placeholders/Sprites
"""
import os
import sys

from PIL import Image, ImageDraw, ImageFont

W, H = 320, 160  # metade da resolução final (pixel art 2x)


FONT = os.environ.get("TELAS_FONTE", "C:/Program Files/Blender Foundation/Blender 5.2/5.2/datafiles/fonts/DejaVuSansMono.woff2")


def font(size):
    return ImageFont.truetype(FONT, size)


def canvas(color):
    img = Image.new("RGB", (W, H), color)
    d = ImageDraw.Draw(img)
    d.fontmode = "1"  # sem antialiasing: pixels nítidos depois de ampliar
    return img, d


def save(img, folder, name):
    img.resize((W * 2, H * 2), Image.NEAREST).save(os.path.join(folder, name), optimize=True)
    print("gerado", name)


def bios(folder):
    azul, cinza, branco, amarelo, vermelho = (0, 0, 170), (170, 170, 170), (255, 255, 255), (255, 255, 85), (255, 85, 85)
    img, d = canvas(azul)
    f, fb = font(9), font(10)
    d.rectangle([0, 0, W, 13], fill=cinza)
    d.text((6, 1), "E-STAGIAR.IO BIOS  v1.0", font=fb, fill=azul)
    d.text((W - 92, 1), "Configuração", font=fb, fill=azul)
    d.rectangle([4, 18, W - 5, 112], outline=cinza)
    linhas = [
        ("Processador", "OK", branco),
        ("Placa-mãe", "OK", branco),
        ("Memória RAM", "4096 MB  OK", branco),
        ("Saída de vídeo", "VGA  OK", branco),
        ("Disco rígido (HD)", "NÃO ENCONTRADO", vermelho),
    ]
    y = 24
    for nome, valor, cor in linhas:
        d.text((12, y), nome, font=f, fill=cinza)
        d.text((150, y), valor, font=f, fill=cor)
        y += 15
    d.text((12, y + 2), "Ordem de inicialização:  1. HD   2. Rede", font=f, fill=cinza)
    d.rectangle([0, 118, W, H], fill=(0, 0, 120))
    d.text((8, 121), "Nenhum dispositivo de inicialização encontrado.", font=f, fill=amarelo)
    d.text((8, 134), "Instale um HD e reinicie o computador.", font=f, fill=branco)
    d.text((8, 147), "F10: Salvar e sair    Esc: Sair", font=f, fill=cinza)
    save(img, folder, "ui_tela_bios.png")


def sistema(folder):
    img, d = canvas((40, 120, 160))
    for y in range(H):  # papel de parede em degradê, em faixas (pixel art)
        t = (y // 8) * 8 / H
        d.line([(0, y), (W, y)], fill=(int(36 + 30 * t), int(140 - 50 * t), int(175 - 40 * t)))
    d.ellipse([200, 30, 300, 130], fill=(70, 170, 200))  # "sol" decorativo
    d.ellipse([214, 44, 286, 116], fill=(95, 190, 215))
    f, fb = font(8), font(10)

    def icone(x, y, cor, rotulo):
        d.rectangle([x, y, x + 17, y + 13], fill=cor, outline=(20, 20, 30))
        d.rectangle([x + 3, y + 3, x + 14, y + 6], fill=(255, 255, 255))
        d.text((x - 8, y + 16), rotulo, font=font(7), fill=(255, 255, 255))

    icone(12, 8, (230, 200, 90), "Arquivos")
    icone(12, 42, (120, 200, 120), "Chamados")
    icone(12, 76, (200, 200, 210), "Lixeira")

    # janela de boas-vindas
    x0, y0, x1, y1 = 66, 22, 290, 100
    d.rectangle([x0 + 3, y0 + 3, x1 + 3, y1 + 3], fill=(20, 50, 70))
    d.rectangle([x0, y0, x1, y1], fill=(235, 238, 242), outline=(20, 30, 40))
    d.rectangle([x0, y0, x1, y0 + 12], fill=(40, 90, 170))
    d.text((x0 + 5, y0 + 1), "EstagiOS", font=fb, fill=(255, 255, 255))
    d.rectangle([x1 - 11, y0 + 2, x1 - 3, y0 + 10], fill=(220, 80, 70))
    d.text((x0 + 8, y0 + 20), "Sistema iniciado com sucesso!", font=font(9), fill=(20, 40, 60))
    d.text((x0 + 8, y0 + 38), "Placa-mãe, memória, vídeo e HD: OK", font=f, fill=(50, 60, 70))
    d.rectangle([x0 + 85, y0 + 56, x0 + 139, y0 + 70], fill=(40, 90, 170))
    d.text((x0 + 105, y0 + 57), "OK", font=fb, fill=(255, 255, 255))

    # barra de tarefas
    d.rectangle([0, H - 14, W, H], fill=(25, 30, 40))
    d.rectangle([2, H - 12, 50, H - 2], fill=(60, 160, 90))
    d.text((5, H - 12), "Iniciar", font=font(9), fill=(255, 255, 255))
    d.rectangle([54, H - 12, 110, H - 2], fill=(55, 65, 85))
    d.text((58, H - 11), "EstagiOS", font=f, fill=(220, 225, 235))
    d.text((W - 34, H - 12), "09:00", font=fb, fill=(255, 255, 255))
    save(img, folder, "ui_tela_sistema.png")


if __name__ == "__main__":
    pasta = sys.argv[1] if len(sys.argv) > 1 else "."
    os.makedirs(pasta, exist_ok=True)
    bios(pasta)
    sistema(pasta)
