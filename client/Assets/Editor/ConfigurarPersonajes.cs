using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Menú "Codice > Configurar personajes".
/// 1. Crea el material URP de cada personaje con sus texturas y se lo pone a su FBX.
/// 2. Crea el Animator Controller compartido "AC_Personaje": un "blend tree" que mezcla
///    parado -> caminar -> correr según el parámetro "Velocidad" (m/s). Como todos los
///    personajes son Humanoid, las mismas animaciones sirven para todos.
///    Si hay una animación Idle en Assets/Art/Characters/Animaciones/, la usa para "parado";
///    si no, usa la primera pose de caminar, quieta.
/// 3. Le dice a VistaAgentes qué modelo usar para guardias, guardia de la canoa, cargadores y aldeanos.
/// 4. Pone a Tariácuri en lugar de la cápsula del jugador (la cápsula sigue siendo la colisión).
/// </summary>
public static class ConfigurarPersonajes
{
    const string Carpeta = "Assets/Art/Characters/";
    const string RutaControlador = "Assets/Art/Characters/AC_Personaje.controller";
    const string RutaEscena = "Assets/Scenes/Pueblo.unity";
    static readonly string[] Personajes = { "Tariacuri", "Cargador", "Guardia", "GuardiaCanoa", "Artesano", "Mujer", "Campesino" };

    // umbrales del blend tree (m/s): parado, caminar, correr
    const float UmbralCaminar = 2.5f;
    const float UmbralCorrer = 7f;

    [MenuItem("Codice/Configurar personajes")]
    public static void Configurar()
    {
        ConfigurarEnEscenaActual();
    }

    /// <summary>
    /// Para la línea de comandos: abre la escena Pueblo, configura los personajes y la guarda.
    /// Unity.exe -batchmode -projectPath client -executeMethod ConfigurarPersonajes.ConfigurarEnEscenaPueblo -quit
    /// </summary>
    public static void ConfigurarEnEscenaPueblo()
    {
        EditorSceneManager.OpenScene(RutaEscena, OpenSceneMode.Single);
        if (!ConfigurarEnEscenaActual())
        {
            EditorApplication.Exit(1);
            return;
        }
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("Escena guardada con los personajes: " + RutaEscena);
    }

    static bool ConfigurarEnEscenaActual()
    {
        // ---------- 1. materiales ----------
        foreach (string nombre in Personajes)
        {
            PonerMaterial(nombre);
        }

        // ---------- 2. animator compartido ----------
        AnimatorController controlador = CrearControlador();
        if (controlador == null)
        {
            return false;
        }

        // ---------- 3. modelos para guardias y aldeanos ----------
        VistaAgentes vista = Object.FindAnyObjectByType<VistaAgentes>();
        if (vista == null)
        {
            Debug.LogError("No encontré VistaAgentes: usa primero Codice > Conectar con el servidor.");
            return false;
        }
        Undo.RecordObject(vista, "Configurar personajes");
        vista.modeloGuardia = Modelo("Guardia");
        vista.modeloGuardiaCanoa = Modelo("GuardiaCanoa");
        vista.modeloCargador = Modelo("Cargador");
        vista.modelosAldeanos = new GameObject[] { Modelo("Artesano"), Modelo("Mujer"), Modelo("Campesino") };
        vista.controlador = controlador;
        EditorUtility.SetDirty(vista);

        // ---------- 4. Tariácuri en lugar de la cápsula ----------
        MovimientoJugador jugador = Object.FindAnyObjectByType<MovimientoJugador>();
        if (jugador == null)
        {
            Debug.LogError("No encontré al jugador: usa primero Codice > Colocar jugador y cámara.");
            return false;
        }
        PonerModeloAlJugador(jugador.gameObject, controlador);

        EditorSceneManager.MarkSceneDirty(jugador.gameObject.scene);
        Debug.Log("Personajes configurados. Guarda la escena (Ctrl + S) y dale Play.");
        return true;
    }

