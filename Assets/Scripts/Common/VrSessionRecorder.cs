using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using Unity.Collections;
using UnityEditor.Media;
#endif

/// <summary>
/// In-Unity session recorder (no external capture app).
/// In the Editor: writes an MP4 (H.264) with in-task game audio under Assets/Screen Recordings/.
/// Audio from AudioListener with recording-only gain; spatial sources (James, plane, cars, …) have
/// spatialize relaxed only while recording so they reach the mix. Works for Tasks 1–5.
/// Player builds fall back to JPG + WAV.
/// </summary>
public class VrSessionRecorder : MonoBehaviour
{
    public static VrSessionRecorder Instance { get; private set; }

    [Header("Capture")]
    [Tooltip("Camera to record. Leave empty to auto-pick: SteamVR HMD camera when VR is on, otherwise Camera.main (PC Game view).")]
    public Camera captureCamera;

    [Tooltip("Target encode frame rate. Keep modest (15–20) so capture can keep up — if encode FPS is higher than capture can manage, the MP4 looks sped-up and audio drifts.")]
    [Range(10, 30)] public int framesPerSecond = 20;

    [Tooltip("1 = full resolution (sharper; larger files).")]
    [Range(0.5f, 1f)] public float resolutionScale = 1f;

    [Range(10, 100)] public int jpegQuality = 95;

    [Tooltip("RenderTexture anti-aliasing for sharper capture (1, 2, 4, or 8).")]
    public int captureAntiAliasing = 4;

    [Header("Recording audio")]
    [Tooltip("Gain applied only to the MP4 mix (does not change headset volume).")]
    [Range(0.5f, 3f)] public float recordingAudioGain = 1.8f;

    [Header("Output")]
    [Tooltip("Folder under Application.dataPath (Assets/ in Editor).")]
    public string outputFolderName = "Screen Recordings";

    [Tooltip("Optional label included in the file name (e.g. Sc2a).")]
    public string sessionLabel = "";

    [Header("Behaviour")]
    public bool dontDestroyOnLoad = true;
    public bool autoStartOnEnable;
    public bool logPathsToConsole = true;

    public bool IsRecording { get; private set; }
    /// <summary>Folder that contains the session output.</summary>
    public string CurrentOutputDirectory { get; private set; }
    /// <summary>MP4 path in Editor, or folder path when using frame fallback.</summary>
    public string CurrentOutputFile { get; private set; }

