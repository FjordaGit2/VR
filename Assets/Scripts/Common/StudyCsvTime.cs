using System;
using System.Globalization;
using UnityEngine;

/// <summary>Milliseconds for behavioural CSV time columns (session streams, summaries, trials).</summary>
public static class StudyCsvTime
{
    public const string VideoSessionTimeColumnsHeader =
        "time_since_video_ms,pupil_timestamp_ms,unity_time_ms";

    public const string TaskSessionTimeColumnsHeader =
        "time_since_task_start_ms,pupil_timestamp_ms,unity_time_ms";

    public static string FormatSecondsAsMs(float seconds) =>
        Mathf.Round(seconds * 1000f).ToString(CultureInfo.InvariantCulture);

    public static string FormatSecondsAsMs(double seconds) =>
        Math.Round(seconds * 1000.0).ToString(CultureInfo.InvariantCulture);

    public static string FormatOptionalPupilTimestampMs(bool havePupilTs, double pupilTsSeconds) =>
        havePupilTs ? FormatSecondsAsMs(pupilTsSeconds) : "";

    public static string FormatOptionalTimestampCellMs(double seconds) =>
        double.IsNaN(seconds) ? "NaN" : FormatSecondsAsMs(seconds);

    public static int GazeSampleCountToMs(int sampleCount, float samplePeriod) =>
        Mathf.RoundToInt(sampleCount * samplePeriod * 1000f);
}
