using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;
using Valve.VR;
using PupilLabs;
using UnityEngine.UI;
using Looxid.Link;
using System.IO;
using System.Text;
using UnityEngine.Networking;
using SimpleJSON;
using System;
using System.Globalization;
using UnityEngine.SceneManagement;

public class Sc1LivingRoom : LevelScript
{
    [SerializeField] VideoPlayer video = null;
    [SerializeField] AudioSource[] audios = null;
    public GameObject Pointer;

    [Tooltip("Optional. World point for TV distance/angle vs head. If unset, uses first GameObject tagged TV.")]
    [SerializeField] Transform tvHeadMetricsAnchor;

    int lookedtvcount;
    int lookedelsecount;

    private float nextActionTime;
    public float period = 0.1f;

    [Space]
    [Header("Eye Tracker")]
    public RecordingController recorder;
    public Text statusText;
    public GazeVisualizer gazeVisualizer;
    public GazeData gazeData;
    /// <summary>Pupil calibration reference: local space of GazeDirection / eye data. Assign the same camera/transform used in Pupil Capture VR calibration (typically the HMD main camera).</summary>
    public Transform gazeOriginCamera;
    public GazeController gazeController;

    [Tooltip("Gaze is valid (usable) only if confidence is at or above this value.")]
    [Range(0f, 1f)]
    public float gazeConfidenceThreshold = 0.6f;

    [Tooltip("How often to flush CSV streams to disk (seconds). Rows are not flushed per tick.")]
    [Min(0.5f)]
    public float csvFlushIntervalSeconds = 2f;

    [Tooltip("Analog squeeze above this counts as trigger_pressed = 1.")]
    [Range(0f, 1f)]
    public float triggerPressThreshold = 0.5f;

    StreamWriter timeseriesWriter;
    StreamWriter eventsWriter;
    StreamWriter headWriter;
    StreamWriter controllerWriter;
    float videoStartUnityTime;
    float lastCsvFlushTime;
    bool csvGazeLogging;
    bool hasPreviousObservedState;
    int previousObservedState;

    bool headMotionPrimed;
    Vector3 headPrevWorldPos;
    Quaternion headPrevWorldRot;
    Vector3 headLastVelWorld;
    float headLastLinSpeed;
    float headLastAngSpeed;
    int headKinTickCount;

    const int StateTv = 1;
    const int StateNotTv = 0;
    const int StateInvalid = -1;

    static SteamVR_Action_Pose VrPose => SteamVR_Actions.default_Pose;

    void Awake()
    {
        string date = System.DateTime.Now.ToString("yyyy_MM_dd");
        recorder.customPath = $"{Application.dataPath}/Data/{UserGroup}/{UserName + "_" + date}/Sc1LivingRoom/EyeTracking";
    }

    private void OnEnable()
    {
        if (gazeController)
            gazeController.OnReceive3dGaze += OnReceive;
    }

    private void OnDisable()
    {
        if (gazeController)
            gazeController.OnReceive3dGaze -= OnReceive;
    }

    private void OnReceive(GazeData obj)
    {
        gazeData = obj;
    }

    void OnDestroy()
    {
        CloseSessionCsvWriters();
        recorder.StopRecording();
    }

    void Update()
    {
        StartBTN.onClick.AddListener(buttonIsClicked);

        if (!isStarted && btnIsClicked)
        {
            StartTask();
            recorder.StartRecording();
            Pointer.SetActive(false);
        }

        if (isStarted && video.isPaused)
        {
            isStarted = false;
            btnIsClicked = false;
            StartCoroutine(EndTask());
        }

        if (csvGazeLogging && gazeOriginCamera != null && Time.timeScale > 0f)
        {
            if (Time.time >= nextActionTime)
            {
                nextActionTime += period;
                float sinceVideo = Time.time - videoStartUnityTime;
                GazeData gd = gazeData;
                bool havePupilTs = gd != null;
                double pupilTs = havePupilTs ? gd.PupilTimestamp : 0;

                ProcessGazeTick(sinceVideo, havePupilTs, pupilTs);
                ProcessHeadMotionRow(sinceVideo, havePupilTs, pupilTs);
                ProcessControllerHandRow(sinceVideo, havePupilTs, pupilTs, SteamVR_Input_Sources.LeftHand, "left");
                ProcessControllerHandRow(sinceVideo, havePupilTs, pupilTs, SteamVR_Input_Sources.RightHand, "right");
                MaybePeriodicFlushCsv();
            }
        }
    }

