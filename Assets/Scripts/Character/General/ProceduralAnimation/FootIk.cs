using UnityEngine;

[RequireComponent(typeof(Animator))]
public class FootIk : MonoBehaviour
{
    private Animator anim; // ตัวแปรสำหรับดึงคอมโพเนนต์ Animator ของตัวละคร

    [Header("IK Settings (ตั้งค่าพื้นฐาน)")]
    [Range(0, 1f)] public float IkWeight = 1f; // น้ำหนักรวมของ IK (1 = ทำงานเต็มที่)
    public LayerMask GroundLayer;              // เลเยอร์ของพื้นหรือบันไดที่ให้ IK ตรวจจับ
    public float RaycastDistance = 1.2f;       // ระยะทางในการยิง Raycast ลงมาเช็คพื้น
    public float FootOffsetY = 0.1f;           // ระยะยกตัวของเท้า (เผื่อความหนารองเท้า ไม่ให้จมพื้น)
    public float SphereCastRadius = 0.05f;     // ขนาดความอ้วนของลูกบอล SphereCast

    [Header("Dynamic Walk Cycle (แก้ขาตวัด & เท้าติดกาว)")]
    public float LiftThreshold = 0.2f;         // ระยะความสูงที่แอนิเมชันยกเท้า แล้วจะปล่อยให้ IK คลายตัว

    [Header("Smoothing & Limits")]
    public float PositionSmoothSpeed = 15f;    // ความเร็วในการเกลี่ยตำแหน่งเท้าให้สมูท
    public float RotationSmoothSpeed = 15f;    // ความเร็วในการเกลี่ยองศาข้อเท้า
    public float MaxSlopeAngle = 45f;          // มุมความชันสูงสุดที่อนุญาตให้ข้อเท้าบิดตาม

    [Header("Overlap Correction (แก้เท้าทะลุหน้าตัดบันได)")]
    public bool FixFootOverlap = true;         // เปิด/ปิด ระบบกันเท้าทะลุกำแพงหรือหน้าตัดบันได
    [Tooltip("ระยะเผื่อกันชนขอบบันได (ช่วยให้ปลายเท้าไม่จมกำแพง)")]
    public float FootRadius = 0.08f;           // รัศมีเผื่อระยะไม่ให้เท้าชิดขอบผนังเกินไป

    [Header("Pelvis / Hip Correction (ระบบดึงสะโพก)")]
    public bool AdjustPelvis = true;           // เปิด/ปิด ระบบย่อตัวสะโพกอัตโนมัติ
    public float PelvisOffsetSpeed = 10f;      // ความเร็วในการย่อสะโพก
    public float MaxPelvisOffset = 0.6f;       // ระยะย่อตัวสูงสุด ป้องกันตัวละครสควอทลึกเกินไป

    // ตัวแปรภายในสำหรับเก็บค่าคำนวณและทำ Smoothing
    private float leftFootHeight, rightFootHeight;
    private Vector3 leftFootNormal = Vector3.up;
    private Vector3 rightFootNormal = Vector3.up;
    private float lastPelvisYOffset = 0f;
    private float currentLeftWeight = 1f;
    private float currentRightWeight = 1f;

    void Start()
    {
        anim = GetComponent<Animator>(); // ดึงคอมโพเนนต์ Animator มาเก็บไว้ตอนเริ่มเกม
    }

    void OnAnimatorIK(int layerIndex)
    {
        if (anim == null) return; // ถ้าไม่มี Animator ให้ข้ามการทำงาน

        // 1. ดึงตำแหน่งและองศาของเท้าซ้าย-ขวาจากแอนิเมชันดิบๆ
        Vector3 leftIKPos = anim.GetIKPosition(AvatarIKGoal.LeftFoot);
        Quaternion leftIKRot = anim.GetIKRotation(AvatarIKGoal.LeftFoot);
        Vector3 rightIKPos = anim.GetIKPosition(AvatarIKGoal.RightFoot);
        Quaternion rightIKRot = anim.GetIKRotation(AvatarIKGoal.RightFoot);

        // 2. คำนวณน้ำหนัก IK แบบไดนามิก (เช็คว่าเท้ากำลังลอยก้าวขาอยู่หรือไม่)
        CalculateProceduralWeight(leftIKPos, ref currentLeftWeight);
        CalculateProceduralWeight(rightIKPos, ref currentRightWeight);

        // 3. คำนวณตำแหน่งลงพื้นและความชันของเท้าแต่ละข้าง
        CalculateFootIK(ref leftIKPos, ref leftIKRot, ref leftFootHeight, ref leftFootNormal);
        CalculateFootIK(ref rightIKPos, ref rightIKRot, ref rightFootHeight, ref rightFootNormal);

        // 4. ถ้าเปิดใช้งาน ให้คำนวณย่อสะโพกลงมาช่วยกรณีขาต้องยืดลงที่ต่ำ
        if (AdjustPelvis)
        {
            ApplyPelvisOffset(leftIKPos, rightIKPos);
        }

        // 5. ส่งค่าตำแหน่ง องศา และน้ำหนักที่คำนวณเสร็จแล้วเข้ากระดูก IK ของ Animator
        SetFootIK(AvatarIKGoal.LeftFoot, leftIKPos, leftIKRot, currentLeftWeight);
        SetFootIK(AvatarIKGoal.RightFoot, rightIKPos, rightIKRot, currentRightWeight);
    }

