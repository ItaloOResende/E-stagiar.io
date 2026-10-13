"""
Cabos e conectores 3D (autoria própria do projeto), na mesma escala "cartoon" (x1,35) dos conectores do
gabinete/monitor (Tools/blender/pc_modelos_build.py), para que os plugues encaixem nas portas modeladas lá.

Uso (headless):
  blender -b --factory-startup --python Tools/blender/cabos_build.py -- <saida.blend> <saida.fbx> [<pasta_renders>]

Objetos exportados (cada um vira uma malha no Unity, transform aplicado):
  Plugue_Tomada, Plugue_IEC, Plugue_VGA, Plugue_HDMI, Plugue_RJ45
      ponta (face que entra na porta) no plano y = 0, voltada para -Y; corpo em +Y; o cabo sai em y = comprimento.
      No Unity (FBX com bake_space_transform): ponta para +Z, cabo saindo para -Z.
  Rolo_Energia, Rolo_VGA, Rolo_HDMI, Rolo_Rede
      item guardado: cabo enrolado (3 voltas) com as duas pontas por cima; base em z = 0, centrado.
  PontoEletrico
      espelho de parede: tomada do PC (x = +0,07), tomada do monitor (x = 0), ponto de rede (x = -0,07),
      face da frente em y = 0 voltada para -Y (no Unity: +Z, X espelhado).
Com <pasta_renders>: imagens para o Canvas (plugues vistos por trás, módulos da tomada e do ponto de rede).
"""
import bpy, bmesh, math, os, sys
from mathutils import Vector, Matrix

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import gabinete_v2_build as v2  # noqa: E402  (box, cyl, join, material, srgb)

S = 1.35                      # mesma escala de leitura dos conectores do gabinete
R_CABO = {"Energia": 0.0042, "VGA": 0.0045, "HDMI": 0.0034, "Rede": 0.0030}
M = {}


def mats():
    v2.mats()
    M.update(v2.MAT)
    M["cabo_preto"] = v2.material("MAT_Cabo_Preto", (0.07, 0.07, 0.08), rough=0.65)
    M["cabo_cinza"] = v2.material("MAT_Cabo_Cinza", (0.78, 0.78, 0.76), rough=0.6)
    M["cabo_azul"] = v2.material("MAT_Cabo_Azul", (0.18, 0.45, 0.85), rough=0.55)
    M["plug"] = v2.material("MAT_Plug_Preto", (0.11, 0.11, 0.12), rough=0.5)
    M["translucido"] = v2.material("MAT_Plug_Translucido", (0.82, 0.90, 0.95), rough=0.15)
    M["tomada"] = v2.material("MAT_Tomada_Branco", (0.93, 0.93, 0.91), rough=0.45)
    M["tomada_fundo"] = v2.material("MAT_Tomada_Interior", (0.30, 0.30, 0.32), rough=0.6)
    M["tomada"].diffuse_color = (0.85, 0.85, 0.83, 1)
    M["rebaixo"] = v2.material("MAT_Tomada_Rebaixo", (0.74, 0.74, 0.72), rough=0.5)
    M["translucido"].diffuse_color = (0.75, 0.85, 0.92, 1)


def box(size, loc, mat, bevel=0.0):
    return v2.box("b", size, loc, mat, bevel=bevel, segments=2)


def cyl_y(r, depth, loc, mat, verts=16, bevel=0.0):
    return v2.cyl("c", r, depth, loc, 'Y', mat, verts=verts, bevel=bevel)


def tube(points, r, mat, name="cabo"):
    cu = bpy.data.curves.new(name, 'CURVE')
    cu.dimensions = '3D'; cu.bevel_depth = r; cu.bevel_resolution = 2; cu.resolution_u = 8; cu.use_fill_caps = True
    sp = cu.splines.new('BEZIER'); sp.bezier_points.add(len(points) - 1)
    for bp, p in zip(sp.bezier_points, points):
        bp.co = p; bp.handle_left_type = bp.handle_right_type = 'AUTO'
    o = bpy.data.objects.new(name, cu); bpy.context.scene.collection.objects.link(o)
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
    bpy.ops.object.convert(target='MESH')
    o = bpy.context.active_object
    o.data.materials.append(mat)
    bpy.ops.object.shade_smooth()
    return o


