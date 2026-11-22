# Navegación con A* sobre una rejilla.
#
# El mapa se divide en celdas de 20 x 20 px. Una celda está bloqueada si su
# centro cae en el agua o cerca de un obstáculo (obstáculos inflados 11 px,
# para que el cuerpo del guardia no los roce).
#
# find_path:
#   1. A* con 8 direcciones (sin cortar esquinas) de la celda inicial a la final.
#   2. Suavizado de la ruta ("string pulling"): se salta los puntos intermedios
#      cuando se puede caminar en línea recta hasta uno más lejano.
#
# Celdas: tuplas (gx, gy). Puntos: tuplas (x, y) en px.

import heapq
import math

import numpy as np

from app.geometry import clamp, dist, rect_distance
from app.map_data import OBSTACLES, WORLD_H, WORLD_W, is_water
from app.params import (
    NAV_CELL, NAV_INFLATE, NAV_MAX_ITERATIONS, NAV_WALK_CLEARANCE, NAV_WATER_MARGIN,
)

SQRT2 = 1.4142


class NavGrid:
    def __init__(self, obstacles=OBSTACLES, cell=NAV_CELL):
        self.cell = cell
        self.obstacles = obstacles
        self.width = math.ceil(WORLD_W / cell)
        self.height = math.ceil(WORLD_H / cell)
        # blocked[gx][gy] = 1 si no se puede pisar esa celda
        self.blocked = np.zeros((self.width, self.height), dtype=int)
        for gx in range(self.width):
            for gy in range(self.height):
                if self.cell_is_blocked(gx, gy):
                    self.blocked[gx][gy] = 1

    def cell_is_blocked(self, gx, gy):
        cx, cy = self.cell_center((gx, gy))
        if is_water((cx - 4, cy)):
            return True
        m = NAV_INFLATE
        for o in self.obstacles:
            x, y, w, h = o["rect"]
            if x - m < cx < x + w + m and y - m < cy < y + h + m:
                return True
        return False

    # ---------- celdas ----------
    def cell_of(self, p):
        """Celda que contiene el punto"""
        gx = clamp(int(p[0] // self.cell), 0, self.width - 1)
        gy = clamp(int(p[1] // self.cell), 0, self.height - 1)
        return (gx, gy)

    def cell_center(self, c):
        return (c[0] * self.cell + self.cell / 2, c[1] * self.cell + self.cell / 2)

    def is_free(self, c):
        return self.blocked[c[0]][c[1]] == 0

    def nearest_free(self, c):
        """La celda libre más cercana (busca en anillos de hasta 7 celdas)"""
        if self.is_free(c):
            return c
        for r in range(1, 8):
            for dy in range(-r, r + 1):
                for dx in range(-r, r + 1):
                    x = c[0] + dx
                    y = c[1] + dy
                    if 0 <= x < self.width and 0 <= y < self.height and self.is_free((x, y)):
                        return (x, y)
        return c

    # ---------- caminar en línea recta ----------
    def point_clear(self, p):
        """¿Cabe una persona en este punto? (lejos del agua y de los obstáculos)"""
        if is_water((p[0] - NAV_WATER_MARGIN, p[1])):
            return False
        for o in self.obstacles:
            if rect_distance(p, o["rect"]) < NAV_WALK_CLEARANCE:
                return False
        return True

    def line_walkable(self, a, b):
        """¿Se puede caminar en línea recta de a hasta b? (revisa un punto cada 6 px)"""
        n = math.ceil(dist(a, b) / 6)
        for k in range(1, n + 1):
            x = a[0] + (b[0] - a[0]) * k / n
            y = a[1] + (b[1] - a[1]) * k / n
            if not self.point_clear((x, y)):
                return False
        return True

    # ---------- A* ----------
    def heuristic(self, c, t):
        """Distancia octil: el costo de llegar con pasos rectos (1) y diagonales (1.41)"""
        dx = abs(c[0] - t[0])
        dy = abs(c[1] - t[1])
        return (dx + dy) + (SQRT2 - 2) * min(dx, dy)

    def find_path(self, a, b):
        """Ruta de a hasta b como lista de puntos (sin incluir a). Lista vacía si no hay ruta."""
        start = self.nearest_free(self.cell_of(a))
        goal = self.nearest_free(self.cell_of(b))
        goal_is_exact = goal == self.cell_of(b)   # b está en una celda libre
        if start == goal:
            return [b]

        g = {start: 0}          # costo para llegar a cada celda
        came_from = {}          # de qué celda se llegó
        closed = set()          # celdas ya revisadas
        heap = [(self.heuristic(start, goal), start)]
        found = False
        iterations = 0

        while len(heap) > 0 and iterations < NAV_MAX_ITERATIONS:
            iterations += 1
            f, current = heapq.heappop(heap)
            if current in closed:
                continue
            closed.add(current)
            if current == goal:
                found = True
                break
            x, y = current
            for dy in [-1, 0, 1]:
                for dx in [-1, 0, 1]:
                    if dx == 0 and dy == 0:
                        continue
                    nx = x + dx
                    ny = y + dy
                    if nx < 0 or ny < 0 or nx >= self.width or ny >= self.height:
                        continue
                    neighbor = (nx, ny)
                    if not self.is_free(neighbor) or neighbor in closed:
                        continue
                    # en diagonal no se cortan esquinas: las dos celdas rectas deben estar libres
                    if dx != 0 and dy != 0:
                        if not self.is_free((nx, y)) or not self.is_free((x, ny)):
                            continue
                    step = SQRT2 if (dx != 0 and dy != 0) else 1
                    new_g = g[current] + step
                    if new_g < g.get(neighbor, math.inf):
                        g[neighbor] = new_g
                        came_from[neighbor] = current
                        heapq.heappush(heap, (new_g + self.heuristic(neighbor, goal), neighbor))

        if not found:
            return []

        # reconstruir la ruta de la meta hacia atrás
        cells = []
        c = goal
        while c != start:
            cells.append(c)
            c = came_from[c]
        cells.reverse()
        points = [self.cell_center(c) for c in cells]
        if goal_is_exact:
            points[-1] = b
        return self.smooth(a, points)

    def smooth(self, a, points):
        """String pulling: desde el punto actual, salta al punto más lejano visible en línea recta"""
        out = []
        current = a
        i = 0
        while i < len(points):
            j = len(points) - 1
            while j > i and not self.line_walkable(current, points[j]):
                j -= 1
            out.append(points[j])
            current = points[j]
            i = j + 1
        return out

    # ---------- puntos al azar ----------
    def random_near(self, c, r, rng):
        """Punto libre al azar a menos de r px de c (rng = self.random del modelo)"""
        for _ in range(20):
            x = c[0] + rng.uniform(-r, r)
            y = c[1] + rng.uniform(-r, r)
            inside = 20 < x < WORLD_W - 20 and 20 < y < WORLD_H - 20
            if inside and self.is_free(self.cell_of((x, y))):
                return (x, y)
        return (c[0], c[1])
