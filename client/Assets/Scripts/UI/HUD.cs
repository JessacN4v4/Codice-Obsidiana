using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Interfaz en pantalla (provisional para el graybox), con uGUI en los dos displays:
/// - Panel arriba a la derecha: fase, sospecha (o alerta de los guardias en la huida),
///   alerta del pueblo, trojes, piedras, aliento y objetivo.
/// - Mensajes del servidor arriba al centro, con color según el tipo.
/// - Aviso para quemar el troje abajo al centro.
/// Se crea un Canvas por display (OnGUI solo se ve en el Display 1) con la misma información.
/// Medidas en una pantalla de referencia de 1920 x 1080 que se escala según el alto real.
/// </summary>
public class HUD : MonoBehaviour
{
    public ClienteSimulacion cliente;
    public MovimientoJugador movimiento;
    public InteraccionJugador interaccion;

    [Header("Mensajes")]
    public float duracionMensaje = 3.5f;
    public int maximoMensajes = 3;

    class Mensaje
    {
        public string texto;
        public Color color;
        public float hasta;      // hora en que desaparece
    }

    /// <summary>Los elementos de la interfaz en un display.</summary>
    class Vista
    {
        public GameObject panel;
        public Text fase, sospecha, alertaPueblo, trojes, piedras, aliento, objetivo;
        public Image barraSospecha, barraAliento;
        public Image[] cuadrosAlerta = new Image[3];
        public Image[] cuadrosTrojes = new Image[3];
        public Text[] letrasTrojes = new Text[3];
        public Image[] cajasMensaje;
        public Text[] textosMensaje;
        public Image cajaAviso;
        public Text textoAviso;
        public Image cajaConexion;
        public Text textoConexion;
    }

    private readonly List<Mensaje> mensajes = new List<Mensaje>();
    private readonly List<Vista> vistas = new List<Vista>();

    static readonly Color Apagado = new Color(0.25f, 0.22f, 0.2f);

    void Start()
    {
        cliente.AlRecibirEstado += AlRecibirEstado;
        for (int display = 0; display < 2; display++)
        {
            vistas.Add(CrearVista(display));
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
            if (e.type == "stone")
            {
                continue;        // la onda en el suelo ya lo muestra
            }
            Avisar(e.message, ColorDelEvento(e.type));
        }
    }

    /// <summary>Muestra un mensaje arriba al centro (los del servidor y los del propio cliente).</summary>
    public void Avisar(string texto, Color color)
    {
        Mensaje m = new Mensaje();
        m.texto = texto;
        m.color = color;
        m.hasta = Time.time + duracionMensaje;
        mensajes.Add(m);
        if (mensajes.Count > maximoMensajes)
        {
            mensajes.RemoveAt(0);
        }
    }