def done(parts, name):
    o = v2.join(parts, name)
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    o.select_set(False)
    return o


# ------------------------------------------------------------------ plugues (ponta em y = 0, corpo em +Y)
def plug_tomada():
    """Plugue NBR 14136 (2P+T), redondo, pinos de latão; entra na tomada de parede."""
    L = 0.034
    p = [cyl_y(0.0165, L, (0, L / 2, 0), M["plug"], verts=24, bevel=0.003)]
    for i in range(8):                                                   # frisos de pega
        a = i * math.pi / 4
        p.append(box((0.003, L * 0.55, 0.003), (0.0168 * math.cos(a), L * 0.6, 0.0168 * math.sin(a)), M["plug"]))
    for x, z in ((-0.0095, 0.0), (0.0095, 0.0), (0.0, -0.0075)):
        p.append(cyl_y(0.0021, 0.019, (x, -0.0095, z), M["latao"], verts=10))
    p.append(cyl_y(0.0075, 0.014, (0, L + 0.007, 0), M["plug"], verts=16, bevel=0.002))  # alívio de tensão
    return done(p, "Plugue_Tomada"), L + 0.014


def plug_iec():
    """Plugue IEC C13 (fêmea), encaixa na entrada de energia do gabinete/monitor."""
    w, h, L = 0.024 * S + 0.003, 0.015 * S + 0.003, 0.040
    p = [box((w, L, h), (0, L / 2, 0), M["plug"], bevel=0.003)]
    p.append(box((w * 0.82, 0.002, h * 0.72), (0, -0.0005, 0), M["preto"], bevel=0.0015))   # face com os furos
    for x in (-0.0075, 0.0, 0.0075):
        p.append(box((0.0022, 0.002, 0.006), (x, -0.0012, 0.002 if x == 0 else -0.002), M["tomada_fundo"]))
    for i in range(5):
        p.append(box((w + 0.001, 0.002, h + 0.001), (0, L * 0.45 + i * 0.004, 0), M["plug"]))
    p.append(cyl_y(0.0075, 0.016, (0, L + 0.007, 0), M["plug"], verts=16, bevel=0.002))
    return done(p, "Plugue_IEC"), L + 0.016


def plug_vga():
    """D-sub 15 macho: capa preta, frente metálica trapezoidal com pinos, 2 parafusos borboleta azuis."""
    w1, w2, h = 0.031 * S, 0.025 * S, 0.012 * S
    bm = bmesh.new()
    vs = [bm.verts.new((px, 0.0, pz)) for px, pz in ((-w1 / 2, h / 2), (w1 / 2, h / 2), (w2 / 2, -h / 2), (-w2 / 2, -h / 2))]
    f = bm.faces.new(vs)
    ext = bmesh.ops.extrude_face_region(bm, geom=[f])
    bmesh.ops.translate(bm, vec=(0, 0.007, 0), verts=[v for v in ext["geom"] if isinstance(v, bmesh.types.BMVert)])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new("vga_frente"); bm.to_mesh(me); bm.free()
    fr = bpy.data.objects.new("vga_frente", me); bpy.context.scene.collection.objects.link(fr)
    v2.finish(fr, M["chapa"], bevel=0.001)
    L = 0.034
    p = [fr, box((0.050, L - 0.007, 0.024), (0, 0.007 + (L - 0.007) / 2, 0), M["plug"], bevel=0.004)]
    for x in (-0.0215 * S, 0.0215 * S):
        p.append(cyl_y(0.0045, 0.012, (x, 0.012, 0), M["vga"], verts=12, bevel=0.0012))      # borboleta
        p.append(cyl_y(0.0016, 0.010, (x, 0.002, 0), M["parafuso"], verts=8))                 # rosca
    p.append(cyl_y(0.0078, 0.018, (0, L + 0.008, 0), M["plug"], verts=16, bevel=0.002))
    return done(p, "Plugue_VGA"), L + 0.017


