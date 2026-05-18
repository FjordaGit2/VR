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

    public static void BeginStudyAfterLogin()
    {
        _sequenceActive = true;
        _stepIndex = 0;
        LoadStep(_stepIndex);
    }

    public static void AdvanceToNextScene()
    {
        if (!_sequenceActive)
        {
            Debug.LogWarning("StudySceneFlow: sequence not started; using build-order NextScene fallback.");
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
            return;
        }

        _stepIndex++;
        if (_stepIndex >= GetMiddleScenes().Count)
        {
            _sequenceActive = false;
            SceneManager.LoadScene(SceneEnd);
            return;
        }

        LoadStep(_stepIndex);
    }

    static void LoadStep(int middleIndex)
    {
        IReadOnlyList<string> middle = GetMiddleScenes();
        if (middleIndex < 0 || middleIndex >= middle.Count)
        {
            Debug.LogError($"StudySceneFlow: invalid step {middleIndex}.");
            return;
        }

        string sceneName = middle[middleIndex];
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError(
                $"StudySceneFlow: scene '{sceneName}' is not in Build Settings. " +
                "Add it under File → Build Settings → Scenes In Build.");
            return;
        }

        Debug.Log($"StudySceneFlow: loading '{sceneName}' (order {GetOrderLabel()}, step {middleIndex + 1}/{middle.Count}).");
        SceneManager.LoadScene(sceneName);
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
