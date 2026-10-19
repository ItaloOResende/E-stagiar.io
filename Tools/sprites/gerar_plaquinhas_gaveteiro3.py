"""Plaquinhas das gavetas que juntam duas gavetas antigas (gaveteiros de 3 gavetas, gavetas.fbx do Matheus).
Mesma tipografia e desenho das plaquinhas versionadas de suprimentos (toner, papel A4, suprimentos): a função
plaquinha() de gerar_plaquinhas_suprimentos.py (cartão creme, borda arredondada e texto em Comic Sans Bold na cor da
categoria). Só as cores mudam, para não confundir com as gavetas antigas.
Uso: python Tools/sprites/gerar_plaquinhas_gaveteiro3.py
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from gerar_plaquinhas_suprimentos import plaquinha, SAIDA, LARANJA  # noqa: E402

ROXO = (140, 82, 200, 255)
VERMELHO = (214, 64, 64, 255)

PLAQUINHAS = {
    "placa_ram_cooler": ("PLACA-MÃE, RAM E COOLER", ROXO),
    "video": ("CABOS VGA E HDMI", VERMELHO),
    "memoria_ssd_pendrive": ("MEMÓRIA, SSD E PENDRIVE", LARANJA),
}

if __name__ == "__main__":
    for chave, (texto, cor) in PLAQUINHAS.items():
        plaquinha(texto, cor).save(os.path.join(SAIDA, "plaquinha_" + chave + ".png"))
        print("plaquinha_" + chave + ".png")