def plug_hdmi():
    """HDMI tipo A: capa preta achatada e ponta metálica fina."""
    a1, a2, hh = 0.0165 * S * 0.94, 0.0125 * S * 0.94, 0.0055 * S * 0.9
    bm = bmesh.new()
    pts = ((-a1 / 2, hh / 2), (a1 / 2, hh / 2), (a1 / 2, 0.0), (a2 / 2, -hh / 2), (-a2 / 2, -hh / 2), (-a1 / 2, 0.0))
    f = bm.faces.new([bm.verts.new((x, 0.0, z)) for x, z in pts])
    ext = bmesh.ops.extrude_face_region(bm, geom=[f])
    bmesh.ops.translate(bm, vec=(0, 0.010, 0), verts=[v for v in ext["geom"] if isinstance(v, bmesh.types.BMVert)])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new("hdmi_ponta"); bm.to_mesh(me); bm.free()
    tip = bpy.data.objects.new("hdmi_ponta", me); bpy.context.scene.collection.objects.link(tip)
    v2.finish(tip, M["chapa"], bevel=0.0005)
    L = 0.032
    p = [tip, box((0.027, L - 0.010, 0.012), (0, 0.010 + (L - 0.010) / 2, 0), M["plug"], bevel=0.0035)]
    p.append(cyl_y(0.0058, 0.014, (0, L + 0.006, 0), M["plug"], verts=16, bevel=0.0015))
    return done(p, "Plugue_HDMI"), L + 0.013


def plug_rj45():
    """RJ45: corpo transparente com contatos dourados, trava em cima e capa (bota) azul."""
    w, h, L = 0.0125 * S, 0.0095 * S, 0.020
    p = [box((w, L, h), (0, L / 2, 0), M["translucido"], bevel=0.0012)]
    for i in range(8):
        p.append(box((0.0009, 0.004, 0.0012), (-w / 2 + 0.002 + i * (w - 0.004) / 7, 0.003, h / 2 - 0.0004), M["latao"]))
    lever = box((0.006, 0.014, 0.0015), (0, 0.010, h / 2 + 0.0018), M["translucido"])
    lever.rotation_euler = (math.radians(-8), 0, 0)
    p.append(lever)
    p.append(box((w + 0.004, 0.018, h + 0.004), (0, L + 0.007, 0), M["cabo_azul"], bevel=0.003))   # bota
    p.append(cyl_y(0.0045, 0.010, (0, L + 0.020, 0), M["cabo_azul"], verts=12, bevel=0.001))
    return done(p, "Plugue_RJ45"), L + 0.025


# ------------------------------------------------------------------ rolos (itens guardados)
def place_plug(src, loc, yaw, pitch=0.0):
    o = src.copy(); o.data = src.data.copy(); bpy.context.scene.collection.objects.link(o)
    o.matrix_world = Matrix.Translation(loc) @ Matrix.Rotation(yaw, 4, 'Z') @ Matrix.Rotation(pitch, 4, 'X')
    return o


def rolo(nome, r, cabo_mat, plug_a, len_a, plug_b, len_b):
    """Cabo enrolado (3 voltas, raio 4,2 cm) deitado; as duas pontas saem e os plugues ficam sobre o rolo."""
    R, turns = 0.040, 3
    pts = []
    for i in range(turns * 12 + 1):
        a = i / 12 * 2 * math.pi
        rr = R + (i / (turns * 12) - 0.5) * 3.2 * r          # espiral leve: as voltas não se cruzam
        pts.append(Vector((rr * math.cos(a), rr * math.sin(a), r + (i % 2) * 0.25 * r)))
    p0, p1 = pts[0], pts[-1]
    top = 2 * r + 0.004
    # pontas: sobem para cima do rolo e terminam nos plugues, deitados lado a lado
    def half_w(o):
        xs = [v.co.x for v in o.data.vertices]
        return (max(xs) - min(xs)) / 2
    ya, yb = -(half_w(plug_a) + 0.002), half_w(plug_b) + 0.002   # lado a lado, sem se cruzar
    a_end = Vector((-0.012, ya, top + 0.006)); b_end = Vector((0.012, yb, top + 0.006))
    cabo_a = tube([p0 + Vector((0, 0, 0)), Vector((p0.x - 0.01, p0.y - 0.012, top)), Vector((a_end.x + 0.012, ya, top + 0.004)), a_end],
                  r, cabo_mat, "ponta_a")
    cabo_b = tube([p1, Vector((p1.x + 0.005, p1.y + 0.01, top)), Vector((b_end.x - 0.012, yb, top + 0.004)), b_end], r, cabo_mat, "ponta_b")
    coil = tube(pts, r, cabo_mat, "rolo")
    # plugues: deitados (eixo do plugue na horizontal), cabo saindo para o centro
    # yaw +90: ponta para +X, corpo para -X (o cabo chega por trás do plugue, em a_end)
    pa = place_plug(plug_a, a_end + Vector((len_a, 0, 0.004)), math.radians(90))
    pb = place_plug(plug_b, b_end + Vector((-len_b, 0, 0.004)), math.radians(-90))
    o = done([coil, cabo_a, cabo_b, pa, pb], nome)
    mn = Vector([min((o.matrix_world @ v.co)[i] for v in o.data.vertices) for i in range(3)])
    mx = Vector([max((o.matrix_world @ v.co)[i] for v in o.data.vertices) for i in range(3)])
    c = (mn + mx) / 2
    for v in o.data.vertices:
        v.co -= Vector((c.x, c.y, mn.z))
    print("ROLO", nome, "tamanho", tuple(round(x, 3) for x in (mx - mn)))
    return o


