"""Mobiliário da sala de TI e da área das impressoras (etapa 1 do escritório).

Gera 4 modelos, cada um um .fbx próprio em Assets/Art/Models (origem no chão, no centro; frente para -Y do Blender,
que vira +Z local no Unity depois do giro do filho Visual feito pelo MobiliarioEscritorioSetup.cs):
  - bancada_manutencao.fbx   bancada de manutenção 2,0 x 0,8 m, tampo a 0,90 m, tapete antiestático, prateleira baixa,
                             painel perfurado com ferramentas penduradas, régua de tomadas e prateleira alta
  - armario_ti.fbx           armário de aço de 2 portas 1,0 x 0,5 x 1,95 m (estoque fechado)
  - estante_estoque.fbx      estante aberta de aço 1,8 x 0,5 x 2,03 m, 4 níveis (tampos a 0,125/0,925/1,525/2,005 m)
  - bancada_impressoras.fbx  bancada organizadora 1,6 x 0,6 x 0,78 m: portas, nicho para resmas e 3 gavetas

Materiais = 4 slots nomeados; o setup do Unity troca cada slot pelo material do Matheus correspondente:
  Claro -> M_Escritorio_Claro, Escuro -> M_Escritorio_Escuro, Metal -> M_PC_Chapa, Tapete -> M_Impressora_Escuro.
As medidas usadas pelos colisores do Unity estão repetidas no MobiliarioEscritorioSetup.cs (manter iguais).

Uso:
  blender -b --python Tools/blender/mobiliario_escritorio_build.py -- <pasta .blend> <pasta .fbx>
"""
import math
import os
import sys

import bmesh
import bpy

SLOTS = ("Claro", "Escuro", "Metal", "Tapete")
CORES = {"Claro": (0.86, 0.84, 0.80, 1), "Escuro": (0.25, 0.26, 0.30, 1),
         "Metal": (0.62, 0.64, 0.68, 1), "Tapete": (0.12, 0.13, 0.16, 1)}


def mats():
    for n in SLOTS:
        m = bpy.data.materials.get(n) or bpy.data.materials.new(n)
        m.diffuse_color = CORES[n]


class Peca:
    """Acumula caixas/cilindros (com chanfro leve, visual cartunizado) num único objeto com vários slots."""

    def __init__(self, nome, slots=SLOTS):
        self.nome = nome
        self.slots = slots
        self.bm = bmesh.new()
        self.mat_idx = {n: i for i, n in enumerate(slots)}

    def _faces_novas(self, antes):
        return [f for f in self.bm.faces if f.index == -1 or f not in antes]

    def caixa(self, x0, y0, z0, x1, y1, z1, mat, chanfro=0.006):
        antes = set(self.bm.faces)
        r = bmesh.ops.create_cube(self.bm, size=1.0)
        vs = r["verts"]
        sx, sy, sz = x1 - x0, y1 - y0, z1 - z0
        for v in vs:
            v.co.x = (x0 + x1) / 2 + v.co.x * sx
            v.co.y = (y0 + y1) / 2 + v.co.y * sy
            v.co.z = (z0 + z1) / 2 + v.co.z * sz
        c = min(chanfro, sx / 3, sy / 3, sz / 3)
        if c > 0.0015:
            edges = list({e for v in vs for e in v.link_edges})
            bmesh.ops.bevel(self.bm, geom=edges, offset=c, segments=1, affect='EDGES', profile=0.5)
        for f in self.bm.faces:
            if f not in antes:
                f.material_index = self.mat_idx[mat]

    def cilindro(self, cx, cy, z0, z1, raio, mat, lados=12, eixo='Z'):
        antes = set(self.bm.faces)
        r = bmesh.ops.create_cone(self.bm, cap_ends=True, segments=lados, radius1=raio, radius2=raio, depth=z1 - z0)
        for v in r["verts"]:
            if eixo == 'Z':
                v.co.x += cx; v.co.y += cy; v.co.z += (z0 + z1) / 2
            elif eixo == 'X':     # cilindro deitado ao longo de X: (cx, cy) = (y, z) do centro, z0..z1 = faixa em X
                x, y, z = v.co.z, v.co.y, v.co.x
                v.co.x = (z0 + z1) / 2 + x; v.co.y = cx + y; v.co.z = cy + z
        for f in self.bm.faces:
            if f not in antes:
                f.material_index = self.mat_idx[mat]

    def barra(self, a, b, larg, esp, mat):
        """Barra reta de seção larg x esp entre os pontos a e b (3D), para travamentos em diagonal."""
        from mathutils import Vector
        antes = set(self.bm.faces)
        a, b = Vector(a), Vector(b)
        d = b - a
        r = bmesh.ops.create_cube(self.bm, size=1.0)
        rot = d.to_track_quat('X', 'Z').to_matrix()
        for v in r["verts"]:
            v.co = rot @ Vector((v.co.x * d.length, v.co.y * larg, v.co.z * esp)) + (a + b) / 2
        for f in self.bm.faces:
            if f not in antes:
                f.material_index = self.mat_idx[mat]

    def objeto(self):
        me = bpy.data.meshes.new(self.nome)
        bmesh.ops.remove_doubles(self.bm, verts=self.bm.verts, dist=1e-5)
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces)
        self.bm.to_mesh(me)
        self.bm.free()
        for n in self.slots:
            me.materials.append(bpy.data.materials[n])
        ob = bpy.data.objects.new(self.nome, me)
        bpy.context.scene.collection.objects.link(ob)
        for p in me.polygons:
            p.use_smooth = False
        return ob


