"""
Deja un personaje con rig de Mixamo en T-pose de verdad (tareas 22 a 28).
Mixamo descarga la malla y el esqueleto en la pose original (aquí, brazos abajo) y guarda la T-pose solo como
un clip de un cuadro. Este script aplica ese cuadro como pose base de la malla y del esqueleto y exporta otra vez.
No es imprescindible: se comprobó que Unity crea bien el avatar Humanoid con los brazos abajo (la misma
animación deja el brazo en el mismo ángulo con las dos versiones). Solo deja el modelo en T en el editor.

Uso:
  blender -b -P Tools/Blender/mixamo_tpose.py -- <descarga_de_mixamo.fbx> <salida.fbx>
"""
import sys

import bpy

origen, destino = sys.argv[sys.argv.index("--") + 1:][:2]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=origen)
arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
mallas = [o for o in bpy.data.objects if o.type == "MESH"]
bpy.context.scene.frame_set(0)  # el cuadro de T-pose que trae Mixamo


def altura(hueso):
    return (arm.matrix_world @ arm.pose.bones["mixamorig:" + hueso].head).z


print(f"RES antes: mano {altura('RightHand'):.3f} / hombro {altura('RightArm'):.3f}")
# 1) La malla se queda con la deformación de la T-pose.
for m in mallas:
    bpy.context.view_layer.objects.active = m
    for mod in [mod for mod in m.modifiers if mod.type == "ARMATURE"]:
        bpy.ops.object.modifier_apply(modifier=mod.name)
# 2) Esa pose pasa a ser la pose base del esqueleto y se borra el clip.
bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode="POSE")
bpy.ops.pose.select_all(action="SELECT")
bpy.ops.pose.armature_apply(selected=False)
bpy.ops.object.mode_set(mode="OBJECT")
arm.animation_data_clear()
for accion in list(bpy.data.actions):
    bpy.data.actions.remove(accion)
# 3) La malla vuelve a seguir al esqueleto (los grupos de vértices se conservan).
for m in mallas:
    mod = m.modifiers.new("Armature", "ARMATURE")
    mod.object = arm
bpy.context.view_layer.update()
print(f"RES después: mano {altura('RightHand'):.3f} / hombro {altura('RightArm'):.3f}")
bpy.ops.export_scene.fbx(filepath=destino, use_selection=False, object_types={"ARMATURE", "MESH"},
                         add_leaf_bones=False, bake_anim=False, axis_forward="-Z", axis_up="Y",
                         apply_scale_options="FBX_SCALE_UNITS", primary_bone_axis="Y", secondary_bone_axis="X",
                         mesh_smooth_type="FACE", path_mode="COPY", embed_textures=True)
print(f"RES exportado {destino}")
