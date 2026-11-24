using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Un rombo flotando fijo encima de cada troje, para que se vea qué hay que quemar.
/// Naranja (brilla un poco) mientras falta quemarlo; negro cuando ya se quemó.
/// Sube y baja despacio y gira sobre sí mismo. Es un objeto 3D del mundo, así que se ve
/// igual con la cámara cenital y con la de seguimiento.
/// También pone un óvalo verde flotando encima de la canoa (la meta de la huida).
/// Lo crea el HUD al empezar.
/// </summary>
public class MarcasTrojes : MonoBehaviour
{
    public ClienteSimulacion cliente;

    public Color colorPorQuemar = new Color(1f, 0.5f, 0.1f);
    public Color colorQuemado = new Color(0.04f, 0.04f, 0.04f);
    public float alturaSobreTroje = 1.3f;      // metros sobre lo más alto del troje
    public float alto = 1.4f;                  // metros, de punta a punta
    public float ancho = 0.8f;                 // metros
    public float flotar = 0.2f;                // metros que sube y baja
    public float gradosPorSegundo = 60f;       // giro

    [Header("Canoa")]
    public Color colorCanoa = new Color(0.2f, 0.9f, 0.3f);
    public float alturaSobreCanoa = 2.2f;      // metros sobre el suelo (centro del óvalo)
    public Vector3 tamOvalo = new Vector3(0.7f, 1.1f, 0.7f);   // metros: más alto que ancho

    static readonly string[] Ids = { "A", "B", "C" };

    private Transform ovalo;
    private Vector3 puntoCanoa;

    class Marca
    {
        public string id;
        public Transform rombo;
        public Material material;
        public Vector3 punto;                  // centro del rombo (sin flotar)
        public float quemado;                  // 0 = naranja, 1 = negro (cambia suave)
    }

    private readonly List<Marca> marcas = new List<Marca>();

    /// <summary>Centro fijo del óvalo de la canoa (sin flotar). false si no hay canoa en la escena.</summary>
    public bool PuntoCanoa(out Vector3 punto)
    {
        punto = puntoCanoa;
        return ovalo != null;
    }

    /// <summary>Centro fijo del rombo del troje con ese id (sin flotar). false si no hay.</summary>
    public bool PuntoTroje(string id, out Vector3 punto)
    {
        foreach (Marca m in marcas)
        {
            if (m.id == id)
            {
                punto = m.punto;
                return true;
            }
        }
        punto = Vector3.zero;
        return false;
    }

    void Start()
    {
        Mesh malla = CrearMallaRombo();
        foreach (string id in Ids)
        {
            Vector3 punto;
            if (!PuntoDelTroje(id, out punto))
            {
                continue;
            }
            Marca m = new Marca();
            m.id = id;
            m.punto = punto;

            GameObject rombo = new GameObject("MarcaTroje_" + id);
            rombo.transform.SetParent(transform, false);
            rombo.transform.position = punto;
            rombo.AddComponent<MeshFilter>().sharedMesh = malla;
            MeshRenderer dibujo = rombo.AddComponent<MeshRenderer>();
            dibujo.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            m.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.material.SetFloat("_Smoothness", 0.7f);
            m.material.EnableKeyword("_EMISSION");
            dibujo.sharedMaterial = m.material;
            m.rombo = rombo.transform;
            marcas.Add(m);
            PintarMarca(m);
        }
        CrearOvaloCanoa();
    }

    void CrearOvaloCanoa()
    {
        // la caja "Canoa" del graybox sigue en la escena (sin dibujarse) y marca dónde está la canoa
        GameObject canoa = GameObject.Find("Canoa");
        if (canoa == null)
        {
            return;
        }
        puntoCanoa = new Vector3(canoa.transform.position.x, alturaSobreCanoa, canoa.transform.position.z);

        GameObject esfera = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        esfera.name = "MarcaCanoa";
        Destroy(esfera.GetComponent<Collider>());
        esfera.transform.SetParent(transform, false);
        esfera.transform.position = puntoCanoa;
        esfera.transform.localScale = tamOvalo;            // esfera estirada = óvalo
        MeshRenderer dibujo = esfera.GetComponent<MeshRenderer>();
        dibujo.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.SetColor("_BaseColor", colorCanoa);
        material.SetFloat("_Smoothness", 0.7f);
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", colorCanoa * 0.6f);   // brilla un poco, como los rombos
        dibujo.sharedMaterial = material;
        ovalo = esfera.transform;
    }

