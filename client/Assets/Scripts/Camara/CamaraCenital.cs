using UnityEngine;

/// <summary>
/// Cámara cenital inclinada que sigue al jugador (estilo Civilization).
/// Rueda del mouse: acercar / alejar.
/// Se usa LateUpdate para moverla cuando el jugador ya terminó de moverse en ese frame.
/// </summary>
public class CamaraCenital : MonoBehaviour
{
    public Transform objetivo;                 // el jugador; se asigna en el Inspector

    [Header("Posición respecto al jugador (m)")]
    public float altura = 19f;
    public float distanciaAtras = 14f;         // hacia el sur, para verlo inclinado

    [Header("Zoom con la rueda del mouse")]
    public float zoomMinimo = 0.5f;
    public float zoomMaximo = 1.6f;
    public float velocidadZoom = 2f;

    [Header("Suavizado")]
    public float suavizado = 8f;               // más alto = sigue más pegado

    private float zoom = 1f;

    void LateUpdate()
    {
        if (objetivo == null)
        {
            return;
        }

        if (!ControlDeCamaras.EnSeguimiento)   // la rueda solo hace zoom en la cámara con la que se juega
        {
            float rueda = Input.GetAxis("Mouse ScrollWheel");
            zoom = Mathf.Clamp(zoom - rueda * velocidadZoom, zoomMinimo, zoomMaximo);
        }

        Vector3 desplazamiento = new Vector3(0f, altura, -distanciaAtras) * zoom;
        Vector3 deseada = objetivo.position + desplazamiento;
        transform.position = Vector3.Lerp(transform.position, deseada, suavizado * Time.deltaTime);
        transform.LookAt(objetivo.position);
    }
}
