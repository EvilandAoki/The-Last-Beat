"""
Bardo de The Last Beat a partir del modelo de Tripo3D (tareas 23, 26 y 28): limpia y optimiza la malla, le ajusta el esqueleto
Humanoid (mixamorig:*) a sus proporciones, calcula los pesos, añade el laúd y los clips Idle, Run, Sing, Hit y Die
(los mismos de build_bard.py) y exporta un FBX listo para Unity.

Uso:
  snap run blender -b -P Tools/Blender/build_bard_tripo.py -- <carpeta_con_el_fbx_de_tripo> <salida.fbx>
       [--textura <salida.jpg>] [--guitarra <carpeta_con_el_fbx_de_la_guitarra>] [--textura-guitarra <salida.jpg>]
       [--preview <carpeta>] [--blend <archivo.blend>] [--rig-preview <carpeta>]

La malla de Tripo mide 1 m, está en A-pose (brazos a unos 63° bajo la horizontal) y mira hacia -Y. Se escala a 1.65 m.
El esqueleto se escribe para esa malla (huesos_t) y las poses se escriben como si el reposo fuera T-pose, con la compensación de
A-pose de rig_common (ARM_D).

La guitarra (laúd) cuelga de una correa: va rígida al torso (Spine2), en posición de tocar, y las manos la agarran con IK de dos
huesos (mano izquierda en el mástil, derecha rasgueando sobre la boca). Así la sostiene como una guitarra y no como una espada,
y sigue el cuerpo al caminar, correr o balancearse. Sin --guitarra se usa el laúd procedural de build_bard.py (en la mano).
"""
import glob
import math
import os
import shutil
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bmesh
import bpy
from mathutils import Matrix, Vector

import build_bard as bb
import rig_common as rc
from rig_common import P

ALTURA = 1.65         # metros, tras escalar la malla
S = ALTURA / 1.0      # la malla de Tripo mide 1.0
ANGULO_BRAZO = 63.0   # grados bajo la horizontal en la A-pose del modelo
LAUD_ESCALA = 0.85    # laúd procedural (sin --guitarra): se encoge un poco, los brazos de este modelo son cortos

# Guitarra de Tripo (1.0 de largo, cabeza en +Z, cara hacia -Y). Colgada del torso en posición de tocar.
GUITARRA_LARGO = 0.80                         # metros
GUITARRA_CENTRO = Vector((-0.02, -0.20, 0.86))  # dónde queda el centro de la boca (fracción 0.28 del largo), en metros
GUITARRA_BOCA = 0.28                          # fracción del largo donde está el centro de la boca
GUITARRA_ANGULO = 50.0                        # grados: el mástil apunta arriba y a la izquierda del personaje
GUITARRA_TRIS = 4800
MASTIL_MANO = 0.72                            # fracción del largo donde agarra la mano izquierda
RASGUEO_ZONA = 0.33                           # fracción del largo donde rasguea la derecha
POLO_BRAZO = {"Left": Vector((0.5, 0.2, -1.0)), "Right": Vector((-0.5, 0.2, -1.0))}  # hacia dónde apuntan los codos
MAX_TRIS = 20000
DECIMAR = 0.7         # proporción de triángulos que se conserva (1.0 = no decimar)

# Medidas del esqueleto en unidades de la malla de Tripo (1.0 = altura total). Lado izquierdo = +X.
# La malla de Tripo no está centrada en y = 0: el torso va de y = -0.10 a +0.02, así que los huesos se adelantan para quedar dentro.
CENTRO_T = {
    "Hips": ((0, -0.04, 0.53), (0, -0.04, 0.57)),
    "Spine": ((0, -0.04, 0.57), (0, -0.04, 0.60)),
    "Spine1": ((0, -0.04, 0.60), (0, -0.04, 0.645)),
    "Spine2": ((0, -0.04, 0.645), (0, -0.04, 0.695)),
    "Neck": ((0, -0.04, 0.695), (0, -0.03, 0.745)),
    "Head": ((0, -0.03, 0.745), (0, -0.01, 0.93)),
    "HeadTop_End": ((0, -0.01, 0.93), (0, -0.01, 0.97)),
}
PIERNA_T = {
    "UpLeg": ((0.070, -0.025, 0.530), (0.085, -0.025, 0.285)),
    "Leg": ((0.085, -0.025, 0.285), (0.095, -0.02, 0.085)),
    "Foot": ((0.095, -0.02, 0.085), (0.100, -0.095, 0.030)),
    "ToeBase": ((0.100, -0.095, 0.030), (0.100, -0.135, 0.020)),
    "Toe_End": ((0.100, -0.135, 0.020), (0.100, -0.160, 0.020)),
}
HOMBRO_T = ((0.02, -0.04, 0.665), (0.095, -0.04, 0.665))
LARGOS_BRAZO_T = {"Arm": 0.120, "ForeArm": 0.115, "Hand": 0.050}
FACTOR_DEDOS = 0.5    # los dedos del esqueleto base son de 1.8 m; aquí se reducen a la mitad