    // คำนวณน้ำหนัก IK ตามความสูงของเท้าในแอนิเมชัน (เวลายกขา น้ำหนักจะลดเหลือ 0 เพื่อกันขาตวัด)
    private void CalculateProceduralWeight(Vector3 animFootPos, ref float currentWeight)
    {
        float footLocalHeight = animFootPos.y - transform.position.y;
        float targetWeight = 1f - Mathf.InverseLerp(FootOffsetY, FootOffsetY + LiftThreshold, footLocalHeight);
        currentWeight = Mathf.Lerp(currentWeight, targetWeight, Time.deltaTime * PositionSmoothSpeed);
    }

    // ฟังก์ชันหลักในการยิง Raycast/SphereCast หาพื้นและปรับองศาข้อเท้า
    private void CalculateFootIK(ref Vector3 ikPos, ref Quaternion ikRot, ref float currentHeight, ref Vector3 currentNormal)
    {
        Vector3 rayOrigin = ikPos + Vector3.up * (RaycastDistance / 2f); // ตั้งจุดกำเนิด Raycast ที่ระดับเข่า
        RaycastHit hit;
        bool hasHit = false;

        // ยิง Raycast แบบเส้นตรงก่อนเพื่อความแม่นยำ
        if (Physics.Raycast(rayOrigin, Vector3.down, out hit, RaycastDistance, GroundLayer))
        {
            hasHit = true;
        }
        // ถ้าเส้นตรงพลาด (เช่น ร่องบันได) ให้ใช้ SphereCast เป็นตัวสำรอง
        else if (Physics.SphereCast(rayOrigin, SphereCastRadius, Vector3.down, out hit, RaycastDistance, GroundLayer))
        {
            hasHit = true;
        }

        if (hasHit)
        {
            float surfaceAngle = Vector3.Angle(Vector3.up, hit.normal); // เช็คความชันพื้น
            Vector3 targetNormal = hit.normal;

            // ถ้าพื้นชันเกินไป (เช่น ชนหน้าตัดบันได) ให้บังคับเท้าตั้งตรงปกติ
            if (surfaceAngle > MaxSlopeAngle)
            {
                targetNormal = Vector3.up;
            }

            // คำนวณพิกัดเป้าหมายแกน Y ของเท้า
            Vector3 finalTargetPos = new Vector3(ikPos.x, hit.point.y + FootOffsetY, ikPos.z);

            // ระบบผลักเท้าออกเมื่อชนหน้าตัดบันได (Overlap Correction)
            if (FixFootOverlap)
            {
                Vector3 hipPos = anim.bodyPosition;
                Vector3 dirToFoot = finalTargetPos - hipPos;

                if (Physics.Raycast(hipPos, dirToFoot.normalized, out RaycastHit wallHit, dirToFoot.magnitude, GroundLayer))
                {
                    finalTargetPos.x = wallHit.point.x + wallHit.normal.x * FootRadius;
                    finalTargetPos.z = wallHit.point.z + wallHit.normal.z * FootRadius;
                    targetNormal = Vector3.up;
                }
            }

            // ค่อยๆ เกลี่ยความสูง (Smooth) เฉพาะแกน Y ป้องกันเท้าสไลด์
            currentHeight = Mathf.Lerp(currentHeight, finalTargetPos.y, Time.deltaTime * PositionSmoothSpeed);

            // อัปเดตพิกัดเท้าจริง
            ikPos.x = finalTargetPos.x;
            ikPos.y = currentHeight;
            ikPos.z = finalTargetPos.z;

            // เกลี่ยองศาข้อเท้าให้เอียงตามพื้นอย่างนุ่มนวล
            currentNormal = Vector3.Lerp(currentNormal, targetNormal, Time.deltaTime * RotationSmoothSpeed);
            ikRot = Quaternion.FromToRotation(Vector3.up, currentNormal) * ikRot;
        }
        else
        {
            // ถ้าไม่เจอพื้น ให้ค่อยๆ คืนค่าความสูงและองศากลับสู่แอนิเมชันปกติ
            currentHeight = Mathf.Lerp(currentHeight, ikPos.y, Time.deltaTime * PositionSmoothSpeed);
            ikPos.y = currentHeight;

            currentNormal = Vector3.Lerp(currentNormal, Vector3.up, Time.deltaTime * RotationSmoothSpeed);
            ikRot = Quaternion.FromToRotation(Vector3.up, currentNormal) * ikRot;
        }
    }

