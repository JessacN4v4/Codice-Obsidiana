using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Indicadores en la pantalla del Display 1 para orientarse: trojes que faltan quemar y la canoa.
/// Si el punto queda fuera de la pantalla, en la orilla sale su ícono con una flechita que apunta
/// hacia él: rombo naranja con la letra del troje (A, B, C) u óvalo verde para la canoa.
/// Si se ve en pantalla no sale nada (ya se ve su marca 3D).
/// En la fase 1 la canoa se ve más tenue (primero van los trojes). Los trojes quemados ya no se indican.
/// Se calcula después de que se movieron las cámaras (orden de ejecución 1000) y con el punto fijo de
/// cada marca (sin su flotar), para que los indicadores no tiemblen al moverse.
/// Lo crea el HUD al empezar.
/// </summary>
[DefaultExecutionOrder(1000)]
public class IndicadoresPantalla : MonoBehaviour
{
    public ClienteSimulacion cliente;
    public MarcasTrojes marcas;

    public Color colorTroje = new Color(1f, 0.55f, 0.15f);
    public Color colorCanoa = new Color(0.25f, 0.9f, 0.35f);
    public float margenLados = 50f;            // px de referencia desde la orilla
    public float margenArriba = 50f;
    public float margenAbajo = 120f;           // deja libre el aviso de "Mantén E"

    const int Display = 0;                     // solo el Display 1
    static readonly Color Oscuro = new Color(0.08f, 0.06f, 0.05f, 0.9f);

    class Indicador
    {
        public string id;                      // "A", "B", "C" o "" para la canoa
        public RectTransform raiz;
        public CanvasGroup grupo;              // para hacerlo más tenue
        public RectTransform punta;
    }

    private Canvas canvas;
    private readonly List<Indicador> indicadores = new List<Indicador>();

    void Start()
    {
        canvas = UIFabrica.CrearCanvas("Indicadores", Display, -1, transform);   // debajo del HUD
        foreach (string id in new[] { "A", "B", "C" })
        {
            indicadores.Add(Crear(id, colorTroje));
        }
        indicadores.Add(Crear("", colorCanoa));
    }

