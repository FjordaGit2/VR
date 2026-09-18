using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lecture hall slide + lecturer voice schedule for Tasks 2 and 3.
/// Assign 14 sprites (1.png–14.png) and 14 AudioClips in order.
/// Call <see cref="BeginLecture"/> when the main digit task starts.
/// Student avatar loops and the paper plane sequence continue independently.
/// </summary>
public class AnimAndImage : MonoBehaviour
{
    public const int LessonSlideCount = 14;

    [Header("Lesson slides")]
    [Tooltip("Canvas Image that shows the current lesson screenshot.")]
    public Image imgLession;

    [Tooltip("Optional fallback used only if lessonSprites[0] is empty.")]
    public Sprite img1;

    [Tooltip("14 sprites in order (Element 0 = 1.png … Element 13 = 14.png).")]
    public Sprite[] lessonSprites = new Sprite[LessonSlideCount];

    [Header("Lecturer voice")]
    [Tooltip("AudioSource already on the lecturer / scene. Assigned in Inspector — not created at runtime.")]
    public AudioSource lecturerVoice;

    [Tooltip("14 ElevenLabs clips matching the slides.")]
    public AudioClip[] lessonVoiceClips = new AudioClip[LessonSlideCount];

    [Tooltip("Playback volume multiplier (clips are often quiet).")]
    [Range(0.5f, 8f)] public float lecturerVoiceVolume = 4f;

    [Tooltip("0 = 2D flat, 1 = full 3D. Keep high for spatialization.")]
    [Range(0f, 1f)] public float lecturerSpatialBlend = 1f;

    [Tooltip("Within this distance volume stays near full (classroom scale).")]
    [Min(0.5f)] public float lecturerMinDistance = 10f;
    [Min(1f)] public float lecturerMaxDistance = 40f;

    [Tooltip("How long each slide stays on screen (seconds). Matched to Additional Audios 1.mp3–14.mp3. If a clip is longer, we wait for the clip.")]
    public float[] sectionDurationsSeconds =
    {
        65f, 66f, 40f, 63f,
        43f, 37f, 36f, 32f,
        39f, 36f, 31f, 49f,
        39f, 388f,
    };

    [Header("Lecturer avatar (James)")]
    [Tooltip("James root GameObject. Enabled while the lecture runs.")]
    public GameObject lecturerRoot;

    [Tooltip("Animator using the James controller (JamesTalking1–5).")]
    public Animator anim;

    [Tooltip("How long each JamesTalking clip stays before the next one (seconds).")]
    [Min(1f)] public float lecturerAnimHoldSeconds = 4f;

    [Tooltip("Animator playback speed for talk clips (1 = normal, lower = slower).")]
    [Range(0.25f, 1f)] public float lecturerAnimSpeed = 0.85f;

    [Tooltip("Deprecated — kept for scene serialization.")]
    [Min(0f)] public float lecturerAnimBlendSeconds = 0f;

    [Tooltip("Deprecated — kept for scene serialization.")]
    [Min(1)] public int lecturerAnimCyclesPerClip = 1;

    [Tooltip("Deprecated — kept for scene serialization.")]
    [Min(2f)] public float lecturerAnimPhaseSeconds = 8f;

    [Tooltip("Pin root transform so talk clips cannot sink/slide the avatar.")]
    public bool lockLecturerRootPose = true;

    [Header("Paper plane")]
    public GameObject paperPlane;
    public AudioSource paperPlaneSource;
    public AudioClip paperPlaneClip;

    [Header("Audience avatars")]
    public Animator max;
    public Animator george;
    public Animator john;

    [Header("Options")]
    [Tooltip("If true, BeginLecture is called from Start (useful for preview). Main tasks call BeginLecture explicitly.")]
    public bool autoStartLectureOnPlay;

