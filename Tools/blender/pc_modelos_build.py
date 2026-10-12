"""
Atualiza os modelos OFICIAIS do gabinete e do monitor (do Matheus) com as partes que faltavam.

Uso (headless):
  blender -b --factory-startup --python Tools/blender/pc_modelos_build.py -- \
      gabinete|monitor <original.fbx> <saida.blend> <saida.fbx>

Princípios:
  - A malha do Matheus é a BASE e não é alterada: as peças novas são acrescentadas e JUNTADAS na mesma malha
    (mesmo objeto/nome: 'Cube.001' no gabinete, 'Cube' no monitor), num único modelo por equipamento.
    Assim o fileID da malha no Unity não muda e os prefabs/cenas continuam apontando para ela.
  - O material 0 continua sendo o do Matheus (no Unity: M_Escritorio_Escuro); as peças novas usam
    materiais extras 'MAT_PC_*' (no Unity: remapeados para Assets/Art/Official/Materials/M_PC_*).
  - As peças são modeladas em metros REAIS (escala do Visual nos prefabs = 0,36387107) e depois levadas
    ao espaço do original (x 1/0,36387107). Frente em -Y, traseira em +Y, topo em +Z (como os originais).
  - Gabinete: reaproveita a traseira, os conectores, os parafusos e o botão do gabinete v2 e acrescenta a saída HDMI
    (Tools/blender/gabinete_v2_build.py), sem o corpo/painel do v2.
  - Monitor: traseira nova (carcaça, VESA, ventilação), entradas de energia, VGA e HDMI. O cabo de força é
    um item do jogo, conectado na manutenção (ver CABO_FIXO).
"""
import bpy, bmesh, math, sys, os
from mathutils import Vector, Matrix

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import gabinete_v2_build as v2  # noqa: E402  (helpers: box, cyl, join, materiais)

K = 1 / 0.36387107  # metros reais -> unidades do original
CABO_FIXO = False   # monitor: cabo de força modelado encaixado (desligado: o cabo é item do jogo)


def world_bbox(o):
    ws = [o.matrix_world @ v.co for v in o.data.vertices]
    return (Vector([min(v[i] for v in ws) for i in range(3)]), Vector([max(v[i] for v in ws) for i in range(3)]))


def import_original(path):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path)
    o = [x for x in bpy.data.objects if x.type == 'MESH' and x not in before][0]
    return o


def place(objs, origin):
    """Leva peças modeladas em metros reais (em torno de 0) para o espaço do original."""
    m = Matrix.Translation(origin) @ Matrix.Scale(K, 4)
    for o in objs:
        mw = o.matrix_world.copy()
        o.parent = None
        o.matrix_world = m @ mw
    bpy.context.view_layer.update()
    for o in objs:
        bpy.ops.object.select_all(action='DESELECT')
        o.select_set(True)
        bpy.context.view_layer.objects.active = o
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)


def merge_into(base, parts, body_mats):
    """Junta as peças na malha original; materiais 'corpo' viram o material 0 do original."""
    orig_mat = base.data.materials[0]
    for p in parts:
        for i, m in enumerate(p.data.materials):
            if m is None or m.name in body_mats:
                p.data.materials[i] = orig_mat
    bpy.ops.object.select_all(action='DESELECT')
    for p in parts:
        p.select_set(True)
    base.select_set(True)
    bpy.context.view_layer.objects.active = base
    bpy.ops.object.join()
    # remove slots duplicados (mesmo material em mais de um slot)
    me = base.data
    first = {}
    remap = []
    for i, m in enumerate(me.materials):
        first.setdefault(m.name, i)
        remap.append(first[m.name])
    for p in me.polygons:
        p.material_index = remap[p.material_index]
    for i in reversed(range(len(me.materials))):
        if first[me.materials[i].name] != i or not any(p.material_index == i for p in me.polygons):
            if i == 0:
                continue
            for p in me.polygons:
                if p.material_index > i:
                    p.material_index -= 1
            me.materials.pop(index=i)
    for m in me.materials:
        if m.name.startswith("MAT_Gabinete_"):
            m.name = m.name.replace("MAT_Gabinete_", "MAT_PC_")


def remove_unused():
    for o in list(bpy.data.objects):
        if o.type == 'EMPTY':
            bpy.data.objects.remove(o)


