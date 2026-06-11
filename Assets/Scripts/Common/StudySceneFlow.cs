using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Counterbalanced scene order for the VR study. ID is always first and End always last.
/// Practice runs inside each task scene (not a separate scene).
/// "1 to 5": level blocks 1→5 (task then questionnaire per level).
/// "5 to 1": level blocks 5→1 with the same within-level order.
/// Scene names must match .unity filenames and be listed in Build Settings.
/// </summary>
public static class StudySceneFlow
{
    public const string SceneId = "ID";
    public const string SceneEnd = "End";

    /// <summary>Scene asset for Sc2a lecture hall.</summary>
    public const string SceneSc2aLectureHall = "Sc2LectureHall";

    public const string DropdownLabelForward = "1 to 5";
    public const string DropdownLabelReverse = "5 to 1";

    /// <summary>Master switch for experimenter debug skip UI (monitor only — not rendered in the VR headset).</summary>
    public static bool EnableDebugSkipButton = true;

    /// <summary>Experimenter monitor / editor controls — independent of whether the participant is in VR.</summary>
    public static bool ShouldShowPcDebugOverlay()
    {
        return EnableDebugSkipButton;
    }

    /// <summary>Per-level scenes after ID (practice is in-scene, not listed here).</summary>
    static readonly string[][] LevelBlocks =
    {
        new[] { "Sc1LivingRoom", "Sc1Questionnaire" },
        new[] { SceneSc2aLectureHall, "Sc2aQuestionnaire" },
        new[] { "Sc2BLectureHall", "Sc2bQuestionnaire" },
        new[] { "SC3AStreet", "Sc3aQuestionnaire" },
        new[] { "SC3BStreet", "Sc3bQuestionnaire" },
    };

    static readonly string[] MiddleScenesForward;
    static readonly string[] MiddleScenesReverse;

    static int _stepIndex = -1;
    static bool _sequenceActive;

    public static bool IsSequenceActive => _sequenceActive;
    public static int StepIndex => _stepIndex;
    public static StudySceneOrder Order { get; private set; } = StudySceneOrder.Forward_1_to_5;

    public enum StudySceneOrder
    {
        Forward_1_to_5,
        Reverse_5_to_1,
    }

    static StudySceneFlow()
    {
        MiddleScenesForward = FlattenBlocks(LevelBlocks);
        MiddleScenesReverse = FlattenBlocks(ReverseBlockOrder(LevelBlocks));
    }

    static bool _sceneSyncHookRegistered;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void RegisterSceneStepSync()
    {
        if (_sceneSyncHookRegistered)
            return;
        _sceneSyncHookRegistered = true;
        SceneManager.sceneLoaded += (_, __) => SyncStepIndexToActiveScene();
        SyncStepIndexToActiveScene();
    }

    static string[][] ReverseBlockOrder(string[][] blocks)
    {
        var reversed = new string[blocks.Length][];
        for (int i = 0; i < blocks.Length; i++)
            reversed[i] = blocks[blocks.Length - 1 - i];
        return reversed;
    }

    static string[] FlattenBlocks(string[][] blocks)
    {
        int count = 0;
        for (int i = 0; i < blocks.Length; i++)
            count += blocks[i].Length;

        var flat = new string[count];
        int index = 0;
        for (int b = 0; b < blocks.Length; b++)
        {
            for (int s = 0; s < blocks[b].Length; s++)
                flat[index++] = blocks[b][s];
        }
        return flat;
    }

    public static void SetOrderFromDropdownLabel(string label)
    {
        if (string.Equals(label?.Trim(), DropdownLabelReverse, StringComparison.OrdinalIgnoreCase))
            Order = StudySceneOrder.Reverse_5_to_1;
        else
            Order = StudySceneOrder.Forward_1_to_5;
    }

    /// <summary>0 = "1 to 5", 1 = "5 to 1" (matches Loginmanager dropdown option order).</summary>
    public static void SetOrderFromDropdownIndex(int index)
    {
        Order = index == 1 ? StudySceneOrder.Reverse_5_to_1 : StudySceneOrder.Forward_1_to_5;
    }

    public static void SetOrder(StudySceneOrder order)
    {
        Order = order;
    }

    public static string GetOrderLabel()
    {
        return Order == StudySceneOrder.Reverse_5_to_1 ? DropdownLabelReverse : DropdownLabelForward;
    }

