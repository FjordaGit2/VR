using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using Valve.VR;
using UnityEngine.UI;
using PupilLabs;
using UnityEngine.EventSystems;

public class Sc7Elevator : LevelScript {
    [SerializeField] GameObject phone = null;
   // [SerializeField] GameObject phoneButton = null;
    [SerializeField] float TextDelay = 3f;
    [SerializeField] float TimeLimit = 600;
    [SerializeField] float StartDelayTime = 10;
    [SerializeField] GameObject Interviewer = null;
    [SerializeField] AudioSource InterviewrAS = null;

    [SerializeField] AudioSource[] characters = null;


    private Animation DoorsAnim;
    public float DoorsAnimSpeed = 1;
    public float OneFloorTime = 1.5f;

    public float OpenDelay = 1;
    public float CloseDelay = 4;
    private bool isOpen = false;
    private string AnimName = "ElevatorDoorsAnim_open";
    private string InputFloor = "";
    private int TargetFloor;

    public int CurrentFloor;
    private int FloorCount;
    [HideInInspector]
    public int ElevatorFloor;
    public TextMesh TextInside;
    [SerializeField] Transform button0 = null;

    private MeshRenderer ElevatorGoBtn;
    private List<MeshRenderer> ElevatorNumericButtons = new List<MeshRenderer>();
    private List<Collision> ElevatorNumericButtonsCollider = new List<Collision>();
    private AudioSource SoundFX;
    private bool SpeedUp = false;
    private bool SlowDown = false;
    private static bool Moving = false;

    public AudioClip Bell;
    [Range(0, 1)]
    public float BellVolume = 1;
    public AudioClip DoorsOpen;
    [Range(0, 1)]
    public float DoorsOpenVolume = 1;
    public AudioClip DoorsClose;
    [Range(0, 1)]
    public float DoorsCloseVolume = 1;
    public AudioClip ElevatorMove;
    [Range(0, 1)]
    public float ElevatorMoveVolume = 1;
    public AudioClip ElevatorBtn;
    [Range(0, 1)]
    public float ElevatorBtnVolume = 1;
    public AudioClip ElevatorError;
    [Range(0, 1)]
    public float ElevatorErrorVolume = 1;

    private AudioSource BtnSoundFX;

    private float hd = 0.07f;
    private float wd = 0.055f;

    bool isRightPressed = false;

    private float startTime = 0f;
    private float reactionTime = 0;
    private int phonePressed = 0;
    
    [Space]
    [Header("VR")]
    //public GameObject VRController;
    public GameObject PointerGameObject;
    private bool phoneFirstShown = false;

    public SteamVR_Input_Sources handType;
    public SteamVR_Action_Boolean grabPinchAction = SteamVR_Input.GetAction<SteamVR_Action_Boolean>("GrabPinch");
    public SteamVR_Action_Boolean touchPadClick = SteamVR_Input.GetAction<SteamVR_Action_Boolean>("TouchpadClick");

    //public SphereCollider Dot;
    public Pointer pointer;

    [Space]
    [Header("Eye Tracker")]
    public RecordingController recorder;
    public Text statusText;

    void Awake() {

        recorder.customPath = $"{Application.dataPath}/Data/{UserGroup}/Sc9Elevator/{UserName}/Behavioural";

        Moving = false;
        BtnSoundFX = GetComponent<AudioSource>();
        SoundFX = new GameObject().AddComponent<AudioSource>();
        SoundFX.transform.parent = gameObject.transform;
        SoundFX.transform.position = new Vector3(gameObject.transform.position.x, gameObject.transform.position.y + 2.2f, gameObject.transform.position.z);
        SoundFX.gameObject.name = "SoundFX";
        SoundFX.playOnAwake = false;
        SoundFX.spatialBlend = 1;
        SoundFX.minDistance = 0.1f;
        SoundFX.maxDistance = 10;
        SoundFX.rolloffMode = AudioRolloffMode.Linear;
        SoundFX.priority = 256;
        DoorsAnim = gameObject.GetComponent<Animation>();
        AnimName = DoorsAnim.clip.name;
        ButtonInit(29);
    }
    void OnDestroy()
    {
        recorder.StopRecording();
    }


