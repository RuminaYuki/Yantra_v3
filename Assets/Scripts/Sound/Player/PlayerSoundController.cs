using UnityEngine;

public class PlayerSoundController : MonoBehaviour
{
    public enum FootstepMode
    {
        DistanceBased,
        AnimationEvent
    }

    [System.Serializable]
    public struct SurfaceSound
    {
        [Tooltip("Surface Tag name (e.g. Wood, Grass, Dirt)")]
        public string surfaceTag;

        public SoundID crouchSound;
        public SoundID walkSound;
        public SoundID runSound;
    }

    [System.Serializable]
    public struct TerrainSound
    {
        [Tooltip("ตั้งชื่อกลุ่มให้ดูง่ายๆ เช่น Grass, Dirt, Rock")]
        public string groupName;

        [Tooltip("ใส่ Terrain Layer ได้หลายอันเลย (ยัดหญ้าทุกแบบเข้ามาในช่องนี้ได้เลย)")]
        public TerrainLayer[] terrainLayers;

        public SoundID crouchSound;
        public SoundID walkSound;
        public SoundID runSound;
    }

    [Header("Surface Sounds Settings (For 3D Models)")]
    [SerializeField] private SurfaceSound[] surfaceSounds;

    [Header("Terrain Sounds Settings (For Unity Terrain)")]
    [SerializeField] private TerrainSound[] terrainSounds;

    [Header("Default Sounds (Fallback)")]
    [SerializeField] private SoundID defaultCrouchID;
    [SerializeField] private SoundID defaultWalkID;
    [SerializeField] private SoundID defaultRunID;

    [Header("Item Sounds")]
    [SerializeField] private SoundID flashlightToggleID;
    [SerializeField] private SoundID openNotebookID;

    // ==========================================
    // Player Vocals & Actions
    // ==========================================
    [Header("Player Vocals & Actions")]
    [Tooltip("เสียงร้องตอนโดนโจมตี (เช่น ท่า GotSlap)")]
    [SerializeField] private SoundID hurtSound;

    [Tooltip("เสียงตอนตาย (เช่น ท่า PlayerDead)")]
    [SerializeField] private SoundID deathSound;

    [Tooltip("กระเป๋าเสียงเผื่อไว้ใช้กับท่าอื่นๆ เช่น 0=ใช้ยันต์, 1=โดนผีจับ")]
    [SerializeField] private SoundID[] actionSounds;

    // ==========================================
    // Foley Sounds (เสียงประกอบกริยา)
    // ==========================================
    [Header("Foley Sounds")]
    [Tooltip("กระเป๋าเสียง Foley เช่น 0=ขยับเสื้อผ้า, 1=กระโดดลงพื้น, 2=หยิบของ")]
    [SerializeField] private SoundID[] foleySounds;

    [Header("Movement Foley (เสื้อผ้า/ของติดตัว)")]
    [Tooltip("ปิดถ้าไม่อยากให้มีเสียงเสื้อผ้าตามการเคลื่อนไหว")]
    [SerializeField] private bool enableMovementFoley = true;

    [Tooltip("ถ้ามีไฟล์เดียว ใส่ SoundID ตัวเดียวกันทั้ง 3 ช่องได้เลย")]
    [SerializeField] private SoundID foleyCrouchID;
    [SerializeField] private SoundID foleyWalkID;
    [SerializeField] private SoundID foleyRunID;

    [Tooltip("เยื้องจังหวะจากฝีเท้า / 0.5 = ตกกลางระหว่างก้าวพอดี (กันเสียงกลบกัน)")]
    [Range(0f, 1f)]
    [SerializeField] private float foleyPhaseOffset = 0.5f;

    [Tooltip("ความสูงที่เสียงเกิด นับจากเท้า / 1.0 = ระดับลำตัว ไม่ใช่พื้น")]
    [SerializeField] private float foleyHeightOffset = 1.0f;

    [Tooltip("ยิงกี่ครั้งต่อ 1 ก้าว / ผ้าเสียดสีถี่กว่าเท้าแตะพื้น เพราะทั้งขาเหวี่ยงและแขนแกว่ง แนะนำ 2-3")]
    [Range(0.5f, 4f)]
    [SerializeField] private float foleyPerStride = 2f;

