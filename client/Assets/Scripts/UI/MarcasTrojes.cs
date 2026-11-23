using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Un rombo flotando fijo encima de cada troje, para que se vea qué hay que quemar.
/// Naranja (brilla un poco) mientras falta quemarlo; negro cuando ya se quemó.
/// Sube y baja despacio y gira sobre sí mismo. Es un objeto 3D del mundo, así que se ve
/// igual con la cámara cenital y con la de seguimiento.
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

    static readonly string[] Ids = { "A", "B", "C" };

    class Marca
    {
        public string id;
        public Transform rombo;
        public Material material;
        public Vector3 punto;                  // centro del rombo (sin flotar)
        public float quemado;                  // 0 = naranja, 1 = negro (cambia suave)
    }

    private readonly List<Marca> marcas = new List<Marca>();

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
    }

    void PintarMarca(Marca m)
    {
        Color color = Color.Lerp(colorPorQuemar, colorQuemado, m.quemado);
        m.material.SetColor("_BaseColor", color);
        m.material.SetColor("_EmissionColor", colorPorQuemar * (0.8f * (1f - m.quemado)));   // el negro no brilla
    }

    bool TrojeQuemado(string id)
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
