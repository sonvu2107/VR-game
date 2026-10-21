using System.Linq;
using UnityEditor;
using UnityEngine;

public static class MenuPreviewLibraryBuilder
{
    [MenuItem("Tools/The Labyrinth/Refresh Menu Preview")]
    public static void Build()
    {
        const string path = "Assets/Resources/MenuPreviewLibrary.asset";
        var library = AssetDatabase.LoadAssetAtPath<MenuPreviewLibrary>(path);
        if (library == null)
        {
            library = ScriptableObject.CreateInstance<MenuPreviewLibrary>();
            AssetDatabase.CreateAsset(library, path);
        }
        library.idle = Frames("idle");
        library.run = Frames("run");
        library.attack = Frames("attack");
        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();
    }

    private static Sprite[] Frames(string name)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"Assets/Animations/Player/{name}.anim");
        return AnimationUtility.GetObjectReferenceCurveBindings(clip)
            .Where(binding => binding.type == typeof(SpriteRenderer) && binding.propertyName == "m_Sprite")
            .SelectMany(binding => AnimationUtility.GetObjectReferenceCurve(clip, binding))
            .Select(key => key.value as Sprite).Where(sprite => sprite != null).ToArray();
    }
}
