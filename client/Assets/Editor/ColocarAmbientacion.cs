using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Menú "Codice > Colocar modelos de ambientación".
/// Pone cada modelo de Assets/Art/Models en el lugar de su caja del graybox y apaga la parte
/// visible de la caja (su Renderer), pero deja su collider: las colisiones siguen siendo
/// exactamente las del servidor.
/// - Almacenes completos (muros, puertas, brechas y casas interiores) en el centro de su piso.
/// - Trojes, casas (el tipo T1..T8 se elige por el tamaño de la caja), puestos, maizales y canoa.
/// - La yácata, el lago, la plaza y el suelo se quedan como están (todavía no hay modelo).
/// Menú "Codice > Quitar modelos de ambientación": regresa al graybox para comparar.
/// </summary>
public static class ColocarAmbientacion
{
    const string Modelos = "Assets/Art/Models/";
    const string RutaEscena = "Assets/Scenes/Pueblo.unity";

    // Huellas de los 8 tipos de casa (ancho X, fondo Z) en metros
    static readonly string[] TiposCasa = { "T1", "T2", "T3", "T4", "T5", "T6", "T7", "T8" };
    static readonly Vector2[] HuellasCasa =
    {
        new Vector2(4.0f, 2.75f), new Vector2(4.0f, 2.5f), new Vector2(3.0f, 2.25f), new Vector2(3.5f, 2.75f),
        new Vector2(4.0f, 3.0f), new Vector2(3.5f, 3.0f), new Vector2(3.75f, 2.75f), new Vector2(3.0f, 2.5f),
    };

    [MenuItem("Codice/Colocar modelos de ambientación")]
    public static void Colocar()
    {
        ColocarEnEscenaActual();
    }

    [MenuItem("Codice/Quitar modelos de ambientación")]
    public static void Quitar()
    {
        Transform graybox = BuscarGraybox();
        if (graybox == null)
        {
            return;
        }
        QuitarAmbientacion(graybox);
        EditorSceneManager.MarkSceneDirty(graybox.gameObject.scene);
        Debug.Log("Modelos quitados: se ve el graybox otra vez.");
    }

