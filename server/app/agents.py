# Agentes de la misión.
#
# Guard: guardia mexica. Todos los guardias son de esta clase; el rol solo
# cambia lo que hacen en reposo:
#   - SENTRY (centinela): quieto en su puesto, gira para vigilar.
#   - CANOE_GUARD (guardia de la canoa): arriba de la canoa, gira muy poco.
#   - PATROL_OUTSIDE / PATROL_INSIDE: recorren su ruta en ciclo usando A*.
#
# Estados en reposo: "stand" (quieto) y "patrol" (recorrer la ruta).
# Los estados de reacción (investigar, perseguir, buscar, regresar) se
# agregan en el siguiente paso.

import math

import mesa

from app.geometry import ang_diff, dist
from app.map_data import CANOE_GUARD, SENTRY
from app.navigation import resolve
from app.params import (
    AGENT_RADIUS, CANOE_SCAN_AMPLITUDE, CANOE_SCAN_FREQUENCY, GUARD_SPEED, GUARD_TURN_RATE,
    SENTRY_SCAN_AMPLITUDE, SENTRY_SCAN_FREQUENCY, STUCK_TIME,
)


class Guard(mesa.Agent):
    def __init__(self, model, spawn):
        super().__init__(model)
        self.role = spawn["role"]
        self.zone = spawn["zone"]          # almacén al que pertenece (None = pueblo)
        self.route = list(spawn["route"])
        self.ri = 0                        # índice del punto de ruta al que va
        self.pos = spawn["pos"]
        self.radius = AGENT_RADIUS
        self.base_face = spawn["face"]     # hacia dónde mira en su puesto
        self.face = spawn["face"]          # hacia dónde mira ahora
        self.target_face = spawn["face"]   # hacia dónde quiere girar
        self.path = []                     # puntos que faltan de la ruta de A*
        self.goal = None
        self.stuck = 0.0                   # tiempo que lleva casi sin avanzar

        if self.is_stationary():
            self.post = spawn["pos"]       # su puesto fijo
            self.state = "stand"
        else:
            self.post = None
            self.state = "patrol"
            self.set_path(self.route[0])

    def is_stationary(self):
        """Centinelas y guardia de la canoa no tienen ruta"""
        return self.role == SENTRY or self.role == CANOE_GUARD

    # ---------- movimiento ----------
    def set_path(self, target):
        self.path = self.model.nav.find_path(self.pos, target)
        self.goal = target

    def move_to(self, target, speed, dt):
        """Avanza hacia target. Regresa True cuando ya llegó (a menos de 6 px)"""
        dx = target[0] - self.pos[0]
        dy = target[1] - self.pos[1]
        d = math.hypot(dx, dy)
        if d < 1:
            return True
        s = min(speed * dt, d)
        new_pos = (self.pos[0] + dx / d * s, self.pos[1] + dy / d * s)
        self.pos = resolve(new_pos, self.radius)
        return d < 6

    def follow_path(self, speed, dt):
        """Sigue la ruta de A*. Regresa True cuando ya no quedan puntos"""
        if len(self.path) == 0:
            return True
        before = self.pos
        if self.move_to(self.path[0], speed, dt):
            self.path.pop(0)
        # atasco: si casi no avanza, recalcula la ruta
        if dist(before, self.pos) < speed * dt * 0.25:
            self.stuck += dt
        else:
            self.stuck = 0.0
        if self.stuck > STUCK_TIME and len(self.path) > 0:
            self.path = self.model.nav.find_path(self.pos, self.path[-1])
            self.stuck = 0.0
        return len(self.path) == 0

    def turn(self, dt):
        """Gira poco a poco hacia target_face"""
        self.face += ang_diff(self.face, self.target_face) * min(1.0, dt * GUARD_TURN_RATE)

    # ---------- comportamiento en reposo ----------
    def stand(self):
        """Quieto en su puesto, girando de un lado a otro"""
        if self.role == CANOE_GUARD:
            amplitude = CANOE_SCAN_AMPLITUDE
            frequency = CANOE_SCAN_FREQUENCY
        else:
            amplitude = SENTRY_SCAN_AMPLITUDE
            frequency = SENTRY_SCAN_FREQUENCY
        t = self.model.sim_time
        self.target_face = self.base_face + math.sin(t * frequency + self.unique_id) * amplitude

    def patrol(self):
        """Al terminar un tramo, va al siguiente punto de su ruta (en ciclo)"""
        if len(self.path) == 0:
            self.ri = (self.ri + 1) % len(self.route)
            self.set_path(self.route[self.ri])

    # ---------- paso ----------
    def step(self):
        dt = self.model.substep_dt

        if self.state == "stand":
            self.stand()
        elif self.state == "patrol":
            self.patrol()

        if self.state != "stand":
            self.follow_path(GUARD_SPEED[self.state], dt)
            # mira hacia donde camina
            if len(self.path) > 0:
                nxt = self.path[0]
                self.target_face = math.atan2(nxt[1] - self.pos[1], nxt[0] - self.pos[0])

        self.turn(dt)

    def to_dict(self):
        """Datos que se mandan a Unity (claves en camelCase)"""
        return {
            "id": self.unique_id,
            "role": self.role,
            "state": self.state,
            "x": round(self.pos[0], 1),
            "y": round(self.pos[1], 1),
            "face": round(self.face, 3),
        }
