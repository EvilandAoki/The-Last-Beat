"""
Efectos de sonido básicos de The Last Beat (tarea 31). Genera, en Assets/_Project/Audio/SFX/, WAV mono a 48 kHz:

  Combo_Ataque_Debil_01..03   Combo_Ataque_Fuerte_01..03   barrido del arma al empezar cada golpe
  Impacto_Debil_01..03        Impacto_Fuerte_01..03        golpe que conecta con un enemigo
  Giro_01..02                 Explosion_01..02             habilidad Giro (1,4 s, 3 vueltas) y su explosión final
  Embestida_01..02                                         habilidad Embestida (0,7 s)
  Dano_Recibido_01..03                                     el caballero recibe daño
  Soldado_Muerte_01..02                                    muere un demonio soldado
  Cancion_Interrumpida_01..02                              golpean a la bardo y su canción se corta

Sintetizados desde cero (sin samples). Cada categoría queda a un volumen de referencia común y con pico
máximo en -1 dBFS. Uso:  python Tools/Audio/sfx.py [carpeta_de_salida]
"""
import os
import sys

import numpy as np

from chiptune import (SR, db, decae, entrada_suave, filtro_variable, guardar, pasa_altos, pasa_banda, pasa_bajos,
                      pico, pulso, reducir_bits, rms_max, saturar, seno, sierra, triangulo)

# Volumen de referencia por categoría: RMS máximo en ventanas de 50 ms (dBFS).
NIVEL = {"Combo_Ataque_Debil": -17, "Combo_Ataque_Fuerte": -15, "Impacto_Debil": -12, "Impacto_Fuerte": -10,
         "Giro": -16, "Explosion": -9, "Embestida": -14, "Dano_Recibido": -12, "Soldado_Muerte": -13,
         "Cancion_Interrumpida": -13}


def n_de(s):
    return int(s * SR)


def curva(n, ini, fin, forma=1.0):
    """Valor que va de ini a fin a lo largo de n muestras (forma > 1 = cambia más al final)."""
    return ini + (fin - ini) * np.linspace(0, 1, n) ** forma


def campana(n, pico_rel, subida=2.0, bajada=2.0):
    """Envolvente que sube hasta pico_rel (0..1 de la duración) y baja: el paso del arma."""
    t = np.linspace(0, 1, n)
    e = np.where(t < pico_rel, (t / pico_rel) ** subida, ((1 - t) / (1 - pico_rel)) ** bajada)
    return e


