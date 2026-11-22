# Visión de los guardias: alcance, cono de visión y línea de vista.
#
# Un guardia ve un punto si:
#   1. está a menos de R px,
#   2. está dentro de su cono (±0.72 rad respecto a hacia donde mira),
#      salvo que esté muy cerca (< 28 px): ahí lo ve en cualquier dirección,
#   3. ningún obstáculo que tape la vista está en medio.
# Los puestos del mercado y los maizales no tapan la vista.

import math

from app.geometry import ang_diff, dist, seg_rect
from app.map_data import OBSTACLES
from app.params import (
    FOV_HALF_ANGLE, MAIZE_REVEAL_RADIUS, NEAR_SENSE_RADIUS, VISION_RANGE_PER_ALERT,
    VISION_RANGE_PHASE1, VISION_RANGE_PHASE2,
)


def vision_range(phase, alert):
    """Alcance de la vista: crece con la alerta del pueblo en fase 1; fijo en fase 2"""
    if phase == 1:
        return VISION_RANGE_PHASE1 + VISION_RANGE_PER_ALERT * alert
    return VISION_RANGE_PHASE2


def ray_hit(a, b, obstacles=OBSTACLES):
    """Fracción (0 a 1) del rayo a -> b hasta el primer obstáculo que tapa la vista"""
    best = 1.0
    for o in obstacles:
        if not o["sight"]:
            continue
        t = seg_rect(a, b, o["rect"])
        if t is not None and t < best:
            best = t
    return best


def line_of_sight(a, b):
    """¿Nada tapa la vista entre a y b?"""
    return ray_hit(a, b) >= 0.999


def in_cone(pos, face, target):
    """¿target está dentro del cono de visión de alguien en pos que mira hacia face?"""
    angle = math.atan2(target[1] - pos[1], target[0] - pos[0])
    return abs(ang_diff(face, angle)) <= FOV_HALF_ANGLE


def sees(pos, face, target, R):
    """¿Un guardia en pos, mirando hacia face, ve el punto target con alcance R?"""
    d = dist(pos, target)
    if d > R:
        return False
    if d > NEAR_SENSE_RADIUS and not in_cone(pos, face, target):
        return False
    return line_of_sight(pos, target)


def sees_player(pos, face, player, R, player_in_maize, player_running):
    """Como sees, pero en un maizal solo te ven si corres o están muy cerca"""
    if not sees(pos, face, player, R):
        return False
    if player_in_maize and not player_running and dist(pos, player) > MAIZE_REVEAL_RADIUS:
        return False
    return True