    void ProcessGazeTick(float sinceVideo, bool havePupilTs, double pupilTs)
    {
        float conf = 0f;
        int valid;
        int state;
        string hitName = "";
        string hitTag = "";
        string invalidReason = "";

        GazeData gd = gazeData;
        bool gazePacket = gd != null;
        if (gazePacket)
            conf = gd.Confidence;

        bool confOk = gazePacket
            && (gazeConfidenceThreshold <= 0f || gd.Confidence >= gazeConfidenceThreshold);

        if (!gazePacket)
        {
            valid = 0;
            state = StateInvalid;
            hitName = "";
            hitTag = "";
            invalidReason = "no_gaze";
        }
        else if (!confOk)
        {
            valid = 0;
            state = StateInvalid;
            hitName = "";
            hitTag = "";
            invalidReason = "low_confidence";
        }
        else
        {
            if (!TryBuildGazeRay(gd, gazeOriginCamera, out Vector3 origin, out Vector3 direction)
                || direction.sqrMagnitude < 1e-8f)
            {
                valid = 0;
                state = StateInvalid;
                hitName = "";
                hitTag = "";
                invalidReason = "ray_fail";
            }
            else if (Physics.SphereCast(origin, 0.05f, direction, out RaycastHit hit, Mathf.Infinity))
            {
                valid = 1;
                var col = hit.collider;
                if (col != null)
                {
                    hitName = col.gameObject.name;
                    hitTag = col.tag;
                    state = col.CompareTag("TV") ? StateTv : StateNotTv;
                }
                else
                {
                    hitName = "";
                    hitTag = "";
                    state = StateNotTv;
                }

                invalidReason = "";
            }
            else
            {
                valid = 1;
                state = StateNotTv;
                hitName = "no_hit";
                hitTag = "";
                invalidReason = "";
            }
        }

        WriteTimeseriesRow(sinceVideo, gazePacket, havePupilTs, pupilTs, conf, valid, state, hitName, hitTag, invalidReason);

        if (!hasPreviousObservedState)
        {
            hasPreviousObservedState = true;
            previousObservedState = state;
        }
        else if (state != previousObservedState)
        {
            string ev = EventForTransition(previousObservedState, state);
            if (ev != null)
                WriteEventRow(sinceVideo, havePupilTs, pupilTs, conf, valid, hitName, hitTag, invalidReason, ev);
            previousObservedState = state;
        }

        if (valid == 1)
        {
            if (state == StateTv)
                lookedtvcount++;
            else
                lookedelsecount++;
        }
    }

    void ProcessHeadMotionRow(float sinceVideo, bool havePupilTs, double pupilTs)
    {
        if (headWriter == null || gazeOriginCamera == null)
            return;

        Transform cam = gazeOriginCamera;
        Vector3 p = cam.position;
        Quaternion r = cam.rotation;
        Vector3 euler = r.eulerAngles;
        Vector3 fwd = cam.forward;
        Vector3 up = cam.up;

        Vector3 velWorld = headMotionPrimed ? (p - headPrevWorldPos) / period : Vector3.zero;
        float linearSpeed = velWorld.magnitude;
        float angDeg = headMotionPrimed ? Quaternion.Angle(headPrevWorldRot, r) : 0f;
        float angularSpeed = headMotionPrimed ? Mathf.Deg2Rad * angDeg / period : 0f;

        float accelLinVecMag = 0f;
        float accelLinear = 0f;
        float accelAngular = 0f;
        if (headMotionPrimed && headKinTickCount >= 2)
        {
            accelLinVecMag = (velWorld - headLastVelWorld).magnitude / period;
            accelLinear = (linearSpeed - headLastLinSpeed) / period;
            accelAngular = (angularSpeed - headLastAngSpeed) / period;
        }

        string distCol = "";
        string angCol = "";
        if (TryGetTvWorldPoint(out Vector3 tvPos))
        {
            distCol = F(Vector3.Distance(p, tvPos));
            Vector3 toTv = tvPos - p;
            if (toTv.sqrMagnitude > 1e-10f)
                angCol = F(Vector3.Angle(cam.forward, toTv.normalized));
        }

        string pCol = havePupilTs ? F(pupilTs) : "";
        headWriter.WriteLine(string.Join(",",
            F(sinceVideo),
            pCol,
            F(Time.time),
            F(p.x), F(p.y), F(p.z),
            F(euler.x), F(euler.y), F(euler.z),
            F(fwd.x), F(fwd.y), F(fwd.z),
            F(up.x), F(up.y), F(up.z),
            F(velWorld.x), F(velWorld.y), F(velWorld.z),
            F(linearSpeed),
            F(accelLinVecMag),
            F(accelLinear),
            F(accelAngular),
            F(angularSpeed),
            distCol,
            angCol));

        if (headMotionPrimed)
        {
            headLastVelWorld = velWorld;
            headLastLinSpeed = linearSpeed;
            headLastAngSpeed = angularSpeed;
        }

        headPrevWorldPos = p;
        headPrevWorldRot = r;
        headMotionPrimed = true;
        headKinTickCount++;
    }

