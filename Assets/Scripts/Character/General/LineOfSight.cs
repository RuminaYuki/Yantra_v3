using UnityEngine;

public class LineOfSight : MonoBehaviour
{
    [Header("If targetAnchor is set, It will override Target value")]
    [SerializeField] TransformAnchor targetAnchor;
    [SerializeField] Transform target;
    [SerializeField] Transform viewpoint;
    [SerializeField] bool showGizmos = true;

    [Header("DetectRange")]
    [SerializeField] float detectRange;

    [Header("FieldOfView")]
    [SerializeField] float viewAngle = 90f;
    [SerializeField] bool enableFOV = true;

    [Header("Raycast")]
    [SerializeField] LayerMask obstacleLayer;
    [SerializeField] bool enableRayCast = true;

    [Header("Notice")]
    [SerializeField] bool enableNotice = true;
    [SerializeField] float minTimeToNotice = 0.5f;
    [SerializeField] float maxTimeToNotice = 3f;
    [Tooltip("X = ระยะ normalize (0=ใกล้สุด, 1=ไกลสุด), Y = สัดส่วน 0-1 ระหว่าง min-max")]
    [SerializeField] AnimationCurve noticeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    float noticeTimer;
    public bool HasNoticed { get; private set; }

    float timeSinceLastSeen;
    public float TimeSinceLastSeen => timeSinceLastSeen;

    // --- API ให้คลาสภายนอกกำหนดค่าได้ ---
    public bool ShowGizmos
    {
        get => showGizmos;
        set => showGizmos = value;
    }
    public float DetectRange
    {
        get => detectRange;
        set => detectRange = value;
    }
    public float MinTimeToNotice
    {
        get => minTimeToNotice;
        set => minTimeToNotice = value;
    }
    public float MaxTimeToNotice
    {
        get => maxTimeToNotice;
        set => maxTimeToNotice = value;
    }
    public AnimationCurve NoticeCurve
    {
        get => noticeCurve;
        set => noticeCurve = value;
    }

    public Transform Target => target;

    private Transform Viewpoint => viewpoint != null ? viewpoint : transform;

    void Start()
    {
        if(targetAnchor != null)
        {
            target = targetAnchor.Value;
            if (target == null)
            {
                GameObject player = GameObject.FindWithTag("Player");
                if (player != null)
                {
                    target = player.transform;
                }
                else
                {
                    Debug.LogError("Dont have Player in Scene");
                }
            }
        }
    }

    void Update()
    {
        UpdateLastSeenTimer();

        if (!enableNotice) return;
        UpdateNotice();
    }

    void UpdateLastSeenTimer()
    {
        if (CanSeeTarget())
        {
            timeSinceLastSeen = 0f;
        }
        else
        {
            timeSinceLastSeen += Time.deltaTime;
        }
    }

    void UpdateNotice()
    {
        if (CanSeeTarget())
        {
            float distance = Vector3.Distance(Viewpoint.position, target.position);
            float normalized = Mathf.Clamp01(distance / detectRange);
            float t = noticeCurve.Evaluate(normalized);
            float timeNeeded = Mathf.Lerp(minTimeToNotice, maxTimeToNotice, t);

            noticeTimer += Time.deltaTime;
            HasNoticed = noticeTimer >= timeNeeded;
        }
        else
        {
            noticeTimer = 0f;
            HasNoticed = false;
        }
    }
    public void ResetnoticTimer()
    {
        noticeTimer = 0f;
        HasNoticed = false;
    }
    public bool DetectRangeCheck()
    {
        float distance = (target.position - Viewpoint.position).sqrMagnitude;
        return distance <= detectRange * detectRange;
    }
    public bool FOVAngle()
    {
        Vector3 dirToTarget = (target.position - Viewpoint.position).normalized;
        float angle = Vector3.Angle(Viewpoint.forward, dirToTarget);

        return angle <= viewAngle * 0.5f;
    }
    public bool Raycast()
    {
        Vector3 origin = Viewpoint.position;
        Vector3 dirToTarget = (target.position - origin).normalized;
        float distance = Vector3.Distance(origin, target.position);

        if (Physics.Raycast(origin, dirToTarget, distance, obstacleLayer))
        {
            return false;
        }
        return true;
    }
    public bool CanSeeTarget()
    {
        if (target == null) return false;
        if (!DetectRangeCheck()) return false;
        if (enableFOV && !FOVAngle()) return false;
        if (enableRayCast &&!Raycast()) return false;

        return true;
    }
    private void OnDrawGizmos()
    {
        if(!showGizmos) return;
        if (target == null) return;

        Transform viewOrigin = Viewpoint;

        //DetectRange
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(viewOrigin.position, detectRange);

        //FOV
        Vector3 left = Quaternion.Euler(0, -viewAngle / 2, 0) * viewOrigin.forward;
        Vector3 right = Quaternion.Euler(0, viewAngle / 2, 0) * viewOrigin.forward;

        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(viewOrigin.position, left * detectRange);
        Gizmos.DrawRay(viewOrigin.position, right * detectRange);

        //Raycast
        Gizmos.color = Color.red;
        Gizmos.DrawLine(viewOrigin.position, target.position);
    }
}
