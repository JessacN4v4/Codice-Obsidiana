using UnityEngine;

/// <summary>
/// Movimiento de Tariácuri con vista cenital.
/// WASD o flechas: caminar. Shift: correr (gasta aliento).
/// W siempre es hacia el norte del mapa (la cámara no gira).
/// Valores iguales a los del servidor: 112 px/s caminando y 185 px/s corriendo (× 0.05 m por px).
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class MovimientoJugador : MonoBehaviour
{
    [Header("Velocidad (m/s)")]
    public float velocidadCaminar = 5.6f;
    public float velocidadCorrer = 9.25f;

    [Header("Aliento")]
    public float alientoMaximo = 100f;
    public float gastoAlCorrer = 28f;          // por segundo
    public float recuperacion = 16f;           // por segundo
    public float minimoParaVolverACorrer = 35f;

    // Lo que leen otros scripts (la red y el HUD)
    public float Aliento { get; private set; }
    public bool Corriendo { get; private set; }
    public bool Agotado { get; private set; }

    // Otros scripts lo activan para que el jugador no se mueva (por ejemplo, mientras quema un troje)
    public bool Bloqueado { get; set; }

    private Rigidbody rb;
    private Vector3 direccion;                 // hacia dónde quiere moverse (largo 0 o 1)

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;              // que no se voltee al chocar
        Aliento = alientoMaximo;
    }

    void Update()   // cada frame: leer el teclado y actualizar el aliento
    {
        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");
        direccion = new Vector3(x, 0f, z).normalized;
        if (Bloqueado)
        {
            direccion = Vector3.zero;          // quieto (y sin correr)
        }
        bool moviendose = direccion != Vector3.zero;

        bool quiereCorrer = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        Corriendo = quiereCorrer && moviendose && !Agotado;

        if (Corriendo)
        {
            Aliento -= gastoAlCorrer * Time.deltaTime;
            if (Aliento <= 0f)
            {
                Aliento = 0f;
                Agotado = true;                // no puede correr hasta recuperar un poco
            }
        }
        else
        {
            Aliento = Mathf.Min(alientoMaximo, Aliento + recuperacion * Time.deltaTime);
            if (Agotado && Aliento > minimoParaVolverACorrer)
            {
                Agotado = false;
            }
        }
    }

    void FixedUpdate()   // física: mover y girar con el Rigidbody
    {
        float velocidad = Corriendo ? velocidadCorrer : velocidadCaminar;
        Vector3 v = direccion * velocidad;
        rb.linearVelocity = new Vector3(v.x, rb.linearVelocity.y, v.z);   // conserva la caída (gravedad)

        if (direccion != Vector3.zero)
        {
            rb.MoveRotation(Quaternion.LookRotation(direccion));          // mira hacia donde camina
        }
    }
}