    void ProcessControllerHandRow(float sinceVideo, bool havePupilTs, double pupilTs, SteamVR_Input_Sources hand, string handLabel)
    {
        if (controllerWriter == null)
            return;

        Vector3 p = Vector3.zero;
        Quaternion rot = Quaternion.identity;
        if (SteamVR.initializedState == SteamVR.InitializedStates.InitializeSuccess && VrPose != null
            && VrPose.GetPoseIsValid(hand))
        {
            Vector3 lp = VrPose.GetLocalPosition(hand);
            Quaternion lr = VrPose.GetLocalRotation(hand);
            Transform rig = GetTrackingRigOrigin();
            if (rig != null)
            {
                p = rig.TransformPoint(lp);
                rot = rig.rotation * lr;
            }
            else
            {
                p = lp;
                rot = lr;
            }
        }

        Vector3 euler = rot.eulerAngles;
        float vLin = 0f;
        float vAng = 0f;
        if (SteamVR.initializedState == SteamVR.InitializedStates.InitializeSuccess && VrPose != null
            && VrPose.GetPoseIsValid(hand))
        {
            Transform rig = GetTrackingRigOrigin();
            Vector3 lv = VrPose.GetVelocity(hand);
            Vector3 av = VrPose.GetAngularVelocity(hand);
            if (rig != null)
            {
                lv = rig.TransformDirection(lv);
                av = rig.TransformDirection(av);
            }

            vLin = lv.magnitude;
            vAng = av.magnitude;
        }

        int triggerPressed = 0;
        int gripPressed = 0;
        if (SteamVR.initializedState == SteamVR.InitializedStates.InitializeSuccess)
        {
            var squeeze = SteamVR_Actions.default_Squeeze;
            if (squeeze != null && squeeze.activeBinding)
                triggerPressed = squeeze.GetAxis(hand) >= triggerPressThreshold ? 1 : 0;
            var grip = SteamVR_Actions.default_GrabGrip;
            if (grip != null && grip.activeBinding)
                gripPressed = grip.GetState(hand) ? 1 : 0;
        }

        string buttonEvents = BuildControllerButtonEvents(hand);

        string pCol = havePupilTs ? F(pupilTs) : "";
        controllerWriter.WriteLine(string.Join(",",
            F(sinceVideo),
            pCol,
            F(Time.time),
            CsvEscape(handLabel),
            F(p.x), F(p.y), F(p.z),
            F(euler.x), F(euler.y), F(euler.z),
            F(vLin),
            F(vAng),
            triggerPressed.ToString(CultureInfo.InvariantCulture),
            gripPressed.ToString(CultureInfo.InvariantCulture),
            CsvEscape(buttonEvents)));
    }

    static string BuildControllerButtonEvents(SteamVR_Input_Sources hand)
    {
        if (SteamVR.initializedState != SteamVR.InitializedStates.InitializeSuccess)
            return "";

        var sb = new StringBuilder(64);
        AppendBoolEdges(sb, SteamVR_Actions.default_GrabPinch, hand, "grab_pinch");
        AppendBoolEdges(sb, SteamVR_Actions.default_GrabGrip, hand, "grab_grip");
        AppendBoolEdges(sb, SteamVR_Actions.default_InteractUI, hand, "interact_ui");
        AppendBoolEdges(sb, SteamVR_Actions.default_Teleport, hand, "teleport");
        AppendBoolEdges(sb, SteamVR_Actions.default_TouchpadClick, hand, "touchpad_click");
        AppendBoolEdges(sb, SteamVR_Actions.default_SnapTurnLeft, hand, "snap_turn_left");
        AppendBoolEdges(sb, SteamVR_Actions.default_SnapTurnRight, hand, "snap_turn_right");
        if (sb.Length > 0 && sb[sb.Length - 1] == ';')
            sb.Length -= 1;
        return sb.ToString();
    }