def huesos_t():
    """Lista (nombre, padre, cabeza, cola) en metros, a la altura final, con los brazos en A-pose."""
    a = math.radians(ANGULO_BRAZO)
    u = Vector((math.cos(a), 0.0, -math.sin(a)))
    pos = {}
    for n, (h, t) in CENTRO_T.items():
        pos[P + n] = (Vector(h), Vector(t))
    for lado, signo in (("Left", 1.0), ("Right", -1.0)):
        m = lambda v: Vector((v[0] * signo, v[1], v[2]))
        for n, (h, t) in PIERNA_T.items():
            pos[P + lado + n] = (m(h), m(t))
        pos[P + lado + "Shoulder"] = (m(HOMBRO_T[0]), m(HOMBRO_T[1]))
        cursor = Vector(HOMBRO_T[1])
        for n in ("Arm", "ForeArm", "Hand"):
            fin = cursor + u * LARGOS_BRAZO_T[n]
            pos[P + lado + n] = (m(cursor), m(fin))
            cursor = fin
        mano_fin = cursor
        for dedo, (dy, largos) in rc.DEDOS.items():
            d = mano_fin + Vector((0, dy * FACTOR_DEDOS, 0))
            for i, largo in enumerate(largos, start=1):
                fin = d + u * (largo * FACTOR_DEDOS)
                pos[P + lado + "Hand" + dedo + str(i)] = (m(d), m(fin))
                d = fin
    huesos = []
    for nombre, padre, _, _ in rc.definir_huesos():
        h, t = pos[nombre]
        huesos.append((nombre, padre, tuple(h * S), tuple(t * S)))
    return huesos


# ----------------------------------------------------------------------------------------------
# Malla de Tripo: importar, escalar, limpiar, decimar
# ----------------------------------------------------------------------------------------------
def importar_tripo(carpeta):
    ruta = glob.glob(os.path.join(carpeta, "*.fbx"))
    if not ruta:
        raise SystemExit(f"No hay ningún .fbx en {carpeta}")
    bpy.ops.import_scene.fbx(filepath=ruta[0])
    mallas = [o for o in bpy.data.objects if o.type == "MESH"]
    if len(mallas) != 1:
        raise RuntimeError(f"Se esperaba una sola malla y hay {len(mallas)}")
    obj = mallas[0]
    obj.name = "Bard_Body"
    obj.data.name = "Bard_Body"
    obj.parent = None
    obj.matrix_world = Matrix.Identity(4)
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    obj.scale = (S, S, S)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return obj


def tris_de(obj):
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)


def limpiar(obj):
    """Une vértices repetidos, borra piezas sueltas diminutas y recalcula normales. Devuelve un resumen."""
    antes_v, antes_t = len(obj.data.vertices), tris_de(obj)
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=0.0005)

    # Islas: se conserva la mayor y las que sean grandes (la coleta o los audífonos no deben quedar fuera).
    visitados, islas = set(), []
    for v in bm.verts:
        if v in visitados:
            continue
        pila, isla = [v], []
        visitados.add(v)
        while pila:
            c = pila.pop()
            isla.append(c)
            for e in c.link_edges:
                o = e.other_vert(c)
                if o not in visitados:
                    visitados.add(o)
                    pila.append(o)
        islas.append(isla)
    islas.sort(key=len, reverse=True)
    minimo = max(30, int(len(islas[0]) * 0.002))
    borrar = [v for isla in islas if len(isla) < minimo for v in isla]
    if borrar:
        bmesh.ops.delete(bm, geom=borrar, context="VERTS")
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()
    return {
        "vertices": (antes_v, len(obj.data.vertices)),
        "tris": (antes_t, tris_de(obj)),
        "islas": len(islas),
        "islas_borradas": sum(1 for i in islas if len(i) < minimo),
    }


