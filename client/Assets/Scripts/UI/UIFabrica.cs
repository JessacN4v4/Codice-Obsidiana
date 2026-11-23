using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Ayudantes para construir la interfaz con uGUI desde código.
/// Se usa uGUI (Canvas) y no OnGUI porque OnGUI solo se dibuja en el Display 1;
/// se crea un Canvas por display con la misma información. Cada Canvas va pegado a la cámara de su
/// display (CanvasDeDisplay, modo Screen Space - Camera) y en la capa UI, para que no quede como un
/// rectángulo gigante sobre el pueblo.
/// Medidas en una pantalla de referencia de 1920 x 1080 que se escala según el alto real.
/// </summary>
public static class UIFabrica
{
    public static readonly Color ColorCaja = new Color(0.08f, 0.07f, 0.06f, 0.78f);
    public static readonly Color ColorTexto = new Color(0.95f, 0.9f, 0.82f);

    private static Font fuente;

    public static Font Fuente
    {
        get
        {
            if (fuente == null)
            {
                fuente = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            return fuente;
        }
    }

    private static int capaUI = -1;

    /// <summary>La capa "UI" (en la vista Scene se puede ocultar desde el menú Layers).</summary>
    static int CapaUI
    {
        get
        {
            if (capaUI < 0)
            {
                int capa = LayerMask.NameToLayer("UI");
                capaUI = capa >= 0 ? capa : 0;
            }
            return capaUI;
        }
    }

    /// <summary>Canvas de pantalla completa en el display indicado (0 = Display 1, 1 = Display 2),
    /// pegado a la cámara de ese display.</summary>
    public static Canvas CrearCanvas(string nombre, int display, int orden, Transform padre, bool recibeClics = false)
    {
        GameObject go = new GameObject(nombre + "_Display" + (display + 1));
        go.layer = CapaUI;
        go.transform.SetParent(padre, false);
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.targetDisplay = display;
        canvas.sortingOrder = orden;
        go.AddComponent<CanvasDeDisplay>().display = display;   // lo pasa a la cámara de su display
        CanvasScaler escala = go.AddComponent<CanvasScaler>();
        escala.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escala.referenceResolution = new Vector2(1920f, 1080f);
        escala.matchWidthOrHeight = 1f;        // escalar según el alto, como antes con OnGUI
        if (recibeClics)
        {
            go.AddComponent<GraphicRaycaster>();
        }
        return canvas;
    }

    /// <summary>
    /// Rectángulo de color. ancla y pivote en 0..1 (0,1 = arriba a la izquierda);
    /// pos en píxeles de referencia desde el ancla (y negativa = hacia abajo).
    /// </summary>
    public static Image Caja(Transform padre, string nombre, Vector2 ancla, Vector2 pivote, Vector2 pos, Vector2 tam, Color color)
    {
        GameObject go = new GameObject(nombre, typeof(RectTransform));
        go.layer = CapaUI;
        go.transform.SetParent(padre, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = ancla;
        rt.anchorMax = ancla;
        rt.pivot = pivote;
        rt.anchoredPosition = pos;
        rt.sizeDelta = tam;
        Image img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    /// <summary>Rectángulo que ocupa todo su padre.</summary>
    public static Image Fondo(Transform padre, string nombre, Color color)
    {
        Image img = Caja(padre, nombre, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, color);
        RectTransform rt = img.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return img;
    }

    public static Text Texto(Transform padre, string nombre, Vector2 ancla, Vector2 pivote, Vector2 pos, Vector2 tam,
                             int tamLetra, TextAnchor alineacion, Color color, bool negrita = false)
    {
        GameObject go = new GameObject(nombre, typeof(RectTransform));
        go.layer = CapaUI;
        go.transform.SetParent(padre, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = ancla;
        rt.anchorMax = ancla;
        rt.pivot = pivote;
        rt.anchoredPosition = pos;
        rt.sizeDelta = tam;
        Text t = go.AddComponent<Text>();
        t.font = Fuente;
        t.fontSize = tamLetra;
        t.fontStyle = negrita ? FontStyle.Bold : FontStyle.Normal;
        t.alignment = alineacion;
        t.color = color;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    /// <summary>Texto que ocupa todo su padre (para centrar dentro de una caja).</summary>
    public static Text TextoLleno(Transform padre, string nombre, int tamLetra, TextAnchor alineacion, Color color, bool negrita = false)
    {
        Text t = Texto(padre, nombre, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, tamLetra, alineacion, color, negrita);
        RectTransform rt = t.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(10f, 4f);
        rt.offsetMax = new Vector2(-10f, -4f);
        return t;
    }

    /// <summary>Barra de progreso: devuelve la imagen del relleno (se ajusta con PonerBarra).</summary>
    public static Image Barra(Transform padre, string nombre, Vector2 pos, Vector2 tam, Color color)
    {
        Image fondo = Caja(padre, nombre, new Vector2(0f, 1f), new Vector2(0f, 1f), pos, tam, new Color(0.15f, 0.12f, 0.1f, 0.9f));
        Image relleno = Fondo(fondo.transform, "Relleno", color);
        return relleno;
    }

    public static void PonerBarra(Image relleno, float valor, Color color)
    {
        RectTransform rt = relleno.rectTransform;
        rt.anchorMax = new Vector2(Mathf.Clamp01(valor), 1f);
        relleno.color = color;
    }

    public static Button Boton(Transform padre, string nombre, Vector2 ancla, Vector2 pos, Vector2 tam, string texto,
                               UnityEngine.Events.UnityAction alPresionar)
    {
        Image img = Caja(padre, nombre, ancla, new Vector2(0.5f, 0.5f), pos, tam, new Color(0.24f, 0.19f, 0.14f, 1f));
        img.raycastTarget = true;
        Button b = img.gameObject.AddComponent<Button>();
        ColorBlock colores = b.colors;
        colores.highlightedColor = new Color(1.5f, 1.4f, 1.25f);   // se aclara al pasar el mouse
        colores.pressedColor = new Color(0.8f, 0.75f, 0.7f);
        colores.selectedColor = colores.highlightedColor;
        b.colors = colores;
        b.onClick.AddListener(alPresionar);
        // borde claro para que el botón se distinga de la caja de atrás
        Outline borde = img.gameObject.AddComponent<Outline>();
        borde.effectColor = new Color(0.95f, 0.85f, 0.6f, 0.8f);
        borde.effectDistance = new Vector2(2f, -2f);
        Text etiqueta = TextoLleno(img.transform, "Texto", 28, TextAnchor.MiddleCenter, Color.white, true);
        etiqueta.text = texto;
        return b;
    }

    /// <summary>Los botones de uGUI necesitan un EventSystem en la escena; se crea si no hay.</summary>
    public static void AsegurarEventSystem()
    {
        if (Object.FindAnyObjectByType<EventSystem>() != null)
        {
            return;
        }
        GameObject go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<InputSystemUIInputModule>();   // el proyecto usa el Input System (y el Input clásico)
    }
}
