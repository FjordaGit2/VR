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
