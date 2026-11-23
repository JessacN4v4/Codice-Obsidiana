# Agentes de la misión.
#
# Guard: guardia mexica. Todos los guardias son de esta clase; el rol solo
# cambia lo que hacen en reposo:
#   - SENTRY (centinela): quieto en su puesto, gira para vigilar.
#   - CANOE_GUARD (guardia de la canoa): arriba de la canoa, gira muy poco.
#     Solo una piedra lo hace moverse; ignora fuego, alarma y llamadas.
#   - PATROL_OUTSIDE / PATROL_INSIDE: recorren su ruta en ciclo usando A*.
#
# Estados:
#   "stand"       quieto en su puesto (centinelas y guardia de la canoa)
#   "patrol"      recorriendo su ruta
#   "investigate" va a un punto (ruido, fuego, algo sospechoso) y mira alrededor
#   "chase"       persigue al jugador (solo en fase 2)
#   "search"      busca alrededor de la última posición donde vieron al jugador
#   "return"      regresa a su puesto o a su ruta
#
# Villager: aldeano que deambula por la plaza, o cargador que va y viene de un almacén.
# Walker: la parte común (caminar con A*), de la que heredan Guard y Villager.

import math

import mesa

from app.geometry import ang_diff, dist
from app.map_data import CANOE_GUARD, PLAZA_CENTER, SENTRY, in_maize, zone_at
from app.navigation import resolve
from app.params import (
    AGENT_RADIUS, AWARENESS_BASE_RATE, AWARENESS_CHASE, AWARENESS_CROWD_MULT, AWARENESS_DECAY,
    AWARENESS_DISTANCE_RATE, AWARENESS_INVESTIGATE, AWARENESS_RUNNING_MULT, CANOE_SCAN_AMPLITUDE,
    CANOE_SCAN_FREQUENCY, CAPTURE_RADIUS, GUARD_SPEED, GUARD_TURN_RATE,
    INVESTIGATE_LOOK_AMPLITUDE, INVESTIGATE_LOOK_FREQUENCY, INVESTIGATE_LOOK_TIME,
    SEARCH_PAUSE, SEARCH_RADIUS, SEARCH_TIME, SENTRY_SCAN_AMPLITUDE, SENTRY_SCAN_FREQUENCY,
    STUCK_TIME, SUSPICION_ALERT_FACTOR, SUSPICION_BASE_RATE, SUSPICION_BURNING_MULT,
    SUSPICION_CARRIER_MULT, SUSPICION_DISTANCE_FALLOFF, SUSPICION_INVESTIGATE_THRESHOLD,
    SUSPICION_PUBLIC_CLOSE_RADIUS, SUSPICION_PUBLIC_CLOSE_RATE, SUSPICION_PUBLIC_RUN_PER_ALERT,
    SUSPICION_PUBLIC_RUN_RADIUS, SUSPICION_PUBLIC_RUN_RATE, SUSPICION_RUNNING_MULT,
    CARRIER_SPEED, CARRIER_WAIT, CARRIER_WAIT_AT_WAREHOUSE, VILLAGER_PHASE2_MULT,
    VILLAGER_RADIUS, VILLAGER_SPEED, VILLAGER_WAIT_MAX, VILLAGER_WAIT_MIN,
    VILLAGER_WANDER_RADIUS,
)
from app.vision import sees_player, vision_range


class Walker(mesa.Agent):
    """Persona que camina por el mapa siguiendo rutas de A* (guardias y aldeanos)"""

    def __init__(self, model, pos, radius):
        super().__init__(model)
        self.pos = pos
        self.radius = radius
        self.face = 0.0                    # hacia dónde mira
        self.path = []                     # puntos que faltan de la ruta de A*
        self.goal = None
        self.stuck = 0.0                   # tiempo que lleva casi sin avanzar

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

    def heading_to_path(self):
        """Ángulo hacia el siguiente punto de la ruta"""
        nxt = self.path[0]
        return math.atan2(nxt[1] - self.pos[1], nxt[0] - self.pos[0])


