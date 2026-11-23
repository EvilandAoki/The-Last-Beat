"""
Sintetizador mínimo de 8 bits para el audio de The Last Beat: canales al estilo NES (pulso con ancho de
pulso, triángulo de 4 bits y ruido LFSR), envolventes, secuenciador por semicorcheas y utilidades de mezcla.

Todo el audio del juego sale de estos generadores: composición original, sin samples de terceros.
Requiere numpy, scipy y soundfile (pip install numpy scipy soundfile).
"""
import numpy as np
import soundfile as sf
from scipy import signal

SR = 48000  # a 120 BPM una semicorchea son 6000 muestras exactas
NOTAS = {"C": 0, "C#": 1, "D": 2, "D#": 3, "E": 4, "F": 5, "F#": 6, "G": 7, "G#": 8, "A": 9, "A#": 10, "B": 11}


def midi(nota):
    """'A4' -> 69, 'C#5' -> 73."""
    return NOTAS[nota[:-1]] + 12 * (int(nota[-1]) + 1)


def hz(n):
    return 440.0 * 2 ** ((n - 69) / 12)


def segundos(muestras):
    return muestras / SR


# ------------------------------------------------------------------------------------------- osciladores
def _polyblep(t, dt):
    """Corrige los saltos de la onda cuadrada para que no suene áspera en las notas agudas."""
    y = np.zeros_like(t)
    dt = np.broadcast_to(dt, t.shape)
    a = t < dt
    x = t[a] / dt[a]
    y[a] = x + x - x * x - 1
    b = t > 1 - dt
    x = (t[b] - 1) / dt[b]
    y[b] = x * x + x + x + 1
    return y


def fase(frec, n):
    """Fase normalizada (0..1) para una frecuencia constante o un arreglo de frecuencias."""
    f = np.broadcast_to(np.asarray(frec, float), (n,))
    return np.cumsum(f / SR) % 1.0, f / SR


def pulso(frec, n, ancho=0.5):
    """Canal de pulso: ancho 0.125, 0.25 o 0.5 como en la NES."""
    t, dt = fase(frec, n)
    y = np.where(t < ancho, 1.0, -1.0)
    y += _polyblep(t, dt)
    y -= _polyblep((t - ancho) % 1.0, dt)
    return y


def triangulo(frec, n, pasos=16):
    """Triángulo escalonado (4 bits) como el canal de bajo de la NES."""
    t, _ = fase(frec, n)
    y = 4 * np.abs(t - 0.5) - 1
    return np.round((y + 1) / 2 * (pasos - 1)) / (pasos - 1) * 2 - 1


def sierra(frec, n):
    t, dt = fase(frec, n)
    return 2 * t - 1 - _polyblep(t, dt)


def seno(frec, n):
    t, _ = fase(frec, n)
    return np.sin(2 * np.pi * t)


def ruido_lfsr(n, frec_reloj=12000.0, corto=False, semilla=1):
    """Ruido de la NES: registro de 15 bits; 'corto' da el timbre metálico de los platillos."""
    reg = (semilla * 7919) % 32767 or 1
    pasos = int(np.ceil(n * frec_reloj / SR)) + 1
    bits = np.empty(pasos)
    tap = 6 if corto else 1
    for i in range(pasos):
        b = (reg ^ (reg >> tap)) & 1
        reg = (reg >> 1) | (b << 14)
        bits[i] = 1.0 if reg & 1 else -1.0
    idx = (np.arange(n) * frec_reloj / SR).astype(int)
    return bits[idx]


def ruido_blanco(n, rng):
    return rng.uniform(-1, 1, n)


# ------------------------------------------------------------------------------------------- envolventes y filtros
def adsr(n, a=0.002, d=0.05, s=0.7, r=0.02):
    """Envolvente para una nota de n muestras (r se suma después del final de la nota)."""
    na, nd, nr = int(a * SR), int(d * SR), int(r * SR)
    env = np.full(n + nr, s, float)
    na = min(na, n)
    env[:na] = np.linspace(0, 1, na, endpoint=False)
    nd_fin = min(na + nd, n)
    env[na:nd_fin] = np.linspace(1, s, nd_fin - na, endpoint=False) if nd_fin > na else env[na:nd_fin]
    nivel_final = env[n - 1] if n > 0 else s
    env[n:] = np.linspace(nivel_final, 0, nr)
    return env


def decae(n, tau):
    """Caída exponencial con constante de tiempo tau (s)."""
    return np.exp(-np.arange(n) / (tau * SR))


def pasa_banda(x, f1, f2, orden=2):
    sos = signal.butter(orden, [f1, f2], btype="band", fs=SR, output="sos")
    return signal.sosfilt(sos, x)