def decimar(obj, proporcion):
    if proporcion >= 1.0:
        return
    mod = obj.modifiers.new("Decimate", "DECIMATE")
    mod.decimate_type = "COLLAPSE"
    mod.ratio = proporcion
    mod.delimit = {"UV", "SHARP"}
    mod.use_collapse_triangulate = True
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier=mod.name)


# ----------------------------------------------------------------------------------------------
# Pesos
# ----------------------------------------------------------------------------------------------
def pesar(obj, arm):
    """Pesos automáticos con los dedos fuera del cálculo (la malla de Tripo tiene manos de una pieza)."""
    dedos = [b for b in arm.data.bones
             if any(b.name.startswith(P + lado + "Hand") and b.name != P + lado + "Hand" for lado in ("Left", "Right"))]
    for b in dedos:
        b.use_deform = False
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    arm.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.parent_set(type="ARMATURE_AUTO")
    for b in dedos:
        b.use_deform = True

    sin_peso = [v.index for v in obj.data.vertices if not any(g.weight > 0.0 for g in v.groups)]
    if sin_peso:
        # Respaldo: cada vértice sin peso toma el hueso (con deform) más cercano.
        huesos = [b for b in arm.data.bones if b.use_deform and b not in dedos]
        for i in sin_peso:
            v = obj.data.vertices[i]
            mejor = min(huesos, key=lambda b: _dist_segmento(v.co, b.head_local, b.tail_local))
            obj.vertex_groups[mejor.name].add([i], 1.0, "REPLACE")
    return len(sin_peso)


def _dist_segmento(p, a, b):
    ab = b - a
    t = max(0.0, min(1.0, (p - a).dot(ab) / max(ab.length_squared, 1e-9)))
    return (p - (a + ab * t)).length


def limitar_influencias(obj, maximo=4):
    """Unity usa 4 influencias por vértice como máximo; se normaliza el resto."""
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.mode_set(mode="OBJECT")
    bpy.ops.object.vertex_group_limit_total(group_select_mode="ALL", limit=maximo)
    bpy.ops.object.vertex_group_normalize_all(group_select_mode="ALL", lock_active=False)


# ----------------------------------------------------------------------------------------------
# Laúd: el de build_bard.py, colocado en la mano izquierda de este esqueleto
# ----------------------------------------------------------------------------------------------
def crear_laud(arm):
    bm = bmesh.new()
    mesh = bpy.data.meshes.new("Bard_Laud")
    obj = bpy.data.objects.new("Bard_Laud", mesh)
    bpy.context.scene.collection.objects.link(obj)
    piezas = bb.laud()
    nombres = []
    for _, mat, _ in piezas:
        if mat not in nombres:
            nombres.append(mat)
    for n in nombres:
        rc.material(n)
        mesh.materials.append(bpy.data.materials[n])
    grupo = obj.vertex_groups.new(name=P + "LeftHand")
    dvert = bm.verts.layers.deform.verify()

    # Las piezas están escritas para la mano del esqueleto base (T-pose, hero*K). Se llevan al frame del brazo y se rotan a la A-pose.
    cabeza_arm = Vector(arm.data.bones[P + "LeftArm"].head_local)
    mano = Vector(arm.data.bones[P + "LeftHand"].head_local)
    largo_mano_t = (mano - cabeza_arm).length
    largo_mano_base = (0.77 - 0.17) * bb.K
    a = math.radians(ANGULO_BRAZO)
    D = Matrix.Rotation(a, 3, "Y")
    pivote_base = Vector((0.17 * bb.K, 0, 1.46 * bb.K))
    for _, mat, fn in piezas:
        antes_v, antes_f = set(bm.verts), set(bm.faces)
        fn(bm)
        for v in bm.verts:
            if v in antes_v:
                continue
            u = (v.co - pivote_base) * LAUD_ESCALA
            u.x -= largo_mano_base * LAUD_ESCALA - largo_mano_t   # el agarre queda en la mano de este modelo
            v.co = cabeza_arm + D @ u
            v[dvert][grupo.index] = 1.0
        for f in bm.faces:
            if f not in antes_f:
                f.material_index = nombres.index(mat)
                f.smooth = False
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bm.to_mesh(mesh)
    bm.free()
    obj.parent = arm
    mod = obj.modifiers.new("Armature", "ARMATURE")
    mod.object = arm
    return obj