# ------------------------------------------------------------------ bancada de manutenção

def bancada_manutencao():
    p = Peca("Bancada_Manutencao")
    W, D, H = 2.0, 0.8, 0.90
    hx, hy = W / 2, D / 2
    t = 0.05                                     # tubo das pernas
    # tampo + tapete antiestático
    p.caixa(-hx, -hy, H - 0.04, hx, hy, H, "Claro", 0.008)
    p.caixa(-0.75, -0.33, H, 0.55, 0.22, H + 0.004, "Tapete", 0.002)
    # pernas e quadro
    for sx in (-1, 1):
        for sy in (-1, 1):
            x = sx * (hx - 0.04 - t / 2); y = sy * (hy - 0.04 - t / 2)
            p.caixa(x - t / 2, y - t / 2, 0, x + t / 2, y + t / 2, H - 0.04, "Metal", 0.004)
            p.caixa(x - 0.045, y - 0.045, 0, x + 0.045, y + 0.045, 0.015, "Escuro", 0.003)   # sapata
    for sy in (-1, 1):
        y = sy * (hy - 0.065)
        p.caixa(-hx + 0.04, y - 0.02, H - 0.10, hx - 0.04, y + 0.02, H - 0.04, "Metal", 0.004)
        p.caixa(-hx + 0.04, y - 0.015, 0.12, hx - 0.04, y + 0.015, 0.15, "Metal", 0.004)
    for sx in (-1, 1):
        x = sx * (hx - 0.065)
        p.caixa(x - 0.02, -hy + 0.04, H - 0.10, x + 0.02, hy - 0.04, H - 0.04, "Metal", 0.004)
        p.caixa(x - 0.015, -hy + 0.04, 0.12, x + 0.015, hy - 0.04, 0.15, "Metal", 0.004)
    # prateleira baixa
    p.caixa(-hx + 0.09, -hy + 0.09, 0.15, hx - 0.09, hy - 0.09, 0.175, "Escuro", 0.004)
    # painel perfurado nos fundos (em +Y = fundos), montantes e prateleira alta
    yb = hy - 0.03
    p.caixa(-hx, yb, H, hx, hy, 1.62, "Escuro", 0.005)
    for sx in (-1, 1):
        x = sx * (hx - 0.025)
        p.caixa(x - 0.025, yb - 0.02, H, x + 0.025, hy, 1.72, "Metal", 0.004)
    p.caixa(-hx, hy - 0.28, 1.62, hx, hy, 1.645, "Claro", 0.005)
    for sx in (-0.6, 0.0, 0.6):                   # mãos-francesas da prateleira
        p.caixa(sx - 0.012, hy - 0.26, 1.54, sx + 0.012, yb, 1.62, "Metal", 0.003)
    # furos do painel (pinos claros, em grade)
    for i in range(19):
        for j in range(6):
            x = -0.9 + i * 0.1; z = H + 0.08 + j * 0.105
            if -0.75 < x < 0.55 and 1.12 < z < 1.5:
                continue                          # área das ferramentas
            p.caixa(x - 0.007, yb - 0.003, z - 0.007, x + 0.007, yb, z + 0.007, "Metal", 0)
    # régua de tomadas (sobre o tampo, encostada no painel)
    p.caixa(0.62, yb - 0.05, H + 0.02, 0.95, yb, H + 0.07, "Claro", 0.006)
    for k in range(4):
        x = 0.66 + k * 0.075
        p.caixa(x, yb - 0.053, H + 0.035, x + 0.035, yb - 0.049, H + 0.058, "Tapete", 0)
    p.caixa(0.92, yb - 0.054, H + 0.04, 0.94, yb - 0.049, H + 0.052, "Tapete", 0)   # interruptor
    # ferramentas penduradas: 2 chaves de fenda, alicate, chave inglesa e rolo de cabo
    for x, cor in ((-0.65, "Tapete"), (-0.55, "Claro")):
        p.caixa(x - 0.015, yb - 0.035, 1.40, x + 0.015, yb - 0.005, 1.46, "Metal", 0.003)   # gancho
        p.cilindro(x, yb - 0.03, 1.33, 1.43, 0.016, cor, 10)                                   # cabo
        p.cilindro(x, yb - 0.03, 1.18, 1.33, 0.004, "Metal", 6)                                # haste
    x = -0.38                                                                                    # alicate
    p.caixa(x - 0.012, yb - 0.035, 1.42, x + 0.012, yb - 0.005, 1.47, "Metal", 0.003)
    for s in (-1, 1):
        p.caixa(x + s * 0.02 - 0.009, yb - 0.035, 1.24, x + s * 0.02 + 0.009, yb - 0.02, 1.38, "Tapete", 0.003)
    p.caixa(x - 0.022, yb - 0.035, 1.38, x + 0.022, yb - 0.02, 1.43, "Metal", 0.004)
    x = -0.18                                                                                    # chave inglesa
    p.caixa(x - 0.012, yb - 0.035, 1.43, x + 0.012, yb - 0.005, 1.47, "Metal", 0.003)
    p.caixa(x - 0.012, yb - 0.03, 1.24, x + 0.012, yb - 0.02, 1.42, "Metal", 0.003)
    p.caixa(x - 0.03, yb - 0.03, 1.38, x + 0.03, yb - 0.02, 1.43, "Metal", 0.004)
    # rolo de cabo de rede (anel) pendurado
    cx, cz = 0.22, 1.30
    p.caixa(cx - 0.015, yb - 0.04, cz + 0.11, cx + 0.015, yb - 0.005, cz + 0.15, "Metal", 0.003)
    n = 24
    for k in range(n):
        for r, dy in ((0.12, 0.032), (0.105, 0.026)):      # duas voltas de cabo
            a0 = 2 * math.pi * k / n; a1 = 2 * math.pi * (k + 1.1) / n
            p.barra((cx + r * math.cos(a0), yb - dy, cz + r * math.sin(a0)),
                    (cx + r * math.cos(a1), yb - dy, cz + r * math.sin(a1)), 0.014, 0.014, "Tapete")
    # luminária de bancada sob a prateleira alta
    p.caixa(-0.8, hy - 0.2, 1.585, 0.8, hy - 0.12, 1.615, "Metal", 0.006)
    p.caixa(-0.78, hy - 0.195, 1.578, 0.78, hy - 0.125, 1.586, "Claro", 0)
    return p.objeto()


