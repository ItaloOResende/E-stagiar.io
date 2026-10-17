"""
Gera as telas do monitor no mesmo formato da tela_no_signal do legado:
BIOS sem armazenamento (PH_ui_tela_bios), BIOS com HD sem sistema (PH_ui_tela_bios_sem_sistema), instalação do
sistema pelo pendrive em etapas (PH_ui_tela_instalando_0..4), sistema sem rede / com rede (PH_ui_tela_sistema[_rede])
e "Sem sinal" do monitor sem cabo de vídeo (PH_ui_tela_sem_sinal),
640 x 320, pixel art (desenhado em 320 x 160 e ampliado 2x sem filtro).

Perfil "notebook" (PH_ui_tela_notebook_*): as MESMAS telas com a terminologia do notebook do minigame — SSD (M.2) no
lugar do HD, memória SO-DIMM, tela integrada no lugar da saída VGA/HDMI, boot pelo pendrive (USB) e, no sistema, sem
aviso de cabo de rede (o notebook não tem porta de rede nem mecânica de Wi-Fi: o ícone da bandeja é o de Wi-Fi, neutro).

Criadas para o projeto, sem arte de terceiros. Fonte: DejaVu Sans Mono (licença livre Bitstream Vera/DejaVu,
permite uso e redistribuição), a cópia que acompanha o Blender; só os pixels do texto vão para a imagem. Marcas e nomes são fictícios (sem logotipos reais de BIOS ou sistema operacional).

Uso:  python Tools/screens/gerar_telas_monitor.py Assets/Art/UI
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


# Terminologia por equipamento. "pc" = telas do gabinete (inalteradas); "notebook" = minigame do notebook.
PERFIS = {
    "pc": dict(
        prefixo="PH_ui_tela_", ventoinha=("Cooler da CPU", "1200 RPM  OK"), memoria=("Memória RAM", "4096 MB  OK"),
        video=("Saída de vídeo", "VGA/HDMI  OK"), disco="Disco rígido (HD)", disco_ok="DETECTADO  500 GB",
        ordem="Ordem de inicialização:  1. HD   2. Rede", sigla="HD", maquina="o computador",
        destino="HD 500 GB", pecas_ok="Placa-mãe, memória, vídeo e HD: OK"),
    "notebook": dict(
        prefixo="PH_ui_tela_notebook_", ventoinha=("Ventoinha", "2400 RPM  OK"), memoria=("Memória RAM (SO-DIMM)", "8192 MB  OK"),
        video=("Tela integrada", "OK"), disco="SSD (M.2)", disco_ok="DETECTADO  256 GB",
        ordem="Ordem de inicialização:  1. SSD   2. USB", sigla="SSD", maquina="o notebook",
        destino="SSD 256 GB", pecas_ok="Memória, tela e SSD: OK"),
}


def save(img, folder, name):
    img.resize((W * 2, H * 2), Image.NEAREST).save(os.path.join(folder, name), optimize=True)
    print("gerado", name)


def bios(folder, hd_detectado=False, perfil="pc"):
    """Sem HD/SSD: 'nenhum dispositivo de armazenamento'. Vazio: detectado, mas 'No bootable device found'."""
    t = PERFIS[perfil]
    azul, cinza, branco, amarelo, vermelho = (0, 0, 170), (170, 170, 170), (255, 255, 255), (255, 255, 85), (255, 85, 85)
    verde = (85, 255, 85)
    img, d = canvas(azul)
    f, fb = font(9), font(10)
    d.rectangle([0, 0, W, 13], fill=cinza)
    d.text((6, 1), "E-STAGIAR.IO BIOS  v1.0", font=fb, fill=azul)
    d.text((W - 92, 1), "Configuração", font=fb, fill=azul)
    d.rectangle([4, 18, W - 5, 115], outline=cinza)
    linhas = [
        ("Processador", "OK", branco),
        ("Placa-mãe", "OK", branco),
        (t["ventoinha"][0], t["ventoinha"][1], branco),
        (t["memoria"][0], t["memoria"][1], branco),
        (t["video"][0], t["video"][1], branco),
        (t["disco"], t["disco_ok"] if hd_detectado else "NÃO ENCONTRADO", verde if hd_detectado else vermelho),
    ]
    y = 24
    for nome, valor, cor in linhas:
        d.text((12, y), nome, font=f, fill=cinza)
        d.text((150, y), valor, font=f, fill=cor)
        y += 13
    d.text((12, y + 1), t["ordem"], font=f, fill=cinza)
    d.rectangle([0, 118, W, H], fill=(0, 0, 120))
    # Rodapé NEUTRO e igual nos dois casos: a tela não diz o que instalar nem o que fazer (o jogador investiga a
    # tabela acima e o próprio equipamento).
    d.text((8, 121), "Falha ao iniciar: nenhum sistema pôde ser carregado.", font=f, fill=amarelo)
    d.text((8, 134), "Verifique " + t["maquina"] + " e reinicie.", font=f, fill=branco)
    d.text((8, 147), "F10: Salvar e sair    Esc: Sair", font=f, fill=cinza)
    save(img, folder, t["prefixo"] + ("bios_sem_sistema.png" if hd_detectado else "bios.png"))


ETAPAS = ["Preparando a instalação...", "Copiando arquivos para o HD...", "Instalando recursos...",
          "Configurando o sistema...", "Concluindo a instalação..."]


def instalando(folder, etapa, perfil="pc"):
    """Instalação do EstagiOS pelo pendrive: mesma janela, barra e texto da etapa mudam (uma imagem por etapa)."""
    t = PERFIS[perfil]
    img, d = canvas((20, 60, 110))
    f, fb = font(9), font(10)
    x0, y0, x1, y1 = 30, 26, W - 30, H - 30
    d.rectangle([x0 + 3, y0 + 3, x1 + 3, y1 + 3], fill=(10, 30, 60))
    d.rectangle([x0, y0, x1, y1], fill=(235, 238, 242), outline=(20, 30, 40))
    d.rectangle([x0, y0, x1, y0 + 13], fill=(40, 90, 170))
    d.text((x0 + 5, y0 + 1), "Instalação do EstagiOS", font=fb, fill=(255, 255, 255))
    d.text((x0 + 10, y0 + 22), "Instalando o sistema operacional...", font=fb, fill=(20, 40, 60))
    d.text((x0 + 10, y0 + 38), "Origem: pendrive (USB)   Destino: " + t["destino"], font=f, fill=(70, 80, 90))
    n = len(ETAPAS)
    pct = int(round((etapa + 1) * 100 / n))
    bx0, by0, bx1, by1 = x0 + 10, y0 + 58, x1 - 10, y0 + 70
    d.rectangle([bx0, by0, bx1, by1], fill=(200, 205, 215), outline=(60, 70, 90))
    fill_w = int((bx1 - bx0 - 2) * (etapa + 1) / n)
    for i in range(0, fill_w, 6):                       # barra em blocos (pixel art)
        d.rectangle([bx0 + 2 + i, by0 + 2, min(bx0 + 2 + i + 4, bx0 + 1 + fill_w), by1 - 2], fill=(60, 160, 90))
    d.text((bx1 - 22, by1 + 3), f"{pct}%", font=f, fill=(20, 40, 60))
    d.text((x0 + 10, by1 + 3), ETAPAS[etapa].replace("o HD", "o " + t["sigla"]), font=f, fill=(50, 60, 70))
    d.text((x0 + 10, y1 - 13), "Não retire o pendrive nem desligue " + t["maquina"] + ".", font=font(8), fill=(170, 60, 50))
    save(img, folder, t["prefixo"] + f"instalando_{etapa}.png")


SEM_SINAL_W, SEM_SINAL_H = 320, 232  # proporção da tela do monitor (2D ~1,35, 3D ~1,41): o texto não deforma


def sem_sinal(folder):
    """
    PC ligado sem imagem chegando ao monitor: a tela inteira preta, com o aviso discreto do próprio monitor
    (só o sintoma, sem instruções). Feita na proporção da área do display (não 2:1 como as outras telas), porque o
    Canvas e o quad 3D esticam a imagem até a área da tela.
    """
    w, h = SEM_SINAL_W, SEM_SINAL_H
    img = Image.new("RGB", (w, h), (0, 0, 0))
    d = ImageDraw.Draw(img)
    d.fontmode = "1"

    def centro(y, texto, f, cor):
        x0_, _, x1_, _ = d.textbbox((0, 0), texto, font=f)
        d.text(((w - (x1_ - x0_)) // 2, y), texto, font=f, fill=cor)

    centro(h // 2 - 14, "Sem sinal", font(14), (210, 210, 210))
    centro(h // 2 + 6, "Nenhuma entrada de vídeo detectada", font(9), (140, 140, 140))
    img.resize((w * 2, h * 2), Image.NEAREST).save(os.path.join(folder, "PH_ui_tela_sem_sinal.png"), optimize=True)
    print("gerado PH_ui_tela_sem_sinal.png")


def sistema(folder, rede=False, perfil="pc"):
    termos = PERFIS[perfil]
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
    d.text((x0 + 8, y0 + 34), termos["pecas_ok"], font=f, fill=(50, 60, 70))
    d.rectangle([x0 + 85, y0 + 56, x0 + 139, y0 + 70], fill=(40, 90, 170))
    d.text((x0 + 105, y0 + 57), "OK", font=fb, fill=(255, 255, 255))

    # barra de tarefas
    d.rectangle([0, H - 14, W, H], fill=(25, 30, 40))
    d.rectangle([2, H - 12, 50, H - 2], fill=(60, 160, 90))
    d.text((5, H - 12), "Iniciar", font=font(9), fill=(255, 255, 255))
    d.rectangle([54, H - 12, 110, H - 2], fill=(55, 65, 85))
    d.text((58, H - 11), "EstagiOS", font=f, fill=(220, 225, 235))
    d.text((W - 34, H - 12), "09:00", font=fb, fill=(255, 255, 255))

    if perfil == "notebook":
        # Notebook: sem porta de rede e sem mecânica de Wi-Fi. Ícone de Wi-Fi neutro na bandeja e nenhum aviso de cabo
        # (não há o que o jogador corrigir aqui); o resto da tela é igual à do PC.
        nx, ny = W - 66, H - 4
        for r_ in (8, 5, 2):
            d.arc([nx - r_, ny - r_, nx + r_, ny + r_], 225, 315, fill=(200, 205, 215), width=1)
        d.rectangle([nx - 1, ny - 1, nx, ny], fill=(200, 205, 215))
        save(img, folder, termos["prefixo"] + "sistema.png")
        return

    # ícone de rede na bandeja: conectado (verde) ou sem rede (X vermelho)
    nx = W - 66
    d.rectangle([nx, H - 11, nx + 9, H - 5], fill=(200, 205, 215))
    d.rectangle([nx + 3, H - 5, nx + 6, H - 3], fill=(200, 205, 215))
    if rede:
        d.rectangle([nx + 1, H - 10, nx + 8, H - 6], fill=(70, 200, 110))
        d.text((x0 + 8, y0 + 46), "Rede: conectado", font=f, fill=(30, 130, 70))
    else:
        d.line([(nx + 11, H - 12), (nx + 17, H - 6)], fill=(235, 70, 60), width=2)
        d.line([(nx + 17, H - 12), (nx + 11, H - 6)], fill=(235, 70, 60), width=2)
        # Aviso grande, legível no monitor 3D de longe. Diz só o SINTOMA (sem rede), nunca a causa/solução: o mesmo
        # aviso vale para cabo solto, cabo defeituoso ou porta do rack desligada.
        ax0, ay0, ax1, ay1 = 92, 34, 262, 112
        d.rectangle([ax0 + 3, ay0 + 3, ax1 + 3, ay1 + 3], fill=(20, 30, 40))
        d.rectangle([ax0, ay0, ax1, ay1], fill=(250, 244, 240), outline=(20, 30, 40))
        d.rectangle([ax0, ay0, ax1, ay0 + 13], fill=(200, 55, 50))
        d.text((ax0 + 5, ay0 + 1), "Rede", font=fb, fill=(255, 255, 255))
        # ícone: dois computadores ligados por uma linha cortada por um X vermelho
        ix, iy = ax0 + 10, ay0 + 24
        for dx in (0, 30):
            d.rectangle([ix + dx, iy, ix + dx + 14, iy + 10], fill=(60, 80, 110), outline=(20, 30, 40))
            d.rectangle([ix + dx + 5, iy + 11, ix + dx + 9, iy + 13], fill=(60, 80, 110))
        d.line([(ix + 14, iy + 5), (ix + 30, iy + 5)], fill=(90, 100, 110), width=1)
        d.line([(ix + 18, iy + 1), (ix + 26, iy + 9)], fill=(220, 50, 45), width=3)
        d.line([(ix + 26, iy + 1), (ix + 18, iy + 9)], fill=(220, 50, 45), width=3)
        d.text((ax0 + 60, ay0 + 20), "Sem acesso", font=font(12), fill=(180, 40, 35))
        d.text((ax0 + 60, ay0 + 34), "à rede", font=font(12), fill=(180, 40, 35))
        d.text((ax0 + 10, ay0 + 52), "Internet e arquivos", font=f, fill=(50, 60, 70))
        d.text((ax0 + 10, ay0 + 62), "compartilhados indisponíveis.", font=f, fill=(50, 60, 70))
    save(img, folder, "PH_ui_tela_sistema_rede.png" if rede else "PH_ui_tela_sistema.png")


if __name__ == "__main__":
    pasta = sys.argv[1] if len(sys.argv) > 1 else "."
    os.makedirs(pasta, exist_ok=True)
    bios(pasta)
    bios(pasta, hd_detectado=True)
    for i in range(len(ETAPAS)):
        instalando(pasta, i)
    sistema(pasta)
    sistema(pasta, rede=True)
    sem_sinal(pasta)
    # notebook (minigame): BIOS, sem sistema, instalação e sistema com a terminologia dele
    bios(pasta, perfil="notebook")
    bios(pasta, hd_detectado=True, perfil="notebook")
    for i in range(len(ETAPAS)):
        instalando(pasta, i, perfil="notebook")
    sistema(pasta, perfil="notebook")
