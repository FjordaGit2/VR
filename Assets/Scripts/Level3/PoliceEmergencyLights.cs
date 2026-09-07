using UnityEngine;

/// <summary>
/// Visible flashing red/blue lenses on the police roof light bar only.
/// Uses Unlit rectangular lenses (readable in daylight). Point Lights stay off so the road is not lit.
/// </summary>
[DisallowMultipleComponent]
public class PoliceEmergencyLights : MonoBehaviour
{
    [SerializeField] Transform redAnchor;
    [SerializeField] Transform blueAnchor;
    [SerializeField] Light redLight;
    [SerializeField] Light blueLight;

    [Min(0.05f)] public float flashIntervalSeconds = 0.28f;

    [Tooltip("Width / height / depth of each light-bar lens (car-local).")]
    public Vector3 lensSize = new Vector3(0.2f, 0.055f, 0.12f);

    [Tooltip("Lift lenses slightly above the light-bar mesh so they are not hidden inside it.")]
    public float lensHeightOffset = 0.05f;

    [Tooltip("How bright the lenses look when flashing (Unlit color multiplier).")]
    [Min(1f)] public float lensIntensity = 2.6f;

    static readonly Color RedOn = new Color(1f, 0.22f, 0.1f, 1f);
    static readonly Color BlueOn = new Color(0.2f, 0.55f, 1f, 1f);
    static readonly Color Off = new Color(0.1f, 0.1f, 0.12f, 1f);
    static readonly Color Housing = new Color(0.04f, 0.04f, 0.05f, 1f);

    Renderer _redLens;
    Renderer _blueLens;
    Material _redMat;
    Material _blueMat;
    Material _housingMat;
    float _nextSwapTime;
    bool _redOn = true;

    void Awake()
    {
        ResolveAnchors();
        DisableSceneLight(redLight);
        DisableSceneLight(blueLight);

        EnsureLightBarHousing();

        if (redAnchor != null)
            _redLens = CreateUnlitLens(redAnchor, "RedLens", out _redMat);
        if (blueAnchor != null)
            _blueLens = CreateUnlitLens(blueAnchor, "BlueLens", out _blueMat);

        _nextSwapTime = Time.unscaledTime + flashIntervalSeconds;
        ApplyFlashState();
    }

    void Update()
    {
        if (Time.unscaledTime < _nextSwapTime)
            return;
        _nextSwapTime = Time.unscaledTime + flashIntervalSeconds;
        _redOn = !_redOn;
        ApplyFlashState();
    }

    void OnDestroy()
    {
        if (_redMat != null)
            Destroy(_redMat);
        if (_blueMat != null)
            Destroy(_blueMat);
        if (_housingMat != null)
            Destroy(_housingMat);
    }

    void ResolveAnchors()
    {
        if (redAnchor == null)
        {
            redAnchor = transform.Find("RedLight");
            if (redAnchor != null)
                redLight = redAnchor.GetComponent<Light>();
        }
        else if (redLight == null)
        {
            redLight = redAnchor.GetComponent<Light>();
        }

        if (blueAnchor == null)
        {
            blueAnchor = transform.Find("BlueLight");
            if (blueAnchor != null)
                blueLight = blueAnchor.GetComponent<Light>();
        }
        else if (blueLight == null)
        {
            blueLight = blueAnchor.GetComponent<Light>();
        }
    }

    static void DisableSceneLight(Light light)
    {
        if (light == null)
            return;
        light.enabled = false;
        light.intensity = 0f;
        light.range = 0.01f;
    }

    void EnsureLightBarHousing()
    {
        if (redAnchor == null || blueAnchor == null)
            return;

        Vector3 redLocal = transform.InverseTransformPoint(redAnchor.position);
        Vector3 blueLocal = transform.InverseTransformPoint(blueAnchor.position);
        Vector3 mid = (redLocal + blueLocal) * 0.5f;
        mid.y += lensHeightOffset * 0.35f;

        float spanX = Mathf.Abs(redLocal.x - blueLocal.x) + lensSize.x * 0.85f;
        var housing = GameObject.CreatePrimitive(PrimitiveType.Cube);
        housing.name = "LightBarHousing";
        housing.layer = gameObject.layer;
        housing.transform.SetParent(transform, false);
        housing.transform.localPosition = mid;
        housing.transform.localRotation = Quaternion.identity;
        housing.transform.localScale = new Vector3(
            Mathf.Max(spanX, lensSize.x * 2.2f),
            lensSize.y * 0.55f,
            lensSize.z * 1.15f);

        var col = housing.GetComponent<Collider>();
        if (col != null)
            Destroy(col);

        _housingMat = CreateUnlitMaterial(Housing);
        var renderer = housing.GetComponent<Renderer>();
        renderer.sharedMaterial = _housingMat;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    Renderer CreateUnlitLens(Transform anchor, string name, out Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.layer = gameObject.layer;
        go.transform.SetParent(transform, false);

        Vector3 local = transform.InverseTransformPoint(anchor.position);
        local.y += lensHeightOffset;
        go.transform.localPosition = local;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = lensSize;

        var col = go.GetComponent<Collider>();
        if (col != null)
            Destroy(col);

        mat = CreateUnlitMaterial(Off);
        var renderer = go.GetComponent<Renderer>();
        renderer.sharedMaterial = mat;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.enabled = true;
        return renderer;
    }

    static Material CreateUnlitMaterial(Color color)
    {
        Shader shader = Shader.Find("Unlit/Color");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Standard");

        var mat = new Material(shader);
        mat.color = color;
        return mat;
    }

    void ApplyFlashState()
    {
        if (redLight != null)
            redLight.enabled = false;
        if (blueLight != null)
            blueLight.enabled = false;

        if (_redMat != null)
            _redMat.color = _redOn ? RedOn * lensIntensity : Off;
        if (_blueMat != null)
            _blueMat.color = _redOn ? Off : BlueOn * lensIntensity;

        if (_redLens != null)
            _redLens.enabled = true;
        if (_blueLens != null)
            _blueLens.enabled = true;
    }
}
