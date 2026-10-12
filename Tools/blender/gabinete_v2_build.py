"""
Gera o gabinete v2 (estilo cartoon) de forma reproduzível e salva o .blend de trabalho.

Uso (headless):
  blender -b --factory-startup --python Tools/blender/gabinete_v2_build.py -- <saida.blend> [<saida.fbx>]

Convenções (iguais ao gabinete.fbx oficial, para o Visual do prefab continuar com a mesma rotação):
  - 1 unidade = 1 m; origem no centro da caixa; frente em -Y, traseira em +Y, topo em +Z.
  - Exporta sem 'Apply Transform' (como o oficial): no Unity o root do FBX recebe -90° em X.
  - Tampa lateral removível no lado -X (o lado dos parafusos obrigatórios, vistos de trás à direita).
  - Tamanho = gabinete oficial x 0,364 (= BoxCollider do Gabinete.prefab): 0,298 x 0,623 x 0,519 m.
  - Parafusos: mesmos ids e posições normalizadas de ComputerCase.CreateDefaultTowerBackScrews
    (imagem pc_back, 710x1580; X 0 = esquerda vista de trás, Y 0 = topo).
Nada aqui escreve em Assets/: a exportação para o Unity é uma etapa separada.
"""
import bpy, bmesh, math, sys, os
from mathutils import Vector

W, D, H = 0.2982, 0.6226, 0.5194          # largura (X), profundidade (Y), altura (Z)
LID_T = 0.006                              # espessura da tampa
BEZEL_D = 0.022                            # profundidade do painel frontal
BACK_T = 0.004                             # espessura da chapa traseira
FLANGE_W = 0.026                           # aba da tampa dobrada sobre a traseira

SCREWS = [  # id, x/710, y/1580, obrigatório  (cópia de ComputerCase.CreateDefaultTowerBackScrews)
    ("topo-esquerda", 118.4 / 710, 33.2 / 1580, False),
    ("topo-direita", 600.6 / 710, 36.4 / 1580, False),
    ("alto-esquerda", 36.9 / 710, 149.8 / 1580, False),
    ("alto-direita", 680.9 / 710, 149.8 / 1580, True),
    ("meio-esquerda", 35.6 / 710, 780.3 / 1580, False),
    ("meio-direita", 679.7 / 710, 780.3 / 1580, True),
    ("base-esquerda", 38.9 / 710, 1456.8 / 1580, False),
    ("base-direita", 679.8 / 710, 1456.8 / 1580, True),
]


def srgb(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def material(name, rgb, metallic=0.0, rough=0.55, emission=None):
    m = bpy.data.materials.new(name)
    bsdf = m.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*[srgb(c) for c in rgb], 1)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = rough
    if emission:
        bsdf.inputs["Emission Color"].default_value = (*[srgb(c) for c in emission], 1)
        bsdf.inputs["Emission Strength"].default_value = 3.0
    m.diffuse_color = (*[srgb(c) for c in rgb], 1)
    return m


MAT = {}


def mats():
    MAT["corpo"] = material("MAT_Gabinete_Corpo", (0.169, 0.170, 0.200), rough=0.5)        # = M_Escritorio_Escuro
    MAT["painel"] = material("MAT_Gabinete_Painel", (0.25, 0.26, 0.31), rough=0.45)
    MAT["chapa"] = material("MAT_Gabinete_Chapa", (0.70, 0.72, 0.77), metallic=0.2, rough=0.4)
    MAT["parafuso"] = material("MAT_Gabinete_Parafuso", (0.88, 0.89, 0.92), metallic=0.6, rough=0.3)
    MAT["preto"] = material("MAT_Gabinete_Porta", (0.05, 0.05, 0.06), rough=0.7)
    MAT["vga"] = material("MAT_Gabinete_VGA", (0.16, 0.38, 0.88), rough=0.4)
    MAT["latao"] = material("MAT_Gabinete_Pino", (0.90, 0.68, 0.25), metallic=0.7, rough=0.3)
    MAT["led"] = material("MAT_Gabinete_LED", (0.25, 1.0, 0.35), emission=(0.25, 1.0, 0.35))


