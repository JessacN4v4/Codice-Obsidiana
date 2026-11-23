using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Menú "Codice > Colocar decoración".
/// Reparte los props (canastos, ollas, petates, fogones, leña, plantas...) por el pueblo según
/// el mapa del servidor (GET /map). Todo es solo visual: sin colliders, así que no cambia el juego.
/// Reglas: nada encima de caminos ni veredas, ni dentro de casas o muros, ni en el agua.
/// La colocación usa números al azar con semilla fija: cada vez sale igual.
/// Menú "Codice > Quitar decoración": borra todo lo que puso este menú.
/// Requiere que el servidor esté corriendo.
/// </summary>
public static class ColocarDecoracion
{
    const string Modelos = "Assets/Art/Models/";
    const string RutaEscena = "Assets/Scenes/Pueblo.unity";
    const float M = 20f;                    // 1 metro = 20 px del mapa
    const int Semilla = 1478;

    static System.Random azar;
    static MapaDatos mapa;
    static int colocados;

    // mercancía de los vendedores del mercado, por giro
    static readonly string[][] Giros =
    {
        new string[] { "SM_Canasto_Chile", "SM_Canasto_Frijol", "SM_Canasto_Mazorcas", "SM_Calabazas", "SM_Bandeja_Pescado" },  // comida
        new string[] { "SM_Olla_Roja", "SM_Cantaro_Crema", "SM_Comales" },                                                     // cerámica
        new string[] { "SM_Mantas_Dobladas", "SM_Petates_Enrollados", "SM_Cobre_Set", "SM_Obsidiana_Set" },                   // artesanía
    };

    [MenuItem("Codice/Colocar decoración")]
    public static void Colocar()
    {
        ColocarEnEscenaActual();
    }

    [MenuItem("Codice/Quitar decoración")]
    public static void Quitar()
    {
        GameObject anterior = GameObject.Find("Decoracion");
        if (anterior != null)
        {
            Undo.DestroyObjectImmediate(anterior);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }
    }

    /// <summary>
    /// Para la línea de comandos: abre la escena Pueblo, coloca la decoración y la guarda.
    /// Unity.exe -batchmode -projectPath client -executeMethod ColocarDecoracion.ColocarEnEscenaPueblo -quit
    /// </summary>
    public static void ColocarEnEscenaPueblo()
    {
        EditorSceneManager.OpenScene(RutaEscena, OpenSceneMode.Single);
        if (!ColocarEnEscenaActual())
        {
            EditorApplication.Exit(1);
            return;
        }
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Debug.Log("Escena guardada con la decoración: " + RutaEscena);
    }

    static bool ColocarEnEscenaActual()
    {
        string json = GeneradorGraybox.DescargarMapa();
        if (json == null)
        {
            return false;
        }
        mapa = JsonUtility.FromJson<MapaDatos>(json);
        azar = new System.Random(Semilla);
        colocados = 0;

        Quitar();
        GameObject raiz = new GameObject("Decoracion");
        Undo.RegisterCreatedObjectUndo(raiz, "Colocar decoración");

        DecorarMercado(Grupo("Mercado", raiz.transform));
        DecorarTrojes(Grupo("Trojes", raiz.transform));
        DecorarCasas(Grupo("Casas", raiz.transform));
        DecorarAlmacenes(Grupo("Almacenes", raiz.transform));
        DecorarOrilla(Grupo("Orilla", raiz.transform));

        EditorSceneManager.MarkSceneDirty(raiz.scene);
        Selection.activeGameObject = raiz;
        Debug.Log("Decoración colocada: " + colocados + " props. Guarda la escena (Ctrl + S).");
        return true;
    }

