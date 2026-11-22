from fastapi.testclient import TestClient

from app.main import app

client = TestClient(app)


def test_health():
    response = client.get("/health")
    assert response.status_code == 200
    assert response.json() == {"status": "ok"}


def test_crear_sesion_y_avanzar():
    created = client.post("/sessions", json={"seed": 1})
    assert created.status_code == 201
    session_id = created.json()["sessionId"]
    assert created.json()["tick"] == 0

    stepped = client.post(f"/sessions/{session_id}/step", json={"steps": 3})
    assert stepped.status_code == 200
    assert stepped.json()["tick"] == 3


def test_sesion_inexistente_da_404():
    response = client.post("/sessions/no-existe/step", json={"steps": 1})
    assert response.status_code == 404


def test_steps_fuera_de_rango_da_422():
    session_id = client.post("/sessions").json()["sessionId"]
    response = client.post(f"/sessions/{session_id}/step", json={"steps": 11})
    assert response.status_code == 422
