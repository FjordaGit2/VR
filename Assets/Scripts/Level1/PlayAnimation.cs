using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Julie head-turn + spoken comment schedule for Sc1LivingRoom.
/// Animator: Animation1 = look at TV (default), Animation2 = turn to player, Animation3 = turn back to TV.
/// Times are seconds from video / task start (paragraph-aligned). Assign 13 separate AudioClips in order.
/// </summary>
public class PlayAnimation : MonoBehaviour
{
    [Header("Animator")]
    public Animator anim;

    [Header("Julie voice (attach 13 clips here)")]
    [Tooltip("Plays Julie's spoken lines. If empty, one is added on this GameObject at runtime.")]
    [SerializeField] AudioSource voiceSource;

    [Tooltip("Exactly 13 clips, in paragraph order (Element 0 = first look, Element 12 = last).")]
    [SerializeField] AudioClip[] lookAtPlayerVoiceClips = new AudioClip[13];

    [Header("Timing")]
    [Tooltip("Seconds Julie looks at the player before turning back to the TV.")]
    [Min(0.1f)]
    public float lookAtPlayerDurationSeconds = 5f;

    [Tooltip("Delay after head-turn starts before playing the voice clip (lets the turn animation settle).")]
    [Min(0f)]
    public float speakDelayAfterHeadTurnSeconds = 0.8f;

    [Tooltip("Video time (seconds from task/video start) when Julie turns to the player.")]
    public float[] lookAtPlayerTimesSeconds =
    {
        35f,
        90f,
        140f,
        185f,
        225f,
        281f,
        318f,
        360f,
        405f,
        452f,
        481f,
        506f,
        551f,
    };

    /// <summary>Julie's spoken-line AudioSource (for session recording boost).</summary>
    public AudioSource VoiceAudioSource => voiceSource;

    /// <summary>True while a Julie voice clip is actively playing.</summary>
    public bool IsSpeaking { get; private set; }

    /// <summary>1-based speech index while speaking; 0 when not speaking.</summary>
    public int CurrentSpeechIndex { get; private set; }

    /// <summary>Fired when a voice clip starts (1-based speech index, clip name).</summary>
    public event Action<int, string> SpeechStarted;

    /// <summary>Fired when a voice clip stops (1-based speech index).</summary>
    public event Action<int> SpeechEnded;

    Coroutine _scheduleRoutine;

    void Awake()
    {
        if (voiceSource == null)
            voiceSource = GetComponent<AudioSource>();
        if (voiceSource == null)
            voiceSource = gameObject.AddComponent<AudioSource>();

        voiceSource.playOnAwake = false;
        voiceSource.spatialBlend = 1f;
        voiceSource.loop = false;
    }

    void Start()
    {
        ResetToLookAtTv();
    }

    /// <summary>Call when the Sc1 video/task starts so head turns and voice match paragraph times.</summary>
    public void BeginLookingSchedule()
    {
        StopLookingSchedule();
        ResetToLookAtTv();
        _scheduleRoutine = StartCoroutine(RunLookingSchedule());
    }

    public void StopLookingSchedule()
    {
        if (_scheduleRoutine != null)
        {
            StopCoroutine(_scheduleRoutine);
            _scheduleRoutine = null;
        }

        if (voiceSource != null && voiceSource.isPlaying)
            voiceSource.Stop();

        EndSpeakingIfNeeded();
        ResetToLookAtTv();
    }

    void ResetToLookAtTv()
    {
        if (anim == null)
            return;
        anim.SetBool("Animation2", false);
        anim.SetBool("Animation3", false);
    }

    IEnumerator RunLookingSchedule()
    {
        if (anim == null || lookAtPlayerTimesSeconds == null || lookAtPlayerTimesSeconds.Length == 0)
            yield break;

        float scheduleStart = Time.time;

        for (int i = 0; i < lookAtPlayerTimesSeconds.Length; i++)
        {
            float targetTime = lookAtPlayerTimesSeconds[i];
            float wait = targetTime - (Time.time - scheduleStart);
            if (wait > 0f)
                yield return new WaitForSeconds(wait);

            // Turn head to player/camera.
            anim.SetBool("Animation3", false);
            anim.SetBool("Animation2", true);

            if (speakDelayAfterHeadTurnSeconds > 0f)
                yield return new WaitForSeconds(speakDelayAfterHeadTurnSeconds);

            PlayVoiceClip(i);

            float remainingLook =
                lookAtPlayerDurationSeconds - speakDelayAfterHeadTurnSeconds;
            if (remainingLook > 0f)
                yield return new WaitForSeconds(remainingLook);

            if (voiceSource != null && voiceSource.isPlaying)
                voiceSource.Stop();

            EndSpeakingIfNeeded();

            // Turn head back to TV.
            anim.SetBool("Animation2", false);
            anim.SetBool("Animation3", true);
        }

        _scheduleRoutine = null;
    }

    void PlayVoiceClip(int index)
    {
        EndSpeakingIfNeeded();

        if (voiceSource == null || lookAtPlayerVoiceClips == null)
            return;
        if (index < 0 || index >= lookAtPlayerVoiceClips.Length)
            return;

        AudioClip clip = lookAtPlayerVoiceClips[index];
        if (clip == null)
            return;

        voiceSource.Stop();
        voiceSource.clip = clip;
        voiceSource.Play();

        CurrentSpeechIndex = index + 1;
        IsSpeaking = true;
        SpeechStarted?.Invoke(CurrentSpeechIndex, clip.name);
    }

    void EndSpeakingIfNeeded()
    {
        if (!IsSpeaking)
            return;

        int endedIndex = CurrentSpeechIndex;
        IsSpeaking = false;
        CurrentSpeechIndex = 0;
        SpeechEnded?.Invoke(endedIndex);
    }
}