    // ---------- construcción ----------
    Vista CrearVista(int display)
    {
        Vista v = new Vista();
        Transform canvas = UIFabrica.CrearCanvas("HUD", display, 0, transform).transform;
        Vector2 arribaIzq = new Vector2(0f, 1f);
        Color texto = UIFabrica.ColorTexto;

        // panel arriba a la derecha
        Image panel = UIFabrica.Caja(canvas, "Panel", new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-20f, -20f), new Vector2(400f, 330f), UIFabrica.ColorCaja);
        v.panel = panel.gameObject;
        Transform p = panel.transform;
        float x = 16f;
        float w = 368f;
        v.fase = UIFabrica.Texto(p, "Fase", arribaIzq, arribaIzq, new Vector2(x, -10f), new Vector2(w, 32f), 24, TextAnchor.MiddleLeft, texto, true);
        v.sospecha = UIFabrica.Texto(p, "Sospecha", arribaIzq, arribaIzq, new Vector2(x, -48f), new Vector2(w, 26f), 20, TextAnchor.MiddleLeft, texto);
        v.barraSospecha = UIFabrica.Barra(p, "BarraSospecha", new Vector2(x, -74f), new Vector2(w, 14f), Color.yellow);
        v.alertaPueblo = UIFabrica.Texto(p, "AlertaPueblo", arribaIzq, arribaIzq, new Vector2(x, -100f), new Vector2(200f, 26f), 20, TextAnchor.MiddleLeft, texto);
        v.alertaPueblo.text = "Alerta del pueblo";
        for (int i = 0; i < 3; i++)
        {
            v.cuadrosAlerta[i] = UIFabrica.Caja(p, "Alerta" + i, arribaIzq, arribaIzq, new Vector2(x + 200f + i * 30f, -105f), new Vector2(20f, 20f), Apagado);
        }
        v.trojes = UIFabrica.Texto(p, "Trojes", arribaIzq, arribaIzq, new Vector2(x, -134f), new Vector2(200f, 26f), 20, TextAnchor.MiddleLeft, texto);
        for (int i = 0; i < 3; i++)
        {
            v.cuadrosTrojes[i] = UIFabrica.Caja(p, "Troje" + i, arribaIzq, arribaIzq, new Vector2(x + 200f + i * 50f, -136f), new Vector2(40f, 24f), Apagado);
            v.letrasTrojes[i] = UIFabrica.TextoLleno(v.cuadrosTrojes[i].transform, "Letra", 18, TextAnchor.MiddleCenter, Color.white, true);
        }
        v.piedras = UIFabrica.Texto(p, "Piedras", arribaIzq, arribaIzq, new Vector2(x, -168f), new Vector2(w, 26f), 20, TextAnchor.MiddleLeft, texto);
        v.aliento = UIFabrica.Texto(p, "Aliento", arribaIzq, arribaIzq, new Vector2(x, -200f), new Vector2(w, 26f), 20, TextAnchor.MiddleLeft, texto);
        v.barraAliento = UIFabrica.Barra(p, "BarraAliento", new Vector2(x, -226f), new Vector2(w, 12f), Color.cyan);
        v.objetivo = UIFabrica.Texto(p, "Objetivo", arribaIzq, arribaIzq, new Vector2(x, -248f), new Vector2(w, 70f), 20, TextAnchor.UpperLeft, texto);

