using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using Valve.VR;
using UnityEngine.UI;
using PupilLabs;


public class Sc5Street : LevelScript
{
   
    [SerializeField] float MaxLimitTime = 600f;
    [SerializeField] GameObject phone = null;
    //[SerializeField] GameObject mapCanvas = null;
    [SerializeField] GameObject mapPan = null;
    [SerializeField] GameObject missedCallPan = null;
    [SerializeField] GameObject callingPan = null;
    [SerializeField] GameObject messagePan = null;
    [SerializeField] AudioSource missedSound = null;
    [SerializeField] AudioSource callIncomeSound = null;
    [SerializeField] AudioSource messageSound = null;
    [SerializeField] float missedCallDelay = 3;
    [SerializeField] float messageDelay = 3;
    [SerializeField] float mapDelay = 4;

    public static Sc5Street Instance;
    
    public GameObject[] CarPrefabs = null;
    int mapOpenCount = 0;
    public float startTime = 0;
    bool isMapOpened = false;
    int marks = 10;
    int currentPointIndex = 0;
    public GameObject VRController;
    public GameObject Pointer;

    [Space]
    [Header("VR Trigger")]
    public SteamVR_Input_Sources handType;
    public SteamVR_Action_Boolean grabPinchAction = SteamVR_Input.GetAction<SteamVR_Action_Boolean>("GrabPinch");

    [Space]
    [Header("IncomingCall")]
    public GameObject IncomingCall;
    public GameObject Calling;
    [SerializeField] AudioSource IncomingCallAudio = null;
    [SerializeField] AudioSource CallingAudio = null;

    private bool callingPanBool = true;

    [Space]
    [Header("Eye Tracker")]
    public RecordingController recorder;
    public Text statusText;
    public Camera camera;

    void Awake()
    {
        Pointer.SetActive(true);
        Instance = this;
        recorder.customPath = $"{Application.dataPath}/Data/{UserGroup}/Sc7StreetPedestrian/{UserName}/Behavioural";
        camera.clearFlags = CameraClearFlags.Skybox;
    }

    void OnDestroy()
    {
        recorder.StopRecording();
    }

   
    new public void StartTask()
    {

        base.StartTask();
        EEG.Instance.Init("Sc7StreetPedestrian");
        recorder.StartRecording();
        VRController.GetComponent<VRController>().enabled = true;
        TaskCanvas.GetComponent<Canvas>().enabled = false;
        TaskCanvas.GetComponent<GraphicRaycaster>().enabled = false;
        Pointer.SetActive(false);
        StartCoroutine(LimitTimer());
    }

    void Update()
    {
        if (btnIsClicked && !isStarted)
        {
            StartTask();


        }

        if (isStarted && !missedCallPan.activeSelf && !callingPan.activeSelf && !messagePan.activeSelf && grabPinchAction.GetStateDown(handType))
        {
            MapOpen();
        }

        if (callingPan.activeSelf)
        {
            StartCall();
        }



    }

    void StartCall()
    {
         if (callingPanBool && grabPinchAction.GetStateDown(handType))
         {
             IncomingCallAudio.Stop();
             IncomingCall.SetActive(false);
             Calling.SetActive(true);
             CallingAudio.Play();
             callingPanBool = false;
             ReceiveCall();
         }
    }

  

   
    private void OnTriggerEnter(Collider other)
    {
        if(other.tag == "Player")
        {
            StartCoroutine(Post());
        }
    }
    private void OnEventTrigger(int index)
    {
        mapPan.SetActive(false);
        
        switch (index)
        {
            case 0:
                StartCoroutine(MissedCall());
                break;
            case 1:
                phone.SetActive(true);
                callingPan.SetActive(true);
                callIncomeSound.Play();
                break;
            case 2:
                StartCoroutine(Message());
                break;
        }
    }
    private void PathPass(int index)
    {
        if(index < 0)
        {
            marks += index;
        }
        else if(currentPointIndex > index)
        {
            marks += index - currentPointIndex;
        }
        else
        {
            currentPointIndex = index;
        }
    }
    public void MapOpen()
    {
        if (isMapOpened) return;
        isMapOpened = true;
        mapOpenCount++;

        phone.SetActive(true);
        mapPan.SetActive(true);
        StartCoroutine(MapClose());

       
       
        
    }
    public void ReceiveCall()
    {
        callingPanBool = false;
        StartCoroutine(AudioCalling());
    }
    IEnumerator AudioCalling() {
        yield return new WaitForSeconds(6.5f);
        callingPan.SetActive(false);
        phone.SetActive(false);

    }
    IEnumerator Post()
    {
        string accuracy = "High";
        if (marks < 8) accuracy = "Medium";
        if (marks < 5) accuracy = "Low";
        string dir = recorder != null ? recorder.customPath : $"{Application.dataPath}/Data/{UserGroup}/Sc7StreetPedestrian/{UserName}/Behavioural";
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "summary.csv");
        if (!File.Exists(path))
            File.WriteAllText(path, "username,reaction_time_ms,map_pressed,accuracy,created_at\n", new UTF8Encoding(false));
        string row = $"{UserName},{((Time.time - startTime) * 1000).ToString("0.0")},{mapOpenCount},{accuracy},{System.DateTime.Now:yyyy-MM-dd HH:mm:ss}\n";
        File.AppendAllText(path, row, new UTF8Encoding(false));
        recorder.StopRecording();
        StartCoroutine(SetLevel(SceneType.Sc5Questionnaire));
        NextScene();
        yield break;
    }
    IEnumerator MapClose()
    {
        yield return new WaitForSeconds(mapDelay);
        isMapOpened = false;
        phone.SetActive(false);
        mapPan.SetActive(false);
    }
    IEnumerator MissedCall()
    {
        missedSound.Play();
        phone.SetActive(true);
        missedCallPan.SetActive(true);
        yield return new WaitForSeconds(missedCallDelay);
        phone.SetActive(false);
        missedCallPan.SetActive(false);
    }
    IEnumerator Message()
    {
        messageSound.Play();
        phone.SetActive(true);
        messagePan.SetActive(true);
        yield return new WaitForSeconds(messageDelay);
        messagePan.SetActive(false);
        phone.SetActive(false);
    }
    public IEnumerator LimitTimer()
    {
        startTime = Time.time;
        yield return new WaitForSeconds(MaxLimitTime);
        marks = 0;
        StartCoroutine(Post());
    }
}