    // ---------- mercado: vendedores con petate y mercancía alrededor de la plaza ----------
    static void DecorarMercado(Transform padre)
    {
        Vector2 centro = new Vector2(mapa.plaza.x, mapa.plaza.y);
        List<Vector2> puestos = new List<Vector2>();
        foreach (ObstaculoDatos o in mapa.obstacles)
        {
            if (o.kind == "stall")
            {
                puestos.Add(Centro(o.rect));
            }
        }
        Vector2 puntaYacata = PuntaSurDeLaYacata();
        Vector2 bordeNortePlaza = new Vector2(centro.x, centro.y - mapa.plaza.r + 30f);

        List<Vector2> vendedores = new List<Vector2>();
        for (int intento = 0; intento < 400 && vendedores.Count < 5; intento++)
        {
            float angulo = Rango(0f, Mathf.PI * 2f);
            float radio = Rango(3.8f, 7f) * M;
            Vector2 p = centro + new Vector2(Mathf.Cos(angulo), Mathf.Sin(angulo)) * radio;
            if (MasCercaQue(p, puestos, 2.8f * M) || MasCercaQue(p, vendedores, 3f * M))
            {
                continue;
            }
            if (DistanciaACaminos(p) < 1.6f * M || DistanciaASegmento(p, puntaYacata, bordeNortePlaza) < 1.8f * M)
            {
                continue;
            }
            vendedores.Add(p);
        }

        for (int i = 0; i < vendedores.Count; i++)
        {
            Vector2 p = vendedores[i];
            // el petate mira al centro de la plaza (el vendedor da la cara a los compradores)
            Quaternion giro = MirarHacia(p, centro);
            GameObject vendedor = new GameObject("Vendedor_" + (i + 1));
            vendedor.transform.SetParent(padre, false);
            vendedor.transform.position = Mundo(p);
            vendedor.transform.rotation = giro;

            PonerLocal("Mercado/SM_Petate", vendedor.transform, Vector3.zero, 0f);
            string[] giroDelVendedor = Giros[i % Giros.Length];
            float[] lugares = { -0.55f, 0f, 0.55f };
            foreach (float x in lugares)
            {
                string mercancia = giroDelVendedor[azar.Next(giroDelVendedor.Length)];
                PonerLocal("Mercado/" + mercancia, vendedor.transform, new Vector3(x, 0.01f, Rango(-0.15f, 0.15f)), Rango(-30f, 30f));
            }
            if (i % 2 == 0)
            {
                PonerLocal("Mercado/SM_Toldo_Manta", vendedor.transform, new Vector3(0f, 0f, -0.25f), 0f);   // toldo de manta
            }
        }
    }

    // ---------- trojes: mazorcas detrás y canastos a un costado ----------
    static void DecorarTrojes(Transform padre)
    {
        foreach (TrojeDatos t in mapa.trojes)
        {
            ZonaDatos zona = Zona(t.zone);
            Vector2 troje = Centro(t.rect);
            Vector2 puerta = Centro(zona.gate);
            Vector2 atras = (troje - puerta).normalized;           // del lado contrario a la puerta
            Vector2 lado = new Vector2(-atras.y, atras.x);

            Vector2 monton = DentroDeZona(troje + atras * 2.1f * M, zona, troje - atras * 2.1f * M);
            Poner("Trojes/SM_Mazorcas_Monton", monton, Rango(0f, 360f), padre);

            Vector2 grupo = DentroDeZona(troje + lado * 2f * M, zona, troje - lado * 2f * M);
            Poner("Mercado/SM_Canasto_Mazorcas", grupo, Rango(0f, 360f), padre);
            Poner("Mercado/SM_Canasto_Vacio", grupo + lado * 0.45f * M + atras * 0.2f * M, Rango(0f, 360f), padre);
            Poner("Mercado/SM_Olla_Roja", grupo - lado * 0.4f * M + atras * 0.3f * M, Rango(0f, 360f), padre);
        }
    }

