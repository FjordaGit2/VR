using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Valve.VR;
using UnityEngine.UI;

/// <summary>
/// Short practice before SC3bStreet. Pass from touchpad only (opposite side from car). No eye-tracker check.
/// </summary>
public class Sc3bPractice : MonoBehaviour
{
    public GameObject ThisGameObject;
    public Button StartPracticeBTNl;
    public GameObject buttonStartPractice;
    bool praticeButtonIsClicked = false;
    public Canvas PracticeCanvas;
    public Text CanvasText;
    public GameObject Hand;
    public Camera camera;

    [SerializeField] Transform[] SpawnPoses = null;
    [SerializeField] GameObject[] SpawnPrefabs = null;

    [Space]
    [Header("Lamp cue (same structure as SC3bStreet)")]
    public GameObject lamp;

    [Space]
    [Header("Practice length")]
    [Tooltip("Number of practice trials (default list is 5 left + 5 right).")]
    [SerializeField] int TotalCount = 10;

    [Space]
    [Header("Timing (ms) — matches SC3bStreet defaults")]
    [Min(0)] public int preTaskDelayMs = 1000;
    [Min(1)] public int lampOnDurationMs = 1000;
    [Min(0)] public int lampOffGapMs = 200;
    [Min(1)] public int carShowDurationMs = 1000;
    [Min(0)] public int interTrialIntervalMs = 1000;

    [Space]
    [Header("Car movement")]
    [Min(0f)] public float carSpeed = 50f;

    readonly List<int> mylist = new List<int>();

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

    void Start()
    {
        if (Pointer != null)
            Pointer.SetActive(true);
        if (lamp != null)
            lamp.SetActive(false);
        if (StartPracticeBTNl != null)
            StartPracticeBTNl.onClick.AddListener(buttonIsClicked);
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
            if (touchpadValue.x > 0)
                PressCorrect1++;
        }
        else if (SpawnPosIndex == 1)
        {
            if (touchpadValue.x < 0)
                PressCorrect2++;
        }

        if (PressCorrect1 >= 2 && PressCorrect2 >= 2)
            StartCoroutine(PracticeCompleted());
    }

    void BuildPracticeRoadList()
    {
        mylist.Clear();
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
        while (mylist.Count > TotalCount)
            mylist.RemoveAt(mylist.Count - 1);
        while (mylist.Count < TotalCount)
            mylist.Add(mylist.Count % 2);
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
            if (mylist.Count == 0)
                break;

            SetLampActive(true);
            yield return WaitMs(lampOnDurationMs);
            SetLampActive(false);
            yield return WaitMs(lampOffGapMs);

            SpawnPosIndex = mylist[Random.Range(0, mylist.Count)];
            mylist.Remove(SpawnPosIndex);

            _responseWindowActive = true;
            int carIndex = SpawnPrefabs != null && SpawnPrefabs.Length > 0
                ? Random.Range(0, SpawnPrefabs.Length)
                : 0;
            SpawnCar(SpawnPosIndex, carIndex);

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

    void SpawnCar(int roadSideIndex, int carPrefabIndex)
    {
        if (SpawnPrefabs == null || SpawnPoses == null || SpawnPrefabs.Length == 0 || SpawnPoses.Length == 0)
            return;
        if (carPrefabIndex < 0 || carPrefabIndex >= SpawnPrefabs.Length)
            carPrefabIndex = 0;
        if (roadSideIndex < 0 || roadSideIndex >= SpawnPoses.Length)
            roadSideIndex = 0;

        float showSec = carShowDurationMs * 0.001f;
        Instantiate(SpawnPrefabs[carPrefabIndex], SpawnPoses[roadSideIndex])
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
            CanvasText.text = "Please start again. Make sure you press the opposite correct direction in the rounded button of the controller depending where the car is shown.";
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
    }
}
