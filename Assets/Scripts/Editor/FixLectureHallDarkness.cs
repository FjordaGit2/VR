#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class FixLectureHallDarkness
{
    const string Sc2A = "Assets/Scenes/Sc2LectureHall.unity";
    const string Sc2B = "Assets/Scenes/Sc2BLectureHall.unity";
    const string RequestPath = "Temp/fix_lecture_dark.request";

    [DidReloadScripts]
    static void AutoRunIfRequested()
    {
        if (!File.Exists(RequestPath))
            return;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        File.Delete(RequestPath);
        EditorApplication.delayCall += () =>
        {
            try
            {
                Fix();
            }
            catch (Exception ex)
            {
                Debug.LogError("[FixLectureDark] " + ex);
            }
        };
    }

    [MenuItem("VR Study/Fix Dark Lecture Hall Lighting")]
    public static void Fix()
    {
        string previous = SceneManager.GetActiveScene().path;
        FixScene(Sc2A);
        FixScene(Sc2B);
        if (!string.IsNullOrEmpty(previous) && File.Exists(previous))
            EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
        Debug.Log("[FixLectureDark] v2 Lecture halls restored to realtime lighting.");
    }

    static void FixScene(string scenePath)
    {
        Debug.Log("[FixLectureDark] v2 Opening " + scenePath);
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
            throw new Exception("Could not open " + scenePath);

        Lightmapping.Clear();
        Lightmapping.lightingDataAsset = null;

        var lights = Resources.FindObjectsOfTypeAll<Light>();
        for (int i = 0; i < lights.Length; i++)
        {
            var light = lights[i];
            if (light == null || light.gameObject.scene != scene)
                continue;
            if (EditorUtility.IsPersistent(light))
                continue;
            light.lightmapBakeType = LightmapBakeType.Realtime;
            if (light.type == LightType.Point)
                light.shadows = LightShadows.None;
            EditorUtility.SetDirty(light);
        }

        var objects = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < objects.Length; i++)
        {
            var go = objects[i];
            if (go == null || go.scene != scene)
                continue;
            if (EditorUtility.IsPersistent(go))
                continue;
            if (GameObjectUtility.GetStaticEditorFlags(go) != 0)
            {
                GameObjectUtility.SetStaticEditorFlags(go, 0);
                EditorUtility.SetDirty(go);
            }
        }

        var renderers = Resources.FindObjectsOfTypeAll<MeshRenderer>();
        for (int i = 0; i < renderers.Length; i++)
        {
            var renderer = renderers[i];
            if (renderer == null || renderer.gameObject.scene != scene)
                continue;
            if (EditorUtility.IsPersistent(renderer))
                continue;
            renderer.lightmapIndex = -1;
            renderer.realtimeLightmapIndex = -1;
            EditorUtility.SetDirty(renderer);
        }

        LightingSettings settings;
        if (Lightmapping.TryGetLightingSettings(out settings) && settings != null)
        {
            settings.ao = false;
            settings.aoExponentDirect = 0f;
            settings.bakedGI = false;
            EditorUtility.SetDirty(settings);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new Exception("SaveScene failed for " + scenePath);

        Debug.Log("[FixLectureDark] Saved " + scenePath);
    }
}
#endif