    RenderTexture _rt;
    Texture2D _readback;
    Coroutine _loop;
    int _frameIndex;
    float _interval;
    VrSessionGameAudioTap _listenerTap;
    bool _listenerTapOwned;
    readonly List<AudioSource> _routedSources = new List<AudioSource>(8);
    readonly List<bool> _routedSpatializeRestore = new List<bool>(8);
    float[] _mixScratch;
    float _nextAudioTapRefreshTime;
    const float AudioTapRefreshIntervalSeconds = 0.5f;

#if UNITY_EDITOR
    MediaEncoder _encoder;
    NativeArray<float> _audioScratch;
    int _samplesPerVideoFrame;
    int _channelCount;
    int _audioSampleRate;
    double _audioSampleCarry;
    bool _useMp4;
#endif
    float _nextCaptureGameTime;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        if (dontDestroyOnLoad)
            DontDestroyOnLoad(gameObject);
    }

    void OnEnable()
    {
        if (autoStartOnEnable)
            StartRecording();
    }

    void OnDisable()
    {
        StopRecording();
    }

    void OnDestroy()
    {
        StopRecording();
        ReleaseBuffers();
        if (Instance == this)
            Instance = null;
    }

    /// <summary>Begin capturing. Safe to call if already recording.</summary>
    public void StartRecording(string labelOverride = null)
    {
        if (IsRecording)
            return;

        if (!ResolveCamera())
            return;

        string label = !string.IsNullOrEmpty(labelOverride) ? labelOverride : sessionLabel;
        if (string.IsNullOrEmpty(label))
            label = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string user = LevelScript.HasParticipantIdentity()
            ? $"{LevelScript.UserGroup}_{LevelScript.UserName}"
            : "local";
        string baseName = $"{stamp}_{label}_{Sanitize(user)}";

        CurrentOutputDirectory = Path.Combine(Application.dataPath, outputFolderName);
        try
        {
            Directory.CreateDirectory(CurrentOutputDirectory);
        }
        catch (Exception e)
        {
            Debug.LogError($"VrSessionRecorder: cannot create output folder ({e.Message})");
            return;
        }

        EnsureBuffers();
        AttachAudioTaps();
        _nextAudioTapRefreshTime = Time.unscaledTime + AudioTapRefreshIntervalSeconds;

        _frameIndex = 0;
        _interval = 1f / Mathf.Max(1, framesPerSecond);
        _nextCaptureGameTime = Time.time;

#if UNITY_EDITOR
        _useMp4 = TryStartMp4Encoder(Path.Combine(CurrentOutputDirectory, baseName + ".mp4"));
        if (_useMp4)
            CurrentOutputFile = Path.Combine(CurrentOutputDirectory, baseName + ".mp4");
        else
#endif
        {
            // Player / encoder unavailable: JPG frames + WAV of game audio.
            CurrentOutputDirectory = Path.Combine(CurrentOutputDirectory, baseName);
            Directory.CreateDirectory(CurrentOutputDirectory);
            CurrentOutputFile = CurrentOutputDirectory;
            File.WriteAllText(
                Path.Combine(CurrentOutputDirectory, "readme.txt"),
                "Fallback JPG + WAV session (MP4 encoder unavailable in this build).\n" +
                $"fps={framesPerSecond}\nscale={resolutionScale}\n" +
                $"camera={captureCamera.name}\nstarted={DateTime.Now:o}\n");
        }

        IsRecording = true;
        _loop = StartCoroutine(CaptureLoop());

        if (logPathsToConsole)
            Debug.Log($"VrSessionRecorder: recording → {CurrentOutputFile}");
    }

    public void StopRecording()
    {
        if (!IsRecording)
            return;

        IsRecording = false;
        if (_loop != null)
        {
            StopCoroutine(_loop);
            _loop = null;
        }

#if UNITY_EDITOR
        if (_useMp4)
            FinishMp4Encoder();
        else
#endif
            FinishWavFallback();

        DetachAudioTaps();

        if (logPathsToConsole)
            Debug.Log($"VrSessionRecorder: stopped. Frames={_frameIndex}. Output={CurrentOutputFile}");
    }

    bool ResolveCamera()
    {
        Camera resolved = ResolvePreferredCamera(captureCamera);
        if (resolved == null)
        {
            Debug.LogError("VrSessionRecorder: no camera to capture (need HMD / Camera.main / any enabled Camera).");
            return false;
        }

        if (captureCamera != resolved)
            captureCamera = resolved;

        if (logPathsToConsole)
        {
            bool xr = IsXrActive();
            Debug.Log($"VrSessionRecorder: capture camera='{captureCamera.name}' mode={(xr ? "VR HMD view" : "PC / Game view camera")}.");
        }
        return true;
    }

    /// <summary>
    /// Prefer the headset camera when VR is running (what the participant looks at).
    /// On PC without HMD, use Camera.main (same camera that drives the Game view).
    /// This is a camera render, not a desktop screenshot of the Editor window.
    /// </summary>
    static Camera ResolvePreferredCamera(Camera current)
    {
        if (IsUsableCamera(current))
            return current;

        // SteamVR HMD camera (participant view in headset).
        try
        {
            var top = Valve.VR.SteamVR_Render.Top();
            if (top != null)
            {
                Camera steamCam = top.camera != null ? top.camera : top.GetComponent<Camera>();
                if (IsUsableCamera(steamCam))
                    return steamCam;
            }
        }
        catch
        {
            // SteamVR not initialised — fall through.
        }

        var steamVrCams = FindObjectsOfType<Valve.VR.SteamVR_Camera>();
        for (int i = 0; i < steamVrCams.Length; i++)
        {
            if (steamVrCams[i] == null)
                continue;
            Camera c = steamVrCams[i].camera != null
                ? steamVrCams[i].camera
                : steamVrCams[i].GetComponent<Camera>();
            if (IsUsableCamera(c))
                return c;
        }

        // PC test / no HMD: main camera (typically the Game view).
        if (IsUsableCamera(Camera.main))
            return Camera.main;

        // Camera that owns the AudioListener is usually the player/HMD cam.
        AudioListener listener = FindObjectOfType<AudioListener>();
        if (listener != null)
        {
            Camera onListener = listener.GetComponent<Camera>();
            if (IsUsableCamera(onListener))
                return onListener;
        }

        Camera[] cams = FindObjectsOfType<Camera>();
        for (int i = 0; i < cams.Length; i++)
        {
            if (IsUsableCamera(cams[i]))
                return cams[i];
        }
        return null;
    }

    static bool IsUsableCamera(Camera c)
    {
        return c != null && c.enabled && c.gameObject.activeInHierarchy;
    }

    static bool IsXrActive()
    {
#if UNITY_2017_2_OR_NEWER
        try
        {
            return UnityEngine.XR.XRSettings.enabled
                && UnityEngine.XR.XRSettings.isDeviceActive;
        }
        catch
        {
            return false;
        }
#else
        return false;
#endif
    }

    /// <summary>
    /// Capture only via AudioListener (reliable). Do not put OnAudioFilterRead on voice
    /// AudioSources — that can silence them. While recording, turn off Unity spatialize on
    /// spatial sources (James, plane, cars, …) so they reach the listener mix; spatialBlend /
    /// distance stay 3D. Restored when recording stops. Applies to Tasks 2 and 3 (and any scene).
    /// </summary>
    void AttachAudioTaps()
    {
        DetachAudioTaps();

        var leftover = FindObjectsOfType<VrSessionGameAudioTap>();
        for (int i = 0; i < leftover.Length; i++)
        {
            if (leftover[i] != null)
                Destroy(leftover[i]);
        }

        AudioListener listener = FindObjectOfType<AudioListener>();
        if (listener == null)
        {
            Debug.LogWarning("VrSessionRecorder: no AudioListener — cannot capture game audio mix.");
            return;
        }

        _listenerTap = listener.gameObject.AddComponent<VrSessionGameAudioTap>();
        _listenerTapOwned = true;
        _listenerTap.Clear();

        ApplySpatialRecordingRouting();
        if (logPathsToConsole)
            Debug.Log($"VrSessionRecorder: listener mix ON, spatial sources routed for capture={_routedSources.Count}, gain={recordingAudioGain:0.##}.");
    }

    void ApplySpatialRecordingRouting()
    {
        RestoreSpatialRecordingRouting();

        // Explicit Task 2 / 3 sources (in case spatialize flag is off but they still need routing).
        var lectures = FindObjectsOfType<AnimAndImage>();
        for (int i = 0; i < lectures.Length; i++)
        {
            AnimAndImage a = lectures[i];
            if (a == null)
                continue;
            RouteSourceForRecording(a.lecturerVoice);
            RouteSourceForRecording(a.paperPlaneSource);
        }

        var planes = FindObjectsOfType<PlaneMove>();
        for (int i = 0; i < planes.Length; i++)
        {
            if (planes[i] != null)
                RouteSourceForRecording(planes[i].PlaneSource);
        }

        // All AudioSources in the scene (Tasks 4–5 phone/bar, cars spawned mid-task, etc.).
        AudioSource[] sources = FindObjectsOfType<AudioSource>();
        for (int i = 0; i < sources.Length; i++)
            RouteSourceForRecording(sources[i]);
    }

    void RouteSourceForRecording(AudioSource src)
    {
        if (src == null || _routedSources.Contains(src))
            return;

#if UNITY_2017_1_OR_NEWER
        _routedSources.Add(src);
        _routedSpatializeRestore.Add(src.spatialize);
        // Keep spatialBlend/distance; disable HRTF spatialize so the clip reaches AudioListener cleanly.
        src.spatialize = false;
        src.spatializePostEffects = false;
#else
        _routedSources.Add(src);
        _routedSpatializeRestore.Add(false);
#endif
        src.bypassListenerEffects = false;
        src.mute = false;
        src.enabled = true;
    }

    void RestoreSpatialRecordingRouting()
    {
#if UNITY_2017_1_OR_NEWER
        for (int i = 0; i < _routedSources.Count; i++)
        {
            AudioSource src = _routedSources[i];
            if (src == null)
                continue;
            src.spatialize = _routedSpatializeRestore[i];
        }
#endif
        _routedSources.Clear();
        _routedSpatializeRestore.Clear();
    }

    void DetachAudioTaps()
    {
        RestoreSpatialRecordingRouting();

        if (_listenerTap != null && _listenerTapOwned)
            Destroy(_listenerTap);
        _listenerTap = null;
        _listenerTapOwned = false;
    }

    void MixAudioInto(float[] dest, int count)
    {
        for (int i = 0; i < count; i++)
            dest[i] = 0f;

        if (_listenerTap != null)
            _listenerTap.MixInto(dest, count);

        float gain = Mathf.Max(0.01f, recordingAudioGain);
        if (Mathf.Abs(gain - 1f) < 0.001f)
            return;

        for (int i = 0; i < count; i++)
            dest[i] = Mathf.Clamp(dest[i] * gain, -1f, 1f);
    }

    IEnumerator CaptureLoop()
    {
        // Clock the encode timeline to game time (same clock as Julie / VideoPlayer / task audio).
        // If capture falls behind, duplicate the latest frame and still write audio for each slot
        // so the MP4 duration matches the task (no sped-up video / lip desync).
        while (IsRecording)
        {
            if (Time.timeScale <= 0f)
            {
                yield return null;
                continue;
            }

            while (IsRecording && Time.time < _nextCaptureGameTime)
                yield return null;

            if (!IsRecording)
                yield break;

            yield return new WaitForEndOfFrame();
            if (!IsRecording || captureCamera == null)
                continue;

            if (Time.unscaledTime >= _nextAudioTapRefreshTime)
            {
                // Re-apply if lecture/cars re-enable spatialize, and pick up newly spawned Task 3 sources.
                ApplySpatialRecordingRouting();
                Camera preferred = ResolvePreferredCamera(null);
                if (preferred != null && preferred != captureCamera)
                {
                    captureCamera = preferred;
                    EnsureBuffers();
                    if (logPathsToConsole)
                        Debug.Log($"VrSessionRecorder: switched capture camera → '{captureCamera.name}'.");
                }
                _nextAudioTapRefreshTime = Time.unscaledTime + AudioTapRefreshIntervalSeconds;
            }

            int slots = 0;
            while (_nextCaptureGameTime <= Time.time + 0.0001f)
            {
                _nextCaptureGameTime += _interval;
                slots++;
                if (slots >= 8)
                    break;
            }
            if (slots < 1)
                slots = 1;

            try
            {
                CaptureImageToReadback();
                for (int s = 0; s < slots; s++)
                    EncodeCurrentFrame();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"VrSessionRecorder: frame failed ({e.Message})");
            }
        }
    }

    void EnsureBuffers()
    {
        ReleaseBuffers();

        int w = Mathf.Max(16, Mathf.RoundToInt(Screen.width * resolutionScale));
        int h = Mathf.Max(16, Mathf.RoundToInt(Screen.height * resolutionScale));
#if UNITY_2017_2_OR_NEWER
        if (UnityEngine.XR.XRSettings.enabled && UnityEngine.XR.XRSettings.eyeTextureWidth > 0)
        {
            w = Mathf.Max(w, Mathf.RoundToInt(UnityEngine.XR.XRSettings.eyeTextureWidth * resolutionScale));
            h = Mathf.Max(h, Mathf.RoundToInt(UnityEngine.XR.XRSettings.eyeTextureHeight * resolutionScale));
        }
#endif
        w &= ~1;
        h &= ~1;
        w = Mathf.Max(16, w);
        h = Mathf.Max(16, h);

        int aa = captureAntiAliasing;
        if (aa != 2 && aa != 4 && aa != 8)
            aa = 1;

        _rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
        _rt.antiAliasing = aa;
        _rt.Create();
        _readback = new Texture2D(w, h, TextureFormat.RGBA32, false);
        _readback.filterMode = FilterMode.Bilinear;
    }

    void ReleaseBuffers()
    {
        if (_rt != null)
        {
            _rt.Release();
            Destroy(_rt);
            _rt = null;
        }
        if (_readback != null)
        {
            Destroy(_readback);
            _readback = null;
        }
    }

    void CaptureImageToReadback()
    {
        if (_rt == null || _readback == null)
            EnsureBuffers();

        RenderTexture prev = captureCamera.targetTexture;
        captureCamera.targetTexture = _rt;
        captureCamera.Render();
        captureCamera.targetTexture = prev;

        RenderTexture prevActive = RenderTexture.active;
        RenderTexture.active = _rt;
        _readback.ReadPixels(new Rect(0, 0, _rt.width, _rt.height), 0, 0);
        _readback.Apply(false, false);
        RenderTexture.active = prevActive;
    }

    void EncodeCurrentFrame()
    {
#if UNITY_EDITOR
        if (_useMp4 && _encoder != null)
        {
            _encoder.AddFrame(_readback);
            WriteAudioForOneVideoFrame();
            _frameIndex++;
            return;
        }
#endif
        byte[] jpg = _readback.EncodeToJPG(jpegQuality);
        string path = Path.Combine(CurrentOutputDirectory, $"frame_{_frameIndex:D6}.jpg");
        File.WriteAllBytes(path, jpg);
        _frameIndex++;
    }

