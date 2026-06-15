#if UNITY_EDITOR
using UnityEditor;

/// <summary>
/// Study CSV/eye-tracking files are written under Assets/Data during play mode.
/// Disable auto-refresh while playing to avoid import loops and editor freezes.
/// </summary>
[InitializeOnLoad]
static class StudyDataPlayModeGuard
{
    static StudyDataPlayModeGuard()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        switch (state)
        {
            case PlayModeStateChange.EnteredPlayMode:
                AssetDatabase.DisallowAutoRefresh();
                break;
            case PlayModeStateChange.ExitingPlayMode:
                AssetDatabase.AllowAutoRefresh();
                break;
        }
    }
}
#endif
