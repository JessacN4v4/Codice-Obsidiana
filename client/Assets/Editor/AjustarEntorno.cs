using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Menú "Codice > Ajustar entorno (agua, cielo y cámara)".
/// - Agua: genera la superficie del lago al oeste y al norte del pueblo (hasta 50 m afuera, en rectángulo)
///   con un material de agua animado, y apaga las franjas azules del graybox (su pared invisible queda).
/// - Cielo: cielo procedural de Unity (sin texturas), que también da la luz ambiental; sol al sureste.
/// - Cámara: un poco más cerca (19 m de alto y 14 m atrás).
/// </summary>
public static class AjustarEntorno
{
    const string RutaEscena = "Assets/Scenes/Pueblo.unity";
    const string Texturas = "Assets/Art/Textures/";
    const string Materiales = "Assets/Art/Materials/";
    const float AlturaCamara = 19f;
    const float DistanciaCamara = 14f;

    [MenuItem("Codice/Ajustar entorno (agua, cielo y cámara)")]
    public static void Ajustar()
    {
        AjustarEnEscenaActual();
    }

    /// <summary>
    /// Para la línea de comandos: abre la escena Pueblo, ajusta el entorno y la guarda.
    /// Unity.exe -batchmode -projectPath client -executeMethod AjustarEntorno.AjustarEnEscenaPueblo -quit
    /// </summary>
    public static void AjustarEnEscenaPueblo()
    {
        EditorSceneManager.OpenScene(RutaEscena, OpenSceneMode.Single);
        AjustarEnEscenaActual();
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
    }

