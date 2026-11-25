"""
Piezas comunes de los generadores de personajes de The Last Beat (build_hero.py, build_bard.py).

Esqueleto Humanoid con nombres mixamorig:* (65 huesos, T-pose de 1.8 m, el personaje mira hacia -Y y su izquierda es +X),
primitivas para mallas provisionales, helpers de animación FK (rotaciones en ejes del mundo en reposo), exportación a FBX
y vista previa headless. Cada script de personaje rellena COLORES y CLIP_FRAMES y define su propia malla y sus clips.
"""
import math
import os

import bmesh
import bpy
from mathutils import Euler, Matrix, Vector

FPS = 30
COLORES = {}      # nombre de material -> (r, g, b, a); lo rellena cada script de personaje
CLIP_FRAMES = {}  # nombre de clip -> duración en frames; lo rellena cada script de personaje
REST_CONTACT = 0.0
ARM_D = {}        # 'Left'/'Right' -> rotación (Matrix 3x3) que lleva el brazo de la T-pose a la A-pose de reposo; vacío = T-pose

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


def definir_huesos(escala=1.0):
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


def crear_armature(escala=1.0, huesos=None):
    """escala multiplica todas las posiciones: 1.0 = 1.8 m; la bardo usa 0.9167 (1.65 m).
    huesos = lista (nombre, padre, cabeza, cola) para esqueletos ajustados a una malla; vacío = el esqueleto base."""
    arm_data = bpy.data.armatures.new("Armature")
    arm = bpy.data.objects.new("Armature", arm_data)
    bpy.context.scene.collection.objects.link(arm)
    bpy.context.view_layer.objects.active = arm
    arm.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    for nombre, padre, cabeza, cola in (huesos if huesos is not None else definir_huesos()):
        cabeza = tuple(c * escala for c in cabeza)
        cola = tuple(c * escala for c in cola)
        eb = arm_data.edit_bones.new(nombre)
        eb.head = Vector(cabeza)
        eb.tail = Vector(cola)
        eb.roll = 0.0
        if padre:
            eb.parent = arm_data.edit_bones[padre]
            eb.use_connect = False
    bpy.ops.object.mode_set(mode="OBJECT")
    return arm

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


FOOT_BONES = ("Foot", "ToeBase", "Toe_End")


def contacto(arm):
    """Altura del punto más bajo de los pies (talón o punta), en el espacio de la armature."""
    bajo = 1e9
    for lado in ("Left", "Right"):
        foot = arm.pose.bones[P + lado + "Foot"].head.z - 0.075
        toe = arm.pose.bones[P + lado + "ToeBase"].head.z
        end = arm.pose.bones[P + lado + "Toe_End"].head.z
        bajo = min(bajo, foot, toe, end)
    return bajo


def fijar_contacto(arm):
    """Guarda la altura de contacto de la pose de reposo; aplicar_pose la usa para que los pies toquen el suelo."""
    global REST_CONTACT
    bpy.context.view_layer.update()
    REST_CONTACT = contacto(arm)


def pies_planos(pose):
    """Mantiene las plantas paralelas al suelo: compensa el cabeceo del muslo y la pantorrilla."""
    for lado in ("Left", "Right"):
        cabeceo = 0.0
        for hueso in ("UpLeg", "Leg"):
            for eje, grados in pose.get(lado + hueso, []):
                if eje == "X":
                    cabeceo += grados
        pose[lado + "Foot"] = [("X", -cabeceo)]
    return pose