    [Tooltip("ห้ามยิงซ้ำจนกว่าเสียงเก่าเล่นไปกี่ % / ยิ่งต่ำยิ่งปล่อยให้หางเสียงเกยกัน = ฟังต่อเนื่องเหมือนผ้าจริง")]
    [Range(0f, 1f)]
    [SerializeField] private float foleyRetriggerGuard = 0.25f;

    [Tooltip("สุ่มระยะก้าวให้เพี้ยนไปมา / 0.25 = บวกลบ 25% กันหูล็อกจังหวะได้")]
    [Range(0f, 0.5f)]
    [SerializeField] private float foleyStrideJitter = 0.25f;

    [Tooltip("โอกาสที่จะดังจริงในแต่ละรอบ (%) / ผ้าจริงไม่ได้ดังทุกก้าว การขาดหายแบบสุ่มทำลาย pattern ได้ดีที่สุด")]
    [Range(0f, 100f)]
    [SerializeField] private float foleyPlayChance = 75f;

    [Header("Footstep Mode")]
    [SerializeField] private FootstepMode mode = FootstepMode.DistanceBased;

    [Header("Distance Mode Settings (Meters)")]
    [SerializeField] private float crouchStrideLength = 1.0f;
    [SerializeField] private float walkStrideLength = 1.6f;
    [SerializeField] private float runStrideLength = 2.4f;

    [Header("Movement Speed Thresholds")]
    [SerializeField] private float minMoveSpeed = 0.15f;
    [SerializeField] private float walkSpeedThreshold = 1.5f;
    [SerializeField] private float runSpeedThreshold = 3.5f;

    [Header("Ground Detection & Cooldown")]
    [SerializeField] private float minStepInterval = 0.15f;
    [SerializeField] private bool requireGrounded = true;
    [SerializeField] private LayerMask groundLayer = ~0;

    [Header("Debug")]
    [SerializeField] private bool logFootsteps = false;

    private CharacterController charController;
    private float lastStepTime = -999f;
    private Vector3 lastPosition;
    private float measuredSpeed;
    private float distanceAccumulated;
    private float foleyDistanceAccum;
    private bool wasMovingForFoley;
    private float foleyBusyUntil = -999f;
    private float foleyTargetStride = -1f;
    private Vector3 currentMoveDir = Vector3.forward;

    public float CurrentSpeed => measuredSpeed;
    public bool IsRunning => measuredSpeed >= runSpeedThreshold;
    public bool IsCrouching => measuredSpeed >= minMoveSpeed && measuredSpeed < walkSpeedThreshold;

    private void Awake()
    {
        charController = GetComponent<CharacterController>();
        lastPosition = transform.position;
    }

    private void Update()
    {
        Vector3 delta = transform.position - lastPosition;
        lastPosition = transform.position;

        float distanceThisFrame = delta.magnitude;

        if (Time.deltaTime > 0f)
            measuredSpeed = distanceThisFrame / Time.deltaTime;

        Vector3 flatDelta = new Vector3(delta.x, 0f, delta.z);
        if (flatDelta.magnitude > 0.001f)
        {
            currentMoveDir = flatDelta.normalized;
        }

        if (mode == FootstepMode.DistanceBased)
            UpdateDistanceFootstep(distanceThisFrame);

        // Foley ทำงานทุกโหมด เพราะเสื้อผ้าขยับตลอด ไม่ขึ้นกับว่าฝีเท้าใช้ระบบไหน
        UpdateMovementFoley(distanceThisFrame);
    }

    private void UpdateDistanceFootstep(float distanceThisFrame)
    {
        if (measuredSpeed < minMoveSpeed || !IsGrounded())
        {
            distanceAccumulated = Mathf.Min(distanceAccumulated, walkStrideLength * 0.8f);
            return;
        }

        distanceAccumulated += distanceThisFrame;

        float stride = walkStrideLength;
        if (IsRunning) stride = runStrideLength;
        else if (IsCrouching) stride = crouchStrideLength;

        if (distanceAccumulated >= stride)
        {
            distanceAccumulated -= stride;
            TriggerFootstep();
        }
    }

    // ==========================================
    // Movement Foley — เสียงเสื้อผ้า/ของติดตัว
    // ==========================================

