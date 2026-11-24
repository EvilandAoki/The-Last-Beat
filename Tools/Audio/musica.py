"""
Música de The Last Beat (tareas 29 y 30). Genera, en Assets/_Project/Audio/Music/:

  Tarea 29, canción de la bardo (dembow en 8 bits):
    Bardo_Cancion_Completa.ogg y sus pistas Bardo_Cancion_{Bateria,Bajo,Laud,Melodia}.ogg
  Tarea 30, música de combate por capas y ambiente del pueblo:
    Combate_{Base,Percusion,Melodia}.ogg, Combate_Completa.ogg (las tres juntas, de referencia)
    Ambiente_Pueblo.ogg (viento y pasos lejanos)

Todo comparte tempo (120 BPM), tonalidad (La menor), acordes (Am F C G, un acorde por compás) y duración
(16 compases = 32 s), así que cualquier combinación de capas suena a tempo y en armonía. Cada archivo es un
bucle exacto: el final empalma con el principio sin cortes.

Uso:  python Tools/Audio/musica.py [carpeta_de_salida]
"""
import os
import sys

import numpy as np

from chiptune import (SR, Pista, adsr, ciclo, db, decae, eco, guardar, hz, midi, mezclar_paneo, pasa_altos,
                      pasa_bajos, pico, pulso, ruido_lfsr, seno, triangulo)

BPM = 120
MPP = int(SR * 60 / BPM / 4)          # muestras por semicorchea (6000)
COMPASES = 16
PASOS = COMPASES * 16
N = PASOS * MPP                        # muestras de un ciclo (32 s)
ACORDES = ["Am", "F", "C", "G"]        # un acorde por compás, se repite cada 4
RAIZ = {"Am": "A", "F": "F", "C": "C", "G": "G"}
QUINTA = {"Am": "E", "F": "C", "C": "G", "G": "D"}
TONOS = {"Am": ["A3", "C4", "E4", "A4"], "F": ["F3", "A3", "C4", "F4"],
         "C": ["G3", "C4", "E4", "G4"], "G": ["G3", "B3", "D4", "G4"]}


def acorde(compas):
    return ACORDES[compas % 4]


# ------------------------------------------------------------------------------------------- instrumentos
def voz_pulso(nota, pasos, ancho=0.25, legato=0.9, vibrato=0.0, ataque=0.003, sostenido=0.75, release=0.04):
    n = int(pasos * MPP * legato)
    env = adsr(n, a=ataque, d=0.08, s=sostenido, r=release)
    f = np.full(len(env), hz(midi(nota)))
    if vibrato:
        t = np.arange(len(env)) / SR
        prof = np.clip((t - 0.15) / 0.2, 0, 1) * vibrato        # el vibrato entra tarde, como en las notas largas
        f = f * 2 ** (prof * np.sin(2 * np.pi * 5.5 * t) / 12)
    return pulso(f, len(env), ancho) * env


def laud(nota, ancho=0.25):
    """Pulsación corta: el laúd de la bardo."""
    n = int(0.32 * SR)
    f = hz(midi(nota)) * (1 + 0.02 * decae(n, 0.004))           # pequeño golpe de afinación al pulsar
    return pasa_altos(pulso(f, n, ancho) * decae(n, 0.11), 120)


def bajo_tri(nota, pasos, legato=0.92):
    n = int(pasos * MPP * legato)
    env = adsr(n, a=0.002, d=0.02, s=1.0, r=0.015)
    return triangulo(hz(midi(nota)), len(env)) * env


def bombo():
    n = int(0.28 * SR)
    t = np.arange(n) / SR
    f = 45 + 150 * np.exp(-t / 0.035)
    golpe = triangulo(f, n) * decae(n, 0.11)
    click = ruido_lfsr(n, 24000, semilla=3) * decae(n, 0.002) * 0.5
    return pasa_bajos(golpe + click, 3000)


def caja(semilla=5, tono=1.0):
    n = int(0.22 * SR)
    ruido = ruido_lfsr(n, 22000 * tono, semilla=semilla) * decae(n, 0.055)
    cuerpo = triangulo(190 * tono * (1 + 0.3 * decae(n, 0.01)), n) * decae(n, 0.035) * 0.8
    return pasa_altos(ruido * 0.9 + cuerpo, 150)


