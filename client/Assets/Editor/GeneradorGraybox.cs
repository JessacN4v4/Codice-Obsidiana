using System;
using System.Net.Http;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Menú "Codice > Generar graybox del pueblo".
/// Descarga el mapa del servidor (GET /map) y construye la escena con cajas de colores:
/// suelo, lago, bordes, almacenes, empalizadas, casas, trojes, yácata, puestos,
/// maizales, plaza, canoa y el punto de inicio del jugador.
/// Si ya había un graybox en la escena, lo borra y lo vuelve a crear.
/// Requiere que el servidor esté corriendo en http://127.0.0.1:8000
/// </summary>
public static class GeneradorGraybox
{
    const string UrlMapa = "http://127.0.0.1:8000/map";
    const string CarpetaMateriales = "Assets/Graybox/Materiales";
    const float K = Mapa.UnidadesPorPixel;

    // Alturas en metros
    const float AltoMuro = 2.5f;
    const float AltoCasa = 3f;
    const float AltoTroje = 2.5f;
    const float AltoPuesto = 1f;
    const float AltoMaizal = 1.8f;
    const float AltoEscalonYacata = 1.2f;
    const int EscalonesYacata = 4;
    const float AltoLimite = 3f;       // paredes invisibles del lago y del borde del mundo

    const string RutaEscena = "Assets/Scenes/Pueblo.unity";

    [MenuItem("Codice/Generar graybox del pueblo")]
    public static void Generar()
    {
        GenerarEnEscenaActual();
    }