def hdmi_port(hx, yio, zio, M):
    """Entrada HDMI tipo A (casca metálica trapezoidal + miolo preto + língua de contatos), voltada para +Y."""
    if "hdmi" not in M:
        M["hdmi"] = v2.material("MAT_Gabinete_HDMI", (0.10, 0.10, 0.11), metallic=0.3, rough=0.45)
    bm = bmesh.new()
    a1, a2, hh2 = 0.0165, 0.0125, 0.0055
    pts = ((-a1 / 2, hh2 / 2), (a1 / 2, hh2 / 2), (a1 / 2, 0.0), (a2 / 2, -hh2 / 2), (-a2 / 2, -hh2 / 2), (-a1 / 2, 0.0))
    f = bm.faces.new([bm.verts.new((hx + px, yio + 0.002, zio + pz)) for px, pz in pts])
    ext = bmesh.ops.extrude_face_region(bm, geom=[f])
    bmesh.ops.translate(bm, vec=(0, 0.007, 0), verts=[v for v in ext["geom"] if isinstance(v, bmesh.types.BMVert)])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new("hdmi_casca"); bm.to_mesh(me); bm.free()
    ho = bpy.data.objects.new("hdmi_casca", me); bpy.context.scene.collection.objects.link(ho)
    v2.finish(ho, M["chapa"], bevel=0.0006)
    ch = [ho,
          v2.box("hdmi_furo", (a1 * 0.80, 0.003, hh2 * 0.62), (hx, yio + 0.0085, zio + 0.0003), M["preto"]),
          v2.box("hdmi_lingua", (a1 * 0.62, 0.003, 0.0012), (hx, yio + 0.0092, zio + 0.0004), M["hdmi"])]
    return v2.scale_about(v2.join(ch, "hdmi"), (hx, yio, zio), v2.CONNECTOR_SCALE)


# ============================== GABINETE ==============================
def build_gabinete(orig_path):
    v2.build()                                  # cena nova com o v2 inteiro
    keep_prefix = ("Traseira_Detalhes", "Conector_", "Parafuso_", "Botao_Energia_Mesh")
    parts = []
    for o in list(bpy.data.objects):
        if o.type != 'MESH':
            continue
        if o.name.startswith(keep_prefix) and o.name.endswith(("_Mesh", "Detalhes")):
            parts.append(o)
        else:
            bpy.data.objects.remove(o)          # corpo, painel frontal, tampa e pés do v2 saem
    # O corpo do original é maciço até a traseira: desloca a traseira do v2 (que ficava 4 mm para dentro)
    # para que a chapa fique 1 mm para fora da caixa do Matheus.
    for o in parts:
        mw = o.matrix_world.copy()
        o.parent = None
        dy = 0.005 if not o.name.startswith("Botao") else -0.001
        o.matrix_world = Matrix.Translation((0, dy, 0)) @ mw
    remove_unused()
    # saída HDMI ao lado da VGA, na mesma placa de expansão (1º slot)
    hx, _ = v2.back_xz(0.55, 0)
    parts.append(hdmi_port(hx, v2.YB + 0.005, v2.H / 2 - 0.777 * v2.H, v2.MAT))

    base = import_original(orig_path)
    mn, mx = world_bbox(base)
    place(parts, (mn + mx) / 2)
    merge_into(base, parts, {"MAT_Gabinete_Corpo"})
    return base