    private void UpdateMovementFoley(float distanceThisFrame)
    {
        if (!enableMovementFoley) return;

        // ยืนนิ่ง หรือลอยอยู่กลางอากาศ = ผ้าไม่เสียดสี
        if (measuredSpeed < minMoveSpeed || !IsGrounded())
        {
            wasMovingForFoley = false;
            return;
        }

        float stride = walkStrideLength;
        if (IsRunning) stride = runStrideLength;
        else if (IsCrouching) stride = crouchStrideLength;

        stride /= Mathf.Max(0.1f, foleyPerStride);

        if (!wasMovingForFoley)
        {
            wasMovingForFoley = true;
            foleyDistanceAccum = stride * foleyPhaseOffset;
            foleyTargetStride = RollFoleyStride(stride);
        }

        if (foleyTargetStride <= 0f) foleyTargetStride = RollFoleyStride(stride);

        foleyDistanceAccum += distanceThisFrame;

        if (foleyDistanceAccum >= foleyTargetStride)
        {
            foleyDistanceAccum -= foleyTargetStride;

            // สุ่มระยะของรอบถัดไปใหม่ทุกครั้ง
            // ถ้าใช้ระยะคงที่ สมองคนจะทำนายจังหวะได้ภายในไม่กี่วินาที แล้วตีความว่าเป็นเครื่องจักร
            foleyTargetStride = RollFoleyStride(stride);

            TriggerFoley();
        }
    }

    private float RollFoleyStride(float baseStride)
    {
        if (foleyStrideJitter <= 0f) return baseStride;
        return baseStride * Random.Range(1f - foleyStrideJitter, 1f + foleyStrideJitter);
    }

    private void TriggerFoley()
    {

        if (Time.time < foleyBusyUntil) return;

        // ไม่ดังทุกครั้ง — ผ้าจริงบางก้าวก็เงียบ
        // การขาดหายแบบสุ่มคือตัวทำลาย pattern ที่ได้ผลที่สุด และลดจำนวนเสียงรวมไปในตัว
        if (Random.Range(0f, 100f) > foleyPlayChance) return;

        SoundID idToPlay = foleyWalkID;

        if (IsRunning) idToPlay = foleyRunID != null ? foleyRunID : foleyWalkID;
        else if (IsCrouching) idToPlay = foleyCrouchID != null ? foleyCrouchID : foleyWalkID;

        if (idToPlay == null || SoundManager.Instance == null) return;

        // เกิดที่ระดับลำตัว ไม่ใช่ที่พื้น เพราะเสื้อผ้าอยู่บนตัวเรา
        float duration = SoundManager.Instance.PlaySFX(idToPlay, transform.position + Vector3.up * foleyHeightOffset);

        foleyBusyUntil = Time.time + (duration * foleyRetriggerGuard);
    }