    void Update()
    {
        foreach (Marca m in marcas)
        {
            bool ardiendo = TrojeQuemado(m.id);
            m.quemado = Mathf.MoveTowards(m.quemado, ardiendo ? 1f : 0f, Time.deltaTime);   // 1 s de naranja a negro
            PintarMarca(m);

            // flotar y girar (cada rombo un poco desfasado para que no se muevan igual)
            float fase = m.id[0] * 1.3f;
            m.rombo.position = m.punto + Vector3.up * (Mathf.Sin(Time.time * 2f + fase) * flotar);
            m.rombo.rotation = Quaternion.Euler(0f, Time.time * gradosPorSegundo + fase * 40f, 0f);
        }
        if (ovalo != null)
        {
            ovalo.position = puntoCanoa + Vector3.up * (Mathf.Sin(Time.time * 2f) * flotar);
        }
    }

    void PintarMarca(Marca m)
    {
        Color color = Color.Lerp(colorPorQuemar, colorQuemado, m.quemado);
        m.material.SetColor("_BaseColor", color);
        m.material.SetColor("_EmissionColor", colorPorQuemar * (0.8f * (1f - m.quemado)));   // el negro no brilla
    }

    /// <summary>¿El servidor ya dice que ese troje arde?</summary>
    public bool TrojeQuemado(string id)
    {
        if (cliente == null || cliente.Estado == null || cliente.Estado.trojes == null)
        {
            return false;
        }
        foreach (TrojeEstado t in cliente.Estado.trojes)
        {
            if (t.id == id)
            {
                return t.burning;
            }
        }
        return false;
    }

    /// <summary>Punto encima del troje: el modelo 3D (ambientación) o, si no hay, la caja del graybox.</summary>
    bool PuntoDelTroje(string id, out Vector3 punto)
    {
        punto = Vector3.zero;
        GameObject troje = null;
        foreach (TrojeModelo modelo in FindObjectsByType<TrojeModelo>())
        {
            if (modelo.id == id)
            {
                troje = modelo.gameObject;
            }
        }
        if (troje == null)
        {
            troje = GameObject.Find("Troje_" + id);
        }
        if (troje == null)
        {
            return false;
        }
        Bounds caja = new Bounds(troje.transform.position, Vector3.zero);
        foreach (Renderer r in troje.GetComponentsInChildren<Renderer>(true))
        {
            if (!(r is ParticleSystemRenderer))
            {
                caja.Encapsulate(r.bounds);
            }
        }
        punto = new Vector3(caja.center.x, caja.max.y + alturaSobreTroje + alto / 2f, caja.center.z);
        return true;
    }

    /// <summary>Rombo de 8 caras (dos pirámides unidas por la base), con caras planas.</summary>
    Mesh CrearMallaRombo()
    {
        Vector3 arriba = new Vector3(0f, alto / 2f, 0f);
        Vector3 abajo = new Vector3(0f, -alto / 2f, 0f);
        float r = ancho / 2f;
        Vector3[] orilla = { new Vector3(r, 0f, 0f), new Vector3(0f, 0f, r), new Vector3(-r, 0f, 0f), new Vector3(0f, 0f, -r) };

        // cada cara con sus propios vértices, así RecalculateNormals da caras planas (aspecto de gema)
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangulos = new List<int>();
        for (int i = 0; i < 4; i++)
        {
            Vector3 a = orilla[i];
            Vector3 b = orilla[(i + 1) % 4];
            AgregarCara(vertices, triangulos, arriba, b, a);
            AgregarCara(vertices, triangulos, abajo, a, b);
        }
        Mesh malla = new Mesh();
        malla.name = "RomboTroje";
        malla.SetVertices(vertices);
        malla.SetTriangles(triangulos, 0);
        malla.RecalculateNormals();
        malla.RecalculateBounds();
        return malla;
    }

    void AgregarCara(List<Vector3> vertices, List<int> triangulos, Vector3 a, Vector3 b, Vector3 c)
    {
        int n = vertices.Count;
        vertices.Add(a);
        vertices.Add(b);
        vertices.Add(c);
        triangulos.Add(n);
        triangulos.Add(n + 1);
        triangulos.Add(n + 2);
    }
}
