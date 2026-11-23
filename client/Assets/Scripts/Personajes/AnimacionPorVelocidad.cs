using UnityEngine;

/// <summary>
/// Elige la animación (parado, caminar o correr) según qué tan rápido se mueve el personaje.
/// Mide cuánto se movió "cuerpo" desde el frame anterior y se lo pasa al Animator en el
/// parámetro "Velocidad" (metros por segundo). Sirve igual para el jugador (lo mueve la física)
/// que para guardias y aldeanos (los mueve VistaAgentes con lo que manda el servidor).
/// </summary>
[RequireComponent(typeof(Animator))]
public class AnimacionPorVelocidad : MonoBehaviour
{
    public Transform cuerpo;               // lo que se mueve; si está vacío, el padre de este objeto
    public float suavizado = 10f;          // más alto = cambia de animación más rápido

    private Animator animator;
    private Vector3 posicionAnterior;
    private float velocidad;

    void Start()
    {
        animator = GetComponent<Animator>();
        animator.applyRootMotion = false;      // a los personajes los mueve el juego, no la animación
        if (cuerpo == null)
        {
            cuerpo = transform.parent != null ? transform.parent : transform;
        }
        posicionAnterior = cuerpo.position;
    }

    void Update()
    {
        Vector3 movimiento = cuerpo.position - posicionAnterior;
        movimiento.y = 0f;                     // solo cuenta el movimiento por el suelo
        posicionAnterior = cuerpo.position;

        float actual = Time.deltaTime > 0f ? movimiento.magnitude / Time.deltaTime : 0f;
        velocidad = Mathf.Lerp(velocidad, actual, Mathf.Clamp01(Time.deltaTime * suavizado));
        animator.SetFloat("Velocidad", velocidad);
    }
}
