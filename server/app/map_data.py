# Mapa de la misión "Los trojes del pueblo fronterizo".
#
# Mundo de 2000 x 1300 px, x a la derecha, y hacia abajo.
# Rectángulos: (x, y, w, h). Puntos: (x, y).

import math

from app.geometry import in_rect, rect_center

WORLD_W = 2000
WORLD_H = 1300

# Ángulos (y hacia abajo)
FACE_EAST = 0.0
FACE_SOUTH = math.pi / 2
FACE_WEST = math.pi
FACE_NORTH = -math.pi / 2


def shore_x(y):
    """Orilla del lago (oeste): todo lo que esté a la izquierda es agua"""
    return 235 + 28 * math.sin(y / 95) + 12 * math.sin(y / 37)


def is_water(p):
    return p[0] < shore_x(p[1])


# ---------- Almacenes ----------
# mult: multiplicador de sospecha. gate: la puerta también es zona restringida.
ZONES = [
    {"id": "A", "name": "Almacén del mercado", "level": "baja", "mult": 0.45,
        "rect": (1532, 872, 236, 196), "gate": (1606, 848, 88, 30)},
    {"id": "B", "name": "Almacén de flechas", "level": "media", "mult": 1.0,
        "rect": (772, 912, 276, 236), "gate": (876, 888, 68, 30)},
    {"id": "C", "name": "Almacén de la guarnición", "level": "alta", "mult": 1.8,
        "rect": (432, 122, 336, 296), "gate": (566, 405, 68, 30)},
]


def get_zone(zone_id):
    for z in ZONES:
        if z["id"] == zone_id:
            return z
    return None


def zone_at(p):
    """Almacén en el que está el punto, o None si es zona pública"""
    for z in ZONES:
        if in_rect(p, z["rect"]) or in_rect(p, z["gate"]):
            return z
    return None


# ---------- Obstáculos ----------
# kind: "wall" (empalizada), "house", "troje", "yacata", "stall" (puesto del mercado)
# sight: True si tapa la vista, los puestos son bajos y no tapan.
OBSTACLES = []


def add_obstacle(x, y, w, h, kind, sight=True):
    OBSTACLES.append({"rect": (x, y, w, h), "kind": kind, "sight": sight})


# C · guarnición (noroeste): brecha al oeste, puerta al sur
add_obstacle(420, 110, 360, 12, "wall")
add_obstacle(420, 110, 12, 140, "wall")
add_obstacle(420, 290, 12, 140, "wall")
add_obstacle(768, 110, 12, 320, "wall")
add_obstacle(420, 418, 150, 12, "wall")
add_obstacle(630, 418, 150, 12, "wall")
add_obstacle(500, 300, 80, 55, "house")
add_obstacle(650, 300, 80, 50, "house")
# B · flechas (sur): puerta al norte, brecha al este
add_obstacle(760, 900, 120, 12, "wall")
add_obstacle(940, 900, 120, 12, "wall")
add_obstacle(760, 900, 12, 260, "wall")
add_obstacle(1048, 900, 12, 110, "wall")
add_obstacle(1048, 1050, 12, 110, "wall")
add_obstacle(760, 1148, 300, 12, "wall")
add_obstacle(930, 1060, 80, 50, "house")
# A · mercado (sureste): puerta ancha al norte
add_obstacle(1520, 860, 90, 12, "wall")
add_obstacle(1690, 860, 90, 12, "wall")
add_obstacle(1520, 860, 12, 220, "wall")
add_obstacle(1768, 860, 12, 220, "wall")
add_obstacle(1520, 1068, 260, 12, "wall")
add_obstacle(1550, 990, 60, 45, "house")

# Trojes: uno por almacén (orden sugerido A -> B -> C). También son obstáculos.
TROJES = [
    {"id": "A", "zone": "A", "rect": (1680, 960, 48, 48)},
    {"id": "B", "zone": "B", "rect": (790, 930, 48, 48)},
    {"id": "C", "zone": "C", "rect": (600, 170, 48, 48)},
]
for t in TROJES:
    t["center"] = rect_center(t["rect"])
    x, y, w, h = t["rect"]
    add_obstacle(x, y, w, h, "troje")

# Yácata
add_obstacle(1110, 335, 180, 90, "yacata")
add_obstacle(1150, 420, 100, 80, "yacata")

