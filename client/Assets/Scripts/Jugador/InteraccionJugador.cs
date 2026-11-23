using UnityEngine;

/// <summary>
/// Acciones del jugador sobre el pueblo.
/// Quemar troje: cerca de un troje sin quemar, mantener E (o Espacio).
/// El servidor lleva el progreso (1.3 s) y decide cuándo arde; aquí solo se le avisa
/// qué troje se está quemando y se deja al jugador quieto mientras tanto.
/// </summary>
public class InteraccionJugador : MonoBehaviour
{
    public ClienteSimulacion cliente;
    public MovimientoJugador movimiento;

    [Header("Quemar trojes")]
    public float alcanceQuemar = 2.7f;         // metros (54 px del mapa, igual que el servidor)
    public KeyCode teclaQuemar = KeyCode.E;
    public KeyCode teclaQuemarAlterna = KeyCode.Space;

    private string[] idsTrojes = { "A", "B", "C" };
    private Transform[] trojes;                // las cajas Troje_A, Troje_B y Troje_C del pueblo
    private string trojeCercano = "";          // id del troje que tiene a su alcance ("" = ninguno)
    private bool quemando = false;

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

    TrojeEstado EstadoDelTroje(string id)
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

    // Aviso en pantalla (provisional, hasta tener el HUD)
    void OnGUI()
    {
        if (trojeCercano == "")
        {
            return;
        }
        string texto;
        if (quemando)
        {
            TrojeEstado t = EstadoDelTroje(trojeCercano);
            float progreso = t != null ? t.progress : 0f;
            texto = "Quemando el troje " + trojeCercano + "... " + Mathf.RoundToInt(progreso * 100f) + "%";
        }
        else
        {
            texto = "Mantén E para quemar el troje " + trojeCercano;
        }
        GUIStyle estilo = new GUIStyle(GUI.skin.box);
        estilo.fontSize = 20;
        float ancho = 420f;
        GUI.Box(new Rect((Screen.width - ancho) / 2f, Screen.height - 90f, ancho, 40f), texto, estilo);
    }
}
