using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Menú "Codice > Configurar personajes".
/// 1. Crea el material URP de cada personaje con sus texturas y se lo pone a su FBX.
/// 2. Arma el Animator Controller compartido "AC_Personaje": un "blend tree" 2D que mezcla
///    parado, caminar, correr, hacia atrás y de lado según VelocidadX y VelocidadZ (m/s, en ejes
///    del personaje). Como todos los personajes son Humanoid, las mismas animaciones sirven para todos.
///    Animaciones extra (opcionales) en Assets/Art/Characters/Animaciones/, se reconocen por el nombre
///    del archivo o del clip (los nombres de Mixamo):
///      "Idle"                                -> parado (si no hay: primera pose de caminar, quieta)
///      "Walking Backwards" / "Running Backward" -> hacia atrás (si no hay: caminar/correr al revés)
///      "Left/Right Strafe Walking"           -> de lado caminando (con una sola, la otra se refleja)
///      "Left/Right Strafe"                   -> de lado corriendo
///    Sin animaciones de lado, AnimacionPorVelocidad gira el modelo hacia donde camina.
///    Si el controlador ya existe NO se borra: se actualiza en su lugar (mismos IDs en el archivo),
///    así correr este menú otra vez no ensucia los diffs ni causa conflictos.
/// 3. Le dice a VistaAgentes qué modelo usar para guardias, guardia de la canoa, cargadores y aldeanos.
/// 4. Pone a Tariácuri en lugar de la cápsula del jugador (la cápsula sigue siendo la colisión), con dos
///    atuendos: disfrazado de tameme ("Modelo") y de guerrero ("ModeloGuerrero", al quemar los 3 trojes).
///    Si los modelos ya estaban puestos, se reutilizan (no se borran y vuelven a crear).
/// </summary>
public static class ConfigurarPersonajes
{
    const string Carpeta = "Assets/Art/Characters/";
    const string CarpetaAnimaciones = "Assets/Art/Characters/Animaciones";
    const string RutaControlador = "Assets/Art/Characters/AC_Personaje.controller";
    const string RutaEscena = "Assets/Scenes/Pueblo.unity";
    static readonly string[] Personajes = { "Tariacuri", "TariacuriGuerrero", "Cargador", "Guardia", "GuardiaCanoa", "Artesano", "Mujer", "Campesino" };

