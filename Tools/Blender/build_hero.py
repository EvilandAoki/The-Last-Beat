"""
Héroe provisional de The Last Beat: esqueleto Humanoid (nombres mixamorig:*), malla low-poly
con pesos rígidos y clips Idle, Run, Spin (bucle; Unity gira el modelo), Charge (bucle; Unity desplaza al héroe), Light1, Light2, Heavy1 y Heavy2. Exporta un FBX listo para Unity.

Uso:
  snap run blender -b -P Tools/Blender/build_hero.py -- <salida.fbx> [--preview <carpeta>]

Convenciones: 1 unidad = 1 m, +Z arriba, el héroe mira hacia -Y y su izquierda es +X.
T-pose de 1.8 m. Los clips son 30 fps, solo rotaciones en huesos (y un poco de movimiento de caderas),
sin root motion. Los frames de impacto de cada ataque están en HIT_FRAMES (el script de editor de Unity
los usa para crear los Animation Events). El esqueleto y los helpers de animación están en rig_common.py.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bmesh
import bpy
from mathutils import Vector

import rig_common as rc
from rig_common import (P, caja, clip, crear_armature, esfera, exportar, fijar_contacto, material, pies_planos,
                        piernas, prisma, torso, _paso)

HIT_FRAMES = {"Light1": 9, "Light2": 9, "Heavy1": 16, "Heavy2": 17}
rc.CLIP_FRAMES.update({"Idle": 60, "Run": 20, "Spin": 20, "Charge": 16, "Light1": 22, "Light2": 22, "Heavy1": 34, "Heavy2": 38})
COLORES = rc.COLORES
COLORES.update({
    "HeroArmor": (0.45, 0.50, 0.58, 1.0),
    "HeroCloth": (0.42, 0.10, 0.12, 1.0),
    "HeroSkin": (0.78, 0.58, 0.46, 1.0),
    "HeroSword": (0.80, 0.82, 0.86, 1.0),
})

def piezas():
    """(hueso, material, función(bm) que dibuja la pieza). Solo lado izquierdo; se espeja con 'lado'."""
    L = []

    def add(hueso, mat, fn):
        L.append((hueso, mat, fn))

    add("Hips", "HeroCloth", lambda bm: prisma(bm, (0, 0, 0.92), (0, 0, 1.08), (0.17, 0.11), (0.16, 0.10)))
    add("Spine1", "HeroArmor", lambda bm: prisma(bm, (0, 0, 1.08), (0, 0, 1.25), (0.15, 0.10), (0.15, 0.10)))
    add("Spine2", "HeroArmor", lambda bm: prisma(bm, (0, 0, 1.25), (0, 0, 1.49), (0.19, 0.12), (0.21, 0.12)))
    add("Neck", "HeroSkin", lambda bm: prisma(bm, (0, 0, 1.49), (0, 0, 1.60), 0.05, 0.05))
    add("Head", "HeroSkin", lambda bm: esfera(bm, (0, 0, 1.69), (0.095, 0.11, 0.115)))
    add("Head", "HeroSkin", lambda bm: caja(bm, (0, -0.115, 1.68), (0.03, 0.04, 0.05)))  # nariz: marca el frente
    return L


def piezas_lado(lado, s):
    L = []

    def x(v):
        return (v[0] * s, v[1], v[2])

    def add(hueso, mat, fn):
        L.append((P + lado + hueso, mat, fn))

    add("Shoulder", "HeroArmor", lambda bm: caja(bm, x((0.17, 0, 1.49)), (0.13, 0.15, 0.10)))
    add("Arm", "HeroCloth", lambda bm: prisma(bm, x((0.17, 0, 1.46)), x((0.46, 0, 1.46)), 0.05, 0.04))
    add("ForeArm", "HeroArmor", lambda bm: prisma(bm, x((0.46, 0, 1.46)), x((0.72, 0, 1.46)), 0.045, 0.035))
    add("Hand", "HeroSkin", lambda bm: caja(bm, x((0.77, 0, 1.46)), (0.09, 0.04, 0.09)))
    add("UpLeg", "HeroCloth", lambda bm: prisma(bm, x((0.09, 0, 0.98)), x((0.09, 0, 0.54)), 0.085, 0.065))
    add("Leg", "HeroArmor", lambda bm: prisma(bm, x((0.09, 0, 0.54)), x((0.09, 0, 0.10)), 0.062, 0.045))
    add("Foot", "HeroArmor", lambda bm: caja(bm, x((0.09, -0.07, 0.045)), (0.09, 0.24, 0.09)))
    return L


def espadon():
    """Empuñadura en la mano derecha; hoja en la prolongación del antebrazo (-X en T-pose)."""
    L = []
    mano = P + "RightHand"

    def add(mat, fn):
        L.append((mano, mat, fn))

    add("HeroCloth", lambda bm: prisma(bm, (-0.78, 0, 1.46), (-0.89, 0, 1.46), 0.022, 0.022))
    add("HeroSword", lambda bm: caja(bm, (-0.90, 0, 1.46), (0.03, 0.16, 0.035)))  # guarda
    add("HeroSword", lambda bm: caja(bm, (-1.42, 0, 1.46), (1.04, 0.075, 0.014)))  # hoja
    return L


def crear_malla(arm):
    mat_nombres = list(COLORES)
    for n in mat_nombres:
        material(n)
    bm = bmesh.new()
    mesh = bpy.data.meshes.new("Hero_Body")
    obj = bpy.data.objects.new("Hero_Body", mesh)
    bpy.context.scene.collection.objects.link(obj)
    for n in mat_nombres:
        mesh.materials.append(bpy.data.materials[n])

    todas = []
    for hueso, mat, fn in piezas():
        todas.append((P + hueso, mat, fn))
    for lado, s in (("Left", 1), ("Right", -1)):
        todas += piezas_lado(lado, s)
    todas += espadon()

    # Un grupo de vértices por hueso; los pesos se escriben en la capa de deformación de bmesh,
    # sin depender de índices de vértice (que cambian al crear primitivas).
    indice_grupo = {}
    for hueso, _, _ in todas:
        if hueso not in indice_grupo:
            indice_grupo[hueso] = obj.vertex_groups.new(name=hueso).index
    dvert = bm.verts.layers.deform.verify()
    for hueso, mat, fn in todas:
        antes = set(bm.verts)
        antes_f = set(bm.faces)
        fn(bm)
        for v in bm.verts:
            if v not in antes:
                v[dvert][indice_grupo[hueso]] = 1.0
        for f in bm.faces:
            if f not in antes_f:
                f.material_index = mat_nombres.index(mat)
                f.smooth = False
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bm.to_mesh(mesh)
    bm.free()

    sin_peso = [v.index for v in mesh.vertices if not v.groups]
    if sin_peso:
        raise RuntimeError(f"Hay {len(sin_peso)} vértices sin peso: {sin_peso[:10]}")
    obj.parent = arm
    mod = obj.modifiers.new("Armature", "ARMATURE")
    mod.object = arm
    return obj


# Postura base: brazos abajo, espadón en alto hacia delante, rodillas ligeras.
def base(**extra):
    p = {
        "LeftArm": [("Y", 68)], "RightArm": [("Y", -64)],
        "LeftForeArm": [("Z", -18)], "RightForeArm": [("Z", 30)],
        "RightHand": [("Y", 150), ("X", 20)],
        "LeftUpLeg": [("X", -4)], "RightUpLeg": [("X", 4)],
        "LeftLeg": [("X", 6)], "RightLeg": [("X", 6)],
    }
    p.update(extra)
    return p


def golpe(giro=0, yaw=0, inclina=0, paso=0, lado="L", brazo=None, codo=0, mano=None, avance=0.0, izq=None):
    """Una pose de ataque con el brazo derecho. brazo = ops de RightArm; el resto acompaña."""
    p = base(**torso(giro, inclina))
    p["Hips"] = [("Z", yaw)]
    p["RightArm"] = brazo if brazo is not None else [("Y", -64)]
    p["RightForeArm"] = [("Z", codo)]
    p["RightHand"] = mano if mano is not None else [("Y", 20)]
    p["LeftArm"] = izq if izq is not None else [("Y", 60), ("X", -giro * 0.5)]
    piernas(p, lado, paso, yaw)
    p["Hips.loc"] = (0, -0.14 * paso * avance, 0)
    return pies_planos(p)


def crear_clips(arm):
    # --- Idle (60 f): respiración suave y balanceo del arma
    def idle(resp, sway):
        p = base(**torso(0, 1.5 * resp))
        p["Hips.loc"] = (0, 0, -0.01 * resp)
        p["Head"] = [("X", -1.0 * resp)]
        p["RightHand"] = [("Y", 150 + 3 * sway), ("X", 20)]
        p["RightArm"] = [("Y", -64 + 2 * sway)]
        return pies_planos(p)

    clip(arm, "Idle", [(0, idle(0, 0)), (30, idle(1, 1)), (60, idle(0, 0))])

    # --- Run (20 f): zancada con brazos opuestos e inclinación al frente
    def run(pierna_izq_adelante, fase_vertical):
        a = 1 if pierna_izq_adelante else -1
        p = {
            "LeftUpLeg": [("X", -38 * a)], "RightUpLeg": [("X", 38 * a)],
            "LeftLeg": [("X", 8 + (55 if a < 0 else 6))], "RightLeg": [("X", 8 + (55 if a > 0 else 6))],
            "LeftFoot": [("X", -10 * a)], "RightFoot": [("X", 10 * a)],
            "LeftArm": [("Y", 60), ("X", 40 * a)], "RightArm": [("Y", -60), ("X", -40 * a)],
            "LeftForeArm": [("Z", -75)], "RightForeArm": [("Z", 75)],
            "RightHand": [("Y", 150), ("X", 20)],
        }
        p.update(torso(giro=-8 * a, inclina=14))
        p["Hips.loc"] = (0, 0, fase_vertical)
        return p

    clip(arm, "Run", [
        (0, run(True, -0.02)),
        (5, _paso(run(True, 0.03), run(False, 0.03), 0.5)),
        (10, run(False, -0.02)),
        (15, _paso(run(False, 0.03), run(True, 0.03), 0.5)),
        (20, run(True, -0.02)),
    ], suelo=False)

    guardia = [("Y", 150), ("X", 20)]  # espadón en alto, igual que Idle
    medio = [("Y", 90), ("X", 10)]     # la muñeca gira entre la guardia y el golpe
    reposo = golpe(mano=guardia)

    # --- Spin (20 f, en bucle): postura de giro con el espadón extendido; Unity rota el modelo alrededor de Y.
    def spin(vaiven):
        p = golpe(giro=7 * vaiven, yaw=0, inclina=10, paso=0.4, lado="L",
                  brazo=[("Y", -8), ("Z", 95)], codo=8,
                  izq=[("Y", 8), ("Z", -65)])
        p["LeftForeArm"] = [("Z", -10)]
        p["Hips.loc"] = (0, 0, -0.012 * abs(vaiven))
        return p

    clip(arm, "Spin", [(0, spin(0)), (5, spin(1)), (10, spin(0)), (15, spin(-1)), (20, spin(0))])

    # --- Charge (16 f, en bucle): carrera inclinada con el espadón al frente; Unity desplaza al héroe.
    def carga(pierna_izq_adelante, bote):
        a = 1 if pierna_izq_adelante else -1
        p = {
            "LeftUpLeg": [("X", -48 * a)], "RightUpLeg": [("X", 48 * a)],
            "LeftLeg": [("X", 10 + (62 if a < 0 else 8))], "RightLeg": [("X", 10 + (62 if a > 0 else 8))],
            "LeftFoot": [("X", -12 * a)], "RightFoot": [("X", 12 * a)],
            "LeftArm": [("Y", 55), ("X", 45 * a)], "LeftForeArm": [("Z", -70)],
            "RightArm": [("Y", -8), ("Z", 108)], "RightForeArm": [("Z", 14)],
            "RightHand": [("Y", 20)],
        }
        p.update(torso(giro=-4 * a, inclina=28))
        p["Hips.loc"] = (0, 0, bote)
        return p

    clip(arm, "Charge", [
        (0, carga(True, -0.03)),
        (4, _paso(carga(True, 0.04), carga(False, 0.04), 0.5)),
        (8, carga(False, -0.03)),
        (12, _paso(carga(False, 0.04), carga(True, 0.04), 0.5)),
        (16, carga(True, -0.03)),
    ], suelo=False)

    # --- Light1 (22 f, impacto en 9): tajo horizontal de derecha a izquierda, avanza el pie izquierdo
    clip(arm, "Light1", [
        (0, reposo),
        (3, golpe(giro=6, yaw=6, paso=0.2, lado="L", brazo=[("Y", -30)], codo=40, mano=medio)),
        (6, golpe(giro=-34, yaw=-14, inclina=4, paso=0.55, lado="L", brazo=[("Y", -8), ("Z", -45)], codo=60)),
        (9, golpe(giro=30, yaw=20, inclina=8, paso=1.0, lado="L", brazo=[("Y", -8), ("Z", 95)], codo=10, avance=1)),
        (13, golpe(giro=44, yaw=26, inclina=8, paso=1.0, lado="L", brazo=[("Y", -8), ("Z", 120)], codo=10, avance=1)),
        (17, golpe(giro=16, yaw=10, inclina=3, paso=0.5, lado="L", brazo=[("Y", -40), ("Z", 55)], codo=25, avance=1, mano=medio)),
        (22, reposo),
    ])
    # --- Light2 (22 f, impacto en 9): revés de izquierda a derecha, avanza el pie derecho
    clip(arm, "Light2", [
        (0, reposo),
        (3, golpe(giro=-6, yaw=-6, paso=0.2, lado="R", brazo=[("Y", -30)], codo=40, mano=medio)),
        (6, golpe(giro=36, yaw=18, inclina=4, paso=0.55, lado="R", brazo=[("Y", -6), ("Z", 115)], codo=10)),
        (9, golpe(giro=-34, yaw=-20, inclina=8, paso=1.0, lado="R", brazo=[("Y", -6), ("Z", -35)], codo=35, avance=1)),
        (13, golpe(giro=-46, yaw=-26, inclina=8, paso=1.0, lado="R", brazo=[("Y", -6), ("Z", -52)], codo=45, avance=1)),
        (17, golpe(giro=-16, yaw=-10, inclina=3, paso=0.5, lado="R", brazo=[("Y", -40), ("Z", -20)], codo=30, avance=1, mano=medio)),
        (22, reposo),
    ])
    # --- Heavy1 (34 f, impacto en 16): tajo vertical descendente con paso profundo del pie izquierdo
    clip(arm, "Heavy1", [
        (0, reposo),
        (5, golpe(giro=4, yaw=0, inclina=-3, paso=0.15, lado="L", brazo=[("Y", 40), ("X", -10)], codo=30, mano=medio)),
        (12, golpe(giro=8, yaw=6, inclina=-12, paso=0.3, lado="L", brazo=[("Y", 100), ("X", -35)], codo=25,
                   izq=[("Y", 40), ("X", -20)])),
        (16, golpe(giro=-4, yaw=-4, inclina=24, paso=1.0, lado="L", brazo=[("Y", -35), ("Z", 80)], codo=6, avance=1)),
        (20, golpe(giro=-6, yaw=-4, inclina=26, paso=1.0, lado="L", brazo=[("Y", -42), ("Z", 82)], codo=6, avance=1)),
        (27, golpe(giro=0, yaw=0, inclina=10, paso=0.5, lado="L", brazo=[("Y", -50), ("Z", 40)], codo=18, avance=1, mano=medio)),
        (34, reposo),
    ])
    # --- Heavy2 (38 f, impacto en 17): barrido amplio con giro de todo el cuerpo, remate del combo
    clip(arm, "Heavy2", [
        (0, reposo),
        (6, golpe(giro=-52, yaw=-30, inclina=6, paso=0.6, lado="R", brazo=[("Y", -8), ("Z", -60)], codo=70)),
        (12, golpe(giro=8, yaw=4, inclina=8, paso=0.8, lado="R", brazo=[("Y", -8), ("Z", 40)], codo=30, avance=1)),
        (17, golpe(giro=62, yaw=40, inclina=10, paso=1.0, lado="L", brazo=[("Y", -8), ("Z", 118)], codo=8, avance=1)),
        (22, golpe(giro=70, yaw=50, inclina=8, paso=1.0, lado="L", brazo=[("Y", -8), ("Z", 135)], codo=8, avance=1)),
        (30, golpe(giro=24, yaw=18, inclina=4, paso=0.5, lado="L", brazo=[("Y", -40), ("Z", 60)], codo=24, avance=1, mano=medio)),
        (38, reposo),
    ])


MUESTRAS = {
    "Idle": [0, 30],
    "Run": [0, 5, 10, 15],
    "Spin": [0, 5, 15],
    "Charge": [0, 4, 8],
    "Light1": [0, 6, 9, 13],
    "Light2": [0, 6, 9, 13],
    "Heavy1": [0, 12, 16, 20],
    "Heavy2": [0, 6, 17, 22],
}


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if not args:
        raise SystemExit("Uso: blender -b -P build_hero.py -- <salida.fbx> [--preview <carpeta>]")
    salida = args[0]
    preview = args[args.index("--preview") + 1] if "--preview" in args else None

    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.unit_settings.system = "METRIC"
    sc.unit_settings.scale_length = 1.0
    sc.render.fps = rc.FPS

    arm = crear_armature()
    crear_malla(arm)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="POSE")
    fijar_contacto(arm)
    crear_clips(arm)
    bpy.ops.object.mode_set(mode="OBJECT")

    huesos = len(arm.data.bones)
    tris = sum(len(p.vertices) - 2 for o in bpy.data.objects if o.type == "MESH" for p in o.data.polygons)
    print(f"RES huesos={huesos} tris={tris} acciones={sorted(a.name for a in bpy.data.actions)}")
    exportar(salida)
    print(f"RES exportado {salida}")
    if preview:
        rc.vista_previa(arm, preview, MUESTRAS)
        print(f"RES vista previa en {preview}")


main()
