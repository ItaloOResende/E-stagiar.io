"""Carrinho técnico do rack de rede (sala de TI).

Gera Assets/Art/Models/carrinho_rack.fbx (origem no chão, no centro; frente em -Y do Blender = +Z local no Unity,
como no mobiliario_escritorio_build.py):
  - estrutura metálica preta: 4 colunas, tampo com borda (onde o RackInternet fica apoiado, a 0,80 m)
  - gaveta logo abaixo do tampo (puxador metálico) e prateleira do meio aberta
  - compartimento inferior fechado com porta e puxador
  - 4 rodízios giratórios (garfo + roda de borracha) e alça lateral
Tampo útil: 0,70 x 0,62 m. Slots: Preto (estrutura), Metal (puxadores, garfos, alça), Escuro (rodas, frisos).

Uso:
  blender -b --python Tools/blender/carrinho_rack_build.py -- <pasta .blend> <pasta .fbx>
"""
import os
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import mobiliario_escritorio_build as mob  # noqa: E402

SLOTS = ("Preto", "Metal", "Escuro")
CORES = {"Preto": (0.07, 0.07, 0.08, 1), "Metal": (0.62, 0.63, 0.66, 1), "Escuro": (0.18, 0.18, 0.20, 1)}

W, D = 0.70, 0.62      # tampo
TOPO = 0.80            # altura do tampo (superfície)
RODA = 0.11            # altura até a base do chassi (rodízios)


def mats():
    for n in SLOTS:
        m = bpy.data.materials.get(n) or bpy.data.materials.new(n)
        m.diffuse_color = CORES[n]