def link(o, parent):
    o.parent = parent
    return o


def finish(o, mat, bevel=0.0, segments=2, smooth_angle=35):
    if mat:
        o.data.materials.append(mat)
    if bevel > 0:
        mod = o.modifiers.new("Bevel", 'BEVEL')
        mod.width = bevel
        mod.segments = segments
        mod.limit_method = 'ANGLE'
        mod.harden_normals = False
        bpy.context.view_layer.objects.active = o
        bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.context.view_layer.objects.active = o
    o.select_set(True)
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(smooth_angle))
    o.select_set(False)
    return o


def box(name, size, loc, mat, parent=None, bevel=0.0, segments=1):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
    o = bpy.context.active_object
    o.name = o.data.name = name
    o.scale = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    finish(o, mat, bevel, segments)
    return link(o, parent)


def cyl(name, r, depth, loc, axis, mat, parent=None, verts=16, bevel=0.0):
    rot = {'X': (0, math.pi / 2, 0), 'Y': (math.pi / 2, 0, 0), 'Z': (0, 0, 0)}[axis]
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=r, depth=depth, location=loc, rotation=rot)
    o = bpy.context.active_object
    o.name = o.data.name = name
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    finish(o, mat, bevel, 1, smooth_angle=40)
    return link(o, parent)


def empty(name, loc, parent=None):
    """Empty com posição dada em coordenadas de MUNDO (os pais não têm rotação nem escala)."""
    o = bpy.data.objects.new(name, None)
    o.empty_display_size = 0.05
    bpy.context.scene.collection.objects.link(o)
    o.parent = parent
    o.location = Vector(loc) - (parent.matrix_world.translation if parent else Vector())
    bpy.context.view_layer.update()
    return o


def join(objs, name):
    """Junta objetos de detalhe numa malha só (menos draw calls no Unity)."""
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    o = bpy.context.active_object
    o.name = o.data.name = name
    o.select_set(False)
    return o


CONNECTOR_SCALE = 1.35  # conectores exagerados (leitura cartoon e identificação em jogo)


def scale_about(o, pivot, k):
    """Escala a malha (já com transform aplicado) em torno de um ponto de mundo, sem mexer na escala do objeto."""
    pivot = Vector(pivot)
    mw, mi = o.matrix_world, o.matrix_world.inverted()
    for v in o.data.vertices:
        w = mw @ v.co
        v.co = mi @ (pivot + (w - pivot) * k)
    return o


def to_local(o, parent):
    """Reparenta mantendo a posição de mundo e leva a origem do objeto para a do pai."""
    mw = o.matrix_world.copy()
    o.parent = parent
    o.matrix_world = mw


# --- coordenadas das imagens de referência -------------------------------------------------
YB = D / 2  # plano da traseira


def back_xz(xn, yn):   # pc_back: X 0 = esquerda vista de trás (= +X do mundo)
    return W / 2 - xn * W, H / 2 - yn * H


def front_xz(xn, yn):  # pc_front: X 0 = esquerda vista de frente (= -X do mundo)
    return -W / 2 + xn * W, H / 2 - yn * H