    static void AjustarEnEscenaActual()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Art/Materials"))
        {
            AssetDatabase.CreateFolder("Assets/Art", "Materials");
        }
        PonerLago();
        PonerCielo();
        AcercarCamara();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("Entorno ajustado: agua en rectángulo, cielo y cámara a " + AlturaCamara + " m. Guarda la escena (Ctrl + S).");
    }

    // ---------- lago ----------
    // Todo el escenario cabe en un rectángulo: 50 m de agua al oeste y al norte del mapa,
    // 50 m de tierra al este y 30 m al sur.
    const float AguaOeste = 50f;
    const float AguaNorte = 50f;

    static void PonerLago()
    {
        // quitar el lago anterior (el objeto suelto "Lago" de la escena, no el del graybox)
        foreach (GameObject raiz in EditorSceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (raiz.name == "Lago" || raiz.name == "Agua")
            {
                Undo.DestroyObjectImmediate(raiz);
            }
        }
        string json = GeneradorGraybox.DescargarMapa();      // la orilla viene del mapa del servidor
        if (json == null)
        {
            return;
        }
        MapaDatos mapa = JsonUtility.FromJson<MapaDatos>(json);

        GameObject agua = new GameObject("Agua");
        Undo.RegisterCreatedObjectUndo(agua, "Poner agua");
        Material material = MaterialAgua();
        PonerAguaDelOeste(material, agua.transform, mapa);
        PonerAguaDelNorte(material, agua.transform);

        // apagar las franjas azules del graybox (los "LimiteLago" no tienen Renderer: siguen bloqueando)
        GameObject franjas = GameObject.Find("Graybox/Lago");
        if (franjas != null)
        {
            foreach (Renderer r in franjas.GetComponentsInChildren<Renderer>())
            {
                Undo.RecordObject(r, "Apagar franjas del lago");
                r.enabled = false;
            }
        }
    }

    static void PonerAguaDelOeste(Material agua, Transform padre, MapaDatos mapa)
    {
        // Del borde oeste del rectángulo (50 m al oeste del mapa) hasta la orilla del pueblo,
        // de punta a punta en norte-sur. La orilla es la del mapa del servidor.
        float z0 = -(mapa.worldH * Mapa.UnidadesPorPixel + PintarTerreno.ExtraSur);   // borde sur
        float z1 = AguaNorte;                                                          // borde norte
        int filas = Mathf.CeilToInt(z1 - z0) + 1;

        Vector3[] vertices = new Vector3[filas * 2];
        Vector2[] uv = new Vector2[filas * 2];
        int[] triangulos = new int[(filas - 1) * 6];
        for (int i = 0; i < filas; i++)
        {
            float z = Mathf.Min(z0 + i, z1);
            float orilla = OrillaEnMetros(mapa, z);
            vertices[i * 2] = new Vector3(-AguaOeste, 0f, z);
            vertices[i * 2 + 1] = new Vector3(orilla, 0f, z);
            uv[i * 2] = new Vector2(-AguaOeste, z);          // UV en metros
            uv[i * 2 + 1] = new Vector2(orilla, z);
        }
        for (int i = 0; i < filas - 1; i++)
        {
            int a = i * 2;
            triangulos[i * 6] = a;
            triangulos[i * 6 + 1] = a + 2;
            triangulos[i * 6 + 2] = a + 1;
            triangulos[i * 6 + 3] = a + 1;
            triangulos[i * 6 + 4] = a + 2;
            triangulos[i * 6 + 5] = a + 3;
        }
        CrearSuperficie("AguaDelOeste", vertices, uv, triangulos, agua, padre, 0.03f);
    }

    static float OrillaEnMetros(MapaDatos mapa, float z)
    {
        // la orilla viene en px cada 20 px de "y" del mapa (y = -z / 0.05); fuera del mapa se repite la última
        float y = -z / Mapa.UnidadesPorPixel;
        float indice = Mathf.Clamp(y / 20f, 0f, mapa.shore.Length - 1.001f);
        int i = Mathf.FloorToInt(indice);
        return Mathf.Lerp(mapa.shore[i].x, mapa.shore[i + 1].x, indice - i) * Mapa.UnidadesPorPixel;
    }

    static void CrearSuperficie(string nombre, Vector3[] vertices, Vector2[] uv, int[] triangulos, Material agua, Transform padre, float altura)
    {
        Mesh malla = new Mesh();
        malla.name = nombre;
        malla.vertices = vertices;
        malla.uv = uv;
        malla.triangles = triangulos;
        malla.RecalculateNormals();
        malla.RecalculateTangents();                         // el mapa de normales las necesita
        malla.RecalculateBounds();

        GameObject objeto = new GameObject(nombre);
        objeto.transform.SetParent(padre, false);
        objeto.transform.localPosition = new Vector3(0f, altura, 0f);
        objeto.AddComponent<MeshFilter>().sharedMesh = malla;
        MeshRenderer dibujo = objeto.AddComponent<MeshRenderer>();
        dibujo.sharedMaterial = agua;
        dibujo.shadowCastingMode = ShadowCastingMode.Off;
        objeto.AddComponent<AguaAnimada>();
    }

    static void PonerAguaDelNorte(Material agua, Transform padre)
    {
        // Franja de agua detrás de la yácata: del borde norte del mapa (z = 0) hacia afuera,
        // de la orilla del oeste hasta el final del terreno decorativo del este.
        // Su orilla (lado sur) es irregular y siempre queda FUERA del mapa jugable (z > 0).
        float x0 = -AguaOeste;                               // desde el borde oeste del rectángulo
        float x1 = 100f + PintarTerreno.ExtraEste;
        float fondo = AguaNorte;                             // 5 cuadros de 10 m hacia el norte
        float paso = 1f;
        int columnas = Mathf.CeilToInt((x1 - x0) / paso) + 1;

        Vector3[] vertices = new Vector3[columnas * 2];
        Vector2[] uv = new Vector2[columnas * 2];
        int[] triangulos = new int[(columnas - 1) * 6];
        for (int i = 0; i < columnas; i++)
        {
            float x = x0 + i * paso;
            float orilla = 0.3f + Mathf.PerlinNoise(x * 0.15f, 3.7f) * 1.2f;   // entre 0.3 y 1.5 m afuera del borde
            vertices[i * 2] = new Vector3(x, 0f, orilla);
            vertices[i * 2 + 1] = new Vector3(x, 0f, fondo);
            uv[i * 2] = new Vector2(x, orilla);              // UV en metros, igual que el agua del oeste
            uv[i * 2 + 1] = new Vector2(x, fondo);
        }
        for (int i = 0; i < columnas - 1; i++)
        {
            int a = i * 2;
            triangulos[i * 6] = a;
            triangulos[i * 6 + 1] = a + 1;
            triangulos[i * 6 + 2] = a + 2;
            triangulos[i * 6 + 3] = a + 2;
            triangulos[i * 6 + 4] = a + 1;
            triangulos[i * 6 + 5] = a + 3;
        }
        Mesh malla = new Mesh();
        malla.name = "AguaDelNorte";
        malla.vertices = vertices;
        malla.uv = uv;
        malla.triangles = triangulos;
        malla.RecalculateNormals();
        malla.RecalculateTangents();                         // el mapa de normales las necesita
        malla.RecalculateBounds();

        GameObject objeto = new GameObject("AguaDelNorte");
        objeto.transform.SetParent(padre, false);
        objeto.transform.localPosition = new Vector3(0f, 0.025f, 0f);   // 5 mm abajo del lago: sin parpadeo donde se enciman
        objeto.AddComponent<MeshFilter>().sharedMesh = malla;
        MeshRenderer dibujo = objeto.AddComponent<MeshRenderer>();
        dibujo.sharedMaterial = agua;
        dibujo.shadowCastingMode = ShadowCastingMode.Off;
        objeto.AddComponent<AguaAnimada>();
    }

    static Material MaterialAgua()
    {
        string ruta = Materiales + "M_Agua.mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, ruta);
        }
        // color verde turbio con algas + olas grandes (mapa principal, cada 8 m)
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Texturas + "Agua/T_Agua_BaseColor.png"));
        m.SetColor("_BaseColor", Color.white);
        m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Texturas + "Agua/T_Agua_Normal_Olas.png"));
        m.SetFloat("_BumpScale", 0.7f);
        m.EnableKeyword("_NORMALMAP");
        m.SetTextureScale("_BaseMap", new Vector2(1f / 8f, 1f / 8f));       // las UV del lago están en metros
        // rizos finos (mapa de detalle, cada 2 m)
        m.SetTexture("_DetailNormalMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Texturas + "Agua/T_Agua_Normal_Rizos.png"));
        m.SetFloat("_DetailNormalMapScale", 0.5f);
        m.SetTextureScale("_DetailAlbedoMap", new Vector2(0.5f, 0.5f));
        m.EnableKeyword("_DETAIL_MULX2");
        // agua: nada metálica y muy lisa (refleja el cielo)
        m.SetFloat("_Metallic", 0f);
        m.SetFloat("_Smoothness", 0.9f);
        EditorUtility.SetDirty(m);
        return m;
    }

    // ---------- cielo ----------
    static void PonerCielo()
    {
        // Cielo "procedural": Unity lo calcula con fórmulas (sin texturas), casi no gasta recursos.
        // Con la cámara cenital apenas se ve, así que basta con un cielo azul con sol.
        string ruta = Materiales + "M_Cielo.mat";
        Material cielo = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (cielo != null && cielo.shader.name != "Skybox/Procedural")
        {
            AssetDatabase.DeleteAsset(ruta);           // era el panorama: se rehace
            cielo = null;
        }
        if (cielo == null)
        {
            cielo = new Material(Shader.Find("Skybox/Procedural"));
            AssetDatabase.CreateAsset(cielo, ruta);
        }
        cielo.SetFloat("_SunSize", 0.04f);
        cielo.SetFloat("_AtmosphereThickness", 0.9f);
        cielo.SetColor("_SkyTint", new Color(0.55f, 0.6f, 0.68f));
        cielo.SetColor("_GroundColor", new Color(0.38f, 0.34f, 0.28f));   // tierra de la cuenca
        cielo.SetFloat("_Exposure", 1.25f);
        EditorUtility.SetDirty(cielo);

        RenderSettings.skybox = cielo;
        RenderSettings.ambientMode = AmbientMode.Skybox;       // la luz ambiental sale del cielo
        DynamicGI.UpdateEnvironment();

        // sol de media mañana al sureste: alto 40° y mirando hacia el noroeste
        Light sol = RenderSettings.sun;
        if (sol == null)
        {
            foreach (Light luz in Object.FindObjectsByType<Light>())
            {
                if (luz.type == LightType.Directional)
                {
                    sol = luz;
                    break;
                }
            }
        }
        if (sol != null)
        {
            Undo.RecordObject(sol.transform, "Orientar el sol");
            sol.transform.rotation = Quaternion.Euler(40f, -45f, 0f);
        }
    }

    // ---------- cámara ----------
    static void AcercarCamara()
    {
        Camera camara = Camera.main;
        if (camara == null)
        {
            return;
        }
        CamaraCenital seguir = camara.GetComponent<CamaraCenital>();
        if (seguir == null)
        {
            return;
        }
        Undo.RecordObject(seguir, "Acercar cámara");
        seguir.altura = AlturaCamara;
        seguir.distanciaAtras = DistanciaCamara;
        EditorUtility.SetDirty(seguir);
    }
}
