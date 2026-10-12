"""
Gera as imagens 2D FIXAS do Canvas de manutenção a partir dos modelos 3D oficiais (sem desenhar outra versão).

Uso (headless):
  blender -b --factory-startup --python Tools/blender/pc_canvas_renders.py -- \
      <gabinete_oficial.blend> <monitor_oficial.blend> <pasta_saida>

Câmeras ortográficas fixas, enquadradas EXATAMENTE na caixa do equipamento (sem margem), para que as
coordenadas normalizadas usadas no Canvas (parafusos do ComputerCase, portas, botão Power, tela) batam
com a imagem:
  ui_pc_frente.png      gabinete visto de frente (-Y)          X 0 = esquerda da imagem = -X do mundo
  ui_pc_traseira.png    gabinete visto de trás (+Y)            X 0 = esquerda da imagem = +X do mundo
  ui_pc_aberto.png      gabinete visto pelo lado da tampa (-X), sem a tampa: interior com os encaixes
  ui_monitor_frente.png / ui_monitor_traseira.png
O interior não existe no modelo (o corpo do Matheus é maciço): é montado só nesta cena de render,
dentro do contorno do próprio gabinete, com os encaixes nas posições dos Encaixe_* do Canvas.
Imprime 'ANCORA <nome> xmin ymin xmax ymax' (0–1, origem embaixo à esquerda, como o anchor do Unity).
"""
import bpy, sys, os, math
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import gabinete_v2_build as v2  # noqa: E402

gab_blend, mon_blend, out = map(os.path.abspath, sys.argv[sys.argv.index("--") + 1:])
os.makedirs(out, exist_ok=True)
HEIGHT = 1600
ESCURO = (0.169, 0.170, 0.200)  # M_Escritorio_Escuro


def lin(c):
    return tuple(v2.srgb(x) for x in c) + (1,)


def prepare(path):
    bpy.ops.wm.open_mainfile(filepath=path)
    o = [x for x in bpy.data.objects if x.type == 'MESH'][0]
    for m in o.data.materials:
        if not m.name.startswith("MAT_PC_"):
            m.diffuse_color = lin(ESCURO)
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_WORKBENCH'
    sh = sc.display.shading
    sh.light = 'STUDIO'; sh.color_type = 'MATERIAL'
    sh.show_cavity = True; sh.cavity_type = 'BOTH'
    sh.show_object_outline = True; sh.object_outline_color = (0, 0, 0)
    sh.show_specular_highlight = False
    sc.render.film_transparent = True
    sc.view_settings.view_transform = 'Standard'
    sc.render.image_settings.file_format = 'PNG'; sc.render.image_settings.color_mode = 'RGBA'
    return o


def bbox(o, faces=None):
    vs = [o.matrix_world @ o.data.vertices[i].co for p in (faces or o.data.polygons) for i in p.vertices]
    return Vector([min(v[i] for v in vs) for i in range(3)]), Vector([max(v[i] for v in vs) for i in range(3)])


def render(name, axis, lo, hi):
    """axis: '-Y', '+Y', '-X'. lo/hi: cantos (mundo) do quadro, nos 2 eixos da imagem."""
    sc = bpy.context.scene
    (u0, v0), (u1, v1) = lo, hi
    w, h = u1 - u0, v1 - v0
    sc.render.resolution_y = HEIGHT
    sc.render.resolution_x = round(HEIGHT * w / h)
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam
    cam.data.type = 'ORTHO'; cam.data.ortho_scale = max(w, h); cam.data.clip_end = 100
    cu, cv = (u0 + u1) / 2, (v0 + v1) / 2
    if axis == '-Y':    # de frente: imagem X = mundo X
        cam.location = (cu, -50, cv); cam.rotation_euler = (math.pi / 2, 0, 0)
    elif axis == '+Y':  # de trás: imagem X = -mundo X  (u = -X)
        cam.location = (-cu, 50, cv); cam.rotation_euler = (math.pi / 2, 0, math.pi)
    else:               # pelo lado -X: imagem X = -mundo Y (u = -Y)
        cam.location = (-50, -cu, cv); cam.rotation_euler = (math.pi / 2, 0, -math.pi / 2)
    sc.render.filepath = os.path.join(out, name + ".png")
    bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(cam)
    print("RENDER", name, sc.render.resolution_x, "x", sc.render.resolution_y)


