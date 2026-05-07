using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using Valve.VR;
using UnityEngine.UI;
using PupilLabs;
using UnityEngine.SceneManagement;

public class SC3aStreet : LevelScript
{
    [SerializeField] Transform[] SpawnPoses = null;
    [SerializeField] GameObject[] SpawnPrefabs = null;
    [SerializeField] float CarShowTime = 1.15f;
    [SerializeField] float CarSpeed = 50f;
    [SerializeField] float Delay = 1.15f;
    [SerializeField] int TotalCount = 50;
    List<int> mylist = new List<int>();

    int SpawnPosIndex;
    int count;
    float startTime;
    bool isPressed;

    public GameObject Pointer;

    [Space]
    [Header("VR Touchpad")]
    public SteamVR_Input_Sources handType;
    public SteamVR_Action_Vector2 touchPadAction = SteamVR_Input.GetAction<SteamVR_Action_Vector2>("TouchpadLeftRight");
    public SteamVR_Action_Boolean touchPadClick = SteamVR_Input.GetAction<SteamVR_Action_Boolean>("TouchpadClick");

    [Space]
    [Header("Eye Tracker")]
    public RecordingController recorder;
    public Text statusText;
    public Camera camera;
    public GazeVisualizer gazeVisualizer;
    public GazeData gazeData;
    public Transform gazeOriginCamera;
    public GazeController gazeController;



   

    void Awake()
    {
        Pointer.SetActive(true);

        Scene scene = SceneManager.GetActiveScene();

        recorder.customPath = $"{Application.dataPath}/Data/{UserGroup}/Sc4Street/{UserName}/Behavioural";

        bool connected = recorder.requestCtrl.IsConnected;

        camera.clearFlags = CameraClearFlags.Skybox;

        mylist.Add(0);
        mylist.Add(1);
        mylist.Add(0);
        mylist.Add(1);
        mylist.Add(0);
        mylist.Add(1);
        mylist.Add(0);
        mylist.Add(1);
        mylist.Add(0);
        mylist.Add(1);
        mylist.Add(0);
        mylist.Add(1);
        mylist.Add(0);
        mylist.Add(1);
        mylist.Add(0);
        mylist.Add(1);
        mylist.Add(0);
        mylist.Add(1);
        mylist.Add(0);
        mylist.Add(1);
        mylist.Add(0);
        mylist.Add(1);
        mylist.Add(0);
        mylist.Add(1);
        mylist.Add(0);
        mylist.Add(1);
        mylist.Add(0);
        mylist.Add(1);
        mylist.Add(0);
        mylist.Add(1);
        mylist.Add(0);
        mylist.Add(1);
        mylist.Add(0);
        mylist.Add(1);
        mylist.Add(0);
        mylist.Add(1);
        mylist.Add(0);
        mylist.Add(1);
        mylist.Add(0);
        mylist.Add(1);
        mylist.Add(0);
        mylist.Add(1);
        mylist.Add(0);
        mylist.Add(1);
        mylist.Add(0);
        mylist.Add(1);
        mylist.Add(0);
        mylist.Add(1);
        mylist.Add(0);
        mylist.Add(1);
        

    }

    private void OnEnable()
    {
        if (gazeController)
        {
            gazeController.OnReceive3dGaze += OnReceive;
        }
    }
    private void OnReceive(GazeData obj)
    {
        gazeData = obj;
    }
    void OnDestroy()
    {
        recorder.StopRecording();
    }

    new public void StartTask()
    {
        base.StartTask();
        StartCoroutine(ClearData("sc3a_data"));
        StartCoroutine(ShowCar());

        EEG.Instance.Init("Sc4Street");

    }
    private void Update()
    {
        StartBTN.onClick.AddListener(buttonIsClicked);

        if (!isStarted && btnIsClicked)
        {

            StartTask();
            recorder.StartRecording(); 
            Pointer.SetActive(false);

            

        }

        Vector2 touchpadValue = touchPadAction.GetAxis(handType);
        bool touchpadClicked = touchPadClick.GetStateDown(handType);

        if (!isPressed)
        {

            if (touchpadValue.x < 0 && touchpadClicked)
            {
                //Debug.Log("Pressed Left");
                StartCoroutine(Post(true));
            }
            if (touchpadValue.x > 0 && touchpadClicked)
            {
                //Debug.Log("Pressed Right");
                StartCoroutine(Post(false));
            }

            
        }

        
    }

    void buttonIsClicked()
    {
        btnIsClicked = true;
    }

    IEnumerator Post(bool IsLeft)
    {
        isPressed = true;
        string looked = "";
        if (gazeData != null)
        {
            Vector3 origin = gazeOriginCamera.position;
            Vector3 direction = gazeOriginCamera.TransformDirection(gazeData.GazeDirection);

            if (Physics.SphereCast(origin, 0.05f, direction, out RaycastHit hit, Mathf.Infinity))
            {
                if (hit.collider.CompareTag("Left"))
                {
                    looked = "Left";
                }
                else if (hit.collider.CompareTag("Right"))
                {
                    looked = "Right";
                }
                else
                {
                    looked = "Else";
                }

            }
        }

        string dir = recorder != null ? recorder.customPath : $"{Application.dataPath}/Data/{UserGroup}/Sc4Street/{UserName}/Behavioural";
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "task_trials.csv");
        if (!File.Exists(path))
            File.WriteAllText(path, "username,car_shown,arrow_pressed,accuracy,reaction_time_ms,looked,created_at\n", new UTF8Encoding(false));
        string accuracy = (SpawnPosIndex == 0 == IsLeft) ? "Correct" : "Wrong";
        string carShown = (SpawnPosIndex == 0) ? "Left" : "Right";
        string arrowPressed = IsLeft ? "Left" : "Right";
        string reaction = ((Time.time - startTime) * 1000).ToString("0.0");
        File.AppendAllText(path, $"{UserName},{carShown},{arrowPressed},{accuracy},{reaction},{looked},{System.DateTime.Now:yyyy-MM-dd HH:mm:ss}\n", new UTF8Encoding(false));
        yield break;
    }
    IEnumerator ShowCar()
    {
        isPressed = false;
        yield return new WaitForSeconds(2);

        SpawnPosIndex = mylist[Random.Range(0, mylist.Count)];
        int _carIndex = Random.Range(0, SpawnPrefabs.Length);
        Instantiate(SpawnPrefabs[_carIndex], SpawnPoses[SpawnPosIndex]).AddComponent<AutoCar>().Set(CarShowTime, CarSpeed);
        mylist.Remove(SpawnPosIndex);
        startTime = Time.time;
        yield return new WaitForSeconds(CarShowTime + Delay);
        count++;
        if (count < TotalCount)
        {
            StartCoroutine(ShowCar());
        }
        else
        {
            recorder.StopRecording();
            StartCoroutine(SetLevel(SceneType.Sc3aQuestionnaire));
            yield return new WaitForSeconds(2f);
            NextScene();
        }
    }
}