# ------------------------------------------------------------------ espelho de parede (2 tomadas + rede)
def ponto_eletrico():
    W, H, Dp = 0.22, 0.09, 0.012
    p = [box((W, Dp, H), (0, Dp / 2, 0), M["tomada"], bevel=0.004)]
    for x in (-0.1, 0.1):
        p.append(cyl_y(0.0028, 0.002, (x, -0.0005, 0), M["parafuso"], verts=10))
    for x in (0.07, 0.0):                                  # tomadas NBR 14136 (rebaixo sextavado + 3 furos)
        bm = bmesh.new()
        hexa = [(-0.019, 0.0), (-0.010, 0.016), (0.010, 0.016), (0.019, 0.0), (0.010, -0.016), (-0.010, -0.016)]
        f = bm.faces.new([bm.verts.new((x + a, -0.0005, b)) for a, b in hexa])
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        ext = bmesh.ops.extrude_face_region(bm, geom=[f])
        bmesh.ops.translate(bm, vec=(0, 0.0035, 0), verts=[v for v in ext["geom"] if isinstance(v, bmesh.types.BMVert)])
        me = bpy.data.meshes.new("rebaixo"); bm.to_mesh(me); bm.free()
        o = bpy.data.objects.new("rebaixo", me); bpy.context.scene.collection.objects.link(o)
        v2.finish(o, M["rebaixo"], bevel=0.0)
        p.append(o)
        for a, b in ((-0.0095, 0.0), (0.0095, 0.0), (0.0, -0.0075)):
            p.append(cyl_y(0.0026, 0.002, (x + a, -0.0008, b), M["preto"], verts=10))
    x = -0.07                                              # ponto de rede (keystone RJ45)
    p.append(box((0.028, 0.004, 0.034), (x, -0.001, 0), M["tomada_fundo"], bevel=0.001))
    p.append(box((0.0125 * S + 0.002, 0.002, 0.0095 * S + 0.002), (x, -0.0032, -0.002), M["preto"]))
    p.append(box((0.020, 0.002, 0.005), (x, -0.0032, 0.0115), M["cabo_azul"]))
    return done(p, "PontoEletrico")


# ------------------------------------------------------------------ renders para o Canvas
def render_setup():
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_WORKBENCH'
    sh = sc.display.shading
    sh.light = 'STUDIO'; sh.color_type = 'MATERIAL'; sh.show_cavity = True; sh.cavity_type = 'BOTH'
    sh.show_object_outline = True; sh.object_outline_color = (0, 0, 0); sh.show_specular_highlight = False
    sc.render.film_transparent = True; sc.view_settings.view_transform = 'Standard'
    sc.render.image_settings.file_format = 'PNG'; sc.render.image_settings.color_mode = 'RGBA'


