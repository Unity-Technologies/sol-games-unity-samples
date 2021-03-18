using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Content;
using UnityEditor.Build.Pipeline;
using UnityEngine;

public static class BundleBuilder
{
    private const string k_OutputBasePath = "Build/AssetBundles/";
    const string k_UrpBundleName = "urp_shaders";

    [MenuItem("AssetBundles/Build with built-in pipeline (duplicated URP shaders)")]
    public static void BuildAssetBundles()
    {
        var outputPath = k_OutputBasePath + "standard";
        PrepareOutputPath(outputPath);
        BuildPipeline.BuildAssetBundles(outputPath, BuildAssetBundleOptions.None, EditorUserBuildSettings.activeBuildTarget);
    }

    [MenuItem("AssetBundles/Build with built-in pipeline (separate URP shader bundle)")]
    public static void BuildAssetBundlesWithShaderBundle()
    {
        AssetBundleBuild[] content = SetupBundles();
        var outputPath = k_OutputBasePath + "urp_bundle_builtin";
        PrepareOutputPath(outputPath);
        BuildPipeline.BuildAssetBundles(outputPath, content, BuildAssetBundleOptions.None, EditorUserBuildSettings.activeBuildTarget);
    }

    [MenuItem("AssetBundles/Build with SRP Compatibility (separate URP shader bundle)")]
    public static void BuildAssetBundlesWithShaderBundleSRPCompat()
    {
        AssetBundleBuild[] content = SetupBundles();
        var outputPath = k_OutputBasePath + "urp_bundle_srp_compat";
        PrepareOutputPath(outputPath);
        CompatibilityBuildPipeline.BuildAssetBundles(outputPath, content, BuildAssetBundleOptions.None, EditorUserBuildSettings.activeBuildTarget);
    }

    static AssetBundleBuild[] SetupBundles()
    {
        var content = ContentBuildInterface.GenerateAssetBundleBuilds();

        // Add another AssetBundle with the URP shaders
        Array.Resize(ref content, content.Length + 1);
        content[content.Length - 1].assetBundleName = k_UrpBundleName;

        // Only include the Lit shader for faster builds
        content[content.Length - 1].assetNames = new string[] { "Packages/com.unity.render-pipelines.universal/Shaders/Lit.shader" };

        // Uncomment these lines to include all shaders from URP
        //var urpShaders = AssetDatabase.FindAssets("t:shader", new string[] { "Packages/com.unity.render-pipelines.universal/Shaders" });
        //var shaderAssetNames = new List<String>();
        //foreach (var guid in urpShaders)
        //{
        //    shaderAssetNames.Add(AssetDatabase.GUIDToAssetPath(guid));
        //}
        //content[content.Length - 1].assetNames = shaderAssetNames.ToArray();
        
        return content;
    }

    static void PrepareOutputPath(string outputPath)
    {
        if (Directory.Exists(outputPath))
            Directory.Delete(outputPath, true);
        Directory.CreateDirectory(outputPath);
    }
}
