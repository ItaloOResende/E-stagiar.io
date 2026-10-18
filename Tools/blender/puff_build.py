"""Puff estofado baixo (sala de TI): almofada volumosa apoiada no chão, sem pés.

Gera Assets/Art/Models/puff.fbx (origem no centro da base, no chão; redondo, sem frente).
  - corpo: perfil "superelipse" girado (laterais abauladas, topo levemente afundado no centro), sombreado suave
  - vivos (costura em relevo) no alto e embaixo + botão de capitonê no centro do topo
Slots: Tecido (amarelo) e Costura (amarelo mais escuro); os materiais vêm do Unity (Materials/Mobiliario).

Uso:
  blender -b --python Tools/blender/puff_build.py -- <pasta .blend> <pasta .fbx>
"""
import math
import os
import sys

import bmesh
import bpy

SLOTS = ("Tecido", "Costura")
CORES = {"Tecido": (0.95, 0.76, 0.19, 1), "Costura": (0.78, 0.58, 0.10, 1)}

R = 0.27          # raio no "bojo"
H = 0.38          # altura
SEG = 48          # divisões em volta


def perfil():
    """Pontos (r, z) do centro da base até o centro do topo."""
    # superelipse única do polo de baixo ao de cima: laterais abauladas (n) e base/topo achatados (m);
    # o bojo fica um pouco abaixo do meio e o centro do topo afunda (capitonê)
    n, m = 2.4, 5.0
    zc = H * 0.47
    pts = []
    for i in range(25):
        t = -math.pi / 2 + math.pi * i / 24
        c, s = math.cos(t), math.sin(t)
        r = R * (abs(c) ** (2 / n)) if i not in (0, 24) else 0.0
        z = zc + (H - zc if s > 0 else zc) * (abs(s) ** (2 / m)) * (1 if s >= 0 else -1)
        if s > 0 and r < R * 0.45:
            z -= 0.03 * (1 - r / (R * 0.45)) ** 2
        pts.append((r, max(z, 0.0)))
    return pts


def girar(bm, pts, mat):
    aneis = []
    for k in range(SEG):
        a = 2 * math.pi * k / SEG
        aneis.append([bm.verts.new((r * math.cos(a), r * math.sin(a), z)) for r, z in pts])
    faces = []
    for k in range(SEG):
        A, B = aneis[k], aneis[(k + 1) % SEG]
        for j in range(len(pts) - 1):
            vs = [A[j], B[j], B[j + 1], A[j + 1]]
            # nos polos (r = 0) os vértices coincidem; remove_doubles depois junta
            faces.append(bm.faces.new(vs))
    for f in faces:
        f.material_index = mat
        f.smooth = True


def toro(bm, raio, z, r_tubo, mat, seg=SEG, seg_tubo=8):
    aneis = []
    for k in range(seg):
        a = 2 * math.pi * k / seg
        ca, sa = math.cos(a), math.sin(a)
        anel = []
        for j in range(seg_tubo):
            b = 2 * math.pi * j / seg_tubo
            rr = raio + r_tubo * math.cos(b)
            anel.append(bm.verts.new((rr * ca, rr * sa, z + r_tubo * math.sin(b))))
        aneis.append(anel)
    for k in range(seg):
        A, B = aneis[k], aneis[(k + 1) % seg]
        for j in range(seg_tubo):
            f = bm.faces.new((A[j], B[j], B[(j + 1) % seg_tubo], A[(j + 1) % seg_tubo]))
            f.material_index = mat
            f.smooth = True


def raio_em(pts, z):
    """Raio do perfil (lado de fora) na altura z."""
    lado = [p for p in pts if p[0] > R * 0.5]
    for (r0, z0), (r1, z1) in zip(lado, lado[1:]):
        if min(z0, z1) <= z <= max(z0, z1) and z1 != z0:
            return r0 + (r1 - r0) * (z - z0) / (z1 - z0)
    return R


def botao(bm, z, mat):
    """Botão do capitonê: disco achatado no centro do topo."""
    rb, hb = 0.022, 0.010
    pts = [(0.0, z - 0.004), (rb, z - 0.002), (rb * 1.05, z + hb * 0.5), (rb * 0.7, z + hb), (0.0, z + hb)]
    aneis = []
    for k in range(16):
        a = 2 * math.pi * k / 16
        aneis.append([bm.verts.new((r * math.cos(a), r * math.sin(a), zz)) for r, zz in pts])
    for k in range(16):
        A, B = aneis[k], aneis[(k + 1) % 16]
        for j in range(len(pts) - 1):
            f = bm.faces.new((A[j], B[j], B[j + 1], A[j + 1]))
            f.material_index = mat
            f.smooth = True


def puff():
    bm = bmesh.new()
    pts = perfil()
    girar(bm, pts, 0)
    zs, zi = H * 0.80, H * 0.10
    toro(bm, raio_em(pts, zs) - 0.004, zs, 0.009, 1)
    toro(bm, raio_em(pts, zi) - 0.004, zi, 0.008, 1)
    botao(bm, H - 0.032, 1)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)
    bmesh.ops.dissolve_degenerate(bm, edges=bm.edges, dist=1e-6)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new("Puff")
    bm.to_mesh(me)
    bm.free()
    for n in SLOTS:
        m = bpy.data.materials.get(n) or bpy.data.materials.new(n)
        m.diffuse_color = CORES[n]
        me.materials.append(m)
    ob = bpy.data.objects.new("Puff", me)
    bpy.context.scene.collection.objects.link(ob)
    return ob


def main():
    args = sys.argv[sys.argv.index("--") + 1:]
    out_blend_dir, out_fbx_dir = map(os.path.abspath, args[:2])
    os.makedirs(out_blend_dir, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    ob = puff()
    print("MODELO puff dims", tuple(round(d, 3) for d in ob.dimensions), "faces", len(ob.data.polygons))
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(out_blend_dir, "puff.blend"))
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(out_fbx_dir, "puff.fbx"), use_selection=False, object_types={'MESH'},
        apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
        bake_space_transform=True, mesh_smooth_type='FACE', use_tspace=False, add_leaf_bones=False,
        bake_anim=False, path_mode='STRIP')
    print("EXPORTADO puff")


if __name__ == "__main__":
    main()
