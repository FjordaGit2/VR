using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class Sc4Question : MonoBehaviour
{
    [SerializeField] Button BtSubmit = null;
    string _q1 = "";
    string _q2 = "";
    string _q3 = "";
    string _q4 = "";
    string _q5 = "";
    string _q6 = "";

    public string Q1 { set { _q1 = value; Validate(); } }
    public string Q2 { set { _q2 = value; Validate(); } }
    public string Q3 { set { _q3 = value; Validate(); } }
    public string Q4 { set { _q4 = value; Validate(); } }
    public string Q5 { set { _q5 = value; Validate(); } }
    public string Q6 { set { _q6 = value; Validate(); } }

    void Validate()
    {
        BtSubmit.interactable = _q1 != "" && _q2 != "" && _q3 != "" && _q4 != "" && _q5 != "" && _q6 != "";
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
            string dir = $"{Application.dataPath}/Data/{LevelScript.UserGroup}/Sc4Questionnaire/{LevelScript.UserName}";
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "answers.csv");
            if (!File.Exists(path))
                File.WriteAllText(path, "username,q1,q2,q3,q4,q5,q6,created_at\n", new UTF8Encoding(false));
            File.AppendAllText(path, $"{LevelScript.UserName},{_q1},{_q2},{_q3},{_q4},{_q5},{_q6},{System.DateTime.Now:yyyy-MM-dd HH:mm:ss}\n", new UTF8Encoding(false));

            StartCoroutine(LevelScript.SetLevel(SceneType.Sc5StreetPedestrian));
            LevelScript.NextScene();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Sc4Question local save failed: {e.Message}");
            BtSubmit.interactable = true;
        }

        yield break;
    }
}
