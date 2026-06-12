/// <summary>Shared CPT outcome / commission / omission helpers for task_trials.csv.</summary>
public static class StudyTaskTrialsLog
{
    public const string OutcomeColumnName = "outcome_Hit1_Miss2_FalseAlarm3_CorrectRejection4";
    public const string OutcomeBothColumnName = "outcome_both_Hit1_Miss2_FalseAlarm3_CorrectRejection4";

    public const int OutcomeHit = 1;
    public const int OutcomeMiss = 2;
    public const int OutcomeFalseAlarm = 3;
    public const int OutcomeCorrectRejection = 4;

    /// <summary>Sc3 car trials always require a touchpad response.</summary>
    public const int Sc3TargetAlwaysRespond = 1;

    public static int ComputeOutcomeCode(int targetFlag, int responseFlag)
    {
        if (targetFlag == 1 && responseFlag == 1)
            return OutcomeHit;
        if (targetFlag == 1 && responseFlag == 0)
            return OutcomeMiss;
        if (targetFlag == 0 && responseFlag == 1)
            return OutcomeFalseAlarm;
        return OutcomeCorrectRejection;
    }

    /// <summary>Go-only CPT mapping from response and accuracy (1 correct, 0 wrong, 2 no response).</summary>
    public static int ComputeRespondGoOutcomeCode(int responseFlag, int accuracyCode)
    {
        if (responseFlag == 0)
            return OutcomeMiss;
        return accuracyCode == 1 ? OutcomeHit : OutcomeFalseAlarm;
    }

    public static int CommissionFromOutcome(int outcomeCode) =>
        outcomeCode == OutcomeFalseAlarm ? 1 : 0;

    public static int OmissionFromOutcome(int outcomeCode) =>
        outcomeCode == OutcomeMiss ? 1 : 0;

    public static void IncrementOutcomeSummary(
        int outcomeCode,
        ref int hits,
        ref int misses,
        ref int falseAlarms,
        ref int correctRejections)
    {
        switch (outcomeCode)
        {
            case OutcomeHit:
                hits++;
                break;
            case OutcomeMiss:
                misses++;
                break;
            case OutcomeFalseAlarm:
                falseAlarms++;
                break;
            case OutcomeCorrectRejection:
                correctRejections++;
                break;
        }
    }
}
