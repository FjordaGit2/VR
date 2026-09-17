#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BakeTask23Lighting
{
    const string Sc2A = "Assets/Scenes/Sc2LectureHall.unity";
    const string Sc2B = "Assets/Scenes/Sc2BLectureHall.unity";

    [MenuItem("VR Study/Bake Lecture Hall Lighting")]
    public static void Bake()
    {
        string previous = SceneManager.GetActiveScene().path;
        BakeScene(Sc2A);
        BakeScene(Sc2B);
        if (!string.IsNullOrEmpty(previous) && File.Exists(previous))
            EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
        Debug.Log("[BakeTask23] Both lecture-hall scenes baked.");
    }

    static void BakeScene(string scenePath)
    {
        Debug.Log("[BakeTask23] Opening " + scenePath);
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
            throw new Exception("Could not open " + scenePath);

        var settings = EnsureLightingSettings(scene);
        Lightmapping.Clear();

        settings.lightmapper = LightingSettings.Lightmapper.ProgressiveGPU;
        EditorUtility.SetDirty(settings);
        bool ok = false;
        try
        {
            ok = Lightmapping.Bake();
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BakeTask23] GPU bake threw, trying CPU. " + ex.Message);
            ok = false;
        }

        if (!ok)
        {
            Debug.LogWarning("[BakeTask23] GPU bake failed for " + scenePath + ", retrying Progressive CPU.");
            settings.lightmapper = LightingSettings.Lightmapper.ProgressiveCPU;
            EditorUtility.SetDirty(settings);
            if (!Lightmapping.Bake())
                throw new Exception("CPU bake also failed for " + scenePath);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new Exception("SaveScene failed for " + scenePath);

        Debug.Log("[BakeTask23] Saved " + scenePath);
    }

    static LightingSettings EnsureLightingSettings(Scene scene)
    {
        LightingSettings settings;
        if (Lightmapping.TryGetLightingSettings(out settings) && settings != null)
        {
            Configure(settings);
            EditorUtility.SetDirty(settings);
            return settings;
        }

        string assetPath = scene.name == "Sc2BLectureHall"
            ? "Assets/Scenes/Sc2b.lighting"
            : "Assets/Scenes/Sc2a.lighting";
        settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(assetPath);
        if (settings == null)
            throw new Exception("No LightingSettings on " + scene.path + " and could not load " + assetPath);
        Configure(settings);
        Lightmapping.lightingSettings = settings;
        EditorUtility.SetDirty(settings);
        Debug.Log("[BakeTask23] Assigned " + assetPath);
        return settings;
    }

    static void Configure(LightingSettings settings)
    {
        settings.bakedGI = true;
        settings.realtimeGI = false;
        settings.mixedBakeMode = MixedLightingMode.Shadowmask;
        settings.prioritizeView = false;
        settings.directSampleCount = 32;
        settings.indirectSampleCount = 512;
        settings.lightmapMaxSize = 1024;
        settings.lightmapResolution = 40f;
        settings.lightmapPadding = 2;
        settings.filteringMode = LightingSettings.FilterMode.Auto;
        settings.ao = true;
        settings.aoMaxDistance = 1f;
        settings.aoExponentDirect = 1f;
        settings.aoExponentIndirect = 1f;
        settings.directionalityMode = LightmapsMode.CombinedDirectional;
        settings.compressLightmaps = true;
    }
}
#endif