    bool _planePlayed;
    Coroutine _lectureRoutine;
    Coroutine _studentRoutine;
    Coroutine _lecturerAnimRoutine;
    Vector3 _lecturerHomePosition;
    Quaternion _lecturerHomeRotation;
    bool _lecturerHomeCached;
    static readonly string[] JamesTalkStates =
    {
        "JamesTalking1",
        "JamesTalking2",
        "JamesTalking3",
        "JamesTalking4",
        "JamesTalking5",
    };

    static readonly string[] JamesAnimBools =
    {
        "Animation1",
        "Animation2",
        "Animation3",
        "Animation4",
        "Animation5",
        "Animation6",
    };

    public bool IsLecturing { get; private set; }
    public int CurrentSlideIndex { get; private set; } = -1;

    void Awake()
    {
        // Voice is created when the lecturer is enabled in BeginLecture.
    }

    void Start()
    {
        if (paperPlane != null)
            paperPlane.SetActive(false);

        CacheLecturerHomePose();

        // James stays visible once LectureHallA/B is enabled after calibration,
        // but talking/voice wait for BeginLecture (task Start button).
        SetLecturerAvatarVisible(true);
        SetLecturerTalkingEnabled(false);

        if (!IsLecturing)
        {
            ShowSlide(0, playVoice: false);
            ResetStudentAnimators();
            if (_studentRoutine == null)
                _studentRoutine = StartCoroutine(StudentAnimationLoop());

            if (autoStartLectureOnPlay)
                BeginLecture();
        }
        else if (_studentRoutine == null)
        {
            ResetStudentAnimators();
            _studentRoutine = StartCoroutine(StudentAnimationLoop());
        }
    }

    void LateUpdate()
    {
        if (!IsLecturing || !lockLecturerRootPose || lecturerRoot == null || !_lecturerHomeCached)
            return;
        lecturerRoot.transform.position = _lecturerHomePosition;
        lecturerRoot.transform.rotation = _lecturerHomeRotation;
    }

    void OnDisable()
    {
        if (IsLecturing)
            StopLecture();
    }

    /// <summary>
    /// Shows/hides the lecturer. If this script is on the lecturer root, toggles renderers
    /// instead of SetActive so AnimAndImage stays alive for StartTask → BeginLecture.
    /// </summary>
    void SetLecturerAvatarVisible(bool visible)
    {
        GameObject root = lecturerRoot != null ? lecturerRoot : (anim != null ? anim.gameObject : null);
        if (root == null)
            return;

        if (root == gameObject)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
                renderers[i].enabled = visible;
            return;
        }