    // posiciones en el blend tree (m/s): caminar y correr
    const float UmbralCaminar = 1.5f;   // velocidad natural de la animación de caminar
    const float UmbralCorrer = 9f;

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
        AnimatorController controlador = ActualizarControlador();
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
        PonerModelosAlJugador(jugador.gameObject, controlador);

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
    static AnimatorController ActualizarControlador()
    {
        AnimationClip caminar = Clip(Carpeta + "Campesino/SK_Campesino.fbx", "walk");
        AnimationClip correr = Clip(Carpeta + "Campesino/SK_Campesino.fbx", "run");
        if (caminar == null || correr == null)
        {
            Debug.LogError("No encontré las animaciones Walking y Running del campesino.");
            return null;
        }
        AnimationClip parado = BuscarAnimacion(new[] { "idle" }, new string[0]);
        AnimationClip atrasCaminar = BuscarAnimacion(new[] { "walk", "back" }, new string[0]);
        AnimationClip atrasCorrer = BuscarAnimacion(new[] { "run", "back" }, new string[0]);
        AnimationClip izqCaminar = BuscarAnimacion(new[] { "strafe", "left", "walk" }, new string[0]);
        AnimationClip derCaminar = BuscarAnimacion(new[] { "strafe", "right", "walk" }, new string[0]);
        AnimationClip izqCorrer = BuscarAnimacion(new[] { "strafe", "left" }, new[] { "walk" });
        AnimationClip derCorrer = BuscarAnimacion(new[] { "strafe", "right" }, new[] { "walk" });
        bool laterales = izqCaminar != null || derCaminar != null;

        // hijos del blend tree: (clip, posición x = de lado, y = adelante, velocidad de la animación)
        List<ChildMotion> hijos = new List<ChildMotion>();
        // parado; sin Idle: la primera pose de caminar, quieta (timeScale 0)
        Agregar(hijos, parado != null ? parado : caminar, 0f, 0f, parado != null ? 1f : 0f, false);
        Agregar(hijos, caminar, 0f, UmbralCaminar, 1f, false);
        Agregar(hijos, correr, 0f, UmbralCorrer, 1f, false);
        // hacia atrás; sin la animación: caminar / correr al revés (timeScale -1)
        Agregar(hijos, atrasCaminar != null ? atrasCaminar : caminar, 0f, -UmbralCaminar, atrasCaminar != null ? 1f : -1f, false);
        Agregar(hijos, atrasCorrer != null ? atrasCorrer : correr, 0f, -UmbralCorrer, atrasCorrer != null ? 1f : -1f, false);
        if (laterales)
        {
            AgregarLaterales(hijos, izqCaminar, derCaminar, UmbralCaminar, 1f);
            if (izqCorrer != null || derCorrer != null)
            {
                AgregarLaterales(hijos, izqCorrer, derCorrer, UmbralCorrer, 1f);
            }
            else
            {
                AgregarLaterales(hijos, izqCaminar, derCaminar, UmbralCorrer, 2f);   // de lado caminando, más rápido
            }
        }

        // el controlador se crea solo si no existe; si existe se actualiza (mismos IDs)
        AnimatorController controlador = AssetDatabase.LoadAssetAtPath<AnimatorController>(RutaControlador);
        bool cambio = false;
        if (controlador == null)
        {
            controlador = AnimatorController.CreateAnimatorControllerAtPath(RutaControlador);
            cambio = true;
        }
        cambio |= AsegurarParametro(controlador, "VelocidadX", AnimatorControllerParameterType.Float, false);
        cambio |= AsegurarParametro(controlador, "VelocidadZ", AnimatorControllerParameterType.Float, false);
        cambio |= AsegurarParametro(controlador, "TieneLaterales", AnimatorControllerParameterType.Bool, laterales);
        cambio |= QuitarParametro(controlador, "Velocidad");      // el del blend tree 1D anterior
        cambio |= QuitarParametro(controlador, "Blend");          // lo agregaba CreateBlendTreeInController

        AnimatorStateMachine maquina = controlador.layers[0].stateMachine;
        AnimatorState estado = null;
        foreach (ChildAnimatorState hijo in maquina.states)
        {
            if (hijo.state.name == "Movimiento")
            {
                estado = hijo.state;
            }
        }
        if (estado == null)
        {
            estado = maquina.AddState("Movimiento");
            cambio = true;
        }
        if (maquina.defaultState != estado)
        {
            maquina.defaultState = estado;
            cambio = true;
        }

        BlendTree arbol = estado.motion as BlendTree;
        if (arbol == null)
        {
            arbol = new BlendTree();
            arbol.name = "Movimiento";
            arbol.hideFlags = HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(arbol, controlador);
            estado.motion = arbol;
            cambio = true;
        }
        if (arbol.blendType != BlendTreeType.FreeformDirectional2D || arbol.blendParameter != "VelocidadX"
            || arbol.blendParameterY != "VelocidadZ" || arbol.useAutomaticThresholds)
        {
            arbol.blendType = BlendTreeType.FreeformDirectional2D;
            arbol.blendParameter = "VelocidadX";
            arbol.blendParameterY = "VelocidadZ";
            arbol.useAutomaticThresholds = false;
            cambio = true;
        }
        ChildMotion[] nuevos = hijos.ToArray();
        if (!MismosHijos(arbol.children, nuevos))
        {
            arbol.children = nuevos;
            cambio = true;
        }

        if (cambio)
        {
            EditorUtility.SetDirty(arbol);
            EditorUtility.SetDirty(controlador);
            AssetDatabase.SaveAssets();
        }
        Debug.Log("Animator " + (cambio ? "actualizado" : "sin cambios") + ". Parado = " + Nombre(parado, "primera pose de caminar") +
                  "; atrás = " + Nombre(atrasCaminar, "caminar al revés") +
                  "; de lado = " + (laterales ? "sí" : "no (el modelo gira hacia donde camina)"));
        return controlador;
    }

    static void Agregar(List<ChildMotion> hijos, Motion clip, float x, float y, float velocidad, bool reflejar)
    {
        ChildMotion hijo = new ChildMotion();
        hijo.motion = clip;
        hijo.position = new Vector2(x, y);
        hijo.timeScale = velocidad;
        hijo.mirror = reflejar;
        hijo.directBlendParameter = "VelocidadX";   // no se usa en 2D, pero Unity lo pide no vacío
        hijos.Add(hijo);
    }

