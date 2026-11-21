"""
Héroe provisional de The Last Beat: esqueleto Humanoid (nombres mixamorig:*), malla low-poly
con pesos rígidos y clips Idle, Run, Attack1, Attack2, Attack3. Exporta un FBX listo para Unity.

Uso:
  snap run blender -b -P Tools/Blender/build_hero.py -- <salida.fbx> [--preview <carpeta>]

Convenciones: 1 unidad = 1 m, +Z arriba, el héroe mira hacia -Y y su izquierda es +X.
T-pose de 1.8 m. Los clips son 30 fps, solo rotaciones en huesos (y un poco de movimiento de caderas),
sin root motion. Los frames de impacto de cada ataque están en HIT_FRAMES (el script de editor de Unity
los usa para crear los Animation Events).
"""
import math
import sys

import bmesh
import bpy
from mathutils import Euler, Matrix, Vector

FPS = 30
HIT_FRAMES = {"Attack1": 12, "Attack2": 11, "Attack3": 13}

# ----------------------------------------------------------------------------------------------
# Esqueleto
# ----------------------------------------------------------------------------------------------
P = "mixamorig:"

# (nombre, padre, cabeza, cola)   -- solo el lado izquierdo (+X); el derecho se espeja.
CENTRO = [
    ("Hips", None, (0, 0, 0.98), (0, 0, 1.05)),
    ("Spine", "Hips", (0, 0, 1.05), (0, 0, 1.14)),
    ("Spine1", "Spine", (0, 0, 1.14), (0, 0, 1.26)),
    ("Spine2", "Spine1", (0, 0, 1.26), (0, 0, 1.48)),
    ("Neck", "Spine2", (0, 0, 1.48), (0, 0, 1.58)),
    ("Head", "Neck", (0, 0, 1.58), (0, 0, 1.78)),
    ("HeadTop_End", "Head", (0, 0, 1.78), (0, 0, 1.83)),
]

BRAZO = [
    ("Shoulder", "Spine2", (0.03, 0, 1.46), (0.17, 0, 1.46)),
    ("Arm", "Shoulder", (0.17, 0, 1.46), (0.46, 0, 1.46)),
    ("ForeArm", "Arm", (0.46, 0, 1.46), (0.72, 0, 1.46)),
    ("Hand", "ForeArm", (0.72, 0, 1.46), (0.80, 0, 1.46)),
]

DEDOS = {  # nombre: (desplazamiento en Y, longitudes de las 4 falanges)
    "Thumb": (-0.035, (0.035, 0.030, 0.025, 0.020)),
    "Index": (-0.025, (0.040, 0.025, 0.020, 0.015)),
    "Middle": (-0.005, (0.045, 0.028, 0.022, 0.015)),
    "Ring": (0.015, (0.040, 0.025, 0.020, 0.015)),
    "Pinky": (0.032, (0.032, 0.020, 0.015, 0.012)),
}

PIERNA = [
    ("UpLeg", "Hips", (0.09, 0, 0.98), (0.09, 0, 0.54)),
    ("Leg", "UpLeg", (0.09, 0, 0.54), (0.09, 0, 0.10)),
    ("Foot", "Leg", (0.09, 0, 0.10), (0.09, -0.12, 0.035)),
    ("ToeBase", "Foot", (0.09, -0.12, 0.035), (0.09, -0.21, 0.02)),
    ("Toe_End", "ToeBase", (0.09, -0.21, 0.02), (0.09, -0.25, 0.02)),
]


def espejo(v):
    return (-v[0], v[1], v[2])


def definir_huesos():
    huesos = []  # (nombre completo, padre completo, cabeza, cola)
    for n, p, h, t in CENTRO:
        huesos.append((P + n, P + p if p else None, h, t))
    for lado, signo in (("Left", 1), ("Right", -1)):
        f = (lambda v: v) if signo == 1 else espejo
        for n, p, h, t in BRAZO:
            padre = P + (p if p == "Spine2" else lado + p)
            huesos.append((P + lado + n, padre, f(h), f(t)))
        mano_cola = BRAZO[-1][3]
        for dedo, (dy, largos) in DEDOS.items():
            x = mano_cola[0]
            anterior = P + lado + "Hand"
            for i, largo in enumerate(largos, start=1):
                cabeza = (x, mano_cola[1] + dy, mano_cola[2])
                cola = (x + largo, mano_cola[1] + dy, mano_cola[2])
                nombre = P + lado + "Hand" + dedo + str(i)
                huesos.append((nombre, anterior, f(cabeza), f(cola)))
                anterior = nombre
                x += largo
        for n, p, h, t in PIERNA:
            padre = P + (p if p == "Hips" else lado + p)
            huesos.append((P + lado + n, padre, f(h), f(t)))
    return huesos


