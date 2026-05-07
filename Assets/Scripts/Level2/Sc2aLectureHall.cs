using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using TMPro;
using Valve.VR;
using UnityEngine.UI;
using PupilLabs;
using UnityEngine.SceneManagement;



public class Sc2aLectureHall : LevelScript
{
    [SerializeField] TextMeshPro text = null;
    [SerializeField] float delay = 1.15f;
    [SerializeField] bool isReverse = false;
    int count = 0;
    int currentNumber = 0;
    bool posted = false;
    float startTime = 0f;
    public Camera camera;
    List<int> mylist = new List<int>();
    int newNumber;

    [Space]
    [Header("VR Trigger")]
    public SteamVR_Input_Sources handType;
    public SteamVR_Action_Boolean grabPinchAction = SteamVR_Input.GetAction<SteamVR_Action_Boolean>("GrabPinch");
    public GameObject Pointer;

    [Space]
    [Header("Eye Tracker")]
    public RecordingController recorder;
    public Text statusText;



    void Awake()
    {
        
        camera.clearFlags = CameraClearFlags.Skybox;
        Pointer.SetActive(true);

        Scene scene = SceneManager.GetActiveScene();

        recorder.customPath = $"{Application.dataPath}/Data/{UserGroup}/Sc2LectureHall/{UserName}/Behavioural";

        bool connected = recorder.requestCtrl.IsConnected;
        
        mylist.Add(1);
        mylist.Add(2);
        mylist.Add(3);
        mylist.Add(4);
        mylist.Add(5);
        mylist.Add(6);
        mylist.Add(7);
        mylist.Add(8);
        mylist.Add(9);
        mylist.Add(1);
        mylist.Add(2);
        mylist.Add(3);
        mylist.Add(4);
        mylist.Add(5);
        mylist.Add(6);
        mylist.Add(7);
        mylist.Add(8);
        mylist.Add(9);
        mylist.Add(1);
        mylist.Add(2);
        mylist.Add(3);
        mylist.Add(4);
        mylist.Add(5);
        mylist.Add(6);
        mylist.Add(7);
        mylist.Add(8);
        mylist.Add(9);
        mylist.Add(1);
        mylist.Add(2);
        mylist.Add(3);
        mylist.Add(4);
        mylist.Add(5);
        mylist.Add(6);
        mylist.Add(7);
        mylist.Add(8);
        mylist.Add(9);
        mylist.Add(1);
        mylist.Add(2);
        mylist.Add(3);
        mylist.Add(4);
        mylist.Add(5);
        mylist.Add(6);
        mylist.Add(7);
        mylist.Add(8);
        mylist.Add(9);
        mylist.Add(1);
        mylist.Add(2);
        mylist.Add(3);
        mylist.Add(4);
        mylist.Add(5);
        mylist.Add(6);
        mylist.Add(7);
        mylist.Add(8);
        mylist.Add(9);
        mylist.Add(1);
        mylist.Add(2);
        mylist.Add(3);
        mylist.Add(4);
        mylist.Add(5);
        mylist.Add(6);
        mylist.Add(7);
        mylist.Add(8);
        mylist.Add(9);
        mylist.Add(1);
        mylist.Add(2);
        mylist.Add(3);
        mylist.Add(4);
        mylist.Add(5);
        mylist.Add(6);
        mylist.Add(7);
        mylist.Add(8);
        mylist.Add(9);
        mylist.Add(1);
        mylist.Add(2);
        mylist.Add(3);
        mylist.Add(4);
        mylist.Add(5);
        mylist.Add(6);
        mylist.Add(7);
        mylist.Add(8);
        mylist.Add(9);
        mylist.Add(1);
        mylist.Add(2);
        mylist.Add(3);
        mylist.Add(4);
        mylist.Add(5);
        mylist.Add(6);
        mylist.Add(7);
        mylist.Add(8);
        mylist.Add(9);
        mylist.Add(1);
        mylist.Add(2);
        mylist.Add(3);
        mylist.Add(4);
        mylist.Add(5);
        mylist.Add(6);
        mylist.Add(7);
        mylist.Add(8);
        mylist.Add(9);
        mylist.Add(1);
        mylist.Add(2);
        mylist.Add(3);
        mylist.Add(4);
        mylist.Add(5);
        mylist.Add(6);
        mylist.Add(7);
        mylist.Add(8);
        mylist.Add(9);
        mylist.Add(1);
        mylist.Add(2);
        mylist.Add(3);
        mylist.Add(4);
        mylist.Add(5);
        mylist.Add(6);
        mylist.Add(7);
        mylist.Add(8);
        mylist.Add(9);
        mylist.Add(1);
        mylist.Add(2);
        mylist.Add(3);
        mylist.Add(4);
        mylist.Add(5);
        mylist.Add(6);
        mylist.Add(7);
        mylist.Add(8);
        mylist.Add(9);
        mylist.Add(1);
        mylist.Add(2);
        mylist.Add(3);
        mylist.Add(4);
        mylist.Add(5);
        mylist.Add(6);
        mylist.Add(7);
        mylist.Add(8);
        mylist.Add(9);
        mylist.Add(1);
        mylist.Add(2);
        mylist.Add(3);
        mylist.Add(4);
        mylist.Add(5);
        mylist.Add(6);
        mylist.Add(7);
        mylist.Add(8);
        mylist.Add(9);
        mylist.Add(1);
        mylist.Add(2);
        mylist.Add(3);
        mylist.Add(4);
        mylist.Add(5);
        mylist.Add(6);
        mylist.Add(7);
        mylist.Add(8);
        mylist.Add(9);
        mylist.Add(1);
        mylist.Add(2);
        mylist.Add(3);
        mylist.Add(4);
        mylist.Add(5);
        mylist.Add(6);
        mylist.Add(7);
        mylist.Add(8);
        mylist.Add(9);
        mylist.Add(1);
        mylist.Add(2);
        mylist.Add(3);
        mylist.Add(4);
        mylist.Add(5);
        mylist.Add(6);
        mylist.Add(7);
        mylist.Add(8);
        mylist.Add(9);
        mylist.Add(1);
        mylist.Add(2);
        mylist.Add(3);
        mylist.Add(4);
        mylist.Add(5);
        mylist.Add(6);
        mylist.Add(7);
        mylist.Add(8);
        mylist.Add(9);
        mylist.Add(1);
        mylist.Add(2);
        mylist.Add(3);
        mylist.Add(4);
        mylist.Add(5);
        mylist.Add(6);
        mylist.Add(7);
        mylist.Add(8);
        mylist.Add(9);
        mylist.Add(1);
        mylist.Add(2);
        mylist.Add(3);
        mylist.Add(4);
        mylist.Add(5);
        mylist.Add(6);
        mylist.Add(7);
        mylist.Add(8);
        mylist.Add(9);
        mylist.Add(1);
        mylist.Add(2);
        mylist.Add(3);
        mylist.Add(4);
        mylist.Add(5);
        mylist.Add(6);
        mylist.Add(7);
        mylist.Add(8);
        mylist.Add(9);
        mylist.Add(1);
        mylist.Add(2);
        mylist.Add(3);
        mylist.Add(4);
        mylist.Add(5);
        mylist.Add(6);
        mylist.Add(7);
        mylist.Add(8);
        mylist.Add(9);
        mylist.Add(1);
        mylist.Add(2);
        mylist.Add(3);
        mylist.Add(4);
        mylist.Add(5);
        mylist.Add(6);
        mylist.Add(7);
        mylist.Add(8);
        mylist.Add(9);








    }

 

