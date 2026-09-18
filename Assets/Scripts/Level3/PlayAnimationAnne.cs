using System.Collections;
using UnityEngine;

public class PlayAnimationAnne : MonoBehaviour
{
    public Animator anim;
    public GameObject Anne;

    [Tooltip("Target length for one full Anne sequence (~16 min matches Sc3 task).")]
    [Min(1f)] public float taskDurationSeconds = 960f;

    /// <summary>Original script length before scaling (seconds).</summary>
    const float BaseCycleDuration = 190f;

    float _timelineStart;
    float WaitScale => taskDurationSeconds / BaseCycleDuration;

    void Start()
    {
        if (anim == null && Anne != null)
            anim = Anne.GetComponent<Animator>();

        ResetBools();
        _timelineStart = Time.time;
        StartCoroutine(RunAnneTimeline());
    }

    IEnumerator RunAnneTimeline()
    {
        yield return PlayAnimPart1();
        if (IsTimelineComplete()) yield break;
        yield return PlayAnimPart2();
        if (IsTimelineComplete()) yield break;
        yield return PlayAnimPart3();
        if (IsTimelineComplete()) yield break;
        yield return PlayAnimPart4();

        yield return WaitUntilTimelineEnd();
    }

    bool IsTimelineComplete()
    {
        return Time.time - _timelineStart >= taskDurationSeconds;
    }

    IEnumerator WaitUntilTimelineEnd()
    {
        float remaining = taskDurationSeconds - (Time.time - _timelineStart);
        if (remaining > 0f)
            yield return new WaitForSeconds(remaining);
    }

    IEnumerator WaitScaled(float originalSeconds)
    {
        float remaining = taskDurationSeconds - (Time.time - _timelineStart);
        if (remaining <= 0f)
            yield break;

        float scaled = originalSeconds * WaitScale;
        if (scaled > remaining)
            scaled = remaining;

        if (scaled > 0f)
            yield return new WaitForSeconds(scaled);
    }

    void ResetBools()
    {
        if (anim == null)
            return;
        anim.SetBool("Animation1", false);
        anim.SetBool("Animation2", false);
        anim.SetBool("Animation3", false);
    }

    void SetTalking(int activePart)
    {
        if (anim == null)
            return;
        anim.SetBool("Animation1", activePart == 1);
        anim.SetBool("Animation2", activePart == 2);
        anim.SetBool("Animation3", activePart == 3);
    }

    IEnumerator PlayAnimPart1()
    {
        yield return WaitScaled(10f);
        SetTalking(2);

        yield return WaitScaled(5f);
        SetTalking(3);

        yield return WaitScaled(5f);
        SetTalking(1);
    }

    IEnumerator PlayAnimPart2()
    {
        yield return WaitScaled(15f);
        SetTalking(2);

        yield return WaitScaled(10f);
        SetTalking(3);

        yield return WaitScaled(10f);
        SetTalking(1);
    }

    IEnumerator PlayAnimPart3()
    {
        yield return WaitScaled(15f);
        SetTalking(2);

        yield return WaitScaled(25f);
        SetTalking(3);

        yield return WaitScaled(10f);
        SetTalking(1);
    }

    IEnumerator PlayAnimPart4()
    {
        yield return WaitScaled(30f);
        SetTalking(2);

        yield return WaitScaled(15f);
        SetTalking(3);

        yield return WaitScaled(15f);
        SetTalking(1);

        yield return WaitScaled(25f);
        SetTalking(2);
    }
}
