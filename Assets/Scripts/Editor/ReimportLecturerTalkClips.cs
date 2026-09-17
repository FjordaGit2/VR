#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class ReimportLecturerTalkClips
{
    [MenuItem("VR Study/Reimport Lecturer Talk Animations (loop)")]
    public static void Reimport()
    {
        string[] paths =
        {
            "Assets/Static/Sc2LectureHall/3DCharacters/LecturerAnim1.fbx",
            "Assets/Static/Sc2LectureHall/3DCharacters/LecturerAnim2.fbx",
            "Assets/Static/Sc2LectureHall/3DCharacters/LecturerAnim3.fbx",
            "Assets/Static/Sc2LectureHall/3DCharacters/LecturerAnim4.fbx",
        };

        for (int i = 0; i < paths.Length; i++)
        {
            var importer = AssetImporter.GetAtPath(paths[i]) as ModelImporter;
            if (importer == null)
                continue;

            var clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0)
                clips = importer.defaultClipAnimations;

            bool changed = false;
            for (int c = 0; c < clips.Length; c++)
            {
                if (!clips[c].loopTime ||
                    !clips[c].keepOriginalPositionY ||
                    !clips[c].keepOriginalPositionXZ)
                {
                    clips[c].loopTime = true;
                    clips[c].keepOriginalPositionY = true;
                    clips[c].keepOriginalPositionXZ = true;
                    changed = true;
                }
            }

            if (changed)
            {
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
            }
            else
            {
                AssetDatabase.ImportAsset(paths[i], ImportAssetOptions.ForceUpdate);
            }
        }

        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog(
            "Lecturer Animations",
            "Reimported LecturerAnim1–4 with loop enabled.\nAlso check AnimAndImage: Lecturer Voice Volume (default 4).",
            "OK");
    }
}
#endif
