"""
Gabinete aberto do Canvas (vista Interior) em 3D: o gabinete oficial (malha do Matheus + traseira) em metros reais,
aberto do lado da tampa (-X) por boolean, com estrutura interna simples e os componentes de
Tools/blender/componentes_build.py nos encaixes reais. Câmera ortográfica fixa em 3/4 (um pouco de cima e da
frente), para dar profundidade e mostrar a face da RAM em pé no slot.

Uso (headless):
  blender -b --factory-startup --python Tools/blender/pc_interior_renders.py -- <gabinete_oficial.blend> <pasta_saida>

Saída (mesmo enquadramento em todas; depois Tools/blender/pc_interior_camadas.py recorta as camadas):
  int_base.png              gabinete aberto vazio
  int_placa.png             + placa-mãe
  int_placa_ram.png         + placa-mãe + RAM
  int_placa_cooler.png      + placa-mãe + cooler
  int_fonte.png / int_hd.png
"""
import bpy, bmesh, math, os, sys
from mathutils import Vector, Matrix

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import gabinete_v2_build as v2  # noqa: E402
import componentes_build as cb  # noqa: E402

K = 0.36387107
W, D, H = v2.W, v2.D, v2.H          # 0,298 x 0,623 x 0,519 m
WALL = 0.010

src, out = map(os.path.abspath, sys.argv[sys.argv.index("--") + 1:][:2])
os.makedirs(out, exist_ok=True)


def lin(c):
    return tuple(v2.srgb(x) for x in c) + (1,)


bpy.ops.wm.open_mainfile(filepath=src)
case = [o for o in bpy.data.objects if o.type == 'MESH'][0]
for m in case.data.materials:
    if not m.name.startswith("MAT_PC_"):
        m.diffuse_color = lin((0.169, 0.170, 0.200))     # M_Escritorio_Escuro
    if m.name == "MAT_PC_LED":
        m.diffuse_color = lin((0.35, 0.37, 0.36))        # desligado
