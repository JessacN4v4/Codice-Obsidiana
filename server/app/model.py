"""Modelo de la misión (Mesa).

Por ahora está vacío: solo lleva la cuenta del tiempo simulado. Aquí irán
los guardias, los aldeanos, la sospecha y las fases de la misión.
"""

import mesa


class MissionModel(mesa.Model):
    """Simulación de una sesión de juego.

    - dt: segundos simulados por paso (el cliente llama a 10 Hz -> 0.1 s).
    - rng: semilla; con la misma semilla la simulación se repite igual.
    """

    def __init__(self, dt=0.1, rng=None):
        super().__init__(rng=rng)
        self.dt = dt
        # Tiempo simulado en segundos. No se llama "time" porque Mesa
        # ya usa ese nombre internamente.
        self.sim_time = 0.0

    def step(self):
        # Activa a todos los agentes en orden aleatorio (todavía no hay)
        self.agents.shuffle_do("step")
        self.sim_time += self.dt

    def state(self):
        """Estado que se manda al cliente. Claves en camelCase para JsonUtility."""
        return {
            "tick": self.steps,
            "simTime": round(self.sim_time, 3),
        }
