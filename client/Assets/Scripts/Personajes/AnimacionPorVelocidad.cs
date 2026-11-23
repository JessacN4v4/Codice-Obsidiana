using UnityEngine;

/// <summary>
/// Elige la animación (parado, caminar, correr, hacia atrás o de lado) según cómo se mueve el personaje.
/// Mide cuánto se movió "cuerpo" desde el frame anterior, lo pasa a ejes del propio cuerpo
/// y se lo da al Animator en dos parámetros (metros por segundo):
///   VelocidadZ: hacia adelante (+) o hacia atrás (-)
///   VelocidadX: hacia la derecha (+) o hacia la izquierda (-)
/// Sirve igual para el jugador (lo mueve la física) que para guardias y aldeanos (los mueve
/// VistaAgentes con lo que manda el servidor; ellos siempre caminan hacia adelante).
///
/// Si el Animator no tiene animaciones de lado (parámetro TieneLaterales = false), el modelo
/// se gira hacia donde camina: de lado gira el cuerpo 90° y camina normal; hacia atrás
/// no gira y usa la animación de caminar hacia atrás. Así no parece que se desliza.
/// </summary>
[RequireComponent(typeof(Animator))]
public class AnimacionPorVelocidad : MonoBehaviour
{
    public Transform cuerpo;               // lo que se mueve; si está vacío, el padre de este objeto
    public float suavizado = 10f;          // más alto = cambia de animación más rápido
    public float velocidadGiro = 540f;     // grados por segundo al girar el modelo (sin laterales)
    public float anguloHaciaAtras = 110f;  // más de este ángulo = camina hacia atrás en lugar de girar

    private Animator animator;
    private Vector3 posicionAnterior;
    private Vector2 velocidad;             // x = de lado, y = adelante (suavizada)
    private bool tieneXZ;                  // el Animator tiene VelocidadX y VelocidadZ
    private bool tieneVelocidad;           // Animator viejo: solo "Velocidad"
    private bool girarModelo;              // no hay laterales: girar el modelo hacia donde camina
    private float giroModelo = 0f;         // grados respecto al cuerpo

    void Start()
    {
        animator = GetComponent<Animator>();
        animator.applyRootMotion = false;      // a los personajes los mueve el juego, no la animación
        if (cuerpo == null)
        {
            cuerpo = transform.parent != null ? transform.parent : transform;
        }
        posicionAnterior = cuerpo.position;

        bool tieneX = false;
        bool tieneZ = false;
        bool tieneLaterales = false;
        foreach (AnimatorControllerParameter p in animator.parameters)
        {
            if (p.name == "VelocidadX")
            {
                tieneX = true;
            }
            else if (p.name == "VelocidadZ")
            {
                tieneZ = true;
            }
            else if (p.name == "Velocidad")
            {
                tieneVelocidad = true;
            }
            else if (p.name == "TieneLaterales")
            {
                tieneLaterales = p.defaultBool;
            }
        }
        tieneXZ = tieneX && tieneZ;
        girarModelo = tieneXZ && !tieneLaterales;
    }

    void OnEnable()
    {
        // al prender el modelo (p. ej. al cambiar de atuendo) no contar el salto como velocidad
        if (cuerpo != null)
        {
            posicionAnterior = cuerpo.position;
        }
    }

    void Update()
    {
        Vector3 movimiento = cuerpo.position - posicionAnterior;
        movimiento.y = 0f;                     // solo cuenta el movimiento por el suelo
        posicionAnterior = cuerpo.position;

        Vector3 porSegundo = Time.deltaTime > 0f ? movimiento / Time.deltaTime : Vector3.zero;
        Vector3 local = Quaternion.Inverse(cuerpo.rotation) * porSegundo;   // en ejes del cuerpo
        Vector2 actual = new Vector2(local.x, local.z);

        if (girarModelo)
        {
            actual = GirarHaciaMovimiento(actual);
        }

        velocidad = Vector2.Lerp(velocidad, actual, Mathf.Clamp01(Time.deltaTime * suavizado));
        if (tieneXZ)
        {
            animator.SetFloat("VelocidadX", velocidad.x);
            animator.SetFloat("VelocidadZ", velocidad.y);
        }
        else if (tieneVelocidad)
        {
            animator.SetFloat("Velocidad", velocidad.magnitude);
        }
    }

    /// <summary>
    /// Sin animaciones de lado: gira el modelo hacia el eje en que camina y devuelve
    /// la velocidad que debe animar (solo adelante o atrás).
    /// </summary>
    Vector2 GirarHaciaMovimiento(Vector2 v)
    {
        float rapidez = v.magnitude;
        float objetivo = 0f;                   // parado: mirar hacia donde mira el cuerpo
        Vector2 animar = Vector2.zero;
        if (rapidez > 0.2f)
        {
            float angulo = Mathf.Atan2(v.x, v.y) * Mathf.Rad2Deg;   // 0 = adelante, 90 = derecha
            if (Mathf.Abs(angulo) <= anguloHaciaAtras)
            {
                objetivo = angulo;             // de frente hacia donde camina
                animar = new Vector2(0f, rapidez);
            }
            else
            {
                objetivo = angulo - 180f * Mathf.Sign(angulo);   // de espaldas a donde camina
                animar = new Vector2(0f, -rapidez);
            }
        }
        giroModelo = Mathf.MoveTowardsAngle(giroModelo, objetivo, velocidadGiro * Time.deltaTime);
        transform.localRotation = Quaternion.Euler(0f, giroModelo, 0f);
        return animar;
    }
}