    /// <summary>Izquierda en x negativa y derecha en x positiva; si falta un lado, se refleja el otro.</summary>
    static void AgregarLaterales(List<ChildMotion> hijos, AnimationClip izq, AnimationClip der, float distancia, float velocidad)
    {
        Agregar(hijos, izq != null ? izq : der, -distancia, 0f, velocidad, izq == null);
        Agregar(hijos, der != null ? der : izq, distancia, 0f, velocidad, der == null);
    }

    static bool MismosHijos(ChildMotion[] a, ChildMotion[] b)
    {
        if (a.Length != b.Length)
        {
            return false;
        }
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i].motion != b[i].motion || a[i].position != b[i].position ||
                a[i].timeScale != b[i].timeScale || a[i].mirror != b[i].mirror)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>Agrega el parámetro si falta (y ajusta el valor inicial de los bool). Devuelve si cambió algo.</summary>
    static bool AsegurarParametro(AnimatorController controlador, string nombre, AnimatorControllerParameterType tipo, bool valorBool)
    {
        AnimatorControllerParameter[] parametros = controlador.parameters;
        foreach (AnimatorControllerParameter p in parametros)
        {
            if (p.name != nombre)
            {
                continue;
            }
            if (tipo == AnimatorControllerParameterType.Bool && p.defaultBool != valorBool)
            {
                p.defaultBool = valorBool;
                controlador.parameters = parametros;
                return true;
            }
            return false;
        }
        AnimatorControllerParameter nuevo = new AnimatorControllerParameter();
        nuevo.name = nombre;
        nuevo.type = tipo;
        nuevo.defaultBool = valorBool;
        controlador.AddParameter(nuevo);
        return true;
    }

    static bool QuitarParametro(AnimatorController controlador, string nombre)
    {
        bool quitado = false;
        for (int i = controlador.parameters.Length - 1; i >= 0; i--)
        {
            if (controlador.parameters[i].name == nombre)
            {
                controlador.RemoveParameter(i);
                quitado = true;
            }
        }
        return quitado;
    }

    /// <summary>
    /// Busca en la carpeta Animaciones un clip cuyo nombre de archivo + nombre de clip contenga todas
    /// las palabras y ninguna de las excluidas (sin importar mayúsculas). Los FBX de Mixamo traen el
    /// clip con el nombre "mixamo.com", por eso también cuenta el nombre del archivo.
    /// </summary>
    static AnimationClip BuscarAnimacion(string[] palabras, string[] excluir)
    {
        if (!AssetDatabase.IsValidFolder(CarpetaAnimaciones))
        {
            return null;
        }
        string[] guids = AssetDatabase.FindAssets("t:Model", new[] { CarpetaAnimaciones });
        System.Array.Sort(guids);                  // siempre el mismo orden: mismo resultado en cada corrida
        foreach (string guid in guids)
        {
            string ruta = AssetDatabase.GUIDToAssetPath(guid);
            foreach (AnimationClip clip in AssetDatabase.LoadAllAssetsAtPath(ruta).OfType<AnimationClip>())
            {
                if (clip.name.StartsWith("__preview__"))
                {
                    continue;
                }
                string texto = (Path.GetFileNameWithoutExtension(ruta) + " " + clip.name).ToLower();
                if (ContieneTodas(texto, palabras) && !ContieneAlguna(texto, excluir))
                {
                    return clip;
                }
            }
        }
        return null;
    }

    static bool ContieneTodas(string texto, string[] palabras)
    {
        foreach (string p in palabras)
        {
            if (!texto.Contains(p))
            {
                return false;
            }
        }
        return true;
    }

    static bool ContieneAlguna(string texto, string[] palabras)
    {
        foreach (string p in palabras)
        {
            if (texto.Contains(p))
            {
                return true;
            }
        }
        return false;
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

    static string Nombre(Object o, string siFalta)
    {
        return o != null ? o.name : siFalta;
    }

    // ---------- jugador ----------
    static void PonerModelosAlJugador(GameObject jugador, AnimatorController controlador)
    {
        // apagar lo que se ve de la cápsula y su "frente" (la colisión sigue)
        Renderer capsula = jugador.GetComponent<Renderer>();
        if (capsula != null && capsula.enabled)
        {
            Undo.RecordObject(capsula, "Ocultar cápsula");
            capsula.enabled = false;
        }
        Transform frente = jugador.transform.Find("Frente");
        if (frente != null && frente.GetComponent<Renderer>() != null && frente.GetComponent<Renderer>().enabled)
        {
            Undo.RecordObject(frente.GetComponent<Renderer>(), "Ocultar frente");
            frente.GetComponent<Renderer>().enabled = false;
        }

        GameObject disfraz = PonerModelo(jugador, "Modelo", Modelo("Tariacuri"), controlador, true);
        GameObject guerrero = null;
        if (Modelo("TariacuriGuerrero") != null)
        {
            guerrero = PonerModelo(jugador, "ModeloGuerrero", Modelo("TariacuriGuerrero"), controlador, false);
        }
        else
        {
            Debug.LogWarning("No encontré el modelo TariacuriGuerrero: Tariácuri no cambiará de atuendo.");
        }

        // cambio de atuendo al quemar los 3 trojes
        AtuendoJugador atuendo = jugador.GetComponent<AtuendoJugador>();
        if (atuendo == null)
        {
            atuendo = Undo.AddComponent<AtuendoJugador>(jugador);
        }
        Undo.RecordObject(atuendo, "Atuendo del jugador");
        atuendo.modeloDisfraz = disfraz;
        atuendo.modeloGuerrero = guerrero;
        atuendo.cliente = Object.FindAnyObjectByType<ClienteSimulacion>();
        atuendo.hud = Object.FindAnyObjectByType<HUD>();
        EditorUtility.SetDirty(atuendo);
    }

    /// <summary>
    /// Pone el modelo como hijo del jugador con ese nombre. Si ya hay uno del mismo FBX, lo reutiliza
    /// (así la escena no cambia de IDs cada vez); si es de otro FBX, lo reemplaza.
    /// </summary>
    static GameObject PonerModelo(GameObject jugador, string nombre, GameObject prefab, AnimatorController controlador, bool activo)
    {
        GameObject modelo = null;
        Transform anterior = jugador.transform.Find(nombre);
        if (anterior != null)
        {
            if (PrefabUtility.GetCorrespondingObjectFromSource(anterior.gameObject) == prefab)
            {
                modelo = anterior.gameObject;
            }
            else
            {
                Undo.DestroyObjectImmediate(anterior.gameObject);
            }
        }
        if (modelo == null)
        {
            modelo = (GameObject)PrefabUtility.InstantiatePrefab(prefab, jugador.transform);
            modelo.name = nombre;
            Undo.RegisterCreatedObjectUndo(modelo, "Modelo del jugador");
        }

        // la cápsula está escalada (1, 0.9, 1) y su centro a 0.9 m del suelo:
        // se compensa para que el modelo quede a escala 1 y con los pies en el suelo
        Undo.RecordObject(modelo.transform, "Modelo del jugador");
        Vector3 escala = jugador.transform.localScale;
        modelo.transform.localScale = new Vector3(1f / escala.x, 1f / escala.y, 1f / escala.z);
        modelo.transform.localPosition = new Vector3(0f, -1f, 0f);
        modelo.transform.localRotation = Quaternion.identity;

        Animator animator = modelo.GetComponent<Animator>();
        Undo.RecordObject(animator, "Modelo del jugador");
        animator.runtimeAnimatorController = controlador;
        animator.applyRootMotion = false;

        AnimacionPorVelocidad animacion = modelo.GetComponent<AnimacionPorVelocidad>();
        if (animacion == null)
        {
            animacion = Undo.AddComponent<AnimacionPorVelocidad>(modelo);
        }
        Undo.RecordObject(animacion, "Modelo del jugador");
        animacion.cuerpo = jugador.transform;

        if (modelo.activeSelf != activo)
        {
            Undo.RecordObject(modelo, "Modelo del jugador");
            modelo.SetActive(activo);
        }
        return modelo;
    }

    static GameObject Modelo(string nombre)
    {
        return AssetDatabase.LoadAssetAtPath<GameObject>(Carpeta + nombre + "/SK_" + nombre + ".fbx");
    }
}
