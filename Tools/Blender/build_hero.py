"""
El caballero de The Last Beat: esqueleto Humanoid (nombres mixamorig:*), malla low-poly con pesos rígidos
y los colores del concept art (Art/ConceptArt/Caballero.png y Caballero_Espadon.png): armadura azul con
ribetes plateados, traje azul oscuro, pelo negro, capa y espadón azul y plata.
Clips: Idle, Run, Spin (bucle; Unity gira el modelo), Charge (bucle; Unity desplaza al héroe), Light1, Light2,
Heavy1, Heavy2, Hit (recibir golpe) y Die (morir). Exporta un FBX listo para Unity.

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
HIT_FRAMES = {"Light1": 9, "Light2": 9, "Heavy1": 16, "Heavy2": 17}
CLIP_FRAMES = {"Idle": 60, "Run": 20, "Spin": 20, "Charge": 16, "Light1": 22, "Light2": 22, "Heavy1": 34, "Heavy2": 38,
               "Hit": 12, "Die": 40}

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
# Malla (piezas rígidas pesadas al 100 % a su hueso)
# ----------------------------------------------------------------------------------------------
def hex_lineal(h):
    """'#2e47ae' -> color lineal RGBA (Blender guarda los colores en espacio lineal)."""
    def canal(c):
        c /= 255.0
        return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4
    return (canal(int(h[1:3], 16)), canal(int(h[3:5], 16)), canal(int(h[5:7], 16)), 1.0)


# Colores medidos en el concept art del caballero.
COLORES = {
    "HeroArmor": hex_lineal("#2e47ae"),   # armadura azul
    "HeroTrim": hex_lineal("#cfcbd2"),    # ribetes, hebilla y filos plateados
    "HeroSuit": hex_lineal("#23253a"),    # traje y guantes azul oscuro
    "HeroSkin": hex_lineal("#f0caa8"),
    "HeroHair": hex_lineal("#16171f"),    # pelo y ojos
    "HeroCape": hex_lineal("#1d2c78"),    # capa, un tono más oscuro que la armadura
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


def cono(bm, base, punta, r, lados=5):
    """Punta (mechones de pelo, filo del espadón): un prisma que se cierra casi en un punto."""
    prisma(bm, base, punta, r, 0.002, lados)


def placa(bm, a, b, c, d, grosor):
    """Lámina con grosor entre cuatro esquinas: a-b arriba, c-d abajo (capa, faldones)."""
    a, b, c, d = Vector(a), Vector(b), Vector(c), Vector(d)
    n = (b - a).cross(c - a).normalized() * (grosor / 2)
    delante = [bm.verts.new(p - n) for p in (a, b, d, c)]
    detras = [bm.verts.new(p + n) for p in (a, b, d, c)]
    bm.faces.new(delante)
    bm.faces.new(detras[::-1])
    for i in range(4):
        j = (i + 1) % 4
        bm.faces.new((delante[i], detras[i], detras[j], delante[j]))


def con_ribete(a, b, c, d, grosor, borde, mat_pieza, mat_ribete, add, hueso):
    """Placa de armadura con un borde plateado: una placa un poco mayor detrás asoma como ribete."""
    a, b, c, d = Vector(a), Vector(b), Vector(c), Vector(d)
    centro = (a + b + c + d) / 4
    n = (b - a).cross(c - a).normalized()
    if n.y < 0:  # el ribete va siempre por detrás (hacia el cuerpo), también en el lado espejado
        n = -n
    crecer = lambda p: centro + (p - centro) * (1 + borde) + n * grosor * 0.6
    add(hueso, mat_ribete, lambda bm: placa(bm, crecer(a), crecer(b), crecer(c), crecer(d), grosor))
    add(hueso, mat_pieza, lambda bm: placa(bm, a, b, c, d, grosor))


def piezas():
    """(hueso, material, función(bm) que dibuja la pieza) del centro del cuerpo."""
    L = []

    def add(hueso, mat, fn):
        L.append((hueso, mat, fn))

    # Cadera, cinturón y faldón frontal en punta (con ribete plateado)
    add("Hips", "HeroSuit", lambda bm: prisma(bm, (0, 0, 0.90), (0, 0, 1.07), (0.165, 0.11), (0.16, 0.10)))
    add("Hips", "HeroSuit", lambda bm: prisma(bm, (0, 0, 1.02), (0, 0, 1.08), (0.172, 0.116), (0.172, 0.116)))
    add("Hips", "HeroTrim", lambda bm: caja(bm, (0, -0.12, 1.05), (0.08, 0.02, 0.055)))   # hebilla
    con_ribete((-0.10, -0.118, 1.02), (0.10, -0.118, 1.02), (-0.035, -0.135, 0.74), (0.035, -0.135, 0.74),
               0.016, 0.12, "HeroArmor", "HeroTrim", add, "Hips")
    # Torso: abdomen y pecho azules, línea plateada bajo el pecho y la estrella del pecho
    add("Spine1", "HeroArmor", lambda bm: prisma(bm, (0, 0, 1.07), (0, 0, 1.25), (0.15, 0.10), (0.155, 0.10)))
    add("Spine2", "HeroTrim", lambda bm: prisma(bm, (0, 0, 1.24), (0, 0, 1.265), (0.19, 0.125), (0.19, 0.125)))
    add("Spine2", "HeroArmor", lambda bm: prisma(bm, (0, 0, 1.265), (0, 0, 1.47), (0.19, 0.122), (0.205, 0.125)))
    add("Spine2", "HeroTrim", lambda bm: caja(bm, (0, -0.128, 1.36), (0.022, 0.012, 0.13)))
    add("Spine2", "HeroTrim", lambda bm: caja(bm, (0, -0.128, 1.38), (0.08, 0.012, 0.022)))
    # Cuello alto de la armadura
    add("Spine2", "HeroArmor", lambda bm: prisma(bm, (0, 0, 1.46), (0, 0, 1.56), (0.085, 0.078), (0.075, 0.07)))
    add("Spine2", "HeroTrim", lambda bm: prisma(bm, (0, 0, 1.555), (0, 0, 1.57), (0.077, 0.072), (0.077, 0.072)))
    add("Neck", "HeroSkin", lambda bm: prisma(bm, (0, 0, 1.49), (0, 0, 1.61), 0.048, 0.048))
    # Cabeza: cara, nariz (marca el frente), ojos y pelo negro en punta
    add("Head", "HeroSkin", lambda bm: esfera(bm, (0, 0, 1.69), (0.095, 0.105, 0.115)))
    add("Head", "HeroSkin", lambda bm: caja(bm, (0, -0.105, 1.68), (0.022, 0.03, 0.035)))
    for sx in (-1, 1):
        add("Head", "HeroHair", lambda bm, sx=sx: caja(bm, (0.036 * sx, -0.097, 1.705), (0.03, 0.012, 0.022)))
    add("Head", "HeroHair", lambda bm: esfera(bm, (0, 0.03, 1.74), (0.106, 0.106, 0.10)))
    mechones = [  # (base, punta, radio)
        ((0, 0.05, 1.80), (0, 0.15, 1.88), 0.045), ((0.06, 0.03, 1.80), (0.13, 0.08, 1.87), 0.04),
        ((-0.06, 0.03, 1.80), (-0.13, 0.08, 1.87), 0.04), ((0, -0.02, 1.82), (0.01, -0.03, 1.91), 0.04),
        ((0.08, 0.0, 1.76), (0.15, 0.02, 1.74), 0.035), ((-0.08, 0.0, 1.76), (-0.15, 0.02, 1.74), 0.035),
        ((0.04, -0.06, 1.79), (0.06, -0.115, 1.72), 0.03), ((-0.04, -0.06, 1.79), (-0.05, -0.115, 1.72), 0.03),
        ((0, -0.07, 1.80), (0.0, -0.12, 1.745), 0.028), ((0, 0.09, 1.72), (0, 0.15, 1.64), 0.045),
    ]
    for base, punta, r in mechones:
        add("Head", "HeroHair", lambda bm, b=base, p=punta, r=r: cono(bm, b, p, r))
    # Capa: de los hombros a la cintura con el pecho, y de la cintura a las corvas con la cadera
    add("Spine2", "HeroCape", lambda bm: placa(bm, (-0.17, 0.128, 1.47), (0.17, 0.128, 1.47),
                                                (-0.21, 0.17, 1.06), (0.21, 0.17, 1.06), 0.02))
    add("Hips", "HeroCape", lambda bm: placa(bm, (-0.21, 0.17, 1.065), (0.21, 0.17, 1.065),
                                              (-0.25, 0.30, 0.50), (0.25, 0.30, 0.50), 0.02))
    return L


def piezas_lado(lado, s):
    L = []

    def x(v):
        return (v[0] * s, v[1], v[2])

    def add(hueso, mat, fn):
        L.append((P + lado + hueso, mat, fn))

    # Hombrera redonda con borde plateado
    add("Shoulder", "HeroArmor", lambda bm: esfera(bm, x((0.205, 0, 1.50)), (0.095, 0.10, 0.075)))
    add("Shoulder", "HeroTrim", lambda bm: caja(bm, x((0.22, 0, 1.452)), (0.14, 0.17, 0.022)))
    add("Shoulder", "HeroTrim", lambda bm: esfera(bm, x((0.20, -0.085, 1.525)), (0.022, 0.018, 0.022)))
    # Brazo: traje oscuro, brazal azul entre dos aros plateados, guante oscuro y dedos al aire
    add("Arm", "HeroSuit", lambda bm: prisma(bm, x((0.17, 0, 1.46)), x((0.46, 0, 1.46)), 0.05, 0.042))
    add("ForeArm", "HeroTrim", lambda bm: prisma(bm, x((0.465, 0, 1.46)), x((0.49, 0, 1.46)), 0.054, 0.054))
    add("ForeArm", "HeroArmor", lambda bm: prisma(bm, x((0.49, 0, 1.46)), x((0.69, 0, 1.46)), 0.052, 0.046))
    add("ForeArm", "HeroTrim", lambda bm: prisma(bm, x((0.69, 0, 1.46)), x((0.715, 0, 1.46)), 0.05, 0.05))
    add("Hand", "HeroSuit", lambda bm: caja(bm, x((0.765, 0, 1.46)), (0.08, 0.045, 0.09)))
    add("Hand", "HeroSkin", lambda bm: caja(bm, x((0.82, 0, 1.46)), (0.035, 0.035, 0.08)))
    # Faldón lateral con ribete
    con_ribete(x((0.105, -0.10, 1.00)), x((0.175, -0.06, 1.00)), x((0.115, -0.125, 0.82)), x((0.195, -0.08, 0.82)),
               0.014, 0.14, "HeroArmor", "HeroTrim", lambda h, m, f: L.append((P + h, m, f)), "Hips")
    # Pierna: muslo oscuro, rodillera, greba azul con aro plateado y bota con puntera plateada
    add("UpLeg", "HeroSuit", lambda bm: prisma(bm, x((0.09, 0, 0.98)), x((0.09, 0, 0.56)), 0.085, 0.065))
    add("Leg", "HeroArmor", lambda bm: caja(bm, x((0.09, -0.058, 0.55)), (0.10, 0.04, 0.11)))
    add("Leg", "HeroTrim", lambda bm: caja(bm, x((0.09, -0.082, 0.55)), (0.04, 0.018, 0.065)))
    add("Leg", "HeroArmor", lambda bm: prisma(bm, x((0.09, 0, 0.54)), x((0.09, 0, 0.12)), 0.065, 0.05))
    add("Leg", "HeroTrim", lambda bm: prisma(bm, x((0.09, 0, 0.13)), x((0.09, 0, 0.16)), 0.058, 0.058))
    add("Foot", "HeroArmor", lambda bm: caja(bm, x((0.09, -0.07, 0.045)), (0.095, 0.24, 0.09)))
    add("Foot", "HeroTrim", lambda bm: caja(bm, x((0.09, -0.165, 0.04)), (0.10, 0.06, 0.08)))
    return L


def espadon():
    """Empuñadura en la mano derecha; hoja en la prolongación del antebrazo (-X en T-pose).
    Como en el concept art: hoja azul con filos plateados, guarda plateada con gema azul y pomo plateado."""
    L = []
    mano = P + "RightHand"

    def add(mat, fn):
        L.append((mano, mat, fn))

    add("HeroSuit", lambda bm: prisma(bm, (-0.76, 0, 1.46), (-0.89, 0, 1.46), 0.021, 0.021))       # empuñadura
    add("HeroTrim", lambda bm: esfera(bm, (-0.745, 0, 1.46), (0.028, 0.028, 0.028)))                # pomo
    add("HeroTrim", lambda bm: caja(bm, (-0.90, 0, 1.46), (0.035, 0.22, 0.04)))                    # guarda
    add("HeroArmor", lambda bm: caja(bm, (-0.905, 0, 1.46), (0.045, 0.05, 0.05)))                  # gema
    add("HeroArmor", lambda bm: caja(bm, (-1.38, 0, 1.46), (0.92, 0.085, 0.018)))                  # hoja
    for sy in (-1, 1):
        add("HeroTrim", lambda bm, sy=sy: caja(bm, (-1.38, 0.0525 * sy, 1.46), (0.92, 0.02, 0.012)))  # filos
    add("HeroTrim", lambda bm: prisma(bm, (-1.84, 0, 1.46), (-1.98, 0, 1.46), (0.009, 0.0625), 0.002, 4))  # punta
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


REST_CONTACT = 0.0
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
        if spec is None:
            pb.rotation_quaternion = (1, 0, 0, 0)
        else:
            rot = combinar(spec)
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

    for pb in arm.pose.bones:
        pb.keyframe_insert("rotation_quaternion", frame=frame)
    hips.keyframe_insert("location", frame=frame)


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
    global REST_CONTACT
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

    # --- Hit (12 f): retroceso del torso al recibir un golpe. En Unity va en una capa que solo mueve el torso,
    # así no corta el ataque en curso (el evento OnHit del ataque sigue llegando).
    def retroceso(fuerza):
        p = golpe(giro=-10 * fuerza, inclina=-16 * fuerza, mano=guardia,
                  izq=[("Y", 60 - 25 * fuerza), ("X", -25 * fuerza)])
        p["Head"] = [("X", 14 * fuerza), ("Z", 6 * fuerza)]
        return p

    clip(arm, "Hit", [(0, reposo), (3, retroceso(1.0)), (7, retroceso(0.45)), (12, reposo)])

    # --- Die (40 f, sin bucle): se tambalea, cae de rodillas y se desploma boca abajo. Las caderas bajan a mano
    # (suelo=False) para que el cuerpo quede tendido sobre el suelo.
    def caida(muslo, pierna, pie, tronco, frente, baja, avance, brazos, codo=1.0):
        """muslo y pierna son relativos a la cadera; frente inclina todo el cuerpo hacia delante.
        Las alturas salen de la geometría: muslo y espinilla miden 0,44 m. pie estira el pie hacia atrás
        (empeine en el suelo), como al arrodillarse; con los pies planos la punta se clavaría en el suelo."""
        p = {
            "Hips": [("X", frente)],
            "LeftUpLeg": [("X", muslo)], "RightUpLeg": [("X", muslo * 0.9)],
            "LeftLeg": [("X", pierna)], "RightLeg": [("X", pierna * 0.95)],
            "LeftArm": [("Y", brazos)], "RightArm": [("Y", -brazos * 0.5)],
            "LeftForeArm": [("Z", -20 * codo)], "RightForeArm": [("Z", 25 * codo)],
            # mientras cae, el espadón sigue en alto (como en la guardia) para no atravesar el suelo
            "RightHand": [("Y", 150 * codo), ("X", 20 * codo)],
        }
        p.update(torso(giro=4, inclina=tronco))
        p["Head"] = [("X", tronco * 0.4), ("Z", 25 if frente > 60 else 0)]
        p["Hips.loc"] = (0, -avance, -baja)
        p["LeftFoot"] = [("X", pie)]
        p["RightFoot"] = [("X", pie)]
        return p

    tendido = caida(muslo=0, pierna=4, pie=88, tronco=0, frente=88, baja=0.79, avance=0.55, brazos=15, codo=0)

    nueva_accion(arm, "Die", CLIP_FRAMES["Die"])
    aplicar_pose(arm, reposo, 0, suelo=True)
    aplicar_pose(arm, retroceso(1.2), 5, suelo=True)
    # agachado sobre la punta de los pies: la rodilla baja sin que la punta del pie barra el suelo
    aplicar_pose(arm, caida(muslo=-40, pierna=80, pie=50, tronco=15, frente=0, baja=0.11, avance=0.0, brazos=70), 10, suelo=False)
    # de rodillas: muslo vertical, espinilla en el suelo, cadera a 0,52 m
    aplicar_pose(arm, caida(muslo=0, pierna=88, pie=92, tronco=22, frente=0, baja=0.46, avance=0.0, brazos=78), 16, suelo=False)
    # cayendo: muslo a 25°, rodilla a 8 cm del suelo
    aplicar_pose(arm, caida(muslo=-30, pierna=70, pie=85, tronco=10, frente=55, baja=0.50, avance=0.2, brazos=60), 26, suelo=False)
    aplicar_pose(arm, tendido, 33, suelo=False)
    aplicar_pose(arm, tendido, 40, suelo=False)


def _paso(a, b, t):
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
        "Spin": [0, 5, 15],
        "Charge": [0, 4, 8],
        "Light1": [0, 6, 9, 13],
        "Light2": [0, 6, 9, 13],
        "Heavy1": [0, 12, 16, 20],
        "Heavy2": [0, 6, 17, 22],
        "Hit": [3],
        "Die": [16, 26, 40],
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
    global REST_CONTACT
    bpy.context.view_layer.update()
    REST_CONTACT = contacto(arm)
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


if __name__ == "__main__":  # así los scripts de la bardo y el soldado pueden reutilizar las piezas y las poses
    main()
