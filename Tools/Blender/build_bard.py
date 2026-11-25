"""
Bardo provisional de The Last Beat (tarea 23/26, prueba): esqueleto Humanoid (mixamorig:*) de 1.65 m, malla low-poly
estilizada con los colores del concept art (coleta roja, audífonos, chaqueta lila, pantalón morado ancho, botas, cinturón
con bolso) y el laúd morado con parlante, todo con pesos rígidos. Clips: Idle, Run, Sing (bucle de 2 tiempos a 100 BPM),
Hit y Die. Exporta un FBX listo para Unity.

Uso:
  snap run blender -b -P Tools/Blender/build_bard.py -- <salida.fbx> [--preview <carpeta>] [--blend <archivo.blend>]

Convenciones (las mismas que build_hero.py): 1 unidad = 1 m, +Z arriba, mira hacia -Y, su izquierda es +X, T-pose.
El laúd está pesado a la mano izquierda (LeftHand): su clavijero queda en la palma y el cuerpo, más allá, en +X en T-pose.
El esqueleto y los helpers de animación están en rig_common.py.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bmesh
import bpy

import rig_common as rc
from rig_common import (P, caja, clip, crear_armature, esfera, exportar, fijar_contacto, material, pies_planos,
                        piernas, prisma, torso, _paso)

K = 1.65 / 1.8  # escala respecto al esqueleto base de 1.8 m
rc.CLIP_FRAMES.update({"Idle": 60, "Run": 20, "Sing": 36, "Hit": 12, "Die": 42})
COLORES = rc.COLORES
COLORES.update({
    "BardHair": (0.55, 0.20, 0.17, 1.0),
    "BardSkin": (0.96, 0.74, 0.64, 1.0),
    "BardJacket": (0.68, 0.60, 0.90, 1.0),
    "BardPants": (0.47, 0.30, 0.67, 1.0),
    "BardCream": (0.96, 0.89, 0.72, 1.0),
    "BardGold": (0.95, 0.74, 0.20, 1.0),
    "BardBrown": (0.40, 0.25, 0.18, 1.0),
    "BardDark": (0.10, 0.10, 0.13, 1.0),
    "BardEye": (0.38, 0.28, 0.62, 1.0),
})


# ----------------------------------------------------------------------------------------------
# Malla: las coordenadas se escriben para el esqueleto de 1.8 m y se escalan por K al dibujar.
# ----------------------------------------------------------------------------------------------
def _k(v):
    return tuple(c * K for c in v)


def pr(bm, p0, p1, r0, r1, lados=8):
    rr = lambda r: tuple(c * K for c in r) if isinstance(r, tuple) else r * K
    prisma(bm, _k(p0), _k(p1), rr(r0), rr(r1), lados)


def cj(bm, centro, tam):
    caja(bm, _k(centro), _k(tam))


def es(bm, centro, radios):
    esfera(bm, _k(centro), _k(radios))


def cuerpo():
    """(hueso, material, función) de las piezas centrales."""
    L = []

    def add(hueso, mat, fn):
        L.append((P + hueso, mat, fn))

    # Cadera, cinturón, bolso y faldón
    add("Hips", "BardPants", lambda bm: pr(bm, (0, 0, 0.90), (0, 0, 1.07), (0.18, 0.115), (0.16, 0.105)))
    add("Hips", "BardBrown", lambda bm: pr(bm, (0, 0, 1.05), (0, 0, 1.10), (0.172, 0.112), (0.172, 0.112)))
    add("Hips", "BardGold", lambda bm: cj(bm, (0, -0.118, 1.075), (0.06, 0.02, 0.055)))
    add("Hips", "BardBrown", lambda bm: cj(bm, (-0.215, -0.02, 0.97), (0.05, 0.13, 0.14)))   # bolso (cadera derecha)
    add("Hips", "BardGold", lambda bm: cj(bm, (-0.243, -0.02, 1.02), (0.012, 0.04, 0.04)))
    add("Hips", "BardJacket", lambda bm: cj(bm, (0.10, -0.118, 0.99), (0.09, 0.02, 0.17)))   # faldón con la nota
    add("Hips", "BardGold", lambda bm: cj(bm, (0.10, -0.130, 0.99), (0.035, 0.008, 0.06)))
    # Torso: camisa crema y chaqueta lila corta
    add("Spine1", "BardCream", lambda bm: pr(bm, (0, 0, 1.08), (0, 0, 1.26), (0.14, 0.095), (0.145, 0.10)))
    add("Spine2", "BardJacket", lambda bm: pr(bm, (0, 0, 1.25), (0, 0, 1.49), (0.17, 0.11), (0.19, 0.115)))
    add("Spine2", "BardCream", lambda bm: cj(bm, (0, -0.118, 1.37), (0.08, 0.02, 0.21)))     # pechera crema
    add("Spine2", "BardGold", lambda bm: cj(bm, (0, -0.130, 1.37), (0.012, 0.008, 0.20)))
    # Cuello, cabeza y cara
    add("Neck", "BardSkin", lambda bm: pr(bm, (0, 0, 1.49), (0, 0, 1.60), 0.05, 0.05))
    add("Head", "BardSkin", lambda bm: es(bm, (0, 0, 1.69), (0.095, 0.10, 0.115)))
    add("Head", "BardEye", lambda bm: cj(bm, (0.04, -0.098, 1.70), (0.028, 0.02, 0.045)))
    add("Head", "BardEye", lambda bm: cj(bm, (-0.04, -0.098, 1.70), (0.028, 0.02, 0.045)))
    add("Head", "BardCream", lambda bm: cj(bm, (0, -0.103, 1.655), (0.03, 0.012, 0.012)))    # sonrisa
    # Pelo: casco, flequillo, lazo y coleta en cuatro segmentos
    add("Head", "BardHair", lambda bm: es(bm, (0, 0.025, 1.725), (0.105, 0.105, 0.105)))
    add("Head", "BardHair", lambda bm: cj(bm, (0, -0.075, 1.775), (0.19, 0.055, 0.05)))
    add("Head", "BardHair", lambda bm: cj(bm, (0.085, -0.05, 1.70), (0.03, 0.05, 0.12)))
    add("Head", "BardHair", lambda bm: cj(bm, (-0.085, -0.05, 1.70), (0.03, 0.05, 0.12)))
    add("Head", "BardGold", lambda bm: cj(bm, (0, 0.07, 1.825), (0.13, 0.045, 0.04)))
    add("Head", "BardHair", lambda bm: pr(bm, (0, 0.09, 1.80), (0, 0.17, 1.75), 0.05, 0.055))
    add("Head", "BardHair", lambda bm: pr(bm, (0, 0.17, 1.75), (0, 0.21, 1.64), 0.055, 0.05))
    add("Head", "BardHair", lambda bm: pr(bm, (0, 0.21, 1.64), (0, 0.19, 1.53), 0.05, 0.035))
    add("Head", "BardHair", lambda bm: pr(bm, (0, 0.19, 1.53), (0, 0.16, 1.45), 0.035, 0.015))
    # Audífonos al cuello: copas oscuras con centro lila y banda por detrás del cuello
    for s in (1, -1):
        add("Neck", "BardDark", lambda bm, s=s: pr(bm, (0.115 * s, -0.04, 1.50), (0.115 * s, -0.08, 1.50), 0.062, 0.062, 10))
        add("Neck", "BardEye", lambda bm, s=s: pr(bm, (0.115 * s, -0.08, 1.50), (0.115 * s, -0.088, 1.50), 0.04, 0.04, 10))
        add("Neck", "BardDark", lambda bm, s=s: cj(bm, (0.12 * s, 0.0, 1.55), (0.02, 0.10, 0.12)))
    add("Neck", "BardDark", lambda bm: cj(bm, (0, 0.06, 1.585), (0.2, 0.03, 0.03)))
    return L


def lado(nombre_lado, s):
    L = []

    def x(v):
        return (v[0] * s, v[1], v[2])

    def add(hueso, mat, fn):
        L.append((P + nombre_lado + hueso, mat, fn))

    add("Shoulder", "BardJacket", lambda bm: cj(bm, x((0.17, 0, 1.49)), (0.12, 0.14, 0.09)))
    add("Shoulder", "BardGold", lambda bm: cj(bm, x((0.20, 0, 1.54)), (0.10, 0.12, 0.012)))   # hombrera
    add("Arm", "BardJacket", lambda bm: pr(bm, x((0.17, 0, 1.46)), x((0.38, 0, 1.46)), 0.058, 0.052))
    add("Arm", "BardCream", lambda bm: pr(bm, x((0.38, 0, 1.46)), x((0.47, 0, 1.46)), 0.066, 0.066))   # puño crema
    add("ForeArm", "BardSkin", lambda bm: pr(bm, x((0.46, 0, 1.46)), x((0.72, 0, 1.46)), 0.040, 0.032))
    add("Hand", "BardSkin", lambda bm: cj(bm, x((0.77, 0, 1.46)), (0.09, 0.04, 0.09)))
    # Pantalón ancho con bolsillo cargo; bota con puño crema y punta dorada
    add("UpLeg", "BardPants", lambda bm: pr(bm, x((0.09, 0, 0.98)), x((0.10, 0, 0.54)), 0.105, 0.100))
    add("UpLeg", "BardPants", lambda bm: cj(bm, x((0.19, -0.01, 0.72)), (0.035, 0.085, 0.11)))
    add("UpLeg", "BardGold", lambda bm: cj(bm, x((0.208, -0.045, 0.74)), (0.012, 0.015, 0.015)))
    add("Leg", "BardPants", lambda bm: pr(bm, x((0.10, 0, 0.54)), x((0.09, 0, 0.26)), 0.100, 0.085))
    add("Leg", "BardCream", lambda bm: pr(bm, x((0.09, 0, 0.26)), x((0.09, 0, 0.19)), 0.075, 0.078))
    add("Leg", "BardBrown", lambda bm: pr(bm, x((0.09, 0, 0.19)), x((0.09, 0, 0.06)), 0.066, 0.056))
    add("Foot", "BardBrown", lambda bm: cj(bm, x((0.09, -0.07, 0.04)), (0.10, 0.25, 0.08)))
    add("Foot", "BardGold", lambda bm: cj(bm, x((0.09, -0.16, 0.04)), (0.095, 0.09, 0.075)))
    return L


def laud():
    """Cuerpo en forma de gota con el parlante al frente (-Y), mástil con cuerdas y clavijero; en la mano izquierda."""
    L = []
    mano = P + "LeftHand"

    def add(mat, fn):
        L.append((mano, mat, fn))

    z = 1.46
    add("BardGold", lambda bm: cj(bm, (0.75, 0.0, z), (0.10, 0.04, 0.06)))                     # clavijero
    add("BardBrown", lambda bm: cj(bm, (0.95, 0.0, z), (0.34, 0.035, 0.040)))                   # mástil
    add("BardCream", lambda bm: cj(bm, (0.95, -0.02, z), (0.34, 0.006, 0.006)))                 # cuerdas
    add("BardJacket", lambda bm: es(bm, (1.28, 0.0, z), (0.20, 0.050, 0.15)))                   # cuerpo
    add("BardJacket", lambda bm: es(bm, (1.40, 0.0, z), (0.14, 0.048, 0.11)))                   # punta
    add("BardGold", lambda bm: pr(bm, (1.27, -0.04, z), (1.27, -0.055, z), 0.068, 0.068, 10))   # aro del parlante
    add("BardDark", lambda bm: pr(bm, (1.27, -0.055, z), (1.27, -0.062, z), 0.052, 0.052, 10))  # parlante
    add("BardBrown", lambda bm: cj(bm, (1.40, -0.052, z), (0.07, 0.012, 0.018)))                # puente
    return L


def crear_malla(arm):
    nombres = list(COLORES)
    for n in nombres:
        material(n)
    bm = bmesh.new()
    mesh = bpy.data.meshes.new("Bard_Body")
    obj = bpy.data.objects.new("Bard_Body", mesh)
    bpy.context.scene.collection.objects.link(obj)
    for n in nombres:
        mesh.materials.append(bpy.data.materials[n])

    todas = list(cuerpo())
    for nombre_lado, s in (("Left", 1), ("Right", -1)):
        todas += lado(nombre_lado, s)
    laud_piezas = laud()
    todas += laud_piezas

    # El laúd se dibuja en coordenadas del esqueleto escaladas igual que el cuerpo, con la mano en x = 0.77 * K.
    indice = {}
    for hueso, _, _ in todas:
        if hueso not in indice:
            indice[hueso] = obj.vertex_groups.new(name=hueso).index
    dvert = bm.verts.layers.deform.verify()
    for hueso, mat, fn in todas:
        antes_v, antes_f = set(bm.verts), set(bm.faces)
        fn(bm)
        for v in bm.verts:
            if v not in antes_v:
                v[dvert][indice[hueso]] = 1.0
        for f in bm.faces:
            if f not in antes_f:
                f.material_index = nombres.index(mat)
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


# ----------------------------------------------------------------------------------------------
# Animación
# ----------------------------------------------------------------------------------------------
def llevar(**extra):
    """Postura base: el laúd cuelga de la mano izquierda, el brazo derecho cuelga suelto, rodillas ligeras."""
    p = {
        "LeftArm": [("Y", 74)], "RightArm": [("Y", -72)],
        "LeftForeArm": [("Z", -8)], "RightForeArm": [("Z", 10)],
        "LeftHand": [("Y", 8)],
        "LeftUpLeg": [("X", -3)], "RightUpLeg": [("X", 3)],
        "LeftLeg": [("X", 5)], "RightLeg": [("X", 5)],
    }
    p.update(extra)
    return p


def tocar(sway=0.0, rasgueo=0.0, pie=0.0, **extra):
    """
    Postura de cantar: el laúd cruza el pecho (mano izquierda en el mástil, a la izquierda del cuerpo) y la derecha rasguea
    sobre el parlante. sway balancea cadera y cabeza (-1 a 1); rasgueo mueve la mano derecha arriba/abajo (0 a 1);
    pie levanta la punta del pie derecho para marcar el tiempo (0 a 1).
    """
    p = {
        "LeftArm": [("Y", 38), ("Z", -36)],
        "LeftForeArm": [("Z", -104)],
        "LeftHand": [("Z", -10), ("Y", 8)],
        "RightArm": [("Y", -22), ("Z", 38)],
        "RightForeArm": [("Z", 60 + 14 * rasgueo)],
        "RightHand": [("Y", 12 * rasgueo - 6)],
        "LeftUpLeg": [("X", -5), ("Z", -3 * sway)], "RightUpLeg": [("X", 3 - 8 * pie), ("Z", -3 * sway)],
        "LeftLeg": [("X", 8)], "RightLeg": [("X", 6 + 14 * pie)],
    }
    p.update(torso(giro=-3 * sway, inclina=2))
    p["Hips"] = [("Z", 4 * sway)]
    p["Head"] = [("Z", -5 * sway), ("X", 2 * pie)]
    p["Hips.loc"] = (0.018 * sway, 0, -0.012 * abs(sway) - 0.01 * pie)
    p.update(extra)
    return pies_planos(p)


def crear_clips(arm):
    # --- Idle (60 f): respiración suave, laúd colgando
    def idle(resp, sway):
        p = llevar(**torso(0, 1.2 * resp))
        p["Hips.loc"] = (0, 0, -0.008 * resp)
        p["Head"] = [("X", -1.2 * resp), ("Z", 2 * sway)]
        p["LeftArm"] = [("Y", 74 + 1.5 * sway)]
        return pies_planos(p)

    clip(arm, "Idle", [(0, idle(0, 0)), (30, idle(1, 1)), (60, idle(0, 0))])

    # --- Run (20 f): zancada ligera, brazos opuestos, laúd balanceándose
    def run(adelante, vertical):
        a = 1 if adelante else -1
        p = {
            "LeftUpLeg": [("X", -36 * a)], "RightUpLeg": [("X", 36 * a)],
            "LeftLeg": [("X", 8 + (52 if a < 0 else 6))], "RightLeg": [("X", 8 + (52 if a > 0 else 6))],
            "LeftFoot": [("X", -10 * a)], "RightFoot": [("X", 10 * a)],
            "LeftArm": [("Y", 62), ("X", 28 * a)], "RightArm": [("Y", -60), ("X", -40 * a)],
            "LeftForeArm": [("Z", -62)], "RightForeArm": [("Z", 70)],
            "LeftHand": [("Y", 10)],
        }
        p.update(torso(giro=-7 * a, inclina=12))
        p["Hips.loc"] = (0, 0, vertical)
        return p

    clip(arm, "Run", [
        (0, run(True, -0.02)),
        (5, _paso(run(True, 0.03), run(False, 0.03), 0.5)),
        (10, run(False, -0.02)),
        (15, _paso(run(False, 0.03), run(True, 0.03), 0.5)),
        (20, run(True, -0.02)),
    ], suelo=False)

    # --- Sing (36 f = 2 tiempos a 100 BPM, bucle): rasgueo en cada tiempo, balanceo y pie marcando el compás
    clip(arm, "Sing", [
        (0, tocar(sway=-1, rasgueo=0, pie=0)),
        (4, tocar(sway=-0.6, rasgueo=1, pie=0.4)),
        (9, tocar(sway=0, rasgueo=0.3, pie=1)),
        (13, tocar(sway=0.6, rasgueo=0, pie=0.2)),
        (18, tocar(sway=1, rasgueo=0, pie=0)),
        (22, tocar(sway=0.6, rasgueo=1, pie=0.4)),
        (27, tocar(sway=0, rasgueo=0.3, pie=1)),
        (31, tocar(sway=-0.6, rasgueo=0, pie=0.2)),
        (36, tocar(sway=-1, rasgueo=0, pie=0)),
    ])

    # --- Hit (12 f, 0.4 s): sacudida hacia atrás y vuelta a la guardia
    def golpeada(atras, brazos):
        p = llevar(**torso(giro=-6 * atras, inclina=-20 * atras))
        p["Head"] = [("X", 18 * atras)]
        p["LeftArm"] = [("Y", 74 - 28 * brazos), ("X", 10 * brazos)]
        p["RightArm"] = [("Y", -72 + 40 * brazos), ("X", -10 * brazos)]
        p["LeftUpLeg"] = [("X", -3 + 10 * atras)]
        p["RightUpLeg"] = [("X", 3 - 6 * atras)]
        p["Hips.loc"] = (0, 0.05 * atras, -0.02 * atras)
        return pies_planos(p)

    clip(arm, "Hit", [
        (0, golpeada(0, 0)), (3, golpeada(1, 1)), (6, golpeada(0.7, 0.6)), (12, golpeada(0, 0)),
    ])

    # --- Die (42 f): tambalea y cae de espaldas; queda tendida en el suelo (la última pose se mantiene)
    def caida(angulo, y, z, piernas_x, brazos_y, cabeza):
        p = llevar()
        p["Hips"] = [("X", -angulo)]
        p["Spine1"] = [("X", -angulo * 0.1)]
        p["Head"] = [("X", cabeza)]
        p["LeftUpLeg"] = [("X", -piernas_x)]
        p["RightUpLeg"] = [("X", -piernas_x * 0.6)]
        p["LeftLeg"] = [("X", 6 + piernas_x * 0.4)]
        p["RightLeg"] = [("X", 6 + piernas_x * 0.3)]
        p["LeftArm"] = [("Y", brazos_y)]
        p["RightArm"] = [("Y", -brazos_y)]
        p["Hips.loc"] = (0, y, z)
        return p

    clip(arm, "Die", [
        (0, caida(0, 0, 0, 0, 74, 0)),
        (8, caida(14, 0.10, -0.03, 10, 50, 14)),
        (18, caida(55, 0.45, -0.38, 28, 35, 20)),
        (28, caida(86, 0.74, -0.80, 12, 60, 8)),
        (42, caida(88, 0.77, -0.84, 8, 66, 4)),
    ], suelo=False)


MUESTRAS = {
    "Idle": [0, 30],
    "Run": [0, 5, 10, 15],
    "Sing": [0, 4, 9, 18, 22],
    "Hit": [0, 3, 6],
    "Die": [0, 8, 18, 28, 42],
}


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if not args:
        raise SystemExit("Uso: blender -b -P build_bard.py -- <salida.fbx> [--preview <carpeta>] [--blend <archivo.blend>]")
    salida = args[0]
    preview = args[args.index("--preview") + 1] if "--preview" in args else None
    blend = args[args.index("--blend") + 1] if "--blend" in args else None

    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.unit_settings.system = "METRIC"
    sc.unit_settings.scale_length = 1.0
    sc.render.fps = rc.FPS

    arm = crear_armature(K)
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
    if blend:
        bpy.ops.wm.save_as_mainfile(filepath=blend)
        print(f"RES blend en {blend}")
    if preview:
        rc.vista_previa(arm, preview, MUESTRAS, objetivo_z=0.85, escala=2.6)
        print(f"RES vista previa en {preview}")


if __name__ == "__main__":
    main()