# ============================== MONITOR ==============================
def taper_box(name, w0, h0, w1, h1, depth, y0, zc, mat, bevel=0.0):
    """Caixa afunilada: face (w0 x h0) em y0, face (w1 x h1) em y0+depth (carcaça traseira)."""
    bm = bmesh.new()
    def ring(w, h, y):
        return [bm.verts.new((sx * w / 2, y, zc + sz * h / 2)) for sx, sz in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
    a, b = ring(w0, h0, y0), ring(w1, h1, y0 + depth)
    bm.faces.new(a[::-1]); bm.faces.new(b)
    for i in range(4):
        j = (i + 1) % 4
        bm.faces.new((a[i], a[j], b[j], b[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(o)
    bpy.context.view_layer.objects.active = o
    return v2.finish(o, mat, bevel, 3)


def cable(name, pts, radius, mat):
    """Cabo como curva (bevel redondo) convertida em malha."""
    cu = bpy.data.curves.new(name, 'CURVE')
    cu.dimensions = '3D'; cu.bevel_depth = radius; cu.bevel_resolution = 3; cu.resolution_u = 10
    sp = cu.splines.new('BEZIER'); sp.bezier_points.add(len(pts) - 1)
    for bp, p in zip(sp.bezier_points, pts):
        bp.co = p; bp.handle_left_type = bp.handle_right_type = 'AUTO'
    cu.use_fill_caps = True
    o = bpy.data.objects.new(name, cu); bpy.context.scene.collection.objects.link(o)
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
    bpy.ops.object.convert(target='MESH')
    o = bpy.context.active_object
    o.data.materials.append(mat)
    bpy.ops.object.shade_smooth()
    return o


def build_monitor(orig_path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    v2.mats()
    M = v2.MAT
    M["hdmi"] = v2.material("MAT_Gabinete_HDMI", (0.10, 0.10, 0.11), metallic=0.3, rough=0.45)
    base = import_original(orig_path)
    mn, mx = world_bbox(base)

    # Referência: centro do painel na face de trás (o pé ocupa a parte de baixo da caixa).
    # Medidas do original (unidades do FBX): painel com 0,215 de espessura, face traseira em y = mx.y - 0,099.
    verts = [base.matrix_world @ v.co for v in base.data.vertices]
    panel = [v for v in verts if v.z > mn.z + 0.45]          # acima do pé
    p_mn = Vector([min(v[i] for v in panel) for i in range(3)]); p_mx = Vector([max(v[i] for v in panel) for i in range(3)])
    origin = Vector(((p_mn.x + p_mx.x) / 2, p_mx.y, (p_mn.z + p_mx.z) / 2))
    PW, PH = (p_mx.x - p_mn.x) / K, (p_mx.z - p_mn.z) / K   # painel em metros reais (~0,73 x 0,54)
    desk_z = (mn.z - origin.z) / K                           # base do pé = tampo da mesa
    print("MONITOR painel real", round(PW, 3), round(PH, 3), "mesa z", round(desk_z, 3))

    parts = []
    # --- carcaça traseira (afunilada), deixa uma borda do painel original à mostra ---
    hw, hh, hd, hz = PW * 0.62, PH * 0.66, 0.05, -PH * 0.04
    parts.append(taper_box("carcaca", hw, hh, hw * 0.82, hh * 0.80, hd, -0.002, hz, M["corpo"], bevel=0.008))
    yb = hd - 0.002                                          # face de trás da carcaça
    # ventilação (frisos no alto da carcaça)
    for i in range(6):
        parts.append(v2.box("vent", (hw * 0.55, 0.004, 0.006), (0, yb + 0.0005, hz + hh * 0.30 - i * 0.017), M["preto"], bevel=0.002))
    # suporte VESA 100 x 100 com 4 parafusos
    parts.append(v2.box("vesa", (0.13, 0.004, 0.13), (0, yb + 0.002, hz + 0.02), M["painel"], bevel=0.004))
    for sx in (-1, 1):
        for sz in (-1, 1):
            parts.append(v2.cyl("vesa_parafuso", 0.0055, 0.004, (sx * 0.05, yb + 0.0045, hz + 0.02 + sz * 0.05), 'Y', M["parafuso"], verts=12))

    # --- painel de entradas (parte de baixo da carcaça), conectores voltados para trás ---
    zio = hz - hh * 0.80 / 2 + 0.042
    parts.append(v2.box("io_painel", (hw * 0.70, 0.003, 0.055), (0, yb + 0.0005, zio), M["preto"], bevel=0.003))
    S = v2.CONNECTOR_SCALE
    yio = yb + 0.002

    # Energia (IEC C14, igual à do gabinete) — à esquerda vista de trás (+X do mundo)
    ex = 0.105
    ce = [v2.box("iec_corpo", (0.034, 0.008, 0.024), (ex, yio + 0.004, zio), M["preto"], bevel=0.003),
          v2.box("iec_furo", (0.024, 0.004, 0.015), (ex, yio + 0.0085, zio - 0.0005), M["corpo"], bevel=0.002)]
    energia = v2.scale_about(v2.join(ce, "energia"), (ex, yio, zio), S)
    parts.append(energia)

    # VGA (D-sub azul, 15 pinos) — centro
    vx = 0.0
    bm = bmesh.new()
    w1, w2, h, d = 0.031, 0.025, 0.012, 0.007
    vs = [bm.verts.new((vx + px, yio + 0.002, zio + pz)) for px, pz in ((-w1 / 2, h / 2), (w1 / 2, h / 2), (w2 / 2, -h / 2), (-w2 / 2, -h / 2))]
    f = bm.faces.new(vs)
    ext = bmesh.ops.extrude_face_region(bm, geom=[f])
    bmesh.ops.translate(bm, vec=(0, d, 0), verts=[v for v in ext["geom"] if isinstance(v, bmesh.types.BMVert)])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new("vga_corpo"); bm.to_mesh(me); bm.free()
    vo = bpy.data.objects.new("vga_corpo", me); bpy.context.scene.collection.objects.link(vo)
    v2.finish(vo, M["vga"], bevel=0.0015)
    cv = [vo]
    for row, zz in enumerate((0.0035, 0.0, -0.0035)):
        span = (w1 - 0.010) if row == 0 else (w2 - 0.008)
        for k in range(5):
            cv.append(v2.cyl("vga_pino", 0.0011, 0.002, (vx - span / 2 + k * span / 4, yio + 0.0093, zio + zz), 'Y', M["preto"], verts=6))
    for s in (-1, 1):
        cv.append(v2.cyl("vga_parafuso", 0.0028, 0.006, (vx + s * 0.0215, yio + 0.004, zio), 'Y', M["parafuso"], verts=6))
    parts.append(v2.scale_about(v2.join(cv, "vga"), (vx, yio, zio), S))

    # HDMI (tipo A) — à direita vista de trás (-X do mundo)
    parts.append(hdmi_port(-0.085, yio, zio, M))

    # O cabo de força do monitor é um ITEM do jogo (Pickup_CaboEnergia), conectado/desconectado na manutenção;
    # por isso não faz parte do modelo. CABO_FIXO = True volta a modelá-lo encaixado (só para renders/estudos).
    if CABO_FIXO:
        # --- cabo de força: plugue C13 encaixado na entrada de energia, cabo descendo até a mesa e plugue de tomada ---
        yz = yio + 0.0105 * S                                    # frente da entrada de energia
        plug = [v2.box("c13_corpo", (0.030 * S, 0.022, 0.020 * S), (ex, yz + 0.008, zio), M["preto"], bevel=0.004),
                v2.box("c13_alivio", (0.012, 0.016, 0.012), (ex, yz + 0.014, zio - 0.017), M["preto"], bevel=0.004)]
        parts.append(v2.join(plug, "c13"))
        r = 0.0045
        z_mesa = desk_z + r + 0.003
        path = [Vector(p) for p in ((ex, yz + 0.014, zio - 0.022), (ex + 0.004, yz + 0.030, zio - 0.12),
                                    (ex + 0.02, yz + 0.07, z_mesa + 0.03), (ex + 0.05, yz + 0.15, z_mesa),
                                    (ex + 0.03, yz + 0.26, z_mesa), (ex - 0.02, yz + 0.33, z_mesa))]
        parts.append(cable("cabo_forca", path, r, M["preto"]))
        # plugue de tomada (padrão brasileiro NBR 14136, 3 pinos), deitado na mesa no fim do cabo
        end, prev = path[-1], path[-2]
        dirv = (end - prev).normalized()
        pz = desk_z + 0.0165
        pc = end + dirv * 0.020
        pg = [v2.cyl("plugue_corpo", 0.016, 0.034, (0, 0, 0), 'Y', M["preto"], verts=20, bevel=0.003)]
        for px, pzz, rr in ((-0.0095, 0.0, 0.0024), (0.0095, 0.0, 0.0024), (0.0, -0.0075, 0.0024)):
            pg.append(v2.cyl("plugue_pino", rr, 0.019, (px, 0.026, pzz), 'Y', M["parafuso"], verts=10))
        pl = v2.join(pg, "plugue")
        pl.matrix_world = Matrix.Translation((pc.x, pc.y, pz)) @ Vector((0, 1, 0)).rotation_difference(Vector((dirv.x, dirv.y, 0)).normalized()).to_matrix().to_4x4()
        parts.append(pl)


    place(parts, origin)
    merge_into(base, parts, {"MAT_Gabinete_Corpo"})
    return base


def export_fbx(path, obj):
    """Mesmas opções que reproduzem o FBX original sem diferença (testado ida e volta)."""
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={'MESH'},
        apply_unit_scale=True, apply_scale_options='FBX_SCALE_NONE',
        axis_forward='-Z', axis_up='Y', bake_space_transform=False,
        mesh_smooth_type='FACE', use_tspace=False, add_leaf_bones=False, bake_anim=False, path_mode='STRIP')


def report(o):
    o.data.calc_loop_triangles()
    mn, mx = world_bbox(o)
    print("MODELO", o.name, "malha", o.data.name, "verts", len(o.data.vertices), "tris", len(o.data.loop_triangles))
    print("MATERIAIS", [m.name for m in o.data.materials])
    print("BBOX", tuple(round(x, 3) for x in mn), tuple(round(x, 3) for x in mx))


if __name__ == "__main__":
    kind, orig, out_blend, out_fbx = sys.argv[sys.argv.index("--") + 1:]
    obj = build_gabinete(os.path.abspath(orig)) if kind == "gabinete" else build_monitor(os.path.abspath(orig))
    report(obj)
    for p in (out_blend, out_fbx):
        os.makedirs(os.path.dirname(os.path.abspath(p)), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(out_blend))
    export_fbx(os.path.abspath(out_fbx), obj)
    print("SALVO", out_blend, out_fbx)
