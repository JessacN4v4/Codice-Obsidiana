using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Pantalla de victoria o derrota (provisional para el graybox), con uGUI en los dos displays.
/// Aparece cuando el servidor dice que la partida terminó (over = true):
/// resultado, motivo, estadísticas y botones para reintentar o salir (R también reintenta).
/// Reintentar recarga la escena, y al empezar se crea una partida nueva en el servidor.
/// Se crea un Canvas por display porque OnGUI solo se ve en el Display 1.
/// </summary>
public class PantallaFinal : MonoBehaviour
{
    public ClienteSimulacion cliente;
    public KeyCode teclaReintentar = KeyCode.R;

    const int PiedrasIniciales = 3;

    class Vista
    {
        public GameObject raiz;
        public Text titulo, motivo, estadisticas;
    }

    private string motivo = "";        // mensaje del servidor al terminar
    private readonly Vista[] vistas = new Vista[2];

    void Start()
    {
        cliente.AlRecibirEstado += AlRecibirEstado;
        UIFabrica.AsegurarEventSystem();
        for (int display = 0; display < 2; display++)
        {
            vistas[display] = CrearVista(display);
        }
    }

    void OnDestroy()
    {
        if (cliente != null)
        {
            cliente.AlRecibirEstado -= AlRecibirEstado;
        }
    }

    void AlRecibirEstado(EstadoDatos estado)
    {
        if (estado.events == null)
        {
            return;
        }
        foreach (EventoDatos e in estado.events)
        {
            if (e.type == "captured" || e.type == "win")
            {
                motivo = e.message;
            }
        }
    }

    bool Terminada()
    {
        return cliente != null && cliente.Estado != null && cliente.Estado.over;
    }

    void Update()
    {
        bool terminada = Terminada();
        foreach (Vista v in vistas)
        {
            v.raiz.SetActive(terminada);
            if (terminada)
            {
                Actualizar(v);
            }
        }
        if (terminada && Input.GetKeyDown(teclaReintentar))
        {
            Reintentar();
        }
    }

    Vista CrearVista(int display)
    {
        Vista v = new Vista();
        Canvas canvas = UIFabrica.CrearCanvas("PantallaFinal", display, 20, transform, true);
        v.raiz = canvas.gameObject;

        UIFabrica.Fondo(canvas.transform, "Oscurecer", new Color(0f, 0f, 0f, 0.65f));
        Image caja = UIFabrica.Caja(canvas.transform, "Caja", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(640f, 470f), UIFabrica.ColorCaja);
        Transform c = caja.transform;
        Vector2 arriba = new Vector2(0.5f, 1f);
        v.titulo = UIFabrica.Texto(c, "Titulo", arriba, arriba, new Vector2(0f, -25f), new Vector2(640f, 60f), 48, TextAnchor.MiddleCenter, Color.white, true);
        v.motivo = UIFabrica.Texto(c, "Motivo", arriba, arriba, new Vector2(0f, -95f), new Vector2(580f, 60f), 26, TextAnchor.UpperCenter, UIFabrica.ColorTexto);
        v.estadisticas = UIFabrica.Texto(c, "Estadisticas", arriba, arriba, new Vector2(0f, -160f), new Vector2(580f, 160f), 26, TextAnchor.UpperCenter, UIFabrica.ColorTexto);

        Vector2 abajo = new Vector2(0.5f, 0f);
        UIFabrica.Boton(c, "Reintentar", abajo, new Vector2(-135f, 65f), new Vector2(240f, 60f), "Reintentar (R)", Reintentar);
        UIFabrica.Boton(c, "Salir", abajo, new Vector2(135f, 65f), new Vector2(240f, 60f), "Salir", Salir);

        v.raiz.SetActive(false);
        return v;
    }

    void Actualizar(Vista v)
    {
        EstadoDatos e = cliente.Estado;
        bool gano = e.result == "win";
        v.titulo.text = gano ? "¡ESCAPASTE!" : "TE ATRAPARON";
        v.titulo.color = gano ? new Color(0.5f, 1f, 0.55f) : new Color(1f, 0.4f, 0.35f);
        v.motivo.text = motivo != "" ? motivo : (gano ? "Huiste en la canoa." : "La misión falló.");
        v.estadisticas.text =
            "Tiempo: " + FormatoTiempo(e.simTime) + "\n" +
            "Trojes quemados: " + e.burned + " / 3\n" +
            "Veces que te descubrieron: " + e.detections + "\n" +
            "Piedras usadas: " + (PiedrasIniciales - e.stones) + " / " + PiedrasIniciales;
    }

    void Reintentar()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    void Salir()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;   // en el editor: detener el Play
#else
        Application.Quit();
#endif
    }

    string FormatoTiempo(float segundos)
    {
        int total = Mathf.FloorToInt(segundos);
        return (total / 60) + ":" + (total % 60).ToString("00");
    }
}
