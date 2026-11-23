using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Dibuja lo que manda el servidor: guardias (con su cono de visión), aldeanos y cargadores.
/// El servidor manda posiciones 10 veces por segundo; aquí se mueven suavemente entre
/// la posición anterior y la nueva (interpolación) para que no se vean a saltos.
/// También pinta los trojes según su progreso y los pone rojos cuando arden.
///
/// Conos de visión: cada frame se lanzan rayos desde el guardia dentro de su cono; cada rayo se
/// corta en el primer obstáculo que tapa la vista, así el cono queda "en sombra" detrás de muros,
/// casas, trojes y la yácata. Se usan los mismos obstáculos (GET /map) y el mismo cálculo que el
/// servidor, para que lo que se ve coincida con lo que el guardia de verdad ve.
/// Además de los rayos parejos, se lanzan rayos justo a los lados de cada esquina de los
/// obstáculos, para que el borde de la sombra salga recto y no en escalones.
///
/// La sospecha se pinta en el cono de naranja, desde el guardia hacia el borde del alcance
/// (de rojo cuando ya te persigue):
/// - Fase 1: solo en los guardias que te están viendo, hasta donde va la sospecha del pueblo.
/// - Fase 2: cada guardia con su propia alerta. Si te persigue, el cono queda rojo completo.
/// </summary>
public class VistaAgentes : MonoBehaviour
{
    public ClienteSimulacion cliente;

    [Header("Materiales (los crea el menú Codice > Conectar con el servidor)")]
    public Material materialGuardia;
    public Material materialGuardiaCanoa;
    public Material materialAldeano;
    public Material materialCargador;
    public Material materialCono;          // transparente

    [Header("Modelos 3D (los asigna el menú Codice > Configurar personajes)")]
    public GameObject modeloGuardia;
    public GameObject modeloGuardiaCanoa;
    public GameObject modeloCargador;
    public GameObject[] modelosAldeanos;   // se reparten entre los aldeanos de la plaza
    public RuntimeAnimatorController controlador;   // parado / caminar / correr

    [Header("Color del cono y de la sospecha que se pinta en él")]
    public Color conoTranquilo = new Color(1f, 0.95f, 0.7f, 0.18f);
    public Color colorSospecha = new Color(1f, 0.5f, 0.05f, 0.5f);      // naranja mientras sube la sospecha
    public Color colorPersecucion = new Color(1f, 0.1f, 0.05f, 0.5f);   // rojo cuando ya te persigue
    public float velocidadRelleno = 1.5f;  // qué tan rápido sigue el rojo a la sospecha (fracción del cono por segundo)

    [Header("Trojes")]
    public Color trojeNormal = new Color(0.85f, 0.45f, 0.15f);
    public Color trojeArdiendo = new Color(1f, 0.15f, 0.05f);

    [Header("Forma del cono")]
    public float gradosPorRayo = 1.5f;     // más chico = borde más suave (y más cálculo)
    [Range(0f, 1f)]
    public float alfaAlFinal = 0.3f;       // transparencia en el borde del alcance (1 = igual que junto al guardia)
    public float alturaCono = 0.06f;       // metros sobre el suelo

    const float MedioAnguloCono = 0.72f;   // radianes, igual que en el servidor (FOV_HALF_ANGLE)
    const float Epsilon = 0.0005f;         // radianes: los rayos a los lados de cada esquina

    // Lo que se guarda de cada agente dibujado
    class Visual
    {
        public GameObject raiz;
        public Vector3 desde;
        public Vector3 hacia;
        public Quaternion girarDesde;
        public Quaternion girarHacia;
        public Mesh mallaCono;             // solo guardias: se rehace cada frame
        public Material materialCono;      // copia propia para pintarle su sospecha
        public float sospecha;             // lo que se ve pintado en el cono (0 a 1)
        public float sospechaObjetivo;     // lo que dice el servidor (0 a 1)
        public bool persiguiendo;          // el cono se pinta de rojo en vez de naranja
    }

    private Dictionary<int, Visual> guardias = new Dictionary<int, Visual>();
    private Dictionary<int, Visual> aldeanos = new Dictionary<int, Visual>();

    private Dictionary<string, TrojeModelo> modelosTroje = new Dictionary<string, TrojeModelo>();

