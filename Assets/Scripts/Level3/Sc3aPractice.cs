using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Valve.VR;
using UnityEngine.UI;

/// <summary>
/// Short practice before SC3aStreet. Pass from touchpad only (same side as car). No eye-tracker check.
/// </summary>
public class Sc3aPractice : MonoBehaviour
{
    public Button StartPracticeBTNl;
    public GameObject buttonStartPractice;
    bool praticeButtonIsClicked = false;
    public Canvas PracticeCanvas;
    public Text CanvasText;
    public GameObject Hand;
    public Camera camera;

    [SerializeField] Transform[] SpawnPoses = null;
    [SerializeField] GameObject[] SpawnPrefabs = null;
    [Tooltip("Police (or other) car spawned on the opposite road on ~50% of trials. Participants must ignore it and still respond to the target.")]
    [SerializeField] GameObject DistractorPrefab = null;

    [Space]
    [Header("Lamp cue (same structure as SC3aStreet)")]
    public GameObject lamp;

    [Space]
    [Header("Practice length")]
    [Tooltip("Number of practice trials (default list is 5 left + 5 right).")]
    [SerializeField] int TotalCount = 10;

    [Space]
    [Header("Timing (ms) — matches SC3aStreet defaults")]
    [Min(0)] public int preTaskDelayMs = 1000;
    [Min(1)] public int lampOnDurationMs = 1000;
    [Min(0)] public int lampOffGapMs = 200;
    [Min(1)] public int carShowDurationMs = 1200;
    [Min(0)] public int interTrialIntervalMs = 800;

    [Space]
    [Header("Car movement")]
    [Min(0f)] public float carSpeed = 35f;

    List<int> _practiceRoadSides;
    List<int> _practiceCarIndices;
    List<int> _practiceDistractorPresent;
    List<int> _practiceTravelDirections;

    int SpawnPosIndex;
    int count;
    int count2;

    bool _responseWindowActive;
    int PressCorrect1;
    int PressCorrect2;
    bool _practiceFinished;

    public GameObject Pointer;

    [Space]
    [Header("VR Touchpad")]
    public SteamVR_Input_Sources handType;
    public SteamVR_Action_Vector2 touchPadAction = SteamVR_Input.GetAction<SteamVR_Action_Vector2>("TouchpadLeftRight");
    public SteamVR_Action_Boolean touchPadClick = SteamVR_Input.GetAction<SteamVR_Action_Boolean>("TouchpadClick");

    [Space]
    [Header("Practice Completed")]
    public GameObject GazeTracker;
    public GameObject Recorder;
    public GameObject RightHand;

    [Space]
    [Header("PC test (no VR)")]
    [Tooltip("If enabled, practice starts automatically on Play without clicking the VR canvas Start button. Leave OFF for real participants.")]
    [SerializeField] bool autoStartOnPlayForPcTest = false;

    void Start()
    {
        if (Pointer != null)
            Pointer.SetActive(true);
        if (lamp != null)
            lamp.SetActive(false);
        if (StartPracticeBTNl != null)
            StartPracticeBTNl.onClick.AddListener(buttonIsClicked);
        if (autoStartOnPlayForPcTest)
            BeginPracticeForPcTest();
    }

    void BeginPracticeForPcTest()
    {
        // Street LevelScript sets timeScale=0 until its Start button; unfreeze for PC testing.
        Time.timeScale = 1f;
        AudioListener.volume = 1f;

        if (PracticeCanvas != null)
        {
            PracticeCanvas.enabled = false;
            PracticeCanvas.gameObject.SetActive(false);
        }
        if (buttonStartPractice != null)
            buttonStartPractice.SetActive(false);
        if (Pointer != null)
            Pointer.SetActive(false);

        praticeButtonIsClicked = true;
        if (count2 == 0)
        {
            count2 = 1;
            BuildPracticeRoadList();
            StartPractice();
        }
    }

    void Update()
    {
        if (_practiceFinished)
            return;

        if (praticeButtonIsClicked && count2 == 0)
        {
            count2 = 1;
            BuildPracticeRoadList();
            StartPractice();
            if (Pointer != null)
                Pointer.SetActive(false);
            if (buttonStartPractice != null)
                buttonStartPractice.SetActive(false);
        }

        if (!_responseWindowActive || touchPadAction == null || touchPadClick == null)
            return;

        Vector2 touchpadValue = touchPadAction.GetAxis(handType);
        bool touchpadClicked = touchPadClick.GetStateDown(handType);
        if (!touchpadClicked)
            return;

        if (SpawnPosIndex == 0)
        {
            if (touchpadValue.x < 0)
                PressCorrect1++;
        }
        else if (SpawnPosIndex == 1)
        {
            if (touchpadValue.x > 0)
                PressCorrect2++;
        }

        if (PressCorrect1 >= 2 && PressCorrect2 >= 2)
            StartCoroutine(PracticeCompleted());
    }

    void BuildPracticeRoadList()
    {
        int prefabCount = SpawnPrefabs != null ? SpawnPrefabs.Length : 1;
        var built = StudyTrialSequence.BuildPracticeSequences(
            TotalCount, prefabCount, StudyTrialSequence.SeedSaltSc3aPractice);
        _practiceRoadSides = built.RoadSides;
        _practiceCarIndices = built.CarIndices;
        _practiceDistractorPresent = built.DistractorPresent;
        _practiceTravelDirections = built.TravelDirections;
    }