def crear_armature():
    arm_data = bpy.data.armatures.new("Armature")
    arm = bpy.data.objects.new("Armature", arm_data)
    bpy.context.scene.collection.objects.link(arm)
    bpy.context.view_layer.objects.active = arm
    arm.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    for nombre, padre, cabeza, cola in definir_huesos():
        eb = arm_data.edit_bones.new(nombre)
        eb.head = Vector(cabeza)
        eb.tail = Vector(cola)
        eb.roll = 0.0
        if padre:
            eb.parent = arm_data.edit_bones[padre]
            eb.use_connect = False
    bpy.ops.object.mode_set(mode="OBJECT")
    return arm


# ----------------------------------------------------------------------------------------------
# Malla provisional (piezas rígidas pesadas al 100 % a su hueso)
# ----------------------------------------------------------------------------------------------
COLORES = {
    "HeroArmor": (0.45, 0.50, 0.58, 1.0),
    "HeroCloth": (0.42, 0.10, 0.12, 1.0),
    "HeroSkin": (0.78, 0.58, 0.46, 1.0),
    "HeroSword": (0.80, 0.82, 0.86, 1.0),
}


def material(nombre):
    m = bpy.data.materials.get(nombre)
    if m is None:
        m = bpy.data.materials.new(nombre)
        m.use_nodes = True
        bsdf = m.node_tree.nodes.get("Principled BSDF")
        bsdf.inputs["Base Color"].default_value = COLORES[nombre]
        bsdf.inputs["Roughness"].default_value = 0.6
        m.diffuse_color = COLORES[nombre]
    return m


def prisma(bm, p0, p1, r0, r1, lados=8):
    """Tubo cónico de sección elíptica entre dos puntos. r = (radio_x, radio_y) en el plano perpendicular."""
    p0, p1 = Vector(p0), Vector(p1)
    eje = (p1 - p0).normalized()
    ref = Vector((0, 1, 0)) if abs(eje.y) < 0.9 else Vector((1, 0, 0))
    u = eje.cross(ref).normalized()
    v = eje.cross(u).normalized()
    anillos = []
    for p, r in ((p0, r0), (p1, r1)):
        rx, ry = (r, r) if isinstance(r, (int, float)) else r
        anillo = []
        for i in range(lados):
            a = 2 * math.pi * i / lados
            anillo.append(bm.verts.new(p + u * (math.cos(a) * rx) + v * (math.sin(a) * ry)))
        anillos.append(anillo)
    for i in range(lados):
        j = (i + 1) % lados
        bm.faces.new((anillos[0][i], anillos[0][j], anillos[1][j], anillos[1][i]))
    bm.faces.new(anillos[0][::-1])
    bm.faces.new(anillos[1])


def caja(bm, centro, tam):
    m = Matrix.Translation(Vector(centro))
    bmesh.ops.create_cube(bm, size=1.0, matrix=m @ Matrix.Diagonal((tam[0], tam[1], tam[2], 1.0)))


def esfera(bm, centro, radios):
    m = Matrix.Translation(Vector(centro)) @ Matrix.Diagonal((radios[0], radios[1], radios[2], 1.0))
    bmesh.ops.create_uvsphere(bm, u_segments=8, v_segments=6, radius=1.0, matrix=m)


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

    grupos = {}
    for hueso, mat, fn in todas:
        antes = set(bm.verts)
        antes_f = set(bm.faces)
        fn(bm)
        bm.verts.index_update()
        nuevos = [v for v in bm.verts if v not in antes]
        for f in bm.faces:
            if f not in antes_f:
                f.material_index = mat_nombres.index(mat)
                f.smooth = False
        grupos.setdefault(hueso, []).extend(v.index for v in nuevos)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bm.to_mesh(mesh)
    bm.free()

    for hueso, indices in grupos.items():
        vg = obj.vertex_groups.new(name=hueso)
        vg.add(indices, 1.0, "REPLACE")
    obj.parent = arm
    mod = obj.modifiers.new("Armature", "ARMATURE")
    mod.object = arm
    return obj


# ----------------------------------------------------------------------------------------------
# Animación (FK en ejes del mundo en reposo; ver docstring de rotar)
# ----------------------------------------------------------------------------------------------
def R(eje, grados):
    return Matrix.Rotation(math.radians(grados), 3, eje)


def combinar(ops):
    """ops = [('Y', 65), ('Z', -20)]  → primero Y, luego Z, ambos en ejes del mundo en reposo."""
    m = Matrix.Identity(3)
    for eje, grados in ops:
        m = R(eje, grados) @ m
    return m


