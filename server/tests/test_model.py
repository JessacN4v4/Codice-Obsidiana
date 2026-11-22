import pytest

from app.model import MissionModel


def test_modelo_nuevo_empieza_en_cero():
    model = MissionModel()
    assert model.state() == {"tick": 0, "simTime": 0.0}


def test_step_avanza_tick_y_tiempo():
    model = MissionModel(dt=0.1)
    for _ in range(5):
        model.step()
    assert model.state()["tick"] == 5
    assert model.sim_time == pytest.approx(0.5)


def test_misma_semilla_mismos_numeros():
    a = MissionModel(rng=42)
    b = MissionModel(rng=42)
    assert [a.random.random() for _ in range(5)] == [b.random.random() for _ in range(5)]