class Guard(Walker):
    def __init__(self, model, spawn):
        super().__init__(model, spawn["pos"], AGENT_RADIUS)
        self.role = spawn["role"]
        self.zone = spawn["zone"]          # almacén al que pertenece (None = pueblo)
        self.route = list(spawn["route"])
        self.ri = 0                        # índice del punto de ruta al que va
        self.base_face = spawn["face"]     # hacia dónde mira en su puesto
        self.face = spawn["face"]          # hacia dónde mira ahora
        self.target_face = spawn["face"]   # hacia dónde quiere girar

        # percepción
        self.seeing = False                # ¿ve al jugador en este momento?
        self.awareness = 0.0               # alerta individual (fase 2), de 0 a 100
        self.raised_alarm = False          # guardia de la canoa: ya avisó a los demás

        # temporizadores de los estados
        self.look_t = 0.0                  # investigar: tiempo mirando alrededor
        self.look_base = 0.0
        self.search_t = 0.0                # buscar: tiempo que le queda
        self.pause = 0.0                   # buscar: pausa entre puntos
        self.repath = 0.0                  # perseguir: cuándo recalcular la ruta

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

    def is_canoe_guard(self):
        return self.role == CANOE_GUARD

    def turn(self, dt):
        """Gira poco a poco hacia target_face"""
        self.face += ang_diff(self.face, self.target_face) * min(1.0, dt * GUARD_TURN_RATE)

    # ---------- cambios de estado ----------
    def investigate(self, target):
        """Ir a ver un punto y mirar alrededor al llegar"""
        self.state = "investigate"
        self.set_path(target)
        self.look_t = 0.0

    def search(self, seconds, target):
        """Buscar al jugador alrededor de target durante unos segundos"""
        self.state = "search"
        self.search_t = seconds
        self.set_path(target)

    def go_return(self):
        """Regresar a su puesto o al punto de ruta en el que iba"""
        self.state = "return"
        if self.post is not None:
            self.set_path(self.post)
        else:
            self.set_path(self.route[self.ri])

    # ---------- percepción ----------
    def perceive(self, dt):
        """Mira al jugador y actualiza la sospecha (fase 1) o su alerta (fase 2)"""
        model = self.model
        player = model.player["pos"]
        running = model.player["running"]
        R = vision_range(model.phase, model.alert)
        self.seeing = sees_player(self.pos, self.face, player, R, in_maize(player), running)
        d = dist(self.pos, player)

        if model.phase == 1:
            if self.seeing:
                self.add_suspicion(d, R)
        else:
            if self.seeing:
                self.add_awareness(d, R, dt)
            elif self.state != "chase":
                self.awareness = max(0.0, self.awareness - AWARENESS_DECAY * dt)
                if self.awareness == 0:
                    self.raised_alarm = False
            if d < CAPTURE_RADIUS:
                model.capture("Un guardia te atrapó.")

    def add_suspicion(self, d, R):
        """Fase 1: ver al jugador suma a la sospecha global del pueblo"""
        model = self.model
        player = model.player
        zone = zone_at(player["pos"])
        alert_mult = 1 + SUSPICION_ALERT_FACTOR * model.alert
        rate = 0.0
        if zone is not None:
            # dentro de un almacén: depende de la distancia y de la vigilancia del almacén
            rate = SUSPICION_BASE_RATE * (1 - d / R * SUSPICION_DISTANCE_FALLOFF)
            rate = rate * zone["mult"] * alert_mult
            if model.crowd_carriers > 0:
                rate *= SUSPICION_CARRIER_MULT
            if player["lighting"] is not None:
                rate *= SUSPICION_BURNING_MULT
            if player["running"]:
                rate *= SUSPICION_RUNNING_MULT
        elif player["running"] and d < SUSPICION_PUBLIC_RUN_RADIUS + SUSPICION_PUBLIC_RUN_PER_ALERT * model.alert:
            rate = SUSPICION_PUBLIC_RUN_RATE * alert_mult
        elif model.alert >= 2 and d < SUSPICION_PUBLIC_CLOSE_RADIUS:
            rate = SUSPICION_PUBLIC_CLOSE_RATE
        model.suspicion_gain += rate

        # si ya hay mucha sospecha, va a ver al jugador (el guardia de la canoa no se mueve)
        calm = self.state == "patrol" or self.state == "stand" or self.state == "return"
        if zone is not None and model.suspicion > SUSPICION_INVESTIGATE_THRESHOLD and calm:
            if not self.is_canoe_guard():
                self.investigate(player["pos"])

    def add_awareness(self, d, R, dt):
        """Fase 2: cada guardia tiene su propia alerta. A 35 investiga, a 100 persigue"""
        model = self.model
        rate = AWARENESS_BASE_RATE + AWARENESS_DISTANCE_RATE * (1 - d / R)
        if model.crowd >= 2 and not model.player["running"]:
            rate *= AWARENESS_CROWD_MULT
        if model.player["running"]:
            rate *= AWARENESS_RUNNING_MULT
        self.awareness = min(100.0, self.awareness + rate * dt)

        if self.is_canoe_guard():
            # no se mueve: solo avisa a los guardias cercanos
            if self.awareness >= AWARENESS_CHASE and not self.raised_alarm:
                self.raised_alarm = True
                model.spotted(self)
            return

        if self.state == "chase":
            model.last_known = model.player["pos"]
        elif self.awareness >= AWARENESS_CHASE:
            model.start_chase(self)
        elif self.awareness >= AWARENESS_INVESTIGATE and self.state != "investigate" and self.state != "search":
            self.investigate(model.player["pos"])

    # ---------- comportamiento por estado ----------
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

    def look_around(self, dt):
        """Investigar: al llegar, mira a los lados un rato"""
        if len(self.path) > 0:
            return
        if self.look_t == 0:
            self.look_base = self.face
        self.look_t += dt
        self.target_face = self.look_base + math.sin(self.look_t * INVESTIGATE_LOOK_FREQUENCY) * INVESTIGATE_LOOK_AMPLITUDE
        if self.look_t > INVESTIGATE_LOOK_TIME:
            if self.model.phase == 2 and self.awareness > 20 and not self.is_canoe_guard():
                self.search(5.0, self.pos)
            else:
                self.go_return()

    def chase(self, dt):
        """Perseguir: va hacia el jugador si lo ve, o a su última posición si no"""
        model = self.model
        self.repath -= dt
        if self.seeing:
            if self.repath <= 0:
                self.set_path(model.player["pos"])
                self.repath = 0.3
        else:
            last = model.last_known
            if self.repath <= 0 and (self.goal is None or dist(self.goal, last) > 20):
                self.set_path(last)
                self.repath = 0.5
            if len(self.path) == 0:
                self.state = "search"
                self.search_t = SEARCH_TIME
                self.awareness = 60.0
                model.add_event("lost", "Te perdieron de vista. Te están buscando.")

    def search_around(self, dt):
        """Buscar: va a puntos al azar cerca de la última posición vista"""
        self.search_t -= dt
        if len(self.path) == 0:
            self.pause += dt
            self.target_face = self.face + 0.8
            if self.pause > SEARCH_PAUSE:
                self.pause = 0.0
                center = self.model.last_known
                if center is None:
                    center = self.pos
                target = self.model.nav.random_near(center, SEARCH_RADIUS, self.model.random)
                self.set_path(target)
        if self.search_t <= 0:
            self.go_return()

    def come_back(self):
        """Regresar: al llegar vuelve a su estado de reposo"""
        if len(self.path) > 0:
            return
        if self.post is not None:
            if dist(self.pos, self.post) > 2:
                self.path = [self.post]       # el último tramo, derecho a su puesto
            else:
                self.pos = self.post
                self.state = "stand"
        else:
            self.state = "patrol"

    # ---------- paso ----------
    def step(self):
        model = self.model
        if model.over:
            return
        dt = model.substep_dt

        self.perceive(dt)
        if model.over:
            return

        if self.state == "stand":
            self.stand()
        elif self.state == "patrol":
            self.patrol()
        elif self.state == "investigate":
            self.look_around(dt)
        elif self.state == "chase":
            self.chase(dt)
        elif self.state == "search":
            self.search_around(dt)
        elif self.state == "return":
            self.come_back()

        if self.state != "stand":
            self.follow_path(GUARD_SPEED[self.state], dt)
            # mira hacia donde camina
            if len(self.path) > 0:
                self.target_face = self.heading_to_path()
        # si persigue y te ve, te mira a ti
        if self.state == "chase" and self.seeing:
            player = model.player["pos"]
            self.target_face = math.atan2(player[1] - self.pos[1], player[0] - self.pos[0])

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
            "awareness": round(self.awareness, 1),
        }


