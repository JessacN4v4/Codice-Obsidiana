using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Pantalla de victoria o derrota (provisional para el graybox, dibujada con OnGUI).
/// Aparece cuando el servidor dice que la partida terminó (over = true):
/// resultado, motivo, estadísticas y botones para reintentar o salir.
/// Reintentar recarga la escena, y al empezar se crea una partida nueva en el servidor.
/// </summary>
public class PantallaFinal : MonoBehaviour
{
    public ClienteSimulacion cliente;
    public KeyCode teclaReintentar = KeyCode.R;

    const float AltoBase = 1080f;
    const int PiedrasIniciales = 3;

    private string motivo = "";        // mensaje del servidor al terminar
    private GUIStyle estiloTitulo;
    private GUIStyle estiloTexto;
    private GUIStyle estiloBoton;

    void Start()
    {
        cliente.AlRecibirEstado += AlRecibirEstado;
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
        if (Terminada() && Input.GetKeyDown(teclaReintentar))
        {
            Reintentar();
        }
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

    void OnGUI()
    {
        if (!Terminada())
        {
            return;
        }
        PrepararEstilos();
        float escala = Screen.height / AltoBase;
        GUI.matrix = Matrix4x4.Scale(new Vector3(escala, escala, 1f));
        float ancho = Screen.width / escala;

        // oscurecer toda la pantalla
        GUI.color = new Color(0f, 0f, 0f, 0.65f);
        GUI.DrawTexture(new Rect(0f, 0f, ancho, AltoBase), Texture2D.whiteTexture);
        GUI.color = Color.white;

        EstadoDatos e = cliente.Estado;
        bool gano = e.result == "win";

        float w = 640f;
        float h = 470f;
        float x = (ancho - w) / 2f;
        float y = (AltoBase - h) / 2f;
        GUI.Box(new Rect(x, y, w, h), "");

        estiloTitulo.normal.textColor = gano ? new Color(0.5f, 1f, 0.55f) : new Color(1f, 0.4f, 0.35f);
        GUI.Label(new Rect(x, y + 25f, w, 60f), gano ? "¡ESCAPASTE!" : "TE ATRAPARON", estiloTitulo);

        string texto = motivo != "" ? motivo : (gano ? "Huiste en la canoa." : "La misión falló.");
        GUI.Label(new Rect(x + 30f, y + 95f, w - 60f, 50f), texto, estiloTexto);

        string estadisticas =
            "Tiempo: " + FormatoTiempo(e.simTime) + "\n" +
            "Trojes quemados: " + e.burned + " / 3\n" +
            "Veces que te descubrieron: " + e.detections + "\n" +
            "Piedras usadas: " + (PiedrasIniciales - e.stones) + " / " + PiedrasIniciales;
        GUI.Label(new Rect(x + 30f, y + 160f, w - 60f, 160f), estadisticas, estiloTexto);

        float anchoBoton = 240f;
        if (GUI.Button(new Rect(x + w / 2f - anchoBoton - 15f, y + h - 95f, anchoBoton, 60f), "Reintentar (R)", estiloBoton))
        {
            Reintentar();
        }
        if (GUI.Button(new Rect(x + w / 2f + 15f, y + h - 95f, anchoBoton, 60f), "Salir", estiloBoton))
        {
            Salir();
        }
    }

    string FormatoTiempo(float segundos)
    {
        int total = Mathf.FloorToInt(segundos);
        return (total / 60) + ":" + (total % 60).ToString("00");
    }

    void PrepararEstilos()
    {
        if (estiloTitulo != null)
        {
            return;
        }
        estiloTitulo = new GUIStyle(GUI.skin.label);
        estiloTitulo.fontSize = 48;
        estiloTitulo.fontStyle = FontStyle.Bold;
        estiloTitulo.alignment = TextAnchor.MiddleCenter;

        estiloTexto = new GUIStyle(GUI.skin.label);
        estiloTexto.fontSize = 26;
        estiloTexto.wordWrap = true;
        estiloTexto.alignment = TextAnchor.UpperCenter;
        estiloTexto.normal.textColor = new Color(0.95f, 0.9f, 0.82f);

        estiloBoton = new GUIStyle(GUI.skin.button);
        estiloBoton.fontSize = 26;
    }
}
