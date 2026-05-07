using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnitySimpleLiquid;
using PupilLabs;

public class Sc8ChemistryLab : LevelScript
{
    public Button btn;
    public GameObject TaskButton;
    public GameObject CanvasInstructions;
    public TextMeshProUGUI RobotInstructions;
    public GameObject Pointer;
    float startTime = 0;
    [SerializeField] int TimeLimit = 600;
    private float attentionAverage;
    private int count;
    int countBool = 0;
    int countBool2 = 0;

    [Space]
    [Header("Experiment 1")]
    public GameObject Exp1;
    public bool experiment1;
    public GameObject MiniPotassium;
    public MeshCollider PotassiumMeshCollider;

    [Space]
    [Header("Experiment 2")]
    public GameObject Exp2;
    public bool experiment2;
    public GameObject BeakerExp2;
    public MeshCollider BrownBottleMeshCollider;
    public MeshCollider BrownBottleFluidMeshCollider;
    public CapsuleCollider BrownBottleCapsuleCollider;
    public MeshCollider AluminumMeshCollider;
    public CapsuleCollider AluminumCapsuleCollider;
    public GameObject MiniAluminum;

    [Space]
    [Header("Experiment 3")]
    public GameObject Exp3;
    public bool experiment3;
    public GameObject PlateExp3;
    public MeshCollider NitromethaneMeshCollider;
    public MeshCollider NitromethaneFluidMeshCollider;
    public CapsuleCollider NitromethaneCapsuleCollider;
    public MeshCollider MethanolMeshCollider;
    public MeshCollider MethanolFluidMeshCollider;
    public CapsuleCollider MethanolCapsuleCollider;
    public MeshCollider LighterMeshCollider;
    public BoxCollider LighterBoxCollider;
    public GameObject FlameExp3;

    [Space]
    [Header("Eye Tracker")]
    public RecordingController recorder;

    void Awake()
    {
        recorder.customPath = $"{Application.dataPath}/Data/{UserGroup}/Sc10ChemistryLab/{UserName}/Behavioural";
    }

    void OnDestroy()
    {
        recorder.StopRecording();
    }


    void Start()
    {
        experiment1 = false;
        experiment2 = false;
        experiment3 = false;
        Exp1.SetActive(false);
        Exp2.SetActive(false);
        Exp3.SetActive(false);

        AluminumMeshCollider.enabled = false;
        AluminumCapsuleCollider.enabled = false;

        btn.onClick.AddListener(ButtonOnClick);
    }

    void ButtonOnClick()
    {
        StartTask();
    }

    new public void StartTask()
    {
        base.StartTask();
        EEG.Instance.Init("Sc10ChemistryLab");
        recorder.StartRecording();
        CanvasInstructions.SetActive(false);
        experiment1 = true;
        StartCoroutine(Experiments());
        StartCoroutine(LimitTimeCounter());
    }

    // Update is called once per frame
    void Update()
    {
        EEG.Instance.attention.value = Mathf.Lerp((float)EEG.Instance.attention.value, (float)EEG.Instance.attention.target, 0.2f);
        attentionAverage = (float)(attentionAverage * count + EEG.Instance.attention.value) / (count + 1);
        count++;

   
        if (experiment1 == true)
        {
            StartCoroutine(StartExperiment1());
        }

        if (experiment2 == true)
        {
            StartCoroutine(StartExperiment2());
        }
        if (experiment3 == true)
        {
            StartCoroutine(StartExperiment3());
        }
    }

    IEnumerator StartExperiment1()
    {
        Exp1.SetActive(true);
        TaskButton.SetActive(false);
        

        if (MiniPotassium.activeSelf == false)
        {
            if (EEG.Instance.attention.value < 0.4)
            {
                PotassiumMeshCollider.enabled = false;
                RobotInstructions.text = "Your attention level is low. You can only complete this experiment with higher attention levels";
            }
            else
            {
                PotassiumMeshCollider.enabled = true;
                RobotInstructions.text = "Grab the potassium rock and place it in the water of the beaker (do not drop it).";
            }

        }
        else 
        { 
            experiment1 = false;
            RobotInstructions.text = "Well done for completing experiment 1.";
            yield return new WaitForSeconds(10.0f);
            StartCoroutine(StartExperiment2());

        }
    }

