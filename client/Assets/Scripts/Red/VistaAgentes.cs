using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Dibuja lo que manda el servidor: guardias (con su cono de visión), aldeanos y cargadores.
/// El servidor manda posiciones 10 veces por segundo; aquí se mueven suavemente entre
/// la posición anterior y la nueva (interpolación) para que no se vean a saltos.
/// También pinta los trojes según su progreso y los pone rojos cuando arden.
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

    [Header("Color del cono según el estado del guardia")]
    public Color conoTranquilo = new Color(1f, 0.95f, 0.7f, 0.18f);
    public Color conoInvestigando = new Color(1f, 0.6f, 0.1f, 0.28f);
    public Color conoPersiguiendo = new Color(1f, 0.1f, 0.05f, 0.35f);

    [Header("Trojes")]
    public Color trojeNormal = new Color(0.85f, 0.45f, 0.15f);
    public Color trojeArdiendo = new Color(1f, 0.15f, 0.05f);

    const float MedioAnguloCono = 0.72f;   // radianes, igual que en el servidor

    // Lo que se guarda de cada agente dibujado
    class Visual
    {
        public GameObject raiz;
        public Vector3 desde;
        public Vector3 hacia;
        public Quaternion girarDesde;
        public Quaternion girarHacia;
        public Transform cono;             // solo guardias
        public Material materialCono;      // copia propia para cambiarle el color
    }

    private Dictionary<int, Visual> guardias = new Dictionary<int, Visual>();
    private Dictionary<int, Visual> aldeanos = new Dictionary<int, Visual>();
    private Mesh mallaCono;

    private Dictionary<string, TrojeModelo> modelosTroje = new Dictionary<string, TrojeModelo>();

    void Start()
    {
        mallaCono = CrearMallaCono(24);
        // modelos 3D de los trojes (si ya se colocó la ambientación)
        foreach (TrojeModelo modelo in FindObjectsByType<TrojeModelo>())
        {
            modelosTroje[modelo.id] = modelo;
        }
        cliente.AlRecibirEstado += AlRecibirEstado;
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
        // alcance de la vista en metros: crece con la alerta del pueblo en fase 1
        float alcancePixeles = estado.phase == 1 ? 185f + 20f * estado.alert : 250f;
        float alcance = alcancePixeles * Mapa.UnidadesPorPixel;

        foreach (GuardiaDatos g in estado.guards)
        {
            Visual v;
            if (!guardias.TryGetValue(g.id, out v))
            {
                v = CrearGuardia(g);
                guardias[g.id] = v;
            }
            NuevoDestino(v, g.x, g.y, g.face);
            v.cono.localScale = new Vector3(alcance, 1f, alcance);
            v.materialCono.SetColor("_BaseColor", ColorDelCono(g.state));
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

    // ---------- crear las figuras ----------
    Visual CrearGuardia(GuardiaDatos g)
    {
        Material cuerpo = g.role == "canoe_guard" ? materialGuardiaCanoa : materialGuardia;
        Visual v = CrearPersona("Guardia_" + g.id + " (" + g.role + ")", cuerpo, g.x, g.y, g.face);

        GameObject cono = new GameObject("Cono");
        cono.transform.SetParent(v.raiz.transform, false);
        cono.transform.localPosition = new Vector3(0f, 0.05f, 0f);    // casi al ras del suelo
        cono.AddComponent<MeshFilter>().sharedMesh = mallaCono;
        MeshRenderer dibujo = cono.AddComponent<MeshRenderer>();
        v.materialCono = new Material(materialCono);
        dibujo.sharedMaterial = v.materialCono;
        dibujo.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        v.cono = cono.transform;
        return v;
    }

    Visual CrearAldeano(AldeanoDatos a)
    {
        string nombre = a.carrier ? "Cargador_" + a.id : "Aldeano_" + a.id;
        Visual v = CrearPersona(nombre, a.carrier ? materialCargador : materialAldeano, a.x, a.y, a.face);
        if (a.carrier)
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

    Visual CrearPersona(string nombre, Material material, float x, float y, float angulo)
    {
        // raíz al nivel del suelo; el cuerpo es una cápsula de 1.8 m sin collider
        GameObject raiz = new GameObject(nombre);
        raiz.transform.SetParent(transform, false);
        raiz.transform.position = Mapa.AMundo(x, y);
        raiz.transform.rotation = Quaternion.Euler(0f, Mapa.AnguloAGrados(angulo), 0f);

        GameObject cuerpo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        cuerpo.name = "Cuerpo";
        Destroy(cuerpo.GetComponent<Collider>());
        cuerpo.transform.SetParent(raiz.transform, false);
        cuerpo.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        cuerpo.transform.localScale = new Vector3(1f, 0.9f, 1f);
        cuerpo.GetComponent<Renderer>().sharedMaterial = material;

        Visual v = new Visual();
        v.raiz = raiz;
        v.desde = raiz.transform.position;
        v.hacia = raiz.transform.position;
        v.girarDesde = raiz.transform.rotation;
        v.girarHacia = raiz.transform.rotation;
        return v;
    }

    Mesh CrearMallaCono(int segmentos)
    {
        // Abanico plano de radio 1 que apunta hacia adelante (+Z); se escala con el alcance
        Vector3[] vertices = new Vector3[segmentos + 2];
        int[] triangulos = new int[segmentos * 3];
        vertices[0] = Vector3.zero;
        for (int i = 0; i <= segmentos; i++)
        {
            float a = -MedioAnguloCono + 2f * MedioAnguloCono * i / segmentos;
            vertices[i + 1] = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
        }
        for (int i = 0; i < segmentos; i++)
        {
            triangulos[i * 3] = 0;
            triangulos[i * 3 + 1] = i + 1;
            triangulos[i * 3 + 2] = i + 2;
        }
        Mesh malla = new Mesh();
        malla.name = "ConoVision";
        malla.vertices = vertices;
        malla.triangles = triangulos;
        malla.RecalculateNormals();
        malla.RecalculateBounds();
        return malla;
    }

    Color ColorDelCono(string estado)
    {
        if (estado == "chase")
        {
            return conoPersiguiendo;
        }
        if (estado == "investigate" || estado == "search")
        {
            return conoInvestigando;
        }
        return conoTranquilo;
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
