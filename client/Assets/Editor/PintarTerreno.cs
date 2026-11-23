using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Menú "Codice > Pintar terreno del pueblo".
/// Crea un Terrain de 100 x 65 m y lo pinta con las texturas de los pisos según el mapa
/// del servidor (GET /map). Un Terrain mezcla varias "capas" de textura; cuánto se ve cada
/// una en cada punto lo dice el "mapa de pesos" (alphamap), que aquí se calcula por código.
/// Para que se vea vivo, los bordes y las manchas usan ruido Perlin (valores suaves y orgánicos):
/// - manchas de tierra pelada y de tierra húmeda por todo el pueblo,
/// - caminos de ancho irregular; el principal (del inicio al mercado) con lajas sueltas,
/// - patios, veredas de la puerta al camino y huertos en las casas,
/// - plaza con centro de barro aplanado, yácata rodeada de lajas con calzada a la plaza,
/// - cada almacén con su piso y sus detalles (pasos, grano, lascas de obsidiana, patio de lajas),
/// - milpa con bordes orgánicos, orilla irregular y una playita junto a la canoa.
/// El terreno es solo visual: el suelo con colisión sigue siendo la caja del graybox.
/// Requiere que el servidor esté corriendo.
/// </summary>
public static class PintarTerreno
{
    const string CarpetaTexturas = "Assets/Art/Textures/Pisos/";
    const string CarpetaTerreno = "Assets/Art/Terreno";
    const string RutaDatos = "Assets/Art/Terreno/TerrenoPueblo.asset";
    const string RutaEscena = "Assets/Scenes/Pueblo.unity";
    const int Resolucion = 1024;                   // texels del mapa de pesos (~13 cm cada uno)
    const float M = 20f;                           // 1 metro = 20 px del mapa

    // Terreno decorativo fuera del mapa jugable (metros). Las paredes invisibles del graybox
    // siguen en el borde del mapa: el jugador no puede salir.
    public const float ExtraEste = 50f;
    public const float ExtraSur = 30f;
    public const float ExtraNorte = 3f;            // al norte empieza el agua: solo una franja de orilla

    // Caminitos que salen del pueblo hacia afuera (px del mapa; el mapa jugable mide 2000 x 1300)
    public static readonly Vector2[][] CaminitosExteriores =
    {
        new Vector2[] { new Vector2(2000, 700), new Vector2(2300, 690), new Vector2(2600, 725), new Vector2(3000, 705) },                     // la entrada del este sigue
        new Vector2[] { new Vector2(2300, 690), new Vector2(2440, 1000), new Vector2(2510, 1400), new Vector2(2600, 1900) }, // se desvía al sureste
        new Vector2[] { new Vector2(1220, 1080), new Vector2(1215, 1300), new Vector2(1170, 1600), new Vector2(1240, 1900) }, // baja al sur
        new Vector2[] { new Vector2(272, 1175), new Vector2(320, 1400), new Vector2(340, 1650), new Vector2(310, 1900) },   // sigue la orilla del lago
    };
    public static readonly float[] AnchoCaminitos = { 36f, 24f, 24f, 22f };

    // Milpas grandes fuera del mapa: (x, y, ancho, alto) en px. Solo decoración.
    // Las usa también ColocarDecoracion para poner las plantas encima.
    // Al sur empiezan 8 m después del borde (y >= 1460) para no tapar al jugador desde la cámara.
    public static readonly Vector4[] MilpasExteriores =
    {
        new Vector4(2100, 150, 300, 300),     // este, arriba de la entrada
        new Vector4(2550, 250, 350, 300),     // este, arriba de la entrada (más lejos)
        new Vector4(2650, 950, 300, 350),     // este, al oriente del caminito del sureste
        new Vector4(2120, 1000, 260, 350),    // este, al poniente del caminito del sureste
        new Vector4(600, 1480, 300, 300),     // sur
        new Vector4(1450, 1500, 350, 300),    // sur, entre los caminitos
        new Vector4(1950, 1550, 300, 300),    // sur, hacia el este
    };