def aplicar_pose(arm, pose, frame, suelo=True):
    """
    pose: {hueso_sin_prefijo: ops, 'Hips.loc': (dx, dy, dz)}. Los no listados vuelven a reposo.
    Con suelo=True las caderas suben o bajan para que el pie más bajo toque el suelo (sin flotar ni hundirse).
    """
    for pb in arm.pose.bones:
        corto = pb.name[len(P):]
        rot_reposo = pb.bone.matrix_local.to_3x3()
        spec = pose.get(corto)
        pb.rotation_mode = "QUATERNION"
        # Esqueleto en A-pose: las poses se escriben como si el reposo fuera T-pose. El brazo se compensa con D^-1 y el
        # antebrazo y la mano con D * rot * D^-1, así el resultado final es el mismo que partiendo de T-pose.
        D, es_brazo = None, False
        for lado_arm, d in ARM_D.items():
            if corto in (lado_arm + "Arm", lado_arm + "ForeArm", lado_arm + "Hand"):
                D, es_brazo = d, corto == lado_arm + "Arm"
        if spec is None and D is None:
            pb.rotation_quaternion = (1, 0, 0, 0)
        else:
            rot = combinar(spec or [])
            if D is not None:
                rot = rot @ D.inverted() if es_brazo else D @ rot @ D.inverted()
            pb.rotation_quaternion = (rot_reposo.inverted() @ rot @ rot_reposo).to_quaternion()
        pb.location = (0, 0, 0)

    hips = arm.pose.bones[P + "Hips"]
    rot_hips = hips.bone.matrix_local.to_3x3()
    desp = Vector(pose.get("Hips.loc", (0, 0, 0)))
    hips.location = rot_hips.inverted() @ desp
    if suelo:
        bpy.context.view_layer.update()
        desp.z += REST_CONTACT - contacto(arm)
        hips.location = rot_hips.inverted() @ desp

    # Gancho opcional (pose['_hook']): ajusta huesos a mano (p. ej. IK de brazos) antes de guardar los keyframes.
    hook = pose.get("_hook")
    if hook is not None:
        bpy.context.view_layer.update()
        hook(arm)

    for pb in arm.pose.bones:
        pb.keyframe_insert("rotation_quaternion", frame=frame)
    hips.keyframe_insert("location", frame=frame)

def nueva_accion(arm, nombre, frames):
    accion = bpy.data.actions.new(nombre)
    accion.use_fake_user = True
    if arm.animation_data is None:
        arm.animation_data_create()
    arm.animation_data.action = accion
    accion.use_frame_range = True
    accion.frame_start, accion.frame_end = 0, frames
    return accion


def clip(arm, nombre, claves, suelo=True):
    frames = CLIP_FRAMES[nombre]
    nueva_accion(arm, nombre, frames)
    for frame, pose in claves:
        aplicar_pose(arm, pose, frame, suelo)


def torso(giro=0.0, inclina=0.0):
    """Giro e inclinación repartidos: el pecho gira más que la cintura, así el cuerpo 'late' tras las caderas."""
    return {
        "Spine": [("Z", giro * 0.2), ("X", inclina * 0.25)],
        "Spine1": [("Z", giro * 0.3), ("X", inclina * 0.3)],
        "Spine2": [("Z", giro * 0.5), ("X", inclina * 0.45)],
        "Neck": [("Z", -giro * 0.15)],
        "Head": [("Z", -giro * 0.2), ("X", -inclina * 0.3)],
    }


def piernas(p, lado, paso, yaw):
    """Apoyo: la pierna 'lado' ('L' o 'R') avanza con el paso (0 a 1); la otra queda atrás. Pies planos."""
    adel, atras = ("Left", "Right") if lado == "L" else ("Right", "Left")
    p[adel + "UpLeg"] = [("X", -4 - 36 * paso), ("Z", -yaw * 0.9)]
    p[adel + "Leg"] = [("X", 6 + 42 * paso)]
    p[atras + "UpLeg"] = [("X", 4 + 14 * paso), ("Z", -yaw * 0.9)]
    p[atras + "Leg"] = [("X", 6 + 14 * paso)]
    return p
def _paso(a, b, t):
    """Pose intermedia (paso medio): interpola ángulos de dos poses de zancada y levanta la pierna libre."""
    p = {}
    claves = set(a) | set(b)
    for k in claves:
        if k.startswith("_"):  # ganchos (_hook): no se interpolan
            continue
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


def vista_previa(arm, carpeta, muestras, objetivo_z=0.95, escala=3.0):
    """Renderiza poses clave a PNG (Workbench). muestras = {clip: [frames]}; dos cámaras: 3/4 frontal y lateral."""
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
    cam.data.ortho_scale = escala

    def camara(frontal):
        if frontal:  # 3/4 frontal derecha
            cam.location = (-2.4, -3.6, 1.2)
        else:        # lateral derecha
            cam.location = (-4.5, 0.0, 1.2)
        direccion = Vector((0, 0, objetivo_z)) - cam.location
        cam.rotation_euler = direccion.to_track_quat("-Z", "Y").to_euler()

    for nombre, frames in muestras.items():
        arm.animation_data.action = bpy.data.actions[nombre]
        for f in frames:
            sc.frame_set(f)
            for lado in (True, False):
                camara(lado)
                sc.render.filepath = os.path.join(carpeta, f"{nombre}_{f:02d}_{'3q' if lado else 'side'}.png")
                bpy.ops.render.render(write_still=True)