def pasa_bajos(x, f, orden=2):
    sos = signal.butter(orden, f, btype="low", fs=SR, output="sos")
    return signal.sosfilt(sos, x)


def pasa_altos(x, f, orden=2):
    sos = signal.butter(orden, f, btype="high", fs=SR, output="sos")
    return signal.sosfilt(sos, x)


def filtro_variable(x, centros, q=2.0, bloque=256):
    """Pasa banda cuyo centro se mueve en el tiempo (silbidos y barridos)."""
    y = np.zeros_like(x)
    zi = None
    for i in range(0, len(x), bloque):
        c = float(np.clip(centros[min(i, len(centros) - 1)], 40, SR / 2 - 2000))
        sos = signal.butter(1, [c / (1 + 0.5 / q), c * (1 + 0.5 / q)], btype="band", fs=SR, output="sos")
        if zi is None:
            zi = np.zeros((sos.shape[0], 2))
        y[i:i + bloque], zi = signal.sosfilt(sos, x[i:i + bloque], zi=zi)
    return y


def saturar(x, drive=2.0):
    return np.tanh(x * drive) / np.tanh(drive)


def reducir_bits(x, bits=6):
    q = 2 ** (bits - 1)
    return np.round(x * q) / q


# ------------------------------------------------------------------------------------------- secuenciador
class Pista:
    """Buffer mono de una pista. Los tiempos se dan en semicorcheas (step) desde el inicio."""

    def __init__(self, n_muestras, muestras_por_paso):
        self.buf = np.zeros(n_muestras)
        self.mpp = muestras_por_paso

    def poner(self, paso, sonido, ganancia=1.0):
        i = int(round(paso * self.mpp))
        if i >= len(self.buf):
            return
        sonido = entrada_suave(sonido)
        fin = min(len(self.buf), i + len(sonido))
        self.buf[i:fin] += sonido[:fin - i] * ganancia


def entrada_suave(x, s=0.0007):
    """Rampa de 0,7 ms al empezar un sonido: quita el clic sin quitarle el golpe."""
    n = min(len(x), int(s * SR))
    x = x.copy()
    x[:n] *= np.linspace(0, 1, n, endpoint=False) if x.ndim == 1 else np.linspace(0, 1, n, endpoint=False)[:, None]
    return x


def mezclar_paneo(pistas):
    """pistas: lista de (mono, ganancia, paneo -1..1). Devuelve estéreo (n, 2) con paneo de potencia constante."""
    n = max(len(p[0]) for p in pistas)
    out = np.zeros((n, 2))
    for mono, g, pan in pistas:
        ang = (pan + 1) * np.pi / 4
        out[:len(mono), 0] += mono * g * np.cos(ang)
        out[:len(mono), 1] += mono * g * np.sin(ang)
    return out


def eco(x, retardo_s, realim=0.3, mezcla=0.25, repeticiones=4):
    y = x.copy()
    d = int(retardo_s * SR)
    for k in range(1, repeticiones + 1):
        if d * k >= len(x):
            break
        y[d * k:] += x[:-d * k] * mezcla * realim ** (k - 1)
    return y


def ciclo(x, n_ciclo):
    """De un render de dos pasadas, toma la segunda: las colas del final ya suenan al empezar (bucle limpio)."""
    return x[n_ciclo:2 * n_ciclo]


# ------------------------------------------------------------------------------------------- niveles y archivos
def db(x):
    return 20 * np.log10(max(x, 1e-12))


def pico(x):
    return float(np.max(np.abs(x)))


def rms_max(x, ventana_s=0.05):
    """RMS más alto en ventanas cortas: mide qué tan fuerte se oye un efecto."""
    m = x if x.ndim == 1 else x.mean(axis=1)
    w = max(1, int(ventana_s * SR))
    if len(m) < w:
        return float(np.sqrt(np.mean(m ** 2)))
    e = np.convolve(m ** 2, np.ones(w) / w, mode="valid")
    return float(np.sqrt(e.max()))


def fundido_final(x, s=0.01):
    n = min(len(x), int(s * SR))
    x[-n:] *= np.linspace(1, 0, n)[:, None] if x.ndim == 2 else np.linspace(1, 0, n)
    return x


def guardar(ruta, x):
    x = np.clip(x, -1, 1)
    if ruta.endswith(".ogg"):
        # Calidad máxima: con la de por defecto las ondas cuadradas pierden ~8 % y las pistas ya no suman la mezcla.
        sf.write(ruta, x, SR, format="OGG", subtype="VORBIS", compression_level=0.0)
    else:
        sf.write(ruta, x, SR, subtype="PCM_16")
