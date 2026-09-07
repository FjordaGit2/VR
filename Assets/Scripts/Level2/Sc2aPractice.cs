using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Valve.VR;
using UnityEngine.UI;

public class Sc2aPractice : MonoBehaviour
{
    [SerializeField] TextMeshPro text = null;

    [Space]
    [Header("Trial timing (same as Sc2aLectureHall)")]
    [Min(1)] public int stimulusDurationMs = 100;
    [Min(0)] public int postStimulusBlankMs = 1100;
    int count = 0;
    int currentNumber = 0;
    int countPress = 0;
    public Button StartPracticeBTNl;
    bool praticeButtonIsClicked = false;
    public Canvas PracticeCanvas;
    public Text CanvasText;
    public GameObject Hand;
    public GameObject buttonStartPractice;
    public Camera camera;
    int count2 = 0;
    List<int> mylist = new List<int>();
    int newNumber;

    [Space]
    [Header("VR Trigger")]
    public SteamVR_Input_Sources handType;
    public SteamVR_Action_Boolean grabPinchAction = SteamVR_Input.GetAction<SteamVR_Action_Boolean>("GrabPinch");
    public GameObject Pointer;

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
        if (StartPracticeBTNl != null)
            StartPracticeBTNl.onClick.AddListener(buttonIsClicked);
        if (autoStartOnPlayForPcTest)
            BeginPracticeForPcTest();
    }

    void BeginPracticeForPcTest()
    {
        // Lecture LevelScript sets timeScale=0 until its Start button; unfreeze for PC testing.
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
            BuildPracticeDigitList();
            StartPractice();
        }
    }

    void BuildPracticeDigitList()
    {
        mylist.Clear();
        mylist.Add(1);
        mylist.Add(2);
        mylist.Add(3);
        mylist.Add(4);
        mylist.Add(5);
        mylist.Add(6);
        mylist.Add(7);
        mylist.Add(3);
        mylist.Add(8);
        mylist.Add(9);
        mylist.Add(1);
        mylist.Add(2);
        mylist.Add(3);
        mylist.Add(4);
        mylist.Add(5);
        mylist.Add(6);
        mylist.Add(7);
        mylist.Add(3);
        mylist.Add(8);
        mylist.Add(9);
        mylist.Add(1);
        mylist.Add(2);
        mylist.Add(3);
        mylist.Add(4);
        mylist.Add(5);
        mylist.Add(6);
        mylist.Add(7);
        mylist.Add(3);
        mylist.Add(8);
        mylist.Add(9);
    }

    // Update is called once per frame
    void Update()
    {
        if (praticeButtonIsClicked)
        {
            count2++;

            if (count2 == 1) {


                BuildPracticeDigitList();
                StartPractice();
                Pointer.SetActive(false);
                buttonStartPractice.SetActive(false);
                



            }
            if (currentNumber == 3 && grabPinchAction.GetStateDown(handType))
            {
                countPress++;


                if (countPress == 2)
                {
                    StartCoroutine(PracticeCompleted());
                    text.enabled = false;


                }



            }

            

        }


       

        

    }

    void buttonIsClicked()
    {
        praticeButtonIsClicked = true;
       


    }

    void StartPractice()
    {

        
        text.enabled = true;
        StartCoroutine(ShowNumber());

    }



    IEnumerator ShowNumber(bool _startDelay = false)
    {
        if (_startDelay)
        {
            yield return new WaitForSeconds(3);
        }

        if (mylist.Count > 0)
        {
            newNumber = mylist[Random.Range(0, mylist.Count)];

            if (newNumber != currentNumber)
            {
                currentNumber = newNumber;
                if (text != null)
                    text.text = currentNumber.ToString();

                float stimSec = stimulusDurationMs * 0.001f;
                float blankSec = postStimulusBlankMs * 0.001f;
                yield return new WaitForSeconds(stimSec);
                if (text != null)
                    text.text = string.Empty;
                yield return new WaitForSeconds(blankSec);

                mylist.Remove(currentNumber);
            }


            StartCoroutine(ShowNumber());

        }

        
        else if (countPress <= 1 && mylist.Count == 0)
        {
            StartCoroutine(StartAgain());
        }

    }

    

    IEnumerator StartAgain()
    {


        CanvasText.text = "Please start again. Make sure you press the trigger button of the controller when digit 3 is shown.";
        buttonStartPractice.SetActive(true);
        praticeButtonIsClicked = false;
        Pointer.SetActive(true);
        countPress = 0;
        count2 = 0;
        count = 0;
        text.enabled = false;
        

        yield return new WaitForSeconds(1f);
       
        //StartCoroutine(ShowNumber(false));


    }

    IEnumerator PracticeCompleted()
    {
        if (CanvasText != null)
            CanvasText.text = "Practice completed. You will now start the main task.";
        

        yield return new WaitForSeconds(3f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        this.gameObject.SetActive(false);
        GazeTracker.SetActive(true);
        Recorder.SetActive(true);
        RightHand.SetActive(false);

    }
}