#if UNITY_EDITOR
    bool TryStartMp4Encoder(string mp4Path)
    {
        try
        {
            if (File.Exists(mp4Path))
                File.Delete(mp4Path);

            _channelCount = AudioSettings.speakerMode == AudioSpeakerMode.Mono ? 1 : 2;
            _audioSampleRate = AudioSettings.outputSampleRate;
            if (_audioSampleRate <= 0)
                _audioSampleRate = 48000;
            _audioSampleCarry = 0;

            int fps = Mathf.Max(1, framesPerSecond);
            var videoAttr = new VideoTrackAttributes
            {
                frameRate = new MediaRational(fps),
                width = (uint)_readback.width,
                height = (uint)_readback.height,
                includeAlpha = false
            };
            var audioAttr = new AudioTrackAttributes
            {
                sampleRate = new MediaRational(_audioSampleRate),
                channelCount = (ushort)_channelCount,
                language = string.Empty
            };

            // Max interleaved samples for one frame (+1 for rounding carry).
            _samplesPerVideoFrame = Mathf.Max(1, _channelCount * ((_audioSampleRate + fps - 1) / fps + 1));
            if (_audioScratch.IsCreated)
                _audioScratch.Dispose();
            _audioScratch = new NativeArray<float>(_samplesPerVideoFrame, Allocator.Persistent);

            _encoder = new MediaEncoder(mp4Path, videoAttr, audioAttr);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"VrSessionRecorder: MP4 encoder failed ({e.Message}); falling back to JPG+WAV.");
            CleanupMp4Encoder();
            return false;
        }
    }

    void WriteAudioForOneVideoFrame()
    {
        if (!_audioScratch.IsCreated || _encoder == null)
            return;

        // Exact average sample count per video frame (avoids long-term A/V drift).
        int fps = Mathf.Max(1, framesPerSecond);
        _audioSampleCarry += (double)_audioSampleRate * _channelCount / fps;
        int count = (int)_audioSampleCarry;
        _audioSampleCarry -= count;
        count = Mathf.Clamp(count, 1, _audioScratch.Length);

        // Mix into a temp region then copy — MixAudioInto expects exact length.
        if (_mixScratch == null || _mixScratch.Length < count)
            _mixScratch = new float[count];
        MixAudioInto(_mixScratch, count);
        for (int i = 0; i < count; i++)
            _audioScratch[i] = _mixScratch[i];

        // MediaEncoder expects a NativeArray of the samples to append.
        NativeArray<float> slice = _audioScratch.GetSubArray(0, count);
        _encoder.AddSamples(slice);
    }

    void FinishMp4Encoder()
    {
        CleanupMp4Encoder();
    }

    void CleanupMp4Encoder()
    {
        if (_encoder != null)
        {
            try { _encoder.Dispose(); }
            catch (Exception e) { Debug.LogWarning($"VrSessionRecorder: encoder dispose ({e.Message})"); }
            _encoder = null;
        }
        if (_audioScratch.IsCreated)
            _audioScratch.Dispose();
        _useMp4 = false;
    }
