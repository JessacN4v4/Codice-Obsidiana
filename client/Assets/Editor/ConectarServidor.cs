using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Menú "Codice > Conectar con el servidor".
/// Crea el objeto "Simulacion" con ClienteSimulacion (habla con el servidor) y
/// VistaAgentes (dibuja guardias y aldeanos), ya conectados al jugador y con sus materiales.
/// Si ya existía, lo reemplaza.
/// </summary>
public static class ConectarServidor
{
    const string CarpetaMateriales = "Assets/Graybox/Materiales";
    const string RutaEscena = "Assets/Scenes/Pueblo.unity";

    [MenuItem("Codice/Conectar con el servidor")]
    public static void Conectar()
    {
        ConectarEnEscenaActual();
    }

    /// <summary>
    /// Para la línea de comandos (sin abrir el editor):
    /// Unity.exe -batchmode -projectPath client -executeMethod ConectarServidor.ConectarEnEscenaPueblo -quit
    /// </summary>
    public static void ConectarEnEscenaPueblo()
    {
        EditorSceneManager.OpenScene(RutaEscena, OpenSceneMode.Single);
        if (!ConectarEnEscenaActual())
        {
            EditorApplication.Exit(1);
            return;
        }
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("Escena guardada con la simulación: " + RutaEscena);
    }

    static bool ConectarEnEscenaActual()
    {
        MovimientoJugador jugador = Object.FindAnyObjectByType<MovimientoJugador>();
        if (jugador == null)
        {
            Debug.LogError("No encontré al jugador.");
            EditorUtility.DisplayDialog("Falta el jugador",
                "Primero usa el menú Codice > Colocar jugador y cámara.", "OK");
            return false;
        }

        GameObject anterior = GameObject.Find("Simulacion");
        if (anterior != null)
        {
            Undo.DestroyObjectImmediate(anterior);
        }

        GameObject simulacion = new GameObject("Simulacion");
        ClienteSimulacion cliente = simulacion.AddComponent<ClienteSimulacion>();
        cliente.jugador = jugador;

        VistaAgentes vista = simulacion.AddComponent<VistaAgentes>();
        vista.cliente = cliente;
        vista.materialGuardia = MaterialOpaco("Guardia", new Color(0.72f, 0.14f, 0.12f));
        vista.materialGuardiaCanoa = MaterialOpaco("GuardiaCanoa", new Color(0.45f, 0.08f, 0.25f));
        vista.materialAldeano = MaterialOpaco("Aldeano", new Color(0.89f, 0.84f, 0.72f));
        vista.materialCargador = MaterialOpaco("Cargador", new Color(0.80f, 0.71f, 0.54f));
        vista.materialCono = MaterialTransparente("ConoVision", new Color(1f, 0.95f, 0.7f, 0.18f));

        Undo.RegisterCreatedObjectUndo(simulacion, "Conectar con el servidor");

        // Acciones del jugador (quemar trojes), conectadas al cliente
        InteraccionJugador interaccion = jugador.GetComponent<InteraccionJugador>();
        if (interaccion == null)
        {
            interaccion = Undo.AddComponent<InteraccionJugador>(jugador.gameObject);
        }
        interaccion.cliente = cliente;
        interaccion.movimiento = jugador;
        interaccion.materialPiedra = MaterialOpaco("Piedra", new Color(0.45f, 0.43f, 0.40f));
        interaccion.materialRuido = MaterialTransparente("OndaRuido", new Color(0.95f, 0.92f, 0.8f, 0.35f));
        EditorUtility.SetDirty(interaccion);

        EditorSceneManager.MarkSceneDirty(simulacion.scene);
        Selection.activeGameObject = simulacion;
        Debug.Log("Simulación lista. Levanta el servidor, guarda la escena y dale Play.");
        return true;
    }

    static Material MaterialOpaco(string nombre, Color color)
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

    static Material MaterialTransparente(string nombre, Color color)
    {
        // Unlit (sin luz ni sombras) y transparente, visible por los dos lados
        string ruta = CarpetaMateriales + "/" + nombre + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            AssetDatabase.CreateAsset(material, ruta);
        }
        material.SetFloat("_Surface", 1f);                         // 1 = transparente
        material.SetFloat("_Blend", 0f);                           // mezcla por alfa
        material.SetFloat("_Cull", 0f);                            // sin ocultar caras traseras
        material.SetFloat("_ZWrite", 0f);
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;
        material.SetColor("_BaseColor", color);
        EditorUtility.SetDirty(material);
        return material;
    }
}