class Villager(Walker):
    """Aldeano del pueblo.

    - Aldeano normal: deambula por la plaza, va a un punto al azar y espera.
    - Cargador (carrier): recorre su ruta en ciclo entre la plaza y un almacén.
      Junto a los cargadores el jugador (disfrazado de cargador) levanta menos sospecha.
    En fase 2 caminan más rápido.
    """

    def __init__(self, model, pos, carrier=False, route=None, ri=0):
        super().__init__(model, pos, VILLAGER_RADIUS)
        self.carrier = carrier
        self.route = route if route is not None else []
        self.ri = ri
        if carrier:
            self.wait = 0.0
        else:
            self.wait = self.random.uniform(0, 2)    # espera antes de moverse

    def step(self):
        model = self.model
        if model.over:
            return
        dt = model.substep_dt
        speed = CARRIER_SPEED if self.carrier else VILLAGER_SPEED
        if model.phase == 2:
            speed *= VILLAGER_PHASE2_MULT

        if len(self.path) == 0:
            self.wait -= dt
            if self.wait > 0:
                return
            if self.carrier:
                # siguiente punto de la ruta; en el almacén (punto 2) se queda más tiempo
                self.ri = (self.ri + 1) % len(self.route)
                self.set_path(self.route[self.ri])
                self.wait = CARRIER_WAIT_AT_WAREHOUSE if self.ri == 2 else CARRIER_WAIT
            else:
                target = model.nav.random_near(PLAZA_CENTER, VILLAGER_WANDER_RADIUS, self.random)
                self.set_path(target)
                self.wait = self.random.uniform(VILLAGER_WAIT_MIN, VILLAGER_WAIT_MAX)

        if len(self.path) > 0:
            self.face = self.heading_to_path()
        self.follow_path(speed, dt)

    def to_dict(self):
        """Datos que se mandan a Unity (claves en camelCase)"""
        return {
            "id": self.unique_id,
            "carrier": self.carrier,
            "x": round(self.pos[0], 1),
            "y": round(self.pos[1], 1),
            "face": round(self.face, 3),
        }