# ----------------------------------------------------------------------------------------------
# Guitarra de Tripo + correa, rígidas al torso
# ----------------------------------------------------------------------------------------------
def rot_guitarra():
    return Matrix.Rotation(math.radians(GUITARRA_ANGULO), 3, "Y")


def punto_guitarra(frac_z, y=0.0):
    """Punto de la guitarra en espacio de reposo (metros): frac_z = fracción del largo desde la punta de abajo."""
    return GUITARRA_CENTRO + rot_guitarra() @ Vector((0.0, y, (frac_z - GUITARRA_BOCA) * GUITARRA_LARGO))


def importar_guitarra(carpeta, arm):
    antes = set(bpy.data.objects)
    ruta = glob.glob(os.path.join(carpeta, "*.fbx"))
    if not ruta:
        raise SystemExit(f"No hay ningún .fbx en {carpeta}")
    bpy.ops.import_scene.fbx(filepath=ruta[0])
    nuevos = [o for o in bpy.data.objects if o not in antes and o.type == "MESH"]
    if len(nuevos) != 1:
        raise RuntimeError(f"Se esperaba una sola malla de guitarra y hay {len(nuevos)}")
    obj = nuevos[0]
    for o in set(bpy.data.objects) - antes - {obj}:
        bpy.data.objects.remove(o)
    obj.name = obj.data.name = "Bard_Laud"
    obj.parent = None
    obj.matrix_world = Matrix.Identity(4)

    # Unir vértices, decimar y llevar la malla a su sitio sobre el torso.
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=0.0003)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bm.to_mesh(obj.data)
    bm.free()
    if len(obj.data.polygons) * 1 > GUITARRA_TRIS:
        bpy.context.view_layer.objects.active = obj
        mod = obj.modifiers.new("Decimate", "DECIMATE")
        mod.ratio = min(1.0, GUITARRA_TRIS / max(1, tris_de(obj)))
        mod.delimit = {"UV", "SHARP"}
        bpy.ops.object.modifier_apply(modifier=mod.name)

    R = rot_guitarra()
    ancla = Vector((0.0, 0.0, GUITARRA_BOCA))
    for v in obj.data.vertices:
        v.co = GUITARRA_CENTRO + R @ ((v.co - ancla) * GUITARRA_LARGO)
    obj.data.update()

    mat = obj.data.materials[0]
    mat.name = "BardLaud"
    grupo = obj.vertex_groups.new(name=P + "Spine2")
    grupo.add([v.index for v in obj.data.vertices], 1.0, "REPLACE")
    obj.parent = arm
    obj.modifiers.new("Armature", "ARMATURE").object = arm
    return obj, mat


def crear_correa(arm):
    """Correa marrón: del talón del mástil sobre el hombro izquierdo, baja por la espalda y vuelve a la punta de la guitarra."""
    bm = bmesh.new()
    mesh = bpy.data.meshes.new("Bard_Strap")
    obj = bpy.data.objects.new("Bard_Strap", mesh)
    bpy.context.scene.collection.objects.link(obj)
    rc.material("BardBrown")
    mesh.materials.append(bpy.data.materials["BardBrown"])
    talon = punto_guitarra(0.58, -0.02)
    punta = punto_guitarra(0.03, -0.02)
    ruta = [talon, Vector((0.13, -0.15, 1.10)), Vector((0.12, -0.07, 1.165)), Vector((0.10, 0.045, 1.12)),
            Vector((-0.02, 0.05, 0.95)), Vector((-0.12, 0.045, 0.84)), Vector((-0.20, -0.04, 0.78)), punta]
    for a, b in zip(ruta, ruta[1:]):
        rc.prisma(bm, a, b, (0.014, 0.004), (0.014, 0.004), 4)
    dvert = bm.verts.layers.deform.verify()
    grupo = obj.vertex_groups.new(name=P + "Spine2")
    for v in bm.verts:
        v[dvert][grupo.index] = 1.0
    for f in bm.faces:
        f.smooth = False
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bm.to_mesh(mesh)
    bm.free()
    obj.parent = arm
    obj.modifiers.new("Armature", "ARMATURE").object = arm
    return obj


