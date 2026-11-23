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
        DecorarMilpasExteriores(Grupo("MilpasExteriores", raiz.transform));   // al final: no cambia lo anterior
        PrepararMaterialVegetacion();
        DecorarCasasExteriores(Grupo("CasasExteriores", raiz.transform));
        DecorarRincones(Grupo("Rincones", raiz.transform));
        DecorarVegetacion(Grupo("Vegetacion", raiz.transform));
        DecorarVegetacionInterior(Grupo("VegetacionInterior", raiz.transform));

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

    // ---------- milpas grandes fuera del mapa ----------
    static void DecorarMilpasExteriores(Transform padre)
    {
        // Cada milpa es una cuadrícula de parcelas de ~4.5 x 5 m (variantes alternadas, giradas
        // 0° o 180°). Algunas esquinas se dejan vacías para que el borde no quede cuadrado.
        string[] parcelas = { "Milpa/SM_Milpa_Parcela", "Milpa/SM_Maizal_0", "Milpa/SM_Maizal_2", "Milpa/SM_Maizal_5", "Milpa/SM_Maizal_7" };
        int numero = 1;
        foreach (Vector4 r in PintarTerreno.MilpasExteriores)
        {
            Transform milpa = Grupo("Milpa_" + numero, padre);
            numero++;
            int columnas = Mathf.Max(1, Mathf.RoundToInt(r.z / (4.5f * M)));
            int filas = Mathf.Max(1, Mathf.RoundToInt(r.w / (5f * M)));
            for (int i = 0; i < columnas; i++)
            {
                for (int j = 0; j < filas; j++)
                {
                    bool esquina = (i == 0 || i == columnas - 1) && (j == 0 || j == filas - 1);
                    if (esquina && azar.NextDouble() < 0.35)
                    {
                        continue;
                    }
                    Vector2 p = new Vector2(r.x + (i + 0.5f) * r.z / columnas + Rango(-0.3f, 0.3f) * M,
                                            r.y + (j + 0.5f) * r.w / filas + Rango(-0.3f, 0.3f) * M);
                    Poner(parcelas[azar.Next(parcelas.Length)], p, azar.Next(2) * 180f, milpa);
                }
            }
        }
    }

    // ---------- vegetación seca fuera del mapa ----------
    static readonly string[] Pastos = { "Vegetacion/SM_Hierba_Seca_01", "Vegetacion/SM_Hierba_Seca_02", "Vegetacion/SM_Hierba_Seca_03" };

    static void DecorarVegetacion(Transform padre)
    {
        List<Vector2> usados = new List<Vector2>();
        // grupitos de pasto seco (3 a 5 macollos juntos se ven más naturales que sueltos)
        foreach (Vector2 centro in PuntosAfuera(70, 2.5f * M, 0.6f * M, usados))
        {
            int cuantos = 3 + azar.Next(3);
            for (int k = 0; k < cuantos; k++)
            {
                Vector2 p = centro + new Vector2(Rango(-1.2f, 1.2f), Rango(-1.2f, 1.2f)) * M;
                if (LibreAfuera(p, 0.4f * M))
                {
                    PonerPlanta(Pastos[azar.Next(Pastos.Length)], p, padre);
                }
            }
        }
        // zacatones sueltos, y pocos magueyes y nopales
        foreach (Vector2 p in PuntosAfuera(30, 2.5f * M, 0.9f * M, usados))
        {
            PonerPlanta("Vegetacion/SM_Zacate_Alto", p, padre);
        }
        foreach (Vector2 p in PuntosAfuera(35, 3.5f * M, 1.1f * M, usados))
        {
            PonerPlanta("Vegetacion/SM_Maguey", p, padre);
        }
        foreach (Vector2 p in PuntosAfuera(12, 4f * M, 0.5f * M, usados))
        {
            PonerPlanta("Vegetacion/SM_Nopal", p, padre);
        }
    }

    static void PonerPlanta(string modelo, Vector2 p, Transform padre)
    {
        GameObject planta = Poner(modelo, p, Rango(0f, 360f), padre);
        if (planta != null)
        {
            planta.transform.localScale = Vector3.one * Rango(0.85f, 1.2f);
        }
    }

    /// <summary>Hasta "cuantos" puntos libres al azar fuera del mapa, separados al menos "separacion".</summary>
    static List<Vector2> PuntosAfuera(int cuantos, float separacion, float radio, List<Vector2> usados)
    {
        List<Vector2> puntos = new List<Vector2>();
        float derecha = mapa.worldW + PintarTerreno.ExtraEste * M - 1f * M;
        float abajo = mapa.worldH + PintarTerreno.ExtraSur * M - 1f * M;
        for (int intento = 0; intento < cuantos * 60 && puntos.Count < cuantos; intento++)
        {
            Vector2 p = new Vector2(Rango(0f, derecha), Rango(1f * M, abajo));
            if (!LibreAfuera(p, radio) || MasCercaQue(p, usados, separacion))
            {
                continue;
            }
            puntos.Add(p);
            usados.Add(p);
        }
        return puntos;
    }

    /// <summary>¿Es un lugar fuera del mapa jugable, en tierra, lejos de caminitos y milpas?</summary>
    static bool LibreAfuera(Vector2 p, float radio)
    {
        bool afuera = p.x > mapa.worldW + 2f * M || p.y > mapa.worldH + 2f * M;
        if (!afuera)
        {
            return false;
        }
        if (p.x - radio < OrillaEn(p.y) + 1.5f * M)
        {
            return false;                                   // en el agua o pegado a ella
        }
        for (int i = 0; i < PintarTerreno.CaminitosExteriores.Length; i++)
        {
            Vector2[] puntos = PintarTerreno.CaminitosExteriores[i];
            for (int k = 0; k < puntos.Length - 1; k++)
            {
                float d = DistanciaASegmento(p, puntos[k], puntos[k + 1]) - PintarTerreno.AnchoCaminitos[i] / 2f;
                if (d < radio + 0.6f * M)
                {
                    return false;
                }
            }
        }
        foreach (Vector4 m in PintarTerreno.MilpasExteriores)
        {
            if (DistanciaARect(p, Rect4(m)) < radio + 0.8f * M)
            {
                return false;
            }
        }
        foreach (Vector4 c in PintarTerreno.CasasExteriores)
        {
            if (DistanciaARect(p, Rect4(c)) < radio + 1.5f * M)
            {
                return false;
            }
        }
        return true;
    }

    static RectDatos Rect4(Vector4 v)
    {
        RectDatos r = new RectDatos();
        r.x = v.x;
        r.y = v.y;
        r.w = v.z;
        r.h = v.w;
        return r;
    }

    // ---------- casas fuera del mapa ----------
    static void DecorarCasasExteriores(Transform padre)
    {
        for (int i = 0; i < PintarTerreno.CasasExteriores.Length; i++)
        {
            RectDatos c = Rect4(PintarTerreno.CasasExteriores[i]);
            Transform casa = Grupo("CasaExterior_" + (i + 1), padre);
            Poner(PintarTerreno.ModelosCasasExteriores[i], Centro(c), 0f, casa);
            // fogón al frente (puerta al norte) y leña contra el muro oeste, como las del pueblo
            Poner("Casas/SM_Fogon_Comal", new Vector2(c.x + c.w - 0.7f * M, c.y - 1.3f * M), Rango(0f, 360f), casa);
            Poner("Casas/SM_Lena_Pila", new Vector2(c.x - 0.4f * M, c.y + c.h / 2f), 90f, casa);
            Poner("Mercado/SM_Cantaro_Crema", new Vector2(c.x + c.w / 2f - 0.9f * M, c.y - 0.45f * M), Rango(0f, 360f), casa);
        }
    }

    // ---------- rincones del mapa jugable que se veían vacíos ----------
    static void DecorarRincones(Transform padre)
    {
        List<Vector2> puestos = new List<Vector2>();

        // detrás de la yácata: ofrendas (ollas, cántaros, mazorcas), leña para el fuego y magueyes
        Transform yacata = Grupo("DetrasDeLaYacata", padre);
        Vector2 ofrenda = new Vector2(1240, 170);
        string[] ofrendas = { "Mercado/SM_Olla_Roja", "Mercado/SM_Cantaro_Crema", "Mercado/SM_Cantaro_Crema",
                              "Mercado/SM_Canasto_Mazorcas", "Mercado/SM_Petate", "Casas/SM_Lena_Pila", "Mercado/SM_Calabazas" };
        foreach (string prop in ofrendas)
        {
            PonerAlrededor(prop, ofrenda, 2.2f * M, 0.6f * M, yacata, puestos);
        }
        PonerAlrededor("Vegetacion/SM_Maguey", ofrenda, 3.5f * M, 1f * M, yacata, puestos);
        PonerAlrededor("Vegetacion/SM_Maguey", ofrenda, 3.5f * M, 1f * M, yacata, puestos);
        PonerAlrededor("Vegetacion/SM_Maguey", new Vector2(1120, 230), 1.5f * M, 1f * M, yacata, puestos);
        PonerAlrededor("Mercado/SM_Cantaro_Crema", new Vector2(1120, 230), 1.5f * M, 0.4f * M, yacata, puestos);
        for (int k = 0; k < 6; k++)
        {
            PonerAlrededor(Pastos[azar.Next(Pastos.Length)], ofrenda, 4f * M, 0.4f * M, yacata, puestos);
        }

        // esquina superior derecha (detrás de las casas del norte): cosecha guardada y magueyes
        Transform esquina = Grupo("EsquinaNoreste", padre);
        Vector2 cosecha = new Vector2(1780, 130);
        string[] cosas = { "Trojes/SM_Mazorcas_Monton", "Mercado/SM_Canasto_Vacio", "Mercado/SM_Canasto_Frijol",
                           "Casas/SM_Lena_Pila", "Mercado/SM_Calabazas", "Milpa/SM_Rastrojo" };
        foreach (string prop in cosas)
        {
            PonerAlrededor(prop, cosecha, 2.2f * M, 0.6f * M, esquina, puestos);
        }
        Vector2[] grupos = { cosecha, new Vector2(1930, 330), new Vector2(1640, 110) };
        foreach (Vector2 g in grupos)
        {
            PonerAlrededor("Vegetacion/SM_Maguey", g, 3f * M, 1f * M, esquina, puestos);
            PonerAlrededor("Vegetacion/SM_Zacate_Alto", g, 3f * M, 0.8f * M, esquina, puestos);
            for (int k = 0; k < 3; k++)
            {
                PonerAlrededor(Pastos[azar.Next(Pastos.Length)], g, 3f * M, 0.4f * M, esquina, puestos);
            }
        }
    }

    /// <summary>Pone el prop en un lugar libre al azar a menos de "radio" del centro (dentro del mapa).</summary>
    static void PonerAlrededor(string modelo, Vector2 centro, float radio, float radioProp, Transform padre, List<Vector2> puestos)
    {
        for (int intento = 0; intento < 30; intento++)
        {
            Vector2 p = centro + new Vector2(Rango(-1f, 1f), Rango(-1f, 1f)) * radio;
            if (LibreDentro(p, radioProp) && !MasCercaQue(p, puestos, radioProp + 0.5f * M))
            {
                puestos.Add(p);
                if (modelo.StartsWith("Vegetacion/"))
                {
                    PonerPlanta(modelo, p, padre);
                }
                else
                {
                    Poner(modelo, p, Rango(0f, 360f), padre);
                }
                return;
            }
        }
    }

    // ---------- vegetación dentro del mapa jugable ----------
    static void DecorarVegetacionInterior(Transform padre)
    {
        // pastos bajos dispersos (no esconden a nadie) y magueyes sobre todo cerca de los bordes
        List<Vector2> usados = new List<Vector2>();
        for (int i = 0; i < 45; i++)
        {
            Vector2 centro = PuntoDentro(2.5f * M, 0.5f * M, usados, false);
            if (centro.x < 0f)
            {
                continue;
            }
            int cuantos = 2 + azar.Next(2);
            for (int k = 0; k < cuantos; k++)
            {
                Vector2 p = centro + new Vector2(Rango(-0.9f, 0.9f), Rango(-0.9f, 0.9f)) * M;
                if (LibreDentro(p, 0.35f * M))
                {
                    PonerPlanta(Pastos[azar.Next(Pastos.Length)], p, padre);
                }
            }
        }
        for (int i = 0; i < 18; i++)
        {
            Vector2 p = PuntoDentro(4f * M, 1f * M, usados, i < 12);     // 12 cerca de los bordes, 6 donde sea
            if (p.x >= 0f)
            {
                PonerPlanta("Vegetacion/SM_Maguey", p, padre);
            }
        }
    }

    /// <summary>Un punto libre al azar dentro del mapa (o cerca de un borde); (-1, -1) si no encontró.</summary>
    static Vector2 PuntoDentro(float separacion, float radio, List<Vector2> usados, bool cercaDeBorde)
    {
        for (int intento = 0; intento < 200; intento++)
        {
            Vector2 p = new Vector2(Rango(0f, mapa.worldW), Rango(0f, mapa.worldH));
            if (cercaDeBorde)
            {
                float alBorde = Mathf.Min(Mathf.Min(p.x, mapa.worldW - p.x), Mathf.Min(p.y, mapa.worldH - p.y));
                if (alBorde > 3.5f * M)
                {
                    continue;
                }
            }
            if (LibreDentro(p, radio) && !MasCercaQue(p, usados, separacion))
            {
                usados.Add(p);
                return p;
            }
        }
        return new Vector2(-1f, -1f);
    }

    /// <summary>¿Cabe una planta o prop aquí dentro del mapa sin estorbar? (no caminos, plaza, maizales, almacenes ni obstáculos)</summary>
    static bool LibreDentro(Vector2 p, float radio)
    {
        if (p.x < radio + 0.3f * M || p.y < radio + 0.3f * M || p.x > mapa.worldW - radio - 0.3f * M || p.y > mapa.worldH - radio - 0.3f * M)
        {
            return false;
        }
        if (!Libre(p, radio))
        {
            return false;                       // agua, casas, muros, caminos o almacenes
        }
        foreach (RectDatos m in mapa.maize)
        {
            if (DistanciaARect(p, m) < radio + 0.3f * M)
            {
                return false;
            }
        }
        float plaza = Vector2.Distance(p, new Vector2(mapa.plaza.x, mapa.plaza.y));
        return plaza > mapa.plaza.r + radio + 0.5f * M;
    }

    static void PrepararMaterialVegetacion()
    {
        // URP Lit con alfa recortado (las hojas son tarjetas con transparencia) y de dos caras
        const string ruta = "Assets/Art/Materials/M_VegetacionSeca.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, ruta);
        }
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Textures/Vegetacion/T_VegetacionSeca_BaseColor.png"));
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_AlphaClip", 1f);
        material.SetFloat("_Cutoff", 0.5f);
        material.EnableKeyword("_ALPHATEST_ON");
        material.SetFloat("_Cull", 0f);                     // se ven las dos caras
        material.SetFloat("_Smoothness", 0.1f);
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
        material.enableInstancing = true;                   // muchas copias: Unity las dibuja juntas
        EditorUtility.SetDirty(material);

        // cada FBX de vegetación usa este material en lugar del suyo
        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { Modelos + "Vegetacion" }))
        {
            string rutaModelo = AssetDatabase.GUIDToAssetPath(guid);
            ModelImporter importador = (ModelImporter)AssetImporter.GetAtPath(rutaModelo);
            bool cambio = false;
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(rutaModelo))
            {
                Material original = o as Material;
                if (original != null)
                {
                    importador.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), original.name), material);
                    cambio = true;
                }
            }
            if (cambio)
            {
                importador.SaveAndReimport();
            }
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
