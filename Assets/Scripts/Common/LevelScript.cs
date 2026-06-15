using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LevelScript : MonoBehaviour
{
    public static string UserName;
    public static string UserGroup;
    public static bool IsVR;

    public const string DataRootFolderName = "Data";
    public const string BehaviouralStreamFolder = "Behavioural";
    public const string EyeTrackingStreamFolder = "EyeTracking";
    public const string QuestionnaireStreamFolder = "Questionnaire";

    /// <summary>Task scene folder names (scene order does not affect paths).</summary>
    public const string DataFolderSc1LivingRoom = "Sc1LivingRoom";
    public const string DataFolderSc2LectureHall = "Sc2LectureHall";
    public const string DataFolderSc2bLectureHall = "Sc2bLectureHall";
    public const string DataFolderSc3aStreet = "Sc3aStreet";
    public const string DataFolderSc3bStreet = "Sc3bStreet";

    public const string DataFolderSc1Questionnaire = "Sc1Questionnaire";
    public const string DataFolderSc2aQuestionnaire = "Sc2aQuestionnaire";
    public const string DataFolderSc2bQuestionnaire = "Sc2bQuestionnaire";
    public const string DataFolderSc3aQuestionnaire = "Sc3aQuestionnaire";
    public const string DataFolderSc3bQuestionnaire = "Sc3bQuestionnaire";

    public const string QuestionnaireFileSc1 = "Sc1Questionnaire.csv";
    public const string QuestionnaireFileSc2a = "Sc2aQuestionnaire.csv";
    public const string QuestionnaireFileSc2b = "Sc2bQuestionnaire.csv";
    public const string QuestionnaireFileSc3a = "Sc3aQuestionnaire.csv";
    public const string QuestionnaireFileSc3b = "Sc3bQuestionnaire.csv";

    public static string GetDataRootPath()
    {
        return $"{Application.dataPath}/{DataRootFolderName}";
    }

    /// <summary>Assets/Data/{stream}/{sceneFolder}/{userGroup}/{userName}</summary>
    public static string GetParticipantStreamPath(string streamFolder, string sceneFolder, string userGroup, string userName)
    {
        return $"{GetDataRootPath()}/{streamFolder}/{sceneFolder}/{userGroup}/{userName}";
    }

    public static string GetBehaviouralPath(string sceneFolder, string userGroup, string userName)
    {
        return GetParticipantStreamPath(BehaviouralStreamFolder, sceneFolder, userGroup, userName);
    }

    public static string GetBehaviouralPath(string sceneFolder)
    {
        return GetBehaviouralPath(sceneFolder, UserGroup, UserName);
    }

    public static string GetEyeTrackingPath(string sceneFolder, string userGroup, string userName)
    {
        return GetParticipantStreamPath(EyeTrackingStreamFolder, sceneFolder, userGroup, userName);
    }

    public static string GetEyeTrackingPath(string sceneFolder)
    {
        return GetEyeTrackingPath(sceneFolder, UserGroup, UserName);
    }

    public static string GetQuestionnaireDirectory()
    {
        return $"{GetDataRootPath()}/{QuestionnaireStreamFolder}";
    }

    public static bool HasParticipantIdentity()
    {
        return !string.IsNullOrWhiteSpace(UserGroup) && !string.IsNullOrWhiteSpace(UserName);
    }

    public static string EscapeCsvField(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";
        if (value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0)
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }

    /// <summary>Maps Unity default/empty collider tags to <c>Else</c> in gaze CSV <c>hit_tag</c>.</summary>
    public static string FormatHitTagForCsv(int valid, string hitTag)
    {
        if (valid != 1)
            return hitTag ?? "";
        if (string.IsNullOrWhiteSpace(hitTag) || hitTag == "Untagged")
            return "Else";
        return hitTag;
    }

    /// <summary>Stub JSON: fill <c>pano_export</c> after you render one reference equirect in Unity (pose + image size) for offline gaze_world → pixel mapping.</summary>
    public static void WritePanoReferenceJson(string sessionDir, string unitySceneName)
    {
        try
        {
            Directory.CreateDirectory(sessionDir);
            string esc = unitySceneName ?? "";
            esc = esc.Replace("\\", "\\\\").Replace("\"", "\\\"");
            string json =
                "{\"schema\":\"pano_reference_v1\"," +
                "\"unity_scene_name\":\"" + esc + "\"," +
                "\"note\":\"Fill pano_export when you export one equirectangular reference from Unity (world pose + image dimensions). Map gaze_world_* to pixels offline.\"," +
                "\"pano_export\":{" +
                "\"position_xyz_m\":null,\"rotation_quat_wxyz\":null," +
                "\"equirect_width_px\":null,\"equirect_height_px\":null,\"image_filename\":null" +
                "}}";
            File.WriteAllText(Path.Combine(sessionDir, "pano_reference.json"), json, new UTF8Encoding(false));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"WritePanoReferenceJson failed: {e.Message}");
        }
    }

  //  public static bool PlayerFreeze = false;
    //[SerializeField] protected GameObject MainCamera = null;
    [SerializeField] protected GameObject VRCamera;
    //public GameObject StartButton;
    [SerializeField] protected Button StartBTN;
    public Canvas TaskCanvas = null;
    public bool isStarted = false;
    public bool btnIsClicked = false;
    bool TaskLevel = true;
    bool _studyTaskStartConsumed;
    protected bool sceneReferenceJsonWritten;

    /// <summary>Call from Update once per scene: handles start button without re-entering every frame.</summary>
    protected bool ConsumeStartButtonForTask()
    {
        if (_studyTaskStartConsumed || !btnIsClicked || isStarted)
            return false;
        _studyTaskStartConsumed = true;
        btnIsClicked = false;
        return true;
    }

    /// <summary>Writes scene ROI / pano JSON once per scene load (avoids repeated disk writes).</summary>
    protected void WriteSceneReferenceJsonOnce(string sessionDir, Action writeAction)
    {
        if (sceneReferenceJsonWritten || writeAction == null)
            return;
        sceneReferenceJsonWritten = true;
        try
        {
            Directory.CreateDirectory(sessionDir);
            writeAction();
        }
        catch (Exception e)
        {
            sceneReferenceJsonWritten = false;
            Debug.LogWarning($"WriteSceneReferenceJsonOnce failed: {e.Message}");
        }
    }
    void Start()
    {
        StartBTN.onClick.AddListener(buttonIsClicked);
        if (TaskLevel)
        {
            AudioListener.volume = 0;
            Time.timeScale = 0;
        }

        //if (VRCamera)
        //{
            //MainCamera.SetActive(!IsVR);
        //    VRCamera.SetActive(IsVR);
        //}
        //if(!IsVR && StartButton)
        //{
            //StartButton.SetActive(false);
        //}
    }
    void buttonIsClicked()
    {
        btnIsClicked = true;
        //Debug.Log("Button is pressed");
    }
    public void StartTask()
    {
        if (TaskCanvas) TaskCanvas.enabled = false;
        isStarted = true;
        AudioListener.volume = 1;
        Time.timeScale = 1;
    }
    [Obsolete("Legacy scene flow only; study scenes use StudySceneFlow and no longer write level_progress.csv.")]
    public static IEnumerator SetLevel(SceneType sceneType)
    {
        yield break;
    }
    public static IEnumerator ClearData(string table)
    {
        yield break;
    }

    public static void LoadScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }
    public static void LoadScene(int sceneIndex)
    {
        SceneManager.LoadScene(sceneIndex);
    }

    static bool _sceneAdvanceLocked;
    static bool _sceneAdvanceHookRegistered;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void RegisterSceneAdvanceUnlock()
    {
        if (_sceneAdvanceHookRegistered)
            return;
        _sceneAdvanceHookRegistered = true;
        SceneManager.sceneLoaded += (_, __) => _sceneAdvanceLocked = false;
        _sceneAdvanceLocked = false;
    }

    public static void NextScene()
    {
        if (_sceneAdvanceLocked)
        {
            Debug.LogWarning("LevelScript.NextScene ignored — scene advance already in progress.");
            return;
        }

        _sceneAdvanceLocked = true;

        if (StudySceneFlow.IsSequenceActive)
        {
            if (!StudySceneFlow.AdvanceToNextScene())
                _sceneAdvanceLocked = false;
            return;
        }

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
    }
}
