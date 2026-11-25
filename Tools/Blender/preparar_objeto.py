"""
Prepara un objeto de Tripo3D (arma o instrumento) para Unity: lo escala a su largo real, pone el origen donde lo
agarra la mano y deja su eje largo hacia +Z (en Unity, +Y). Exporta un FBX sin rig; la textura se copia aparte
(Unity no toma la que viene dentro del FBX).

Uso:
  blender -b -P Tools/Blender/preparar_objeto.py -- <entrada.fbx> <salida.fbx> <largo_m> <modo>
modo:
  empunadura  espada: el agarre es el centro entre la guarda (la parte más ancha) y el extremo corto
  mastil      instrumento: el agarre es el centro del mástil (la mitad estrecha), el cuerpo queda hacia -Z
"""
import sys

import bpy
from mathutils import Matrix, Vector

entrada, salida, largo, modo = sys.argv[sys.argv.index("--") + 1:][:4]
largo = float(largo)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=entrada)
obj = next(o for o in bpy.data.objects if o.type == "MESH")
bpy.context.view_layer.objects.active = obj
obj.select_set(True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
pts = [v.co.copy() for v in obj.data.vertices]

# Eje largo y perfil de anchos a lo largo de él (20 cortes)
mn = Vector((min(p[i] for p in pts) for i in range(3)))
mx = Vector((max(p[i] for p in pts) for i in range(3)))
eje = max(range(3), key=lambda i: mx[i] - mn[i])
otros = [i for i in range(3) if i != eje]
L = mx[eje] - mn[eje]
cortes = 20
anchos = []
for k in range(cortes):
    a, b = mn[eje] + L * k / cortes, mn[eje] + L * (k + 1) / cortes
    sel = [p for p in pts if a <= p[eje] <= b]
    anchos.append(max((max(p[otros[0]] for p in sel) - min(p[otros[0]] for p in sel)) if sel else 0,
                      (max(p[otros[1]] for p in sel) - min(p[otros[1]] for p in sel)) if sel else 0))
print("RES perfil de anchos:", " ".join(f"{w:.2f}" for w in anchos))

if modo == "empunadura":
    guarda = max(range(cortes), key=lambda k: anchos[k])
    extremo = 0 if guarda < cortes / 2 else cortes  # la empuñadura es el lado corto desde la guarda
    agarre_frac = (guarda + 0.5 + extremo) / 2 / cortes
    hacia_hoja = 1 if extremo == 0 else -1           # la hoja va hacia el lado largo
else:  # mastil: la mitad más estrecha es el mástil; el cuerpo, la más ancha
    mitad_baja = sum(anchos[:cortes // 2])
    mitad_alta = sum(anchos[cortes // 2:])
    mastil_arriba = mitad_alta < mitad_baja
    agarre_frac = 0.72 if mastil_arriba else 0.28
    hacia_hoja = 1 if mastil_arriba else -1          # para el instrumento, "hoja" = mástil

agarre = mn.copy()
centro = (mn + mx) / 2
agarre[otros[0]], agarre[otros[1]] = centro[otros[0]], centro[otros[1]]
agarre[eje] = mn[eje] + L * agarre_frac
print(f"RES eje {'XYZ'[eje]}, largo {L:.3f}, agarre al {agarre_frac:.0%}, sentido {hacia_hoja:+d}")

# Origen en el agarre, eje largo hacia +Z con la hoja (o el mástil) hacia +Z, y escala al largo real
direccion = Vector((0, 0, 0))
direccion[eje] = hacia_hoja
giro = direccion.rotation_difference(Vector((0, 0, 1))).to_matrix().to_4x4()
obj.data.transform(Matrix.Translation(-agarre))
obj.data.transform(giro)
obj.data.transform(Matrix.Scale(largo / L, 4))
obj.data.update()
zs = [v.co.z for v in obj.data.vertices]
print(f"RES listo: de {min(zs):.2f} a {max(zs):.2f} m en Z, {len(obj.data.polygons)} caras")
bpy.ops.export_scene.fbx(filepath=salida, use_selection=False, object_types={"MESH"}, axis_forward="-Z", axis_up="Y",
                         apply_scale_options="FBX_SCALE_UNITS", mesh_smooth_type="FACE")
