using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using System.Reflection;
using UnityEditor;
#endif

/// <summary>
/// Decide con qué cámara se juega.
/// - Display 1: cámara cenital (W = norte, piedras hacia el mouse).
/// - Display 2: cámara de seguimiento con controles estilo shooter: el mouse gira la cámara siempre
///   (se captura solo), W/S van adelante/atrás y A/D de lado; Tariácuri mira hacia donde mira la
///   cámara (S retrocede sin darse la vuelta). Piedras hacia la mira del centro.
/// En la vista Game de Unity se cambia con el menú "Display 1 / Display 2" de arriba a la izquierda.
/// Se juega con la cámara del display que se está viendo:
///  - en el editor, el que muestra la vista Game enfocada (o la que tiene el mouse encima);
///  - en un build con dos monitores, el monitor donde está el mouse;
///  - con un solo monitor, el Display 1. La tecla V intercambia los displays de las dos cámaras.
/// Con la cámara de seguimiento: Esc suelta el mouse (para usar menús) y clic izquierdo lo recupera.
/// Cada display muestra abajo la ayuda de controles de SU cámara, y el de seguimiento la mira del
/// centro (con uGUI: OnGUI solo se ve en el Display 1).
/// </summary>
public class ControlDeCamaras : MonoBehaviour
{
    public Camera camaraCenital;
    public Camera camaraSeguimiento;
    public MovimientoJugador movimiento;
    public ClienteSimulacion cliente;

    public KeyCode teclaCambiar = KeyCode.V;

    // Lo que leen otros scripts
    public static bool EnSeguimiento { get; private set; }
    public static Camera CamaraActiva { get; private set; }

    private CamaraSeguimiento seguimiento;
    private int displayVisto = 0;              // el display que muestra la vista Game (0 = Display 1)
    private bool soltadoConEsc = false;        // el jugador soltó el mouse a propósito: no recapturarlo solo
    private bool avisado = false;              // ya se escribió en la consola qué cámara se usa

    // ayuda de controles y mira, una por display
    class Ayuda
    {
        public GameObject mira;
        public Image caja;
        public Text texto;
    }
    private readonly Ayuda[] ayudas = new Ayuda[2];

    const string AyudaCenital =
        "W/A/S/D: caminar (W = norte) · Shift: correr · E: quemar troje · Q o clic der.: piedra hacia el mouse · Rueda: zoom · V: cambiar cámara";
    const string AyudaSeguimiento =
        "Mouse: girar cámara · W/S: adelante/atrás · A/D: de lado · Shift: correr · E: quemar troje · Q o clic der.: piedra a la mira · Esc: soltar mouse · V: cambiar cámara";
    const string AyudaMouseSuelto =
        "Mouse suelto · Clic izquierdo: volver a girar la cámara con el mouse · W/A/S/D: caminar · V: cambiar cámara";

    void Start()
    {
        // si la escena perdió las referencias (p. ej. tras volver a crear al jugador), buscarlas
        if (movimiento == null)
        {
            movimiento = FindAnyObjectByType<MovimientoJugador>();
            Debug.LogWarning("ControlDeCamaras: faltaba 'movimiento'. Usa Codice > Reconectar referencias del jugador.");
        }
        if (cliente == null)
        {
            cliente = FindAnyObjectByType<ClienteSimulacion>();
        }
        seguimiento = camaraSeguimiento.GetComponent<CamaraSeguimiento>();
        camaraCenital.targetDisplay = 0;
        camaraSeguimiento.targetDisplay = 1;

        // en un build con dos monitores hay que encender el segundo
        if (!Application.isEditor && Display.displays.Length > 1)
        {
            Display.displays[1].Activate();
        }

        for (int d = 0; d < 2; d++)
        {
            ayudas[d] = CrearAyuda(d);
        }
    }

    Ayuda CrearAyuda(int display)
    {
        Ayuda a = new Ayuda();
        Transform canvas = UIFabrica.CrearCanvas("AyudaControles", display, 5, transform).transform;
        // mira en el centro de la pantalla (hacia ahí van las piedras con la cámara de seguimiento)
        Image horizontal = UIFabrica.Caja(canvas, "Mira", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(18f, 2f), new Color(1f, 1f, 1f, 0.85f));
        UIFabrica.Caja(horizontal.transform, "Vertical", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(2f, 18f), new Color(1f, 1f, 1f, 0.85f));
        a.mira = horizontal.gameObject;
        // ayuda abajo a la izquierda
        a.caja = UIFabrica.Caja(canvas, "Ayuda", Vector2.zero, Vector2.zero, new Vector2(20f, 8f), new Vector2(800f, 36f),
            new Color(0.08f, 0.07f, 0.06f, 0.6f));
        a.texto = UIFabrica.TextoLleno(a.caja.transform, "Texto", 20, TextAnchor.MiddleLeft, UIFabrica.ColorTexto);
        a.texto.horizontalOverflow = HorizontalWrapMode.Overflow;
        return a;
    }