    // obstáculos que tapan la vista, en coordenadas del mapa (px); vacío hasta que llega GET /map
    private List<Rect> obstaculos = new List<Rect>();
    private float alcancePixeles = 185f;
    private GameObject raizConos;          // los conos van en coordenadas del mundo, fuera de los guardias

    // listas que se reutilizan cada frame (para no crear basura)
    private List<float> angulos = new List<float>();
    private List<Vector3> vertices = new List<Vector3>();
    private List<Color> colores = new List<Color>();
    private List<int> triangulos = new List<int>();

    void Start()
    {
        // modelos 3D de los trojes (si ya se colocó la ambientación)
        foreach (TrojeModelo modelo in FindObjectsByType<TrojeModelo>())
        {
            modelosTroje[modelo.id] = modelo;
        }
        raizConos = new GameObject("ConosDeVision");
        cliente.AlRecibirEstado += AlRecibirEstado;
        StartCoroutine(CargarObstaculos());
    }

    /// <summary>Pide el mapa al servidor (reintenta cada 2 s si aún no responde) y guarda los obstáculos.</summary>
    IEnumerator CargarObstaculos()
    {
        while (true)
        {
            using (UnityWebRequest peticion = UnityWebRequest.Get(cliente.urlServidor + "/map"))
            {
                yield return peticion.SendWebRequest();
                if (peticion.result == UnityWebRequest.Result.Success)
                {
                    MapaDatos mapa = JsonUtility.FromJson<MapaDatos>(peticion.downloadHandler.text);
                    foreach (ObstaculoDatos o in mapa.obstacles)
                    {
                        if (o.sight)
                        {
                            obstaculos.Add(new Rect(o.rect.x, o.rect.y, o.rect.w, o.rect.h));
                        }
                    }
                    Debug.Log("Conos de visión: " + obstaculos.Count + " obstáculos que tapan la vista.");
                    yield break;
                }
            }
            yield return new WaitForSeconds(2f);
        }
    }

    void OnDestroy()
    {
        if (cliente != null)
        {
            cliente.AlRecibirEstado -= AlRecibirEstado;
        }
    }

    // ---------- cuando llega un estado nuevo ----------
    void AlRecibirEstado(EstadoDatos estado)
    {
        // alcance de la vista en px: crece con la alerta del pueblo en fase 1 (igual que vision_range)
        alcancePixeles = estado.phase == 1 ? 185f + 20f * estado.alert : 250f;

        foreach (GuardiaDatos g in estado.guards)
        {
            Visual v;
            if (!guardias.TryGetValue(g.id, out v))
            {
                v = CrearGuardia(g);
                guardias[g.id] = v;
            }
            NuevoDestino(v, g.x, g.y, g.face);
            v.sospechaObjetivo = SospechaDelGuardia(g, estado);
            v.persiguiendo = g.state == "chase";
        }

        foreach (AldeanoDatos a in estado.villagers)
        {
            Visual v;
            if (!aldeanos.TryGetValue(a.id, out v))
            {
                v = CrearAldeano(a);
                aldeanos[a.id] = v;
            }
            NuevoDestino(v, a.x, a.y, a.face);
        }

        foreach (TrojeEstado t in estado.trojes)
        {
            PintarTroje(t);
        }
    }

    /// <summary>Hasta dónde se pinta de rojo el cono de este guardia (0 a 1).</summary>
    float SospechaDelGuardia(GuardiaDatos g, EstadoDatos estado)
    {
        if (g.state == "chase")
        {
            return 1f;
        }
        if (estado.phase == 1)
        {
            return g.seeing ? estado.suspicion / 100f : 0f;   // la sospecha es del pueblo: se ve en quien te mira
        }
        return g.awareness / 100f;
    }

    void NuevoDestino(Visual v, float x, float y, float angulo)
    {
        v.desde = v.raiz.transform.position;
        v.hacia = Mapa.AMundo(x, y);
        v.girarDesde = v.raiz.transform.rotation;
        v.girarHacia = Quaternion.Euler(0f, Mapa.AnguloAGrados(angulo), 0f);
    }