    // คำนวณและดึงสะโพกลงมาเพื่อช่วยให้ขายืดแตะพื้นได้โดยไม่ต้องเกร็ง
    private void ApplyPelvisOffset(Vector3 leftIKPos, Vector3 rightIKPos)
    {
        float expectedRestHeight = transform.position.y + FootOffsetY;

        float leftStretch = expectedRestHeight - leftIKPos.y;
        float rightStretch = expectedRestHeight - rightIKPos.y;

        float targetPelvisOffset = Mathf.Max(0, Mathf.Max(leftStretch, rightStretch));
        targetPelvisOffset = Mathf.Clamp(targetPelvisOffset, 0f, MaxPelvisOffset);

        lastPelvisYOffset = Mathf.Lerp(lastPelvisYOffset, targetPelvisOffset, Time.deltaTime * PelvisOffsetSpeed);

        // ดึงตำแหน่งแกน Y ของตัวละครลงตามค่า Offset
        Vector3 currentBodyPos = anim.bodyPosition;
        currentBodyPos.y -= lastPelvisYOffset;
        anim.bodyPosition = currentBodyPos;
    }

    // สั่งจ่ายค่าพิกัดและน้ำหนักสุดท้ายให้ระบบ IK ของ Animator
    private void SetFootIK(AvatarIKGoal goal, Vector3 pos, Quaternion rot, float dynamicWeight)
    {
        float finalWeight = IkWeight * dynamicWeight;

        anim.SetIKPositionWeight(goal, finalWeight);
        anim.SetIKRotationWeight(goal, finalWeight);

        anim.SetIKPosition(goal, pos);
        anim.SetIKRotation(goal, rot);
    }

    // วาดเส้น Debug Gizmos ในหน้าจอ Scene เพื่อช่วยพรีเซนต์งาน
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying || anim == null) return;
        DrawFootGizmo(AvatarIKGoal.LeftFoot, Color.red);
        DrawFootGizmo(AvatarIKGoal.RightFoot, Color.cyan);
    }

    private void DrawFootGizmo(AvatarIKGoal goal, Color color)
    {
        // 🛠 FIX ERROR: ใช้ GetBoneTransform แทน GetIKPosition เพื่อหลีกเลี่ยง Warning นอก OnAnimatorIK
        Transform footBone = goal == AvatarIKGoal.LeftFoot ?
                             anim.GetBoneTransform(HumanBodyBones.LeftFoot) :
                             anim.GetBoneTransform(HumanBodyBones.RightFoot);

        if (footBone == null) return;

        Vector3 footPos = footBone.position;
        Vector3 rayOrigin = footPos + Vector3.up * (RaycastDistance / 2f);

        Gizmos.color = color;
        Gizmos.DrawLine(rayOrigin, rayOrigin + Vector3.down * RaycastDistance);
        Gizmos.DrawWireSphere(rayOrigin + Vector3.down * RaycastDistance, SphereCastRadius);

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, RaycastDistance, GroundLayer) ||
            Physics.SphereCast(rayOrigin, SphereCastRadius, Vector3.down, out hit, RaycastDistance, GroundLayer))
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(hit.point, 0.03f);
            Gizmos.DrawLine(hit.point, hit.point + hit.normal * 0.3f);
        }

        if (FixFootOverlap)
        {
            // 🛠 FIX ERROR: ใช้ตำแหน่งกระดูกสะโพก (Hips) แทนคำสั่ง anim.bodyPosition
            Vector3 hipPos = anim.GetBoneTransform(HumanBodyBones.Hips).position;
            Vector3 target = new Vector3(footPos.x, hit.point.y + FootOffsetY, footPos.z);

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(hipPos, target);
        }
    }
}