def charles(abierto=False, semilla=9):
    n = int((0.35 if abierto else 0.06) * SR)
    x = ruido_lfsr(n, 34000, corto=True, semilla=semilla) * decae(n, 0.12 if abierto else 0.014)
    return pasa_altos(x, 6000)


def platillo(semilla=11):
    n = int(1.6 * SR)
    return pasa_altos(ruido_lfsr(n, 30000, semilla=semilla) * decae(n, 0.45), 3500)


def tom(nota):
    n = int(0.3 * SR)
    f0 = hz(midi(nota))
    return triangulo(f0 * (1 + 0.6 * decae(n, 0.03)), n) * decae(n, 0.12)


# ------------------------------------------------------------------------------------------- tarea 29: canción de la bardo
# (paso dentro del compás en semicorcheas, nota, duración en semicorcheas)
MELODIA_BARDO = [
    [(0, "E5", 2), (3, "A5", 2), (6, "A5", 1), (7, "G5", 1), (8, "E5", 2), (11, "C5", 2), (14, "D5", 2)],
    [(0, "C5", 3), (3, "A4", 3), (6, "C5", 2), (8, "F5", 4), (12, "E5", 2), (14, "C5", 2)],
    [(0, "E5", 2), (3, "G5", 2), (6, "G5", 1), (7, "A5", 1), (8, "G5", 2), (11, "E5", 2), (14, "D5", 2)],
    [(0, "D5", 3), (3, "B4", 3), (6, "D5", 2), (8, "G5", 4), (12, "A5", 2), (14, "B5", 2)],
    [(0, "C6", 2), (3, "A5", 2), (6, "A5", 1), (7, "G5", 1), (8, "E5", 2), (11, "G5", 2), (14, "A5", 2)],
    [(0, "F5", 3), (3, "E5", 3), (6, "C5", 2), (8, "A4", 4), (12, "C5", 2), (14, "D5", 2)],
    [(0, "E5", 2), (3, "G5", 2), (6, "C6", 2), (8, "B5", 2), (11, "G5", 2), (14, "E5", 2)],
    [(0, "D5", 3), (3, "E5", 3), (6, "G5", 2), (8, "A5", 6)],
    [(0, "A5", 1), (1, "A5", 1), (3, "C6", 2), (6, "B5", 2), (8, "A5", 2), (11, "E5", 3), (14, "G5", 2)],
    [(0, "A5", 3), (3, "F5", 3), (6, "E5", 2), (8, "C5", 6), (14, "E5", 2)],
    [(0, "G5", 1), (1, "G5", 1), (3, "C6", 2), (6, "B5", 2), (8, "G5", 2), (11, "E5", 3), (14, "D5", 2)],
    [(0, "D5", 3), (3, "G5", 3), (6, "B5", 2), (8, "D6", 6), (14, "C6", 2)],
    [(0, "A5", 1), (1, "A5", 1), (3, "C6", 2), (6, "E6", 2), (8, "D6", 2), (11, "C6", 3), (14, "B5", 2)],
    [(0, "A5", 3), (3, "C6", 3), (6, "A5", 2), (8, "F5", 4), (12, "G5", 2), (14, "A5", 2)],
    [(0, "G5", 2), (3, "E5", 2), (6, "G5", 2), (8, "C6", 4), (12, "B5", 2), (14, "A5", 2)],
    [(0, "B5", 2), (3, "A5", 2), (6, "G5", 2), (8, "E5", 2), (11, "D5", 2), (14, "B4", 2)],
]
DEMBOW_BOMBO = [0, 4, 8, 12]
DEMBOW_CAJA = [3, 6, 11, 14]          # el 3+3+2 del dembow


def bardo_bateria(p):
    for c in range(COMPASES * 2):
        base = c * 16
        for s in DEMBOW_BOMBO:
            p.poner(base + s, bombo(), 1.0)
        relleno = c % 8 == 7
        for s in DEMBOW_CAJA:
            if not (relleno and s == 14):
                p.poner(base + s, caja(semilla=5 + s), 0.8)
        if relleno:                                              # redoble de caja al final de cada 8 compases
            for k, s in enumerate((12, 13, 14, 15)):
                p.poner(base + s, caja(semilla=20 + k, tono=1 + 0.08 * k), 0.45 + 0.12 * k)
        for s in range(16):
            if s in DEMBOW_CAJA:
                continue
            acento = s in (2, 10)
            p.poner(base + s, charles(semilla=30 + s), 0.35 if acento else 0.16)
        if c % 4 == 3:
            p.poner(base + 15, charles(abierto=True), 0.25)


