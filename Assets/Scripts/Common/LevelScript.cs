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

    /// <summary>Folder name under Assets/Data/{UserGroup}/ for Sc1 behavioural data (matches Loginmanager).</summary>
    public const string DataFolderSc1LivingRoom = "Sc1LivingRoom";
    /// <summary>Folder name under Assets/Data/{UserGroup}/ for Sc2a behavioural data.</summary>
    public const string DataFolderSc2LectureHall = "Sc2LectureHall";
    /// <summary>Folder name under Assets/Data/{UserGroup}/ for Sc2b behavioural data (inverted go/no-go vs Sc2a).</summary>
    public const string DataFolderSc2bLectureHall = "Sc2bLectureHall";
    /// <summary>Folder name under Assets/Data/{UserGroup}/ for Sc3a street car-detection task.</summary>
    public const string DataFolderSc3aStreet = "Sc3aStreet";

    /// <summary>Assets/Data/{userGroup}/{levelSubfolder}/{userName} — same layout as Loginmanager creates for Sc1.</summary>
    public static string GetDataPathForLevel(string levelSubfolder, string userGroup, string userName)
    {
        return $"{Application.dataPath}/Data/{userGroup}/{levelSubfolder}/{userName}";
    }

    /// <summary>Assets/Data/{UserGroup}/{levelSubfolder}/{UserName} after login.</summary>
    public static string GetDataPathForLevel(string levelSubfolder)
    {
        return GetDataPathForLevel(levelSubfolder, UserGroup, UserName);
    }

    /// <summary>Pupil + CSV session folder: .../{levelSubfolder}/{userName}/Behavioural</summary>
    public static string GetBehaviouralPath(string levelSubfolder, string userGroup, string userName)
    {
        return $"{GetDataPathForLevel(levelSubfolder, userGroup, userName)}/Behavioural";
    }

    public static string GetBehaviouralPath(string levelSubfolder)
    {
        return GetBehaviouralPath(levelSubfolder, UserGroup, UserName);
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
    public static IEnumerator SetLevel(SceneType sceneType)
    {
        try
        {
            string dataDir = $"{Application.dataPath}/Data";
            Directory.CreateDirectory(dataDir);
            string levelPath = $"{dataDir}/level_progress.csv";
            if (!File.Exists(levelPath))
                File.WriteAllText(levelPath, "username,group,level,created_at\n", new UTF8Encoding(false));
            File.AppendAllText(levelPath, $"{UserName},{UserGroup},{sceneType},{System.DateTime.Now:yyyy-MM-dd HH:mm:ss}\n", new UTF8Encoding(false));
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"SetLevel local save failed: {e.Message}");
        }

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
    public static void NextScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
    }
}
