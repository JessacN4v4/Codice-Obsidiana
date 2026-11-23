using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Menú "Codice > Colocar jugador y cámara".
/// Pone al jugador (cápsula de 1.8 m) en el punto InicioJugador del graybox, con su
/// Rigidbody y el script MovimientoJugador, y hace que la cámara principal lo siga.
/// Si ya había un jugador en la escena, lo reemplaza.
/// </summary>
public static class ColocarJugador
{
    const string CarpetaMateriales = "Assets/Graybox/Materiales";
    const string RutaEscena = "Assets/Scenes/Pueblo.unity";

    [MenuItem("Codice/Colocar jugador y cámara")]
    public static void Colocar()
    {
        ColocarEnEscenaActual();
    }

    /// <summary>
    /// Para la línea de comandos (sin abrir el editor):
    /// Unity.exe -batchmode -projectPath client -executeMethod ColocarJugador.ColocarEnEscenaPueblo -quit
    /// Abre la escena Pueblo, coloca al jugador y la cámara, y la guarda.
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
        AssetDatabase.SaveAssets();
        Debug.Log("Escena guardada con el jugador: " + RutaEscena);
    }

    static bool ColocarEnEscenaActual()
    {
        GameObject inicio = GameObject.Find("InicioJugador");
        if (inicio == null)
        {
            Debug.LogError("No encontré el objeto InicioJugador.");
            EditorUtility.DisplayDialog("Falta el graybox",
                "No encontré el objeto InicioJugador. Abre la escena Pueblo o genera el graybox primero.", "OK");
            return false;
        }

        GameObject anterior = GameObject.Find("Jugador");
        if (anterior != null)
        {
            Undo.DestroyObjectImmediate(anterior);
        }

        // Cuerpo: cápsula de 1.8 m de alto y 0.5 m de radio (10 px del mapa)
        GameObject jugador = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        jugador.name = "Jugador";
        jugador.tag = "Player";
        jugador.transform.position = inicio.transform.position + new Vector3(0f, 0.9f, 0f);
        jugador.transform.rotation = Quaternion.Euler(0f, -90f, 0f);    // mirando al oeste, hacia el pueblo
        jugador.transform.localScale = new Vector3(1f, 0.9f, 1f);
        jugador.GetComponent<Renderer>().sharedMaterial = ObtenerMaterial("Jugador", new Color(0.18f, 0.44f, 0.42f));

        // Sin fricción, para que se deslice por las paredes en lugar de quedarse pegado
        jugador.GetComponent<CapsuleCollider>().sharedMaterial = ObtenerMaterialFisico();

        // "Nariz": una cajita al frente para ver hacia dónde mira
        GameObject nariz = GameObject.CreatePrimitive(PrimitiveType.Cube);
        nariz.name = "Frente";
        nariz.transform.SetParent(jugador.transform, false);
        nariz.transform.localPosition = new Vector3(0f, 0.35f, 0.45f);
        nariz.transform.localScale = new Vector3(0.3f, 0.15f, 0.3f);
        Object.DestroyImmediate(nariz.GetComponent<BoxCollider>());
        nariz.GetComponent<Renderer>().sharedMaterial = ObtenerMaterial("JugadorFrente", new Color(0.95f, 0.85f, 0.6f));

        Rigidbody rb = jugador.AddComponent<Rigidbody>();
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;              // movimiento suave en pantalla
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;      // que no atraviese paredes al correr
        jugador.AddComponent<MovimientoJugador>();

        Undo.RegisterCreatedObjectUndo(jugador, "Colocar jugador");

        // Cámara principal siguiendo al jugador
        Camera camara = Camera.main;
        if (camara != null)
        {
            CamaraCenital seguir = camara.GetComponent<CamaraCenital>();
            if (seguir == null)
            {
                seguir = Undo.AddComponent<CamaraCenital>(camara.gameObject);
            }
            seguir.objetivo = jugador.transform;
            camara.transform.position = jugador.transform.position + new Vector3(0f, seguir.altura, -seguir.distanciaAtras);
            camara.transform.LookAt(jugador.transform.position);
            EditorUtility.SetDirty(seguir);
        }

        EditorSceneManager.MarkSceneDirty(jugador.scene);
        // el jugador es nuevo: reconectar la red, el HUD, las piedras y las cámaras con él
        ReconectarJugador.ReconectarEnEscenaActual();
        Selection.activeGameObject = jugador;
        Debug.Log("Jugador colocado en el inicio. Guarda la escena (Ctrl + S) y dale Play.");
        return true;
    }

    static Material ObtenerMaterial(string nombre, Color color)
    {
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

    static PhysicsMaterial ObtenerMaterialFisico()
    {
        string ruta = CarpetaMateriales + "/SinFriccion.physicMaterial";
        PhysicsMaterial material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(ruta);
        if (material == null)
        {
            material = new PhysicsMaterial("SinFriccion");
            material.dynamicFriction = 0f;
            material.staticFriction = 0f;
            material.frictionCombine = PhysicsMaterialCombine.Minimum;
            AssetDatabase.CreateAsset(material, ruta);
        }
        return material;
    }
}
