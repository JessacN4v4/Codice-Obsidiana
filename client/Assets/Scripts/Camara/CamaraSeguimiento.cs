using UnityEngine;

/// <summary>
/// Cámara de seguimiento en tercera persona (estilo GTA), detrás y un poco a la derecha del jugador.
/// Mouse: girar la cámara alrededor del jugador (solo mientras el cursor está capturado).
/// Rueda del mouse: acercar / alejar.
/// Si hay una pared o una casa entre el jugador y la cámara, la cámara se acerca para no atravesarla.
/// ControlDeCamaras le dice si está activa (si es la que el jugador está viendo).
/// </summary>
[RequireComponent(typeof(Camera))]
public class CamaraSeguimiento : MonoBehaviour
{
    public Transform objetivo;                 // el jugador

    [Header("Posición respecto al jugador (m)")]
    public float alturaPivote = 1.6f;          // a qué altura del jugador mira (los hombros)
    public float hombro = 0.6f;                // desplazamiento a la derecha, para no tapar el centro
    public float distancia = 5.5f;             // antes 4.5: un poco más lejos del personaje
    public float distanciaMinima = 1.5f;
    public float distanciaMaxima = 9f;
    public float velocidadZoom = 4f;

    [Header("Giro con el mouse (grados)")]
    public float sensibilidad = 3f;
    public float inclinacionMinima = -20f;     // mirar hacia arriba
    public float inclinacionMaxima = 65f;      // mirar hacia abajo
    public float inclinacionInicial = 15f;

    [Header("Choques con paredes")]
    public float radioChoque = 0.3f;           // grosor de la "esfera" que se lanza hacia la cámara

    [Header("Suavizado")]
    public float suavizadoPivote = 15f;        // qué tan pegado sigue al jugador
    public float suavizadoAlejarse = 6f;       // al dejar de chocar, vuelve poco a poco

    // Lo pone ControlDeCamaras
    public bool Activa { get; set; }
    public bool MouseCapturado { get; set; }

    // Ángulos actuales (los lee ControlDeCamaras para el movimiento)
    public float Giro
    {
        get { return giro; }
    }

    private float giro;                        // alrededor del eje vertical (0 = mirando al norte)
    private float inclinacion;
    private Vector3 pivote;
    private float distanciaActual;
    private bool iniciada = false;

    void LateUpdate()
    {
        if (objetivo == null)
        {
            // la escena perdió la referencia (p. ej. tras volver a crear al jugador): buscarlo
            MovimientoJugador jugador = FindAnyObjectByType<MovimientoJugador>();
            if (jugador == null)
            {
                return;
            }
            objetivo = jugador.transform;
            Debug.LogWarning("CamaraSeguimiento: faltaba el objetivo; se usó " + jugador.name +
                ". Usa Codice > Reconectar referencias del jugador y guarda la escena.");
        }
        if (!iniciada)
        {
            // empieza detrás del jugador, mirando hacia donde él mira
            giro = objetivo.eulerAngles.y;
            inclinacion = inclinacionInicial;
            pivote = objetivo.position + Vector3.up * alturaPivote;
            distanciaActual = distancia;
            iniciada = true;
        }

        if (Activa)
        {
            if (MouseCapturado)
            {
                giro += Input.GetAxis("Mouse X") * sensibilidad;
                inclinacion -= Input.GetAxis("Mouse Y") * sensibilidad;
                inclinacion = Mathf.Clamp(inclinacion, inclinacionMinima, inclinacionMaxima);
            }
            float rueda = Input.GetAxis("Mouse ScrollWheel");
            distancia = Mathf.Clamp(distancia - rueda * velocidadZoom, distanciaMinima, distanciaMaxima);
        }

        // el pivote sigue al jugador con un poco de suavizado
        Vector3 pivoteDeseado = objetivo.position + Vector3.up * alturaPivote;
        pivote = Vector3.Lerp(pivote, pivoteDeseado, suavizadoPivote * Time.deltaTime);

        Quaternion rotacion = Quaternion.Euler(inclinacion, giro, 0f);
        Vector3 haciaAtras = rotacion * Vector3.back;
        Vector3 inicio = pivote + rotacion * Vector3.right * hombro;

        // ¿choca con algo entre el jugador y la cámara?
        float libre = DistanciaLibre(inicio, haciaAtras, distancia);
        if (libre < distanciaActual)
        {
            distanciaActual = libre;           // acercarse de golpe, para no ver a través de la pared
        }
        else
        {
            distanciaActual = Mathf.Lerp(distanciaActual, libre, suavizadoAlejarse * Time.deltaTime);
        }

        transform.position = inicio + haciaAtras * distanciaActual;
        transform.rotation = rotacion;
    }

    float DistanciaLibre(Vector3 inicio, Vector3 direccion, float maxima)
    {
        // se lanza una esfera desde el jugador hacia atrás; lo primero que toque (que no sea el jugador) la detiene
        RaycastHit[] golpes = Physics.SphereCastAll(inicio, radioChoque, direccion, maxima,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float libre = maxima;
        foreach (RaycastHit golpe in golpes)
        {
            if (golpe.transform.IsChildOf(objetivo))
            {
                continue;                      // el propio jugador no cuenta
            }
            if (golpe.distance > 0f && golpe.distance < libre)
            {
                libre = golpe.distance;
            }
        }
        return Mathf.Max(libre, 0.2f);
    }
}