    public static IReadOnlyList<string> GetMiddleScenes()
    {
        return Order == StudySceneOrder.Reverse_5_to_1
            ? MiddleScenesReverse
            : MiddleScenesForward;
    }

    /// <summary>After ID login: start first middle scene (not ID).</summary>
    public static void BeginStudyAfterLogin()
    {
        _sequenceActive = true;
        _stepIndex = 0;
        LoadStep(_stepIndex);
    }

    /// <summary>For PC testing: start counterbalanced flow at the currently open scene.</summary>
    public static void BeginSequenceAtCurrentScene(StudySceneOrder order)
    {
        Order = order;
        _sequenceActive = true;
        if (!SyncStepIndexToActiveScene())
        {
            Debug.LogWarning("StudySceneFlow: current scene is not in the study sequence; starting at step 0.");
            _stepIndex = 0;
        }
        Debug.Log($"StudySceneFlow: testing mode — order {GetOrderLabel()}, step {_stepIndex + 1}/{GetMiddleScenes().Count}, scene '{SceneManager.GetActiveScene().name}'.");
    }

    /// <summary>Keep step index aligned with the loaded scene (guards against duplicate NextScene calls).</summary>
    public static bool SyncStepIndexToActiveScene()
    {
        if (!_sequenceActive)
            return false;

        string active = SceneManager.GetActiveScene().name;
        if (active == SceneId || active == SceneEnd)
            return false;

        IReadOnlyList<string> middle = GetMiddleScenes();
        for (int i = 0; i < middle.Count; i++)
        {
            if (string.Equals(middle[i], active, StringComparison.Ordinal))
            {
                _stepIndex = i;
                return true;
            }
        }

        Debug.LogWarning($"StudySceneFlow: scene '{active}' is not in order '{GetOrderLabel()}'.");
        return false;
    }

    public static string GetNextSceneNamePreview()
    {
        if (!_sequenceActive)
            return "(sequence inactive — build index + 1)";

        int next = _stepIndex + 1;
        IReadOnlyList<string> middle = GetMiddleScenes();
        if (next >= middle.Count)
            return SceneEnd;
        return middle[next];
    }

    public static string GetDebugStatusLine()
    {
        string active = SceneManager.GetActiveScene().name;
        if (!_sequenceActive)
            return $"Flow: OFF | Scene: {active} | Next: build+1";

        IReadOnlyList<string> middle = GetMiddleScenes();
        int stepNum = _stepIndex >= 0 ? _stepIndex + 1 : 0;
        return $"Order: {GetOrderLabel()} | Step {stepNum}/{middle.Count} | {active} | Next: {GetNextSceneNamePreview()}";
    }

    /// <returns>False if the next scene could not be loaded (advance lock should be released).</returns>
    public static bool AdvanceToNextScene()
    {
        if (!_sequenceActive)
        {
            Debug.LogWarning("StudySceneFlow: sequence not started; using build-order NextScene fallback.");
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
            return true;
        }

        SyncStepIndexToActiveScene();

        _stepIndex++;
        if (_stepIndex >= GetMiddleScenes().Count)
        {
            _sequenceActive = false;
            Debug.Log($"StudySceneFlow: study complete — loading '{SceneEnd}'.");
            SceneManager.LoadScene(SceneEnd);
            return true;
        }

        return LoadStep(_stepIndex);
    }

    static bool LoadStep(int middleIndex)
    {
        IReadOnlyList<string> middle = GetMiddleScenes();
        if (middleIndex < 0 || middleIndex >= middle.Count)
        {
            Debug.LogError($"StudySceneFlow: invalid step {middleIndex}.");
            return false;
        }

        string sceneName = middle[middleIndex];
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError(
                $"StudySceneFlow: scene '{sceneName}' is not in Build Settings. " +
                "Add it under File → Build Settings → Scenes In Build.");
            return false;
        }

        Debug.Log($"StudySceneFlow: loading '{sceneName}' (order {GetOrderLabel()}, step {middleIndex + 1}/{middle.Count}). Next after that: {GetNextSceneNamePreview()}");
        SceneManager.LoadScene(sceneName);
        return true;
    }

    public static string GetCurrentSceneNameForLog()
    {
        if (!_sequenceActive || _stepIndex < 0)
            return SceneManager.GetActiveScene().name;
        IReadOnlyList<string> middle = GetMiddleScenes();
        if (_stepIndex < middle.Count)
            return middle[_stepIndex];
        return SceneEnd;
    }
}