# Casas del pueblo
HOUSES = [
    (1020, 585, 70, 55), (1360, 570, 80, 60), (1380, 820, 70, 60), (1010, 830, 75, 55),
    (1560, 600, 70, 55), (1700, 480, 70, 60), (820, 650, 70, 60), (640, 760, 70, 55),
    (480, 560, 60, 50), (1300, 1150, 70, 55), (1850, 950, 70, 60), (1500, 250, 70, 60),
    (1800, 230, 70, 60), (900, 250, 70, 55), (1100, 1150, 70, 55), (560, 1000, 70, 55),
    (350, 690, 60, 50), (1250, 990, 70, 55),
]
for (x, y, w, h) in HOUSES:
    add_obstacle(x, y, w, h, "house")

# Puestos del mercado (bajos: no tapan la vista)
for (x, y, w, h) in [(1150, 650, 40, 22), (1240, 690, 40, 22), (1100, 715, 40, 22)]:
    add_obstacle(x, y, w, h, "stall", sight=False)

# ---------- Maizales, plaza, canoa, inicio ----------
# Maizales pequeños y separados: ocultan pero no bloquean
MAIZE = [
    (300, 180, 90, 110), (300, 430, 85, 90), (310, 770, 80, 100), (470, 1100, 110, 80),
    (1080, 985, 80, 95), (620, 900, 90, 90), (1450, 120, 120, 100), (1820, 560, 90, 110),
    (780, 480, 90, 80), (1600, 1150, 110, 80),
]


def in_maize(p):
    for m in MAIZE:
        if in_rect(p, m):
            return True
    return False


PLAZA_CENTER = (1200, 700)
PLAZA_RADIUS = 160

# ---------- Caminos de tierra (solo decorativos: no cambian la jugabilidad) ----------
# points: recorrido del camino. width: ancho en px.
ROADS = [
    {"points": [(2000, 700), (1600, 690), (1360, 700)], "width": 40},                  # entrada este
    {"points": [(1045, 700), (800, 590), (600, 470), (600, 430)], "width": 34},       # plaza -> almacén C
    {"points": [(1300, 770), (1500, 800), (1650, 845)], "width": 32},                 # plaza -> almacén A
    {"points": [(1150, 850), (1000, 870), (910, 885)], "width": 30},                  # plaza -> almacén B
    {"points": [(760, 870), (560, 960), (420, 1100), (272, 1175)], "width": 30},      # almacén B -> canoa
]

# ---------- Cargadores ----------
# Van de la plaza al almacén y regresan. El punto 2 de cada ruta está dentro del almacén.
CARRIER_ROUTE_A = [(1300, 780), (1650, 830), (1600, 940), (1650, 830)]
CARRIER_ROUTE_B = [(1150, 860), (910, 870), (900, 1000), (910, 870)]
# pos: dónde empieza. ri: índice del punto de ruta en el que empieza.
CARRIER_SPAWNS = [
    {"pos": (1300, 780), "ri": 0, "route": CARRIER_ROUTE_A},
    {"pos": (1600, 940), "ri": 2, "route": CARRIER_ROUTE_A},
    {"pos": (1480, 800), "ri": 1, "route": CARRIER_ROUTE_A},
    {"pos": (1150, 860), "ri": 0, "route": CARRIER_ROUTE_B},
]
CANOE = (shore_x(1180) + 16, 1180)
START = (1940, 700)

# ---------- Guardias ----------
# Rol = lo que hace el guardia en reposo. Los tres comparten los estados de
# reacción (investigar, buscar, perseguir, regresar).
SENTRY = "sentry"                    # centinela: quieto en su puesto, solo gira
PATROL_OUTSIDE = "patrol_outside"    # patrulla por el pueblo
PATROL_INSIDE = "patrol_inside"      # patrulla dentro de un almacén
CANOE_GUARD = "canoe_guard"          # guardia de la canoa: no se mueve y gira muy poco;
                                     # solo una piedra lo hace ir a ver