    IEnumerator StartExperiment2()
    {
        experiment2 = true;
        yield return new WaitForSeconds(2.0f);
        Exp1.SetActive(false);
        Exp2.SetActive(true);


        if (BeakerExp2.GetComponent<LiquidContainer>().fillAmountPercent > 0.5f && MiniAluminum.activeSelf == false)
        {
            if (EEG.Instance.attention.value < 0.4)
            {
                AluminumMeshCollider.enabled = false;
                AluminumCapsuleCollider.enabled = false;

                RobotInstructions.text = "Your attention level is low. You can only complete this experiment with higher attention levels";
            }
            else
            {
                BrownBottleMeshCollider.enabled = false;
                BrownBottleFluidMeshCollider.enabled = false;
                BrownBottleCapsuleCollider.enabled = false;

                AluminumMeshCollider.enabled = true;
                AluminumCapsuleCollider.enabled = true;

                RobotInstructions.text = "Grab the aluminum and place it into the beaker with bromine (do not drop it).";

            }

        }
        else if (BeakerExp2.GetComponent<LiquidContainer>().fillAmountPercent > 0.5f && MiniAluminum.activeSelf == true)
        {
            experiment2 = false;
            RobotInstructions.text = "Well done for completing experiment 2.";
            yield return new WaitForSeconds(12.0f);
            StartCoroutine(StartExperiment3());
        }
        else
        {
            if (EEG.Instance.attention.value < 0.4)
            {
                BrownBottleMeshCollider.enabled = false;
                BrownBottleFluidMeshCollider.enabled = false;
                BrownBottleCapsuleCollider.enabled = false;

                RobotInstructions.text = "Your attention level is low. You can only complete this experiment with higher attention levels";
            }
            else
            {
                BrownBottleMeshCollider.enabled = true;
                BrownBottleFluidMeshCollider.enabled = true;
                BrownBottleCapsuleCollider.enabled = true;

                RobotInstructions.text = "Grab the brown bottle with bromine and pour it into the beaker.";
            }
           
        }

    }