    // Casas fuera del mapa (x, y, ancho, alto en px) y su modelo. Solo decoración.
    public static readonly Vector4[] CasasExteriores =
    {
        new Vector4(2420, 560, 80, 60),       // este, junto a la entrada (4 x 3 m)
        new Vector4(960, 1390, 70, 60),       // sur, 4.5 m después del borde (3.5 x 3 m)
    };
    public static readonly string[] ModelosCasasExteriores = { "Casas/SM_Casa_T5", "Casas/SM_Casa_T6" };

    // Capas (en este orden)
    const int TierraVolcanica = 0;
    const int TierraApisonada = 1;
    const int Sendero = 2;
    const int Lajas = 3;
    const int AplanadoBarro = 4;
    const int OrillaLago = 5;
    const int SueloMilpa = 6;
    const int LascasObsidiana = 7;

    static readonly string[] NombresCapas =
    {
        "P1_TierraVolcanica", "P2_TierraApisonada", "P3_Sendero", "P4_Lajas",
        "P5_AplanadoBarro", "P6_OrillaLago", "P7_SueloMilpa", "P8_LascasObsidiana",
    };
    static readonly float[] TamanoMosaico = { 3f, 2.5f, 2f, 1.6f, 2f, 3f, 2.5f, 1.5f };   // metros por repetición

    // Un tramo recto de vereda o de paso (en px del mapa)
    class Tramo
    {
        public Vector2 a;
        public Vector2 b;
        public float ancho;
        public int capa;
        public float fuerza;
    }

    [MenuItem("Codice/Pintar terreno del pueblo")]
    public static void Pintar()
    {
        PintarEnEscenaActual();
    }

    /// <summary>
    /// Para la línea de comandos: abre la escena Pueblo, pinta el terreno y la guarda.
    /// Unity.exe -batchmode -projectPath client -executeMethod PintarTerreno.PintarEnEscenaPueblo -quit
    /// </summary>
    public static void PintarEnEscenaPueblo()
    {
        EditorSceneManager.OpenScene(RutaEscena, OpenSceneMode.Single);
        if (!PintarEnEscenaActual())
        {
            EditorApplication.Exit(1);
            return;
        }
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("Escena guardada con el terreno: " + RutaEscena);
    }