    // ---------- cada frame: moverlos suavemente ----------
    void Update()
    {
        if (cliente == null || cliente.Estado == null)
        {
            return;
        }
        // t va de 0 (llegó el estado) a 1 (ya debería llegar el siguiente)
        float t = Mathf.Clamp01((Time.time - cliente.HoraUltimoEstado) / cliente.intervalo);
        foreach (Visual v in guardias.Values)
        {
            Mover(v, t);
        }
        foreach (Visual v in aldeanos.Values)
        {
            Mover(v, t);
        }
    }

    void Mover(Visual v, float t)
    {
        v.raiz.transform.position = Vector3.Lerp(v.desde, v.hacia, t);
        v.raiz.transform.rotation = Quaternion.Slerp(v.girarDesde, v.girarHacia, t);
    }

    // ---------- conos de visión (después de mover a los guardias) ----------
    void LateUpdate()
    {
        foreach (Visual v in guardias.Values)
        {
            ActualizarCono(v);
        }
    }

    void ActualizarCono(Visual v)
    {
        // posición y hacia dónde mira, en coordenadas del mapa (px, y hacia abajo)
        Vector2 origen = Mapa.AMapa(v.raiz.transform.position);
        Vector3 adelante = v.raiz.transform.forward;
        float mira = Mathf.Atan2(-adelante.z, adelante.x);   // en el mapa y va hacia abajo (= -z)
        float R = alcancePixeles;

        // 1. ángulos de los rayos (respecto a hacia donde mira): parejos de un lado al otro del cono...
        angulos.Clear();
        int pasos = Mathf.Max(2, Mathf.CeilToInt(2f * MedioAnguloCono * Mathf.Rad2Deg / gradosPorRayo));
        for (int i = 0; i <= pasos; i++)
        {
            angulos.Add(-MedioAnguloCono + 2f * MedioAnguloCono * i / pasos);
        }
        // ...más dos rayos junto a cada esquina de los obstáculos cercanos (bordes de sombra rectos)
        foreach (Rect r in obstaculos)
        {
            if (!CercaDelCono(origen, R, r))
            {
                continue;
            }
            AgregarEsquina(origen, mira, r.xMin, r.yMin);
            AgregarEsquina(origen, mira, r.xMax, r.yMin);
            AgregarEsquina(origen, mira, r.xMin, r.yMax);
            AgregarEsquina(origen, mira, r.xMax, r.yMax);
        }
        angulos.Sort();

        // 2. cada rayo llega hasta el primer obstáculo (fracción t del alcance)
        vertices.Clear();
        colores.Clear();
        triangulos.Clear();
        float y = v.raiz.transform.position.y + alturaCono;
        vertices.Add(new Vector3(v.raiz.transform.position.x, y, v.raiz.transform.position.z));
        colores.Add(new Color(1f, 1f, 1f, 1f));
        foreach (float a in angulos)
        {
            Vector2 direccion = new Vector2(Mathf.Cos(mira + a), Mathf.Sin(mira + a));
            Vector2 fin = origen + direccion * R;
            float t = RayoHasta(origen, fin);
            Vector3 punto = Mapa.AMundo(origen.x + direccion.x * R * t, origen.y + direccion.y * R * t);
            vertices.Add(new Vector3(punto.x, y, punto.z));
            colores.Add(new Color(1f, 1f, 1f, Mathf.Lerp(1f, alfaAlFinal, t)));   // se desvanece con la distancia
        }
        for (int i = 1; i < vertices.Count - 1; i++)
        {
            triangulos.Add(0);
            triangulos.Add(i);
            triangulos.Add(i + 1);
        }

        Mesh malla = v.mallaCono;
        malla.Clear();
        malla.SetVertices(vertices);
        malla.SetColors(colores);
        malla.SetTriangles(triangulos, 0);
        malla.RecalculateBounds();

        // 3. la sospecha pintada de rojo desde el guardia (sube y baja suave, no a saltos)
        v.sospecha = Mathf.MoveTowards(v.sospecha, v.sospechaObjetivo, velocidadRelleno * Time.deltaTime);
        v.materialCono.SetFloat("_Progreso", v.sospecha);
        v.materialCono.SetColor("_ColorSospecha", v.persiguiendo ? colorPersecucion : colorSospecha);
        v.materialCono.SetVector("_Origen", v.raiz.transform.position);
        v.materialCono.SetFloat("_Alcance", R * Mapa.UnidadesPorPixel);
    }