    /// <summary>
    /// Para correr Unity desde la línea de comandos (sin abrir el editor):
    /// Unity.exe -batchmode -projectPath client -executeMethod GeneradorGraybox.GenerarEscenaPueblo -quit
    /// Crea la escena Pueblo, genera el graybox, acomoda la cámara y la guarda.
    /// </summary>
    public static void GenerarEscenaPueblo()
    {
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        if (!GenerarEnEscenaActual())
        {
            EditorApplication.Exit(1);
            return;
        }
        AcomodarCamaraPrincipal();
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), RutaEscena);
        AssetDatabase.SaveAssets();
        Debug.Log("Escena guardada en " + RutaEscena);
    }

    static bool GenerarEnEscenaActual()
    {
        string json = DescargarMapa();
        if (json == null)
        {
            return false;
        }
        MapaDatos mapa = JsonUtility.FromJson<MapaDatos>(json);

        GameObject anterior = GameObject.Find("Graybox");
        if (anterior != null)
        {
            Undo.DestroyObjectImmediate(anterior);
        }
        GameObject raiz = new GameObject("Graybox");
        Undo.RegisterCreatedObjectUndo(raiz, "Generar graybox");

        CrearSuelo(mapa, raiz.transform);
        CrearLago(mapa, raiz.transform);
        CrearBordes(mapa, raiz.transform);
        CrearAlmacenes(mapa, raiz.transform);
        CrearObstaculos(mapa, raiz.transform);
        CrearTrojes(mapa, raiz.transform);
        CrearMaizales(mapa, raiz.transform);
        CrearPlaza(mapa, raiz.transform);
        CrearCanoa(mapa, raiz.transform);
        CrearInicio(mapa, raiz.transform);

        EditorSceneManager.MarkSceneDirty(raiz.scene);
        Selection.activeGameObject = raiz;
        Debug.Log("Graybox generado: " + mapa.obstacles.Length + " obstáculos, "
                  + mapa.trojes.Length + " trojes y " + mapa.maize.Length + " maizales.");
        return true;
    }

    // ---------- cámara ----------
    static void AcomodarCamaraPrincipal()
    {
        // Vista cenital inclinada sobre el inicio del jugador (estilo Civilization)
        Camera camara = Camera.main;
        if (camara == null)
        {
            return;
        }
        GameObject inicio = GameObject.Find("InicioJugador");
        Vector3 objetivo = inicio != null ? inicio.transform.position : Vector3.zero;
        camara.transform.position = objetivo + new Vector3(0f, 22f, -16f);
        camara.transform.rotation = Quaternion.Euler(55f, 0f, 0f);
    }

    // ---------- descarga ----------
    public static string DescargarMapa()
    {
        try
        {
            using (HttpClient cliente = new HttpClient())
            {
                cliente.Timeout = TimeSpan.FromSeconds(5);
                return cliente.GetStringAsync(UrlMapa).Result;
            }
        }
        catch (Exception e)
        {
            Debug.LogError("No se pudo descargar el mapa de " + UrlMapa + ": " + e.GetBaseException().Message);
            EditorUtility.DisplayDialog(
                "No se pudo descargar el mapa",
                "Levanta el servidor primero:\n\n"
                + "cd server\n"
                + ".venv\\Scripts\\Activate.ps1\n"
                + "uvicorn app.main:app --port 8000\n\n"
                + "Error: " + e.GetBaseException().Message,
                "OK");
            return null;
        }
    }

    // ---------- partes del pueblo ----------
    static void CrearSuelo(MapaDatos mapa, Transform raiz)
    {
        RectDatos todo = NuevoRect(0, 0, mapa.worldW, mapa.worldH);
        CrearCaja("Suelo", raiz, todo, -0.5f, 0.5f, ObtenerMaterial("Suelo", Rgb(154, 134, 86)), true);
    }

    static void CrearLago(MapaDatos mapa, Transform raiz)
    {
        // El lago se arma con franjas de 20 px de alto, desde x = 0 hasta la orilla
        Transform grupo = CrearGrupo("Lago", raiz);
        Material agua = ObtenerMaterial("Agua", Rgb(62, 108, 128));
        for (int i = 0; i < mapa.shore.Length - 1; i++)
        {
            PuntoDatos a = mapa.shore[i];
            PuntoDatos b = mapa.shore[i + 1];
            RectDatos franja = NuevoRect(0, a.y, Mathf.Max(a.x, b.x), b.y - a.y);
            CrearCaja("Agua", grupo, franja, 0f, 0.05f, agua, false);
            CrearLimite("LimiteLago", grupo, franja);     // pared invisible: no se puede entrar al lago
        }
    }

    static void CrearBordes(MapaDatos mapa, Transform raiz)
    {
        // Paredes invisibles alrededor del mundo
        Transform grupo = CrearGrupo("Bordes", raiz);
        float w = mapa.worldW;
        float h = mapa.worldH;
        CrearLimite("BordeNorte", grupo, NuevoRect(-20, -20, w + 40, 20));
        CrearLimite("BordeSur", grupo, NuevoRect(-20, h, w + 40, 20));
        CrearLimite("BordeOeste", grupo, NuevoRect(-20, 0, 20, h));
        CrearLimite("BordeEste", grupo, NuevoRect(w, 0, 20, h));
    }

    static void CrearAlmacenes(MapaDatos mapa, Transform raiz)
    {
        // Piso de color de cada almacén (amarillo = vigilancia baja, naranja = media, rojo = alta)
        Transform grupo = CrearGrupo("Almacenes", raiz);
        foreach (ZonaDatos zona in mapa.zones)
        {
            Transform almacen = CrearGrupo("Almacen_" + zona.id + " (" + zona.name + ")", grupo);
            Color color = ColorDeZona(zona.id);
            Color piso = Color.Lerp(Rgb(154, 134, 86), color, 0.45f);
            CrearCaja("Piso", almacen, zona.rect, 0f, 0.02f, ObtenerMaterial("Zona_" + zona.id, piso), false);
            CrearCaja("Puerta", almacen, zona.gate, 0f, 0.03f, ObtenerMaterial("Puerta_" + zona.id, color), false);
        }
    }

    static void CrearObstaculos(MapaDatos mapa, Transform raiz)
    {
        Transform muros = CrearGrupo("Empalizadas", raiz);
        Transform casas = CrearGrupo("Casas", raiz);
        Transform puestos = CrearGrupo("Puestos", raiz);
        Transform yacata = CrearGrupo("Yacata", raiz);
        Material muro = ObtenerMaterial("Empalizada", Rgb(90, 61, 37));
        Material casa = ObtenerMaterial("Casa", Rgb(205, 174, 98));
        Material puesto = ObtenerMaterial("Puesto", Rgb(181, 69, 47));
        Material piedra = ObtenerMaterial("Yacata", Rgb(145, 124, 102));

        foreach (ObstaculoDatos o in mapa.obstacles)
        {
            if (o.kind == "wall")
            {
                CrearCaja("Empalizada", muros, o.rect, 0f, AltoMuro, muro, true);
            }
            else if (o.kind == "house")
            {
                CrearCaja("Casa", casas, o.rect, 0f, AltoCasa, casa, true);
            }
            else if (o.kind == "stall")
            {
                CrearCaja("Puesto", puestos, o.rect, 0f, AltoPuesto, puesto, true);
            }
            else if (o.kind == "yacata")
            {
                // basamento escalonado: cada escalón es más chico y está más arriba
                for (int i = 0; i < EscalonesYacata; i++)
                {
                    float margen = 11f * i;
                    RectDatos escalon = NuevoRect(o.rect.x + margen, o.rect.y + margen,
                                             o.rect.w - 2 * margen, o.rect.h - 2 * margen);
                    CrearCaja("Escalon_" + i, yacata, escalon, i * AltoEscalonYacata,
                              AltoEscalonYacata, piedra, true);
                }
            }
        }
    }

    static void CrearTrojes(MapaDatos mapa, Transform raiz)
    {
        Transform grupo = CrearGrupo("Trojes", raiz);
        Material troje = ObtenerMaterial("Troje", Rgb(217, 115, 38));
        foreach (TrojeDatos t in mapa.trojes)
        {
            CrearCaja("Troje_" + t.id, grupo, t.rect, 0f, AltoTroje, troje, true);
        }
    }

    static void CrearMaizales(MapaDatos mapa, Transform raiz)
    {
        // Los maizales ocultan pero no bloquean: su collider es trigger
        Transform grupo = CrearGrupo("Maizales", raiz);
        Material maiz = ObtenerMaterial("Maizal", Rgb(79, 92, 36));
        for (int i = 0; i < mapa.maize.Length; i++)
        {
            GameObject maizal = CrearCaja("Maizal_" + i, grupo, mapa.maize[i], 0f, AltoMaizal, maiz, true);
            maizal.GetComponent<BoxCollider>().isTrigger = true;
        }
    }

    static void CrearPlaza(MapaDatos mapa, Transform raiz)
    {
        GameObject plaza = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        plaza.name = "Plaza";
        plaza.transform.SetParent(raiz, false);
        UnityEngine.Object.DestroyImmediate(plaza.GetComponent<Collider>());
        Vector3 centro = Mapa.AMundo(mapa.plaza.x, mapa.plaza.y);
        float diametro = 2f * mapa.plaza.r * K;
        plaza.transform.localPosition = new Vector3(centro.x, 0.01f, centro.z);
        plaza.transform.localScale = new Vector3(diametro, 0.01f, diametro);
        plaza.GetComponent<Renderer>().sharedMaterial = ObtenerMaterial("Plaza", Rgb(191, 163, 114));
    }

    static void CrearCanoa(MapaDatos mapa, Transform raiz)
    {
        // Canoa de 24 x 70 px sobre la orilla; sin collider para poder subirse
        RectDatos r = NuevoRect(mapa.canoe.x - 12, mapa.canoe.y - 35, 24, 70);
        CrearCaja("Canoa", raiz, r, 0.05f, 0.5f, ObtenerMaterial("Canoa", Rgb(107, 74, 43)), false);
    }

    static void CrearInicio(MapaDatos mapa, Transform raiz)
    {
        GameObject inicio = new GameObject("InicioJugador");
        inicio.transform.SetParent(raiz, false);
        inicio.transform.localPosition = Mapa.AMundo(mapa.start.x, mapa.start.y);
    }

    // ---------- ayudantes ----------
    static GameObject CrearCaja(string nombre, Transform padre, RectDatos r, float desde, float alto,
                                Material material, bool conColision)
    {
        GameObject caja = GameObject.CreatePrimitive(PrimitiveType.Cube);
        caja.name = nombre;
        caja.transform.SetParent(padre, false);
        Vector3 centro = Mapa.AMundo(r.x + r.w / 2f, r.y + r.h / 2f);
        caja.transform.localPosition = new Vector3(centro.x, desde + alto / 2f, centro.z);
        caja.transform.localScale = new Vector3(r.w * K, alto, r.h * K);
        caja.GetComponent<Renderer>().sharedMaterial = material;
        if (!conColision)
        {
            UnityEngine.Object.DestroyImmediate(caja.GetComponent<BoxCollider>());
        }
        return caja;
    }

    static void CrearLimite(string nombre, Transform padre, RectDatos r)
    {
        // Objeto vacío con un BoxCollider: no se ve, pero no deja pasar
        GameObject limite = new GameObject(nombre);
        limite.transform.SetParent(padre, false);
        Vector3 centro = Mapa.AMundo(r.x + r.w / 2f, r.y + r.h / 2f);
        limite.transform.localPosition = new Vector3(centro.x, AltoLimite / 2f, centro.z);
        BoxCollider colision = limite.AddComponent<BoxCollider>();
        colision.size = new Vector3(r.w * K, AltoLimite, r.h * K);
    }

    static Transform CrearGrupo(string nombre, Transform padre)
    {
        GameObject grupo = new GameObject(nombre);
        grupo.transform.SetParent(padre, false);
        return grupo.transform;
    }

    static Material ObtenerMaterial(string nombre, Color color)
    {
        // Los materiales se guardan como archivos para que la escena no los pierda
        if (!AssetDatabase.IsValidFolder("Assets/Graybox"))
        {
            AssetDatabase.CreateFolder("Assets", "Graybox");
        }
        if (!AssetDatabase.IsValidFolder(CarpetaMateriales))
        {
            AssetDatabase.CreateFolder("Assets/Graybox", "Materiales");
        }
        string ruta = CarpetaMateriales + "/" + nombre + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, ruta);
        }
        material.SetColor("_BaseColor", color);
        EditorUtility.SetDirty(material);
        return material;
    }

    static Color ColorDeZona(string id)
    {
        if (id == "A") return Rgb(230, 190, 70);    // vigilancia baja
        if (id == "B") return Rgb(235, 140, 50);    // media
        return Rgb(215, 60, 45);                    // alta
    }

    static Color Rgb(int r, int g, int b)
    {
        return new Color(r / 255f, g / 255f, b / 255f);
    }

    static RectDatos NuevoRect(float x, float y, float w, float h)
    {
        RectDatos r = new RectDatos();
        r.x = x;
        r.y = y;
        r.w = w;
        r.h = h;
        return r;
    }
}
