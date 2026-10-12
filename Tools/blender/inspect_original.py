"""Importa um FBX, aplica transforms e imprime bbox em mundo + vértices (análise, não salva nada)."""
import bpy, sys
from mathutils import Vector
f = sys.argv[sys.argv.index("--") + 1]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=f)
o = [x for x in bpy.data.objects if x.type == 'MESH'][0]
ws = [o.matrix_world @ v.co for v in o.data.vertices]
mn = Vector((min(v[i] for v in ws) for i in range(3))); mx = Vector((max(v[i] for v in ws) for i in range(3)))
print("BBOX min", tuple(round(x,4) for x in mn), "max", tuple(round(x,4) for x in mx), "size", tuple(round(x,4) for x in mx-mn))
print("UNIQUE X", sorted({round(v.x,3) for v in ws})); print("UNIQUE Y", sorted({round(v.y,3) for v in ws})); print("UNIQUE Z", sorted({round(v.z,3) for v in ws}))
for p in o.data.polygons:
    n = (o.matrix_world.to_3x3() @ p.normal).normalized()
    c = o.matrix_world @ p.center
    print("F", tuple(round(x,2) for x in n), "c", tuple(round(x,3) for x in c), "area", round(p.area*1e-4*1,4), "n", len(p.vertices))