    /// <summary>
    /// Para la línea de comandos (sin abrir el editor):
    /// Unity.exe -batchmode -projectPath client -executeMethod ColocarAmbientacion.ColocarEnEscenaPueblo -quit
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
        Debug.Log("Escena guardada con los modelos: " + RutaEscena);
    }

    static bool ColocarEnEscenaActual()
    {
        Transform graybox = BuscarGraybox();
        if (graybox == null)
        {
            return false;
        }
        QuitarAmbientacion(graybox);             // así se puede usar el menú varias veces

        GameObject raiz = new GameObject("Ambientacion");
        Undo.RegisterCreatedObjectUndo(raiz, "Colocar ambientación");
        int colocados = 0;

        // ---------- almacenes completos ----------
        Transform almacenes = graybox.Find("Almacenes");
        Transform grupoAlmacenes = Grupo("Almacenes", raiz.transform);
        foreach (Transform almacen in almacenes)
        {
            string id = almacen.name.Substring("Almacen_".Length, 1);       // "Almacen_A (...)" -> "A"
            Transform piso = almacen.Find("Piso");
            if (Poner(Modelos + "Almacenes/SM_Almacen_" + id + ".fbx", piso.position, grupoAlmacenes))
            {
                colocados++;
            }
            Apagar(piso);
            Apagar(almacen.Find("Puerta"));
        }
        // los muros ya vienen dentro de los almacenes completos
        foreach (Transform muro in graybox.Find("Empalizadas"))
        {
            Apagar(muro);
        }

        // ---------- trojes ----------
        Transform grupoTrojes = Grupo("Trojes", raiz.transform);
        foreach (Transform troje in graybox.Find("Trojes"))
        {
            string id = troje.name.Substring("Troje_".Length);              // "Troje_A" -> "A"
            if (CrearTroje(id, troje.position, grupoTrojes))
            {
                colocados++;
            }
            Apagar(troje);
        }

        // ---------- casas ----------
        Transform grupoCasas = Grupo("Casas", raiz.transform);
        foreach (Transform casa in graybox.Find("Casas"))
        {
            Apagar(casa);
            if (DentroDeUnAlmacen(casa.position, almacenes))
            {
                continue;                        // ya viene dentro del modelo del almacén
            }
            string tipo = TipoDeCasa(casa.localScale.x, casa.localScale.z);
            if (Poner(Modelos + "Casas/SM_Casa_" + tipo + ".fbx", casa.position, grupoCasas))
            {
                colocados++;
            }
        }

        // ---------- puestos del mercado (en orden: 1, 2, 3) ----------
        Transform grupoMercado = Grupo("Mercado", raiz.transform);
        int numeroPuesto = 1;
        foreach (Transform puesto in graybox.Find("Puestos"))
        {
            if (Poner(Modelos + "Mercado/SM_Puesto_" + numeroPuesto + ".fbx", puesto.position, grupoMercado))
            {
                colocados++;
            }
            Apagar(puesto);
            numeroPuesto++;
        }

        // ---------- maizales (siguen escondiendo: su collider trigger queda activo) ----------
        Transform grupoMilpa = Grupo("Milpa", raiz.transform);
        foreach (Transform maizal in graybox.Find("Maizales"))
        {
            if (Poner(Modelos + "Milpa/SM_" + maizal.name + ".fbx", maizal.position, grupoMilpa))
            {
                colocados++;
            }
            Apagar(maizal);
        }

        // ---------- canoa ----------
        Transform canoa = graybox.Find("Canoa");
        if (Poner(Modelos + "Lago/SM_Canoa.fbx", canoa.position, raiz.transform))
        {
            colocados++;
        }
        Apagar(canoa);

        // ---------- yácata ----------
        // su pivote es el centro de la caja que encierra los escalones del graybox
        Transform yacata = graybox.Find("Yacata");
        if (yacata != null)
        {
            AplicarTexturaYacata();
            float norte = float.MinValue;
            float sur = float.MaxValue;
            foreach (Transform escalon in yacata)
            {
                norte = Mathf.Max(norte, escalon.position.z + escalon.localScale.z / 2f);
                sur = Mathf.Min(sur, escalon.position.z - escalon.localScale.z / 2f);
                Apagar(escalon);
            }
            Vector3 pivote = new Vector3(yacata.GetChild(0).position.x, 0f, (norte + sur) / 2f);
            if (Poner(Modelos + "Yacata/SM_Yacata.fbx", pivote, raiz.transform))
            {
                colocados++;
            }
        }

        EditorSceneManager.MarkSceneDirty(raiz.scene);
        Selection.activeGameObject = raiz;
        Debug.Log("Ambientación colocada: " + colocados + " modelos. Guarda la escena (Ctrl + S).");
        return true;
    }

    // ---------- ayudantes ----------
    static Transform BuscarGraybox()
    {
        GameObject graybox = GameObject.Find("Graybox");
        if (graybox == null)
        {
            Debug.LogError("No encontré el Graybox en la escena.");
            EditorUtility.DisplayDialog("Falta el graybox", "Abre la escena Pueblo (o genera el graybox) primero.", "OK");
            return null;
        }
        return graybox.transform;
    }

    static void QuitarAmbientacion(Transform graybox)
    {
        GameObject anterior = GameObject.Find("Ambientacion");
        if (anterior != null)
        {
            Undo.DestroyObjectImmediate(anterior);
        }
        // volver a encender solo las cajas que maneja este menú (el suelo y la plaza los maneja el terreno)
        string[] grupos = { "Almacenes", "Empalizadas", "Trojes", "Casas", "Puestos", "Maizales", "Canoa", "Yacata" };
        foreach (string nombre in grupos)
        {
            Transform grupo = graybox.Find(nombre);
            if (grupo == null)
            {
                continue;
            }
            foreach (Renderer r in grupo.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.enabled)
                {
                    Undo.RecordObject(r, "Encender graybox");
                    r.enabled = true;
                }
            }
        }
    }

    static bool Poner(string ruta, Vector3 posicionCaja, Transform padre)
    {
        return PonerModelo(ruta, posicionCaja, padre) != null;
    }

    static GameObject PonerModelo(string ruta, Vector3 posicionCaja, Transform padre)
    {
        GameObject modelo = AssetDatabase.LoadAssetAtPath<GameObject>(ruta);
        if (modelo == null)
        {
            Debug.LogWarning("No encontré el modelo " + ruta);
            return null;
        }
        GameObject copia = (GameObject)PrefabUtility.InstantiatePrefab(modelo, padre);
        // misma posición en el suelo que la caja, sin rotar (los modelos ya vienen orientados)
        copia.transform.position = new Vector3(posicionCaja.x, 0f, posicionCaja.z);
        copia.transform.rotation = Quaternion.identity;
        return copia;
    }

    // ---------- textura de piedra de la yácata ----------
    static void AplicarTexturaYacata()
    {
        // Crea un material URP Lit con las texturas T_MuroXanamu y le dice al importador del FBX
        // que lo use en lugar de su material "M_MuroXanamu" (eso se llama "remap" de material).
        const string carpeta = "Assets/Art/Materials";
        const string texturas = "Assets/Art/Textures/Modelos/T_MuroXanamu_";
        if (!AssetDatabase.IsValidFolder(carpeta))
        {
            AssetDatabase.CreateFolder("Assets/Art", "Materials");
        }
        string ruta = carpeta + "/M_MuroXanamu.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, ruta);
        }
        Texture2D color = AssetDatabase.LoadAssetAtPath<Texture2D>(texturas + "BaseColor.png");
        Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(texturas + "Normal.png");
        Texture2D mascara = AssetDatabase.LoadAssetAtPath<Texture2D>(texturas + "Mask.png");
        material.SetTexture("_BaseMap", color);
        material.SetColor("_BaseColor", Color.white);
        material.SetTexture("_BumpMap", normal);
        material.EnableKeyword("_NORMALMAP");
        // máscara: R = metal y A = suavidad (MetallicGlossMap), G = oclusión (OcclusionMap)
        material.SetTexture("_MetallicGlossMap", mascara);
        material.EnableKeyword("_METALLICSPECGLOSSMAP");
        material.SetTexture("_OcclusionMap", mascara);
        material.EnableKeyword("_OCCLUSIONMAP");
        // las UV del modelo están en metros: que la textura cubra 2 m
        material.SetTextureScale("_BaseMap", new Vector2(0.5f, 0.5f));
        EditorUtility.SetDirty(material);

        string rutaModelo = Modelos + "Yacata/SM_Yacata.fbx";
        ModelImporter importador = (ModelImporter)AssetImporter.GetAtPath(rutaModelo);
        if (importador == null)
        {
            return;
        }
        importador.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "M_MuroXanamu"), material);
        importador.SaveAndReimport();
    }

    // ---------- trojes: modelo intacto + quemado (oculto) + fuego y humo ----------
    static bool CrearTroje(string id, Vector3 posicion, Transform padre)
    {
        // "TrojeModelo_A" y no "Troje_A": ese nombre lo usa la caja del graybox
        GameObject raizTroje = new GameObject("TrojeModelo_" + id);
        raizTroje.transform.SetParent(padre, false);
        raizTroje.transform.position = new Vector3(posicion.x, 0f, posicion.z);

        GameObject intacto = PonerModelo(Modelos + "Trojes/SM_Troje_" + id + "_Intacto.fbx", posicion, raizTroje.transform);
        GameObject quemado = PonerModelo(Modelos + "Trojes/SM_Troje_" + id + "_Quemado.fbx", posicion, raizTroje.transform);
        if (intacto == null || quemado == null)
        {
            return false;
        }
        quemado.SetActive(false);

        TrojeModelo modelo = raizTroje.AddComponent<TrojeModelo>();
        modelo.id = id;
        modelo.intacto = intacto;
        modelo.quemado = quemado;
        modelo.fuego = CrearFuego(raizTroje.transform);
        modelo.humo = CrearHumo(raizTroje.transform);
        return true;
    }

    static ParticleSystem CrearFuego(Transform padre)
    {
        ParticleSystem ps = NuevoSistemaDeParticulas("Fuego", padre, 1.2f);
        ParticleSystem.MainModule principal = ps.main;
        principal.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
        principal.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3f);
        principal.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
        principal.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.55f, 0.1f), new Color(1f, 0.85f, 0.3f));
        principal.maxParticles = 300;

        ParticleSystem.EmissionModule emision = ps.emission;
        emision.rateOverTime = 70f;

        ParticleSystem.ShapeModule forma = ps.shape;
        forma.shapeType = ParticleSystemShapeType.Cone;
        forma.angle = 12f;
        forma.radius = 1.0f;                       // del ancho del troje

        // amarillo -> naranja -> gris, y se desvanece
        ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
        color.enabled = true;
        color.color = Degradado(new Color(1f, 0.8f, 0.3f), new Color(0.9f, 0.25f, 0.05f), new Color(0.2f, 0.2f, 0.2f), 0.9f, 0.6f, 0f);

        ParticleSystem.SizeOverLifetimeModule tamano = ps.sizeOverLifetime;
        tamano.enabled = true;
        tamano.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));   // se encoge
        return ps;
    }

    static ParticleSystem CrearHumo(Transform padre)
    {
        ParticleSystem ps = NuevoSistemaDeParticulas("Humo", padre, 3f);
        ParticleSystem.MainModule principal = ps.main;
        principal.startLifetime = new ParticleSystem.MinMaxCurve(3f, 5f);
        principal.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 1.5f);
        principal.startSize = new ParticleSystem.MinMaxCurve(1.2f, 2.5f);
        principal.startColor = new ParticleSystem.MinMaxGradient(new Color(0.25f, 0.24f, 0.22f), new Color(0.45f, 0.43f, 0.4f));
        principal.maxParticles = 150;

        ParticleSystem.EmissionModule emision = ps.emission;
        emision.rateOverTime = 14f;

        ParticleSystem.ShapeModule forma = ps.shape;
        forma.shapeType = ParticleSystemShapeType.Cone;
        forma.angle = 15f;
        forma.radius = 0.8f;

        // el viento lo empuja un poco hacia el este
        ParticleSystem.VelocityOverLifetimeModule viento = ps.velocityOverLifetime;
        viento.enabled = true;
        viento.space = ParticleSystemSimulationSpace.World;
        viento.x = new ParticleSystem.MinMaxCurve(0.6f);
        viento.y = new ParticleSystem.MinMaxCurve(0f);
        viento.z = new ParticleSystem.MinMaxCurve(0f);

        ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
        color.enabled = true;
        color.color = Degradado(Color.white, Color.white, Color.white, 0f, 0.5f, 0f);   // aparece y se desvanece

        ParticleSystem.SizeOverLifetimeModule tamano = ps.sizeOverLifetime;
        tamano.enabled = true;
        tamano.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.6f));  // crece
        return ps;
    }

    static ParticleSystem NuevoSistemaDeParticulas(string nombre, Transform padre, float altura)
    {
        GameObject objeto = new GameObject(nombre);
        objeto.transform.SetParent(padre, false);
        objeto.transform.localPosition = new Vector3(0f, altura, 0f);
        objeto.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);    // el cono de emisión apunta hacia arriba
        ParticleSystem ps = objeto.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule principal = ps.main;
        principal.playOnAwake = false;             // empieza apagado; TrojeModelo lo enciende
        principal.loop = true;
        principal.simulationSpace = ParticleSystemSimulationSpace.World;

        Material material = AssetDatabase.LoadAssetAtPath<Material>(
            "Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat");
        if (material == null)
        {
            Debug.LogWarning("No encontré el material de partículas de URP.");
        }
        objeto.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
        return ps;
    }

    static Gradient Degradado(Color inicio, Color medio, Color fin, float alfaInicio, float alfaMedio, float alfaFin)
    {
        Gradient g = new Gradient();
        g.SetKeys(
            new GradientColorKey[] { new GradientColorKey(inicio, 0f), new GradientColorKey(medio, 0.5f), new GradientColorKey(fin, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(alfaInicio, 0f), new GradientAlphaKey(alfaMedio, 0.4f), new GradientAlphaKey(alfaFin, 1f) });
        return g;
    }

    static void Apagar(Transform caja)
    {
        // se apaga solo lo que se ve; el collider sigue funcionando
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

    static bool DentroDeUnAlmacen(Vector3 punto, Transform almacenes)
    {
        foreach (Transform almacen in almacenes)
        {
            Transform piso = almacen.Find("Piso");
            Vector3 c = piso.position;
            Vector3 s = piso.localScale;
            if (Mathf.Abs(punto.x - c.x) < s.x / 2f && Mathf.Abs(punto.z - c.z) < s.z / 2f)
            {
                return true;
            }
        }
        return false;
    }

    static string TipoDeCasa(float ancho, float fondo)
    {
        // el tipo cuya huella se parece más al tamaño de la caja
        int mejor = 0;
        float menorDiferencia = float.MaxValue;
        for (int i = 0; i < HuellasCasa.Length; i++)
        {
            float diferencia = Mathf.Abs(HuellasCasa[i].x - ancho) + Mathf.Abs(HuellasCasa[i].y - fondo);
            if (diferencia < menorDiferencia)
            {
                menorDiferencia = diferencia;
                mejor = i;
            }
        }
        return TiposCasa[mejor];
    }

    static Transform Grupo(string nombre, Transform padre)
    {
        GameObject grupo = new GameObject(nombre);
        grupo.transform.SetParent(padre, false);
        return grupo.transform;
    }
}