def anchor(name, lo, hi, a, b):
    """Retângulo [a,b] (cantos no plano da imagem) em coordenadas 0–1 do quadro [lo,hi]."""
    (u0, v0), (u1, v1) = lo, hi
    xs = sorted(((a[0] - u0) / (u1 - u0), (b[0] - u0) / (u1 - u0)))
    ys = sorted(((a[1] - v0) / (v1 - v0), (b[1] - v0) / (v1 - v0)))
    print("ANCORA", name, *(round(x, 4) for x in (xs[0], ys[0], xs[1], ys[1])))


def faces_of(o, matname):
    idx = [i for i, m in enumerate(o.data.materials) if m.name == matname]
    return [p for p in o.data.polygons if p.material_index in idx]


# ============================== GABINETE ==============================
o = prepare(gab_blend)
# limites da caixa original do Matheus (o v2 foi encaixado nela; ver pc_modelos_build.py)
X0, X1, Z0, Z1 = -7.611, -6.792, 2.150, 3.577
Y0, Y1 = 2.989, 4.700
# Frente com o botão Power APAGADO (o PC começa desligado); o LED aceso sai num render à parte, recortado depois
# por Tools/blender/pc_interior_camadas.py --led (sobreposição mostrada só com o PC ligado).
led = bpy.data.materials["MAT_PC_LED"]
led_on = tuple(led.diffuse_color)
led.diffuse_color = lin((0.36, 0.38, 0.37))
render("ui_pc_frente", '-Y', (X0, Z0), (X1, Z1))
led.diffuse_color = led_on
render("ui_pc_frente_ledon", '-Y', (X0, Z0), (X1, Z1))
f = [p for p in faces_of(o, "MAT_PC_LED") if (o.matrix_world @ p.center).y < (Y0 + Y1) / 2]   # só o da frente
a, b = bbox(o, f)
anchor("BotaoPower", (X0, Z0), (X1, Z1), (a.x, a.z), (b.x, b.z))
led.diffuse_color = lin((0.36, 0.38, 0.37))   # traseira também com os LEDs apagados
render("ui_pc_traseira", '+Y', (-X1, Z0), (-X0, Z1))
MID = (Z0 + Z1) / 2
for nm, mat, keep in (("Porta_EnergiaGabinete", "MAT_PC_Pino", lambda z: z > MID),   # pinos da entrada IEC (em cima)
                      ("Porta_VGAGabinete", "MAT_PC_VGA", None),
                      ("Porta_HDMIGabinete", "MAT_PC_HDMI", None),
                      ("Porta_RedeGabinete", "MAT_PC_LED", lambda z: z < MID)):    # LEDs do RJ45 (embaixo)
    f = faces_of(o, mat)
    if keep:
        f = [p for p in f if keep((o.matrix_world @ p.center).z)]
    a, b = bbox(o, f)
    anchor(nm, (-X1, Z0), (-X0, Z1), (-a.x, a.z), (-b.x, b.z))

# ---- interior (só nesta cena): lateral -X aberta, encaixes nas posições do Canvas ----
v2.mats(); M = v2.MAT
xs = X0 - 0.004                        # plano logo à frente da lateral -X
lo, hi = (-Y1, Z0), (-Y0, Z1)          # u = -Y (traseira à esquerda), v = Z
def at(u, v):                          # 0–1 do quadro -> (y, z) do mundo
    return Vector((-(lo[0] + u * (hi[0] - lo[0])), lo[1] + v * (hi[1] - lo[1])))
def rect(name, u0, v0, u1, v1, depth, mat, dx=0.0, bevel=0.0):
    p0, p1 = at(u0, v0), at(u1, v1)
    c = (p0 + p1) / 2
    return v2.box(name, (depth, abs(p1.x - p0.x), abs(p1.y - p0.y)), (xs - dx - depth / 2, c.x, c.y), mat, bevel=bevel)