# ----------------------------------------------------------------------------------------------
# IK de dos huesos para los brazos
# ----------------------------------------------------------------------------------------------
def _neutral(pb):
    """Orientación de reposo del hueso llevada por la pose actual de su padre (3x3, espacio de la armature)."""
    if pb.parent:
        return pb.parent.matrix.to_3x3() @ pb.parent.bone.matrix_local.to_3x3().inverted() @ pb.bone.matrix_local.to_3x3()
    return pb.bone.matrix_local.to_3x3()


def _orientar(pb, direccion):
    """Gira el hueso (rotación mínima desde su orientación neutra) para que su eje Y apunte hacia 'direccion'."""
    n = _neutral(pb)
    q = (n @ Vector((0, 1, 0))).rotation_difference(direccion.normalized())
    m = (q.to_matrix() @ n).to_4x4()
    m.translation = pb.head
    pb.matrix = m
    bpy.context.view_layer.update()


def ik_brazo(arm, lado, objetivo, polo):
    pb_arm = arm.pose.bones[P + lado + "Arm"]
    pb_fore = arm.pose.bones[P + lado + "ForeArm"]
    pb_hand = arm.pose.bones[P + lado + "Hand"]
    hombro = pb_arm.head.copy()
    a, b = pb_arm.bone.length, pb_fore.bone.length
    v = objetivo - hombro
    d = min(max(v.length, abs(a - b) + 1e-3), a + b - 1e-3)
    u = v.normalized()
    x = (a * a - b * b + d * d) / (2 * d)
    h = math.sqrt(max(a * a - x * x, 0.0))
    pp = polo - u * polo.dot(u)
    pp.normalize()
    codo = hombro + u * x + pp * h
    muneca = hombro + u * d
    _orientar(pb_arm, codo - hombro)
    _orientar(pb_fore, muneca - codo)
    _orientar(pb_hand, muneca - codo)


def hook_tocando(deslizar=0.0, rasgueo=0.0, balanceo=0.0):
    """
    Gancho de pose: las dos manos siguen la guitarra, que va con el torso.
    deslizar mueve la mano izquierda por el mástil (m), rasgueo (-1 a 1) sube/baja la derecha sobre la boca,
    balanceo (-1 a 1) la mece un poco hacia fuera y dentro.
    """
    eje = rot_guitarra() @ Vector((0, 0, 1))
    normal = rot_guitarra() @ Vector((0, -1, 0))
    g_izq = punto_guitarra(MASTIL_MANO, 0.02) + eje * deslizar
    g_der = punto_guitarra(RASGUEO_ZONA, -0.075) + eje * (0.045 * rasgueo) + normal * (0.01 * balanceo)

    def hook(arm):
        pb = arm.pose.bones[P + "Spine2"]
        m = pb.matrix @ pb.bone.matrix_local.inverted()
        ik_brazo(arm, "Left", m @ g_izq, POLO_BRAZO["Left"])
        ik_brazo(arm, "Right", m @ g_der, POLO_BRAZO["Right"])

    return hook