    void OnDestroy()
    {
        recorder.StopRecording();
        
    }
    void Update()
    {
        if (btnIsClicked && !isStarted)
        {
            StartTask();
            recorder.StartRecording();
            Pointer.SetActive(false);

            
        }

        if (!posted)
        {
            if (grabPinchAction.GetStateDown(handType))
            {
                StartCoroutine(Post(true));
            }
        }

        


    }

    new public void StartTask()
    {
        base.StartTask();
        StartCoroutine(ClearData("sc2_data"));
        StartCoroutine(ShowNumber(true));
        EEG.Instance.Init("Sc2LectureHall");

    }

    IEnumerator Post(bool pressed)
    {

        posted = true;
        string dir = recorder != null ? recorder.customPath : $"{Application.dataPath}/Data/{UserGroup}/Sc2LectureHall/{UserName}/Behavioural";
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "task_trials.csv");
        if (!File.Exists(path))
            File.WriteAllText(path, "username,digit,trigger_pressed,accuracy,reaction_time_ms,created_at\n", new UTF8Encoding(false));

        string accuracy = pressed ? ((currentNumber == 3) ? "Correct" : "Wrong") : "";
        string reactionMs = pressed ? ((Time.time - startTime) * 1000).ToString("0.0") : "";
        File.AppendAllText(path, $"{UserName},{currentNumber},{(pressed ? "YES" : "NO")},{accuracy},{reactionMs},{System.DateTime.Now:yyyy-MM-dd HH:mm:ss}\n", new UTF8Encoding(false));

        if (!pressed)
        {
            yield break;
        }
        yield break;
    }
    IEnumerator ShowNumber(bool _startDelay = false)
    {
        if (_startDelay)
        {
            yield return new WaitForSeconds(3);
        }

        posted = false;



        do
        {

            newNumber = mylist[Random.Range(0, mylist.Count)];

            if (newNumber != currentNumber)
            {
                currentNumber = newNumber;
                break;
            }

        } while (mylist.Count > 0);

       

        text.text = currentNumber.ToString();
        mylist.Remove(currentNumber);
        startTime = Time.time;
        yield return new WaitForSeconds(delay);
        count++;

        if (count < 225)
        {
            StartCoroutine(ShowNumber());
        }
        else
        {
            recorder.StopRecording();
            StartCoroutine(SetLevel(SceneType.Sc2aQuestionnaire));
            yield return new WaitForSeconds(2f);
            NextScene();
        }


    }

   
}