def aplicar_pose(arm, pose, frame):
    """pose: {hueso_sin_prefijo: ops | ('mover', (dx,dy,dz))}. Los no listados vuelven a reposo."""
    for pb in arm.pose.bones:
        corto = pb.name[len(P):]
        rot_reposo = pb.bone.matrix_local.to_3x3()
        spec = pose.get(corto)
        pb.rotation_mode = "QUATERNION"
        if spec is None:
            pb.rotation_quaternion = (1, 0, 0, 0)
        else:
            rot = combinar(spec)
            pb.rotation_quaternion = (rot_reposo.inverted() @ rot @ rot_reposo).to_quaternion()
        pb.keyframe_insert("rotation_quaternion", frame=frame)
        desp = pose.get(corto + ".loc")
        if desp is not None:
            pb.location = rot_reposo.inverted() @ Vector(desp)
        else:
            pb.location = (0, 0, 0)
        if corto == "Hips":
            pb.keyframe_insert("location", frame=frame)


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


def nueva_accion(arm, nombre, frames):
    accion = bpy.data.actions.new(nombre)
    accion.use_fake_user = True
    if arm.animation_data is None:
        arm.animation_data_create()
    arm.animation_data.action = accion
    accion.use_frame_range = True
    accion.frame_start, accion.frame_end = 0, frames
    return accion


def clip(arm, nombre, frames, claves):
    nueva_accion(arm, nombre, frames)
    for frame, pose in claves:
        aplicar_pose(arm, pose, frame)


def torso(giro=0.0, inclina=0.0):
    return {
        "Spine": [("Z", giro * 0.3), ("X", inclina * 0.3)],
        "Spine1": [("Z", giro * 0.3), ("X", inclina * 0.3)],
        "Spine2": [("Z", giro * 0.4), ("X", inclina * 0.4)],
    }


def crear_clips(arm):
    # --- Idle (60 f): respiración suave y balanceo del arma
    def idle(resp, sway):
        p = base(**torso(0, 1.5 * resp))
        p["Hips.loc"] = (0, 0, -0.012 * resp)
        p["Head"] = [("X", -1.0 * resp)]
        p["RightHand"] = [("Y", 150 + 3 * sway), ("X", 20)]
        p["RightArm"] = [("Y", -64 + 2 * sway)]
        return p

    clip(arm, "Idle", 60, [(0, idle(0, 0)), (30, idle(1, 1)), (60, idle(0, 0))])

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

    clip(arm, "Run", 20, [
        (0, run(True, -0.02)),
        (5, _paso(run(True, 0.03), run(False, 0.03), 0.5, pierna_sube=True)),
        (10, run(False, -0.02)),
        (15, _paso(run(False, 0.03), run(True, 0.03), 0.5, pierna_sube=True)),
        (20, run(True, -0.02)),
    ])

    # --- Ataques (todos con el brazo derecho; el cuerpo acompaña)
    def ataque(giro, brazo_z, brazo_y, codo, pie, adelante, hombro=0):
        p = base(**torso(giro=giro, inclina=6))
        p["RightArm"] = [("Y", brazo_y), ("Z", brazo_z)]
        p["RightForeArm"] = [("Z", codo)]
        p["RightHand"] = [("Y", 20), ("X", 0)]
        p["LeftArm"] = [("Y", 60), ("X", -giro * 0.6)]
        p["LeftUpLeg"] = [("X", -pie)]
        p["LeftLeg"] = [("X", 6 + pie * 0.8)]
        p["RightUpLeg"] = [("X", 8)]
        p["Hips.loc"] = (0, -adelante, -0.03 * (pie > 5))
        return p

    reposo = base()
    # Ataque 1: tajo horizontal de derecha a izquierda
    clip(arm, "Attack1", 26, [
        (0, reposo),
        (6, ataque(-38, -40, -8, 55, 0, -0.02)),
        (12, ataque(38, 100, -6, 8, 24, 0.10)),
        (18, ataque(48, 122, -6, 10, 22, 0.12)),
        (26, reposo),
    ])
    # Ataque 2: revés de izquierda a derecha
    clip(arm, "Attack2", 24, [
        (0, reposo),
        (5, ataque(42, 118, -6, 10, 18, 0.08)),
        (11, ataque(-42, -38, -6, 12, 24, 0.10)),
        (17, ataque(-50, -52, -6, 16, 20, 0.12)),
        (24, reposo),
    ])
    # Ataque 3: estocada vertical (tajo descendente con salto de paso)
    def vertical(alto, giro, pie, adelante):
        p = base(**torso(giro=giro, inclina=18 if not alto else -6))
        p["RightArm"] = [("Y", 100), ("X", -25)] if alto else [("Y", -35), ("Z", 80)]
        p["RightForeArm"] = [("Z", 20 if alto else 6)]
        p["RightHand"] = [("Y", 20), ("X", 0)]
        p["LeftArm"] = [("Y", 55)]
        p["LeftUpLeg"] = [("X", -pie)]
        p["LeftLeg"] = [("X", 6 + pie * 0.9)]
        p["RightUpLeg"] = [("X", 10)]
        p["Hips.loc"] = (0, -adelante, -0.05 * (pie > 5))
        return p

    clip(arm, "Attack3", 30, [
        (0, reposo),
        (7, vertical(True, 8, 4, 0.0)),
        (13, vertical(False, -6, 34, 0.18)),
        (22, vertical(False, -8, 30, 0.20)),
        (30, reposo),
    ])


