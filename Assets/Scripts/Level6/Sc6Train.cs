using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using DG.Tweening;
using Valve.VR;
using UnityEngine.UI;
using PupilLabs;


public class Sc6Train : LevelScript
{
    int CorrectIndex = 3;//19 Gloucester Road This train should arrive more often.Maybe every once in 3 trains.So user does not wait much for right train
    public static string[] trainDestinations = {
        "5 Oxford Circus",
        "7 Great Portland Street",
        "11 Baker Street",
        "19 Gloucester Road",
        "29 Tottenham Court Road",
        "45 Bond Street",
        "58 Marble Arch",
        "73 Edgware Road",
        "95 Piccadilly Circus"};
    [SerializeField] Transform[] spawnPos = null;
    [SerializeField] GameObject trainPrefab = null;
    [SerializeField] AudioClip arriveAudio = null;
    [SerializeField] AudioClip leaveAudio = null;
    [SerializeField] float GeneratingDelay = 20f;
    [SerializeField] float WaitingTime = 10f;
    [SerializeField] int TimeLimit = 600; 
    [SerializeField] float InTrainDelay = 2f;
    [SerializeField] Animator TalkAnimator = null;
    [SerializeField] AudioSource TalkAudio = null;
    public int buttonClickCount = 0;
    Transform[] arrivePos;
    Transform[] targetPos;
    int roadIndex = 0;
    float startTime = 0;
    int trainState = 0; // bit state => 00 : two road empty, 01 10 : one road fill, 11 : two road fill
    int countWrongTrains = 0;
    bool personInTrain = false;

    public GameObject VRController;
    public GameObject Pointer;

    [Space]
    [Header("VR Trigger")]
    public SteamVR_Input_Sources handType;
    public SteamVR_Action_Boolean grabPinchAction = SteamVR_Input.GetAction<SteamVR_Action_Boolean>("GrabPinch");

    [Space]
    [Header("Eye Tracker")]
    public RecordingController recorder;
    public Text statusText;

    void Awake()
    {
        recorder.customPath = $"{Application.dataPath}/Data/{UserGroup}/Sc8TrainStation/{UserName}/Behavioural";
        bool connected = recorder.requestCtrl.IsConnected;
        Pointer.SetActive(true);
    }

    void OnDestroy()
    {
        recorder.StopRecording();
    }
    new void StartTask()
    {
        //PlayerFreeze = true;
        base.StartTask();
        EEG.Instance.Init("Sc8TrainStation");
        arrivePos = new Transform[2];
        targetPos = new Transform[2];
        for (int i = 0; i < 2; i++)
        {
            arrivePos[i] = spawnPos[i].GetChild(0);
            targetPos[i] = spawnPos[i].GetChild(1);
        }
        StartCoroutine(GernerateTrain());
        StartCoroutine(talk());
    }

    IEnumerator talk()
    {
        yield return new WaitForSeconds(3);
        TalkAnimator.enabled = true;
        TalkAudio.Play();
        StartCoroutine(PlayerRelease());
    }
    IEnumerator PlayerRelease()
    {
        yield return new WaitForSeconds(52);
        startTime = Time.time;
        StartCoroutine(LimitTimeCounter());
       // PlayerFreeze = false;
        VRController.GetComponent<VRController>().enabled = true;
        recorder.StartRecording();
    }
    private void Update()
    {
        StartBTN.onClick.AddListener(buttonIsClicked);

        if (!isStarted && btnIsClicked)
        {
            
            StartTask();
           
            TaskCanvas.GetComponent<Canvas>().enabled = false;
            TaskCanvas.GetComponent<GraphicRaycaster>().enabled = false;
            Pointer.SetActive(false);
        }
    }

    void buttonIsClicked()
    {
        btnIsClicked = true;
    }


    IEnumerator Post()
    {
        float time = Time.time - startTime;
        int marks = Mathf.Clamp((int)(20 - time * 2 / TimeLimit) - buttonClickCount, 0, 10);
        string accuracy = "High";
        if (marks < 7) accuracy = "Medium";
        if (marks < 5) accuracy = "Low";
        string dir = recorder != null ? recorder.customPath : $"{Application.dataPath}/Data/{UserGroup}/Sc8TrainStation/{UserName}/Behavioural";
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "summary.csv");
        if (!File.Exists(path))
            File.WriteAllText(path, "username,reaction_time_ms,accuracy,button_pressed,created_at\n", new UTF8Encoding(false));
        File.AppendAllText(path, $"{UserName},{(time * 1000).ToString("0.0")},{accuracy},{buttonClickCount},{System.DateTime.Now:yyyy-MM-dd HH:mm:ss}\n", new UTF8Encoding(false));
        recorder.StopRecording();
        StartCoroutine(SetLevel(SceneType.Sc6Questionnaire));
        NextScene();
        yield break;
    }
    IEnumerator GernerateTrain()
    {
        if (trainState < 3)
        {
            if (trainState == 0)
            {
                roadIndex ^= 1;
            }
            else
            {
                roadIndex = (trainState == 1) ? 1 : 0;
            }
            Transform train = Instantiate(trainPrefab, spawnPos[roadIndex]).transform;
            Train trainScript = train.GetComponent<Train>();
            trainScript.DestinationIndex = Random.Range(0, trainDestinations.Length);
            if(countWrongTrains > 4)
            {
                trainScript.DestinationIndex = CorrectIndex;
            }
            if(trainScript.DestinationIndex == CorrectIndex)
            {
                countWrongTrains = 0;
            }
            countWrongTrains++;
            trainScript.roadIndex = roadIndex;
            Sequence s = DOTween.Sequence();
            trainScript.sequence = s;
            s.Append(
                train.DOMove(arrivePos[roadIndex].position, 3, true).SetEase(Ease.OutQuad)
                .OnStart(() =>
                {
                    train.GetComponent<AudioSource>().PlayOneShot(arriveAudio);
                })
                .OnComplete(() =>
                {
                    trainState |= 1 << trainScript.roadIndex; // train state set
                    train.GetComponent<Animator>().SetBool("Open", true);
                }))
            .AppendInterval(WaitingTime)
            .AppendCallback(() =>
            {
                train.GetComponent<Animator>().SetBool("Open", false);
                train.GetComponent<AudioSource>().PlayOneShot(leaveAudio);
                train.GetComponent<Train>().DoorBlock.enabled = true;
            })
            .AppendInterval(2)
            .Append(
                train.DOMove(targetPos[roadIndex].position, 3, true).SetEase(Ease.InQuad)
                .OnComplete(() =>
                {
                    trainState &= ~(1 << trainScript.roadIndex);
                    //trainState ^= 1 << trainScript.roadIndex; // train state toggle
                    Destroy(train.gameObject);
                })
            );
        }
        
        yield return new WaitForSeconds(GeneratingDelay);
        StartCoroutine(GernerateTrain());
    }
    IEnumerator LimitTimeCounter()
    {
        yield return new WaitForSeconds(TimeLimit);
        StartCoroutine(Post());
    }
    public IEnumerator TrainTrigger(int index)
    {
        if (index < 0)
        {
            MessageManager.Instance.MessageOff();
            personInTrain = false;
        }
        else
        {
            personInTrain = true;
            yield return new WaitForSeconds(InTrainDelay);
            if (personInTrain)
            {
                if (index == CorrectIndex)
                {
                    StartCoroutine(Post());
                }
                else
                {
                    MessageManager.Instance.Messge("You are in the wrong train.");
                }
            }
        }
    }
}
