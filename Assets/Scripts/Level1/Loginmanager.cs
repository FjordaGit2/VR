using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.IO;
using System.Text;


public class Loginmanager : MonoBehaviour
{
    [SerializeField] TMP_InputField IFUserName = null;
    [SerializeField] TMP_InputField IFAge = null;
    [SerializeField] TMP_Dropdown DDGender = null;
    [SerializeField] TMP_Dropdown DDHighestEducation = null;
    [SerializeField] TMP_Dropdown DDGroup = null;
    [SerializeField] TMP_Dropdown DDVision = null;
    [SerializeField] TMP_Dropdown DDHearing = null;
    [Tooltip("Scene order for this participant batch: options \"1 to 5\" and \"5 to 1\" (filled in code if empty).")]
    [SerializeField] TMP_Dropdown DDSceneOrder = null;
    [SerializeField] Button BtSubmit = null;
    [SerializeField] TMP_Text ErrorMessage = null;

    private string UserId;
    const string DemographicsHeader = "username,age,gender,highest_education,group,vision,hearing,scene_order,created_at";

    string _platform;
    public string Platform
    {
        get { return _platform; }
        set { _platform = value; }
    }

    private void Start()
    {
        Platform = "VR";
        IFUserName.onValueChanged.AddListener(delegate { Validate(); });
        IFAge.onValueChanged.AddListener(delegate { Validate(); });
        BtSubmit.onClick.AddListener(delegate { StartCoroutine(Login()); });
        EnsureSceneOrderDropdownOptions();
    }

    void EnsureSceneOrderDropdownOptions()
    {
        if (DDSceneOrder == null)
            return;
        DDSceneOrder.ClearOptions();
        DDSceneOrder.AddOptions(new System.Collections.Generic.List<string>
        {
            StudySceneFlow.DropdownLabelForward,
            StudySceneFlow.DropdownLabelReverse,
        });
        DDSceneOrder.value = 0;
    }

    public IEnumerator Login()
    {
        string username = IFUserName.text.Trim();
        string group = DDGroup.captionText.text;
        string baseDataPath = LevelScript.GetDataRootPath();
        string demographicsPath = $"{baseDataPath}/demographics.csv";

        BtSubmit.interactable = false;
        try
        {
            Directory.CreateDirectory(baseDataPath);

            StringBuilder rowBuilder = new StringBuilder(256);
            rowBuilder.Append(EscapeCsv(username)).Append(",");
            rowBuilder.Append(EscapeCsv(IFAge.text.Trim())).Append(",");
            rowBuilder.Append(EscapeCsv(DDGender.captionText.text)).Append(",");
            rowBuilder.Append(EscapeCsv(DDHighestEducation.captionText.text)).Append(",");
            rowBuilder.Append(EscapeCsv(group)).Append(",");
            rowBuilder.Append(EscapeCsv(DDVision.captionText.text)).Append(",");
            rowBuilder.Append(EscapeCsv(DDHearing.captionText.text)).Append(",");
            string sceneOrder = DDSceneOrder != null
                ? DDSceneOrder.options[DDSceneOrder.value].text
                : StudySceneFlow.DropdownLabelForward;
            rowBuilder.Append(EscapeCsv(sceneOrder)).Append(",");
            rowBuilder.AppendLine(System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

            if (!File.Exists(demographicsPath))
                File.WriteAllText(demographicsPath, DemographicsHeader + "\n", new UTF8Encoding(false));
            File.AppendAllText(demographicsPath, rowBuilder.ToString(), new UTF8Encoding(false));

            LevelScript.UserName = username;
            LevelScript.UserGroup = group;
            LevelScript.IsVR = Platform == "VR";

            if (DDSceneOrder != null)
                StudySceneFlow.SetOrderFromDropdownIndex(DDSceneOrder.value);
            else
                StudySceneFlow.SetOrderFromDropdownLabel(sceneOrder);
            StudySceneFlow.BeginStudyAfterLogin();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Local save failed: {e.Message}");
            StartCoroutine(Error("Could not save local user data. Please try again."));
            BtSubmit.interactable = true;
        }

        yield break;
    }

    static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";
        if (value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0)
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }

    void Validate()
    {
        BtSubmit.interactable = IFUserName.text != "" && IFAge.text != "";
    }
    IEnumerator Error(string message)
    {
        ErrorMessage.transform.parent.parent.gameObject.SetActive(true);
        ErrorMessage.text = message;
        yield return new WaitForSeconds(3);
        ErrorMessage.transform.parent.parent.gameObject.SetActive(false);
    }
}
