using UnityEngine;

public class TextRotationCompensator : MonoBehaviour
{
    private static readonly Quaternion CompensatedRotation = Quaternion.Euler(0f, 0f, 180f);
    private static readonly Quaternion DefaultRotation = Quaternion.identity;

    void LateUpdate()
    {
        // Check if the parent object's "up" vector is pointing downward
        transform.localRotation = transform.parent.up.y < 0 ? CompensatedRotation : DefaultRotation;
    }
}