def bardo_bajo(p):
    for c in range(COMPASES * 2):
        a = acorde(c)
        r, q = RAIZ[a] + "2", QUINTA[a] + ("3" if QUINTA[a] in "CDE" else "2")
        if a == "C":
            r = "C3"
        for s, nota, dur in [(0, r, 3), (3, r, 3), (6, q, 2), (8, r, 3), (11, r[:-1] + str(int(r[-1]) + 1), 3), (14, q, 2)]:
            p.poner(c * 16 + s, bajo_tri(nota, dur))


def bardo_laud(p):
    orden = [0, 2, 1, 3, 2, 1, 3, 2, 0, 2, 1, 3, 2, 3, 1, 2]
    acentos = {0, 3, 6, 8, 11, 14}
    for c in range(COMPASES * 2):
        tonos = TONOS[acorde(c)]
        for s in range(16):
            p.poner(c * 16 + s, laud(tonos[orden[s]]), 1.0 if s in acentos else 0.5)


def bardo_melodia(p):
    for c in range(COMPASES * 2):
        for s, nota, dur in MELODIA_BARDO[c % COMPASES]:
            p.poner(c * 16 + s, voz_pulso(nota, dur, ancho=0.25, vibrato=0.25 if dur >= 4 else 0.0))
    p.buf = eco(p.buf, 3 * MPP / SR, realim=0.45, mezcla=0.22)


# ------------------------------------------------------------------------------------------- tarea 30: combate por capas
MELODIA_COMBATE = [
    [(0, "A4", 4), (4, "C5", 2), (6, "E5", 2), (8, "A5", 6), (14, "G5", 2)],
    [(0, "F5", 4), (4, "E5", 2), (6, "C5", 2), (8, "A4", 8)],
    [(0, "G4", 4), (4, "C5", 2), (6, "E5", 2), (8, "G5", 6), (14, "E5", 2)],
    [(0, "D5", 6), (6, "B4", 2), (8, "G4", 4), (12, "B4", 2), (14, "D5", 2)],
    [(0, "E5", 4), (4, "A5", 2), (6, "C6", 2), (8, "B5", 4), (12, "A5", 2), (14, "G5", 2)],
    [(0, "A5", 6), (6, "F5", 2), (8, "C5", 6), (14, "D5", 2)],
    [(0, "E5", 4), (4, "G5", 2), (6, "C6", 2), (8, "B5", 4), (12, "G5", 4)],
    [(0, "D5", 4), (4, "G5", 4), (8, "B4", 8)],
    [(0, "A4", 1), (1, "A4", 1), (2, "C5", 1), (3, "A4", 1), (4, "E5", 2), (6, "A4", 2), (8, "D5", 2), (10, "C5", 2), (12, "B4", 2), (14, "C5", 2)],
    [(0, "F4", 1), (1, "F4", 1), (2, "A4", 1), (3, "F4", 1), (4, "C5", 2), (6, "F4", 2), (8, "E5", 2), (10, "D5", 2), (12, "C5", 2), (14, "A4", 2)],
    [(0, "G4", 1), (1, "G4", 1), (2, "C5", 1), (3, "G4", 1), (4, "E5", 2), (6, "G4", 2), (8, "G5", 2), (10, "F5", 2), (12, "E5", 2), (14, "C5", 2)],
    [(0, "D5", 1), (1, "D5", 1), (2, "G5", 1), (3, "D5", 1), (4, "B5", 2), (6, "D5", 2), (8, "D6", 4), (12, "B5", 4)],
    [(0, "C6", 4), (4, "B5", 2), (6, "A5", 2), (8, "E6", 6), (14, "D6", 2)],
    [(0, "C6", 4), (4, "A5", 2), (6, "F5", 2), (8, "A5", 8)],
    [(0, "G5", 4), (4, "C6", 2), (6, "E6", 2), (8, "D6", 4), (12, "C6", 2), (14, "B5", 2)],
    [(0, "B5", 4), (4, "D6", 4), (8, "B5", 2), (10, "G5", 2), (12, "E5", 2), (14, "D5", 2)],
]


