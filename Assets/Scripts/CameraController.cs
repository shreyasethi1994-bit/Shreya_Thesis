using UnityEngine;

public class CameraController : MonoBehaviour
{
    [SerializeField] private Transform target;

    [Tooltip("Fixed offset from the target, applied along the target's yaw (not its pitch/roll).")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 3f, -8f);

    [Tooltip("How quickly the camera catches up to its desired position. Higher = snappier.")]
    [SerializeField] private float followSpeed = 5f;

    [Tooltip("How quickly the camera catches up to its desired look angle. Higher = snappier.")]
    [SerializeField] private float rotationSpeed = 5f;

    private void LateUpdate()
    {
        if (target == null) return;

        Quaternion yawOnly = Quaternion.Euler(0f, target.eulerAngles.y, 0f);
        Vector3 desiredPosition = target.position + yawOnly * offset;
        transform.position = Vector3.Lerp(transform.position, desiredPosition, followSpeed * Time.deltaTime);

        Quaternion desiredRotation = Quaternion.LookRotation(target.position - transform.position, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationSpeed * Time.deltaTime);
    }
}