    static bool PintarEnEscenaActual()
    {
        string json = GeneradorGraybox.DescargarMapa();
        if (json == null)
        {
            return false;
        }
        MapaDatos mapa = JsonUtility.FromJson<MapaDatos>(json);

        // ---------- capas de textura ----------
        TerrainLayer[] capas = new TerrainLayer[NombresCapas.Length];
        for (int i = 0; i < capas.Length; i++)
        {
            capas[i] = CrearCapa(NombresCapas[i], TamanoMosaico[i]);
            if (capas[i] == null)
            {
                return false;
            }
        }

        // ---------- datos del terreno (plano, 100 x 65 m) ----------
        float[,,] pesos;
        try
        {
            pesos = CalcularPesos(mapa, capas.Length);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
        if (AssetDatabase.LoadAssetAtPath<TerrainData>(RutaDatos) != null)
        {
            AssetDatabase.DeleteAsset(RutaDatos);
        }
        TerrainData datos = new TerrainData();
        datos.heightmapResolution = 33;            // es plano: basta con poca resolución
        // el mapa jugable (100 x 65 m) más la franja decorativa al este, al sur y al norte
        datos.size = new Vector3(mapa.worldW * Mapa.UnidadesPorPixel + ExtraEste, 1f,
                                 mapa.worldH * Mapa.UnidadesPorPixel + ExtraSur + ExtraNorte);
        // primero se guarda como archivo y DESPUÉS se pinta: si se pinta antes, el pintado
        // solo vive en memoria y se pierde al dar Play
        AssetDatabase.CreateAsset(datos, RutaDatos);
        datos.alphamapResolution = Resolucion;
        datos.terrainLayers = capas;
        datos.SetAlphamaps(0, 0, pesos);
        // las texturas internas del pintado ("splatmaps") también se guardan dentro del archivo
        foreach (Texture2D splat in datos.alphamapTextures)
        {
            if (!AssetDatabase.Contains(splat))
            {
                AssetDatabase.AddObjectToAsset(splat, datos);
            }
        }
        EditorUtility.SetDirty(datos);
        AssetDatabase.SaveAssets();

        // ---------- objeto Terreno en la escena ----------
        GameObject anterior = GameObject.Find("Terreno");
        if (anterior != null)
        {
            Undo.DestroyObjectImmediate(anterior);
        }
        GameObject terreno = Terrain.CreateTerrainGameObject(datos);
        terreno.name = "Terreno";
        // el Terrain crece desde su esquina sur-oeste: la esquina noroeste del mapa es (0, 0, 0)
        // 1 cm arriba del suelo del graybox para que no se encimen
        terreno.transform.position = new Vector3(0f, 0.01f, ExtraNorte - datos.size.z);
        Object.DestroyImmediate(terreno.GetComponent<TerrainCollider>());   // la colisión es la del graybox
        Undo.RegisterCreatedObjectUndo(terreno, "Pintar terreno");

        // apagar lo que el terreno reemplaza (sus colliders siguen activos)
        ApagarCaja("Graybox/Suelo");
        ApagarCaja("Graybox/Plaza");

        EditorSceneManager.MarkSceneDirty(terreno.scene);
        Selection.activeGameObject = terreno;
        Debug.Log("Terreno pintado con " + capas.Length + " capas.");
        return true;
    }

    // ---------- mapa de pesos ----------
    static float[,,] CalcularPesos(MapaDatos mapa, int numeroCapas)
    {
        // --- preparar los datos que se usan en cada punto ---
        List<RectDatos> casasFuera = new List<RectDatos>();
        List<RectDatos> yacata = new List<RectDatos>();
        foreach (ObstaculoDatos o in mapa.obstacles)
        {
            if (o.kind == "house" && ZonaDe(mapa, Centro(o.rect)) == null)
            {
                casasFuera.Add(o.rect);
            }
            if (o.kind == "yacata")
            {
                yacata.Add(o.rect);
            }
        }

        // veredas: de la puerta de cada casa (lado norte) al punto más cercano de un camino
        List<Tramo> tramos = new List<Tramo>();
        foreach (RectDatos casa in casasFuera)
        {
            Vector2 puerta = new Vector2(casa.x + casa.w / 2f, casa.y - 4f);
            Vector2 destino = PuntoMasCercanoDeLosCaminos(mapa, puerta);
            if (Vector2.Distance(puerta, destino) < 30f * M)
            {
                tramos.Add(NuevoTramo(puerta, destino, 0.9f * M, Sendero, 0.85f));
            }
        }
        // calzada de lajas de la escalinata de la yácata a la plaza
        Vector2 puntaYacata = new Vector2(mapa.plaza.x, 0f);
        foreach (RectDatos r in yacata)
        {
            puntaYacata.y = Mathf.Max(puntaYacata.y, r.y + r.h);
        }
        tramos.Add(NuevoTramo(puntaYacata, new Vector2(mapa.plaza.x, mapa.plaza.y - mapa.plaza.r + 30f), 2.2f * M, Lajas, 1f));

        // pasos dentro de los almacenes y explanadas frente a sus puertas
        List<Vector2> explanadas = new List<Vector2>();
        foreach (ZonaDatos zona in mapa.zones)
        {
            Vector2 puerta = Centro(zona.gate);
            float haciaAfuera = puerta.y < Centro(zona.rect).y ? -1f : 1f;
            explanadas.Add(new Vector2(puerta.x, puerta.y + haciaAfuera * 2f * M));
            Vector2 troje = CentroDelTroje(mapa, zona.id);
            if (zona.id == "A")
            {
                tramos.Add(NuevoTramo(puerta, troje, 1.5f * M, Sendero, 0.8f));
                foreach (RectDatos casa in CasasDentro(mapa, zona))
                {
                    tramos.Add(NuevoTramo(puerta, Centro(casa), 1.3f * M, Sendero, 0.7f));
                }
            }
            else if (zona.id == "B")
            {
                foreach (RectDatos casa in CasasDentro(mapa, zona))
                {
                    tramos.Add(NuevoTramo(puerta, Centro(casa), 1.3f * M, Sendero, 0.7f));
                }
            }
            else
            {
                // C: calzada de lajas desde la puerta (sur) hacia el centro del patio
                tramos.Add(NuevoTramo(puerta, new Vector2(puerta.x, zona.rect.y + zona.rect.h * 0.4f), 2.5f * M, Lajas, 1f));
            }
        }

        // --- pintar punto por punto ---
        float[,,] pesos = new float[Resolucion, Resolucion, numeroCapas];
        float[] w = new float[numeroCapas];
        for (int fila = 0; fila < Resolucion; fila++)
        {
            if (fila % 64 == 0)
            {
                EditorUtility.DisplayProgressBar("Pintando el terreno", "Fila " + fila + " de " + Resolucion, fila / (float)Resolucion);
            }
            for (int col = 0; col < Resolucion; col++)
            {
                // fila 0 = borde sur del mapa (y = worldH); última fila = borde norte (y = 0)
                // en px del mapa; fuera del mapa jugable x pasa de worldW, y pasa de worldH (sur) o es negativa (norte)
                float anchoPx = mapa.worldW + ExtraEste * M;
                float altoPx = mapa.worldH + (ExtraSur + ExtraNorte) * M;
                float x = col / (float)(Resolucion - 1) * anchoPx;
                float y = mapa.worldH + ExtraSur * M - fila / (float)(Resolucion - 1) * altoPx;

                for (int c = 0; c < numeroCapas; c++)
                {
                    w[c] = 0f;
                }
                w[TierraVolcanica] = 1f;

                PintarManchasNaturales(w, x, y);
                PintarMilpaYHuertos(w, mapa, casasFuera, x, y);
                PintarOrilla(w, mapa, x, y);
                PintarPatios(w, casasFuera, x, y);
                PintarPlazaYYacata(w, mapa, yacata, x, y);
                PintarCaminos(w, mapa, x, y);
                PintarExterior(w, x, y);
                foreach (Tramo t in tramos)
                {
                    float d = DistanciaASegmento(x, y, t.a, t.b) - t.ancho / 2f + Irregular(x, y, 0.25f * M, 1f / 30f, 4.2f);
                    Pintar(w, t.capa, t.fuerza * Borde(d, 0f, 0.4f * M));
                }
                foreach (Vector2 e in explanadas)
                {
                    float d = Vector2.Distance(new Vector2(x, y), e) + Irregular(x, y, 0.6f * M, 1f / 40f, 8.8f);
                    Pintar(w, TierraApisonada, Borde(d, 1.5f * M, 4f * M));
                    Pintar(w, Sendero, 0.6f * Borde(d, 0.5f * M, 2.5f * M));
                }
                PintarAlmacenes(w, mapa, x, y);

                for (int c = 0; c < numeroCapas; c++)
                {
                    pesos[fila, col, c] = w[c];
                }
            }
        }
        return pesos;
    }

    // ---------- cada parte del pueblo ----------
    static void PintarManchasNaturales(float[] w, float x, float y)
    {
        // manchas grandes de tierra pelada y de tierra húmeda; el grano fino las rompe
        float grano = 0.7f + 0.6f * Ruido(x, y, 1f / 40f, 5.5f);
        float pelada = Mathf.InverseLerp(0.58f, 0.75f, Ruido(x, y, 1f / 260f, 3.1f));
        Pintar(w, TierraApisonada, 0.55f * pelada * grano);
        float humeda = Mathf.InverseLerp(0.34f, 0.2f, Ruido(x, y, 1f / 180f, 11.7f));
        Pintar(w, SueloMilpa, 0.4f * humeda * grano);
    }

    static void PintarMilpaYHuertos(float[] w, MapaDatos mapa, List<RectDatos> casasFuera, float x, float y)
    {
        foreach (RectDatos m in mapa.maize)
        {
            float d = DistanciaARect(x, y, m) + Irregular(x, y, 0.8f * M, 1f / 50f, 1.9f);
            Pintar(w, SueloMilpa, Borde(d, 0.5f * M, 2f * M));
        }
        // huerto detrás (al sur) de una de cada dos casas
        for (int i = 0; i < casasFuera.Count; i += 2)
        {
            RectDatos c = casasFuera[i];
            RectDatos huerto = new RectDatos();
            huerto.x = c.x + c.w * 0.1f;
            huerto.y = c.y + c.h + 0.8f * M;
            huerto.w = c.w * 0.8f;
            huerto.h = 2f * M;
            float d = DistanciaARect(x, y, huerto) + Irregular(x, y, 0.3f * M, 1f / 25f, 6.6f);
            Pintar(w, SueloMilpa, 0.9f * Borde(d, 0f, 0.5f * M));
        }
    }

    static void PintarOrilla(float[] w, MapaDatos mapa, float x, float y)
    {
        float d = x - OrillaEn(mapa, y) + Irregular(x, y, 1.2f * M, 1f / 50f, 2.3f);
        Pintar(w, OrillaLago, Borde(d, 1.2f * M, 4f * M));
        // playita donde está la canoa
        float dc = Vector2.Distance(new Vector2(x, y), new Vector2(mapa.canoe.x, mapa.canoe.y)) + Irregular(x, y, 0.8f * M, 1f / 40f, 9.4f);
        Pintar(w, OrillaLago, Borde(dc, 3f * M, 6.5f * M));
    }

    static void PintarPatios(float[] w, List<RectDatos> casasFuera, float x, float y)
    {
        foreach (RectDatos c in casasFuera)
        {
            float d = DistanciaARect(x, y, c) + Irregular(x, y, 0.6f * M, 1f / 30f, 7.3f);
            Pintar(w, TierraApisonada, 0.9f * Borde(d, 0.5f * M, 1.8f * M));
        }
    }

    static void PintarPlazaYYacata(float[] w, MapaDatos mapa, List<RectDatos> yacata, float x, float y)
    {
        float centro = Vector2.Distance(new Vector2(x, y), new Vector2(mapa.plaza.x, mapa.plaza.y));
        float dPlaza = centro - mapa.plaza.r + Irregular(x, y, 1f * M, 1f / 45f, 12.1f);
        Pintar(w, TierraApisonada, Borde(dPlaza, 0f, 1.5f * M));
        // centro del mercado de barro aplanado (donde están los puestos)
        float dMercado = centro - 5f * M + Irregular(x, y, 0.5f * M, 1f / 30f, 14.6f);
        Pintar(w, AplanadoBarro, 0.85f * Borde(dMercado, 0f, 1.2f * M));

        // lajas alrededor de la yácata
        float dYacata = float.MaxValue;
        foreach (RectDatos r in yacata)
        {
            dYacata = Mathf.Min(dYacata, DistanciaARect(x, y, r));
        }
        dYacata += Irregular(x, y, 0.4f * M, 1f / 25f, 15.2f);
        Pintar(w, Lajas, Borde(dYacata, 1.2f * M, 2.2f * M));
    }

    static void PintarExterior(float[] w, float x, float y)
    {
        // orilla del agua del norte (el agua empieza justo afuera del borde norte del mapa)
        float dNorte = y + Irregular(x, y, 0.8f * M, 1f / 45f, 22.4f);
        Pintar(w, OrillaLago, Borde(dNorte, 0.8f * M, 3f * M));

        // suelo de milpa bajo las milpas grandes de afuera
        foreach (Vector4 m in MilpasExteriores)
        {
            float dx = Mathf.Max(m.x - x, 0f, x - (m.x + m.z));
            float dy = Mathf.Max(m.y - y, 0f, y - (m.y + m.w));
            float d = Mathf.Sqrt(dx * dx + dy * dy) + Irregular(x, y, 1f * M, 1f / 50f, 50f);
            Pintar(w, SueloMilpa, Borde(d, 0.5f * M, 2.2f * M));
        }

        // patio de tierra apisonada alrededor de las casas de afuera
        foreach (Vector4 c in CasasExteriores)
        {
            float dx = Mathf.Max(c.x - x, 0f, x - (c.x + c.z));
            float dy = Mathf.Max(c.y - y, 0f, y - (c.y + c.w));
            float d = Mathf.Sqrt(dx * dx + dy * dy) + Irregular(x, y, 0.6f * M, 1f / 30f, 60f);
            Pintar(w, TierraApisonada, 0.9f * Borde(d, 0.6f * M, 2f * M));
        }

        // caminitos que salen del pueblo
        for (int i = 0; i < CaminitosExteriores.Length; i++)
        {
            Vector2[] puntos = CaminitosExteriores[i];
            float distancia = float.MaxValue;
            for (int k = 0; k < puntos.Length - 1; k++)
            {
                distancia = Mathf.Min(distancia, DistanciaASegmento(x, y, puntos[k], puntos[k + 1]));
            }
            float medioAncho = AnchoCaminitos[i] / 2f * (0.8f + 0.4f * Ruido(x, y, 1f / 100f, 30f + i));
            float d = distancia - medioAncho + Irregular(x, y, 0.4f * M, 1f / 30f, 40f + i);
            Pintar(w, TierraApisonada, 0.45f * Borde(d, 0f, 0.9f * M));
            Pintar(w, Sendero, 0.9f * Borde(d, 0f, 0.4f * M));
        }
    }

    static void PintarCaminos(float[] w, MapaDatos mapa, float x, float y)
    {
        for (int i = 0; i < mapa.roads.Length; i++)
        {
            CaminoDatos camino = mapa.roads[i];
            // el ancho cambia un poco a lo largo del camino y la orilla es irregular
            float medioAncho = camino.width / 2f * (0.85f + 0.3f * Ruido(x, y, 1f / 120f, 7f + i));
            float d = DistanciaACamino(x, y, camino) - medioAncho + Irregular(x, y, 0.5f * M, 1f / 35f, 3.3f + i);
            Pintar(w, TierraApisonada, 0.5f * Borde(d, 0f, 1.1f * M));     // orillas de tierra pisada
            Pintar(w, Sendero, Borde(d, 0f, 0.5f * M));

            // el camino principal (el primero: del inicio al mercado) lleva lajas sueltas en el centro
            if (i == 0 && d < -0.4f * M)
            {
                float laja = Mathf.InverseLerp(0.66f, 0.74f, Ruido(x, y, 1f / 14f, 21f));
                Pintar(w, Lajas, 0.85f * laja);
            }
        }
    }

    static void PintarAlmacenes(float[] w, MapaDatos mapa, float x, float y)
    {
        foreach (ZonaDatos zona in mapa.zones)
        {
            float dentro = Borde(DistanciaARect(x, y, zona.rect), 0f, 0.3f * M);
            if (dentro <= 0f)
            {
                continue;
            }
            RectDatos r = zona.rect;
            if (zona.id == "A")
            {
                // mercado: tierra apisonada, con grano y tierra regada junto al troje
                Pintar(w, TierraApisonada, dentro);
                Vector2 troje = CentroDelTroje(mapa, "A");
                float dt = Vector2.Distance(new Vector2(x, y), troje) + Irregular(x, y, 0.4f * M, 1f / 20f, 16.4f);
                Pintar(w, SueloMilpa, 0.55f * dentro * Borde(dt, 1.5f * M, 2.8f * M));
                Pintar(w, Sendero, 0.8f * dentro * PasoAlTroje(mapa, zona, x, y));
            }
            else if (zona.id == "B")
            {
                // flechas: barro aplanado y lascas de obsidiana en el taller (mitad sureste)
                Pintar(w, AplanadoBarro, dentro);
                RectDatos taller = new RectDatos();
                taller.x = r.x + r.w * 0.45f;
                taller.y = r.y + r.h * 0.45f;
                taller.w = r.w * 0.55f;
                taller.h = r.h * 0.55f;
                float enTaller = Borde(DistanciaARect(x, y, taller), 0f, 1.5f * M);
                float lascas = Mathf.InverseLerp(0.48f, 0.62f, Ruido(x, y, 1f / 28f, 13f));
                Pintar(w, LascasObsidiana, 0.9f * dentro * enTaller * lascas);
                Pintar(w, Sendero, 0.7f * dentro * PasoAlTroje(mapa, zona, x, y));
            }
            else
            {
                // guarnición: tierra pisada, patio central de lajas y barro junto a los edificios
                Pintar(w, TierraApisonada, dentro);
                RectDatos patio = new RectDatos();
                patio.x = r.x + 3.5f * M;
                patio.y = r.y + 3f * M;
                patio.w = r.w - 7f * M;
                patio.h = r.h * 0.45f;
                float dp = DistanciaARect(x, y, patio) + Irregular(x, y, 0.9f * M, 1f / 30f, 17.7f);
                Pintar(w, Lajas, dentro * Borde(dp, 0f, 1f * M));
                foreach (RectDatos casa in CasasDentro(mapa, zona))
                {
                    float dc = DistanciaARect(x, y, casa) + Irregular(x, y, 0.3f * M, 1f / 20f, 18.3f);
                    Pintar(w, AplanadoBarro, 0.8f * dentro * Borde(dc, 0.4f * M, 1.3f * M));
                }
            }
        }
    }

    static float PasoAlTroje(MapaDatos mapa, ZonaDatos zona, float x, float y)
    {
        // camino de tierra pisada de la puerta del almacén a su troje
        Vector2 puerta = Centro(zona.gate);
        Vector2 troje = CentroDelTroje(mapa, zona.id);
        float d = DistanciaASegmento(x, y, puerta, troje) - 0.7f * M + Irregular(x, y, 0.25f * M, 1f / 25f, 19.9f);
        return Borde(d, 0f, 0.5f * M);
    }

    // ---------- herramientas de pintura ----------
    /// <summary>Cubre las capas actuales con la capa dada, con fuerza "a" (0 a 1). Los pesos siguen sumando 1.</summary>
    static void Pintar(float[] w, int capa, float a)
    {
        if (a <= 0f)
        {
            return;
        }
        a = Mathf.Clamp01(a);
        for (int i = 0; i < w.Length; i++)
        {
            w[i] *= 1f - a;
        }
        w[capa] += a;
    }

    /// <summary>1 si la distancia es menor que "dentro", 0 si es mayor que "fuera", y una transición suave en medio.</summary>
    static float Borde(float distancia, float dentro, float fuera)
    {
        if (distancia <= dentro)
        {
            return 1f;
        }
        if (distancia >= fuera)
        {
            return 0f;
        }
        return Mathf.SmoothStep(1f, 0f, (distancia - dentro) / (fuera - dentro));
    }

    /// <summary>Ruido Perlin entre 0 y 1. "escala" = 1 / tamaño de las manchas en px; "semilla" cambia el dibujo.</summary>
    static float Ruido(float x, float y, float escala, float semilla)
    {
        return Mathf.PerlinNoise(x * escala + semilla * 17.3f, y * escala + semilla * 9.1f);
    }

    /// <summary>Desplazamiento entre -amplitud y +amplitud, para que los bordes no sean rectos.</summary>
    static float Irregular(float x, float y, float amplitud, float escala, float semilla)
    {
        return (Ruido(x, y, escala, semilla) - 0.5f) * 2f * amplitud;
    }

    // ---------- geometría ----------
    static float DistanciaARect(float x, float y, RectDatos r)
    {
        // 0 dentro del rectángulo; si no, la distancia al borde más cercano
        float dx = Mathf.Max(r.x - x, 0f, x - (r.x + r.w));
        float dy = Mathf.Max(r.y - y, 0f, y - (r.y + r.h));
        return Mathf.Sqrt(dx * dx + dy * dy);
    }

    static float DistanciaACamino(float x, float y, CaminoDatos camino)
    {
        float mejor = float.MaxValue;
        for (int i = 0; i < camino.points.Length - 1; i++)
        {
            Vector2 a = new Vector2(camino.points[i].x, camino.points[i].y);
            Vector2 b = new Vector2(camino.points[i + 1].x, camino.points[i + 1].y);
            mejor = Mathf.Min(mejor, DistanciaASegmento(x, y, a, b));
        }
        return mejor;
    }

    static float DistanciaASegmento(float x, float y, Vector2 a, Vector2 b)
    {
        Vector2 p = new Vector2(x, y);
        return Vector2.Distance(p, PuntoEnSegmento(p, a, b));
    }

    static Vector2 PuntoEnSegmento(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 v = b - a;
        float largo2 = v.sqrMagnitude;
        float t = largo2 > 0f ? Mathf.Clamp01(Vector2.Dot(p - a, v) / largo2) : 0f;
        return a + t * v;
    }

    static Vector2 PuntoMasCercanoDeLosCaminos(MapaDatos mapa, Vector2 p)
    {
        Vector2 mejor = p;
        float menor = float.MaxValue;
        foreach (CaminoDatos camino in mapa.roads)
        {
            for (int i = 0; i < camino.points.Length - 1; i++)
            {
                Vector2 a = new Vector2(camino.points[i].x, camino.points[i].y);
                Vector2 b = new Vector2(camino.points[i + 1].x, camino.points[i + 1].y);
                Vector2 q = PuntoEnSegmento(p, a, b);
                float d = Vector2.Distance(p, q);
                if (d < menor)
                {
                    menor = d;
                    mejor = q;
                }
            }
        }
        return mejor;
    }

    static float OrillaEn(MapaDatos mapa, float y)
    {
        // la orilla viene en puntos cada 20 px: se interpola entre los dos más cercanos
        float indice = Mathf.Clamp(y / 20f, 0f, mapa.shore.Length - 1.001f);
        int i = Mathf.FloorToInt(indice);
        return Mathf.Lerp(mapa.shore[i].x, mapa.shore[i + 1].x, indice - i);
    }

    static Vector2 Centro(RectDatos r)
    {
        return new Vector2(r.x + r.w / 2f, r.y + r.h / 2f);
    }

    static ZonaDatos ZonaDe(MapaDatos mapa, Vector2 p)
    {
        foreach (ZonaDatos z in mapa.zones)
        {
            if (DistanciaARect(p.x, p.y, z.rect) == 0f)
            {
                return z;
            }
        }
        return null;
    }

    static List<RectDatos> CasasDentro(MapaDatos mapa, ZonaDatos zona)
    {
        List<RectDatos> casas = new List<RectDatos>();
        foreach (ObstaculoDatos o in mapa.obstacles)
        {
            if (o.kind == "house" && DistanciaARect(o.rect.x + o.rect.w / 2f, o.rect.y + o.rect.h / 2f, zona.rect) == 0f)
            {
                casas.Add(o.rect);
            }
        }
        return casas;
    }

    static Vector2 CentroDelTroje(MapaDatos mapa, string zona)
    {
        foreach (TrojeDatos t in mapa.trojes)
        {
            if (t.zone == zona)
            {
                return Centro(t.rect);
            }
        }
        return Vector2.zero;
    }

    static Tramo NuevoTramo(Vector2 a, Vector2 b, float ancho, int capa, float fuerza)
    {
        Tramo t = new Tramo();
        t.a = a;
        t.b = b;
        t.ancho = ancho;
        t.capa = capa;
        t.fuerza = fuerza;
        return t;
    }

    // ---------- capas y escena ----------
    static TerrainLayer CrearCapa(string nombre, float mosaico)
    {
        if (!AssetDatabase.IsValidFolder(CarpetaTerreno))
        {
            AssetDatabase.CreateFolder("Assets/Art", "Terreno");
        }
        Texture2D color = AssetDatabase.LoadAssetAtPath<Texture2D>(CarpetaTexturas + "T_" + nombre + "_BaseColor.png");
        if (color == null)
        {
            Debug.LogError("No encontré la textura T_" + nombre + "_BaseColor.png");
            return null;
        }
        string ruta = CarpetaTerreno + "/TL_" + nombre + ".terrainlayer";
        TerrainLayer capa = AssetDatabase.LoadAssetAtPath<TerrainLayer>(ruta);
        if (capa == null)
        {
            capa = new TerrainLayer();
            AssetDatabase.CreateAsset(capa, ruta);
        }
        capa.diffuseTexture = color;
        capa.normalMapTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(CarpetaTexturas + "T_" + nombre + "_Normal.png");
        capa.maskMapTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(CarpetaTexturas + "T_" + nombre + "_Mask.png");
        capa.tileSize = new Vector2(mosaico, mosaico);
        EditorUtility.SetDirty(capa);
        return capa;
    }

    static void ApagarCaja(string ruta)
    {
        GameObject caja = GameObject.Find(ruta);
        if (caja == null)
        {
            return;
        }
        Renderer dibujo = caja.GetComponent<Renderer>();
        if (dibujo != null)
        {
            Undo.RecordObject(dibujo, "Apagar caja del graybox");
            dibujo.enabled = false;
        }
    }
}