    IEnumerator StartExperiment3()
    {
        experiment3 = true;
        yield return new WaitForSeconds(2.0f);
        Exp2.SetActive(false);
        Exp3.SetActive(true);

        

        if (PlateExp3.GetComponent<LiquidContainer>().fillAmountPercent == 0.2f && FlameExp3.activeSelf == false)
        {

            if (EEG.Instance.attention.value < 0.4)
            {
                NitromethaneMeshCollider.enabled = false;
                NitromethaneFluidMeshCollider.enabled = false;
                NitromethaneCapsuleCollider.enabled = false;

                MethanolMeshCollider.enabled = false;
                MethanolFluidMeshCollider.enabled = false;
                MethanolCapsuleCollider.enabled = false;

                RobotInstructions.text = "Your attention level is low. You can only complete this experiment with higher attention levels";
            }
            else
            {
                NitromethaneMeshCollider.enabled = false;
                NitromethaneFluidMeshCollider.enabled = false;
                NitromethaneCapsuleCollider.enabled = false;

                MethanolMeshCollider.enabled = true;
                MethanolFluidMeshCollider.enabled = true;
                MethanolCapsuleCollider.enabled = true;

                RobotInstructions.text = "Grab the tube with Methanol and pour it into the plate that has Nitromethane.";
            }
            
        }
        else if (PlateExp3.GetComponent<LiquidContainer>().fillAmountPercent == 0.9f && FlameExp3.activeSelf == false)
        {

            if (EEG.Instance.attention.value < 0.4)
            {
                NitromethaneMeshCollider.enabled = false;
                NitromethaneFluidMeshCollider.enabled = false;
                NitromethaneCapsuleCollider.enabled = false;

                MethanolMeshCollider.enabled = false;
                MethanolFluidMeshCollider.enabled = false;
                MethanolCapsuleCollider.enabled = false;

                LighterMeshCollider.enabled = false;
                LighterBoxCollider.enabled = false;

                RobotInstructions.text = "Your attention level is low. You can only complete this experiment with higher attention levels";
            }
            else
            {
                NitromethaneMeshCollider.enabled = false;
                NitromethaneFluidMeshCollider.enabled = false;
                NitromethaneCapsuleCollider.enabled = false;

                MethanolMeshCollider.enabled = false;
                MethanolFluidMeshCollider.enabled = false;
                MethanolCapsuleCollider.enabled = false;

                LighterMeshCollider.enabled = true;
                LighterBoxCollider.enabled = true;

                RobotInstructions.text = "Grab the lighter and add the flame into the plate that has Nitromethane and Methanol.";
            }
           
        }
        else if (PlateExp3.GetComponent<LiquidContainer>().fillAmountPercent == 0.9f && FlameExp3.activeSelf == true)
        {
            experiment3 = false;
            RobotInstructions.text = "Well done for completing experiment 3.";
            recorder.StopRecording();
            StartCoroutine(PostData());


        }
        else if (PlateExp3.GetComponent<LiquidContainer>().fillAmountPercent < 0.2f && FlameExp3.activeSelf == false)
        {
            if (EEG.Instance.attention.value < 0.4)
            {
                NitromethaneMeshCollider.enabled = false;
                NitromethaneFluidMeshCollider.enabled = false;
                NitromethaneCapsuleCollider.enabled = false;

                MethanolMeshCollider.enabled = false;
                MethanolFluidMeshCollider.enabled = false;
                MethanolCapsuleCollider.enabled = false;

                LighterMeshCollider.enabled = false;
                LighterBoxCollider.enabled = false;

                RobotInstructions.text = "Your attention level is low. You can only complete this experiment with higher attention levels";
            }
            else 
            {
                NitromethaneMeshCollider.enabled = true;
                NitromethaneFluidMeshCollider.enabled = true;
                NitromethaneCapsuleCollider.enabled = true;

                MethanolMeshCollider.enabled = false;
                MethanolFluidMeshCollider.enabled = false;
                MethanolCapsuleCollider.enabled = false;

                LighterMeshCollider.enabled = false;
                LighterBoxCollider.enabled = false;

                RobotInstructions.text = "Grab the white bottle with Nitromethane and pour it into the plate.";
            }
            
        }

    }
    IEnumerator Experiments()
    {
        yield return new WaitForSeconds(0.1f);
        startTime = Time.time;

    }

    public IEnumerator LimitTimeCounter()
    {
        startTime = Time.time;
        yield return new WaitForSeconds(TimeLimit);
        countBool2++;
        if (countBool2 == 1)
        {
            StartCoroutine(Post());
        }
    }

    IEnumerator PostData()
    {
        yield return new WaitForSeconds(7);
        countBool++;

        if (countBool == 1)
        {
            StartCoroutine(Post());
        }

    }

    IEnumerator Post()
    {

        float time = Time.time - startTime;
        float score = Mathf.Clamp((float)(20 - time * 2 / TimeLimit) + (attentionAverage * 100), 0, 100);
        string accuracy = "High";
        if (score < 60f) accuracy = "Medium";
        if (score < 20f) accuracy = "Low";
        string dir = recorder != null ? recorder.customPath : $"{Application.dataPath}/Data/{UserGroup}/Sc10ChemistryLab/{UserName}/Behavioural";
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "summary.csv");
        if (!File.Exists(path))
            File.WriteAllText(path, "username,attention_average,score,accuracy,reaction_time_ms,created_at\n", new UTF8Encoding(false));
        File.AppendAllText(path, $"{UserName},{attentionAverage},{score},{accuracy},{(time * 1000).ToString("0.0")},{System.DateTime.Now:yyyy-MM-dd HH:mm:ss}\n", new UTF8Encoding(false));
        StartCoroutine(SetLevel(SceneType.Sc8Questionnaire));
        NextScene();
        yield break;

    }

}
