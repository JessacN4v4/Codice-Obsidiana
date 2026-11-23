# Parámetros de diseño
#
# Unidades: píxeles del mapa (px), segundos (s) y radianes (rad).

# ---------- Jugador (lo mueve Unity; aquí solo para referencia y pruebas) ----------
PLAYER_WALK_SPEED = 112.0
PLAYER_RUN_SPEED = 185.0
AGENT_RADIUS = 10.0          # radio de colisión de personas
BURN_TIME = 1.3              # mantener E para quemar un troje
BURN_REACH = 54.0            # distancia máxima al centro del troje
BURN_DECAY = 1.5             # si sueltas E, el progreso baja 1.5 por segundo

# ---------- Visión ----------
FOV_HALF_ANGLE = 0.72        # medio ángulo del cono (~41°, cono total ~82°)
NEAR_SENSE_RADIUS = 28.0     # más cerca que esto ve en cualquier dirección
VISION_RANGE_PHASE1 = 185.0  # + VISION_RANGE_PER_ALERT * alerta
VISION_RANGE_PER_ALERT = 20.0
VISION_RANGE_PHASE2 = 250.0
MAIZE_REVEAL_RADIUS = 55.0   # en un maizal solo te ven si estás más cerca (o corres)

# ---------- Velocidades de los guardias por estado ----------
GUARD_SPEED = {
    "patrol": 70.0,
    "investigate": 95.0,
    "chase": 165.0,
    "search": 100.0,
    "return": 80.0,
}
GUARD_TURN_RATE = 7.0        # qué tan rápido gira hacia su objetivo (factor por s)

# ---------- Centinela: giro en su puesto ----------
SENTRY_SCAN_AMPLITUDE = 0.65  # rad a cada lado del ángulo base (~37°)
SENTRY_SCAN_FREQUENCY = 0.9   # rad/s dentro del seno

# ---------- Guardia de la canoa ----------
# Está arriba de la canoa y gira muy poco. Si subes a la canoa con él ahí, te captura:
# hay que alejarlo con una piedra para poder escapar.
CANOE_SCAN_AMPLITUDE = 0.25   # rad a cada lado (~14°), giro muy leve
CANOE_SCAN_FREQUENCY = 0.6
CANOE_BOARD_RADIUS = 40.0     # a menos de esto del centro de la canoa ya estás "en la canoa"

# ---------- Investigar / buscar / perseguir ----------
INVESTIGATE_LOOK_TIME = 2.6   # segundos mirando alrededor al llegar
INVESTIGATE_LOOK_AMPLITUDE = 1.1
INVESTIGATE_LOOK_FREQUENCY = 2.2
SEARCH_TIME = 10.0
SEARCH_RADIUS = 150.0
SEARCH_PAUSE = 0.8
SEARCH_CALL_RADIUS = 380.0    # al perseguir, avisa a guardias a esta distancia
CAPTURE_RADIUS = 19.0

# ---------- Sospecha global (fase 1) ----------
SUSPICION_MAX = 100.0
SUSPICION_BASE_RATE = 50.0              # dentro de almacén
SUSPICION_DISTANCE_FALLOFF = 0.55
SUSPICION_ALERT_FACTOR = 0.4            # × (1 + 0.4 × alerta)
SUSPICION_CARRIER_MULT = 0.25           # junto a cargadores
SUSPICION_BURNING_MULT = 2.2            # mientras quema un troje
SUSPICION_RUNNING_MULT = 1.6
SUSPICION_PUBLIC_RUN_RATE = 14.0        # corriendo en zona pública...
SUSPICION_PUBLIC_RUN_RADIUS = 120.0     # ...a menos de 120 + 30 × alerta
SUSPICION_PUBLIC_RUN_PER_ALERT = 30.0
SUSPICION_PUBLIC_CLOSE_RATE = 8.0       # con alerta >= 2 y a menos de 80 px
SUSPICION_PUBLIC_CLOSE_RADIUS = 80.0
SUSPICION_DECAY = 12.0                  # - 2.5 × alerta por segundo
SUSPICION_DECAY_PER_ALERT = 2.5
SUSPICION_INVESTIGATE_THRESHOLD = 45.0  # dentro de almacén, el guardia va a ver

# ---------- Alerta individual (fase 2) ----------
AWARENESS_BASE_RATE = 70.0              # + 170 × (1 - d/R)
AWARENESS_DISTANCE_RATE = 170.0
AWARENESS_CROWD_MULT = 0.45             # entre >= 2 aldeanos (sin correr)
AWARENESS_RUNNING_MULT = 1.3
AWARENESS_DECAY = 22.0
AWARENESS_INVESTIGATE = 35.0
AWARENESS_CHASE = 100.0

# ---------- Aldeanos y cargadores ----------
VILLAGER_COUNT = 10
VILLAGER_RADIUS = 9.0
VILLAGER_SPEED = 28.0                   # 1.4 m/s: la velocidad natural de su animación de caminar
CARRIER_SPEED = 30.0                    # 1.5 m/s
VILLAGER_PHASE2_MULT = 1.25             # en la huida caminan más rápido
VILLAGER_SPAWN_RADIUS = 140.0           # aparecen alrededor de la plaza
VILLAGER_WANDER_RADIUS = 145.0          # y deambulan por ella
VILLAGER_WAIT_MIN = 1.0
VILLAGER_WAIT_MAX = 4.0
CARRIER_WAIT = 0.6
CARRIER_WAIT_AT_WAREHOUSE = 2.5         # se quedan más tiempo dentro del almacén
CROWD_RADIUS = 48.0                     # aldeanos a esta distancia del jugador son "multitud"

# ---------- Fuego, ruido y alarma ----------
FIRE_INVESTIGATE_RADIUS = 480.0         # fase 1: guardias a esta distancia van a ver el fuego
FIRE_NOISE_RADIUS = 260.0               # fase 2: quemar un troje hace ruido
ALARM_DELAY = 2.5                       # tras el tercer troje
CANOE_MESSAGE_COOLDOWN = 3.0            # cada cuánto repetir "aún quedan trojes"
STONE_NOISE_RADIUS = 230.0
RUN_NOISE_RADIUS = 115.0                # fase 2
RUN_NOISE_INTERVAL = 0.4

STONES = 3
STONE_RANGE = 260.0
STONE_FLIGHT_TIME = 0.45

# ---------- Simulación ----------
# El cliente pide un paso cada 0.1 s; el modelo lo divide en subpasos de 0.05 s
SIM_SUBSTEP = 0.05

# ---------- Navegación ----------
NAV_CELL = 20.0                         # tamaño de celda de la rejilla de A*
NAV_INFLATE = 11.0                      # margen alrededor de obstáculos
NAV_MAX_ITERATIONS = 6000
NAV_WALK_CLEARANCE = 12.0               # distancia mínima a un obstáculo al suavizar la ruta
NAV_WATER_MARGIN = 8.0
STUCK_TIME = 0.6                        # si casi no avanza este tiempo, recalcula la ruta