    private bool IsGrounded()
    {
        if (!requireGrounded) return true;
        if (charController != null && charController.isGrounded) return true;

        return Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, 1.2f, groundLayer);
    }

    private void TriggerFootstep()
    {
        if (Time.time - lastStepTime < minStepInterval) return;
        lastStepTime = Time.time;

        SoundID crouchToPlay = defaultCrouchID;
        SoundID walkToPlay = defaultWalkID;
        SoundID runToPlay = defaultRunID;
        string hitTag = "Untagged";

        Vector3 rayOrigin = transform.position + (Vector3.up * 0.5f) + (currentMoveDir * 0.3f);
        Vector3 soundSpawnPos = transform.position;

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 1.5f, groundLayer))
        {
            soundSpawnPos = hit.point;
            Terrain hitTerrain = hit.collider.GetComponent<Terrain>();

            if (hitTerrain != null)
            {
                TerrainLayer dominantLayer = GetDominantTerrainLayer(hit.point, hitTerrain);
                if (dominantLayer != null)
                {
                    hitTag = dominantLayer.name;
                    bool foundMatch = false;

                    foreach (var tSound in terrainSounds)
                    {
                        if (tSound.terrainLayers == null) continue;
                        foreach (var layer in tSound.terrainLayers)
                        {
                            if (layer == dominantLayer)
                            {
                                crouchToPlay = tSound.crouchSound;
                                walkToPlay = tSound.walkSound;
                                runToPlay = tSound.runSound;
                                foundMatch = true;
                                break;
                            }
                        }
                        if (foundMatch) break;
                    }
                }
            }
            else
            {
                hitTag = hit.collider.tag;
                foreach (var surface in surfaceSounds)
                {
                    if (surface.surfaceTag == hitTag)
                    {
                        crouchToPlay = surface.crouchSound;
                        walkToPlay = surface.walkSound;
                        runToPlay = surface.runSound;
                        break;
                    }
                }
            }
        }

        SoundID idToPlay = walkToPlay;
        string moveState = "Walk";

        if (IsRunning)
        {
            idToPlay = runToPlay != null ? runToPlay : walkToPlay;
            moveState = "Run";
        }
        else if (IsCrouching)
        {
            idToPlay = crouchToPlay != null ? crouchToPlay : walkToPlay;
            moveState = "Crouch";
        }

        if (idToPlay == null) return;

        if (logFootsteps)
            Debug.Log($"Footstep: [{moveState}] on surface [{hitTag}], speed = {measuredSpeed:F2}");

        SoundManager.Instance.PlaySFX(idToPlay, soundSpawnPos);
    }

    private TerrainLayer GetDominantTerrainLayer(Vector3 worldPos, Terrain terrain)
    {
        TerrainData terrainData = terrain.terrainData;
        Vector3 terrainPos = terrain.transform.position;

        float mapX = ((worldPos.x - terrainPos.x) / terrainData.size.x) * terrainData.alphamapWidth;
        float mapZ = ((worldPos.z - terrainPos.z) / terrainData.size.z) * terrainData.alphamapHeight;

        int x = Mathf.FloorToInt(mapX);
        int z = Mathf.FloorToInt(mapZ);

        if (x < 0 || z < 0 || x >= terrainData.alphamapWidth || z >= terrainData.alphamapHeight)
            return null;

        float[,,] aMap = terrainData.GetAlphamaps(x, z, 1, 1);
        float maxMix = 0f;
        int maxIndex = 0;

        for (int n = 0; n < aMap.GetUpperBound(2) + 1; n++)
        {
            if (aMap[0, 0, n] > maxMix)
            {
                maxIndex = n;
                maxMix = aMap[0, 0, n];
            }
        }

        if (terrainData.terrainLayers != null && maxIndex < terrainData.terrainLayers.Length)
        {
            return terrainData.terrainLayers[maxIndex];
        }

        return null;
    }

    public void PlaySneakSound()
    {
        if (mode != FootstepMode.AnimationEvent) return;
        if (measuredSpeed < (minMoveSpeed * 1.5f)) return;
        if (!IsGrounded()) return;
        TriggerFootstep();
    }

    public void PlayFootstepSound()
    {
        if (mode != FootstepMode.AnimationEvent) return;
        if (measuredSpeed < (minMoveSpeed * 1.5f)) return;
        if (!IsGrounded()) return;
        TriggerFootstep();
    }

    public void PlayFlashlightSound()
    {
        if (flashlightToggleID != null)
            SoundManager.Instance.PlaySFX(flashlightToggleID, transform.position);
    }

    public void PlayNotebookSound()
    {
        if (openNotebookID != null)
            SoundManager.Instance.PlaySFX(openNotebookID, transform.position);
    }

    // ==========================================
    // Vocals & Actions Methods
    // ==========================================
    public void PlayHurtSound()
    {
        if (hurtSound != null && SoundManager.Instance != null)
            SoundManager.Instance.PlaySFX(hurtSound, transform.position);
    }

    public void PlayDeathSound()
    {
        if (deathSound != null && SoundManager.Instance != null)
            SoundManager.Instance.PlaySFX(deathSound, transform.position);
    }

    public void PlayFoleySound(int index)
    {
        if (foleySounds == null || index < 0 || index >= foleySounds.Length) return;
        if (foleySounds[index] == null || SoundManager.Instance == null) return;

        SoundManager.Instance.PlaySFX(foleySounds[index], transform.position + Vector3.up * foleyHeightOffset);
    }

    public void PlayActionSound(int index)
    {
        if (actionSounds != null && index >= 0 && index < actionSounds.Length)
        {
            if (actionSounds[index] != null && SoundManager.Instance != null)
            {
                SoundManager.Instance.PlaySFX(actionSounds[index], transform.position);
            }
        }
    }

}