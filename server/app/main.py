"""API HTTP del servidor de simulación (FastAPI).

Endpoints:
- GET  /health                  -> comprobar que el servidor está vivo
- POST /sessions                -> crea una simulación nueva y regresa su id
- POST /sessions/{id}/step      -> avanza la simulación y regresa su estado
"""

from uuid import uuid4

from fastapi import FastAPI, HTTPException
from pydantic import BaseModel, Field

from app.model import MissionModel

app = FastAPI(title="Códice de Obsidiana — simulación")

# Sesiones activas en memoria: {id: modelo}. Se pierden al reiniciar.
sessions: dict[str, MissionModel] = {}


class CreateSessionRequest(BaseModel):
    seed: int | None = None


class PlayerInput(BaseModel):
    x: float
    y: float
    running: bool = False
    lighting: str | None = None     # id del troje que está quemando ("A", "B", "C")


class StepRequest(BaseModel):
    # Si el cliente se atrasa puede pedir varios pasos a la vez (máx. 10)
    steps: int = Field(default=1, ge=1, le=10)
    player: PlayerInput | None = None


@app.get("/health")
def health():
    return {"status": "ok"}


@app.post("/sessions", status_code=201)
def create_session(request: CreateSessionRequest | None = None):
    seed = request.seed if request else None
    session_id = uuid4().hex
    model = MissionModel(rng=seed)
    sessions[session_id] = model
    return {"sessionId": session_id, **model.state()}


@app.post("/sessions/{session_id}/step")
def step_session(session_id: str, request: StepRequest | None = None):
    model = sessions.get(session_id)
    if model is None:
        raise HTTPException(status_code=404, detail="Sesión no encontrada")
    steps = 1
    if request is not None:
        steps = request.steps
        if request.player is not None:
            p = request.player
            model.set_player(p.x, p.y, p.running, p.lighting)
    # se juntan los eventos de todos los pasos para no perder ninguno
    events = []
    for _ in range(steps):
        model.step()
        events = events + model.events
    state = model.state()
    state["events"] = events
    return {"sessionId": session_id, **state}
