using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class Sc1Question : MonoBehaviour
{
    [SerializeField] Button BtSubmit = null;

    string _q1 = "";
    string _q2 = "";
    string _q3 = "";
    string _q4 = "";

    bool _submitting;

    public string Q1 { set { _q1 = value; Validate(); } }
    public string Q2 { set { _q2 = value; Validate(); } }
    public string Q3 { set { _q3 = value; Validate(); } }
    public string Q4 { set { _q4 = value; Validate(); } }

    void Start()
    {
        if (BtSubmit != null)
        {
            BtSubmit.onClick.RemoveAllListeners();
            BtSubmit.onClick.AddListener(Submit);
        }
        Validate();
    }

    void Validate()
    {
        if (BtSubmit != null)
            BtSubmit.interactable = _q1 != "" && _q2 != "" && _q3 != "" && _q4 != "";
    }

    public void Submit()
    {
        if (_submitting)
            return;
        StartCoroutine(SaveAnswersLocally());
    }

    IEnumerator SaveAnswersLocally()
    {
        if (_submitting)
            yield break;
        _submitting = true;

        if (BtSubmit != null)
            BtSubmit.interactable = false;

        if (!LevelScript.HasParticipantIdentity())
        {
            Debug.LogError("Sc1Question: UserGroup/UserName are empty. Log in from the ID scene first (or set LevelScript.UserGroup and LevelScript.UserName for testing).");
            _submitting = false;
            if (BtSubmit != null)
                BtSubmit.interactable = true;
            yield break;
        }

        try
        {
            StudyQuestionnaireSave.SaveAnswers(LevelScript.QuestionnaireFileSc1, new[] { _q1, _q2, _q3, _q4 });
            Debug.Log($"Sc1Question: saved to {LevelScript.GetQuestionnaireDirectory()}/{LevelScript.QuestionnaireFileSc1}");
            LevelScript.NextScene();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Sc1Question local save failed: {e.Message}");
            _submitting = false;
            if (BtSubmit != null)
                BtSubmit.interactable = true;
        }

        yield break;
    }
}