def combate_base(p):
    """Bajo en corcheas que empuja y un colchón de quintas en pulso ancho."""
    for c in range(COMPASES * 2):
        a = acorde(c)
        r = RAIZ[a] + "2"
        r_oct = RAIZ[a] + "3"
        for k, nota in enumerate([r, r, r_oct, r, r, r, r_oct, r]):
            p.poner(c * 16 + 2 * k, bajo_tri(nota, 2, legato=0.7), 1.0 if k % 2 == 0 else 0.8)
        quinta = QUINTA[a] + ("4" if QUINTA[a] in "CD" else "3")
        for nota in (RAIZ[a] + "3", quinta):
            n = 16 * MPP
            env = adsr(n, a=0.08, d=0.3, s=0.6, r=0.15)
            t = np.arange(len(env)) / SR
            tremolo = 1 - 0.25 * (0.5 + 0.5 * np.sin(2 * np.pi * 4 * t))   # 4 Hz = una vez por corchea
            p.poner(c * 16, pulso(hz(midi(nota)), len(env), 0.5) * env * tremolo, 0.18)


def combate_percusion(p):
    for c in range(COMPASES * 2):
        base = c * 16
        if c % 8 == 0:
            p.poner(base, platillo(), 0.55)
        for s in (0, 8, 10):
            p.poner(base + s, bombo(), 1.0)
        for s in (4, 12):
            p.poner(base + s, caja(semilla=40 + s), 0.95)
        for s in range(0, 16, 2):
            p.poner(base + s, charles(semilla=50 + s), 0.28 if s % 4 == 2 else 0.18)
        if c % 4 == 3:                                           # redoble de toms cada 4 compases
            for k, (s, nota) in enumerate([(12, "A3"), (13, "F3"), (14, "D3"), (15, "A2")]):
                p.poner(base + s, tom(nota), 0.8)


def combate_melodia(p):
    for c in range(COMPASES * 2):
        for s, nota, dur in MELODIA_COMBATE[c % COMPASES]:
            p.poner(c * 16 + s, voz_pulso(nota, dur, ancho=0.125, vibrato=0.3 if dur >= 4 else 0.0, sostenido=0.85))
    p.buf = eco(p.buf, 2 * MPP / SR, realim=0.4, mezcla=0.18)


# ------------------------------------------------------------------------------------------- render por capas
def render(capas, niveles_db, paneos):
    """Cada capa se renderiza dos ciclos seguidos y se toma el segundo, y se iguala en RMS a su nivel
    relativo. Devuelve las capas en estéreo, todavía sin la ganancia final (ver principal)."""
    estereo = {}
    for nombre, f in capas.items():
        p = Pista(2 * N, MPP)
        f(p)
        x = ciclo(p.buf, N)
        g = 10 ** (niveles_db[nombre] / 20) / np.sqrt(np.mean(x ** 2))
        estereo[nombre] = mezclar_paneo([(x, g, paneos[nombre])])
    return estereo


