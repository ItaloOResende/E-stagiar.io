"""Complementos da sala de TI (ajustes da etapa 1): segunda bancada e peças de parede.

Gera, em Assets/Art/Models (origem e frente como no mobiliario_escritorio_build.py: frente em -Y do Blender = +Z
local no Unity):
  - bancada_armarios.fbx   bancada 1,8 x 0,6 m, tampo a 0,90 m, com armário de 2 portas, 1 porta e 4 gavetas embaixo
                           (origem no chão, no centro)
  - quadro_retrato.fbx     moldura com imagem 0,40 x 0,571 m (cartaz 840 x 1200)
  - quadro_paisagem.fbx    moldura com imagem 0,60 x 0,42 m (quadro 1200 x 840)
  - calendario.fbx         calendário de parede com espiral, imagem 0,32 x 0,48 m (720 x 1080)
  - placa.fbx              placa com espaçadores, imagem 0,60 x 0,20 m (1200 x 400)
  Peças de parede: origem no centro do fundo (encostado na parede, em Y = 0).

Slots: Claro, Escuro, Metal (como no mobiliário) e Imagem (face com UV 0..1, recebe a textura de
Assets/Art/Textures/Quadros, gerada por Tools/textures/gerar_quadros_ti.py).

Uso:
  blender -b --python Tools/blender/sala_ti_complementos_build.py -- <pasta .blend> <pasta .fbx>
"""
import os
import sys

import bmesh
import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import mobiliario_escritorio_build as mob  # noqa: E402

SLOTS = ("Claro", "Escuro", "Metal", "Imagem")
CORES = dict(mob.CORES, Imagem=(1, 1, 1, 1))


def mats():
    for n in SLOTS:
        m = bpy.data.materials.get(n) or bpy.data.materials.new(n)
        m.diffuse_color = CORES[n]


class Peca(mob.Peca):
    def __init__(self, nome):
        super().__init__(nome, SLOTS)

    def imagem(self, x0, z0, x1, z1, y):
        """Quad virado para -Y (frente) com UV 0..1 (u para +X, v para +Z)."""
        uv = self.bm.loops.layers.uv.verify()
        vs = [self.bm.verts.new(c) for c in ((x0, y, z0), (x1, y, z0), (x1, y, z1), (x0, y, z1))]
        f = self.bm.faces.new(vs)
        f.material_index = self.mat_idx["Imagem"]
        for loop in f.loops:
            loop[uv].uv = ((loop.vert.co.x - x0) / (x1 - x0), (loop.vert.co.z - z0) / (z1 - z0))
        if f.normal.y > 0:
            f.normal_flip()
        return f

    def objeto(self):
        # o recalc de normais do pai pode virar o quad solto; a imagem é refeita olhando para -Y depois
        me = bpy.data.meshes.new(self.nome)
        bmesh.ops.remove_doubles(self.bm, verts=self.bm.verts, dist=1e-5)
        caixas = [f for f in self.bm.faces if f.material_index != self.mat_idx["Imagem"]]
        bmesh.ops.recalc_face_normals(self.bm, faces=caixas)
        for f in self.bm.faces:
            if f.material_index == self.mat_idx["Imagem"] and f.normal.y > 0:
                f.normal_flip()
        self.bm.to_mesh(me)
        self.bm.free()
        for n in self.slots:
            me.materials.append(bpy.data.materials[n])
        ob = bpy.data.objects.new(self.nome, me)
        bpy.context.scene.collection.objects.link(ob)
        return ob


# ------------------------------------------------------------------ bancada com armários

def bancada_armarios():
    p = Peca("Bancada_Armarios")
    W, D, H = 1.8, 0.6, 0.90
    hx, hy = W / 2, D / 2
    e = 0.025
    p.caixa(-hx - 0.01, -hy - 0.02, H - 0.035, hx + 0.01, hy, H, "Claro", 0.008)          # tampo
    p.caixa(-hx + 0.03, -hy + 0.05, 0, hx - 0.03, hy - 0.02, 0.08, "Escuro", 0.004)       # rodapé recuado
    p.caixa(-hx, -hy + 0.02, 0.08, hx, hy, H - 0.035, "Claro", 0.006)                    # caixa do corpo
    g = 0.004
    zt, zb = H - 0.045, 0.09
    # módulo 1 (x -0,9..0): 2 portas; módulo 2 (0..0,45): 1 porta; módulo 3 (0,45..0,9): 4 gavetas
    for a0, a1, xp in ((-hx + 0.005, -0.45 - g, -0.45 - 0.04), (-0.45 + g, -g, -0.45 + 0.04), (g, 0.45 - g, 0.45 - 0.05)):
        p.caixa(a0, -hy, zb, a1, -hy + 0.02, zt, "Claro", 0.005)
        p.caixa(xp - 0.01, -hy - 0.025, zt - 0.30, xp + 0.01, -hy - 0.008, zt - 0.10, "Metal", 0.004)
    alt = (zt - zb) / 4
    for k in range(4):
        z0 = zb + k * alt + g; z1 = zb + (k + 1) * alt - g
        p.caixa(0.45 + g, -hy, z0, hx - 0.005, -hy + 0.02, z1, "Claro", 0.005)
        xc = (0.45 + hx) / 2; zc = z1 - 0.045
        p.caixa(xc - 0.08, -hy - 0.025, zc - 0.01, xc + 0.08, -hy - 0.008, zc + 0.01, "Metal", 0.004)
    # frisos escuros entre módulos (sombra das juntas)
    for x in (-0.45, 0.0, 0.45):
        p.caixa(x - 0.003, -hy - 0.001, zb, x + 0.003, -hy + 0.002, zt, "Escuro", 0)
    return p.objeto()