        root.SetActive(visible);
    }

    /// <summary>Freeze James pose until the task Start button begins the lecture.</summary>
    void SetLecturerTalkingEnabled(bool enabled)
    {
        if (anim == null && lecturerRoot != null)
            anim = lecturerRoot.GetComponentInChildren<Animator>(true);
        if (anim == null)
            anim = GetComponent<Animator>();
        if (anim == null)
            return;

        ClearJamesAnimBools();
        if (!enabled)
        {
            anim.applyRootMotion = false;
            anim.speed = 0f;
            // Hold first frame of default / idle-looking state without cycling talk clips.
            anim.Play("JamesTalking1", 0, 0f);
            anim.Update(0f);
        }
        else
        {
            anim.enabled = true;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            anim.speed = Mathf.Clamp(lecturerAnimSpeed, 0.25f, 1f);
        }
    }

    void CacheLecturerHomePose()
    {
        if (lecturerRoot == null)
            return;
        bool wasActive = lecturerRoot.activeSelf;
        if (!wasActive)
            lecturerRoot.SetActive(true);
        _lecturerHomePosition = lecturerRoot.transform.position;
        _lecturerHomeRotation = lecturerRoot.transform.rotation;
        _lecturerHomeCached = true;
        if (!wasActive)
            lecturerRoot.SetActive(false);
    }

    void EnsureLecturerVoiceSource()
    {
        // Use the AudioSource already assigned in the Inspector — never create/replace one at runtime.
        if (lecturerVoice != null)
            return;

        if (lecturerRoot != null)
            lecturerVoice = lecturerRoot.GetComponentInChildren<AudioSource>(true);

        if (lecturerVoice == null)
            lecturerVoice = GetComponent<AudioSource>();
    }

    void ConfigureLecturerVoice()
    {
        if (lecturerVoice == null)
            return;

        // Scene serialization often keeps old quiet values; enforce classroom-scale audibility.
        float vol = Mathf.Clamp(Mathf.Max(lecturerVoiceVolume, 5f), 0.5f, 8f);
        float minDist = Mathf.Max(lecturerMinDistance, 18f);
        float maxDist = Mathf.Max(lecturerMaxDistance, minDist + 10f);
        float blend = Mathf.Clamp01(lecturerSpatialBlend);

        lecturerVoice.playOnAwake = false;
        lecturerVoice.loop = false;
        lecturerVoice.mute = false;
        lecturerVoice.enabled = true;
        lecturerVoice.volume = vol;
        lecturerVoice.spatialBlend = blend;
        lecturerVoice.rolloffMode = AudioRolloffMode.Linear;
        lecturerVoice.minDistance = minDist;
        lecturerVoice.maxDistance = maxDist;
        lecturerVoice.dopplerLevel = 0f;
        lecturerVoice.spread = 90f;
        lecturerVoice.priority = 0;
        lecturerVoice.bypassEffects = true;
        lecturerVoice.bypassListenerEffects = true;
        lecturerVoice.bypassReverbZones = true;
#if UNITY_2017_1_OR_NEWER
        lecturerVoice.spatialize = blend >= 0.5f;
        lecturerVoice.spatializePostEffects = false;
#endif
    }

    /// <summary>Start slide + voice schedule (call from Sc2a/Sc2b StartTask).</summary>
    public void BeginLecture()
    {
        StopLecture();

        if (!_lecturerHomeCached)
            CacheLecturerHomePose();

        SetLecturerAvatarVisible(true);

        if (lecturerRoot != null)
        {
            if (!lecturerRoot.activeSelf)
                lecturerRoot.SetActive(true);
            CacheLecturerHomePose();
            lecturerRoot.transform.position = _lecturerHomePosition;
            lecturerRoot.transform.rotation = _lecturerHomeRotation;
        }
        else if (anim != null && !anim.gameObject.activeSelf)
        {
            anim.gameObject.SetActive(true);
        }

        if (anim == null && lecturerRoot != null)
            anim = lecturerRoot.GetComponentInChildren<Animator>(true);
        SetLecturerTalkingEnabled(true);
        if (anim != null)
        {
            anim.Rebind();
            anim.Update(0f);
        }

        EnsureLecturerVoiceSource();
        ConfigureLecturerVoice();

        IsLecturing = true;
        _lecturerAnimRoutine = StartCoroutine(LecturerTalkAnimationLoop());
        _lectureRoutine = StartCoroutine(RunLectureSchedule());
    }

    public void StopLecture()
    {
        if (_lectureRoutine != null)
        {
            StopCoroutine(_lectureRoutine);
            _lectureRoutine = null;
        }

        if (_lecturerAnimRoutine != null)
        {
            StopCoroutine(_lecturerAnimRoutine);
            _lecturerAnimRoutine = null;
        }

        if (lecturerVoice != null && lecturerVoice.isPlaying)
            lecturerVoice.Stop();

        SetLecturerTalkingEnabled(false);
        // Keep James visible in the hall; only stop talking/voice.
        SetLecturerAvatarVisible(true);

        ResetLecturerAnimBools();
        IsLecturing = false;
        CurrentSlideIndex = -1;
    }

    IEnumerator LecturerTalkAnimationLoop()
    {
        if (anim == null)
            yield break;

        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        anim.speed = Mathf.Clamp(lecturerAnimSpeed, 0.25f, 1f);
        ClearJamesAnimBools();

        float hold = Mathf.Max(1f, lecturerAnimHoldSeconds);
        int phase = 0;

        // One James talking clip at a time: 1 → 2 → 3 → 4 → 5 → 1 …
        while (IsLecturing && anim != null)
        {
            string state = JamesTalkStates[phase];
            ClearJamesAnimBools();
            anim.Play(state, 0, 0f);
            anim.Update(0f);

            float waited = 0f;
            while (IsLecturing && waited < hold)
            {
                AnimatorStateInfo info = anim.GetCurrentAnimatorStateInfo(0);
                if (!info.IsName(state) && !info.IsName("Base Layer." + state) && waited > 0.1f)
                    anim.Play(state, 0, 0f);

                waited += Time.deltaTime;
                yield return null;
            }

            phase = (phase + 1) % JamesTalkStates.Length;
        }

        if (anim != null)
            anim.speed = 1f;
        ClearJamesAnimBools();
    }

    void ClearJamesAnimBools()
    {
        if (anim == null)
            return;
        for (int i = 0; i < JamesAnimBools.Length; i++)
        {
            if (HasAnimParam(JamesAnimBools[i], AnimatorControllerParameterType.Bool))
                anim.SetBool(JamesAnimBools[i], false);
        }
    }

    void ResetLecturerAnimBools()
    {
        ClearJamesAnimBools();
    }

    static bool HasAnimParam(Animator animator, string name, AnimatorControllerParameterType type)
    {
        if (animator == null)
            return false;
        var ps = animator.parameters;
        for (int i = 0; i < ps.Length; i++)
        {
            if (ps[i].type == type && ps[i].name == name)
                return true;
        }
        return false;
    }

    bool HasAnimParam(string name, AnimatorControllerParameterType type)
    {
        return HasAnimParam(anim, name, type);
    }

    IEnumerator RunLectureSchedule()
    {
        int count = LessonSlideCount;
        for (int i = 0; i < count; i++)
        {
            CurrentSlideIndex = i;
            float sectionStart = Time.time;
            float targetDuration = GetSectionDuration(i);

            ShowSlide(i, playVoice: true);

            // Wait until the longer of: authored section length, or full voice clip.
            float clipLen = 0f;
            if (lessonVoiceClips != null && i < lessonVoiceClips.Length && lessonVoiceClips[i] != null)
                clipLen = lessonVoiceClips[i].length;

            float waitUntil = sectionStart + Mathf.Max(targetDuration, clipLen);
            while (Time.time < waitUntil)
                yield return null;
        }

        if (lecturerVoice != null && lecturerVoice.isPlaying)
            lecturerVoice.Stop();

        IsLecturing = false;
        CurrentSlideIndex = -1;
        _lectureRoutine = null;

        if (_lecturerAnimRoutine != null)
        {
            StopCoroutine(_lecturerAnimRoutine);
            _lecturerAnimRoutine = null;
        }
        ResetLecturerAnimBools();
    }

    float GetSectionDuration(int index)
    {
        if (sectionDurationsSeconds != null && index >= 0 && index < sectionDurationsSeconds.Length)
            return Mathf.Max(0.1f, sectionDurationsSeconds[index]);
        return 60f;
    }

    void ShowSlide(int index, bool playVoice)
    {
        Sprite sprite = null;
        if (lessonSprites != null && index >= 0 && index < lessonSprites.Length)
            sprite = lessonSprites[index];
        if (sprite == null && index == 0)
            sprite = img1;

        if (imgLession != null && sprite != null)
            imgLession.sprite = sprite;

        if (!playVoice)
            return;

        EnsureLecturerVoiceSource();
        ConfigureLecturerVoice();
        if (lecturerVoice == null)
            return;

        lecturerVoice.Stop();
        if (lessonVoiceClips == null || index < 0 || index >= lessonVoiceClips.Length)
            return;

        AudioClip clip = lessonVoiceClips[index];
        if (clip == null)
            return;

        lecturerVoice.clip = clip;
        ConfigureLecturerVoice();
        lecturerVoice.Play();
        // Keep gain after Play (some Unity versions reset volume when assigning clip).
        ConfigureLecturerVoice();
    }

    void ResetStudentAnimators()
    {
        SetMax(false, false);
        SetGeorge(false, false);
        SetJohn(false, false);
    }

    IEnumerator StudentAnimationLoop()
    {
        while (true)
        {
            yield return RunStudentSequenceOnce();
            ResetStudentAnimators();
        }
    }

    IEnumerator RunStudentSequenceOnce()
    {
        yield return new WaitForSeconds(10f);
        SetMax(false, true);

        yield return new WaitForSeconds(5f);
        SetGeorge(false, true);
        SetJohn(false, true);

        yield return new WaitForSeconds(20f);
        SetMax(true, false);

        yield return new WaitForSeconds(33f);
        yield return new WaitForSeconds(18f);

        yield return new WaitForSeconds(10f);
        SetMax(false, true);
        SetGeorge(true, false);

        yield return new WaitForSeconds(30f);
        SetJohn(true, false);

        yield return new WaitForSeconds(14f);
        SetJohn(false, true);
        SetMax(true, false);
        SetGeorge(false, true);

        yield return new WaitForSeconds(30f);
        SetJohn(true, false);
        SetMax(false, true);
        SetGeorge(true, false);

        yield return new WaitForSeconds(24f);
        TryPlayPlaneOnce();
        SetJohn(false, true);
        SetMax(true, false);
        SetGeorge(false, true);

        yield return new WaitForSeconds(12f);
        SetJohn(true, false);
        SetMax(false, true);
        SetGeorge(true, false);

        yield return new WaitForSeconds(7f);
        SetJohn(false, true);
        SetMax(true, false);
        SetGeorge(false, true);

        yield return new WaitForSeconds(12f);
        SetJohn(true, false);
        SetMax(false, true);
        SetGeorge(true, false);

        yield return new WaitForSeconds(12f);
        SetJohn(false, true);
        SetMax(true, false);
        SetGeorge(false, true);

        yield return new WaitForSeconds(10f);
        SetJohn(true, false);
        SetMax(false, true);
        SetGeorge(true, false);

        yield return new WaitForSeconds(5f);
        SetJohn(false, true);
        SetMax(true, false);
        SetGeorge(false, true);

        yield return new WaitForSeconds(8f);
        SetJohn(true, false);
        SetMax(false, true);
        SetGeorge(true, false);
    }

    void TryPlayPlaneOnce()
    {
        if (_planePlayed)
            return;
        _planePlayed = true;
        StartCoroutine(PlayPlaneSound());
    }

    void SetMax(bool anim1, bool anim2)
    {
        if (max == null)
            return;
        max.SetBool("MaxAnim1", anim1);
        max.SetBool("MaxAnim2", anim2);
    }

    void SetGeorge(bool anim1, bool anim2)
    {
        if (george == null)
            return;
        george.SetBool("GeorgeAnim1", anim1);
        george.SetBool("GeorgeAnim2", anim2);
    }

    void SetJohn(bool anim1, bool anim2)
    {
        if (john == null)
            return;
        john.SetBool("JohnAnim1", anim1);
        john.SetBool("JohnAnim2", anim2);
    }

    IEnumerator PlayPlaneSound()
    {
        if (paperPlane == null)
            yield break;

        paperPlane.SetActive(true);

        iTween.MoveTo(paperPlane, iTween.Hash(
            "position", new Vector3(12f, -6f, 3f),
            "time", 7f,
            "easetype", iTween.EaseType.easeInOutSine));

        yield return new WaitForSeconds(6f);

        if (paperPlaneSource != null && paperPlaneClip != null)
        {
            paperPlaneSource.clip = paperPlaneClip;
            paperPlaneSource.Play();
        }
    }
}