def build():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.unit_settings.scale_length = 1.0
    mats()
    root = empty("Gabinete_v2", (0, 0, 0))

    # Corpo: chassi sem o lado -X (onde fica a tampa)
    body_y0, body_y1 = -D / 2 + BEZEL_D, D / 2 - BACK_T
    corpo = box("Corpo", (W - LID_T, body_y1 - body_y0, H), (LID_T / 2, (body_y0 + body_y1) / 2, 0),
                MAT["corpo"], root, bevel=0.006, segments=3)
    # pés
    feet = []
    for sx in (-1, 1):
        for sy in (-1, 1):
            feet.append(cyl("pe", 0.014, 0.008, (sx * (W / 2 - 0.03), sy * (D / 2 - 0.05), -H / 2 - 0.001), 'Z',
                            MAT["preto"], None, verts=10))
    pes = join(feet, "Pes")
    to_local(pes, root)

    # ---------------- FRENTE ----------------
    yf = -D / 2
    painel = box("Frente_Painel", (W, BEZEL_D, H), (0, yf + BEZEL_D / 2, 0), MAT["painel"], root, bevel=0.012, segments=4)
    det = []
    # 3 baias (referência: grade de recessos do topo do modelo oficial / pc_front)
    bay_w, bay_h = W * 0.74, 0.044
    for i in range(3):
        z = H / 2 - 0.06 - i * (bay_h + 0.012)
        det.append(box("baia", (bay_w, 0.004, bay_h), (0, yf - 0.001, z), MAT["preto"] if i else MAT["corpo"], bevel=0.003))
        if i == 0:  # leitor óptico: bandeja clara + botão de ejetar
            det.append(box("bandeja", (bay_w * 0.9, 0.003, 0.010), (0, yf - 0.0035, z + 0.004), MAT["chapa"], bevel=0.002))
            det.append(box("ejetar", (0.020, 0.004, 0.006), (bay_w * 0.36, yf - 0.004, z - 0.012), MAT["chapa"], bevel=0.0015))
    # faixa de I/O frontal: 2 USB + 2 áudio
    zio = H / 2 - 0.235
    det.append(box("io_frente", (W * 0.74, 0.004, 0.03), (0, yf - 0.001, zio), MAT["preto"], bevel=0.004))
    for i, x in enumerate((0.02, 0.05)):
        det.append(box("usb_f", (0.017, 0.003, 0.0075), (x, yf - 0.0035, zio), MAT["chapa"], bevel=0.001))
    for x in (-0.06, -0.04):
        det.append(cyl("audio_f", 0.0045, 0.003, (x, yf - 0.0035, zio), 'Y', MAT["chapa"], verts=12))
    # entradas de ar (cartoon: frisos horizontais)
    for i in range(5):
        det.append(box("friso", (W * 0.6, 0.003, 0.006), (0, yf - 0.0008, -0.03 - i * 0.022), MAT["corpo"]))
    frente_det = join(det, "Frente_Detalhes")
    to_local(frente_det, root)

    # Botão de energia (objeto próprio, para interação/animação futura)
    xb, zb = front_xz(0.5, 0.87)
    botao = empty("Botao_Energia", (xb, yf - 0.002, zb), root)
    anel = cyl("Botao_Energia_Anel", 0.021, 0.006, (xb, yf - 0.002, zb), 'Y', MAT["led"], verts=24, bevel=0.002)
    tampo = cyl("Botao_Energia_Tampo", 0.016, 0.008, (xb, yf - 0.004, zb), 'Y', MAT["preto"], verts=24, bevel=0.003)
    simbolo = join([cyl("s", 0.007, 0.002, (xb, yf - 0.0085, zb), 'Y', MAT["led"], verts=16),
                    box("s2", (0.003, 0.002, 0.009), (xb, yf - 0.0090, zb + 0.004), MAT["preto"])], "Botao_Energia_Simbolo")
    btn = join([tampo, anel, simbolo], "Botao_Energia_Mesh")
    to_local(btn, botao)

    # ---------------- TAMPA LATERAL (lado -X) ----------------
    # Pivô na aresta traseira-externa: dá para girar (dobradiça) ou deslizar para trás.
    tampa = empty("Tampa_Lateral", (-W / 2, D / 2, 0), root)
    lid_y0, lid_y1 = -D / 2 + BEZEL_D, D / 2 - BACK_T
    parts = [box("tampa_chapa", (LID_T, lid_y1 - lid_y0, H - 0.004), (-W / 2 + LID_T / 2, (lid_y0 + lid_y1) / 2, 0),
                 MAT["corpo"], bevel=0.003),
             box("tampa_aba", (FLANGE_W, BACK_T, H - 0.004), (-W / 2 + FLANGE_W / 2, D / 2 - BACK_T / 2, 0),
                 MAT["corpo"], bevel=0.0015)]
    for i in range(6):  # ventilação da tampa
        parts.append(box("tampa_vent", (0.002, 0.10, 0.007), (-W / 2 - 0.0005, -0.05, H / 2 - 0.09 - i * 0.02),
                         MAT["preto"], bevel=0.003))
    parts.append(box("tampa_pega", (0.003, 0.012, 0.09), (-W / 2 - 0.001, D / 2 - 0.03, 0), MAT["painel"], bevel=0.0025))
    tampa_mesh = join(parts, "Tampa_Lateral_Mesh")
    to_local(tampa_mesh, tampa)

    # ---------------- TRASEIRA ----------------
    xa, za = back_xz(0.105, 0.045)
    xb_, zb_ = back_xz(0.905, 0.975)
    tras = [box("chapa", (abs(xa - xb_), BACK_T, abs(za - zb_)), ((xa + xb_) / 2, YB - BACK_T / 2, (za + zb_) / 2),
                MAT["chapa"], bevel=0.0015)]
    # fonte (PSU): moldura + grade do ventilador
    x0, z0 = back_xz(0.16, 0.085); x1, z1 = back_xz(0.85, 0.24)
    tras.append(box("psu", (abs(x0 - x1), 0.003, abs(z0 - z1)), ((x0 + x1) / 2, YB + 0.0015, (z0 + z1) / 2), MAT["corpo"], bevel=0.002))
    fx, fz = back_xz(0.69, 0.163)
    tras.append(cyl("fan", 0.032, 0.002, (fx, YB + 0.0035, fz), 'Y', MAT["preto"], verts=24))
    bpy.ops.mesh.primitive_torus_add(major_radius=0.026, minor_radius=0.0018, major_segments=24, minor_segments=6,
                                     location=(fx, YB + 0.0045, fz), rotation=(math.pi / 2, 0, 0))
    t = bpy.context.active_object; bpy.ops.object.transform_apply(rotation=True); finish(t, MAT["chapa"]); tras.append(t)
    bpy.ops.mesh.primitive_torus_add(major_radius=0.014, minor_radius=0.0018, major_segments=20, minor_segments=6,
                                     location=(fx, YB + 0.0045, fz), rotation=(math.pi / 2, 0, 0))
    t = bpy.context.active_object; bpy.ops.object.transform_apply(rotation=True); finish(t, MAT["chapa"]); tras.append(t)
    for a in (0, 90):
        b = box("fan_bar", (0.060, 0.0025, 0.003), (fx, YB + 0.0045, fz), MAT["chapa"])
        b.rotation_euler = (0, math.radians(a), 0); tras.append(b)
    # I/O da placa-mãe
    x0, z0 = back_xz(0.17, 0.42); x1, z1 = back_xz(0.47, 0.645)
    tras.append(box("io_shield", (abs(x0 - x1), 0.002, abs(z0 - z1)), ((x0 + x1) / 2, YB + 0.001, (z0 + z1) / 2), MAT["chapa"], bevel=0.001))
    for xn in (0.23, 0.32, 0.41):
        x, z = back_xz(xn, 0.615)
        tras.append(box("usb_t", (0.009, 0.003, 0.016), (x, YB + 0.0025, z), MAT["preto"], bevel=0.001))
    for xn, yn in ((0.25, 0.55), (0.40, 0.55)):
        x, z = back_xz(xn, yn)
        tras.append(cyl("ps2", 0.007, 0.003, (x, YB + 0.0025, z), 'Y', MAT["preto"], verts=12))
    # furos de ventilação (grade de pontos, como na pc_back)
    for yn in (0.43, 0.62):
        cx, cz = back_xz(0.69, yn)
        for i in range(-2, 3):
            for j in range(-2, 3):
                if i * i + j * j <= 5:
                    tras.append(cyl("furo", 0.0045, 0.0012, (cx + i * 0.014, YB + 0.0004, cz + j * 0.014), 'Y', MAT["preto"], verts=6))
    # 4 slots de expansão
    slot_z = []
    for yn in (0.777, 0.832, 0.888, 0.944):
        x0, z0 = back_xz(0.19, yn - 0.022); x1, z1 = back_xz(0.82, yn + 0.022)
        tras.append(box("slot", (abs(x0 - x1), 0.002, abs(z0 - z1)), ((x0 + x1) / 2, YB + 0.001, (z0 + z1) / 2), MAT["chapa"], bevel=0.001))
        slot_z.append((z0 + z1) / 2)
    traseira = join(tras, "Traseira_Detalhes")
    to_local(traseira, root)

    # Conectores (objetos próprios: futuros pontos de encaixe de cabo)
    conectores = empty("Conectores", (0, YB, 0), root)

    ex, ez = back_xz(0.34, 0.165)
    energia = empty("Conector_Energia", (ex, YB, ez), conectores)
    ce = [box("iec_corpo", (0.034, 0.008, 0.024), (ex, YB + 0.004, ez), MAT["preto"], bevel=0.003),
          box("iec_furo", (0.024, 0.004, 0.015), (ex, YB + 0.0085, ez - 0.0005), MAT["corpo"], bevel=0.002)]
    for px in (-0.006, 0, 0.006):
        ce.append(box("iec_pino", (0.0018, 0.004, 0.006), (ex + px, YB + 0.0105, ez + (0.002 if px == 0 else -0.002)), MAT["latao"]))
    to_local(scale_about(join(ce, "Conector_Energia_Mesh"), (ex, YB, ez), CONNECTOR_SCALE), energia)

    vx, _ = back_xz(0.30, 0)
    vga = empty("Conector_VGA", (vx, YB, slot_z[0]), conectores)
    bm = bmesh.new()  # corpo trapezoidal do D-sub
    w1, w2, h, d = 0.031, 0.025, 0.012, 0.007
    pts = [(-w1 / 2, h / 2), (w1 / 2, h / 2), (w2 / 2, -h / 2), (-w2 / 2, -h / 2)]
    vs = [bm.verts.new((vx + px, YB + 0.002, slot_z[0] + pz)) for px, pz in pts]
    f = bm.faces.new(vs)
    ext = bmesh.ops.extrude_face_region(bm, geom=[f])
    bmesh.ops.translate(bm, vec=(0, d, 0), verts=[v for v in ext["geom"] if isinstance(v, bmesh.types.BMVert)])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new("vga_corpo"); bm.to_mesh(me); bm.free()
    vo = bpy.data.objects.new("vga_corpo", me); bpy.context.scene.collection.objects.link(vo)
    finish(vo, MAT["vga"], bevel=0.0015)
    cv = [vo]
    for row, (zz, n) in enumerate(((0.0035, 5), (0.0, 5), (-0.0035, 5))):
        span = (w1 - 0.010) if row == 0 else (w2 - 0.008)
        for k in range(n):
            cv.append(cyl("vga_pino", 0.0011, 0.002, (vx - span / 2 + k * span / (n - 1), YB + 0.0093, slot_z[0] + zz), 'Y', MAT["preto"], verts=6))
    for s in (-1, 1):
        cv.append(cyl("vga_parafuso", 0.0028, 0.006, (vx + s * 0.0215, YB + 0.004, slot_z[0]), 'Y', MAT["parafuso"], verts=6))
    to_local(scale_about(join(cv, "Conector_VGA_Mesh"), (vx, YB, slot_z[0]), CONNECTOR_SCALE), vga)

    rx, _ = back_xz(0.755, 0)
    rj = empty("Conector_RJ45", (rx, YB, slot_z[1]), conectores)
    cr = [box("rj_corpo", (0.018, 0.010, 0.016), (rx, YB + 0.005, slot_z[1]), MAT["chapa"], bevel=0.0015),
          box("rj_furo", (0.013, 0.004, 0.010), (rx, YB + 0.0085, slot_z[1] - 0.001), MAT["preto"], bevel=0.0008),
          box("rj_trava", (0.006, 0.004, 0.003), (rx, YB + 0.0085, slot_z[1] + 0.0055), MAT["preto"]),
          box("rj_led1", (0.003, 0.002, 0.002), (rx - 0.0065, YB + 0.0105, slot_z[1] + 0.0065), MAT["led"]),
          box("rj_led2", (0.003, 0.002, 0.002), (rx + 0.0065, YB + 0.0105, slot_z[1] + 0.0065), MAT["latao"])]
    to_local(scale_about(join(cr, "Conector_RJ45_Mesh"), (rx, YB, slot_z[1]), CONNECTOR_SCALE), rj)

    # Parafusos: um objeto por id, com o mesmo nome do ComputerCase
    grupo = empty("Parafusos", (0, YB, 0), root)
    for sid, xn, yn, req in SCREWS:
        x, z = back_xz(xn, yn)
        p = empty("Parafuso_" + sid, (x, YB, z), grupo)
        p["required"] = req
        head = cyl("cabeca", 0.0075, 0.004, (x, YB + 0.002, z), 'Y', MAT["parafuso"], verts=12, bevel=0.0015)
        for a in (0, 90):  # fenda em cruz (boolean)
            c = box("fenda", (0.0095, 0.004, 0.0016), (x, YB + 0.0042, z), None)
            c.rotation_euler = (0, math.radians(a), 0)
            mod = head.modifiers.new("cruz", 'BOOLEAN'); mod.object = c; mod.operation = 'DIFFERENCE'
            bpy.context.view_layer.objects.active = head
            bpy.ops.object.modifier_apply(modifier=mod.name)
            bpy.data.objects.remove(c)
        head.name = head.data.name = "Parafuso_" + sid + "_Mesh"
        to_local(head, p)

    for o in bpy.data.objects:
        if o.type == 'MESH':
            o.data.calc_loop_triangles()
    return root


