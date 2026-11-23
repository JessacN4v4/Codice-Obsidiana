# Geometría 2D básica: distancias, ángulos y rectángulos.
#
# Coordenadas del mapa: x hacia la derecha, y hacia ABAJO.
#
# Un punto es una tupla (x, y).
# Un rectángulo es una tupla (x, y, w, h): esquina superior izquierda, ancho y alto.

import math


def dist(a, b):
    """Distancia entre dos puntos"""
    return math.hypot(a[0] - b[0], a[1] - b[1])


def clamp(v, lo, hi):
    """Limita v al rango [lo, hi]"""
    return max(lo, min(hi, v))


def ang_diff(a, b):
    """Diferencia b - a entre dos ángulos, tomando el camino corto (entre -pi y pi)"""
    d = b - a
    while d > math.pi:
        d -= 2 * math.pi
    while d < -math.pi:
        d += 2 * math.pi
    return d


def in_rect(p, r):
    """¿El punto está dentro del rectángulo? (en el borde no cuenta)"""
    x, y, w, h = r
    return x < p[0] < x + w and y < p[1] < y + h


def rect_center(r):
    x, y, w, h = r
    return (x + w / 2, y + h / 2)


def inflate(r, m):
    """Agranda el rectángulo m unidades por cada lado"""
    x, y, w, h = r
    return (x - m, y - m, w + 2 * m, h + 2 * m)


def rect_distance(p, r):
    """Distancia del punto al rectángulo (0 si está dentro)"""
    x, y, w, h = r
    cx = clamp(p[0], x, x + w)   # punto del rectángulo más cercano
    cy = clamp(p[1], y, y + h)
    return dist(p, (cx, cy))


def rects_overlap(a, b):
    """¿Los dos rectángulos se enciman?"""
    ax, ay, aw, ah = a
    bx, by, bw, bh = b
    return ax < bx + bw and bx < ax + aw and ay < by + bh and by < ay + ah


def seg_rect(a, b, r):
    """Cruce de un segmento a -> b con un rectángulo (algoritmo de Liang-Barsky).

    Regresa t entre 0 y 1: la fracción del segmento donde entra al
    rectángulo. Regresa None si no lo toca.
    """
    rx, ry, rw, rh = r
    dx = b[0] - a[0]
    dy = b[1] - a[1]
    t0 = 0.0
    t1 = 1.0
    p = [-dx, dx, -dy, dy]
    q = [a[0] - rx, rx + rw - a[0], a[1] - ry, ry + rh - a[1]]
    for i in range(4):
        if p[i] == 0:
            if q[i] < 0:
                return None          # paralelo al lado y por fuera
        else:
            t = q[i] / p[i]
            if p[i] < 0:
                if t > t1:
                    return None
                if t > t0:
                    t0 = t
            else:
                if t < t0:
                    return None
                if t < t1:
                    t1 = t
    return t0
