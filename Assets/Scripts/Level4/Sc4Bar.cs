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

    [Space]
    [Header("Session video (in-Unity recorder)")]
    [Tooltip("If on, saves an MP4 of the camera view + in-task game audio under Assets/Screen Recordings (Editor Play Mode; not the PC mic). Turn off for real participants unless consented.")]
    [SerializeField] bool enableSessionRecording = false;

    void Awake()
    {
        recorder.customPath = $"{Application.dataPath}/Data/{UserGroup}/Sc6Club/{UserName}/Behavioural";
        bool connected = recorder.requestCtrl.IsConnected;
    }

    void OnDestroy()
    {
        if (recorder != null)
            recorder.StopRecording();
        StopSessionRecordingIfNeeded();
    }

    void Update()
    {
        StartBTN.onClick.AddListener(buttonIsClicked);

        if (!isStarted && btnIsClicked)
        {
            StartTask();
            if (recorder != null)
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
        // Start capture before ambient audio so the MP4 includes the beginning of bar sounds.
        StartSessionRecordingIfEnabled("Sc4");
        if (audios != null)
        {
            foreach (var a in audios)
            {
                if (a != null)
                    a.Play();
            }
        }
    }

    void StartSessionRecordingIfEnabled(string label)
    {
        if (!enableSessionRecording)
            return;
        VrSessionRecorder rec = VrSessionRecorder.Instance;
        if (rec == null)
            rec = FindObjectOfType<VrSessionRecorder>();
        if (rec == null)
        {
            var go = new GameObject("VrSessionRecorder");
            rec = go.AddComponent<VrSessionRecorder>();
        }
        rec.StartRecording(label);
    }

    void StopSessionRecordingIfNeeded()
    {
        if (VrSessionRecorder.Instance != null)
            VrSessionRecorder.Instance.StopRecording();
    }

    IEnumerator EndTask()
    {
        if (recorder != null)
            recorder.StopRecording();
        StopSessionRecordingIfNeeded();
        StartCoroutine(SetLevel(SceneType.Sc4Questionnaire));
        yield return new WaitForSeconds(2);
        NextScene();
    }
}