    static void AppendBoolEdges(StringBuilder sb, SteamVR_Action_Boolean action, SteamVR_Input_Sources hand, string id)
    {
        if (action == null || !action.activeBinding)
            return;
        if (action.GetStateDown(hand))
            sb.Append(id).Append("_down;");
        if (action.GetStateUp(hand))
            sb.Append(id).Append("_up;");
    }

    static Transform GetTrackingRigOrigin()
    {
        if (SteamVR.initializedState != SteamVR.InitializedStates.InitializeSuccess)
            return null;
        var top = SteamVR_Render.Top();
        return top != null ? top.origin : null;
    }

    Transform GetTvTransformForSnapshot()
    {
        if (tvHeadMetricsAnchor != null)
            return tvHeadMetricsAnchor;
        return GameObject.FindGameObjectWithTag("TV")?.transform;
    }

    bool TryGetTvWorldPoint(out Vector3 tvPos)
    {
        var t = GetTvTransformForSnapshot();
        if (t != null)
        {
            tvPos = t.position;
            return true;
        }

        tvPos = default;
        return false;
    }

    static void AppendTransformBlock(StringBuilder sb, string key, Transform t)
    {
        sb.Append('"').Append(key).Append("\":");
        if (t == null)
        {
            sb.Append("null");
            return;
        }

        Vector3 p = t.position;
        Quaternion q = t.rotation;
        Vector3 ls = t.localScale;
        sb.Append('{');
        sb.Append("\"position\":[").Append(F(p.x)).Append(',').Append(F(p.y)).Append(',').Append(F(p.z)).Append("],");
        sb.Append("\"rotation\":[").Append(F(q.x)).Append(',').Append(F(q.y)).Append(',').Append(F(q.z)).Append(',').Append(F(q.w)).Append("],");
        sb.Append("\"local_scale\":[").Append(F(ls.x)).Append(',').Append(F(ls.y)).Append(',').Append(F(ls.z)).Append("],");
        sb.Append("\"name\":").Append(JsonString(t.name));
        sb.Append('}');
    }

    static string JsonString(string s)
    {
        if (s == null)
            return "\"\"";
        return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }

    void WriteSceneReferenceJsonFiles(string sessionDir)
    {
        try
        {
            var scene = SceneManager.GetActiveScene();
            var sb = new StringBuilder(1024);
            sb.Append('{');
            sb.Append("\"schema\":\"sc1_scene_rois v1\",");
            sb.Append("\"unity_scene_name\":").Append(JsonString(scene.name)).Append(',');
            sb.Append("\"unity_time_at_snapshot\":").Append(F(Time.time)).Append(',');
            AppendTransformBlock(sb, "tv", GetTvTransformForSnapshot());
            sb.Append(',');
            AppendTransformBlock(sb, "hmd_origin_at_session_start", gazeOriginCamera);
            sb.Append(',');
            Transform rig = GetTrackingRigOrigin();
            AppendTransformBlock(sb, "tracking_rig_origin", rig);
            sb.Append('}');
            File.WriteAllText(Path.Combine(sessionDir, "scene_rois.json"), sb.ToString(), new UTF8Encoding(false));

            const string roiTvTemplate = "{\"roi_name\":\"TV\",\"type\":\"polygon\",\"points\":[],\"note\":\"Normalized panorama UV polygon; fill after calibration or external tooling.\"}";
            File.WriteAllText(Path.Combine(sessionDir, "roi_tv.json"), roiTvTemplate, new UTF8Encoding(false));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Sc1LivingRoom: failed to write scene ROI JSON ({e.Message})");
        }
    }

    /// <summary>TV gaze events: valid↔valid and TV↔invalid so dwell is not inflated when tracking drops.</summary>
    static string EventForTransition(int prev, int curr)
    {
        if (prev == curr)
            return null;
        bool prevTv = prev == StateTv;
        bool currTv = curr == StateTv;
        if (currTv && !prevTv)
            return "enter_tv";
        if (!currTv && prevTv)
            return "exit_tv";
        return null;
    }

