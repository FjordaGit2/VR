using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

/// <summary>Writes one row per participant to shared per-scene questionnaire CSVs under Data/Questionnaire/.</summary>
public static class StudyQuestionnaireSave
{
    public struct QuestionDef
    {
        public string ColumnId;
        public string FullText;

        public QuestionDef(string columnId, string fullText)
        {
            ColumnId = columnId;
            FullText = fullText;
        }
    }

    static readonly QuestionDef[] Sc1Questions =
    {
        new QuestionDef("q_did_well", "I think I did very well in completing this task."),
        new QuestionDef("q_did_not_do_well", "I think I didn't do very well in completing this task."),
        new QuestionDef("q_task_interesting_realistic", "The task was interesting and was similar to a scenario that can happen in our day-to-day life."),
        new QuestionDef("q_felt_present", "I felt present and involved in VR."),
    };

    static readonly QuestionDef[] Sc2LectureQuestions =
    {
        new QuestionDef("q_wrong_number_affected_responses", "Pressing the button for the wrong number affected my next responses."),
        new QuestionDef("q_lecturer_students_distracting", "The lecturer and other students were distracting me during this task."),
        new QuestionDef("q_felt_present", "I felt present and involved in VR."),
        new QuestionDef("q_task_interesting_realistic", "The task was interesting and was similar to a scenario that can happen in our day-to-day life."),
        new QuestionDef("q_did_not_do_well", "I think I didn't do very well in completing this task."),
        new QuestionDef("q_did_well", "I think I did very well in completing this task."),
    };

    static readonly QuestionDef[] Sc3aQuestions =
    {
        new QuestionDef("q_wrong_car_affected_responses", "Pressing the button for the wrong car direction affected my next responses."),
        new QuestionDef("q_difficult_same_direction_click", "It was difficult to look and click in the direction of the car."),
        new QuestionDef("q_felt_present", "I felt present and involved in VR."),
        new QuestionDef("q_task_interesting_realistic", "The task was interesting and was similar to a scenario that can happen in our day-to-day life."),
        new QuestionDef("q_did_not_do_well", "I think I didn't do very well in completing this task."),
        new QuestionDef("q_did_well", "I think I did very well in completing this task."),
    };

    static readonly QuestionDef[] Sc3bQuestions =
    {
        new QuestionDef("q_wrong_car_affected_responses", "Pressing the button for the wrong car direction affected my next responses."),
        new QuestionDef("q_difficult_opposite_direction_click", "It was difficult to look and click in the opposite direction of the car."),
        new QuestionDef("q_felt_present", "I felt present and involved in VR."),
        new QuestionDef("q_task_interesting_realistic", "The task was interesting and was similar to a scenario that can happen in our day-to-day life."),
        new QuestionDef("q_did_not_do_well", "I think I didn't do very well in completing this task."),
        new QuestionDef("q_did_well", "I think I did very well in completing this task."),
    };

    static readonly Dictionary<string, QuestionDef[]> DefinitionsByCsv = new Dictionary<string, QuestionDef[]>
    {
        { LevelScript.QuestionnaireFileSc1, Sc1Questions },
        { LevelScript.QuestionnaireFileSc2a, Sc2LectureQuestions },
        { LevelScript.QuestionnaireFileSc2b, Sc2LectureQuestions },
        { LevelScript.QuestionnaireFileSc3a, Sc3aQuestions },
        { LevelScript.QuestionnaireFileSc3b, Sc3bQuestions },
    };

    public static void SaveAnswers(string questionnaireCsvFileName, IReadOnlyList<string> answers)
    {
        if (!DefinitionsByCsv.TryGetValue(questionnaireCsvFileName, out QuestionDef[] defs))
            throw new ArgumentException($"Unknown questionnaire file: {questionnaireCsvFileName}");

        if (answers == null || answers.Count != defs.Length)
            throw new ArgumentException($"Expected {defs.Length} answers for {questionnaireCsvFileName}, got {answers?.Count ?? 0}.");

        if (!LevelScript.HasParticipantIdentity())
            throw new InvalidOperationException("UserGroup/UserName are empty.");

        string dir = LevelScript.GetQuestionnaireDirectory();
        Directory.CreateDirectory(dir);

        string csvPath = Path.Combine(dir, questionnaireCsvFileName);
        string codebookPath = Path.Combine(dir, questionnaireCsvFileName.Replace(".csv", "_codebook.csv"));
        EnsureCodebook(codebookPath, defs);
        EnsureCsvHeader(csvPath, defs);

        var rowParts = new List<string>(defs.Length + 2)
        {
            LevelScript.EscapeCsvField(LevelScript.UserName),
        };
        for (int i = 0; i < answers.Count; i++)
            rowParts.Add(LevelScript.EscapeCsvField(answers[i]));
        rowParts.Add(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));

        File.AppendAllText(csvPath, string.Join(",", rowParts) + "\n", new UTF8Encoding(false));
    }

    static void EnsureCsvHeader(string csvPath, QuestionDef[] defs)
    {
        if (File.Exists(csvPath))
            return;

        var headerParts = new List<string>(defs.Length + 2) { "username" };
        for (int i = 0; i < defs.Length; i++)
            headerParts.Add(defs[i].ColumnId);
        headerParts.Add("created_at");
        File.WriteAllText(csvPath, string.Join(",", headerParts) + "\n", new UTF8Encoding(false));
    }

    static void EnsureCodebook(string codebookPath, QuestionDef[] defs)
    {
        if (File.Exists(codebookPath))
            return;

        var sb = new StringBuilder(512);
        sb.AppendLine("column_id,full_question_text");
        for (int i = 0; i < defs.Length; i++)
            sb.Append(LevelScript.EscapeCsvField(defs[i].ColumnId)).Append(',')
                .AppendLine(LevelScript.EscapeCsvField(defs[i].FullText));
        File.WriteAllText(codebookPath, sb.ToString(), new UTF8Encoding(false));
    }
}