    // ---------- casas: fogón, leña, cántaro y huerto ----------
    static void DecorarCasas(Transform padre)
    {
        List<RectDatos> casas = new List<RectDatos>();
        foreach (ObstaculoDatos o in mapa.obstacles)
        {
            if (o.kind == "house" && ZonaDe(Centro(o.rect)) == null)
            {
                casas.Add(o.rect);
            }
        }
        for (int i = 0; i < casas.Count; i++)
        {
            RectDatos c = casas[i];
            Transform casa = Grupo("Casa_" + (i + 1), padre);
            float norte = c.y;                                   // la puerta da al norte

            // fogón de tres piedras con comal, al frente y a un lado de la puerta
            Vector2 fogon = new Vector2(c.x + c.w - 0.7f * M, norte - 1.3f * M);
            if (Libre(fogon, 0.5f * M))
            {
                Poner("Casas/SM_Fogon_Comal", fogon, Rango(0f, 360f), casa);
            }
            // leña contra el muro oeste, a lo largo del muro
            Vector2 lena = new Vector2(c.x - 0.4f * M, c.y + c.h / 2f);
            if (Libre(lena, 0.3f * M))
            {
                Poner("Casas/SM_Lena_Pila", lena, 90f, casa);
            }
            // cántaro junto a la puerta en una de cada tres casas
            if (i % 3 == 0)
            {
                Vector2 cantaro = new Vector2(c.x + c.w / 2f - 0.9f * M, norte - 0.45f * M);
                if (Libre(cantaro, 0.2f * M))
                {
                    Poner("Mercado/SM_Cantaro_Crema", cantaro, Rango(0f, 360f), casa);
                }
            }
            // huerto atrás (al sur) en una de cada dos casas: plantas bajas que no esconden
            if (i % 2 == 0)
            {
                RectDatos huerto = new RectDatos();
                huerto.x = c.x + c.w * 0.1f;
                huerto.y = c.y + c.h + 0.8f * M;
                huerto.w = c.w * 0.8f;
                huerto.h = 2f * M;
                for (int k = 0; k < 4; k++)
                {
                    Vector2 planta = new Vector2(huerto.x + (k + 0.5f) / 4f * huerto.w + Rango(-0.2f, 0.2f) * M,
                                                 huerto.y + Rango(0.4f, 1.6f) * M);
                    if (Libre(planta, 0.5f * M))
                    {
                        string modelo = k % 2 == 0 ? "Milpa/SM_Calabaza" : "Milpa/SM_Rastrojo";
                        Poner(modelo, planta, Rango(0f, 360f), casa);
                    }
                }
            }
        }
    }

    // ---------- almacenes ----------
    static void DecorarAlmacenes(Transform padre)
    {
        // A: canastos y petates bajo la ramada (el servidor ya bloquea ahí: nadie los atraviesa)
        RectDatos ramada = CasasDentro(Zona("A"))[0];
        Vector2 r = Centro(ramada);
        Poner("Mercado/SM_Canasto_Mazorcas", r + new Vector2(-0.8f, -0.3f) * M, Rango(0f, 360f), padre);
        Poner("Mercado/SM_Canasto_Frijol", r + new Vector2(-0.3f, 0.35f) * M, Rango(0f, 360f), padre);
        Poner("Mercado/SM_Canasto_Mazorcas", r + new Vector2(0.4f, -0.4f) * M, Rango(0f, 360f), padre);
        Poner("Mercado/SM_Petates_Enrollados", r + new Vector2(0.6f, 0.4f) * M, 0f, padre);

        // B: petate con navajas de obsidiana en el taller (mitad sureste, donde están las lascas)
        RectDatos b = Zona("B").rect;
        Vector2 taller = new Vector2(b.x + b.w * 0.62f, b.y + b.h * 0.6f);
        if (!Libre(taller, 0.8f * M, false))
        {
            taller = new Vector2(b.x + b.w * 0.75f, b.y + b.h * 0.45f);
        }
        GameObject petate = Poner("Mercado/SM_Petate", taller, 0f, padre);
        if (petate != null)
        {
            PonerLocal("Mercado/SM_Obsidiana_Set", petate.transform, new Vector3(-0.45f, 0.01f, 0f), 15f);
            PonerLocal("Mercado/SM_Obsidiana_Set", petate.transform, new Vector3(0.1f, 0.01f, 0.1f), -20f);
            PonerLocal("Mercado/SM_Canasto_Vacio", petate.transform, new Vector3(0.6f, 0.01f, -0.1f), 0f);
        }

        // C: fogón de la guarnición entre la bodega y el cuartel, fuera de las rutas de patrulla
        List<RectDatos> edificios = CasasDentro(Zona("C"));
        if (edificios.Count >= 2)
        {
            RectDatos izq = edificios[0].x < edificios[1].x ? edificios[0] : edificios[1];
            RectDatos der = edificios[0].x < edificios[1].x ? edificios[1] : edificios[0];
            Vector2 fogon = new Vector2((izq.x + izq.w + der.x) / 2f, (izq.y + izq.h / 2f + der.y + der.h / 2f) / 2f);
            Poner("Casas/SM_Fogon_Comal", fogon, 0f, padre);
            Poner("Mercado/SM_Olla_Roja", fogon + new Vector2(0.75f, 0.2f) * M, 0f, padre);
            Poner("Mercado/SM_Cantaro_Crema", fogon + new Vector2(-0.75f, 0.25f) * M, 0f, padre);
        }
    }

