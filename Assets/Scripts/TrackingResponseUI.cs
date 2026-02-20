using UnityEngine;
using TMPro;

/// <summary>
/// Draws a semicircular arc (−90° to +90°) with a moving indicator dot
/// entirely in 3D world space — no Canvas or UI components required.
///
/// The arc is built from a LineRenderer (track) and a small sphere
/// (indicator) that slides along it based on trackingResponse.
///
/// Setup:
///   1. Create an empty GameObject as a child of Screen (e.g. "TrackingArc").
///   2. Add this component to it.
///   3. Position/scale the GameObject on the Screen face as needed.
///   4. Drag it into runContinuousExperiment → Tracking Response UI slot.
///
/// The arc lies in the GameObject's local XY plane (facing local +Z).
/// Scale the GameObject uniformly to resize the whole widget.
/// </summary>
public class TrackingResponseUI : MonoBehaviour
{
    // ──────────────────────────────────────────────────────────────────
    //  Inspector parameters
    // ──────────────────────────────────────────────────────────────────

    [Header("Arc geometry")]
    [Tooltip("Radius of the arc in local units.")]
    [SerializeField] private float arcRadius = 0.3f;
    [Tooltip("Number of line segments used to draw the arc (smoothness).")]
    [SerializeField] private int arcSegments = 48;
    [Tooltip("Width of the arc LineRenderer.")]
    [SerializeField] private float arcLineWidth = 0.015f;

    [Header("Indicator dot")]
    [Tooltip("Scale of the sphere indicator (local units).")]
    [SerializeField] private float dotSize = 0.04f;

    [Header("Colours")]
    [SerializeField] private Color arcTrackColor   = new Color(0.35f, 0.35f, 0.35f, 1f);
    [SerializeField] private Color neutralTickColor = new Color(0.7f,  0.7f,  0.7f, 1f);
    [SerializeField] private Color dotColor         = new Color(1f,   0.65f,  0f,   1f); // orange
    [SerializeField] private Color labelColor       = new Color(0.95f, 0.95f, 0.95f, 1f);

    [Header("Labels")]
    [Tooltip("Font size of the Slower / Faster / Neutral labels in world units.")]
    [SerializeField] private float labelSize = 0.06f;

    [Header("Label offsets (local, relative to computed arc position)")]
    [Tooltip("Extra offset applied to the 'Slower' label position.")]
    [SerializeField] private Vector3 slowerLabelOffset   = Vector3.zero;
    [Tooltip("Extra offset applied to the 'Faster' label position.")]
    [SerializeField] private Vector3 fasterLabelOffset   = Vector3.zero;
    [Tooltip("Extra offset applied to the 'Neutral' label position.")]
    [SerializeField] private Vector3 neutralLabelOffset  = Vector3.zero;
    [Tooltip("Extra offset applied to the instruction label position.")]
    [SerializeField] private Vector3 instrLabelOffset    = Vector3.zero;

    // ──────────────────────────────────────────────────────────────────
    //  Private state
    // ──────────────────────────────────────────────────────────────────

    [Header("Debug / wiring")]
    [Tooltip("Auto-filled by runContinuousExperiment.Awake(). Can also drag manually.")]
    public ContinuousControllerInput controllerInput;
    public bool playinVR;

    private Transform dotTransform;

    // Label transforms — kept so offsets can be live-tweaked in the Inspector
    private Transform labelSlowerTf;
    private Transform labelFasterTf;
    private Transform labelNeutralTf;
    private Transform labelInstrTf;

    // Cached base positions (before offset) so Update() can reapply correctly
    private Vector3 baseSlowerPos;
    private Vector3 baseFasterPos;
    private Vector3 baseNeutralPos;
    private Vector3 baseInstrPos;

    // Arc spans ±90° from the top of the circle
    private const float ArcHalfSpanDeg = 90f;

    // ──────────────────────────────────────────────────────────────────
    //  Lifecycle
    // ──────────────────────────────────────────────────────────────────

    void Start()
    {
        // controllerInput and playinVR are injected by runContinuousExperiment
        // before this Start() runs. Log a warning if somehow missed.
        if (controllerInput == null)
            Debug.LogWarning("[TrackingResponseUI] controllerInput not set — " +
                             "check runContinuousExperiment is injecting it in Awake().");

        Build();
        gameObject.SetActive(false); // hidden until practice trial starts
    }