    // ---------- materiales ----------
    static void PonerMaterial(string nombre)
    {
        string carpeta = Carpeta + nombre + "/";
        string rutaModelo = carpeta + "SK_" + nombre + ".fbx";
        ModelImporter importador = (ModelImporter)AssetImporter.GetAtPath(rutaModelo);
        if (importador == null)
        {
            Debug.LogWarning("No encontré " + rutaModelo);
            return;
        }

        string rutaMaterial = carpeta + "M_" + nombre + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(rutaMaterial);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, rutaMaterial);
        }
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(carpeta + "T_" + nombre + "_BaseColor.png"));
        material.SetColor("_BaseColor", Color.white);
        material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(carpeta + "T_" + nombre + "_Normal.png"));
        material.EnableKeyword("_NORMALMAP");
        // R = metal, A = suavidad (por eso se juntaron en una sola textura)
        material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(carpeta + "T_" + nombre + "_MetallicSmoothness.png"));
        material.EnableKeyword("_METALLICSPECGLOSSMAP");
        material.SetFloat("_Smoothness", 1f);
        EditorUtility.SetDirty(material);

        // el FBX trae sus propios materiales (sin texturas): se cambian todos por este
        bool cambio = false;
        foreach (Material original in AssetDatabase.LoadAllAssetsAtPath(rutaModelo).OfType<Material>())
        {
            importador.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), original.name), material);
            cambio = true;
        }
        if (cambio)
        {
            importador.SaveAndReimport();
        }
    }

    // ---------- animator ----------
    static AnimatorController CrearControlador()
    {
        AnimationClip caminar = Clip(Carpeta + "Campesino/SK_Campesino.fbx", "walk");
        AnimationClip correr = Clip(Carpeta + "Campesino/SK_Campesino.fbx", "run");
        if (caminar == null || correr == null)
        {
            Debug.LogError("No encontré las animaciones Walking y Running del campesino.");
            return null;
        }
        AnimationClip parado = BuscarIdle();

        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(RutaControlador) != null)
        {
            AssetDatabase.DeleteAsset(RutaControlador);      // se vuelve a armar desde cero
        }
        AnimatorController controlador = AnimatorController.CreateAnimatorControllerAtPath(RutaControlador);
        controlador.AddParameter("Velocidad", AnimatorControllerParameterType.Float);

        BlendTree arbol;
        controlador.CreateBlendTreeInController("Movimiento", out arbol, 0);
        arbol.blendType = BlendTreeType.Simple1D;
        arbol.blendParameter = "Velocidad";
        arbol.useAutomaticThresholds = false;
        arbol.AddChild(parado != null ? parado : caminar, 0f);
        arbol.AddChild(caminar, UmbralCaminar);
        arbol.AddChild(correr, UmbralCorrer);
        if (parado == null)
        {
            // sin Idle: la primera pose de caminar, quieta (timeScale 0)
            ChildMotion[] hijos = arbol.children;
            hijos[0].timeScale = 0f;
            arbol.children = hijos;
        }

        // CreateBlendTreeInController agrega un parámetro "Blend" que no usamos
        for (int i = controlador.parameters.Length - 1; i >= 0; i--)
        {
            if (controlador.parameters[i].name == "Blend")
            {
                controlador.RemoveParameter(i);
            }
        }
        EditorUtility.SetDirty(controlador);
        Debug.Log("Animator creado. Parado = " + (parado != null ? parado.name : "primera pose de caminar (falta un Idle)"));
        return controlador;
    }

    static AnimationClip BuscarIdle()
    {
        if (!AssetDatabase.IsValidFolder(Carpeta + "Animaciones"))
        {
            return null;
        }
        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { Carpeta + "Animaciones" }))
        {
            AnimationClip clip = Clip(AssetDatabase.GUIDToAssetPath(guid), "idle");
            if (clip != null)
            {
                return clip;
            }
        }
        return null;
    }

    static AnimationClip Clip(string rutaModelo, string contiene)
    {
        foreach (AnimationClip clip in AssetDatabase.LoadAllAssetsAtPath(rutaModelo).OfType<AnimationClip>())
        {
            if (!clip.name.StartsWith("__preview__") && clip.name.ToLower().Contains(contiene))
            {
                return clip;
            }
        }
        return null;
    }

    // ---------- jugador ----------
    static void PonerModeloAlJugador(GameObject jugador, AnimatorController controlador)
    {
        Transform anterior = jugador.transform.Find("Modelo");
        if (anterior != null)
        {
            Undo.DestroyObjectImmediate(anterior.gameObject);
        }
        // apagar lo que se ve de la cápsula y su "frente" (la colisión sigue)
        Renderer capsula = jugador.GetComponent<Renderer>();
        if (capsula != null)
        {
            Undo.RecordObject(capsula, "Ocultar cápsula");
            capsula.enabled = false;
        }
        Transform frente = jugador.transform.Find("Frente");
        if (frente != null && frente.GetComponent<Renderer>() != null)
        {
            Undo.RecordObject(frente.GetComponent<Renderer>(), "Ocultar frente");
            frente.GetComponent<Renderer>().enabled = false;
        }

        GameObject modelo = (GameObject)PrefabUtility.InstantiatePrefab(Modelo("Tariacuri"), jugador.transform);
        modelo.name = "Modelo";
        // la cápsula está escalada (1, 0.9, 1) y su centro a 0.9 m del suelo:
        // se compensa para que el modelo quede a escala 1 y con los pies en el suelo
        Vector3 escala = jugador.transform.localScale;
        modelo.transform.localScale = new Vector3(1f / escala.x, 1f / escala.y, 1f / escala.z);
        modelo.transform.localPosition = new Vector3(0f, -1f, 0f);
        modelo.transform.localRotation = Quaternion.identity;
        Animator animator = modelo.GetComponent<Animator>();
        animator.runtimeAnimatorController = controlador;
        animator.applyRootMotion = false;
        AnimacionPorVelocidad animacion = modelo.AddComponent<AnimacionPorVelocidad>();
        animacion.cuerpo = jugador.transform;
        Undo.RegisterCreatedObjectUndo(modelo, "Modelo del jugador");
    }

    static GameObject Modelo(string nombre)
    {
        return AssetDatabase.LoadAssetAtPath<GameObject>(Carpeta + nombre + "/SK_" + nombre + ".fbx");
    }
}