    // ---------- orilla: lo del pescador junto a la canoa ----------
    static void DecorarOrilla(Transform padre)
    {
        float y = mapa.canoe.y - 3f * M;                         // 3 m al norte de la canoa
        Vector2 p = new Vector2(OrillaEn(y) + 1.6f * M, y);
        GameObject petate = Poner("Mercado/SM_Petate", p, 90f, padre);
        if (petate != null)
        {
            PonerLocal("Mercado/SM_Bandeja_Pescado", petate.transform, new Vector3(-0.4f, 0.01f, 0f), 10f);
            PonerLocal("Mercado/SM_Canasto_Vacio", petate.transform, new Vector3(0.45f, 0.01f, 0.05f), 0f);
        }
    }

    // ---------- colocar modelos ----------
    static GameObject Poner(string modelo, Vector2 puntoMapa, float giroGrados, Transform padre)
    {
        GameObject copia = Instanciar(modelo, padre);
        if (copia == null)
        {
            return null;
        }
        copia.transform.position = Mundo(puntoMapa);
        copia.transform.rotation = Quaternion.Euler(0f, giroGrados, 0f);
        return copia;
    }

    static GameObject PonerLocal(string modelo, Transform padre, Vector3 posicionLocal, float giroGrados)
    {
        GameObject copia = Instanciar(modelo, padre);
        if (copia == null)
        {
            return null;
        }
        copia.transform.localPosition = posicionLocal;
        copia.transform.localRotation = Quaternion.Euler(0f, giroGrados, 0f);
        return copia;
    }

    static GameObject Instanciar(string modelo, Transform padre)
    {
        GameObject original = AssetDatabase.LoadAssetAtPath<GameObject>(Modelos + modelo + ".fbx");
        if (original == null)
        {
            Debug.LogWarning("No encontré el modelo " + modelo);
            return null;
        }
        colocados++;
        return (GameObject)PrefabUtility.InstantiatePrefab(original, padre);
    }

    static Vector3 Mundo(Vector2 p)
    {
        return Mapa.AMundo(p.x, p.y) + new Vector3(0f, 0.01f, 0f);     // a la altura del terreno
    }

    static Quaternion MirarHacia(Vector2 desde, Vector2 hacia)
    {
        Vector3 direccion = Mundo(hacia) - Mundo(desde);
        direccion.y = 0f;
        return Quaternion.LookRotation(direccion.normalized, Vector3.up);
    }

    static Transform Grupo(string nombre, Transform padre)
    {
        GameObject grupo = new GameObject(nombre);
        grupo.transform.SetParent(padre, false);
        return grupo.transform;
    }

    static float Rango(float min, float max)
    {
        return min + (float)azar.NextDouble() * (max - min);
    }

    // ---------- reglas para no estorbar ----------
    /// <summary>¿Cabe algo de este radio aquí? Fuera del agua, de las casas, muros y caminos (y de los almacenes, si se pide).</summary>
    static bool Libre(Vector2 p, float radio, bool fueraDeAlmacenes = true)
    {
        if (p.x - radio < OrillaEn(p.y) + 0.5f * M)
        {
            return false;                       // en el agua o pegado a ella
        }
        foreach (ObstaculoDatos o in mapa.obstacles)
        {
            if (DistanciaARect(p, o.rect) < radio)
            {
                return false;
            }
        }
        if (DistanciaACaminos(p) < radio + 0.3f * M)
        {
            return false;
        }
        if (fueraDeAlmacenes && ZonaDe(p) != null)
        {
            return false;
        }
        return true;
    }

