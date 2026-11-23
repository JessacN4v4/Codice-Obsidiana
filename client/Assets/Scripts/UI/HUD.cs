using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Interfaz en pantalla (provisional para el graybox), dibujada con OnGUI:
/// - Panel arriba a la derecha: fase, sospecha (o alerta de los guardias en la huida),
///   alerta del pueblo, trojes, piedras, aliento y objetivo.
/// - Mensajes del servidor arriba al centro, con color según el tipo.
/// - Aviso para quemar el troje abajo al centro.
/// Todo se dibuja en una pantalla "virtual" de 1080 px de alto y se escala a la real.
/// </summary>
public class HUD : MonoBehaviour
{
    public ClienteSimulacion cliente;
    public MovimientoJugador movimiento;
    public InteraccionJugador interaccion;

    [Header("Mensajes")]
    public float duracionMensaje = 3.5f;
    public int maximoMensajes = 3;

    const float AltoBase = 1080f;

    class Mensaje
    {
        public string texto;
        public Color color;
        public float hasta;      // hora en que desaparece
    }

    private List<Mensaje> mensajes = new List<Mensaje>();
    private GUIStyle estiloTexto;
    private GUIStyle estiloTitulo;
    private GUIStyle estiloMensaje;
    private GUIStyle estiloAviso;

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
            if (e.type == "stone")
            {
                continue;        // la onda en el suelo ya lo muestra
            }
            Mensaje m = new Mensaje();
            m.texto = e.message;
            m.color = ColorDelEvento(e.type);
            m.hasta = Time.time + duracionMensaje;
            mensajes.Add(m);
            if (mensajes.Count > maximoMensajes)
            {
                mensajes.RemoveAt(0);
            }
        }
    }

    // ---------- dibujo ----------
    void OnGUI()
    {
        PrepararEstilos();
        float escala = Screen.height / AltoBase;
        GUI.matrix = Matrix4x4.Scale(new Vector3(escala, escala, 1f));
        float ancho = Screen.width / escala;   // ancho de la pantalla virtual

        if (!cliente.Conectado)
        {
            DibujarConexion(ancho);
            return;
        }
        EstadoDatos estado = cliente.Estado;
        if (estado == null || estado.over)
        {
            return;      // al terminar, la PantallaFinal ocupa la pantalla
        }
        DibujarPanel(estado, ancho);
        DibujarMensajes(ancho);
        DibujarAvisoTroje(ancho);
    }

    void DibujarPanel(EstadoDatos e, float anchoPantalla)
    {
        float w = 400f;
        float x = anchoPantalla - w - 20f;     // pegado al lado derecho
        float y = 20f;
        GUI.Box(new Rect(x, y, w, 330f), "");
        x += 16f;
        y += 10f;
        float anchoBarra = w - 32f;

        string fase = e.phase == 1 ? "FASE 1 · INFILTRACIÓN" : "FASE 2 · HUIDA";
        estiloTitulo.normal.textColor = e.phase == 1 ? new Color(0.95f, 0.85f, 0.6f) : new Color(1f, 0.45f, 0.35f);
        GUI.Label(new Rect(x, y, anchoBarra, 32f), fase, estiloTitulo);
        y += 38f;

        if (e.phase == 1)
        {
            GUI.Label(new Rect(x, y, anchoBarra, 26f), "Sospecha: " + Mathf.RoundToInt(e.suspicion), estiloTexto);
            y += 26f;
            float s = e.suspicion / 100f;
            Barra(new Rect(x, y, anchoBarra, 14f), s, Color.Lerp(new Color(0.95f, 0.8f, 0.2f), new Color(0.9f, 0.15f, 0.1f), s));
        }
        else
        {
            float maxima = AlertaMaximaDeGuardias(e);
            GUI.Label(new Rect(x, y, anchoBarra, 26f), "Alerta de los guardias: " + Mathf.RoundToInt(maxima), estiloTexto);
            y += 26f;
            Barra(new Rect(x, y, anchoBarra, 14f), maxima / 100f, new Color(0.9f, 0.15f, 0.1f));
        }
        y += 26f;

        GUI.Label(new Rect(x, y, 200f, 26f), "Alerta del pueblo", estiloTexto);
        for (int i = 0; i < 3; i++)
        {
            Color c = i < e.alert ? new Color(0.9f, 0.3f, 0.15f) : new Color(0.25f, 0.22f, 0.2f);
            Cuadro(new Rect(x + 200f + i * 30f, y + 5f, 20f, 20f), c);
        }
        y += 34f;

        GUI.Label(new Rect(x, y, 200f, 26f), "Trojes " + e.burned + "/3", estiloTexto);
        for (int i = 0; i < e.trojes.Length; i++)
        {
            Color c = e.trojes[i].burning ? new Color(1f, 0.35f, 0.1f) : new Color(0.25f, 0.22f, 0.2f);
            Rect r = new Rect(x + 200f + i * 50f, y + 2f, 40f, 24f);
            Cuadro(r, c);
            GUI.Label(r, e.trojes[i].id, estiloAviso);
        }
        y += 34f;

        GUI.Label(new Rect(x, y, anchoBarra, 26f), "Piedras: " + e.stones + "   (Q o clic derecho)", estiloTexto);
        y += 32f;

        string aliento = movimiento.Agotado ? "Aliento (agotado)" : "Aliento   (Shift para correr)";
        GUI.Label(new Rect(x, y, anchoBarra, 26f), aliento, estiloTexto);
        y += 26f;
        Color colorAliento = movimiento.Agotado ? new Color(0.8f, 0.3f, 0.2f) : new Color(0.4f, 0.7f, 0.95f);
        Barra(new Rect(x, y, anchoBarra, 12f), movimiento.Aliento / movimiento.alientoMaximo, colorAliento);
        y += 22f;

        GUI.Label(new Rect(x, y, anchoBarra, 56f), Objetivo(e), estiloTexto);
    }

    void DibujarMensajes(float ancho)
    {
        // quitar los que ya caducaron
        for (int i = mensajes.Count - 1; i >= 0; i--)
        {
            if (Time.time > mensajes[i].hasta)
            {
                mensajes.RemoveAt(i);
            }
        }
        float w = 820f;
        float y = 24f;
        foreach (Mensaje m in mensajes)
        {
            estiloMensaje.normal.textColor = m.color;
            GUI.Box(new Rect((ancho - w) / 2f, y, w, 46f), m.texto, estiloMensaje);
            y += 52f;
        }
    }

    void DibujarAvisoTroje(float ancho)
    {
        string id = interaccion.TrojeCercano;
        if (id == "")
        {
            return;
        }
        string texto;
        if (interaccion.Quemando)
        {
            TrojeEstado t = interaccion.EstadoDelTroje(id);
            float progreso = t != null ? t.progress : 0f;
            texto = "Quemando el troje " + id + "... " + Mathf.RoundToInt(progreso * 100f) + "%";
        }
        else
        {
            texto = "Mantén E para quemar el troje " + id;
        }
        float w = 460f;
        estiloMensaje.normal.textColor = new Color(1f, 0.85f, 0.55f);
        GUI.Box(new Rect((ancho - w) / 2f, AltoBase - 100f, w, 46f), texto, estiloMensaje);
    }

    void DibujarConexion(float ancho)
    {
        string texto = string.IsNullOrEmpty(cliente.ErrorConexion)
            ? "Conectando con el servidor..."
            : cliente.ErrorConexion;
        float w = 1000f;
        estiloMensaje.normal.textColor = string.IsNullOrEmpty(cliente.ErrorConexion) ? Color.white : new Color(1f, 0.5f, 0.4f);
        GUI.Box(new Rect((ancho - w) / 2f, AltoBase / 2f - 30f, w, 60f), texto, estiloMensaje);
    }

    // ---------- ayudantes ----------
    string Objetivo(EstadoDatos e)
    {
        if (e.phase == 1)
        {
            return "Objetivo: quema el troje de cada almacén (A, B y C).";
        }
        if (e.burned < 3)
        {
            return "Objetivo: quema los trojes que faltan y huye a la canoa.";
        }
        return "Objetivo: huye a la canoa (suroeste). Distrae a su guardia con una piedra.";
    }

    float AlertaMaximaDeGuardias(EstadoDatos e)
    {
        float maxima = 0f;
        foreach (GuardiaDatos g in e.guards)
        {
            maxima = Mathf.Max(maxima, g.awareness);
        }
        return maxima;
    }

    Color ColorDelEvento(string tipo)
    {
        if (tipo == "detected" || tipo == "alarm" || tipo == "captured")
        {
            return new Color(1f, 0.45f, 0.4f);      // rojo: peligro
        }
        if (tipo == "fire")
        {
            return new Color(1f, 0.7f, 0.4f);       // naranja: fuego
        }
        if (tipo == "win")
        {
            return new Color(0.5f, 1f, 0.55f);      // verde: victoria
        }
        return new Color(1f, 0.9f, 0.6f);           // amarillo: avisos
    }

    void Barra(Rect r, float valor, Color color)
    {
        Cuadro(r, new Color(0.15f, 0.12f, 0.1f, 0.9f));                                       // fondo
        Cuadro(new Rect(r.x, r.y, r.width * Mathf.Clamp01(valor), r.height), color);          // relleno
    }

    void Cuadro(Rect r, Color color)
    {
        GUI.color = color;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    void PrepararEstilos()
    {
        if (estiloTexto != null)
        {
            return;      // los estilos se crean una sola vez
        }
        estiloTexto = new GUIStyle(GUI.skin.label);
        estiloTexto.fontSize = 20;
        estiloTexto.wordWrap = true;
        estiloTexto.normal.textColor = new Color(0.95f, 0.9f, 0.82f);

        estiloTitulo = new GUIStyle(GUI.skin.label);
        estiloTitulo.fontSize = 24;
        estiloTitulo.fontStyle = FontStyle.Bold;

        estiloMensaje = new GUIStyle(GUI.skin.box);
        estiloMensaje.fontSize = 24;
        estiloMensaje.alignment = TextAnchor.MiddleCenter;
        estiloMensaje.wordWrap = true;

        estiloAviso = new GUIStyle(GUI.skin.label);
        estiloAviso.fontSize = 18;
        estiloAviso.fontStyle = FontStyle.Bold;
        estiloAviso.alignment = TextAnchor.MiddleCenter;
        estiloAviso.normal.textColor = Color.white;
    }
}
