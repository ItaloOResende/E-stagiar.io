"""
Extrai o braço livre do personagem oficial (character.fbx, Matheus) como modelo de 1ª pessoa.

Uso:
  blender -b --factory-startup --python Tools/blender/character_braco_fp.py -- <character.fbx> <saida.fbx>

- Pega as duas partes soltas do braço que NÃO segura o livro: o braço (267 vértices) e a manga (161 vértices),
  identificadas pelo lado +X do modelo. Geometria, UVs e material (slot 'Cylinder.001' =
  M_Character_CylinderLayer2 no Unity) ficam idênticos ao original; só muda posição/orientação/escala.
- Orienta o eixo longo do braço para a frente (-Y no Blender; vira +Z no Unity), com a origem no ombro
  (lado da manga) e a mão na outra ponta. Comprimento total = ARM_LENGTH.
"""
import bpy, bmesh, sys, os
from mathutils import Vector, Matrix

ARM_LENGTH = 0.5
src, out = [os.path.abspath(p) for p in sys.argv[sys.argv.index("--") + 1:]]

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)
obj = [o for o in bpy.data.objects if o.type == 'MESH'][0]
bpy.context.view_layer.objects.active = obj
obj.select_set(True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.separate(type='LOOSE')
bpy.ops.object.mode_set(mode='OBJECT')

parts = [o for o in bpy.data.objects if o.type == 'MESH']
cx = sum((o.matrix_world @ v.co).x for o in parts for v in o.data.vertices) / sum(len(o.data.vertices) for o in parts)


def center(o):
    return sum((v.co for v in o.data.vertices), Vector()) / len(o.data.vertices)


arm = [o for o in parts if len(o.data.vertices) == 267 and center(o).x > cx]
sleeve = [o for o in parts if len(o.data.vertices) == 161 and center(o).x > cx]
assert len(arm) == 1 and len(sleeve) == 1, (len(arm), len(sleeve))
arm, sleeve = arm[0], sleeve[0]
for o in parts:
    if o not in (arm, sleeve):
        bpy.data.objects.remove(o)

bpy.ops.object.select_all(action='DESELECT')
arm.select_set(True); sleeve.select_set(True)
bpy.context.view_layer.objects.active = arm
bpy.ops.object.join()
o = bpy.context.active_object
o.name = o.data.name = "Braco_FP"
bpy.ops.object.material_slot_remove_unused()  # só o slot do braço (Cylinder.001) fica

# eixo longo: do centro da manga (ombro) até o ponto do braço mais distante dele (mão)
verts = [v.co.copy() for v in o.data.vertices]
# a manga é a parte de cima do braço: os vértices mais altos (Z) marcam o ombro
top = max(v.z for v in verts)
shoulder = sum((v for v in verts if v.z > top - 0.15), Vector()) / max(1, sum(1 for v in verts if v.z > top - 0.15))
hand = max(verts, key=lambda v: (v - shoulder).length)
axis = (hand - shoulder).normalized()
length = (hand - shoulder).length

rot = axis.rotation_difference(Vector((0, -1, 0))).to_matrix().to_4x4()
k = ARM_LENGTH / length
o.data.transform(Matrix.Translation(-shoulder))
o.data.transform(rot)
o.data.transform(Matrix.Scale(k, 4))
o.location = (0, 0, 0)
o.data.update()
print("BRACO comprimento_original", round(length, 4), "escala", round(k, 4), "vertices", len(o.data.vertices),
      "materiais", [m.name for m in o.data.materials])

bpy.ops.export_scene.fbx(filepath=out, object_types={'MESH'}, apply_unit_scale=True,
                         apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
                         bake_space_transform=False, add_leaf_bones=False, bake_anim=False, path_mode='STRIP')
print("EXPORTADO", out)
