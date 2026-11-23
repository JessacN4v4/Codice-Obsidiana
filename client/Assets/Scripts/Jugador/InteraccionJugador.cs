using UnityEngine;

/// <summary>
/// Acciones del jugador sobre el pueblo.
/// Quemar troje: cerca de un troje sin quemar, mantener E (o Espacio).
/// El servidor lleva el progreso (1.3 s) y decide cuándo arde; aquí solo se le avisa
/// qué troje se está quemando y se deja al jugador quieto mientras tanto.
/// Lanzar piedra: Q o clic derecho, hacia el punto del suelo donde apunta el mouse.
/// El ruido al caer lo calcula el servidor; aquí solo se dibuja la piedra y la onda.
/// </summary>
public class InteraccionJugador : MonoBehaviour
{
    public ClienteSimulacion cliente;
    public MovimientoJugador movimiento;

    [Header("Quemar trojes")]
    public float alcanceQuemar = 2.7f;         // metros (54 px del mapa, igual que el servidor)
    public KeyCode teclaQuemar = KeyCode.E;
    public KeyCode teclaQuemarAlterna = KeyCode.Space;

    [Header("Piedras")]
    public KeyCode teclaPiedra = KeyCode.Q;     // también clic derecho
    public float alcancePiedra = 13f;           // metros (260 px del mapa)
    public float radioRuido = 11.5f;            // metros (230 px del mapa)
    public Material materialPiedra;
    public Material materialRuido;              // transparente

    private string[] idsTrojes = { "A", "B", "C" };
    private Transform[] trojes;                // las cajas Troje_A, Troje_B y Troje_C del pueblo
    private string trojeCercano = "";          // id del troje que tiene a su alcance ("" = ninguno)
    private bool quemando = false;

    // Lo que lee el HUD
    public string TrojeCercano
    {
        get { return trojeCercano; }
    }
    public bool Quemando
    {
        get { return quemando; }
    }

    void Start()
    {
        trojes = new Transform[idsTrojes.Length];
        for (int i = 0; i < idsTrojes.Length; i++)
        {
            GameObject troje = GameObject.Find("Troje_" + idsTrojes[i]);
            if (troje != null)
            {
                trojes[i] = troje.transform;
            }
        }
    }

    void Update()
    {
        trojeCercano = BuscarTrojeCercano();
        bool teclaPresionada = Input.GetKey(teclaQuemar) || Input.GetKey(teclaQuemarAlterna);
        quemando = teclaPresionada && trojeCercano != "";

        // El cliente manda este id en cada paso; vacío = no está quemando nada
        cliente.trojeQuemando = quemando ? trojeCercano : "";
        movimiento.Bloqueado = quemando;

        bool lanzar = Input.GetKeyDown(teclaPiedra) || Input.GetMouseButtonDown(1);
        if (lanzar && !quemando)
        {
            LanzarPiedra();
        }
    }

    // ---------- piedras ----------
    void LanzarPiedra()
    {
        if (cliente.Estado == null || cliente.Estado.stones <= 0 || cliente.PiedraPendiente)
        {
            return;     // sin piedras, o ya hay una esperando a mandarse
        }
        Vector3 destino;
        if (!PuntoDelMouseEnElSuelo(out destino))
        {
            return;
        }

        // no más lejos del alcance (el servidor también lo limita)
        Vector3 origen = transform.position;
        origen.y = 0f;
        Vector3 diferencia = destino - origen;
        if (diferencia.magnitude > alcancePiedra)
        {
            destino = origen + diferencia.normalized * alcancePiedra;
        }

        cliente.LanzarPiedra(destino);

        GameObject piedra = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        piedra.name = "Piedra";
        Destroy(piedra.GetComponent<Collider>());
        piedra.transform.localScale = new Vector3(0.25f, 0.25f, 0.25f);
        piedra.GetComponent<Renderer>().sharedMaterial = materialPiedra;
        PiedraVisual vuelo = piedra.AddComponent<PiedraVisual>();
        vuelo.desde = transform.position + new Vector3(0f, 1.5f, 0f);
        vuelo.hacia = destino;
        piedra.transform.position = vuelo.desde;    // que no aparezca un instante en el origen
        vuelo.radioRuido = radioRuido;
        vuelo.materialRuido = materialRuido;
    }

    bool PuntoDelMouseEnElSuelo(out Vector3 punto)
    {
        // rayo desde la cámara a través del mouse, contra el plano del suelo (altura 0)
        punto = Vector3.zero;
        Camera camara = Camera.main;
        if (camara == null)
        {
            return false;
        }
        Ray rayo = camara.ScreenPointToRay(Input.mousePosition);
        Plane suelo = new Plane(Vector3.up, Vector3.zero);
        float distancia;
        if (!suelo.Raycast(rayo, out distancia))
        {
            return false;
        }
        punto = rayo.GetPoint(distancia);
        return true;
    }

    string BuscarTrojeCercano()
    {
        string cercano = "";
        float mejor = alcanceQuemar;
        for (int i = 0; i < idsTrojes.Length; i++)
        {
            if (trojes[i] == null || YaArde(idsTrojes[i]))
            {
                continue;
            }
            // distancia en el suelo (sin contar la altura)
            Vector3 diferencia = trojes[i].position - transform.position;
            diferencia.y = 0f;
            float d = diferencia.magnitude;
            if (d < mejor)
            {
                mejor = d;
                cercano = idsTrojes[i];
            }
        }
        return cercano;
    }

    bool YaArde(string id)
    {
        TrojeEstado t = EstadoDelTroje(id);
        return t != null && t.burning;
    }

    public TrojeEstado EstadoDelTroje(string id)
    {
        if (cliente.Estado == null || cliente.Estado.trojes == null)
        {
            return null;
        }
        foreach (TrojeEstado t in cliente.Estado.trojes)
        {
            if (t.id == id)
            {
                return t;
            }
        }
        return null;
    }
}