def report():
    tris = 0
    print("HIERARQUIA:")
    for o in sorted(bpy.data.objects, key=lambda o: o.name):
        n = len(o.data.loop_triangles) if o.type == 'MESH' else 0
        tris += n
        print(f"  {o.type:5} {o.name:32} pai={o.parent.name if o.parent else '-':20} tris={n}")
    print("TRIANGULOS_TOTAL", tris)
    mn = Vector((1e9,) * 3); mx = Vector((-1e9,) * 3)
    for o in bpy.data.objects:
        if o.type == 'MESH':
            for v in o.data.vertices:
                w = o.matrix_world @ v.co
                mn = Vector(map(min, mn, w)); mx = Vector(map(max, mx, w))
    print("BBOX", tuple(round(x, 4) for x in mn), tuple(round(x, 4) for x in mx), "TAMANHO", tuple(round(x, 4) for x in mx - mn))


def export_fbx(path):
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=False, object_types={'EMPTY', 'MESH'},
        apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', global_scale=1.0,
        axis_forward='-Z', axis_up='Y', bake_space_transform=False,  # True distorce hierarquias de empties
        use_mesh_modifiers=True, mesh_smooth_type='FACE', use_tspace=False,
        add_leaf_bones=False, bake_anim=False, path_mode='STRIP')


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    build()
    report()
    if args:
        for a in args:
            os.makedirs(os.path.dirname(os.path.abspath(a)), exist_ok=True)
        bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(args[0]))
        print("SALVO", args[0])
    if len(args) > 1:
        export_fbx(os.path.abspath(args[1]))
        print("EXPORTADO", args[1])
