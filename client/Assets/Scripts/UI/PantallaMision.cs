using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pantalla con el contexto de la misión antes de empezar a jugar, en los dos displays.
/// Mientras está abierta el juego queda en pausa (Time.timeScale = 0): el cliente no manda pasos
/// al servidor, nadie se mueve y el jugador no puede caminar, quemar ni lanzar piedras.
/// Se cierra con el botón o con Enter. Al reintentar (se recarga la escena) ya no vuelve a salir.
/// Lo crea el HUD al empezar.
/// </summary>
public class PantallaMision : MonoBehaviour
{
    public MovimientoJugador movimiento;
    public InteraccionJugador interaccion;

    /// <summary>¿Se está mostrando? (la cámara deja el mouse libre para el botón)</summary>
    public static bool Abierta { get; private set; }

    private static bool yaSeMostro;            // solo la primera vez, no al reintentar
    private readonly GameObject[] raices = new GameObject[2];

    // al dar Play otra vez en el editor, empezar desde cero (aunque Unity no recargue los static)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reiniciar()
    {
        Abierta = false;
        yaSeMostro = false;
    }

    void Start()
    {
        if (yaSeMostro)
        {
            return;
        }
        UIFabrica.AsegurarEventSystem();
        for (int display = 0; display < 2; display++)
        {
            raices[display] = CrearVista(display);
        }
        Abierta = true;
        Time.timeScale = 0f;
        Bloquear(true);
    }

    void Update()
    {
        if (Abierta && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
        {
            Comenzar();
        }
    }

    public void Comenzar()
    {
        if (!Abierta)
        {
            return;
        }
        Abierta = false;
        yaSeMostro = true;
        Time.timeScale = 1f;
        Bloquear(false);
        foreach (GameObject raiz in raices)
        {
            if (raiz != null)
            {
                Destroy(raiz);
            }
        }
    }

    void OnDestroy()
    {
        if (Abierta)
        {
            Abierta = false;
            Time.timeScale = 1f;               // que el juego no se quede en pausa
        }
    }

    void Bloquear(bool bloquear)
    {
        if (movimiento != null)
        {
            movimiento.enabled = !bloquear;
        }
        if (interaccion != null)
        {
            interaccion.enabled = !bloquear;
        }
    }

    // ---------- construcción ----------
    GameObject CrearVista(int display)
    {
        Canvas canvas = UIFabrica.CrearCanvas("PantallaMision", display, 30, transform, true);
        UIFabrica.Fondo(canvas.transform, "Oscurecer", new Color(0.03f, 0.02f, 0.02f, 0.88f));

        Vector2 centro = new Vector2(0.5f, 0.5f);
        Image caja = UIFabrica.Caja(canvas.transform, "Caja", centro, centro, Vector2.zero, new Vector2(1180f, 960f), UIFabrica.ColorCaja);
        Outline borde = caja.gameObject.AddComponent<Outline>();
        borde.effectColor = new Color(0.85f, 0.6f, 0.3f, 0.7f);
        borde.effectDistance = new Vector2(2f, -2f);
        Transform c = caja.transform;
        Vector2 arriba = new Vector2(0.5f, 1f);
        Color dorado = new Color(0.95f, 0.75f, 0.4f);

        Text titulo = UIFabrica.Texto(c, "Titulo", arriba, arriba, new Vector2(0f, -28f), new Vector2(1100f, 56f), 44, TextAnchor.MiddleCenter, dorado, true);
        titulo.text = "LOS TROJES DEL PUEBLO FRONTERIZO";
        Text subtitulo = UIFabrica.Texto(c, "Subtitulo", arriba, arriba, new Vector2(0f, -86f), new Vector2(1100f, 30f), 22, TextAnchor.MiddleCenter, UIFabrica.ColorTexto);
        subtitulo.text = "Frontera del reino purépecha · guerra contra los mexicas (c. 1476–1479)";

        Text cuerpo = UIFabrica.Texto(c, "Contexto", arriba, arriba, new Vector2(0f, -136f), new Vector2(1080f, 700f), 23, TextAnchor.UpperLeft, UIFabrica.ColorTexto);
        cuerpo.supportRichText = true;
        cuerpo.lineSpacing = 1.1f;
        cuerpo.text =
            "Eres <b>Tariácuri</b>, espía de una orden secreta al servicio del cazonci. Una guarnición mexica " +
            "ocupa este pueblo de la frontera y guarda su maíz en tres almacenes. Sin ese grano, sus guerreros " +
            "no podrán sostener la campaña.\n\n" +
            "<b><color=#F2C066>FASE 1 · INFILTRACIÓN</color></b>\n" +
            "Entra disfrazado de cargador y quema el troje de cada almacén. Los <b><color=#FF8C26>rombos naranjas</color></b> " +
            "marcan dónde están.\n" +
            "   <b>A</b> · Almacén del mercado: vigilancia baja, los cargadores entran y salen.\n" +
            "   <b>B</b> · Almacén de flechas: vigilancia media, tiene una brecha al este.\n" +
            "   <b>C</b> · Almacén de la guarnición: vigilancia alta, solo hay una brecha al oeste.\n" +
            "Cada troje que arde sube la alerta del pueblo: los guardias ven más lejos y sospechan más rápido. " +
            "Un <b>círculo</b> sobre un guardia quiere decir que te está viendo; se llena con la sospecha. " +
            "Si se llena, suena la alarma.\n\n" +
            "<b><color=#FF7060>FASE 2 · HUIDA</color></b>\n" +
            "Al arder el tercer troje suena la alarma y el disfraz ya no sirve. Llega a la <b>canoa</b> en la orilla " +
            "del lago, al suroeste. Escóndete en los maizales y distrae a los guardias con piedras.\n\n" +
            "<b><color=#F2C066>CONTROLES</color></b>\n" +
            "W/A/S/D: caminar   ·   Shift: correr   ·   E (mantener): quemar troje\n" +
            "Q o clic derecho: lanzar piedra   ·   V: cambiar de cámara";

        UIFabrica.Boton(c, "Comenzar", new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(380f, 64f), "Comenzar misión (Enter)", Comenzar);
        return canvas.gameObject;
    }
}
