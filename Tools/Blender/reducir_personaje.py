"""
Baja los triángulos de un personaje con rig de Mixamo sin romper el rig (los pesos se interpolan al reducir) y
saca su textura a un archivo aparte (Unity no toma la que viene dentro del FBX).

Uso:
  blender -b -P Tools/Blender/reducir_personaje.py -- <mixamo.fbx> <salida.fbx> <max_triangulos> <textura.jpg>
"""
import sys

import bpy

entrada, salida, maximo, textura = sys.argv[sys.argv.index("--") + 1:][:4]
maximo = int(maximo)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=entrada)
arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
arm.animation_data_clear()  # el cuadro de T-pose de Mixamo no hace falta: Unity arma bien el avatar igual
for accion in list(bpy.data.actions):
    bpy.data.actions.remove(accion)


def tris(m):
    return sum(len(p.vertices) - 2 for p in m.data.polygons)


for m in [o for o in bpy.data.objects if o.type == "MESH"]:
    antes = tris(m)
    if antes > maximo:
        bpy.context.view_layer.objects.active = m
        mod = m.modifiers.new("Reducir", "DECIMATE")
        mod.ratio = (maximo * 0.97) / antes
        # Primero en la pila: se reduce la malla en reposo, no la deformada por el esqueleto.
        bpy.ops.object.modifier_move_to_index(modifier=mod.name, index=0)
        bpy.ops.object.modifier_apply(modifier=mod.name)
    print(f"RES {m.name}: {antes} -> {tris(m)} triángulos, {len(m.vertex_groups)} grupos de pesos")

for img in bpy.data.images:
    if img.packed_file:
        with open(textura, "wb") as f:
            f.write(img.packed_file.data)
        print(f"RES textura {img.size[0]}x{img.size[1]} -> {textura}")
        break

bpy.ops.export_scene.fbx(filepath=salida, use_selection=False, object_types={"ARMATURE", "MESH"},
                         add_leaf_bones=False, bake_anim=False, axis_forward="-Z", axis_up="Y",
                         apply_scale_options="FBX_SCALE_UNITS", primary_bone_axis="Y", secondary_bone_axis="X",
                         mesh_smooth_type="FACE")
print(f"RES exportado {salida}")