ws = [case.matrix_world @ v.co for v in case.data.vertices]
c0 = (Vector([min(v[i] for v in ws) for i in range(3)]) + Vector([max(v[i] for v in ws) for i in range(3)])) / 2
# caixa do Matheus (sem a traseira acrescentada): mesmos limites usados nos outros renders
X0, X1, Z0, Z1, Y0, Y1 = -7.611, -6.792, 2.150, 3.577, 2.989, 4.700
center = Vector(((X0 + X1) / 2, (Y0 + Y1) / 2, (Z0 + Z1) / 2))
case.matrix_world = Matrix.Scale(K, 4) @ Matrix.Translation(-center) @ case.matrix_world
bpy.context.view_layer.objects.active = case; case.select_set(True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

# abre o lado da tampa: tira o miolo (paredes de 1 cm) e a lateral -X
bpy.ops.mesh.primitive_cube_add(size=1, location=(-WALL / 2 - 0.05, 0.004, 0))
cut = bpy.context.active_object
cut.scale = (W - WALL + 0.1, D - 2 * WALL - 0.008, H - 2 * WALL)
mod = case.modifiers.new("abrir", 'BOOLEAN'); mod.object = cut; mod.operation = 'DIFFERENCE'; mod.solver = 'EXACT'
bpy.context.view_layer.objects.active = case
bpy.ops.object.modifier_apply(modifier=mod.name)
bpy.data.objects.remove(cut)

v2.MAT.clear(); cb.M.clear(); cb.mats()
M = cb.M
M["bandeja"] = v2.material("MAT_Int_Bandeja", (0.56, 0.58, 0.63), metallic=0.2, rough=0.5)
M["interno"] = v2.material("MAT_Int_Escuro", (0.22, 0.23, 0.27), rough=0.6)

xin, xr = -W / 2, W / 2 - WALL          # lado aberto / face interna da lateral direita
yf, yb = -D / 2 + WALL, D / 2 - WALL     # face interna da frente / de trás
zb, zt = -H / 2 + WALL, H / 2 - WALL
estrutura = []
B = lambda size, loc, mat, bevel=0.0: estrutura.append(v2.box("e", size, loc, mat, bevel=bevel))
B((0.002, D - 2 * WALL - 0.01, H - 2 * WALL - 0.01), (xr - 0.001, 0, 0), M["bandeja"])                  # bandeja da placa
for (y, z, h) in ((-0.06, -0.02, 0.09), (-0.06, 0.10, 0.07), (0.06, -0.16, 0.05)):                       # passa-cabos
    B((0.003, 0.016, h), (xr - 0.002, y, z), M["chip"], bevel=0.006)
# gaiola de baias na frente: 5,25" em cima (atrás das baias do Matheus) e 3,5" embaixo (HD)
cy0, cy1 = yf, yf + 0.20
B((0.003, cy1 - cy0, zt - zb - 0.02), (xr - 0.04, (cy0 + cy1) / 2, 0), M["chapa"])                   # lateral do fundo da gaiola
for z in (zt - 0.02, zb + 0.02):                                                                   # travessas do lado aberto
    B((0.006, cy1 - cy0, 0.006), (xin + 0.035, (cy0 + cy1) / 2, z), M["chapa"])
for z in (0.235, 0.175, 0.115):
    B((xr - xin - 0.08, cy1 - cy0 - 0.01, 0.003), (0, (cy0 + cy1) / 2, z - 0.03), M["chapa"])          # bandejas 5,25"
for z in (-0.208,):
    for x in (xin + 0.04, xr - 0.045):
        B((0.010, cy1 - cy0 - 0.02, 0.004), (x, (cy0 + cy1) / 2, z), M["chapa"])                       # trilhos do HD
# suporte da fonte (em cima, atrás) e recorte da traseira
B((xr - xin - 0.02, 0.17, 0.003), (0.0, yb - 0.085, zt - 0.092), M["chapa"])
B((xr - xin, 0.003, 0.03), (0.0, yb - 0.17, zt - 0.08), M["chapa"])
# tampas dos slots de expansão vistas por dentro
for i in range(4):
    B((0.06, 0.003, 0.018), (xr - 0.06, yb - 0.002, -0.15 - i * 0.03), M["chapa"])
estrutura = cb.done(estrutura, "Estrutura")


def place(obj, rot, pos):
    obj.matrix_world = Matrix.Translation(pos) @ rot.to_4x4()
    return obj


comps = cb.build_all()
# placa (coords da peça: X = traseira->frente, Y = cima, Z = para fora) -> gabinete: X->-Y, Y->+Z, Z->-X
R_MB = Matrix(((0, 0, -1), (-1, 0, 0), (0, 1, 0)))
mb_center = Vector((xr - 0.008, yb - 0.02 - cb.MB / 2, 0.015))                   # 8 mm de espaçadores
place(comps["PlacaMae"], R_MB, mb_center)
board = lambda x, y, z: mb_center + R_MB @ Vector((x, y, z))
place(comps["Cooler"], R_MB, board(cb.CPU_XY[0], cb.CPU_XY[1], 0.0065))
place(comps["MemoriaRAM"], R_MB, board(cb.RAM_XY[0], cb.RAM_XY[1], 0.0040))
# fonte: deitada em cima-atrás, lateral com a etiqueta para o lado aberto, saída dos cabos para a frente
R_PSU = Matrix(((-1, 0, 0), (0, -1, 0), (0, 0, 1)))
place(comps["Fonte"], R_PSU, Vector((xr - 0.075 - 0.02, yb - 0.075, zt - 0.088)))
# HD deitado na baia de baixo (comprimento ao longo da profundidade, conectores para trás)
R_HD = Matrix(((-1, 0, 0), (0, -1, 0), (0, 0, 1)))
place(comps["HD"], R_HD, Vector((0.0, (cy0 + cy1) / 2, -0.206)))

# ---------------- câmera e renders ----------------
sc = bpy.context.scene
sc.render.engine = 'BLENDER_WORKBENCH'
sh = sc.display.shading
sh.light = 'STUDIO'; sh.color_type = 'MATERIAL'; sh.show_cavity = True; sh.cavity_type = 'BOTH'
sh.show_object_outline = True; sh.object_outline_color = (0, 0, 0); sh.show_specular_highlight = False
sh.show_shadows = False
sc.render.film_transparent = True; sc.view_settings.view_transform = 'Standard'
sc.render.image_settings.file_format = 'PNG'; sc.render.image_settings.color_mode = 'RGBA'
sc.render.filter_size = 1.0

view_dir = Vector((1.0, 0.50, -0.22)).normalized()     # olhando para +X, vindo da frente (-Y) e de cima
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam
cam.data.type = 'ORTHO'
rot = view_dir.to_track_quat('-Z', 'Y')  # câmera: -Z para a frente, Y para cima
cam.rotation_euler = rot.to_euler()
cam.location = -view_dir * 3
bpy.context.view_layer.update()
inv = cam.matrix_world.inverted()
pts = [inv @ (case.matrix_world @ v.co) for v in case.data.vertices]
x0, x1 = min(p.x for p in pts), max(p.x for p in pts)
y0, y1 = min(p.y for p in pts), max(p.y for p in pts)
pad = 0.01
x0 -= pad; x1 += pad; y0 -= pad; y1 += pad
cam.location = cam.matrix_world @ Vector(((x0 + x1) / 2, (y0 + y1) / 2, 0))
cam.data.ortho_scale = max(x1 - x0, y1 - y0)
HEIGHT = 1600
sc.render.resolution_y = HEIGHT
sc.render.resolution_x = round(HEIGHT * (x1 - x0) / (y1 - y0))
print("INTERIOR quadro", sc.render.resolution_x, "x", sc.render.resolution_y)

sets = {
    "int_base": [],
    "int_placa": ["PlacaMae"],
    "int_placa_ram": ["PlacaMae", "MemoriaRAM"],
    "int_placa_cooler": ["PlacaMae", "Cooler"],
    "int_fonte": ["Fonte"],
    "int_hd": ["HD"],
}
for name, visible in sets.items():
    for k, o in comps.items():
        o.hide_render = k not in visible
    sc.render.filepath = os.path.join(out, name + ".png")
    bpy.ops.render.render(write_still=True)
    print("RENDER", name)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(out, "gabinete_aberto.blend"))