def render(objs, path, from_back, frame=None, height=512):
    """Câmera ortográfica fixa de frente (-Y) ou de trás (+Y), enquadrada no objeto (ou em frame=(x0,z0,x1,z1))."""
    sc = bpy.context.scene
    for o in bpy.data.objects:
        o.hide_render = o not in objs
    if frame is None:
        vs = [o.matrix_world @ v.co for o in objs for v in o.data.vertices]
        frame = (min(v.x for v in vs), min(v.z for v in vs), max(v.x for v in vs), max(v.z for v in vs))
    x0, z0, x1, z1 = frame
    w, h = x1 - x0, z1 - z0
    sc.render.resolution_y = height; sc.render.resolution_x = max(8, round(height * w / h))
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam
    cam.data.type = 'ORTHO'; cam.data.ortho_scale = max(w, h); cam.data.clip_end = 10
    cx, cz = (x0 + x1) / 2, (z0 + z1) / 2
    if from_back:
        cam.location = (cx, 2, cz); cam.rotation_euler = (math.pi / 2, 0, math.pi)
    else:
        cam.location = (cx, -2, cz); cam.rotation_euler = (math.pi / 2, 0, 0)
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(cam)
    print("RENDER", os.path.basename(path), sc.render.resolution_x, "x", sc.render.resolution_y)


def main():
    args = sys.argv[sys.argv.index("--") + 1:]
    out_blend, out_fbx = map(os.path.abspath, args[:2])
    renders = os.path.abspath(args[2]) if len(args) > 2 else None
    bpy.ops.wm.read_factory_settings(use_empty=True)
    mats()
    tomada, lt = plug_tomada()
    iec, li = plug_iec()
    vga, lv = plug_vga()
    hdmi, lh = plug_hdmi()
    rj, lr = plug_rj45()
    print("PLUGUE_COMPRIMENTO", {"Tomada": lt, "IEC": li, "VGA": lv, "HDMI": lh, "RJ45": lr})
    rolos = [rolo("Rolo_Energia", R_CABO["Energia"], M["cabo_preto"], tomada, lt, iec, li),
             rolo("Rolo_VGA", R_CABO["VGA"], M["cabo_cinza"], vga, lv, vga, lv),
             rolo("Rolo_HDMI", R_CABO["HDMI"], M["cabo_preto"], hdmi, lh, hdmi, lh),
             rolo("Rolo_Rede", R_CABO["Rede"], M["cabo_azul"], rj, lr, rj, lr)]
    for i, o in enumerate(rolos):       # lado a lado só para o .blend (o FBX aplica e cada um vira malha própria)
        o.location.x = 0.2 + i * 0.15
    pe = ponto_eletrico(); pe.location.x = -0.3
    for i, o in enumerate((tomada, iec, vga, hdmi, rj)):
        o.location.x = i * 0.08; o.location.z = 0.2
    bpy.context.view_layer.update()
    os.makedirs(os.path.dirname(out_blend), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=out_blend)

    if renders:
        os.makedirs(renders, exist_ok=True)
        render_setup()
        for o, nome in ((tomada, "tomada"), (iec, "energia"), (vga, "vga"), (hdmi, "hdmi"), (rj, "rj45")):
            render([o], os.path.join(renders, f"ui_render_plugue_{nome}.png"), from_back=True)
        x = pe.location.x
        render([pe], os.path.join(renders, "ui_render_tomada.png"), False, (x + 0.035, -0.045, x + 0.105, 0.045))
        render([pe], os.path.join(renders, "ui_render_ponto_rede.png"), False, (x - 0.105, -0.045, x - 0.035, 0.045))

    # exporta tudo na origem, com transform aplicado (cada objeto = uma malha)
    for o in rolos + [pe, tomada, iec, vga, hdmi, rj]:
        o.location = (0, 0, 0) if o.name.startswith("Rolo") or o.name == "PontoEletrico" else (0, 0, 0)
    bpy.context.view_layer.update()
    for o in bpy.data.objects:
        o.hide_render = False
    bpy.ops.export_scene.fbx(
        filepath=out_fbx, use_selection=False, object_types={'MESH'},
        apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
        bake_space_transform=True, mesh_smooth_type='FACE', use_tspace=False, add_leaf_bones=False,
        bake_anim=False, path_mode='STRIP')
    print("EXPORTADO", out_fbx)


if __name__ == "__main__":   # importável (render dos plugues reaproveitado por componentes_build)
    main()
