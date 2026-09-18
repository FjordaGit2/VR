using System.Collections;
using UnityEngine;

/// <summary>
/// Worker street ambience for ~16 min Sc3 task. Handles hide/show, phone loops, and repeated turn/hold/walk cycles.
/// Replaces the old split with EnableAndMoveWorker (activation is done here).
/// </summary>
public class DisableEnableMoveWorker : MonoBehaviour
{
    [Header("References")]
    public GameObject worker;
    public Animator anim;

    [Header("Overall (~960 s = 16 min with defaults below)")]
    [Tooltip("Total timeline length; any remaining time is spent in the final turn pose.")]
    [Min(1f)] public float taskDurationSeconds = 960f;

    [Header("Hidden (increased from original 90 s)")]
    [Min(0f)] public float workerHiddenSeconds = 180f;

    [Header("Phone (WorkerTalkingOnPhone)")]
    [Tooltip("Seconds after show for Animator stand/point before scripted phone loops.")]
    [Min(0f)] public float autoIntroAfterShowSeconds = 15f;
    [Min(1)] public int phoneLoopCount = 4;
    [Tooltip("Hold time per phone loop (animation restarted each loop).")]
    [Min(0.1f)] public float phoneLoopSeconds = 42f;

    [Header("Turn / hold / walk cycle (same per-step times as original; repeated")]
    [Min(1)] public int moveTurnCycleCount = 4;
    [Tooltip("Was 35 s — increased to spread activity across the task.")]
    [Min(0f)] public float holdAnim3Seconds = 100f;
    [Min(0f)] public float walkDurationSeconds = 36f;
    [Min(0f)] public float anim2LeadSeconds = 1f;
    [Min(0f)] public float anim3TransitionSeconds = 2f;
    [Min(0f)] public float anim3RotateSeconds = 2f;
    [Min(0f)] public float anim5TailSeconds = 8f;

    const string PhoneStateName = "WorkerTalkingOnPhone";

    float _timelineStart;

    void Start()
    {
        if (anim == null && worker != null)
            anim = worker.GetComponent<Animator>();

        if (worker != null)
            worker.SetActive(false);

        _timelineStart = Time.time;
        StartCoroutine(RunWorkerTimeline());
    }

    IEnumerator RunWorkerTimeline()
    {
        yield return WaitUntilTimeline(workerHiddenSeconds);

        if (worker != null)
            worker.SetActive(true);

        ResetAnimatorBools();
        yield return WaitUntilTimeline(workerHiddenSeconds + autoIntroAfterShowSeconds);

        for (int i = 0; i < phoneLoopCount; i++)
        {
            if (anim != null)
                anim.Play(PhoneStateName, 0, 0f);
            yield return new WaitForSeconds(phoneLoopSeconds);
        }

        for (int cycle = 0; cycle < moveTurnCycleCount; cycle++)
            yield return RunMoveTurnCycle(cycle == 0);

        yield return WaitUntilTimeline(taskDurationSeconds);
    }

    IEnumerator RunMoveTurnCycle(bool firstCycle)
    {
        SetAnimatorBool("Animation2", true);
        yield return new WaitForSeconds(anim2LeadSeconds);

        if (firstCycle && worker != null)
        {
            Vector3 pos = transform.localPosition;
            pos.y = -5.5f;
            worker.transform.localPosition = pos;
        }

        yield return new WaitForSeconds(anim3TransitionSeconds);

        SetAnimatorBool("Animation2", false);
        SetAnimatorBool("Animation3", true);

        yield return new WaitForSeconds(anim3RotateSeconds);

        if (firstCycle && worker != null)
        {
            Vector3 rot = transform.localEulerAngles;
            rot.y = 270f;
            worker.transform.localRotation = Quaternion.Euler(rot);
        }

        yield return new WaitForSeconds(holdAnim3Seconds);

        SetAnimatorBool("Animation3", false);
        SetAnimatorBool("Animation4", true);

        yield return new WaitForSeconds(walkDurationSeconds);

        SetAnimatorBool("Animation4", false);
        SetAnimatorBool("Animation5", true);

        yield return new WaitForSeconds(anim5TailSeconds);

        SetAnimatorBool("Animation5", false);
    }

    IEnumerator WaitUntilTimeline(float targetElapsedSinceStart)
    {
        float wait = _timelineStart + targetElapsedSinceStart - Time.time;
        if (wait > 0f)
            yield return new WaitForSeconds(wait);
    }

    void ResetAnimatorBools()
    {
        SetAnimatorBool("Animation1", false);
        SetAnimatorBool("Animation2", false);
        SetAnimatorBool("Animation3", false);
        SetAnimatorBool("Animation4", false);
        SetAnimatorBool("Animation5", false);
    }

    void SetAnimatorBool(string name, bool value)
    {
        if (anim != null)
            anim.SetBool(name, value);
    }
}