def carrinho():
    p = mob.Peca("Carrinho_Rack", SLOTS)
    hx, hy = W / 2, D / 2
    col = 0.035
    # colunas
    for sx in (-1, 1):
        for sy in (-1, 1):
            x, y = sx * (hx - col / 2), sy * (hy - col / 2)
            p.caixa(x - col / 2, y - col / 2, RODA, x + col / 2, y + col / 2, TOPO - 0.03, "Preto", 0.004)
    # tampo com borda (lip) em volta: o rack apoia em TOPO
    p.caixa(-hx - 0.01, -hy - 0.01, TOPO - 0.03, hx + 0.01, hy + 0.01, TOPO, "Preto", 0.006)
    for (x0, y0, x1, y1) in ((-hx - 0.01, -hy - 0.01, hx + 0.01, -hy + 0.01), (-hx - 0.01, hy - 0.01, hx + 0.01, hy + 0.01),
                             (-hx - 0.01, -hy - 0.01, -hx + 0.01, hy + 0.01), (hx - 0.01, -hy - 0.01, hx + 0.01, hy + 0.01)):
        p.caixa(x0, y0, TOPO, x1, y1, TOPO + 0.02, "Preto", 0.003)
    # gaveta superior (caixa sob o tampo) com frente, friso e puxador
    zg0, zg1 = TOPO - 0.16, TOPO - 0.03
    p.caixa(-hx + col, -hy + 0.02, zg0, hx - col, hy - col, zg1, "Preto", 0.004)
    p.caixa(-hx + col, -hy, zg0 + 0.005, hx - col, -hy + 0.02, zg1 - 0.005, "Preto", 0.004)
    p.caixa(-hx + col, -hy - 0.002, zg0 - 0.004, hx - col, -hy + 0.02, zg0 + 0.001, "Escuro", 0)
    p.caixa(-0.10, -hy - 0.03, (zg0 + zg1) / 2 - 0.008, 0.10, -hy - 0.005, (zg0 + zg1) / 2 + 0.008, "Metal", 0.004)
    for x in (-0.095, 0.095):
        p.caixa(x - 0.007, -hy - 0.012, (zg0 + zg1) / 2 - 0.008, x + 0.007, -hy, (zg0 + zg1) / 2 + 0.008, "Metal", 0.002)
    # prateleira do meio (aberta)
    zm = 0.44
    p.caixa(-hx + 0.005, -hy + 0.005, zm - 0.02, hx - 0.005, hy - 0.005, zm, "Preto", 0.004)
    # compartimento inferior fechado: laterais, fundo e porta com puxador
    zi0, zi1 = RODA + 0.02, 0.38
    p.caixa(-hx + 0.005, -hy + 0.005, RODA, hx - 0.005, hy - 0.005, zi0, "Preto", 0.004)          # piso
    p.caixa(-hx + 0.005, -hy + 0.02, zi0, -hx + 0.02, hy - 0.005, zi1, "Preto", 0.002)
    p.caixa(hx - 0.02, -hy + 0.02, zi0, hx - 0.005, hy - 0.005, zi1, "Preto", 0.002)
    p.caixa(-hx + 0.005, hy - 0.02, zi0, hx - 0.005, hy - 0.005, zi1, "Preto", 0.002)
    p.caixa(-hx + 0.005, -hy + 0.005, zi1, hx - 0.005, hy - 0.005, zi1 + 0.02, "Preto", 0.004)      # teto
    p.caixa(-hx + col, -hy, zi0 + 0.005, hx - col, -hy + 0.02, zi1 - 0.005, "Preto", 0.004)          # porta
    p.caixa(-hx + col + 0.02, -hy - 0.002, zi0 + 0.03, hx - col - 0.02, -hy + 0.005, zi1 - 0.03, "Escuro", 0)
    p.caixa(hx - col - 0.07, -hy - 0.03, (zi0 + zi1) / 2 - 0.06, hx - col - 0.055, -hy - 0.005, (zi0 + zi1) / 2 + 0.06, "Metal", 0.003)
    # alça lateral (lado +X)
    for z in (TOPO - 0.08, TOPO - 0.26):
        p.caixa(hx, -0.18, z - 0.01, hx + 0.06, -0.16, z + 0.01, "Metal", 0.003)
        p.caixa(hx, 0.16, z - 0.01, hx + 0.06, 0.18, z + 0.01, "Metal", 0.003)
    p.cilindro(hx + 0.05, -0.18, TOPO - 0.27, TOPO - 0.07, 0.012, "Metal", 10)
    p.cilindro(hx + 0.05, 0.18, TOPO - 0.27, TOPO - 0.07, 0.012, "Metal", 10)
    p.caixa(hx + 0.038, -0.18, TOPO - 0.09, hx + 0.062, 0.18, TOPO - 0.07, "Metal", 0.004)
    # rodízios: placa, garfo e roda (eixo X)
    for sx in (-1, 1):
        for sy in (-1, 1):
            x, y = sx * (hx - 0.06), sy * (hy - 0.06)
            p.caixa(x - 0.04, y - 0.04, RODA - 0.012, x + 0.04, y + 0.04, RODA, "Metal", 0.003)
            p.cilindro(x, y, RODA - 0.03, RODA - 0.012, 0.012, "Metal", 10)
            for dx in (-0.024, 0.018):
                p.caixa(x + dx, y - 0.03, 0.035, x + dx + 0.006, y + 0.012, RODA - 0.028, "Metal", 0.002)
            p.cilindro(y - 0.01, 0.04, x - 0.017, x + 0.017, 0.04, "Escuro", 16, eixo='X')
            p.cilindro(y - 0.01, 0.04, x - 0.019, x + 0.019, 0.012, "Metal", 10, eixo='X')
    return p.objeto()


def main():
    args = sys.argv[sys.argv.index("--") + 1:]
    out_blend_dir, out_fbx_dir = map(os.path.abspath, args[:2])
    os.makedirs(out_blend_dir, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    mats()
    ob = carrinho()
    print("MODELO carrinho_rack dims", tuple(round(d, 3) for d in ob.dimensions), "faces", len(ob.data.polygons))
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(out_blend_dir, "carrinho_rack.blend"))
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(out_fbx_dir, "carrinho_rack.fbx"), use_selection=False, object_types={'MESH'},
        apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
        bake_space_transform=True, mesh_smooth_type='FACE', use_tspace=False, add_leaf_bones=False,
        bake_anim=False, path_mode='STRIP')
    print("EXPORTADO carrinho_rack")


if __name__ == "__main__":
    main()