# ------------------------------------------------------------------------------------------- golpes del combo
def barrido(rng, fuerte, variante):
    dur = rng.uniform(0.34, 0.42) if fuerte else rng.uniform(0.2, 0.26)
    n = n_de(dur)
    ruido = rng.uniform(-1, 1, n)
    lo, hi = (380, 1500) if fuerte else (700, 3200)
    centro = np.concatenate([curva(n // 2, lo, hi * rng.uniform(0.9, 1.1), 1.5), curva(n - n // 2, hi, lo * 1.2, 0.7)])
    x = filtro_variable(ruido, centro, q=1.6) * campana(n, rng.uniform(0.45, 0.6), 1.6, 2.2)
    if fuerte:
        grave = (85, 110, 70)[variante]
        x += seno(curva(n, grave, grave * 0.7), n) * campana(n, 0.5, 2, 2) * 0.3    # peso del espadón
    return x


def impacto(rng, fuerte, variante):
    """Variante 0: golpe seco. 1: el arma contra la armadura (timbre metálico). 2: crujiente, más 8 bits."""
    n = n_de(0.5 if fuerte else 0.3)
    k = rng.uniform(0.85, 1.2) * (0.85, 1.0, 1.15)[variante]
    golpe = seno(40 * k + (130 if fuerte else 160) * k * decae(n, 0.025), n) * decae(n, 0.11 if fuerte else 0.06)
    chasquido = pasa_altos(rng.uniform(-1, 1, n), 2200) * decae(n, 0.014 if fuerte else 0.008)
    cuerpo = pasa_banda(rng.uniform(-1, 1, n), 350, 1800) * decae(n, 0.05 if fuerte else 0.03)
    x = golpe + chasquido * 0.4 + cuerpo * (0.8 if fuerte else 0.6)
    if variante == 1:
        metal = sum(seno(f * k, n) * decae(n, tau) for f, tau in ((1870, 0.09), (2730, 0.06), (4110, 0.04)))
        x = x * 0.8 + metal * 0.22
    elif variante == 2:
        blip = pulso(curva(n, 1100 * k, 160 * k, 0.5), n, 0.125) * decae(n, 0.04)
        x = x * 0.85 + blip * 0.45 + pasa_banda(rng.uniform(-1, 1, n), 800, 2600) * decae(n, 0.02) * 0.6
    else:
        x = x + pulso(curva(n, 900 * k, 180 * k, 0.5), n, 0.25) * decae(n, 0.025) * 0.2
    return pasa_bajos(saturar(x, 2.8 if fuerte else 2.0), 9000)


# ------------------------------------------------------------------------------------------- habilidades
def giro(rng):
    n = n_de(1.4)
    t = np.arange(n) / SR
    vuelta = 0.5 - 0.5 * np.cos(2 * np.pi * (3 / 1.4) * t + rng.uniform(0, 0.5))   # 3 vueltas en 1,4 s
    centro = 500 + 1900 * vuelta
    x = filtro_variable(rng.uniform(-1, 1, n), centro, q=1.4) * (0.45 + 0.55 * vuelta)
    env = np.clip(t / 0.08, 0, 1) * np.clip((1.4 - t) / 0.15, 0, 1)
    zumbido = pulso(curva(n, 110, 150) * (1 + 0.04 * vuelta), n, 0.125) * 0.06
    return (x + zumbido) * env


def explosion(rng, variante):
    n = n_de(1.5)
    t = np.arange(n) / SR
    base = (45, 36)[variante]
    boom = seno(base + (70, 95)[variante] * decae(n, 0.06), n) * decae(n, (0.28, 0.4)[variante])
    rugido = pasa_bajos(np.cumsum(rng.uniform(-1, 1, n)) / 60, 900) * decae(n, 0.35)  # ruido marrón
    rugido /= max(pico(rugido), 1e-9)
    estallido = pasa_altos(rng.uniform(-1, 1, n), 1500) * decae(n, 0.03)
    chispas = np.zeros(n)
    for i in rng.integers(0, n_de(0.7), 40):
        m = min(n - i, n_de(0.004))
        chispas[i:i + m] += rng.uniform(-1, 1, m) * rng.uniform(0.3, 1) * np.exp(-t[i] / 0.3)
    x = boom * 1.0 + rugido * (0.8, 1.0)[variante] + estallido * 0.5 + pasa_altos(chispas, 2500) * (0.35, 0.15)[variante]
    return pasa_bajos(reducir_bits(saturar(x, 2.2), 8), 8000)


def embestida(rng, variante):
    """Variante 0: carga con pisadas. 1: arranque explosivo y viento más agudo."""
    n = n_de(0.72)
    t = np.arange(n) / SR
    carga = np.clip(t / (0.1, 0.03)[variante], 0, 1) * np.clip((0.72 - t) / 0.3, 0, 1)
    lo, hi = ((250, 1300), (450, 2300))[variante]
    viento = filtro_variable(rng.uniform(-1, 1, n), curva(n, lo, hi * rng.uniform(0.9, 1.1), 0.8), q=1.2)
    retumbo = seno(curva(n, (55, 75)[variante], (95, 60)[variante]), n) * 0.5
    x = (viento + retumbo) * carga
    if variante == 0:
        for k in range(4):                                   # pisadas rápidas de la carga
            i = n_de(0.12 + k * 0.085)
            m = min(n - i, n_de(0.06))
            x[i:i + m] += seno(110, m) * decae(m, 0.015) * 0.6
    else:
        m = n_de(0.25)
        x[:m] += pasa_banda(rng.uniform(-1, 1, m), 120, 900) * decae(m, 0.05) * 0.9   # golpe del arranque
    return saturar(x, 1.6)


# ------------------------------------------------------------------------------------------- daño, muerte y canción
def dano_recibido(rng):
    n = n_de(0.32)
    t = np.arange(n) / SR
    f0 = rng.uniform(620, 820)
    f = curva(n, f0, f0 * 0.32, 0.7) * (1 + 0.05 * np.sin(2 * np.pi * 38 * t))
    quejido = pulso(f, n, 0.5) * decae(n, 0.12) * 0.55
    golpe = seno(curva(n, 140, 60), n) * decae(n, 0.05)
    ruido = pasa_banda(rng.uniform(-1, 1, n), 500, 3000) * decae(n, 0.02) * 0.5
    return saturar(quejido + golpe + ruido, 1.8)


def soldado_muerte(rng):
    n = n_de(0.9)
    t = np.arange(n) / SR
    f = curva(n, rng.uniform(120, 145), 48, 0.6)
    raspado = 1 + 0.5 * np.sin(2 * np.pi * 31 * t)          # garganta áspera
    grunido = pasa_banda(sierra(f, n) * raspado, 250, 1100) * np.clip(t / 0.03, 0, 1) * np.clip((0.7 - t) / 0.4, 0, 1)
    caida = triangulo(curva(n, 420, 90, 0.5), n) * decae(n, 0.18) * 0.25   # arpegio que se apaga, 8 bits
    poof = pasa_banda(rng.uniform(-1, 1, n), 600, 4000) * np.exp(-np.maximum(t - 0.45, 0) / 0.12) * (t > 0.45) * 0.5
    return saturar(grunido * 1.2 + caida + poof, 1.8)


def cancion_interrumpida(rng, variante):
    n = n_de(0.8)
    t = np.arange(n) / SR
    if variante == 0:
        # El acorde del laúd se frena como una cinta (tape stop) y se rompe en ruido.
        frenado = curva(n, 1.0, 0.22, 1.6)
        x = sum(pulso(440 * 2 ** (s / 12) * frenado, n, 0.25) for s in (0, 3, 7)) / 3
        x *= decae(n, 0.35)
    else:
        # Rayón de disco: el tono va y viene rápido y se ahoga.
        vaiven = 1 + 0.45 * np.sin(2 * np.pi * curva(n, 9, 3) * t)
        x = sum(pulso(523 * 2 ** (s / 12) * vaiven * curva(n, 1, 0.5), n, 0.125) for s in (0, 4, 7)) / 3
        x *= decae(n, 0.25)
    cortes = (np.floor(t / 0.045) % 2 == 0) | (t < 0.12)     # tartamudeo digital
    glitch = pasa_altos(rng.uniform(-1, 1, n), 1800) * ((t > 0.08) & (t < 0.35)) * 0.35
    return pasa_bajos(reducir_bits((x * cortes + glitch) * np.clip((0.8 - t) / 0.2, 0, 1), 7), 7000)


# ------------------------------------------------------------------------------------------- salida
def normalizar(x, categoria):
    x = entrada_suave(np.asarray(x, float))
    x = x * 10 ** (NIVEL[categoria] / 20) / rms_max(x)
    if pico(x) > 10 ** (-1 / 20):
        x *= 10 ** (-1 / 20) / pico(x)
    n = min(len(x), n_de(0.01))
    x[-n:] *= np.linspace(1, 0, n)
    return x


def principal():
    raiz = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
    salida = sys.argv[1] if len(sys.argv) > 1 else os.path.join(raiz, "Assets", "_Project", "Audio", "SFX")
    os.makedirs(salida, exist_ok=True)
    rng = np.random.default_rng(31)
    lista = []
    for i in range(3):
        lista += [("Combo_Ataque_Debil", i, barrido(rng, False, i)), ("Combo_Ataque_Fuerte", i, barrido(rng, True, i)),
                  ("Impacto_Debil", i, impacto(rng, False, i)), ("Impacto_Fuerte", i, impacto(rng, True, i)),
                  ("Dano_Recibido", i, dano_recibido(rng))]
    for i in range(2):
        lista += [("Giro", i, giro(rng)), ("Explosion", i, explosion(rng, i)), ("Embestida", i, embestida(rng, i)),
                  ("Soldado_Muerte", i, soldado_muerte(rng)), ("Cancion_Interrumpida", i, cancion_interrumpida(rng, i))]
    for categoria, i, x in sorted(lista, key=lambda e: (e[0], e[1])):
        x = normalizar(x, categoria)
        nombre = f"{categoria}_{i + 1:02d}.wav"
        guardar(os.path.join(salida, nombre), x)
        print(f"{nombre:30s} {len(x) / SR:5.2f} s  pico {db(pico(x)):6.1f} dBFS  RMS máx {db(rms_max(x)):6.1f} dBFS")


if __name__ == "__main__":
    principal()