    void Update()
    {
        if (controllerInput == null || dotTransform == null) return;
        float r = controllerInput.trackingResponse;
        MoveDot(r);

        // Reapply label offsets every frame so Inspector tweaks are visible live
        ApplyLabelOffsets();
    }

    // ──────────────────────────────────────────────────────────────────
    //  Public API
    // ──────────────────────────────────────────────────────────────────

    public void Show() => gameObject.SetActive(true);
    public void Hide() => gameObject.SetActive(false);

    // ──────────────────────────────────────────────────────────────────
    //  Dot movement
    // ──────────────────────────────────────────────────────────────────

    void MoveDot(float response)
    {
        float angleDeg = response * ArcHalfSpanDeg;
        Vector3 pos = ArcPoint(angleDeg);
        pos.z = dotZOffset;   // keep dot in front of arc on both sides
        dotTransform.localPosition = pos;
    }

    // Returns a local-space point on the arc at the given angle from top (+Y),
    // rotating clockwise (toward +X for positive angles).
    // z offset pulls everything slightly in front of the parent mesh surface.
    // Negative = toward viewer (in front of Screen mesh and arc line).
    // Arc line uses zOffset too so it sits in front of the Screen mesh.
    // Dot uses dotZOffset (more negative) so it always renders on top of the arc.
    [SerializeField] private float zOffset    = -0.02f;   // arc track Z
    [SerializeField] private float dotZOffset = -0.04f;   // dot Z (in front of arc)

    Vector3 ArcPoint(float angleDeg)
    {
        float rad = angleDeg * Mathf.Deg2Rad;
        return new Vector3(
            arcRadius * Mathf.Sin(rad),   // X: right = positive
            arcRadius * Mathf.Cos(rad),   // Y: up = neutral
            zOffset                       // Z: in front of Screen mesh
        );
    }

    // ──────────────────────────────────────────────────────────────────
    //  Build all 3D elements
    // ──────────────────────────────────────────────────────────────────

    void Build()
    {
        BuildArcTrack();
        BuildNeutralTick();
        BuildEndTicks();
        BuildDot();
        BuildLabels();
    }

    // ── Arc track (LineRenderer semicircle) ──────────────────────────
    void BuildArcTrack()
    {
        GameObject arcGO = new GameObject("ArcTrack");
        arcGO.transform.SetParent(transform, false);

        LineRenderer lr = arcGO.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = false;
        lr.startWidth = arcLineWidth;
        lr.endWidth   = arcLineWidth;
        lr.positionCount = arcSegments + 1;
        lr.material = CreateUnlitMaterial(arcTrackColor);
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        // View alignment ensures quads always face the camera symmetrically,
        // preventing the right-side occlusion caused by Transform alignment.
        lr.alignment = LineAlignment.View;

        // Fill positions from −90° to +90°
        for (int i = 0; i <= arcSegments; i++)
        {
            float t = (float)i / arcSegments;                  // 0 → 1
            float angleDeg = Mathf.Lerp(-ArcHalfSpanDeg, ArcHalfSpanDeg, t);
            lr.SetPosition(i, ArcPoint(angleDeg));
        }
    }

    // ── Neutral tick at top of arc ───────────────────────────────────
    void BuildNeutralTick()
    {
        Vector3 top = ArcPoint(0f);
        MakeTick("NeutralTick", neutralTickColor, arcLineWidth * 0.8f,
            top, top * (1f - arcLineWidth * 6f / arcRadius));
    }

    // ── Small ticks at −90° and +90° ends ───────────────────────────
    void BuildEndTicks()
    {
        foreach (float angle in new[] { -ArcHalfSpanDeg, ArcHalfSpanDeg })
        {
            Vector3 tip = ArcPoint(angle);
            MakeTick($"EndTick_{angle}", arcTrackColor, arcLineWidth * 0.6f,
                tip, tip * (1f - arcLineWidth * 5f / arcRadius));
        }
    }

    LineRenderer MakeTick(string goName, Color color, float width, Vector3 p0, Vector3 p1)
    {
        GameObject tickGO = new GameObject(goName);
        tickGO.transform.SetParent(transform, false);

        LineRenderer lr = tickGO.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = false;
        lr.startWidth = width;
        lr.endWidth   = width;
        lr.positionCount = 2;
        lr.material = CreateUnlitMaterial(color);
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.alignment = LineAlignment.View;
        lr.SetPosition(0, p0);
        lr.SetPosition(1, p1);
        return lr;
    }

