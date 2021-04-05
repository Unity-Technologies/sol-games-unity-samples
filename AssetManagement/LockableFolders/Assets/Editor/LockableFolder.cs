using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// https://docs.unity3d.com/Manual/RunningEditorCodeOnLaunch.html
[InitializeOnLoadAttribute]
public class LockableFolder
{
    const string k_LockPrefix = "locked=";
    static GUIContent s_ToggleText;

    static LockableFolder()
    {
        s_ToggleText = new GUIContent("Locked", "Check this to prevent adding or removing assets from this folder.");

        // Event raised while drawing the header of the Inspector window, after the default header items have been drawn.
        Editor.finishedDefaultHeaderGUI += DisplayGUI;
    }

    static void DisplayGUI(Editor editor)
    {
        // TODO: support multi-object editing
        // Add a checkbox in the Inspector to folders to "lock" them
        var path = AssetDatabase.GetAssetPath(editor.target);
        if (AssetDatabase.IsValidFolder(path))
        {
            var folderAsset = AssetImporter.GetAtPath(path);
            bool isLocked = folderAsset.userData.StartsWith(k_LockPrefix);
            bool toggleState = EditorGUILayout.Toggle(s_ToggleText, isLocked);
            if (toggleState != isLocked)
            {
                Undo.RecordObject(folderAsset, "Lock");
                if (toggleState)
                {
                    var childrenAssets = AssetDatabase.FindAssets(null, new string[] { path });
                    folderAsset.userData = k_LockPrefix + string.Join(",", childrenAssets);
                }
                else
                {
                    folderAsset.userData = null;
                }
                folderAsset.SaveAndReimport();
            }
        }
    }

    // Validates asset changes against the locked folders
    class LockableFolderValidator : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            bool hasError = false;
            foreach (string assetPath in importedAssets)
            {
                var errorString = "Cannot add asset \"{0}\" to folder \"{1}\"";
                if (CheckViolatedLockedFolder(assetPath, assetPath, errorString))
                {
                    hasError = true;
                }
            }

            foreach (string assetPath in deletedAssets)
            {
                var errorString = "Cannot delete asset \"{0}\" from folder \"{1}\"";
                if (CheckViolatedLockedFolder(assetPath, assetPath, errorString, true))
                {
                    hasError = true;
                }
            }

            for (int i = 0; i < movedFromAssetPaths.Length; ++i)
            {
                var errorString = "Cannot move asset \"{0}\" to folder \"{1}\"";
                if (CheckViolatedLockedFolder(movedAssets[i], movedAssets[i], errorString))
                {
                    hasError = true;
                }

                errorString = "Cannot move asset \"{0}\" from folder \"{1}\"";
                if (CheckViolatedLockedFolder(movedAssets[i], movedFromAssetPaths[i], errorString, true))
                {
                    hasError = true;
                }
            }

            if (hasError)
            {
                EditorUtility.DisplayDialog("Locked folder violation",
                    "Oy! A locked folder's content has been modified! Don't do that! Check the logs for details...",
                    "Whoops!");
            }
        }

        static bool CheckViolatedLockedFolder(string assetPath, string searchPath, string errorString, bool removingAsset = false)
        {
            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (guid == null)
            {
                Debug.LogWarningFormat("Asset GUID not found for asset at path \"{0}\"", assetPath);
                return false;
            }

            var violatedLockedFolder = FindViolatedLockedParentFolder(searchPath, guid, removingAsset);
            if (violatedLockedFolder != null)
            {
                var assetName = assetPath.Substring(assetPath.LastIndexOf('/') + 1);
                Debug.LogErrorFormat("[LockableFolder] " + errorString, assetName, violatedLockedFolder);
                return true;
            }
            return false;
        }

        static string FindViolatedLockedParentFolder(string searchPath, string assetGuid, bool removingAsset = false)
        {
            var parentFolderPath = searchPath;
            int lastSlashIndex;
            while ((lastSlashIndex = parentFolderPath.LastIndexOf('/')) > 0)
            {
                parentFolderPath = parentFolderPath.Substring(0, lastSlashIndex);

                // parentFolderAsset can be null when adding an asset to ProjectSettings, deleting parent folders, etc.
                var parentFolderAsset = AssetImporter.GetAtPath(parentFolderPath);
                if (parentFolderAsset != null && parentFolderAsset.userData.StartsWith(k_LockPrefix))
                {
                    return parentFolderAsset.userData.Contains(assetGuid) == removingAsset ? parentFolderPath : null;
                }
            }
            return null;
        }
    }
}
