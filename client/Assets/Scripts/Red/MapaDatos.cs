using System;

// Clases para leer con JsonUtility el JSON de GET /map del servidor.
// Los nombres de los campos deben ser iguales a las claves del JSON.

[Serializable]
public class RectDatos
{
    public float x;
    public float y;
    public float w;
    public float h;
}

[Serializable]
public class PuntoDatos
{
    public float x;
    public float y;
}

[Serializable]
public class ObstaculoDatos
{
    public string kind;     // "wall", "house", "yacata", "stall"
    public bool sight;      // ¿tapa la vista?
    public RectDatos rect;
}

[Serializable]
public class ZonaDatos
{
    public string id;       // "A", "B", "C"
    public string name;
    public string level;    // vigilancia: "baja", "media", "alta"
    public float mult;
    public RectDatos rect;  // interior del almacén
    public RectDatos gate;  // puerta
}

[Serializable]
public class TrojeDatos
{
    public string id;
    public string zone;
    public RectDatos rect;
}

[Serializable]
public class PlazaDatos
{
    public float x;
    public float y;
    public float r;
}

[Serializable]
public class CaminoDatos
{
    public PuntoDatos[] points;   // recorrido del camino
    public float width;           // ancho en px
}

[Serializable]
public class MapaDatos
{
    public float worldW;
    public float worldH;
    public ObstaculoDatos[] obstacles;
    public ZonaDatos[] zones;
    public TrojeDatos[] trojes;
    public RectDatos[] maize;
    public PlazaDatos plaza;
    public PuntoDatos canoe;
    public PuntoDatos start;
    public PuntoDatos[] shore;   // orilla del lago, un punto cada 20 px
    public CaminoDatos[] roads;  // caminos de tierra (decorativos)
}