# ------------------------------------------------------------------ armário de TI

def armario_ti():
    p = Peca("Armario_TI")
    W, D, H = 1.0, 0.5, 1.95
    hx, hy = W / 2, D / 2
    p.caixa(-hx, -hy + 0.02, 0.06, hx, hy, H, "Claro", 0.01)            # corpo
    p.caixa(-hx + 0.02, -hy + 0.03, 0, hx - 0.02, hy - 0.02, 0.06, "Escuro", 0.004)   # rodapé
    p.caixa(-hx - 0.005, -hy + 0.02, H - 0.03, hx + 0.005, hy + 0.005, H + 0.01, "Claro", 0.008)  # tampo
    g = 0.004
    for s in (-1, 1):                                                    # 2 portas (frente em -Y)
        x0, x1 = (-hx + 0.02, -g) if s < 0 else (g, hx - 0.02)
        p.caixa(x0, -hy, 0.09, x1, -hy + 0.022, H - 0.05, "Claro", 0.006)
        xm = -0.05 if s < 0 else 0.05                                     # puxador vertical
        p.caixa(xm - 0.012, -hy - 0.03, 0.85, xm + 0.012, -hy - 0.012, 1.15, "Metal", 0.005)
        for z in (0.88, 1.12):
            p.caixa(xm - 0.01, -hy - 0.015, z - 0.01, xm + 0.01, -hy, z + 0.01, "Metal", 0)
        xc = (x0 + x1) / 2                                                # venezianas no alto e embaixo
        for k in range(5):
            for zb in (1.62, 0.25):
                z = zb + k * 0.03
                p.caixa(xc - 0.15, -hy - 0.004, z, xc + 0.15, -hy, z + 0.012, "Escuro", 0)
    p.cilindro(0.0, -hy - 0.006, 1.22, 1.24, 0.018, "Metal", 12)          # fechadura (só visual)
    p.caixa(-0.16, -hy - 0.004, 1.74, 0.16, -hy, 1.83, "Metal", 0.004)    # plaqueta
    p.caixa(-0.13, -hy - 0.006, 1.765, 0.13, -hy - 0.004, 1.805, "Escuro", 0)
    return p.objeto()


