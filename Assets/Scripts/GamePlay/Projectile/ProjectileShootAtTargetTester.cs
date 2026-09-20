using UnityEngine;

public class ProjectileShootAtTargetTester : MonoBehaviour
{
    [SerializeField] private ProjectileShooter _shooter;
    [SerializeField] private Transform _target;

    private void OnGUI()
    {
        if (GUI.Button(new Rect(20, 20, 220, 50), "ShootAtTarget"))
        {
            if (_shooter == null || _target == null)
            {
                Debug.LogWarning("[ProjectileShootAtTargetTester] ยังไม่ได้ใส่ Shooter หรือ Target");
                return;
            }

            _shooter.ShootAtTarget(_target.position);
        }
    }
}