# ------------------------------------------------------------------ peças de parede

def quadro(nome, iw, ih, borda=0.03, prof=0.025):
    p = Peca(nome)
    hx, hz = iw / 2 + borda, ih / 2 + borda
    # moldura: 4 réguas + fundo
    p.caixa(-hx, -prof, -hz, hx, -0.004, -hz + borda, "Escuro", 0.004)
    p.caixa(-hx, -prof, hz - borda, hx, -0.004, hz, "Escuro", 0.004)
    p.caixa(-hx, -prof, -hz, -hx + borda, -0.004, hz, "Escuro", 0.004)
    p.caixa(hx - borda, -prof, -hz, hx, -0.004, hz, "Escuro", 0.004)
    p.caixa(-hx + 0.002, -0.012, -hz + 0.002, hx - 0.002, 0, hz - 0.002, "Claro", 0)
    p.imagem(-iw / 2, -ih / 2, iw / 2, ih / 2, -0.0125)
    return p.objeto()


def calendario():
    p = Peca("Calendario")
    iw, ih = 0.32, 0.48
    p.caixa(-iw / 2 - 0.005, -0.006, -ih / 2 - 0.01, iw / 2 + 0.005, 0, ih / 2 + 0.01, "Claro", 0.002)  # papelão
    p.imagem(-iw / 2, -ih / 2, iw / 2, ih / 2, -0.0065)
    for k in range(13):                                                   # espiral
        x = -iw / 2 + 0.02 + k * (iw - 0.04) / 12
        p.cilindro(-0.008, ih / 2 + 0.005, x - 0.003, x + 0.003, 0.009, "Metal", 8, eixo='X')
    p.caixa(-0.006, -0.008, ih / 2 + 0.044, 0.006, 0, ih / 2 + 0.056, "Metal", 0.002)   # prego (cabeça)
    p.caixa(-0.0025, -0.012, ih / 2 + 0.008, 0.0025, -0.004, ih / 2 + 0.05, "Metal", 0)   # fio até o prego
    return p.objeto()


def placa():
    p = Peca("Placa")
    iw, ih, esp = 0.60, 0.20, 0.012
    afast = 0.02                                                          # espaçadores de 2 cm
    p.caixa(-iw / 2, -afast - esp, -ih / 2, iw / 2, -afast, ih / 2, "Claro", 0.003)
    p.imagem(-iw / 2 + 0.002, -ih / 2 + 0.002, iw / 2 - 0.002, ih / 2 - 0.002, -afast - esp - 0.0008)
    for sx in (-1, 1):
        for sz in (-1, 1):
            x, z = sx * (iw / 2 - 0.03), sz * (ih / 2 - 0.03)
            p.caixa(x - 0.008, -afast - esp - 0.004, z - 0.008, x + 0.008, 0, z + 0.008, "Metal", 0.002)
    return p.objeto()


def main():
    args = sys.argv[sys.argv.index("--") + 1:]
    out_blend_dir, out_fbx_dir = map(os.path.abspath, args[:2])
    os.makedirs(out_blend_dir, exist_ok=True)
    pecas = ((bancada_armarios, "bancada_armarios"),
             (lambda: quadro("Quadro_Retrato", 0.40, 0.5714), "quadro_retrato"),
             (lambda: quadro("Quadro_Paisagem", 0.60, 0.42), "quadro_paisagem"),
             (calendario, "calendario"), (placa, "placa"))
    for fn, nome in pecas:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        mats()
        ob = fn()
        print("MODELO", nome, "dims", tuple(round(d, 3) for d in ob.dimensions), "faces", len(ob.data.polygons))
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(out_blend_dir, nome + ".blend"))
        bpy.ops.export_scene.fbx(
            filepath=os.path.join(out_fbx_dir, nome + ".fbx"), use_selection=False, object_types={'MESH'},
            apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
            bake_space_transform=True, mesh_smooth_type='FACE', use_tspace=False, add_leaf_bones=False,
            bake_anim=False, path_mode='STRIP')
        print("EXPORTADO", nome)


main()