# ------------------------------------------------------------------ estante de estoque

ESTANTE_NIVEIS = (0.10, 0.90, 1.50, 1.98)        # base de cada tampo (tampo = +0,025)


def estante_estoque():
    p = Peca("Estante_Estoque")
    W, D = 1.8, 0.5
    hx, hy = W / 2, D / 2
    a = 0.04                                      # cantoneira
    H = ESTANTE_NIVEIS[-1] + 0.05
    for sx in (-1, 1):
        for sy in (-1, 1):
            x = sx * (hx - a / 2); y = sy * (hy - a / 2)
            p.caixa(x - a / 2, y - a / 2, 0, x + a / 2, y + a / 2, H, "Metal", 0.003)
            p.caixa(x - 0.035, y - 0.035, 0, x + 0.035, y + 0.035, 0.012, "Escuro", 0.003)
        # furação das colunas (frente)
        for k in range(26):
            z = 0.2 + k * 0.07
            x = sx * (hx - a / 2)
            p.caixa(x - 0.006, -hy - 0.002, z, x + 0.006, -hy + 0.001, z + 0.025, "Escuro", 0)
    for z in ESTANTE_NIVEIS:
        p.caixa(-hx + 0.005, -hy + 0.005, z, hx - 0.005, hy - 0.005, z + 0.025, "Claro", 0.004)
        for sy in (-1, 1):                         # aba de aço na frente e atrás
            y = sy * (hy - 0.012)
            p.caixa(-hx + a, y - 0.012, z - 0.03, hx - a, y + 0.012, z + 0.005, "Metal", 0.003)
    # travamento em X nos fundos e nas laterais
    yb = hy - 0.006
    p.barra((-hx + a, yb, 0.15), (hx - a, yb, 1.45), 0.008, 0.016, "Metal")
    p.barra((-hx + a, yb, 1.45), (hx - a, yb, 0.15), 0.008, 0.016, "Metal")
    for sx in (-1, 1):
        x = sx * (hx - 0.006)
        p.barra((x, -hy + a, 0.15), (x, hy - a, 0.85), 0.008, 0.016, "Metal")
        p.barra((x, -hy + a, 0.85), (x, hy - a, 0.15), 0.008, 0.016, "Metal")
    return p.objeto()


# ------------------------------------------------------------------ bancada organizadora (impressoras)

