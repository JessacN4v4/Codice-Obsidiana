# Modelo de la misión (Mesa).
#
# Cada step() del cliente avanza dt segundos (0.1 s a 10 Hz). Para que los
# guardias rápidos no se "salten" al jugador, el step se divide en subpasos
# de 0.05 s.
#
# Fase 1 (infiltración): el jugador va disfrazado y debe quemar los tres
# trojes. Los guardias que lo ven suman a una sospecha global; si llega a 100
# suena la alarma. Cada troje quemado sube la alerta del pueblo (0 a 3).
# Fase 2 (huida): empieza 2.5 s después del tercer troje (o con la alarma).
# Cada guardia tiene su propia alerta y puede perseguir y atrapar al jugador.
# Para ganar hay que subir a la canoa con los tres trojes quemados, después de
# alejar con una piedra al guardia que la vigila.

import mesa

from app.agents import Guard, Villager
from app.geometry import dist
from app.map_data import (
    CANOE, CARRIER_SPAWNS, GUARD_SPAWNS, PLAZA_CENTER, REINFORCEMENT_SPAWNS, START, TROJES,
    get_zone,
)
from app.navigation import NavGrid
from app.params import (
    ALARM_DELAY, BURN_DECAY, BURN_REACH, BURN_TIME, CANOE_BOARD_RADIUS, CANOE_MESSAGE_COOLDOWN,
    CROWD_RADIUS, FIRE_INVESTIGATE_RADIUS, FIRE_NOISE_RADIUS, RUN_NOISE_INTERVAL,
    RUN_NOISE_RADIUS, SEARCH_CALL_RADIUS, SEARCH_TIME, SIM_SUBSTEP, STONE_FLIGHT_TIME,
    STONE_NOISE_RADIUS, STONE_RANGE, STONES, SUSPICION_DECAY, SUSPICION_DECAY_PER_ALERT,
    SUSPICION_MAX, VILLAGER_COUNT, VILLAGER_SPAWN_RADIUS,
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
        self.alarm_timer = -1.0    # cuenta regresiva a la alarma tras el tercer troje
        self.last_known = None     # última posición donde vieron al jugador
        self.detections = 0        # veces que lo descubrieron
        self.over = False          # ¿terminó la partida?
        self.result = None         # "win" o "lose"
        self.events = []           # avisos para Unity en este step

        # Trojes: uno por almacén
        self.trojes = []
        for t in TROJES:
            self.trojes.append({"id": t["id"], "zone": t["zone"], "center": t["center"],
                                "burning": False, "progress": 0.0})
        self.burned = 0

        # Piedras
        self.stones = STONES
        self.stones_used = 0
        self.flying = []           # piedras en el aire: {"to": (x, y), "t": 0 a 1}

        # El jugador lo mueve Unity; aquí solo se guarda lo que manda
        self.player = {"pos": START, "running": False, "lighting": None}
        self.noise_t = 0.0         # fase 2: cada cuánto hace ruido al correr
        self.canoe_message_t = 0.0
        # Aldeanos cerca del jugador
        self.crowd = 0
        self.crowd_carriers = 0

        for spawn in GUARD_SPAWNS:
            Guard(self, spawn)
        # aldeanos repartidos alrededor de la plaza
        for _ in range(VILLAGER_COUNT):
            pos = self.nav.random_near(PLAZA_CENTER, VILLAGER_SPAWN_RADIUS, self.random)
            Villager(self, pos)
        # cargadores
        for c in CARRIER_SPAWNS:
            Villager(self, c["pos"], carrier=True, route=c["route"], ri=c["ri"])

    # ---------- consultas ----------
    def guards(self):
        """Guardias ordenados por id"""
        return sorted(self.agents_by_type[Guard], key=lambda g: g.unique_id)

    def villagers(self):
        """Aldeanos y cargadores ordenados por id"""
        return sorted(self.agents_by_type[Villager], key=lambda v: v.unique_id)

    def update_crowd(self):
        """Cuántos aldeanos (y cuántos cargadores) hay junto al jugador"""
        self.crowd = 0
        self.crowd_carriers = 0
        for v in self.villagers():
            if dist(v.pos, self.player["pos"]) < CROWD_RADIUS:
                self.crowd += 1
                if v.carrier:
                    self.crowd_carriers += 1

    def canoe_guard(self):
        for g in self.guards():
            if g.is_canoe_guard():
                return g
        return None

    def get_troje(self, troje_id):
        for t in self.trojes:
            if t["id"] == troje_id:
                return t
        return None

    def add_event(self, kind, message):
        self.events.append({"type": kind, "message": message})

    # ---------- lo que manda el cliente ----------
    def set_player(self, x, y, running=False, lighting=None):
        """Posición del jugador. lighting = id del troje que está quemando (o None)"""
        self.player = {"pos": (x, y), "running": running, "lighting": lighting}

    def throw_stone(self, x, y):
        """Lanza una piedra hacia (x, y). Si está muy lejos, cae a 260 px del jugador"""
        if self.over:
            return
        if self.stones <= 0:
            self.add_event("no_stones", "No te quedan piedras.")
            return
        p = self.player["pos"]
        d = dist(p, (x, y))
        if d > STONE_RANGE:
            x = p[0] + (x - p[0]) / d * STONE_RANGE
            y = p[1] + (y - p[1]) / d * STONE_RANGE
        self.stones -= 1
        self.stones_used += 1
        self.flying.append({"to": (x, y), "t": 0.0})

    # ---------- trojes ----------
    def update_lighting(self, dt):
        """Mantener E cerca de un troje lo quema en 1.3 s; si sueltas, el progreso baja"""
        lit = None
        troje = self.get_troje(self.player["lighting"])
        if troje is not None and not troje["burning"]:
            if dist(self.player["pos"], troje["center"]) < BURN_REACH:
                lit = troje
        # si no está lo bastante cerca, no cuenta como quemar
        self.player["lighting"] = lit["id"] if lit is not None else None

        if lit is not None:
            lit["progress"] += dt / BURN_TIME
            if lit["progress"] >= 1:
                self.ignite(lit)
        for t in self.trojes:
            if t is not lit and not t["burning"]:
                t["progress"] = max(0.0, t["progress"] - dt * BURN_DECAY)

    def ignite(self, troje):
        troje["burning"] = True
        troje["progress"] = 1.0
        self.burned += 1
        self.player["lighting"] = None
        name = get_zone(troje["zone"])["name"]
        if self.phase == 1:
            self.alert = self.burned
            if self.burned == 3:
                self.alarm_timer = ALARM_DELAY
                self.add_event("fire", "¡Los tres trojes arden! Sal de ahí.")
            else:
                if self.burned == 1:
                    mood = "inquieto"
                else:
                    mood = "alarmado"
                self.add_event("fire", f"{name} en llamas ({self.burned}/3). El pueblo está {mood}.")
                # los guardias cercanos vienen a ver el fuego
                c = troje["center"]
                for g in self.guards():
                    if g.state == "chase" or g.is_canoe_guard():
                        continue
                    if dist(g.pos, c) < FIRE_INVESTIGATE_RADIUS:
                        g.investigate(self.nav.random_near((c[0], c[1] + 60), 50, self.random))
        else:
            self.noise(troje["center"], FIRE_NOISE_RADIUS)
            if self.burned == 3:
                self.add_event("fire", "¡Los tres trojes arden! Huye a la canoa.")
            else:
                self.add_event("fire", f"{name} en llamas ({self.burned}/3).")

    # ---------- piedras y ruido ----------
    def update_stones(self, dt):
        """Las piedras tardan 0.45 s en caer; al caer hacen ruido"""
        still_flying = []
        for s in self.flying:
            s["t"] += dt / STONE_FLIGHT_TIME
            if s["t"] >= 1:
                self.noise(s["to"], STONE_NOISE_RADIUS, from_stone=True)
                self.add_event("stone", "La piedra cayó.")
            else:
                still_flying.append(s)
        self.flying = still_flying

    def update_run_noise(self, dt):
        """Fase 2: correr hace ruido cada 0.4 s"""
        if self.phase == 2 and self.player["running"]:
            self.noise_t -= dt
            if self.noise_t <= 0:
                self.noise_t = RUN_NOISE_INTERVAL
                self.noise(self.player["pos"], RUN_NOISE_RADIUS)

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

    # ---------- reacciones ----------
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
        self.alarm_timer = -1.0
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

    def update_alarm_timer(self, dt):
        """Fase 1: la alarma suena 2.5 s después de quemar el tercer troje"""
        if self.alarm_timer > 0:
            self.alarm_timer -= dt
            if self.alarm_timer <= 0:
                self.trigger_alarm("¡Fuego en los tres almacenes!", self.player["pos"])

    # ---------- canoa ----------
    def check_canoe(self, dt):
        """Subir a la canoa: con el guardia ahí te atrapa; sin él y con 3 trojes, ganas"""
        self.canoe_message_t -= dt
        if dist(self.player["pos"], CANOE) >= CANOE_BOARD_RADIUS:
            return
        guard = self.canoe_guard()
        if guard is not None and dist(guard.pos, CANOE) < CANOE_BOARD_RADIUS:
            self.capture("El guardia de la canoa te atrapó. Distráelo con una piedra.")
        elif self.burned == 3:
            self.over = True
            self.result = "win"
            self.add_event("win", "¡Escapaste en la canoa!")
        elif self.canoe_message_t <= 0:
            self.add_event("info", f"Aún quedan trojes por quemar ({self.burned}/3).")
            self.canoe_message_t = CANOE_MESSAGE_COOLDOWN

    # ---------- paso ----------
    def step(self):
        self.events = []
        for _ in range(self.substeps):
            if self.over:
                break
            dt = self.substep_dt
            self.sim_time += dt
            self.update_lighting(dt)
            self.update_crowd()
            self.update_run_noise(dt)

            self.suspicion_gain = 0.0
            self.agents_by_type[Guard].shuffle_do("step")
            if self.over:
                break

            if self.phase == 1:
                self.update_suspicion(dt)
                self.update_alarm_timer(dt)
            self.agents_by_type[Villager].shuffle_do("step")
            self.update_stones(dt)
            self.check_canoe(dt)

    def state(self):
        """Estado que se manda al cliente. Claves en camelCase para JsonUtility."""
        trojes = []
        for t in self.trojes:
            trojes.append({"id": t["id"], "burning": t["burning"],
                           "progress": round(t["progress"], 2)})
        return {
            "tick": self.steps,
            "simTime": round(self.sim_time, 3),
            "phase": self.phase,
            "alert": self.alert,
            "suspicion": round(self.suspicion, 1),
            "burned": self.burned,
            "stones": self.stones,
            "detections": self.detections,
            "over": self.over,
            "result": self.result,
            "events": self.events,
            "trojes": trojes,
            "guards": [g.to_dict() for g in self.guards()],
            "villagers": [v.to_dict() for v in self.villagers()],
        }