# zone: almacén al que pertenece (None = pueblo). face: ángulo base del centinela.
# route: puntos de la ruta cíclica de las patrullas.
GUARD_SPAWNS = [
    # C · guarnición (vigilancia alta): centinela en la puerta sur + 2 patrullas interiores
    {"role": SENTRY, "pos": (600, 455), "zone": "C", "face": FACE_SOUTH, "route": []},
    {"role": PATROL_INSIDE, "pos": (470, 240), "zone": "C", "face": 0.0,
        "route": [(470, 240), (720, 240), (720, 385), (480, 390)]},
    {"role": PATROL_INSIDE, "pos": (560, 150), "zone": "C", "face": 0.0,
        "route": [(560, 150), (740, 150), (560, 260)]},
    # B · flechas (vigilancia media): centinela en la puerta norte + 1 patrulla interior
    {"role": SENTRY, "pos": (910, 880), "zone": "B", "face": FACE_NORTH, "route": []},
    {"role": PATROL_INSIDE, "pos": (790, 1040), "zone": "B", "face": 0.0,
        "route": [(790, 1040), (1010, 940), (1030, 1130)]},
    # A · mercado (vigilancia baja): solo un centinela en la puerta
    {"role": SENTRY, "pos": (1650, 840), "zone": "A", "face": FACE_NORTH, "route": []},
    # Pueblo: patrullas exteriores
    {"role": PATROL_OUTSIDE, "pos": (1200, 540), "zone": None, "face": 0.0,
        "route": [(1050, 760), (1200, 540), (1350, 760), (1200, 880)]},
    {"role": PATROL_OUTSIDE, "pos": (1450, 700), "zone": None, "face": 0.0,
        "route": [(1450, 700), (1850, 720)]},
    {"role": PATROL_OUTSIDE, "pos": (1500, 420), "zone": None, "face": 0.0,
        "route": [(1500, 420), (1000, 420)]},
    {"role": PATROL_OUTSIDE, "pos": (820, 600), "zone": None, "face": 0.0,
        "route": [(820, 600), (700, 860), (560, 700)]},
    {"role": PATROL_OUTSIDE, "pos": (330, 1080), "zone": None, "face": 0.0,
        "route": [(330, 1080), (600, 1160), (500, 1050)]},
    # Canoa de escape: guardia arriba de la canoa, mirando hacia tierra (por donde llegas)
    {"role": CANOE_GUARD, "pos": CANOE, "zone": None, "face": FACE_EAST, "route": []},
]

def rect_as_dict(r):
    return {"x": r[0], "y": r[1], "w": r[2], "h": r[3]}


def map_as_dict():
    """Todo el mapa en un diccionario (claves en camelCase) para mandarlo a Unity"""
    obstacles = []
    for o in OBSTACLES:
        if o["kind"] == "troje":
            continue                       # los trojes van aparte, con su id
        obstacles.append({"kind": o["kind"], "sight": o["sight"], "rect": rect_as_dict(o["rect"])})
    zones = []
    for z in ZONES:
        zones.append({"id": z["id"], "name": z["name"], "level": z["level"], "mult": z["mult"],
                      "rect": rect_as_dict(z["rect"]), "gate": rect_as_dict(z["gate"])})
    trojes = []
    for t in TROJES:
        trojes.append({"id": t["id"], "zone": t["zone"], "rect": rect_as_dict(t["rect"])})
    roads = []
    for road in ROADS:
        points = [{"x": p[0], "y": p[1]} for p in road["points"]]
        roads.append({"points": points, "width": road["width"]})
    # orilla del lago: un punto cada 20 px
    shore = []
    for y in range(0, WORLD_H + 1, 20):
        shore.append({"x": round(shore_x(y), 1), "y": y})
    return {
        "worldW": WORLD_W,
        "worldH": WORLD_H,
        "obstacles": obstacles,
        "zones": zones,
        "trojes": trojes,
        "maize": [rect_as_dict(m) for m in MAIZE],
        "plaza": {"x": PLAZA_CENTER[0], "y": PLAZA_CENTER[1], "r": PLAZA_RADIUS},
        "canoe": {"x": round(CANOE[0], 1), "y": CANOE[1]},
        "start": {"x": START[0], "y": START[1]},
        "shore": shore,
        "roads": roads,
    }


# Refuerzos que aparecen al sonar la alarma (fase 2): salen de las puertas
REINFORCEMENT_SPAWNS = [
    {"role": PATROL_OUTSIDE, "pos": (600, 470), "zone": None, "face": 0.0,
        "route": [(420, 950), (650, 1190)]},
    {"role": PATROL_OUTSIDE, "pos": (910, 860), "zone": None, "face": 0.0,
        "route": [(900, 700), (600, 860)]},
    {"role": PATROL_OUTSIDE, "pos": (1650, 820), "zone": None, "face": 0.0,
        "route": [(1300, 1060), (1000, 1220)]},
]