def _paso(a, b, t, pierna_sube=False):
    """Pose intermedia (paso medio): interpola ángulos de dos poses de zancada y levanta la pierna libre."""
    p = {}
    claves = set(a) | set(b)
    for k in claves:
        va, vb = a.get(k), b.get(k)
        if k.endswith(".loc") or va is None or vb is None:
            p[k] = va if va is not None else vb
            continue
        ops = []
        for (ea, ga), (eb, gb) in zip(va, vb):
            ops.append((ea, ga + (gb - ga) * t))
        p[k] = ops
    p["Hips.loc"] = (0, 0, 0.035)
    return p


# ----------------------------------------------------------------------------------------------
# Exportación y vista previa
# ----------------------------------------------------------------------------------------------
def exportar(ruta):
    bpy.ops.object.select_all(action="DESELECT")
    for o in bpy.data.objects:
        if o.type in {"ARMATURE", "MESH"}:
            o.select_set(True)
    bpy.ops.export_scene.fbx(
        filepath=ruta,
        use_selection=True,
        object_types={"ARMATURE", "MESH"},
        add_leaf_bones=False,
        bake_anim=True,
        bake_anim_use_all_actions=True,
        bake_anim_use_nla_strips=False,
        bake_anim_simplify_factor=0.0,
        bake_anim_force_startend_keying=True,
        axis_forward="-Z",
        axis_up="Y",
        apply_scale_options="FBX_SCALE_UNITS",
        primary_bone_axis="Y",
        secondary_bone_axis="X",
        mesh_smooth_type="FACE",
    )


def vista_previa(arm, carpeta):
    import os

    os.makedirs(carpeta, exist_ok=True)
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_WORKBENCH"
    sc.display.shading.light = "STUDIO"
    sc.display.shading.color_type = "MATERIAL"
    sc.render.resolution_x, sc.render.resolution_y = 360, 480
    sc.render.film_transparent = False
    sc.world = bpy.data.worlds.new("w")
    sc.world.color = (0.18, 0.2, 0.24)
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    bpy.context.scene.collection.objects.link(cam)
    sc.camera = cam
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = 3.0

    def camara(frontal):
        if frontal:  # 3/4 frontal derecha
            cam.location = (-2.4, -3.6, 1.2)
        else:        # lateral derecha
            cam.location = (-4.5, 0.0, 1.2)
        direccion = Vector((0, 0, 0.95)) - cam.location
        cam.rotation_euler = direccion.to_track_quat("-Z", "Y").to_euler()

    muestras = {
        "Idle": [0, 30],
        "Run": [0, 5, 10, 15],
        "Attack1": [0, 6, 12, 18],
        "Attack2": [0, 5, 11, 17],
        "Attack3": [0, 7, 13, 22],
    }
    for nombre, frames in muestras.items():
        arm.animation_data.action = bpy.data.actions[nombre]
        for f in frames:
            sc.frame_set(f)
            for lado in (True, False):
                camara(lado)
                sc.render.filepath = os.path.join(carpeta, f"{nombre}_{f:02d}_{'3q' if lado else 'side'}.png")
                bpy.ops.render.render(write_still=True)


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
    sc.render.fps = FPS

    arm = crear_armature()
    crear_malla(arm)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="POSE")
    crear_clips(arm)
    bpy.ops.object.mode_set(mode="OBJECT")

    huesos = len(arm.data.bones)
    tris = sum(len(p.vertices) - 2 for o in bpy.data.objects if o.type == "MESH" for p in o.data.polygons)
    print(f"RES huesos={huesos} tris={tris} acciones={sorted(a.name for a in bpy.data.actions)}")
    exportar(salida)
    print(f"RES exportado {salida}")
    if preview:
        vista_previa(arm, preview)
        print(f"RES vista previa en {preview}")


main()