    static bool TryBuildGazeRay(GazeData gazeData, Transform pupilLocalToWorld, out Vector3 origin, out Vector3 direction)
    {
        if (gazeData.MappingContext == GazeData.GazeMappingContext.Binocular
            && gazeData.IsEyeDataAvailable(0) && gazeData.IsEyeDataAvailable(1))
        {
            Vector3 localEyeMid = (gazeData.EyeCenter0 + gazeData.EyeCenter1) * 0.5f;
            origin = pupilLocalToWorld.TransformPoint(localEyeMid);
            direction = pupilLocalToWorld.TransformDirection((gazeData.GazeNormal0 + gazeData.GazeNormal1).normalized);
            return true;
        }

        if (gazeData.IsEyeDataAvailable(0))
        {
            origin = pupilLocalToWorld.TransformPoint(gazeData.EyeCenter0);
            direction = pupilLocalToWorld.TransformDirection(gazeData.GazeNormal0.normalized);
            return true;
        }

        if (gazeData.IsEyeDataAvailable(1))
        {
            origin = pupilLocalToWorld.TransformPoint(gazeData.EyeCenter1);
            direction = pupilLocalToWorld.TransformDirection(gazeData.GazeNormal1.normalized);
            return true;
        }

        origin = pupilLocalToWorld.position;
        direction = pupilLocalToWorld.TransformDirection(gazeData.GazeDirection);
        return true;
    }

    static string CsvEscape(string s)
    {
        if (string.IsNullOrEmpty(s))
            return "";
        if (s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0)
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }

    static string F(double v) => v.ToString("G17", CultureInfo.InvariantCulture);
    static string F(float v) => v.ToString("G9", CultureInfo.InvariantCulture);

    void WriteTimeseriesRow(float sinceVideo, bool gazePacket, bool havePupilTs, double pupilTs, float conf, int valid, int state, string hitName, string hitTag, string invalidReason)
    {
        if (timeseriesWriter == null)
            return;
        string pCol = havePupilTs ? F(pupilTs) : "";
        string cCol = gazePacket ? F(conf) : "";
        timeseriesWriter.WriteLine(string.Join(",",
            F(sinceVideo),
            pCol,
            F(Time.time),
            state.ToString(CultureInfo.InvariantCulture),
            cCol,
            valid.ToString(CultureInfo.InvariantCulture),
            CsvEscape(hitName),
            CsvEscape(hitTag),
            CsvEscape(invalidReason)));
    }

    void WriteEventRow(float sinceVideo, bool havePupilTs, double pupilTs, float conf, int valid, string hitName, string hitTag, string invalidReason, string ev)
    {
        if (eventsWriter == null)
            return;
        string pCol = havePupilTs ? F(pupilTs) : "";
        eventsWriter.WriteLine(string.Join(",",
            F(sinceVideo),
            pCol,
            F(Time.time),
            CsvEscape(ev),
            F(conf),
            valid.ToString(CultureInfo.InvariantCulture),
            CsvEscape(hitName),
            CsvEscape(hitTag),
            CsvEscape(invalidReason)));
    }

    void MaybePeriodicFlushCsv()
    {
        if (timeseriesWriter == null && eventsWriter == null && headWriter == null && controllerWriter == null)
            return;
        if (Time.time - lastCsvFlushTime < csvFlushIntervalSeconds)
            return;
        lastCsvFlushTime = Time.time;
        timeseriesWriter?.Flush();
        eventsWriter?.Flush();
        headWriter?.Flush();
        controllerWriter?.Flush();
    }

