using UnityEngine;

/// <summary>
/// Makes a flat plane read as water: scrolls two normal-map layers in different
/// directions and gently bobs the surface.
///
/// Two layers moving at different speeds and angles is the whole trick. One
/// scrolling layer reads as a sliding texture; two crossing layers read as
/// moving water, because the interference pattern never repeats visibly.
///
/// Setup: put this on the water plane. It needs a material using
/// Universal Render Pipeline/Lit with a Normal Map assigned.
/// </summary>
[RequireComponent(typeof(Renderer))]
public class WaterSurface : MonoBehaviour
{
    [Header("Scroll")]
    [Tooltip("Direction and speed of the main swell, in UV units per second.")]
    public Vector2 primaryScroll = new(0.015f, 0.010f);

    [Tooltip("Second layer. Deliberately a different angle and speed.")]
    public Vector2 secondaryScroll = new(-0.009f, 0.017f);

    [Tooltip("Tiling of the normal map across the plane. Higher = smaller ripples.")]
    public float normalTiling = 12f;

    [Header("Bob")]
    [Tooltip("How far the whole surface rises and falls. Keep tiny.")]
    public float bobHeight = 0.06f;
    public float bobSpeed = 0.5f;

    [Header("Shore foam (optional)")]
    [Tooltip("Slight colour lightening as the surface rises, fakes a tide feel.")]
    public bool subtleTide = true;

    private Material _mat;
    private float _baseY;
    private Vector2 _offsetA;
    private Vector2 _offsetB;

    private static readonly int BumpMap = Shader.PropertyToID("_BumpMap");
    private static readonly int DetailNormal = Shader.PropertyToID("_DetailNormalMap");

    private void Awake()
    {
        // Instance the material so scrolling this water does not scroll every
        // other object sharing the same material asset.
        _mat = GetComponent<Renderer>().material;
        _baseY = transform.position.y;

        if (_mat.HasProperty(BumpMap))
            _mat.SetTextureScale(BumpMap, Vector2.one * normalTiling);

        if (_mat.HasProperty(DetailNormal))
            _mat.SetTextureScale(DetailNormal, Vector2.one * normalTiling * 1.7f);
    }

    private void Update()
    {
        _offsetA += primaryScroll * Time.deltaTime;
        _offsetB += secondaryScroll * Time.deltaTime;

        // Wrap so the floats never grow large enough to lose precision.
        _offsetA.x %= 1f; _offsetA.y %= 1f;
        _offsetB.x %= 1f; _offsetB.y %= 1f;

        if (_mat.HasProperty(BumpMap)) _mat.SetTextureOffset(BumpMap, _offsetA);
        if (_mat.HasProperty(DetailNormal)) _mat.SetTextureOffset(DetailNormal, _offsetB);

        if (bobHeight > 0f)
        {
            var p = transform.position;
            p.y = _baseY + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            transform.position = p;
        }
    }

    private void OnDisable()
    {
        // Leave it where it started if the component gets switched off.
        var p = transform.position;
        p.y = _baseY;
        transform.position = p;
    }
}