    new void StartTask()
    {
        base.StartTask();
        EEG.Instance.Init("Sc9Elevator");
        foreach (var a in characters)
        {
            a.enabled = true;
        }
        StartCoroutine(StartDelay(StartDelayTime));
    }
    
    IEnumerator StartDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        ShowPhone();
        startTime = Time.time;
    }

    public void ShowPhone()
    {
        phone.SetActive(true);
        // phoneButton.SetActive(false);
        //phonePressed++;
        StopCoroutine(HidePhone());
        StartCoroutine(HidePhone());
       
        
    }
    IEnumerator HidePhone()
    {
        yield return new WaitForSeconds(TextDelay);
        phone.SetActive(false);
        phoneFirstShown = true;
       // phoneButton.SetActive(true);
    }
    void ButtonInit(int _count) {
        if (_count > 0)
        {
            if (_count > 15)
            {
                Vector3 startPos = button0.localPosition - new Vector3(0, Mathf.FloorToInt(_count / 3f) * hd / 2f, 0);
                int remain = (_count + 1) % 3;
                button0.localPosition = (remain == 1) ? startPos : startPos - new Vector3(wd, 0, 0);
                for (int i = 1; i <= _count; i++)
                {
                    Transform newButton = Instantiate(button0, button0.parent);
                    newButton.localPosition = startPos + new Vector3(((3 + i - remain) % 3 - 1) * wd, Mathf.FloorToInt((i + remain) / 3f) * hd, 0);
                    newButton.name = i.ToString();
                    newButton.GetChild(1).GetChild(0).GetComponent<Text>().text = i.ToString();
                }
            }
            else if (_count > 6)
            {
                Vector3 startPos = button0.localPosition - new Vector3(0, Mathf.FloorToInt(_count / 2f) * hd / 2f, 0);
                int remain = (_count + 1) % 2;
                button0.localPosition = (remain == 1) ? startPos : startPos - new Vector3(wd, 0, 0);
                for (int i = 1; i <= _count; i++)
                {
                    Transform newButton = Instantiate(button0, button0.parent);
                    newButton.localPosition = startPos + new Vector3(((i + remain) % 2 == 1) ? wd : -wd, Mathf.FloorToInt((i + remain) / 2f) * hd, 0);
                    newButton.name = i.ToString();
                    newButton.GetChild(1).GetChild(0).GetComponent<Text>().text = i.ToString();
                }
            }
            else
            {
                Vector3 startPos = button0.localPosition - new Vector3(0, _count * hd / 2f, 0);
                button0.localPosition = startPos;
                for (int i = 1; i <= _count; i++)
                {
                    Transform newButton = Instantiate(button0, button0.parent);
                    newButton.localPosition = startPos + new Vector3(0, i * hd, 0);
                    newButton.name = i.ToString();
                    newButton.GetChild(1).GetChild(0).GetComponent<Text>().text = i.ToString();
                }
            }
        }
        ElevatorFloor = 1;
        TextInside.text = ElevatorFloor.ToString();
    }

    void Update() {

        StartBTN.onClick.AddListener(buttonIsClicked);

        if (!isStarted && btnIsClicked)
        {
            StartTask();

            recorder.StartRecording();
            TaskCanvas.GetComponent<Canvas>().enabled = false;
            //TaskCanvas.GetComponent<GraphicRaycaster>().enabled = false;
            //Pointer.SetActive(false);
        }
        if (phoneFirstShown && touchPadClick.GetStateDown(handType))
        {
            phone.SetActive(true);
            // phoneButton.SetActive(false);
            phonePressed++;
            StopCoroutine(HidePhone());
            StartCoroutine(HidePhone());
        }
       
        
        //if (isStarted && Time.time - startTime > TimeLimit && !Moving)
        //{
         //   reactionTime = TimeLimit;
           // StartCoroutine(Post(false));
        //}
        RaycastHit[] hits;
        //if (grabPinchAction.GetStateDown(handType)) 
        //{
            
            //Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            //hits = Physics.RaycastAll(ray, 3);
            //RaycastHit hit;
            Ray ray = new Ray(pointer.transform.position, pointer.transform.forward);

            hits = Physics.RaycastAll(ray, pointer.m_DefaultLength);
            //Physics.Raycast(ray, out hit, pointer.m_DefaultLength);



            for (int i = 0; i < hits.Length; i++)
            {

                RaycastHit hit = hits[i];

                if (grabPinchAction.GetStateDown(handType) && !Moving)
                {
                    
                    InputFloor = hit.transform.name;
                    hit.transform.GetComponent<MeshRenderer>().enabled = true;
                    ElevatorNumericButtons.Add(hit.transform.GetComponent<MeshRenderer>());
                    BtnSoundFX.clip = ElevatorBtn;
                    BtnSoundFX.volume = ElevatorBtnVolume;
                    BtnSoundFX.Play();
                    TargetFloor = int.Parse(InputFloor);
                    if(TargetFloor == 23 && !isRightPressed)
                    {
                        isRightPressed = true;
                        reactionTime = Time.time - startTime;
                    }
                    BtnSoundFX.clip = ElevatorBtn;
                    BtnSoundFX.volume = ElevatorBtnVolume;
                    BtnSoundFX.Play();
                    ElevatorFloor = TargetFloor;
                    FloorCount = CurrentFloor;
                    ElevatorGoBtn = hit.transform.GetComponent<MeshRenderer>();
                    if (CurrentFloor != ElevatorFloor)
                    {
                        StartCoroutine(ElevatorGO(1));
                        ElevatorGoBtn.enabled = true;
                        Moving = true;
                    }
                    else
                    {
                        StartCoroutine(Error(ElevatorGoBtn));
                    }
                    InputFloor = "";

                    if (TargetFloor != CurrentFloor)
                    {
                        StartCoroutine(DoorsClosing());
                    }
               // }
            }
        }
        if (SpeedUp)
        {
            if (SoundFX.volume < ElevatorMoveVolume)
            {
                SoundFX.volume += 0.9f * Time.deltaTime;
            }
            else
            {
                SpeedUp = false;
            }
            if (SoundFX.pitch < 1)
            {
                SoundFX.pitch += 0.9f * Time.deltaTime;
            }
        }
        if (SlowDown)
        {
            if (SoundFX.volume > 0)
            {
                SoundFX.volume -= 0.9f * Time.deltaTime;
            }
            else
            {
                SlowDown = false;
            }
            if (SoundFX.pitch > 0)
            {
                SoundFX.pitch -= 0.9f * Time.deltaTime;
            }
        }
       
    }


   
    void buttonIsClicked()
    {
        btnIsClicked = true;
    }
    IEnumerator Error(MeshRenderer btn)
    {
        btn.enabled = true;
        MeshRenderer r = btn;
        Color oringinColor = r.material.GetColor("_EmissionColor");
        r.material.SetColor("_EmissionColor", Color.red);
        r.enabled = true;
        InputFloor = "";
        BtnSoundFX.clip = ElevatorError;
        BtnSoundFX.volume = ElevatorErrorVolume;
        BtnSoundFX.Play();
        yield return new WaitForSeconds(2);
        r.material.SetColor("_EmissionColor", oringinColor);
        r.enabled = false;
    }
	IEnumerator ElevatorGO(float delay = 0){
        yield return new WaitForSeconds(delay);
        StartCoroutine(FloorsCounterInside());
		SoundFX.clip = ElevatorMove;
		SoundFX.loop = true;
		SoundFX.volume = 0;
		SoundFX.pitch = 0.5f;
		SpeedUp = true;	
		SoundFX.Play ();
	}
    
    IEnumerator SlowDownStart(float delay){
        yield return new WaitForSeconds(delay);
		SlowDown = true;
	}

	IEnumerator FloorsCounterInside(){
		for (;;) {
			TextInside.text = FloorCount.ToString();
			if(Mathf.Abs(TargetFloor - FloorCount) == 1){
                StartCoroutine(SlowDownStart(OneFloorTime / 2));
			}

			if(TargetFloor == FloorCount){
				yield break;
			}

			yield return new WaitForSeconds (OneFloorTime);
            SlowDown = false;

			if (CurrentFloor < TargetFloor) {
				FloorCount++;
			}

			if (CurrentFloor > TargetFloor) {
				FloorCount--;
			}

			if(FloorCount == TargetFloor){
                CurrentFloor = FloorCount;
                SoundFX.Stop();
                BellSoundPlay();
                StartCoroutine(ElvOpening(OpenDelay));
			}
		}
	}
    IEnumerator Post(bool isRight)
    {
        
        if (isRight)
        {
            //VRController.GetComponent<VRController>().enabled = true;
            InterviewrAS.Play();
            yield return new WaitForSeconds(10);
        }
        int marks = Mathf.Clamp((int)(20 - reactionTime * 2 / TimeLimit) - phonePressed, 0, 10);
        string accuracy = "High";
        if (marks < 7) accuracy = "Medium";
        if (marks < 5) accuracy = "Low";
        string dir = recorder != null ? recorder.customPath : $"{Application.dataPath}/Data/{UserGroup}/Sc9Elevator/{UserName}/Behavioural";
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "summary.csv");
        if (!File.Exists(path))
            File.WriteAllText(path, "username,reaction_time_ms,accuracy,phone_pressed,is_right_choice,created_at\n", new UTF8Encoding(false));
        File.AppendAllText(path, $"{UserName},{(reactionTime * 1000).ToString("0.0")},{accuracy},{phonePressed},{(isRight ? 1 : 0)},{System.DateTime.Now:yyyy-MM-dd HH:mm:ss}\n", new UTF8Encoding(false));

        recorder.StopRecording();
        StartCoroutine(SetLevel(SceneType.Sc7Questionnaire));
        NextScene();
    }

    void BellSoundPlay(){
		SoundFX.clip = Bell;
		SoundFX.loop = false;
		SoundFX.volume = BellVolume;
		SoundFX.pitch = 1;
		SoundFX.Play ();
        if (isRightPressed && Interviewer) Interviewer.SetActive(true);
    }

	void ButtonsReset(){
		foreach (MeshRenderer MR in ElevatorNumericButtons) {
			MR.enabled = false;
		}
		if(ElevatorGoBtn != null){
			ElevatorGoBtn.enabled = false;
		}
	}

	IEnumerator ElvOpening(float delay){
        yield return new WaitForSeconds(delay);
		DoorsAnim[AnimName].normalizedTime = 0;
		DoorsAnim[AnimName].speed = DoorsAnimSpeed;
		DoorsAnim.Play ();
		ButtonsReset ();
	}

	IEnumerator DoorsOpening(float delay = 0){
        yield return new WaitForSeconds(delay);
		TargetFloor = 0;
		DoorsAnim [AnimName].normalizedTime = 0;
		DoorsAnim [AnimName].speed = DoorsAnimSpeed;
		DoorsAnim.Play ();
		ButtonsReset ();
	}

	IEnumerator DoorsClosing(float delay = 0){
		if(isOpen){
            yield return new WaitForSeconds(delay);
			DoorsAnim [AnimName].normalizedTime = 1;
			DoorsAnim [AnimName].speed = -DoorsAnimSpeed;
			DoorsAnim.Play ();
			isOpen = false;
	    }
	}

    // animation event
    void DoorsClosingSoundPlay()
    {
        if (DoorsAnim[AnimName].speed < 0)
        {
            SoundFX.clip = DoorsClose;
            SoundFX.loop = false;
            SoundFX.volume = DoorsCloseVolume;
            SoundFX.pitch = 1;
            SoundFX.Play();
        }
    }
    // animation event
    void DoorsOpeningSoundPlay()
    {
        if (DoorsAnim[AnimName].speed > 0)
        {
            SoundFX.clip = DoorsOpen;
            SoundFX.volume = DoorsOpenVolume;
            SoundFX.pitch = 1;
            SoundFX.Play();
        }
    }
    // animation event
    void DoorsClosingTimer()
    {
        if (DoorsAnim[AnimName].speed > 0)
        {
            StartCoroutine(DoorsClosing(CloseDelay));
            isOpen = true;
            Moving = false;
        }
        if (isRightPressed)
        {
            StartCoroutine(Post(true));
        }
        if (DoorsAnim[AnimName].speed > 0)
        {
           // MessageManager.Instance.Messge("You are on the wrong floor.", 2);
        }
        if (Time.time - startTime > TimeLimit)
        {
            reactionTime = Time.time - startTime;
            StartCoroutine(Post(false));
        }
        if (isStarted && Time.time - startTime > TimeLimit && !Moving)
        {
           reactionTime = TimeLimit;
           StartCoroutine(Post(false));
        }
    }
}
