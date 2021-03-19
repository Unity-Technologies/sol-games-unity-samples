using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public class ProbeActions : Object
{
    const string k_ProbesFile = "probes.txt";
    static readonly Vector3[] k_DefaultPositions = new Vector3[]
    {
        new Vector3(1.0f, 1.0f, 1.0f),
        new Vector3(1.0f, 1.0f, -1.0f),
        new Vector3(1.0f, -1.0f, 1.0f),
        new Vector3(1.0f, -1.0f, -1.0f),
        new Vector3(-1.0f, 1.0f, 1.0f),
        new Vector3(-1.0f, 1.0f, -1.0f),
        new Vector3(-1.0f, -1.0f, 1.0f),
        new Vector3(-1.0f, -1.0f, -1.0f),
    };

    [MenuItem("Light Probes/Import external probe data")]
    public static void ImportProbes()
    {
        try
        {
            EditorUtility.DisplayProgressBar("Importing external probe data", "Parsing data", 0);
            DoImportProbePositions();

            EditorUtility.DisplayProgressBar("Importing external probe data", "Dummy bake job", 0.25f);
            Lightmapping.bakeCompleted -= OnBakeCompleted;
            Lightmapping.bakeCompleted += OnBakeCompleted;
            Lightmapping.BakeAsync();
        }
        catch
        {
            Debug.LogError("Light probe import failed");
            EditorUtility.ClearProgressBar();
            throw;
        }
    }

    [MenuItem("Light Probes/[1] Import probe positions")]
    public static void ImportProbePositions()
    {
        DoImportProbePositions();
    }

    [MenuItem("Light Probes/[2] Trigger light bake")]
    public static void TriggerBake()
    {
        Lightmapping.Bake();
    }

    [MenuItem("Light Probes/[3] Import coefficients")]
    public static void ImportCoefficients()
    {
        DoImportCoefficients();
    }

    // Log all currently baked probe data in the format expected by ParseProbeData()
    [MenuItem("Light Probes/Log light probe data")]
    public static void LogProbeData()
    {
        if (LightmapSettings.lightProbes == null)
        {
            Debug.LogError("Light baking must be done at least once.");
            return;
        }

        var bakedProbes = LightmapSettings.lightProbes.bakedProbes;
        var probePositions = LightmapSettings.lightProbes.positions;
        var probeCount = LightmapSettings.lightProbes.count;
        var builder = new StringBuilder();
        for (int i = 0; i < probeCount; i++)
        {
            builder.Append($"{probePositions[i].x}, {probePositions[i].y}, {probePositions[i].z}");
            for (int coefficient = 0; coefficient < 9; coefficient++)
            {
                for (int rgb = 0; rgb < 3; rgb++)
                {
                    builder.Append($", {bakedProbes[i][rgb, coefficient]}");
                }
            }
            builder.Append("\n");
        }
        Debug.Log(builder);
    }

    [MenuItem("Light Probes/Reset light probes")]
    public static void ResetPositions()
    {
        var group = FindObjectOfType<LightProbeGroup>();
        group.probePositions = k_DefaultPositions;
        EditorUtility.SetDirty(group);
    }

    [MenuItem("Light Probes/Clear baked data")]
    public static void ClearBakedData()
    {
        Lightmapping.ClearLightingDataAsset();
        Lightmapping.Clear();
    }

    static void OnBakeCompleted()
    {
        Lightmapping.bakeCompleted -= OnBakeCompleted;
        try
        {
            EditorUtility.DisplayProgressBar("Importing external probe data", "Applying SH coefficients", 0.75f);
            DoImportCoefficients();
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    static void DoImportProbePositions()
    {
        var probeData = ParseProbeData(k_ProbesFile);

        // The (world) light probe positions need to be converted to the local CS of the LightProbeGroup
        var group = FindObjectOfType<LightProbeGroup>();
        var worldToLocalMatrix = group.transform.worldToLocalMatrix;
        var probePositions = new Vector3[probeData.Count];
        for (int i = 0; i < probeData.Count; i++)
        {
            probePositions[i] = worldToLocalMatrix.MultiplyPoint3x4(probeData[i].Position);
        }
        group.probePositions = probePositions;
    }

    static void DoImportCoefficients()
    {
        var probeData = ParseProbeData(k_ProbesFile);
        var bakedProbes = LightmapSettings.lightProbes.bakedProbes;
        var probePositions = LightmapSettings.lightProbes.positions;
        var probeCount = LightmapSettings.lightProbes.count;

        // This is O(n^2) and should probably be adapted for a large number of light probes
        int nbImported = 0;
        foreach (var data in probeData)
        {
            for (int i = 0; i < probeCount; i++)
            {
                // Don't assume anything about the ordering of light probes and compare the positions instead.
                if (probePositions[i] == data.Position)
                {
                    nbImported += 1;
                    data.ApplyTo(ref bakedProbes[i]);
                }
            }
        }
        LightmapSettings.lightProbes.bakedProbes = bakedProbes;

        if (nbImported == probeData.Count)
        {
            Debug.Log($"Imported {nbImported} light probes from {k_ProbesFile}.");
        }
        else
        {
            var nbNotImported = probeData.Count - nbImported;
            Debug.LogWarning($"Imported {nbImported} light probes from {k_ProbesFile}, but {nbNotImported} light probes were not found.");
        }

        EditorUtility.SetDirty(Lightmapping.lightingDataAsset);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    static List<LightProbeData> ParseProbeData(string path)
    {
        var probeData = new List<LightProbeData>();
        foreach (var line in File.ReadLines(path))
        {
            if (line.Length == 0)
                continue;

            // Expected data layout is csv of:
            // - position: 3 floats
            // - coefficients: 9 * 3 floats
            var coords = line.Split(',');
            if (coords.Length != 3 + 9 * 3)
            {
                throw new System.ArgumentException($"{path} does not contain valid positions and coefficients");
            }
            var item = new LightProbeData();
            item.Position = new Vector3(float.Parse(coords[0]), float.Parse(coords[1]), float.Parse(coords[2]));
            for (int i = 0; i < 9; i++)
            {
                int offset = 3 + i * 3;
                item.Coefficients[i].Set(
                    float.Parse(coords[offset + 0]),
                    float.Parse(coords[offset + 1]),
                    float.Parse(coords[offset + 2])
                );
            }
            probeData.Add(item);
        }
        return probeData;
    }

    class LightProbeData
    {
        public Vector3 Position;
        public Vector3[] Coefficients = new Vector3[9];

        public void ApplyTo(ref SphericalHarmonicsL2 probeCoefficients)
        {
            for (int i = 0; i < Coefficients.Length; i++)
            {
                probeCoefficients[0, i] = Coefficients[i].x;
                probeCoefficients[1, i] = Coefficients[i].y;
                probeCoefficients[2, i] = Coefficients[i].z;
            }
        }
    }
}
