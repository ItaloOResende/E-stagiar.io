"""
Renders de comparação original x v2 e checagem do FBX exportado (não escreve em Assets/).

Uso:
  blender -b --factory-startup --python Tools/blender/gabinete_v2_compare.py -- \
      <gabinete_oficial.fbx> <gabinete_v2.blend> <gabinete_v2.fbx> <pc_back.png> <pasta_saida>
"""
import bpy, sys, os, math
from mathutils import Vector

orig_fbx, v2_blend, v2_fbx, back_png, out = map(os.path.abspath, sys.argv[sys.argv.index("--") + 1:])
os.makedirs(out, exist_ok=True)
OFFSET = 0.45  # distância entre os dois modelos nas vistas lado a lado


def world_bbox(objs):
    mn = Vector((1e9,) * 3); mx = Vector((-1e9,) * 3)
    for o in objs:
        if o.type == 'MESH':
            for v in o.data.vertices:
                w = o.matrix_world @ v.co
                mn = Vector(map(min, mn, w)); mx = Vector(map(max, mx, w))
    return mn, mx


# ---------- 1) checagem do FBX exportado (ida e volta) ----------
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=v2_fbx)
mn, mx = world_bbox(bpy.data.objects)
print("FBX_ROUNDTRIP objetos", len(bpy.data.objects), "malhas", sum(o.type == 'MESH' for o in bpy.data.objects))
print("FBX_ROUNDTRIP tamanho", tuple(round(x, 4) for x in mx - mn), "centro", tuple(round(x, 4) for x in (mn + mx) / 2))
for o in bpy.data.objects:
    if o.type == 'EMPTY' and o.name.startswith(("Tampa", "Botao", "Conector_", "Parafuso_")):
        print(f"  pivo {o.name:28} mundo={tuple(round(x, 4) for x in o.matrix_world.translation)}")
print("FBX_ROUNDTRIP materiais", sorted(m.name for m in bpy.data.materials))

# ---------- 2) cena de comparação ----------
bpy.ops.wm.open_mainfile(filepath=v2_blend)
scene = bpy.context.scene
v2_objs = list(bpy.data.objects)
root = bpy.data.objects["Gabinete_v2"]
root.location.x = OFFSET / 2

before = set(bpy.data.objects)
bpy.ops.import_scene.fbx(filepath=orig_fbx)
orig = [o for o in bpy.data.objects if o not in before][0]
m = bpy.data.materials.new("Original_Escuro")
m.diffuse_color = m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.024, 0.025, 0.033, 1)
orig.data.materials.clear(); orig.data.materials.append(m)
bpy.context.view_layer.update()
mn, mx = world_bbox([orig])
k = 0.36387107  # mesma escala do Visual no Gabinete.prefab
orig.scale = orig.scale * k
bpy.context.view_layer.update()
mn, mx = world_bbox([orig])
orig.location -= (mn + mx) / 2 + Vector((OFFSET / 2, 0, 0))
bpy.context.view_layer.update()
mn, mx = world_bbox([orig])
print("ORIGINAL_x0.364 tamanho", tuple(round(x, 4) for x in mx - mn))

# chão, luz, mundo
bpy.ops.mesh.primitive_plane_add(size=6, location=(0, 0, -0.266))
floor = bpy.context.active_object
fm = bpy.data.materials.new("Chao"); fm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.55, 0.52, 0.48, 1)
floor.data.materials.append(fm)
sun = bpy.data.objects.new("Sol", bpy.data.lights.new("Sol", 'SUN')); scene.collection.objects.link(sun)
sun.data.energy = 3.0; sun.rotation_euler = (math.radians(50), math.radians(10), math.radians(-35)); sun.data.angle = math.radians(8)
fill = bpy.data.objects.new("Preenchimento", bpy.data.lights.new("Preenchimento", 'SUN')); scene.collection.objects.link(fill)
fill.data.energy = 1.2; fill.rotation_euler = (math.radians(60), 0, math.radians(150))
scene.world = bpy.data.worlds.new("Mundo"); scene.world.use_nodes = True
scene.world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.32, 0.36, 0.42, 1)
scene.world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.8

try:
    scene.render.engine = 'BLENDER_EEVEE'
except TypeError:
    scene.render.engine = 'BLENDER_EEVEE_NEXT'
scene.view_settings.view_transform = 'Standard'
scene.render.film_transparent = False

cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam")); scene.collection.objects.link(cam); scene.camera = cam


def look(pos, target, ortho=None, res=(1600, 900), lens=50):
    cam.location = pos
    cam.rotation_euler = (Vector(target) - Vector(pos)).to_track_quat('-Z', 'Y').to_euler()
    if ortho:
        cam.data.type = 'ORTHO'; cam.data.ortho_scale = ortho
    else:
        cam.data.type = 'PERSP'; cam.data.lens = lens
    scene.render.resolution_x, scene.render.resolution_y = res


def shot(name):
    scene.render.filepath = os.path.join(out, name)
    bpy.ops.render.render(write_still=True)
    print("RENDER", name)


T = (0, 0, 0)
look((0, -2.2, 0.35), T, ortho=1.05); shot("01_frente_original_x_v2.png")
look((0, 2.2, 0.35), T, ortho=1.05); shot("02_traseira_v2_x_original.png")
look((-2.2, 0, 0.2), T, ortho=1.0); orig.hide_render = True; shot("03_lateral_tampa_v2.png"); orig.hide_render = False
look((-1.25, -1.45, 0.75), (0, 0, -0.03), lens=55); shot("04_tres_quartos_frente.png")
look((-0.9, 1.3, 0.55), (0, 0, -0.03), lens=55); shot("05_tres_quartos_traseira.png")

# tampa aberta (só o v2)
orig.hide_render = True
tampa = bpy.data.objects["Tampa_Lateral"]
tampa.rotation_euler.z = math.radians(-35)
look((-0.65 + OFFSET / 2, 0.75, 0.45), (OFFSET / 2, 0, 0), lens=40); shot("06_tampa_aberta.png")
tampa.rotation_euler.z = 0

# close da traseira (conectores e parafusos)
look((OFFSET / 2, 1.2, 0.0), (OFFSET / 2, 0, 0.0), ortho=0.62, res=(1000, 1100)); shot("07_traseira_close.png")

# sobreposição com pc_back: câmera ortográfica enquadrando exatamente a face traseira (W x H)
W, H = 0.2982, 0.5194
floor.hide_render = True
scene.render.resolution_x, scene.render.resolution_y = 710, int(710 * H / W)
look((OFFSET / 2, 1.5, 0), (OFFSET / 2, 0, 0), ortho=W, res=(710, int(710 * H / W)))
cam.data.sensor_fit = 'HORIZONTAL'
shot("08_traseira_ortho.png")
render = bpy.data.images.load(os.path.join(out, "08_traseira_ortho.png"))
ref = bpy.data.images.load(back_png)
ref.scale(render.size[0], render.size[1])  # coordenadas normalizadas: estica para o tamanho da face
a = list(render.pixels); b = list(ref.pixels)
# Blender guarda pixels de baixo para cima nas duas imagens; mistura 50%
mix = [a[i] * 0.5 + b[i] * 0.5 if (i % 4) != 3 else 1.0 for i in range(len(a))]
img = bpy.data.images.new("overlay", render.size[0], render.size[1])
img.pixels = mix
img.filepath_raw = os.path.join(out, "09_overlay_pc_back.png"); img.file_format = 'PNG'; img.save()
print("RENDER 09_overlay_pc_back.png")
