using System.Globalization;
using UnityEngine;

/// <summary>
/// Head-centered gaze metrics for egocentric 360° equirectangular heatmaps (common in VR eye-tracking pipelines).
/// <para>
/// <b>HMD local frame</b> (via <see cref="Transform.InverseTransformDirection"/>): +Z = head forward, +Y = up, +X = right.
/// </para>
/// <list type="bullet">
/// <item><b>yaw_deg</b> = atan2(local.x, local.z) in degrees, range (−180, 180]; 0 = straight ahead; positive = to the viewer’s right.</item>
/// <item><b>pitch_deg</b> = asin(local.y) in degrees, range [−90, 90]; positive = above the horizon in head space.</item>
/// <item><b>equirect_u</b> = (yaw_rad + π) / (2π) in [0, 1), horizontal pano coordinate (left→right of pano).</item>
/// <item><b>equirect_v</b> = (π/2 − asin(local.y)) / π in [0, 1]; 0 = zenith (+Y), 1 = nadir (−Y), 0.5 = horizon—typical “sky on top” equirect layout.</item>
/// </list>
/// Bin <c>equirect_u</c>/<c>equirect_v</c> to build group-average heatmaps; local x,y,z recover the same direction without ambiguity.
/// </summary>
public static class VrGazeEquirectMetrics
{
    const float MinDirSq = 1e-12f;

    /// <summary>Fills CSV-safe numeric strings; returns false if HMD or direction invalid (fields left empty).</summary>
    public static bool TryFormatCsvFields(Transform hmd, Vector3 gazeWorldDirection, out string lx, out string ly, out string lz, out string yawDeg, out string pitchDeg, out string equirectU, out string equirectV)
    {
        lx = ly = lz = yawDeg = pitchDeg = equirectU = equirectV = "";
        if (hmd == null || gazeWorldDirection.sqrMagnitude < MinDirSq)
            return false;

        Vector3 d = gazeWorldDirection.normalized;
        Vector3 local = hmd.InverseTransformDirection(d);
        if (local.sqrMagnitude < MinDirSq)
            return false;
        local.Normalize();

        float yawRad = Mathf.Atan2(local.x, local.z);
        float yawD = yawRad * Mathf.Rad2Deg;
        float yClamped = Mathf.Clamp(local.y, -1f, 1f);
        float pitchD = Mathf.Asin(yClamped) * Mathf.Rad2Deg;
        float u = (yawRad + Mathf.PI) / (2f * Mathf.PI);
        float v = (Mathf.PI * 0.5f - Mathf.Asin(yClamped)) / Mathf.PI;

        var inv = CultureInfo.InvariantCulture;
        lx = local.x.ToString("G9", inv);
        ly = local.y.ToString("G9", inv);
        lz = local.z.ToString("G9", inv);
        yawDeg = yawD.ToString("G9", inv);
        pitchDeg = pitchD.ToString("G9", inv);
        equirectU = u.ToString("G9", inv);
        equirectV = v.ToString("G9", inv);
        return true;
    }
}