FR = 0.035                             # moldura do gabinete (chapa dobrada) em fração da altura
inner = M["painel"]; inner.diffuse_color = lin((0.33, 0.34, 0.39))
rect("bandeja", FR * 0.83, FR, 1 - FR * 0.83, 1 - FR, 0.004, inner)
# Layout (u da traseira para a frente, v de baixo para cima) = âncoras dos Encaixe_* no Canvas:
#   fonte (0,03–0,27 x 0,82–0,985), placa-mãe (0,08–0,54 x 0,25–0,80), HD deitado (0,64–0,92 x 0,05–0,27),
#   baias de 5,25" do Matheus no alto da frente. RAM e cooler ficam SOBRE a placa-mãe (sprite dela).
# baia da fonte: suporte em L e furos de fixação
rect("fonte_area", 0.03, 0.82, 0.27, 0.985, 0.003, M["preto"], dx=0.004)
rect("fonte_apoio", 0.03, 0.805, 0.30, 0.82, 0.008, M["chapa"], dx=0.004, bevel=0.002)
# encaixe da placa-mãe: área rebaixada com os pinos de fixação (standoffs)
rect("mb_area", 0.08, 0.25, 0.54, 0.80, 0.003, M["preto"], dx=0.004)
for u in (0.11, 0.31, 0.51):
    for v in (0.28, 0.52, 0.77):
        p = at(u, v)
        v2.cyl("standoff", 0.012, 0.012, (xs - 0.013, p.x, p.y), 'X', M["latao"], verts=12)
# frente: moldura das baias, baias de 5,25" em cima, gaiola do HD embaixo
rect("baias_moldura", 0.62, 0.03, 0.94, 0.97, 0.006, M["chapa"], dx=0.004, bevel=0.003)
rect("baias_fundo", 0.64, 0.05, 0.92, 0.95, 0.004, M["painel"], dx=0.010)
for i in range(4):
    v = 0.62 + i * 0.08
    rect("baia_trilho", 0.645, v, 0.915, v + 0.022, 0.006, M["chapa"], dx=0.012, bevel=0.002)
rect("hd_area", 0.64, 0.05, 0.92, 0.27, 0.003, M["preto"], dx=0.016)
for v in (0.045, 0.275):
    rect("hd_trilho", 0.64, v - 0.012, 0.92, v + 0.012, 0.010, M["chapa"], dx=0.019, bevel=0.002)
for u in (0.655, 0.905):
    for v in (0.08, 0.24):
        p = at(u, v)
        v2.cyl("hd_parafuso", 0.009, 0.006, (xs - 0.024, p.x, p.y), 'X', M["parafuso"], verts=10)
# furos de passagem de cabo entre a placa-mãe e as baias
for v in (0.30, 0.47, 0.64):
    rect("passa_cabo", 0.565, v, 0.590, v + 0.11, 0.003, M["preto"], dx=0.004, bevel=0.004)
o.hide_render = False
# a lateral -X do modelo fica atrás da bandeja; a moldura que sobra é o próprio gabinete
render("ui_pc_aberto", '-X', lo, hi)

# ============================== MONITOR ==============================
o = prepare(mon_blend)
mn, mx = bbox(o)
panel = [p for p in o.data.polygons if p.material_index == 0 and (o.matrix_world @ p.center).z > mn.z + 0.45]
pmn, pmx = bbox(o, panel)
render("ui_monitor_frente", '-Y', (mn.x, mn.z), (mx.x, mx.z))
bz = 0.055  # moldura da tela
anchor("Tela", (mn.x, mn.z), (mx.x, mx.z), (pmn.x + bz, pmn.z + bz), (pmx.x - bz, pmx.z - bz))
render("ui_monitor_traseira", '+Y', (-mx.x, mn.z), (-mn.x, mx.z))
fr = ((-mx.x, mn.z), (-mn.x, mx.z))
a, b = bbox(o, faces_of(o, "MAT_PC_VGA")); anchor("Porta_VGAMonitor", *fr, (-a.x, a.z), (-b.x, b.z))
c, d = bbox(o, faces_of(o, "MAT_PC_HDMI")); anchor("Porta_HDMIMonitor", *fr, (-c.x, c.z), (-d.x, d.z))
# entrada de energia: mesma linha, em x real +0,105 (VGA em 0, HDMI em -0,085; ver pc_modelos_build.py)
vx, hx = -(a.x + b.x) / 2, -(c.x + d.x) / 2
ex = vx + (hx - vx) * (0.105 / -0.085)
w = (b.x - a.x) * 0.6
anchor("Porta_EnergiaMonitor", *fr, (ex - w, a.z - (b.z - a.z) * 0.6), (ex + w, b.z + (b.z - a.z) * 0.6))