    // ── Indicator dot (sphere) ────────────────────────────────────────
    void BuildDot()
    {
        GameObject dotGO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        dotGO.name = "Dot";
        dotGO.transform.SetParent(transform, false);
        dotGO.transform.localScale = Vector3.one * dotSize;

        // Use dotZOffset so the dot sits in front of the arc line on both sides
        Vector3 neutralPos = ArcPoint(0f);
        neutralPos.z = dotZOffset;
        dotGO.transform.localPosition = neutralPos;

        // Remove collider — purely visual
        Destroy(dotGO.GetComponent<Collider>());

        Renderer rend = dotGO.GetComponent<Renderer>();
        Material dotMat = CreateUnlitMaterial(dotColor);
        // Higher render queue draws on top of the arc (queue 2000 = Geometry default)
        dotMat.renderQueue = 3001;
        rend.material = dotMat;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;

        dotTransform = dotGO.transform;
    }

    // ── Text labels ───────────────────────────────────────────────────
    void BuildLabels()
    {
        // Compute base positions independently of offsets
        baseSlowerPos  = ArcPoint(-ArcHalfSpanDeg) + new Vector3(-labelSize * 0.8f, 0f, 0f);
        baseFasterPos  = ArcPoint( ArcHalfSpanDeg) + new Vector3( labelSize * 0.8f, 0f, 0f);
        baseNeutralPos = ArcPoint(0f)              + new Vector3(0f, labelSize * 1.0f,  0f);
        baseInstrPos   = new Vector3(0f, -arcRadius * 0.25f, 0f);

        // "Slower" at left end of arc (−90°)
        labelSlowerTf = CreateWorldLabel("LabelSlower", "Slower",
            baseSlowerPos + slowerLabelOffset,
            TextAlignmentOptions.Right).transform;

        // "Faster" at right end of arc (+90°)
        labelFasterTf = CreateWorldLabel("LabelFaster", "Faster",
            baseFasterPos + fasterLabelOffset,
            TextAlignmentOptions.Left).transform;

        // "Neutral" above the top of the arc
        labelNeutralTf = CreateWorldLabel("LabelNeutral", "Neutral",
            baseNeutralPos + neutralLabelOffset,
            TextAlignmentOptions.Center).transform;

        // Instruction below the arc centre
        string instrText = playinVR
            ? "Tilt controller: slower ◄ | ► faster"
            : "Left/Right Shift: slower ◄ | ► faster ";
        labelInstrTf = CreateWorldLabel("LabelInstruction", instrText,
            baseInstrPos + instrLabelOffset,
            TextAlignmentOptions.Center,
            labelSize * 0.65f).transform;
    }

    // Reapply offsets — called every Update() so Inspector edits show instantly
    void ApplyLabelOffsets()
    {
        if (labelSlowerTf  != null) labelSlowerTf .localPosition = baseSlowerPos  + slowerLabelOffset;
        if (labelFasterTf  != null) labelFasterTf .localPosition = baseFasterPos  + fasterLabelOffset;
        if (labelNeutralTf != null) labelNeutralTf.localPosition = baseNeutralPos + neutralLabelOffset;
        if (labelInstrTf   != null) labelInstrTf  .localPosition = baseInstrPos   + instrLabelOffset;
    }

    GameObject CreateWorldLabel(string goName, string text, Vector3 localPos,
        TextAlignmentOptions alignment, float size = -1f)
    {
        if (size < 0f) size = labelSize;

        GameObject go = new GameObject(goName);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localPos;

        TextMeshPro tmp = go.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = labelColor;
        tmp.alignment = alignment;
        tmp.rectTransform.sizeDelta = new Vector2(arcRadius * 1.2f, size * 2f);

        // Centre pivot
        tmp.rectTransform.pivot = new Vector2(0.5f, 0.5f);

        return go;
    }

    // ──────────────────────────────────────────────────────────────────
    //  Helpers
    // ──────────────────────────────────────────────────────────────────

    Material CreateUnlitMaterial(Color color)
    {
        // Use the built-in Sprites/Default shader — renders reliably on
        // any render pipeline without needing a specific URP/HDRP shader.
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");

        Material mat = new Material(shader);
        mat.color = color;
        return mat;
    }
}
