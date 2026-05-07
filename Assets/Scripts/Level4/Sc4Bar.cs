using System.Collections;
using UnityEngine;
using PupilLabs;
using UnityEngine.UI;

public class Sc4Bar : LevelScript
{
    [SerializeField] AudioSource[] audios = null;
    public GameObject Pointer;

    [Space]
    [Header("Eye Tracker")]
    public RecordingController recorder;
    public Text statusText;

    void Awake()
    {
        recorder.customPath = $"{Application.dataPath}/Data/{UserGroup}/Sc6Club/{UserName}/Behavioural";
        bool connected = recorder.requestCtrl.IsConnected;
    }

    void OnDestroy()
    {
        recorder.StopRecording();
    }

    void Update()
    {
        StartBTN.onClick.AddListener(buttonIsClicked);

        if (!isStarted && btnIsClicked)
        {
            StartTask();
            recorder.StartRecording();
            Pointer.SetActive(false);
        }
    }

    void buttonIsClicked()
    {
        btnIsClicked = true;
    }

    new public void StartTask()
    {
        base.StartTask();
        EEG.Instance.Init("Sc6Club");
        foreach (var a in audios)
            a.Play();
    }

    IEnumerator EndTask()
    {
        recorder.StopRecording();
        StartCoroutine(SetLevel(SceneType.Sc4Questionnaire));
        yield return new WaitForSeconds(2);
        NextScene();
    }
}