# ----------------------------------------------------------------------------------------------
# Clips con la guitarra colgada (Idle, Run, Sing con IK; Hit y Die con los brazos sueltos)
# ----------------------------------------------------------------------------------------------
def crear_clips_guitarra(arm):
    torso, pies_planos, clip, _paso = rc.torso, rc.pies_planos, rc.clip, rc._paso

    def piernas_reposo():
        return {"LeftUpLeg": [("X", -3)], "RightUpLeg": [("X", 3)], "LeftLeg": [("X", 5)], "RightLeg": [("X", 5)]}

    def idle(resp, sway):
        p = piernas_reposo()
        p.update(torso(0, 1.2 * resp))
        p["Hips.loc"] = (0, 0, -0.008 * resp)
        p["Head"] = [("X", -1.2 * resp), ("Z", 2 * sway)]
        p["_hook"] = hook_tocando(rasgueo=0.15 * sway, balanceo=resp)
        return pies_planos(p)

    clip(arm, "Idle", [(0, idle(0, 0)), (30, idle(1, 1)), (60, idle(0, 0))])

    def run(adelante, vertical):
        a = 1 if adelante else -1
        p = {
            "LeftUpLeg": [("X", -36 * a)], "RightUpLeg": [("X", 36 * a)],
            "LeftLeg": [("X", 8 + (52 if a < 0 else 6))], "RightLeg": [("X", 8 + (52 if a > 0 else 6))],
            "LeftFoot": [("X", -10 * a)], "RightFoot": [("X", 10 * a)],
        }
        p.update(torso(giro=-5 * a, inclina=9))
        p["Hips.loc"] = (0, 0, vertical)
        p["_hook"] = hook_tocando(rasgueo=0.5 * a, balanceo=a)
        return p

    def con_gancho(pose, **kw):
        pose["_hook"] = hook_tocando(**kw)
        return pose

    clip(arm, "Run", [
        (0, run(True, -0.02)),
        (5, con_gancho(_paso(run(True, 0.03), run(False, 0.03), 0.5), rasgueo=0.0)),
        (10, run(False, -0.02)),
        (15, con_gancho(_paso(run(False, 0.03), run(True, 0.03), 0.5), rasgueo=0.0)),
        (20, run(True, -0.02)),
    ], suelo=False)

    def cantar(sway, rasgueo, pie):
        p = {
            "LeftUpLeg": [("X", -5), ("Z", -3 * sway)], "RightUpLeg": [("X", 3 - 8 * pie), ("Z", -3 * sway)],
            "LeftLeg": [("X", 8)], "RightLeg": [("X", 6 + 14 * pie)],
        }
        p.update(torso(giro=-3 * sway, inclina=2))
        p["Hips"] = [("Z", 4 * sway)]
        p["Head"] = [("Z", -5 * sway), ("X", 2 * pie - 3 * abs(rasgueo))]
        p["Hips.loc"] = (0.018 * sway, 0, -0.012 * abs(sway) - 0.01 * pie)
        p["_hook"] = hook_tocando(deslizar=0.03 * sway, rasgueo=rasgueo, balanceo=sway)
        return pies_planos(p)

    clip(arm, "Sing", [
        (0, cantar(-1, 1, 0)),
        (4, cantar(-0.6, -1, 0.4)),
        (9, cantar(0, 0.4, 1)),
        (13, cantar(0.6, -1, 0.2)),
        (18, cantar(1, 1, 0)),
        (22, cantar(0.6, -1, 0.4)),
        (27, cantar(0, 0.4, 1)),
        (31, cantar(-0.6, -1, 0.2)),
        (36, cantar(-1, 1, 0)),
    ])

    # Hit y Die: los brazos se sueltan de la guitarra (poses con rotaciones, sin IK).
    def golpeada(atras, brazos):
        p = bb.llevar(**torso(giro=-6 * atras, inclina=-20 * atras))
        p.pop("LeftHand", None)
        p["Head"] = [("X", 18 * atras)]
        p["LeftArm"] = [("Y", 74 - 28 * brazos), ("X", 10 * brazos)]
        p["RightArm"] = [("Y", -72 + 40 * brazos), ("X", -10 * brazos)]
        p["LeftUpLeg"] = [("X", -3 + 10 * atras)]
        p["RightUpLeg"] = [("X", 3 - 6 * atras)]
        p["Hips.loc"] = (0, 0.05 * atras, -0.02 * atras)
        return pies_planos(p)

    clip(arm, "Hit", [(0, golpeada(0, 0)), (3, golpeada(1, 1)), (6, golpeada(0.7, 0.6)), (12, golpeada(0, 0))])

    def caida(angulo, y, z, piernas_x, brazos_y, cabeza):
        p = bb.llevar()
        p.pop("LeftHand", None)
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

