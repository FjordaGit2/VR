using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class Sc2aQuestion : MonoBehaviour
{
    [SerializeField] Button BtSubmit = null;

    string _q1 = "";
    string _q2 = "";
    string _q3 = "";
    string _q4 = "";
    string _q5 = "";
    string _q6 = "";

    bool _submitting;

    public string Q1 { set { _q1 = value; Validate(); } }
    public string Q2 { set { _q2 = value; Validate(); } }
    public string Q3 { set { _q3 = value; Validate(); } }
    public string Q4 { set { _q4 = value; Validate(); } }
    public string Q5 { set { _q5 = value; Validate(); } }
    public string Q6 { set { _q6 = value; Validate(); } }

    void Start()
    {
        if (BtSubmit != null)
        {
            // Replace the event so Inspector persistent Submit calls cannot double-fire NextScene.
            BtSubmit.onClick = new Button.ButtonClickedEvent();
            BtSubmit.onClick.AddListener(Submit);
        }
        Validate();
    }

    void Validate()
    {
        if (BtSubmit != null)
            BtSubmit.interactable = _q1 != "" && _q2 != "" && _q3 != "" && _q4 != "" && _q5 != "" && _q6 != "";
    }

    public void Submit()
    {
        if (_submitting)
            return;
        _submitting = true;
        StartCoroutine(SaveAnswersLocally());
    }

    IEnumerator SaveAnswersLocally()
    {
        if (BtSubmit != null)
            BtSubmit.interactable = false;

        if (!LevelScript.HasParticipantIdentity())
        {
            Debug.LogError("Sc2aQuestion: UserGroup/UserName are empty. Log in from the ID scene first.");
            _submitting = false;
            if (BtSubmit != null)
                BtSubmit.interactable = true;
            yield break;
        }

        try
        {
            StudyQuestionnaireSave.SaveAnswers(LevelScript.QuestionnaireFileSc2a, new[] { _q1, _q2, _q3, _q4, _q5, _q6 });
            Debug.Log($"Sc2aQuestion: saved to {LevelScript.GetQuestionnaireDirectory()}/{LevelScript.QuestionnaireFileSc2a}");
            LevelScript.NextScene();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Sc2aQuestion local save failed: {e.Message}");
            _submitting = false;
            if (BtSubmit != null)
                BtSubmit.interactable = true;
        }

        yield break;
    }
}
