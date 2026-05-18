using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class Sc1Question : MonoBehaviour
{
    [SerializeField] Button BtSubmit = null;
    string _q1 = "";
    string _q2 = "";
    string _q3 = "";
    string _q4 = "";
    public string Q1 { set { _q1 = value; Validate(); } }
    public string Q2 { set { _q2 = value; Validate(); } }
    public string Q3 { set { _q3 = value; Validate(); } }
    public string Q4 { set { _q4 = value; Validate(); } }
    void Validate()
    {
        BtSubmit.interactable = _q1 != "" && _q2 != "" && _q3 != "" && _q4 != "";
    }
    public void Submit()
    {
        StartCoroutine(PostData());
    }
    IEnumerator PostData()
    {
        BtSubmit.interactable = false;

        try
        {
            string dir = $"{Application.dataPath}/Data/{LevelScript.UserGroup}/{LevelScript.UserName}/Sc1Questionnaire";
            Directory.CreateDirectory(dir);

            string path = Path.Combine(dir, "answers.csv");
            StringBuilder csv = new StringBuilder(256);
            csv.AppendLine("username,q1,q2,q3,q4,created_at");
            csv.AppendLine($"{LevelScript.UserName},{_q1},{_q2},{_q3},{_q4},{System.DateTime.Now:O}");
            File.WriteAllText(path, csv.ToString(), new UTF8Encoding(false));

            LevelScript.NextScene();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Sc1Question local save failed: {e.Message}");
            BtSubmit.interactable = true;
        }

        yield break;
    }
}