# ------------------------------------------------------------------------------------------- ambiente
def ambiente_pueblo(segundos=48, semilla=7):
    """Viento con ráfagas y pasos lejanos. Todo es periódico en la duración del archivo: bucle perfecto."""
    rng = np.random.default_rng(semilla)
    n = int(segundos * SR)
    t = np.arange(n) / n                                           # 0..1 a lo largo del bucle

    def viento_canal(rng):
        # Ruido filtrado en frecuencia sobre un buffer circular: el final empalma con el inicio.
        espectro = np.fft.rfft(rng.standard_normal(n))
        f = np.fft.rfftfreq(n, 1 / SR)
        forma = 1 / np.maximum(f, 20) ** 0.9 * np.exp(-((np.log(np.maximum(f, 1) / 420)) ** 2) / 1.3)
        return np.fft.irfft(espectro * forma, n)

    rafagas = 0.55 + 0.25 * np.sin(2 * np.pi * 3 * t + 0.4) + 0.15 * np.sin(2 * np.pi * 7 * t + 1.9) \
        + 0.08 * np.sin(2 * np.pi * 13 * t + 0.7)
    izq, der = viento_canal(rng), viento_canal(rng)
    izq, der = izq / np.std(izq) * rafagas, der / np.std(der) * rafagas
    # Silbido suave del viento en las ráfagas fuertes (frecuencia con ciclos enteros en el bucle).
    f_silbido = 640 + 90 * np.sin(2 * np.pi * 2 * t)
    fase_s = np.cumsum(f_silbido / SR)
    fase_s += (np.ceil(fase_s[-1]) - fase_s[-1]) * np.arange(n) / n   # cierra la fase al final del bucle
    silbido = np.sin(2 * np.pi * fase_s) * np.clip(rafagas - 0.7, 0, None) * 0.6
    viento = np.stack([izq + silbido * 0.7, der + silbido], axis=1) * 0.12

    pasos = np.zeros((n, 2))
    for inicio_s, cuantos, pan, ritmo in [(6.0, 7, -0.6, 0.55), (27.5, 6, 0.5, 0.6), (39.0, 4, -0.2, 0.7)]:
        for k in range(cuantos):
            m = int(0.09 * SR)
            grava = sum(np.roll(pasa_altos(rng.uniform(-1, 1, m), 900) * decae(m, 0.006 + 0.004 * j),
                                int(rng.integers(0, 600))) for j in range(4))
            golpe = seno(95, m) * decae(m, 0.02) * 0.7
            paso = (grava * 0.25 + golpe) * (0.6 + 0.4 * rng.random()) * (0.8 + 0.2 * np.sin(k))
            i = int((inicio_s + k * ritmo + rng.normal(0, 0.02)) * SR) % n
            ang = (pan + 1) * np.pi / 4
            for canal, g in ((0, np.cos(ang)), (1, np.sin(ang))):
                idx = (i + np.arange(m)) % n
                pasos[idx, canal] += paso * g
    pasos = np.stack([pasa_bajos(pasos[:, 0], 2500), pasa_bajos(pasos[:, 1], 2500)], axis=1) * 0.35
    x = viento + pasos
    return x * (10 ** (-6 / 20) / pico(x))                         # ambiente: pico en -6 dBFS, va de fondo


def principal():
    raiz = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
    salida = sys.argv[1] if len(sys.argv) > 1 else os.path.join(raiz, "Assets", "_Project", "Audio", "Music")
    os.makedirs(salida, exist_ok=True)

    bardo = render(
        {"Bateria": bardo_bateria, "Bajo": bardo_bajo, "Laud": bardo_laud, "Melodia": bardo_melodia},
        {"Bateria": 0, "Bajo": -2, "Laud": -8, "Melodia": -2},
        {"Bateria": 0, "Bajo": 0, "Laud": 0.35, "Melodia": -0.15})
    combate = render(
        {"Base": combate_base, "Percusion": combate_percusion, "Melodia": combate_melodia},
        {"Base": -1, "Percusion": 0, "Melodia": -2},
        {"Base": 0, "Percusion": 0, "Melodia": 0.15})
    # Una sola ganancia para las 7 capas: aun sonando todas a la vez, el pico queda en -1 dBFS.
    # Así cualquier combinación de capas que arme la integración (#32) suena sin saturar.
    todas = sum(bardo.values()) + sum(combate.values())
    master = 10 ** (-1 / 20) / pico(todas)
    for prefijo, capas in (("Bardo_Cancion", bardo), ("Combate", combate)):
        for nombre, x in capas.items():
            guardar(os.path.join(salida, f"{prefijo}_{nombre}.ogg"), x * master)
        mezcla = sum(capas.values()) * master
        guardar(os.path.join(salida, f"{prefijo}_Completa.ogg"), mezcla)
        print(f"{prefijo}: {N / SR:.1f} s, pico {db(pico(mezcla)):.1f} dBFS, RMS {db(np.sqrt(np.mean(mezcla ** 2))):.1f} dBFS")

    amb = ambiente_pueblo()
    guardar(os.path.join(salida, "Ambiente_Pueblo.ogg"), amb)
    print(f"Ambiente: {len(amb) / SR:.1f} s, pico {db(pico(amb)):.1f} dBFS")


if __name__ == "__main__":
    principal()