    static Vector2 DentroDeZona(Vector2 p, ZonaDatos zona, Vector2 alternativa)
    {
        RectDatos r = zona.rect;
        float margen = 0.9f * M;
        bool dentro = p.x > r.x + margen && p.x < r.x + r.w - margen && p.y > r.y + margen && p.y < r.y + r.h - margen;
        return dentro ? p : alternativa;
    }

    static bool MasCercaQue(Vector2 p, List<Vector2> puntos, float distancia)
    {
        foreach (Vector2 q in puntos)
        {
            if (Vector2.Distance(p, q) < distancia)
            {
                return true;
            }
        }
        return false;
    }

    // ---------- consultas del mapa ----------
    static ZonaDatos Zona(string id)
    {
        foreach (ZonaDatos z in mapa.zones)
        {
            if (z.id == id)
            {
                return z;
            }
        }
        return null;
    }

    static ZonaDatos ZonaDe(Vector2 p)
    {
        foreach (ZonaDatos z in mapa.zones)
        {
            if (DistanciaARect(p, z.rect) == 0f || DistanciaARect(p, z.gate) == 0f)
            {
                return z;
            }
        }
        return null;
    }

    static List<RectDatos> CasasDentro(ZonaDatos zona)
    {
        List<RectDatos> casas = new List<RectDatos>();
        foreach (ObstaculoDatos o in mapa.obstacles)
        {
            if (o.kind == "house" && DistanciaARect(Centro(o.rect), zona.rect) == 0f)
            {
                casas.Add(o.rect);
            }
        }
        return casas;
    }

    static Vector2 PuntaSurDeLaYacata()
    {
        Vector2 punta = new Vector2(mapa.plaza.x, 0f);
        foreach (ObstaculoDatos o in mapa.obstacles)
        {
            if (o.kind == "yacata")
            {
                punta.y = Mathf.Max(punta.y, o.rect.y + o.rect.h);
            }
        }
        return punta;
    }

    static float OrillaEn(float y)
    {
        float indice = Mathf.Clamp(y / 20f, 0f, mapa.shore.Length - 1.001f);
        int i = Mathf.FloorToInt(indice);
        return Mathf.Lerp(mapa.shore[i].x, mapa.shore[i + 1].x, indice - i);
    }

    static float DistanciaACaminos(Vector2 p)
    {
        // distancia al borde del camino más cercano (negativa si está encima)
        float mejor = float.MaxValue;
        foreach (CaminoDatos camino in mapa.roads)
        {
            for (int i = 0; i < camino.points.Length - 1; i++)
            {
                Vector2 a = new Vector2(camino.points[i].x, camino.points[i].y);
                Vector2 b = new Vector2(camino.points[i + 1].x, camino.points[i + 1].y);
                mejor = Mathf.Min(mejor, DistanciaASegmento(p, a, b) - camino.width / 2f);
            }
        }
        return mejor;
    }

    static float DistanciaASegmento(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 v = b - a;
        float largo2 = v.sqrMagnitude;
        float t = largo2 > 0f ? Mathf.Clamp01(Vector2.Dot(p - a, v) / largo2) : 0f;
        return Vector2.Distance(p, a + t * v);
    }

    static float DistanciaARect(Vector2 p, RectDatos r)
    {
        float dx = Mathf.Max(r.x - p.x, 0f, p.x - (r.x + r.w));
        float dy = Mathf.Max(r.y - p.y, 0f, p.y - (r.y + r.h));
        return Mathf.Sqrt(dx * dx + dy * dy);
    }

    static Vector2 Centro(RectDatos r)
    {
        return new Vector2(r.x + r.w / 2f, r.y + r.h / 2f);
    }
}
