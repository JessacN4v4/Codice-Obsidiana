# Modelo de la misión (Mesa).
#
# Cada step() del cliente avanza dt segundos (0.1 s a 10 Hz). Para que los
# guardias rápidos no se "salten" al jugador, el step se divide en subpasos
# de 0.05 s.

import mesa

from app.agents import Guard
from app.map_data import GUARD_SPAWNS
from app.navigation import NavGrid
from app.params import SIM_SUBSTEP

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
        self.phase = 1        # 1 = infiltración, 2 = huida
        self.alert = 0        # alerta del pueblo (0 a 3)

        for spawn in GUARD_SPAWNS:
            Guard(self, spawn)

    def guards(self):
        """Guardias ordenados por id"""
        return sorted(self.agents_by_type[Guard], key=lambda g: g.unique_id)

    def step(self):
        for _ in range(self.substeps):
            self.sim_time += self.substep_dt
            self.agents_by_type[Guard].shuffle_do("step")

    def state(self):
        """Estado que se manda al cliente. Claves en camelCase para JsonUtility."""
        return {
            "tick": self.steps,
            "simTime": round(self.sim_time, 3),
            "phase": self.phase,
            "guards": [g.to_dict() for g in self.guards()],
        }
