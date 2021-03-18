using System;
using UnityEditor;
using UnityEngine;

// Adds buttons to the Inspector to instantiate assets from a ScriptableObject
// See https://docs.unity3d.com/Manual/RunningEditorCodeOnLaunch.html
[InitializeOnLoadAttribute]
public class ScriptableObjectInstantiator : Editor
{
    static ScriptableObjectInstantiator()
    {
        Editor.finishedDefaultHeaderGUI += ScriptableObjectInstantiator.DisplayGUI;
    }

    private static void DisplayGUI(Editor editor)
    {
        if (!(editor.target is MonoScript target) || !target.GetClass().IsSubclassOf(typeof(ScriptableObject)))
            return;

        Type type = target.GetClass();
        if (GUILayout.Button($"Instantiate {type.Name} asset"))
            ScriptableObjectInstantiator.InstantiateScriptableObject(type, "Assets");

        // Add a second button to instantiate in the currently selected (parent) folder
        string path = AssetDatabase.GetAssetPath(Selection.activeObject);
        if (String.IsNullOrEmpty(path))
            return;

        if (!AssetDatabase.IsValidFolder(path))
            path = path.Substring(0, path.LastIndexOf('/'));
        if (!AssetDatabase.IsValidFolder(path))
            return;

        if (GUILayout.Button($"Instantiate {type.Name} asset in\n{path}"))
            ScriptableObjectInstantiator.InstantiateScriptableObject(type, path);
    }

    private static void InstantiateScriptableObject(Type type, string path)
    {
        ScriptableObject instance = ScriptableObject.CreateInstance(type);
        path = AssetDatabase.GenerateUniqueAssetPath($"{path}/{type.Name}.asset");
        AssetDatabase.CreateAsset(instance, path);
        EditorGUIUtility.PingObject(instance);
    }
}