    bool CercaDelCono(Vector2 origen, float R, Rect r)
    {
        // ¿el rectángulo queda a menos de R del guardia? (distancia al punto más cercano del rectángulo)
        float cx = Mathf.Clamp(origen.x, r.xMin, r.xMax);
        float cy = Mathf.Clamp(origen.y, r.yMin, r.yMax);
        float dx = origen.x - cx;
        float dy = origen.y - cy;
        return dx * dx + dy * dy < R * R;
    }

    void AgregarEsquina(Vector2 origen, float mira, float x, float y)
    {
        float angulo = Mathf.Atan2(y - origen.y, x - origen.x);
        float relativo = Mathf.DeltaAngle(mira * Mathf.Rad2Deg, angulo * Mathf.Rad2Deg) * Mathf.Deg2Rad;
        // solo las esquinas que caen DENTRO del cono (las de los lados o de atrás no cuentan)
        float antes = relativo - Epsilon;
        float despues = relativo + Epsilon;
        if (antes > -MedioAnguloCono && antes < MedioAnguloCono)
        {
            angulos.Add(antes);
        }
        if (despues > -MedioAnguloCono && despues < MedioAnguloCono)
        {
            angulos.Add(despues);
        }
    }

    /// <summary>Fracción (0 a 1) del rayo a -> b hasta el primer obstáculo (como ray_hit del servidor).</summary>
    float RayoHasta(Vector2 a, Vector2 b)
    {
        float mejor = 1f;
        foreach (Rect r in obstaculos)
        {
            if (r.Contains(a))
            {
                continue;                      // guardia pegado a un muro: si no, el cono desaparecería
            }
            float t = CruceConRectangulo(a, b, r);
            if (t >= 0f && t < mejor)
            {
                mejor = t;
            }
        }
        return mejor;
    }

    /// <summary>
    /// Cruce de un segmento a -> b con un rectángulo (algoritmo de Liang-Barsky, igual que seg_rect del servidor).
    /// Regresa t entre 0 y 1 donde entra al rectángulo, o -1 si no lo toca.
    /// </summary>
    float CruceConRectangulo(Vector2 a, Vector2 b, Rect r)
    {
        float dx = b.x - a.x;
        float dy = b.y - a.y;
        float t0 = 0f;
        float t1 = 1f;
        // los cuatro lados: izquierdo, derecho, arriba y abajo
        bool toca = Recortar(-dx, a.x - r.xMin, ref t0, ref t1)
                 && Recortar(dx, r.xMax - a.x, ref t0, ref t1)
                 && Recortar(-dy, a.y - r.yMin, ref t0, ref t1)
                 && Recortar(dy, r.yMax - a.y, ref t0, ref t1);
        return toca ? t0 : -1f;
    }

    /// <summary>Un lado del rectángulo en Liang-Barsky: ajusta [t0, t1]; false si el segmento queda fuera.</summary>
    bool Recortar(float p, float q, ref float t0, ref float t1)
    {
        if (p == 0f)
        {
            return q >= 0f;                    // paralelo al lado: fuera si q < 0
        }
        float t = q / p;
        if (p < 0f)
        {
            if (t > t1)
            {
                return false;
            }
            if (t > t0)
            {
                t0 = t;
            }
        }
        else
        {
            if (t < t0)
            {
                return false;
            }
            if (t < t1)
            {
                t1 = t;
            }
        }
        return true;
    }

    // ---------- crear las figuras ----------
    Visual CrearGuardia(GuardiaDatos g)
    {
        bool deLaCanoa = g.role == "canoe_guard";
        Material cuerpo = deLaCanoa ? materialGuardiaCanoa : materialGuardia;
        GameObject modelo = deLaCanoa ? modeloGuardiaCanoa : modeloGuardia;
        Visual v = CrearPersona("Guardia_" + g.id + " (" + g.role + ")", cuerpo, modelo, g.x, g.y, g.face);

        // el cono vive en coordenadas del mundo (sus vértices se calculan cada frame en LateUpdate)
        GameObject cono = new GameObject("Cono_" + g.id);
        cono.transform.SetParent(raizConos.transform, false);
        v.mallaCono = new Mesh();
        v.mallaCono.name = "ConoVision_" + g.id;
        v.mallaCono.MarkDynamic();
        cono.AddComponent<MeshFilter>().sharedMesh = v.mallaCono;
        MeshRenderer dibujo = cono.AddComponent<MeshRenderer>();
        v.materialCono = new Material(materialCono);
        v.materialCono.SetColor("_BaseColor", conoTranquilo);
        dibujo.sharedMaterial = v.materialCono;
        dibujo.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        dibujo.receiveShadows = false;
        return v;
    }