    /// <summary>En cada display: la ayuda de la cámara que se ve ahí, y la mira solo con la de seguimiento.</summary>
    void ActualizarAyuda(bool terminada)
    {
        for (int d = 0; d < ayudas.Length; d++)
        {
            Ayuda a = ayudas[d];
            if (a == null)
            {
                continue;
            }
            bool esSeguimiento = camaraSeguimiento.targetDisplay == d;
            a.mira.SetActive(esSeguimiento && !terminada);
            a.caja.gameObject.SetActive(!terminada);
            if (terminada)
            {
                continue;
            }
            string texto = !esSeguimiento ? AyudaCenital
                : (seguimiento.MouseCapturado || !EnSeguimiento ? AyudaSeguimiento : AyudaMouseSuelto);
            a.texto.text = texto;
            a.caja.rectTransform.sizeDelta = new Vector2(a.texto.preferredWidth + 24f, 36f);
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(teclaCambiar))
        {
            int display = camaraCenital.targetDisplay;
            camaraCenital.targetDisplay = camaraSeguimiento.targetDisplay;
            camaraSeguimiento.targetDisplay = display;
        }

        // se juega con la cámara que está en el display que se ve
        displayVisto = DisplayVisto();
        bool antes = EnSeguimiento;
        EnSeguimiento = camaraSeguimiento.targetDisplay == displayVisto;
        if (EnSeguimiento != antes || !avisado)
        {
            avisado = true;
            Debug.Log("Jugando con la cámara " + (EnSeguimiento ? "de SEGUIMIENTO" : "CENITAL") +
                " (Display " + (displayVisto + 1) + ")");
        }
        CamaraActiva = EnSeguimiento ? camaraSeguimiento : camaraCenital;
        seguimiento.Activa = EnSeguimiento;

        // el movimiento es relativo a la cámara de seguimiento, o al norte con la cenital;
        // con la de seguimiento el personaje mira hacia donde mira la cámara (A/D de lado, S hacia atrás)
        if (movimiento != null)
        {
            movimiento.referencia = EnSeguimiento ? camaraSeguimiento.transform : null;
            movimiento.mirarConCamara = EnSeguimiento;
        }

        // mouse siempre activo en la cámara de seguimiento: se captura solo, Esc lo suelta
        bool terminada = cliente != null && cliente.Estado != null && cliente.Estado.over;
        if (EnSeguimiento && !terminada)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                soltadoConEsc = true;
                Capturar(false);
            }
            else if (Input.GetMouseButtonDown(0))
            {
                soltadoConEsc = false;
                Capturar(true);
            }
            else if (!soltadoConEsc && Application.isFocused && Cursor.lockState != CursorLockMode.Locked)
            {
                Capturar(true);                // al entrar a esta cámara o al volver a la ventana
            }
        }
        else if (Cursor.lockState != CursorLockMode.None)
        {
            Capturar(false);                   // en la cenital o al terminar: mouse libre para los botones
        }
        seguimiento.MouseCapturado = Cursor.lockState == CursorLockMode.Locked;
        ActualizarAyuda(terminada);
    }

    void Capturar(bool capturar)
    {
        Cursor.lockState = capturar ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !capturar;
    }

    void OnDisable()
    {
        EnSeguimiento = false;
        CamaraActiva = null;
        Capturar(false);
    }

    /// <summary>Qué display está viendo el jugador (0 = Display 1, 1 = Display 2).</summary>
    int DisplayVisto()
    {
#if UNITY_EDITOR
        // En el editor, Event.displayIndex no sirve (casi siempre llega 0): se le pregunta a la
        // vista Game qué display muestra (propiedad interna PlayModeView.targetDisplay).
        int enEditor = DisplayDeLaVistaGame();
        if (enEditor >= 0)
        {
            return enEditor;
        }
#endif
        // build con varios monitores encendidos: el monitor donde está el mouse
        if (Display.displays.Length > 1 && Display.displays[1].active)
        {
            Vector3 relativo = Display.RelativeMouseAt(Input.mousePosition);
            if (relativo != Vector3.zero)
            {
                return (int)relativo.z;
            }
            return displayVisto;               // sin dato (mouse capturado, etc.): el último conocido
        }
        return 0;                              // un solo monitor: Display 1 (V intercambia las cámaras)
    }

#if UNITY_EDITOR
    static System.Type tipoVistaGame;
    static PropertyInfo propTargetDisplay;
    static EditorWindow ultimaVistaGame;

    static int DisplayDeLaVistaGame()
    {
        if (tipoVistaGame == null)
        {
            tipoVistaGame = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            System.Type playModeView = typeof(EditorWindow).Assembly.GetType("UnityEditor.PlayModeView");
            if (playModeView != null)
            {
                propTargetDisplay = playModeView.GetProperty("targetDisplay",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            }
        }
        if (tipoVistaGame == null || propTargetDisplay == null)
        {
            return -1;
        }
        // la vista Game enfocada; si no, la que tiene el mouse encima; si no, la última usada o cualquiera
        EditorWindow vista = null;
        if (EditorWindow.focusedWindow != null && tipoVistaGame.IsInstanceOfType(EditorWindow.focusedWindow))
        {
            vista = EditorWindow.focusedWindow;
        }
        else if (EditorWindow.mouseOverWindow != null && tipoVistaGame.IsInstanceOfType(EditorWindow.mouseOverWindow))
        {
            vista = EditorWindow.mouseOverWindow;
        }
        else if (ultimaVistaGame != null)
        {
            vista = ultimaVistaGame;
        }
        else
        {
            Object[] vistas = Resources.FindObjectsOfTypeAll(tipoVistaGame);
            vista = vistas.Length > 0 ? (EditorWindow)vistas[0] : null;
        }
        if (vista == null)
        {
            return -1;
        }
        ultimaVistaGame = vista;
        return (int)propTargetDisplay.GetValue(vista);
    }
#endif
}