# ----------------------------------------------------------------------------------------------
def vista_rig(arm, cuerpo, carpeta):
    """Renders de control: la malla con los huesos encima (rayos X) de frente y de lado, para revisar el ajuste."""
    os.makedirs(carpeta, exist_ok=True)
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_WORKBENCH"
    sc.display.shading.light = "FLAT"
    sc.display.shading.color_type = "TEXTURE"
    sc.render.resolution_x, sc.render.resolution_y = 600, 720
    sc.world = bpy.data.worlds.new("w2")
    sc.world.color = (0.5, 0.5, 0.55)
    cam = bpy.data.objects.new("cam2", bpy.data.cameras.new("cam2"))
    sc.collection.objects.link(cam)
    sc.camera = cam
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = 2.0
    arm.show_in_front = True
    arm.data.display_type = "STICK"
    cuerpo.show_transparent = True
    arm.animation_data_clear()
    for b in arm.pose.bones:
        b.rotation_mode = "QUATERNION"
        b.rotation_quaternion = (1, 0, 0, 0)
        b.location = (0, 0, 0)
    for nombre, loc in (("frente", (0, -5, 0.85)), ("lado", (-5, 0, 0.85))):
        cam.location = loc
        cam.rotation_euler = (Vector((0, 0, 0.85)) - cam.location).to_track_quat("-Z", "Y").to_euler()
        sc.render.filepath = os.path.join(carpeta, f"rig_{nombre}.png")
        bpy.ops.render.render(write_still=True)


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if len(args) < 2:
        raise SystemExit("Uso: blender -b -P build_bard_tripo.py -- <carpeta_tripo> <salida.fbx> [--textura x.jpg] [--preview dir] [--blend x.blend] [--rig-preview dir]")
    carpeta, salida = args[0], args[1]
    opcion = lambda n: args[args.index(n) + 1] if n in args else None

    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.unit_settings.system = "METRIC"
    sc.unit_settings.scale_length = 1.0
    sc.render.fps = rc.FPS

    cuerpo = importar_tripo(carpeta)
    resumen = limpiar(cuerpo)
    decimar(cuerpo, DECIMAR)
    tris = tris_de(cuerpo)
    if tris > MAX_TRIS:
        raise RuntimeError(f"La malla tiene {tris} triángulos (máximo {MAX_TRIS})")

    mat = cuerpo.data.materials[0]
    mat.name = "BardBody"
    textura = opcion("--textura")
    if textura:
        imagen = next((n.image for n in mat.node_tree.nodes if n.type == "TEX_IMAGE" and n.image), None)
        if imagen is None:
            raise RuntimeError("El material de Tripo no tiene textura")
        os.makedirs(os.path.dirname(os.path.abspath(textura)), exist_ok=True)
        shutil.copyfile(bpy.path.abspath(imagen.filepath), textura)

    arm = rc.crear_armature(1.0, huesos_t())
    arm.show_in_front = False
    print(f"RES malla dims={tuple(round(x, 3) for x in cuerpo.dimensions)} limpieza={resumen} tris_final={tris}")

    sin_peso = pesar(cuerpo, arm)
    limitar_influencias(cuerpo)
    print(f"RES vertices_sin_peso_heat={sin_peso}")

    guitarra = opcion("--guitarra")
    extras = []
    if guitarra:
        laud, mat_laud = importar_guitarra(guitarra, arm)
        extras = [laud, crear_correa(arm)]
        textura_g = opcion("--textura-guitarra")
        if textura_g:
            imagen = next((n.image for n in mat_laud.node_tree.nodes if n.type == "TEX_IMAGE" and n.image), None)
            if imagen is None:
                raise RuntimeError("El material de la guitarra no tiene textura")
            os.makedirs(os.path.dirname(os.path.abspath(textura_g)), exist_ok=True)
            shutil.copyfile(bpy.path.abspath(imagen.filepath), textura_g)
    else:
        laud = crear_laud(arm)
        extras = [laud]

    if opcion("--rig-preview"):
        vista_rig(arm, cuerpo, opcion("--rig-preview"))

    rc.ARM_D.update({"Left": Matrix.Rotation(math.radians(ANGULO_BRAZO), 3, "Y"),
                     "Right": Matrix.Rotation(math.radians(-ANGULO_BRAZO), 3, "Y")})
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="POSE")
    rc.fijar_contacto(arm)
    if guitarra:
        crear_clips_guitarra(arm)
    else:
        bb.crear_clips(arm)
    bpy.ops.object.mode_set(mode="OBJECT")

    huesos = len(arm.data.bones)
    total = sum(tris_de(o) for o in [cuerpo] + extras)
    sin = [v.index for o in [cuerpo] + extras for v in o.data.vertices if not any(g.weight > 0 for g in v.groups)]
    maxinf = max(len([g for g in v.groups if g.weight > 0]) for v in cuerpo.data.vertices)
    print(f"RES huesos={huesos} tris={total} acciones={sorted(a.name for a in bpy.data.actions)} sin_peso={len(sin)} max_influencias={maxinf}")
    rc.exportar(salida)
    print(f"RES exportado {salida}")
    if opcion("--blend"):
        bpy.ops.wm.save_as_mainfile(filepath=opcion("--blend"))
        print(f"RES blend en {opcion('--blend')}")
    if opcion("--preview"):
        rc.vista_previa(arm, opcion("--preview"), bb.MUESTRAS, objetivo_z=0.85, escala=2.6)
        print(f"RES vista previa en {opcion('--preview')}")


if __name__ == "__main__":
    main()
