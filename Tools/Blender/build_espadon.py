"""
El espadón del caballero como modelo aparte (tarea 22): las mismas piezas y colores que build_hero.py
(hoja azul con filos plateados, guarda plateada con gema azul, empuñadura oscura y pomo plateado).
Origen en el centro de la empuñadura y hoja hacia +Z (en Unity, +Y). Unity lo pone de hijo de la mano derecha.

Uso:
  blender -b -P Tools/Blender/build_espadon.py -- <salida.fbx>
"""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import build_hero as H  # noqa: E402  (las piezas del espadón viven en build_hero.espadon)

AGARRE_X = -0.825  # centro de la empuñadura en el héroe en T-pose (entre -0.76 y -0.89)


def main():
    salida = sys.argv[sys.argv.index("--") + 1]
    bpy.ops.wm.read_factory_settings(use_empty=True)
    piezas = H.espadon()
    usados = sorted({mat for _, mat, _ in piezas})
    for n in usados:
        H.material(n)
    mesh = bpy.data.meshes.new("Espadon")
    obj = bpy.data.objects.new("Espadon", mesh)
    bpy.context.scene.collection.objects.link(obj)
    for n in usados:
        mesh.materials.append(bpy.data.materials[n])
    bm = bmesh.new()
    for _, mat, fn in piezas:
        antes = set(bm.faces)
        fn(bm)
        for f in bm.faces:
            if f not in antes:
                f.material_index = usados.index(mat)
    # De la mano del héroe al origen, y la hoja (que apuntaba a -X) hacia +Z.
    bmesh.ops.translate(bm, verts=bm.verts, vec=(-AGARRE_X, 0, -1.46))
    bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=Matrix.Rotation(math.radians(90), 3, "Y"))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bm.to_mesh(mesh)
    bm.free()
    zs = [v.co.z for v in mesh.vertices]
    print(f"RES espadón: {len(mesh.polygons)} caras, de {min(zs):.2f} a {max(zs):.2f} m en Z")
    bpy.ops.export_scene.fbx(filepath=salida, use_selection=False, object_types={"MESH"}, axis_forward="-Z",
                             axis_up="Y", apply_scale_options="FBX_SCALE_UNITS", mesh_smooth_type="FACE")


main()