    void buttonIsClicked()
    {
        praticeButtonIsClicked = true;
        if (PracticeCanvas != null)
            PracticeCanvas.enabled = false;
    }

    void StartPractice()
    {
        count = 0;
        PressCorrect1 = 0;
        PressCorrect2 = 0;
        StartCoroutine(RunPracticeTrials());
    }

    IEnumerator RunPracticeTrials()
    {
        if (preTaskDelayMs > 0)
            yield return WaitMs(preTaskDelayMs);

        while (count < TotalCount && !_practiceFinished)
        {
            if (_practiceRoadSides == null || count >= _practiceRoadSides.Count)
                break;

            SetLampActive(true);
            yield return WaitMs(lampOnDurationMs);
            SetLampActive(false);
            yield return WaitMs(lampOffGapMs);

            SpawnPosIndex = _practiceRoadSides[count];
            _responseWindowActive = true;
            int carIndex = _practiceCarIndices != null && count < _practiceCarIndices.Count
                ? _practiceCarIndices[count]
                : 0;
            int travelDirection = _practiceTravelDirections != null && count < _practiceTravelDirections.Count
                ? _practiceTravelDirections[count]
                : StudyTrialSequence.TravelForward;
            SpawnCar(SpawnPosIndex, carIndex, travelDirection);
            if (_practiceDistractorPresent != null
                && count < _practiceDistractorPresent.Count
                && _practiceDistractorPresent[count] == 1)
            {
                SpawnDistractorCar(SpawnPosIndex == 0 ? 1 : 0, travelDirection);
            }

            yield return WaitMs(carShowDurationMs);
            _responseWindowActive = false;

            count++;

            if (PressCorrect1 >= 2 && PressCorrect2 >= 2)
            {
                yield return StartCoroutine(PracticeCompleted());
                yield break;
            }

            yield return WaitMs(interTrialIntervalMs);
        }

        if (!_practiceFinished
            && ((PressCorrect1 <= 1 && PressCorrect2 <= 1)
                || (PressCorrect1 >= 1 && PressCorrect2 <= 1)
                || (PressCorrect1 <= 1 && PressCorrect2 >= 1)))
        {
            yield return StartAgain();
        }
    }

    void SpawnCar(int roadSideIndex, int carPrefabIndex, int travelDirection)
    {
        if (SpawnPrefabs == null || SpawnPoses == null || SpawnPrefabs.Length == 0 || SpawnPoses.Length == 0)
            return;
        if (carPrefabIndex < 0 || carPrefabIndex >= SpawnPrefabs.Length)
            carPrefabIndex = 0;
        if (SpawnPrefabs[carPrefabIndex] == null)
            return;

        int poseIndex = StudyTrialSequence.ResolveSpawnPoseIndex(
            roadSideIndex, travelDirection, SpawnPoses.Length);
        if (poseIndex < 0 || poseIndex >= SpawnPoses.Length || SpawnPoses[poseIndex] == null)
            return;

        float showSec = carShowDurationMs * 0.001f;
        Instantiate(SpawnPrefabs[carPrefabIndex], SpawnPoses[poseIndex])
            .AddComponent<AutoCar>()
            .Set(showSec, carSpeed);
    }

    void SpawnDistractorCar(int roadSideIndex, int travelDirection)
    {
        if (DistractorPrefab == null || SpawnPoses == null || SpawnPoses.Length < 2)
            return;

        int poseIndex = StudyTrialSequence.ResolveSpawnPoseIndex(
            roadSideIndex, travelDirection, SpawnPoses.Length);
        if (poseIndex < 0 || poseIndex >= SpawnPoses.Length || SpawnPoses[poseIndex] == null)
            return;

        float showSec = carShowDurationMs * 0.001f;
        Instantiate(DistractorPrefab, SpawnPoses[poseIndex])
            .AddComponent<AutoCar>()
            .Set(showSec, carSpeed);
    }

    void SetLampActive(bool on)
    {
        if (lamp != null)
            lamp.SetActive(on);
    }

    static IEnumerator WaitMs(int ms)
    {
        if (ms <= 0)
            yield break;
        yield return new WaitForSeconds(ms * 0.001f);
    }

    IEnumerator StartAgain()
    {
        _responseWindowActive = false;
        if (PracticeCanvas != null)
            PracticeCanvas.enabled = true;
        if (CanvasText != null)
            CanvasText.text = "Please start again. Make sure you press the corresponding correct direction in the rounded button of the controller depending where the target car is shown, and ignore the police car if it appears on the other road.";
        if (buttonStartPractice != null)
            buttonStartPractice.SetActive(true);
        praticeButtonIsClicked = false;
        if (Pointer != null)
            Pointer.SetActive(true);
        PressCorrect1 = 0;
        PressCorrect2 = 0;
        count2 = 0;
        count = 0;
        yield return WaitMs(1000);
    }

    IEnumerator PracticeCompleted()
    {
        if (_practiceFinished)
            yield break;
        _practiceFinished = true;
        _responseWindowActive = false;
        SetLampActive(false);

        SpawnPoses = null;
        SpawnPrefabs = null;
        if (PracticeCanvas != null)
            PracticeCanvas.enabled = true;
        if (CanvasText != null)
            CanvasText.text = "Practice completed. You will now start the main task.";
        yield return WaitMs(3000);

        this.gameObject.SetActive(false);
        GazeTracker.SetActive(true);
        Recorder.SetActive(true);
        RightHand.SetActive(false);
        camera.clearFlags = CameraClearFlags.SolidColor;
    }
}
