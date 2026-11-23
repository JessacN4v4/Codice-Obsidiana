using System;

// Clases para el JSON de POST /sessions y POST /sessions/{id}/step.
// Los nombres de los campos deben ser iguales a las claves del JSON del servidor.

// ---------- lo que responde el servidor ----------

[Serializable]
public class EventoDatos
{
    public string type;       // "detected", "lost", "alarm", "captured", "fire", "stone", "win", "info"...
    public string message;    // texto en español para mostrar al jugador
}

[Serializable]
public class TrojeEstado
{
    public string id;         // "A", "B", "C"
    public bool burning;
    public float progress;    // 0 a 1 mientras lo quemas
}

[Serializable]
public class GuardiaDatos
{
    public int id;
    public string role;       // "sentry", "patrol_outside", "patrol_inside", "canoe_guard"
    public string state;      // "stand", "patrol", "investigate", "chase", "search", "return"
    public float x;
    public float y;
    public float face;        // ángulo en radianes (0 = este, +pi/2 = sur)
    public float awareness;   // alerta individual en fase 2 (0 a 100)
    public bool seeing;       // ¿ve al jugador en este momento?
}

[Serializable]
public class AldeanoDatos
{
    public int id;
    public bool carrier;      // ¿es cargador?
    public float x;
    public float y;
    public float face;
}

[Serializable]
public class EstadoDatos
{
    public string sessionId;
    public int tick;
    public float simTime;
    public int phase;         // 1 = infiltración, 2 = huida
    public int alert;         // alerta del pueblo (0 a 3)
    public float suspicion;   // sospecha global (0 a 100)
    public int burned;        // trojes quemados
    public int stones;        // piedras que quedan
    public int detections;
    public bool over;
    public string result;     // "win", "lose" o vacío
    public EventoDatos[] events;
    public TrojeEstado[] trojes;
    public GuardiaDatos[] guards;
    public AldeanoDatos[] villagers;
}

// ---------- lo que manda Unity ----------

[Serializable]
public class JugadorDatos
{
    public float x;
    public float y;
    public bool running;
    public string lighting;   // id del troje que está quemando; vacío si no quema nada
}

[Serializable]
public class PuntoPiedra
{
    public float x;
    public float y;
}

[Serializable]
public class PasoPeticion
{
    public int steps;
    public JugadorDatos player;
}

// JsonUtility siempre escribe todos los campos, así que la piedra va en una clase aparte:
// si estuviera en PasoPeticion, el servidor recibiría una piedra en (0, 0) en cada paso.
[Serializable]
public class PasoConPiedraPeticion
{
    public int steps;
    public JugadorDatos player;
    public PuntoPiedra stone;
}