    Visual CrearAldeano(AldeanoDatos a)
    {
        string nombre = a.carrier ? "Cargador_" + a.id : "Aldeano_" + a.id;
        GameObject modelo = modeloCargador;
        if (!a.carrier)
        {
            modelo = null;
            if (modelosAldeanos != null && modelosAldeanos.Length > 0)
            {
                modelo = modelosAldeanos[a.id % modelosAldeanos.Length];     // artesano, mujer, campesino...
            }
        }
        Visual v = CrearPersona(nombre, a.carrier ? materialCargador : materialAldeano, modelo, a.x, a.y, a.face);
        if (a.carrier && modelo == null)
        {
            // bulto en la espalda
            GameObject bulto = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bulto.name = "Bulto";
            Destroy(bulto.GetComponent<Collider>());
            bulto.transform.SetParent(v.raiz.transform, false);
            bulto.transform.localPosition = new Vector3(0f, 1.2f, -0.45f);
            bulto.transform.localScale = new Vector3(0.6f, 0.7f, 0.4f);
            bulto.GetComponent<Renderer>().sharedMaterial = materialCargador;
        }
        return v;
    }

    Visual CrearPersona(string nombre, Material material, GameObject modelo, float x, float y, float angulo)
    {
        // raíz al nivel del suelo, mirando hacia donde mira el agente
        GameObject raiz = new GameObject(nombre);
        raiz.transform.SetParent(transform, false);
        raiz.transform.position = Mapa.AMundo(x, y);
        raiz.transform.rotation = Quaternion.Euler(0f, Mapa.AnguloAGrados(angulo), 0f);

        if (modelo != null)
        {
            // modelo 3D animado: el Animator elige parado / caminar / correr según la velocidad
            GameObject cuerpo = Instantiate(modelo, raiz.transform);
            cuerpo.name = "Modelo";
            cuerpo.transform.localPosition = Vector3.zero;
            cuerpo.transform.localRotation = Quaternion.identity;
            Animator animator = cuerpo.GetComponent<Animator>();
            if (animator != null)
            {
                animator.runtimeAnimatorController = controlador;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;   // fuera de cámara no se calcula
                AnimacionPorVelocidad animacion = cuerpo.AddComponent<AnimacionPorVelocidad>();
                animacion.cuerpo = raiz.transform;
            }
        }
        else
        {
            // sin modelo: cápsula de 1.8 m sin collider (como en el graybox)
            GameObject cuerpo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            cuerpo.name = "Cuerpo";
            Destroy(cuerpo.GetComponent<Collider>());
            cuerpo.transform.SetParent(raiz.transform, false);
            cuerpo.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            cuerpo.transform.localScale = new Vector3(1f, 0.9f, 1f);
            cuerpo.GetComponent<Renderer>().sharedMaterial = material;
        }

        Visual v = new Visual();
        v.raiz = raiz;
        v.desde = raiz.transform.position;
        v.hacia = raiz.transform.position;
        v.girarDesde = raiz.transform.rotation;
        v.girarHacia = raiz.transform.rotation;
        return v;
    }

    void PintarTroje(TrojeEstado t)
    {
        // con modelo 3D: fuego, humo y cambio al modelo quemado
        TrojeModelo modelo;
        if (modelosTroje.TryGetValue(t.id, out modelo))
        {
            if (t.burning)
            {
                modelo.Encender();
            }
            return;
        }
        // sin modelo (solo graybox): pintar la caja
        GameObject troje = GameObject.Find("Troje_" + t.id);
        if (troje == null)
        {
            return;
        }
        Color color = t.burning ? trojeArdiendo : Color.Lerp(trojeNormal, trojeArdiendo, t.progress * 0.6f);
        troje.GetComponent<Renderer>().material.SetColor("_BaseColor", color);
    }
}