    // ---------- crear ----------
    Indicador Crear(string id, Color color)
    {
        Indicador ind = new Indicador();
        ind.id = id;
        Vector2 centro = new Vector2(0.5f, 0.5f);
        Image raiz = UIFabrica.Caja(canvas.transform, id != "" ? "Indicador_Troje" + id : "Indicador_Canoa",
            centro, centro, Vector2.zero, new Vector2(1f, 1f), Color.clear);
        ind.raiz = raiz.rectTransform;
        ind.grupo = raiz.gameObject.AddComponent<CanvasGroup>();
        Transform r = ind.raiz;

        // flechita: un cuadro girado que asoma por el lado hacia donde está el punto
        Image punta = UIFabrica.Caja(r, "Flecha", centro, centro, Vector2.zero, new Vector2(18f, 18f), color);
        ind.punta = punta.rectTransform;

        if (id != "")
        {
            // troje: rombo naranja con borde oscuro y la letra (como el rombo 3D)
            Image borde = UIFabrica.Caja(r, "Borde", centro, centro, Vector2.zero, new Vector2(40f, 40f), Oscuro);
            borde.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Image rombo = UIFabrica.Caja(r, "Rombo", centro, centro, Vector2.zero, new Vector2(33f, 33f), color);
            rombo.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Text letra = UIFabrica.Texto(r, "Letra", centro, centro, Vector2.zero, new Vector2(50f, 50f), 24, TextAnchor.MiddleCenter, Color.white, true);
            letra.text = id;
            letra.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.7f);
        }
        else
        {
            // canoa: óvalo verde con borde oscuro (como el óvalo 3D)
            Image borde = UIFabrica.Caja(r, "Borde", centro, centro, Vector2.zero, new Vector2(34f, 48f), Oscuro);
            borde.sprite = UIFabrica.Circulo;
            Image ovalo = UIFabrica.Caja(r, "Ovalo", centro, centro, Vector2.zero, new Vector2(27f, 41f), color);
            ovalo.sprite = UIFabrica.Circulo;
        }
        return ind;
    }

    // ---------- cada frame ----------
    void LateUpdate()   // después de que se movieron las cámaras
    {
        EstadoDatos e = cliente != null ? cliente.Estado : null;
        Camera camara = CanvasDeDisplay.CamaraDelDisplay(Display);
        bool activo = e != null && cliente.Conectado && !e.over && !PantallaMision.Abierta && camara != null && marcas != null;
        canvas.gameObject.SetActive(activo);
        if (!activo)
        {
            return;
        }

        Rect pantalla = ((RectTransform)canvas.transform).rect;
        foreach (Indicador ind in indicadores)
        {
            bool esCanoa = ind.id == "";
            Vector3 objetivo;
            bool hay = esCanoa ? marcas.PuntoCanoa(out objetivo) : marcas.PuntoTroje(ind.id, out objetivo);
            bool mostrar = hay && (esCanoa || !marcas.TrojeQuemado(ind.id));
            // solo se muestra si queda fuera de la pantalla
            mostrar = mostrar && Actualizar(ind, objetivo, camara, pantalla);
            ind.raiz.gameObject.SetActive(mostrar);
            ind.grupo.alpha = esCanoa && e.phase == 1 ? 0.6f : 1f;   // la canoa es lo segundo en la fase 1
        }
    }

    /// <summary>Lo coloca en la orilla apuntando al punto. false si el punto se ve en pantalla.</summary>
    bool Actualizar(Indicador ind, Vector3 objetivo, Camera camara, Rect pantalla)
    {
        // dónde queda en la pantalla (en coordenadas del Canvas, con el centro en 0,0)
        Vector3 enPantalla = camara.WorldToScreenPoint(objetivo);
        bool detras = enPantalla.z < 0f;
        Vector2 pos;
        Camera camaraDelCanvas = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform, enPantalla, camaraDelCanvas, out pos);
        if (detras)
        {
            pos = -pos;                        // detrás de la cámara la pantalla lo da al revés
        }

        float izquierda = -pantalla.width / 2f + margenLados;
        float derecha = pantalla.width / 2f - margenLados;
        float abajo = -pantalla.height / 2f + margenAbajo;
        float arriba = pantalla.height / 2f - margenArriba;
        bool fuera = detras || pos.x < izquierda || pos.x > derecha || pos.y < abajo || pos.y > arriba;
        if (!fuera)
        {
            return false;
        }

        // pegado a la orilla, en la dirección del punto desde el centro de la pantalla
        Vector2 dir = pos.sqrMagnitude > 0.01f ? pos.normalized : Vector2.down;
        float escala = Mathf.Min(
            (dir.x >= 0f ? derecha : -izquierda) / Mathf.Max(Mathf.Abs(dir.x), 0.0001f),
            (dir.y >= 0f ? arriba : -abajo) / Mathf.Max(Mathf.Abs(dir.y), 0.0001f));
        Vector2 enOrilla = dir * escala;
        // el panel del HUD ocupa la esquina de arriba a la derecha: bajarlo para que no quede tapado
        float bordePanel = pantalla.height / 2f - 20f - 330f - 40f;
        if (enOrilla.x > pantalla.width / 2f - 20f - 400f - 30f && enOrilla.y > bordePanel)
        {
            enOrilla = new Vector2(derecha, bordePanel);
        }
        ind.raiz.anchoredPosition = new Vector2(Mathf.Round(enOrilla.x), Mathf.Round(enOrilla.y));   // píxeles enteros

        // una esquina del cuadro apunta hacia el punto (las esquinas de un cuadro sin girar están a 45°)
        ind.punta.anchoredPosition = dir * 30f;
        ind.punta.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 45f);
        return true;
    }
}
