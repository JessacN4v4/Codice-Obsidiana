# Modelo de la misión (Mesa).
#
# Cada step() del cliente avanza dt segundos (0.1 s a 10 Hz). Para que los
# guardias rápidos no se "salten" al jugador, el step se divide en subpasos
# de 0.05 s.
#
# Fase 1 (infiltración): el jugador va disfrazado. Los guardias que lo ven
# suman a una sospecha global; si llega a 100 suena la alarma.
# Fase 2 (huida): el disfraz ya no sirve. Cada guardia tiene su propia alerta
# y puede perseguir y atrapar al jugador.

import mesa

from app.agents import Guard
from app.geometry import dist
from app.map_data import GUARD_SPAWNS, REINFORCEMENT_SPAWNS, START
from app.navigation import NavGrid
from app.params import (
    SEARCH_CALL_RADIUS, SEARCH_TIME, SIM_SUBSTEP, SUSPICION_DECAY, SUSPICION_DECAY_PER_ALERT,
    SUSPICION_MAX,
)

# La rejilla de navegación es igual para todas las partidas: se construye una sola vez
NAV = NavGrid()


class MissionModel(mesa.Model):
    """Simulación de una sesión de juego.

    - dt: segundos simulados por paso.
    - rng: semilla; con la misma semilla la simulación se repite igual.
    """

    def __init__(self, dt=0.1, rng=None):
        super().__init__(rng=rng)
        self.dt = dt
        self.substeps = max(1, round(dt / SIM_SUBSTEP))   # 0.1 / 0.05 = 2
        self.substep_dt = dt / self.substeps
        # Tiempo simulado en segundos. No se llama "time" porque Mesa
        # ya usa ese nombre internamente.
        self.sim_time = 0.0
        self.nav = NAV

        self.phase = 1             # 1 = infiltración, 2 = huida
        self.alert = 0             # alerta del pueblo (0 a 3)
        self.suspicion = 0.0       # sospecha global de la fase 1 (0 a 100)
        self.suspicion_gain = 0.0  # lo que suman los guardias en este subpaso
        self.last_known = None     # última posición donde vieron al jugador
        self.detections = 0        # veces que lo descubrieron
        self.over = False          # ¿terminó la partida?
        self.result = None         # "win" o "lose"
        self.events = []           # avisos para Unity en este step

        # El jugador lo mueve Unity; aquí solo se guarda lo que manda
        self.player = {"pos": START, "running": False, "lighting": None}
        # Aldeanos cerca del jugador (se calculará cuando haya aldeanos)
        self.crowd = 0
        self.crowd_carriers = 0

        for spawn in GUARD_SPAWNS:
            Guard(self, spawn)

    # ---------- consultas ----------
    def guards(self):
        """Guardias ordenados por id"""
        return sorted(self.agents_by_type[Guard], key=lambda g: g.unique_id)

    def add_event(self, kind, message):
        self.events.append({"type": kind, "message": message})

    # ---------- lo que manda el cliente ----------
    def set_player(self, x, y, running=False, lighting=None):
        """Posición del jugador. lighting = id del troje que está quemando (o None)"""
        self.player = {"pos": (x, y), "running": running, "lighting": lighting}

    # ---------- reacciones ----------
    def noise(self, pos, radius, from_stone=False):
        """Un ruido hace que los guardias cercanos vayan a ver.

        El guardia de la canoa solo hace caso al ruido de una piedra.
        """
        for g in self.guards():
            if g.state == "chase":
                continue
            if g.is_canoe_guard() and not from_stone:
                continue
            if dist(g.pos, pos) < radius:
                g.investigate(pos)

    def call_nearby(self, caller):
        """Los guardias cerca de caller van a buscar al jugador"""
        for g in self.guards():
            if g is caller or g.state == "chase" or g.is_canoe_guard():
                continue
            if dist(g.pos, caller.pos) < SEARCH_CALL_RADIUS:
                g.search(SEARCH_TIME, self.player["pos"])

    def start_chase(self, guard):
        """Un guardia empieza a perseguir y avisa a los que están cerca"""
        if guard.state != "chase":
            self.detections += 1
            self.add_event("detected", "¡Te vieron! Rompe la línea de visión.")
        guard.state = "chase"
        guard.awareness = 100.0
        guard.repath = 0.0
        self.last_known = self.player["pos"]
        self.call_nearby(guard)

    def spotted(self, guard):
        """El guardia de la canoa te ve: no se mueve, pero avisa a los demás"""
        self.detections += 1
        self.last_known = self.player["pos"]
        self.add_event("detected", "¡El guardia de la canoa te vio!")
        self.call_nearby(guard)

    def capture(self, message):
        self.over = True
        self.result = "lose"
        self.add_event("captured", message)

    def trigger_alarm(self, reason, pos):
        """Suena la alarma: empieza la fase 2 y llegan refuerzos"""
        if self.phase == 2:
            return
        self.phase = 2
        self.suspicion = 0.0
        self.last_known = pos
        self.add_event("alarm", reason + " Tu disfraz ya no sirve: ¡huye!")
        for g in self.guards():
            if g.is_canoe_guard():
                continue          # el guardia de la canoa no deja su puesto
            g.awareness = 0.0
            if g.seeing and dist(g.pos, self.player["pos"]) < 260:
                self.start_chase(g)
            elif dist(g.pos, pos) < 450:
                g.search(9.0, self.nav.random_near(pos, 70, self.random))
        # refuerzos que salen de las puertas de los almacenes
        for spawn in REINFORCEMENT_SPAWNS:
            g = Guard(self, spawn)
            g.search(SEARCH_TIME, self.nav.random_near(pos, 80, self.random))

    def update_suspicion(self, dt):
        """Fase 1: la sospecha sube si alguien te ve y baja poco a poco si no"""
        if self.suspicion_gain > 0:
            self.suspicion = min(SUSPICION_MAX, self.suspicion + self.suspicion_gain * dt)
        else:
            decay = SUSPICION_DECAY - SUSPICION_DECAY_PER_ALERT * self.alert
            self.suspicion = max(0.0, self.suspicion - decay * dt)
        if self.suspicion >= SUSPICION_MAX:
            self.trigger_alarm("¡Te descubrieron!", self.player["pos"])

    # ---------- paso ----------
    def step(self):
        self.events = []
        for _ in range(self.substeps):
            if self.over:
                break
            self.sim_time += self.substep_dt
            self.suspicion_gain = 0.0
            self.agents_by_type[Guard].shuffle_do("step")
            if self.over:
                break
            if self.phase == 1:
                self.update_suspicion(self.substep_dt)

    def state(self):
        """Estado que se manda al cliente. Claves en camelCase para JsonUtility."""
        return {
            "tick": self.steps,
            "simTime": round(self.sim_time, 3),
            "phase": self.phase,
            "alert": self.alert,
            "suspicion": round(self.suspicion, 1),
            "detections": self.detections,
            "over": self.over,
            "result": self.result,
            "events": self.events,
            "guards": [g.to_dict() for g in self.guards()],
        }