def bancada_impressoras():
    p = Peca("Bancada_Impressoras")
    W, D, H = 1.6, 0.6, 0.78
    hx, hy = W / 2, D / 2
    e = 0.025                                                             # espessura das chapas
    p.caixa(-hx - 0.01, -hy - 0.015, H - 0.03, hx + 0.01, hy, H, "Claro", 0.008)        # tampo
    p.caixa(-hx + 0.02, -hy + 0.04, 0, hx - 0.02, hy - 0.02, 0.07, "Escuro", 0.004)     # rodapé
    p.caixa(-hx, -hy + 0.02, 0.07, -hx + e, hy, H - 0.03, "Claro", 0.005)               # laterais
    p.caixa(hx - e, -hy + 0.02, 0.07, hx, hy, H - 0.03, "Claro", 0.005)
    p.caixa(-hx, hy - e, 0.07, hx, hy, H - 0.03, "Claro", 0.005)                        # fundo
    p.caixa(-hx, -hy + 0.02, 0.07, hx, hy, 0.07 + e, "Claro", 0.005)                    # base
    x1, x2 = -0.27, 0.27                                                  # divisórias: portas | nicho | gavetas
    for x in (x1, x2):
        p.caixa(x - e / 2, -hy + 0.02, 0.07, x + e / 2, hy, H - 0.03, "Claro", 0.004)
    # módulo esquerdo: 2 portas
    g = 0.004
    xm = (-hx + x1) / 2
    for s, (a0, a1) in ((-1, (-hx + 0.005, xm - g)), (1, (xm + g, x1 - 0.005))):
        p.caixa(a0, -hy, 0.08, a1, -hy + 0.02, H - 0.04, "Claro", 0.005)
        xp = xm + s * 0.035
        p.caixa(xp - 0.01, -hy - 0.025, 0.47, xp + 0.01, -hy - 0.008, 0.65, "Metal", 0.004)
    # nicho central aberto com prateleira (resmas): interior escuro
    p.caixa(x1 + e / 2, hy - e - 0.004, 0.07 + e, x2 - e / 2, hy - e, H - 0.03, "Escuro", 0)
    p.caixa(x1 + e / 2, -hy + 0.03, 0.40, x2 - e / 2, hy - e, 0.40 + e, "Claro", 0.004)
    # módulo direito: 3 gavetas
    alturas = ((0.08, 0.30), (0.31, 0.53), (0.54, H - 0.04))
    for z0, z1 in alturas:
        p.caixa(x2 + 0.005, -hy, z0, hx - 0.005, -hy + 0.02, z1, "Claro", 0.005)
        xc = (x2 + hx) / 2; zc = z1 - 0.05
        p.caixa(xc - 0.09, -hy - 0.025, zc - 0.01, xc + 0.09, -hy - 0.008, zc + 0.01, "Metal", 0.004)
        p.caixa(xc - 0.05, -hy - 0.003, z0 + 0.04, xc + 0.05, -hy, z0 + 0.075, "Metal", 0)   # porta-etiqueta
    # passa-cabos no tampo (fundos)
    p.cilindro(0.55, hy - 0.08, H, H + 0.004, 0.03, "Escuro", 14)
    return p.objeto()


def main():
    args = sys.argv[sys.argv.index("--") + 1:]
    out_blend_dir, out_fbx_dir = map(os.path.abspath, args[:2])
    os.makedirs(out_blend_dir, exist_ok=True)
    for fn, nome in ((bancada_manutencao, "bancada_manutencao"), (armario_ti, "armario_ti"),
                     (estante_estoque, "estante_estoque"), (bancada_impressoras, "bancada_impressoras")):
        bpy.ops.wm.read_factory_settings(use_empty=True)
        mats()
        ob = fn()
        dims = ob.dimensions
        print("MODELO", nome, "dims", tuple(round(d, 3) for d in dims), "faces", len(ob.data.polygons))
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(out_blend_dir, nome + ".blend"))
        bpy.ops.export_scene.fbx(
            filepath=os.path.join(out_fbx_dir, nome + ".fbx"), use_selection=False, object_types={'MESH'},
            apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
            bake_space_transform=True, mesh_smooth_type='FACE', use_tspace=False, add_leaf_bones=False,
            bake_anim=False, path_mode='STRIP')
        print("EXPORTADO", nome)


if __name__ == "__main__":
    main()
