using System.Collections;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class Sc2bQuestion : MonoBehaviour
{
    [SerializeField] Button BtSubmit = null;

    string _q1 = "";
    string _q2 = "";
    string _q3 = "";
    string _q4 = "";
    string _q5 = "";
    string _q6 = "";

    const string AnswersHeader = "username,q1,q2,q3,q4,q5,q6,created_at";

    public string Q1 { set { _q1 = value; Validate(); } }
    public string Q2 { set { _q2 = value; Validate(); } }
    public string Q3 { set { _q3 = value; Validate(); } }
    public string Q4 { set { _q4 = value; Validate(); } }
    public string Q5 { set { _q5 = value; Validate(); } }
    public string Q6 { set { _q6 = value; Validate(); } }

    void Start()
    {
        if (BtSubmit != null)
            BtSubmit.onClick.AddListener(Submit);
        Validate();
    }

    void Validate()
    {
        if (BtSubmit != null)
            BtSubmit.interactable = _q1 != "" && _q2 != "" && _q3 != "" && _q4 != "" && _q5 != "" && _q6 != "";
    }

    public void Submit()
    {
        StartCoroutine(SaveAnswersLocally());
    }

    IEnumerator SaveAnswersLocally()
    {
        if (BtSubmit != null)
            BtSubmit.interactable = false;

        if (!LevelScript.HasParticipantIdentity())
        {
            Debug.LogError("Sc2bQuestion: UserGroup/UserName are empty. Log in from the ID scene first.");
            if (BtSubmit != null)
                BtSubmit.interactable = true;
            yield break;
        }

        try
        {
            string dir = LevelScript.GetQuestionnairePath(LevelScript.DataFolderSc2bQuestionnaire);
            Directory.CreateDirectory(dir);

            string path = Path.Combine(dir, "answers.csv");
            if (!File.Exists(path))
                File.WriteAllText(path, AnswersHeader + "\n", new UTF8Encoding(false));

            string row = string.Join(",",
                LevelScript.EscapeCsvField(LevelScript.UserName),
                LevelScript.EscapeCsvField(_q1),
                LevelScript.EscapeCsvField(_q2),
                LevelScript.EscapeCsvField(_q3),
                LevelScript.EscapeCsvField(_q4),
                LevelScript.EscapeCsvField(_q5),
                LevelScript.EscapeCsvField(_q6),
                System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            File.AppendAllText(path, row + "\n", new UTF8Encoding(false));

            Debug.Log($"Sc2bQuestion: saved to {path}");
            LevelScript.NextScene();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Sc2bQuestion local save failed: {e.Message}");
            if (BtSubmit != null)
                BtSubmit.interactable = true;
        }

        yield break;
    }
}
