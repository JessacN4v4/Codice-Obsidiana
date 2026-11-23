using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Configura solo la importación del arte que entra a Assets/Art (un AssetPostprocessor
/// es una clase que Unity llama cada vez que importa un archivo).
/// Modelos: sin cámaras, luces, colliders ni animaciones (las colisiones las pone el graybox).
/// Personajes: esqueleto Humanoid (para compartir animaciones) y animaciones en bucle.
/// Texturas: "_Normal" como mapa de normales; "_Mask" y "_MetallicSmoothness" en modo lineal; máximo 2K.
/// Solo actúa la primera vez que se importa cada archivo, para no borrar ajustes hechos a mano.
/// </summary>
public class ImportacionArte : AssetPostprocessor
{
    const string CarpetaArte = "Assets/Art/";
    const string CarpetaModelos = "Assets/Art/Models/";
    const string CarpetaPersonajes = "Assets/Art/Characters/";

    void OnPreprocessModel()
    {
        bool esModelo = assetPath.StartsWith(CarpetaModelos);
        bool esPersonaje = assetPath.StartsWith(CarpetaPersonajes);
        if ((!esModelo && !esPersonaje) || !assetImporter.importSettingsMissing)
        {
            return;
        }
        ModelImporter modelo = (ModelImporter)assetImporter;
        modelo.importCameras = false;
        modelo.importLights = false;
        modelo.addCollider = false;                 // la colisión exacta ya está en el graybox
        modelo.isReadable = false;                  // ahorra memoria
        modelo.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;

        if (esPersonaje)
        {
            // Humanoid: Unity reconoce cabeza, brazos y piernas, y así una animación sirve para todos
            modelo.animationType = ModelImporterAnimationType.Human;
            modelo.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            modelo.importAnimation = true;
        }
        else
        {
            modelo.importAnimation = false;
            modelo.animationType = ModelImporterAnimationType.None;
        }
    }

    void OnPreprocessAnimation()
    {
        if (!assetPath.StartsWith(CarpetaPersonajes) || !assetImporter.importSettingsMissing)
        {
            return;
        }
        ModelImporter modelo = (ModelImporter)assetImporter;
        ModelImporterClipAnimation[] clips = modelo.defaultClipAnimations;
        foreach (ModelImporterClipAnimation clip in clips)
        {
            string nombre = clip.name.ToLower();
            // caminar, correr, estar parado y moverse de lado se repiten sin fin
            // (los FBX de Mixamo llaman al clip "mixamo.com": entonces cuenta el nombre del archivo)
            string texto = nombre + " " + Path.GetFileNameWithoutExtension(assetPath).ToLower();
            clip.loopTime = texto.Contains("walk") || texto.Contains("run") || texto.Contains("idle") || texto.Contains("strafe");
            // la animación no mueve ni gira al personaje: eso lo hace el juego ("bake into pose")
            clip.lockRootRotation = true;
            clip.lockRootHeightY = true;
            clip.lockRootPositionXZ = true;
        }
        modelo.clipAnimations = clips;
    }

    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(CarpetaArte) || !assetImporter.importSettingsMissing)
        {
            return;
        }
        TextureImporter textura = (TextureImporter)assetImporter;
        string nombre = Path.GetFileNameWithoutExtension(assetPath);
        textura.maxTextureSize = 2048;
        textura.mipmapEnabled = true;
        // los pisos y muros se repiten (tiling); las de personajes no
        textura.wrapMode = assetPath.StartsWith(CarpetaPersonajes) ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;

        if (nombre.Contains("Vegetacion"))
        {
            // hojas con alfa recortado: que no se "adelgacen" al verse de lejos (mipmaps)
            textura.alphaIsTransparency = true;
            textura.mipMapsPreserveCoverage = true;
            textura.alphaTestReferenceValue = 0.5f;
        }
        if (nombre.Contains("_Cielo_"))
        {
            // panorama del cielo (Skybox): a 4K y sin mipmaps para que no se vea la costura
            textura.maxTextureSize = 4096;
            textura.mipmapEnabled = false;
            return;
        }
        if (nombre.EndsWith("_Normal") || nombre.Contains("_Normal_"))   // "_Normal" o "_Normal_Olas"
        {
            textura.textureType = TextureImporterType.NormalMap;
        }
        else if (nombre.EndsWith("_Mask") || nombre.EndsWith("_MetallicSmoothness"))
        {
            textura.textureType = TextureImporterType.Default;
            textura.sRGBTexture = false;            // datos (metal, oclusión, suavidad), no color
        }
        else
        {
            textura.textureType = TextureImporterType.Default;
            textura.sRGBTexture = true;             // color
        }
    }
}
