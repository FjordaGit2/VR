#if UNITY_EDITOR
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class AssignLectureLessonAssets
{
    const string ImagesFolder = "Assets/Sc2LectureHall/Images";
    const string AudiosFolder = "Assets/Additional Audios";

    // Matched to recorded Additional Audios lengths (1.mp3–14.mp3).
    static readonly float[] RecordedDurationsSeconds =
    {
        65f, 66f, 40f, 63f,
        43f, 37f, 36f, 32f,
        39f, 36f, 31f, 49f,
        39f, 388f,
    };

    [MenuItem("VR Study/Assign Lecture Slides + Audio on AnimAndImage")]
    public static void AssignSlidesAndAudio()
    {
        var lecture = Object.FindObjectOfType<AnimAndImage>();
        if (lecture == null)
        {
            EditorUtility.DisplayDialog("Assign Lecture Assets", "No AnimAndImage in the open scene.", "OK");
            return;
        }

        if (lecture.lessonSprites == null || lecture.lessonSprites.Length != AnimAndImage.LessonSlideCount)
            lecture.lessonSprites = new Sprite[AnimAndImage.LessonSlideCount];
        if (lecture.lessonVoiceClips == null || lecture.lessonVoiceClips.Length != AnimAndImage.LessonSlideCount)
            lecture.lessonVoiceClips = new AudioClip[AnimAndImage.LessonSlideCount];

        int sprites = 0;
        int clips = 0;
        for (int i = 1; i <= AnimAndImage.LessonSlideCount; i++)
        {
            Sprite sprite = LoadSlideSprite(i);
            if (sprite != null)
            {
                lecture.lessonSprites[i - 1] = sprite;
                sprites++;
            }

            AudioClip clip = LoadVoiceClip(i);
            if (clip != null)
            {
                lecture.lessonVoiceClips[i - 1] = clip;
                clips++;
            }
        }

        lecture.sectionDurationsSeconds = (float[])RecordedDurationsSeconds.Clone();

        if (lecture.imgLession == null)
        {
            var images = Object.FindObjectsOfType<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i] != null && images[i].gameObject.name == "imgLession")
                {
                    lecture.imgLession = images[i];
                    break;
                }
            }
        }

        if (lecture.lecturerRoot == null)
        {
            var roots = Object.FindObjectsOfType<Transform>(true);
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i] != null && roots[i].name == "LecturerAnim1")
                {
                    lecture.lecturerRoot = roots[i].gameObject;
                    if (lecture.anim == null)
                        lecture.anim = roots[i].GetComponent<Animator>();
                    break;
                }
            }
        }

        if (lecture.lecturerVoice == null && lecture.lecturerRoot != null)
        {
            lecture.lecturerVoice = lecture.lecturerRoot.GetComponentInChildren<AudioSource>(true);
            if (lecture.lecturerVoice == null)
            {
                lecture.lecturerVoice = lecture.lecturerRoot.AddComponent<AudioSource>();
                lecture.lecturerVoice.playOnAwake = false;
                lecture.lecturerVoice.spatialBlend = 1f;
            }
        }

        if (lecture.img1 == null && lecture.lessonSprites.Length > 0)
            lecture.img1 = lecture.lessonSprites[0];

        EditorUtility.SetDirty(lecture);
        if (lecture.lecturerRoot != null)
            EditorUtility.SetDirty(lecture.lecturerRoot);

        EditorUtility.DisplayDialog(
            "Assign Lecture Assets",
            "Sprites: " + sprites + " / " + AnimAndImage.LessonSlideCount + "\n" +
            "Audio clips: " + clips + " / " + AnimAndImage.LessonSlideCount + "\n" +
            "Section durations set to recorded lengths (~16 min total).",
            "OK");
    }

    static Sprite LoadSlideSprite(int oneBasedIndex)
    {
        string[] candidates =
        {
            ImagesFolder + "/" + oneBasedIndex + ".png",
            ImagesFolder + "/" + oneBasedIndex + ".PNG",
        };

        for (int i = 0; i < candidates.Length; i++)
        {
            if (!File.Exists(candidates[i]))
                continue;
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(candidates[i]);
            if (sprite != null)
                return sprite;
        }

        if (!Directory.Exists(ImagesFolder))
            return null;
        string pattern = "^" + oneBasedIndex + "\\.png$";
        foreach (string path in Directory.GetFiles(ImagesFolder))
        {
            string name = Path.GetFileName(path);
            if (!Regex.IsMatch(name, pattern, RegexOptions.IgnoreCase))
                continue;
            string assetPath = ImagesFolder + "/" + name;
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite != null)
                return sprite;
        }

        return null;
    }

    static AudioClip LoadVoiceClip(int oneBasedIndex)
    {
        string path = AudiosFolder + "/" + oneBasedIndex + ".mp3";
        if (!File.Exists(path))
            return null;
        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }
}
#endif