    void OpenSessionCsvWriters()
    {
        CloseSessionCsvWriters();
        string dir = recorder.customPath;
        Directory.CreateDirectory(dir);

        timeseriesWriter = new StreamWriter(Path.Combine(dir, "gaze_timeseries.csv"), false, new UTF8Encoding(false)) { AutoFlush = false };
        eventsWriter = new StreamWriter(Path.Combine(dir, "gaze_events.csv"), false, new UTF8Encoding(false)) { AutoFlush = false };
        headWriter = new StreamWriter(Path.Combine(dir, "head_timeseries.csv"), false, new UTF8Encoding(false)) { AutoFlush = false };
        controllerWriter = new StreamWriter(Path.Combine(dir, "controller_timeseries.csv"), false, new UTF8Encoding(false)) { AutoFlush = false };

        timeseriesWriter.WriteLine("time_since_video_s,pupil_timestamp_s,unity_time_s,state,confidence,valid,hit_name,hit_tag,invalid_reason");
        eventsWriter.WriteLine("time_since_video_s,pupil_timestamp_s,unity_time_s,event,confidence,valid,hit_name,hit_tag,invalid_reason");
        headWriter.WriteLine("time_since_video_s,pupil_timestamp_s,unity_time_s,position_x,position_y,position_z,rotation_x,rotation_y,rotation_z,forward_x,forward_y,forward_z,up_x,up_y,up_z,vel_x,vel_y,vel_z,linear_speed,accel_lin_vec_mag,accel_linear,accel_angular,angular_speed,head_distance_to_tv,head_angle_to_tv");
        controllerWriter.WriteLine("time_since_video_s,pupil_timestamp_s,unity_time_s,hand,position_x,position_y,position_z,rotation_x,rotation_y,rotation_z,velocity_linear,velocity_angular,trigger_pressed,grip_pressed,button_events");

        lastCsvFlushTime = Time.time;
        timeseriesWriter.Flush();
        eventsWriter.Flush();
        headWriter.Flush();
        controllerWriter.Flush();
    }

    void CloseSessionCsvWriters()
    {
        if (timeseriesWriter != null)
        {
            timeseriesWriter.Flush();
            timeseriesWriter.Dispose();
            timeseriesWriter = null;
        }

        if (eventsWriter != null)
        {
            eventsWriter.Flush();
            eventsWriter.Dispose();
            eventsWriter = null;
        }

        if (headWriter != null)
        {
            headWriter.Flush();
            headWriter.Dispose();
            headWriter = null;
        }

        if (controllerWriter != null)
        {
            controllerWriter.Flush();
            controllerWriter.Dispose();
            controllerWriter = null;
        }
    }

    IEnumerator Post()
    {
        float tvDwell = lookedtvcount * period;
        float elseDwell = lookedelsecount * period;

        List<IMultipartFormSection> formData = new List<IMultipartFormSection>();
        formData.Add(new MultipartFormDataSection("username", UserName));
        formData.Add(new MultipartFormDataSection("lookedtvcount", lookedtvcount.ToString()));
        formData.Add(new MultipartFormDataSection("lookedtvtime", tvDwell.ToString("0.0", CultureInfo.InvariantCulture)));
        formData.Add(new MultipartFormDataSection("lookedelsecount", lookedelsecount.ToString()));
        formData.Add(new MultipartFormDataSection("lookedelsetime", elseDwell.ToString("0.0", CultureInfo.InvariantCulture)));

        string url = Constant.DOMAIN + Constant.SC1EyeTrackingData;

        UnityWebRequest www = UnityWebRequest.Post(url, formData);
        yield return www.SendWebRequest();
        if (www.result != UnityWebRequest.Result.Success)
            Debug.LogError(www.error);
    }

    new public void StartTask()
    {
        base.StartTask();
        // EEG.Instance.Init("Sc1LivingRoom");
        lookedtvcount = 0;
        lookedelsecount = 0;
        hasPreviousObservedState = false;
        previousObservedState = StateInvalid;
        headMotionPrimed = false;
        headKinTickCount = 0;
        headLastVelWorld = Vector3.zero;
        headLastLinSpeed = 0f;
        headLastAngSpeed = 0f;
        videoStartUnityTime = Time.time;
        nextActionTime = Time.time;

        OpenSessionCsvWriters();
        if (gazeOriginCamera != null)
            WriteSceneReferenceJsonFiles(recorder.customPath);
        csvGazeLogging = true;

        video.Play();
        foreach (var a in audios)
            a.Play();
    }

    void buttonIsClicked()
    {
        btnIsClicked = true;
    }

    IEnumerator EndTask()
    {
        csvGazeLogging = false;
        CloseSessionCsvWriters();

        recorder.StopRecording();
        StartCoroutine(Post());
        StartCoroutine(SetLevel(SceneType.Sc1Questionnaire));
        yield return new WaitForSeconds(1);
        NextScene();
    }
}