#endif

    void FinishWavFallback()
    {
        if (string.IsNullOrEmpty(CurrentOutputDirectory) || _listenerTap == null)
            return;
        try
        {
            int channels = AudioSettings.speakerMode == AudioSpeakerMode.Mono ? 1 : 2;
            int rate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 48000;
            float[] samples = _listenerTap.TakeAll();
            if (samples == null || samples.Length == 0)
                return;
            string wavPath = Path.Combine(CurrentOutputDirectory, "game_audio.wav");
            WriteWav(wavPath, samples, channels, rate);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"VrSessionRecorder: WAV save failed ({e.Message})");
        }
    }

    static void WriteWav(string path, float[] samples, int channels, int sampleRate)
    {
        int sampleCount = samples.Length;
        short[] pcm = new short[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            float v = Mathf.Clamp(samples[i], -1f, 1f);
            pcm[i] = (short)Mathf.RoundToInt(v * short.MaxValue);
        }

        int byteRate = sampleRate * channels * 2;
        int dataSize = pcm.Length * 2;
        using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
        using (var bw = new BinaryWriter(fs))
        {
            bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            bw.Write(36 + dataSize);
            bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
            bw.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            bw.Write(16);
            bw.Write((short)1);
            bw.Write((short)channels);
            bw.Write(sampleRate);
            bw.Write(byteRate);
            bw.Write((short)(channels * 2));
            bw.Write((short)16);
            bw.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            bw.Write(dataSize);
            for (int i = 0; i < pcm.Length; i++)
                bw.Write(pcm[i]);
        }
    }

    static string Sanitize(string s)
    {
        if (string.IsNullOrEmpty(s))
            return "unknown";
        foreach (char c in Path.GetInvalidFileNameChars())
            s = s.Replace(c, '_');
        return s;
    }
}

/// <summary>
/// Taps game audio for recording (not the PC microphone).
/// On AudioListener: final mix. On AudioSource: dry source signal (before 3D distance fade).
/// </summary>
public class VrSessionGameAudioTap : MonoBehaviour
{
    readonly object _gate = new object();
    readonly List<float> _pending = new List<float>(8192);

    void OnAudioFilterRead(float[] data, int channels)
    {
        if (data == null || data.Length == 0)
            return;
        lock (_gate)
        {
            for (int i = 0; i < data.Length; i++)
                _pending.Add(data[i]);
        }
    }

    public void Clear()
    {
        lock (_gate)
            _pending.Clear();
    }

    /// <summary>Drain up to count samples and add them into dest (dest should start at 0 for first tap).</summary>
    public void MixInto(float[] dest, int count)
    {
        lock (_gate)
        {
            int n = Mathf.Min(count, _pending.Count);
            for (int i = 0; i < n; i++)
                dest[i] += _pending[i];
            if (n > 0)
                _pending.RemoveRange(0, n);
        }
    }

    public float[] TakeAll()
    {
        lock (_gate)
        {
            var arr = _pending.ToArray();
            _pending.Clear();
            return arr;
        }
    }
}