        // mensajes del servidor arriba al centro
        int n = Mathf.Max(1, maximoMensajes);
        v.cajasMensaje = new Image[n];
        v.textosMensaje = new Text[n];
        for (int i = 0; i < n; i++)
        {
            v.cajasMensaje[i] = UIFabrica.Caja(canvas, "Mensaje" + i, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -24f - i * 52f), new Vector2(820f, 46f), UIFabrica.ColorCaja);
            v.textosMensaje[i] = UIFabrica.TextoLleno(v.cajasMensaje[i].transform, "Texto", 24, TextAnchor.MiddleCenter, texto);
        }

        // aviso para quemar el troje abajo al centro
        v.cajaAviso = UIFabrica.Caja(canvas, "AvisoTroje", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 54f), new Vector2(460f, 46f), UIFabrica.ColorCaja);
        v.textoAviso = UIFabrica.TextoLleno(v.cajaAviso.transform, "Texto", 24, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.55f));

        // estado de la conexión al centro
        v.cajaConexion = UIFabrica.Caja(canvas, "Conexion", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(1000f, 60f), UIFabrica.ColorCaja);
        v.textoConexion = UIFabrica.TextoLleno(v.cajaConexion.transform, "Texto", 24, TextAnchor.MiddleCenter, Color.white);
        return v;
    }

    // ---------- actualización ----------
    void Update()
    {
        // quitar los mensajes que ya caducaron
        for (int i = mensajes.Count - 1; i >= 0; i--)
        {
            if (Time.time > mensajes[i].hasta)
            {
                mensajes.RemoveAt(i);
            }
        }
        foreach (Vista v in vistas)
        {
            Actualizar(v);
        }
    }

    void Actualizar(Vista v)
    {
        bool conectado = cliente.Conectado;
        EstadoDatos e = cliente.Estado;
        bool jugando = conectado && e != null && !e.over;   // al terminar, la PantallaFinal ocupa la pantalla

        // conexión
        v.cajaConexion.gameObject.SetActive(!conectado);
        if (!conectado)
        {
            bool error = !string.IsNullOrEmpty(cliente.ErrorConexion);
            v.textoConexion.text = error ? cliente.ErrorConexion : "Conectando con el servidor...";
            v.textoConexion.color = error ? new Color(1f, 0.5f, 0.4f) : Color.white;
        }

        v.panel.SetActive(jugando);
        if (jugando)
        {
            ActualizarPanel(v, e);
        }

        // mensajes
        for (int i = 0; i < v.cajasMensaje.Length; i++)
        {
            bool hay = jugando && i < mensajes.Count;
            v.cajasMensaje[i].gameObject.SetActive(hay);
            if (hay)
            {
                v.textosMensaje[i].text = mensajes[i].texto;
                v.textosMensaje[i].color = mensajes[i].color;
            }
        }

        // aviso del troje
        string aviso = jugando ? TextoAvisoTroje() : "";
        v.cajaAviso.gameObject.SetActive(aviso != "");
        v.textoAviso.text = aviso;
    }

    void ActualizarPanel(Vista v, EstadoDatos e)
    {
        v.fase.text = e.phase == 1 ? "FASE 1 · INFILTRACIÓN" : "FASE 2 · HUIDA";
        v.fase.color = e.phase == 1 ? new Color(0.95f, 0.85f, 0.6f) : new Color(1f, 0.45f, 0.35f);

        if (e.phase == 1)
        {
            float s = e.suspicion / 100f;
            v.sospecha.text = "Sospecha: " + Mathf.RoundToInt(e.suspicion);
            UIFabrica.PonerBarra(v.barraSospecha, s, Color.Lerp(new Color(0.95f, 0.8f, 0.2f), new Color(0.9f, 0.15f, 0.1f), s));
        }
        else
        {
            float maxima = AlertaMaximaDeGuardias(e);
            v.sospecha.text = "Alerta de los guardias: " + Mathf.RoundToInt(maxima);
            UIFabrica.PonerBarra(v.barraSospecha, maxima / 100f, new Color(0.9f, 0.15f, 0.1f));
        }

        for (int i = 0; i < 3; i++)
        {
            v.cuadrosAlerta[i].color = i < e.alert ? new Color(0.9f, 0.3f, 0.15f) : Apagado;
        }

        v.trojes.text = "Trojes " + e.burned + "/3";
        for (int i = 0; i < 3; i++)
        {
            bool hay = e.trojes != null && i < e.trojes.Length;
            v.cuadrosTrojes[i].gameObject.SetActive(hay);
            if (hay)
            {
                v.cuadrosTrojes[i].color = e.trojes[i].burning ? new Color(1f, 0.35f, 0.1f) : Apagado;
                v.letrasTrojes[i].text = e.trojes[i].id;
            }
        }

        v.piedras.text = "Piedras: " + e.stones + "   (Q o clic derecho)";
        v.aliento.text = movimiento.Agotado ? "Aliento (agotado)" : "Aliento   (Shift para correr)";
        Color colorAliento = movimiento.Agotado ? new Color(0.8f, 0.3f, 0.2f) : new Color(0.4f, 0.7f, 0.95f);
        UIFabrica.PonerBarra(v.barraAliento, movimiento.Aliento / movimiento.alientoMaximo, colorAliento);
        v.objetivo.text = Objetivo(e);
    }

    string TextoAvisoTroje()
    {
        string id = interaccion.TrojeCercano;
        if (id == "")
        {
            return "";
        }
        if (interaccion.Quemando)
        {
            TrojeEstado t = interaccion.EstadoDelTroje(id);
            float progreso = t != null ? t.progress : 0f;
            return "Quemando el troje " + id + "... " + Mathf.RoundToInt(progreso * 100f) + "%";
        }
        return "Mantén E para quemar el troje " + id;
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
}
