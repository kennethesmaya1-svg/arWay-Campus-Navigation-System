using UnityEngine;

public class LookAtCamera : MonoBehaviour
{
    private Transform camTransform;

    private void Start()
    {
        if (Camera.main != null)
            camTransform = Camera.main.transform;
    }

    private void LateUpdate()
    {
        if (camTransform != null)
        {
            // Keep icon billboarded towards the user's phone camera
            transform.LookAt(transform.position + camTransform.rotation * Vector3.forward,
                             camTransform.rotation * Vector3.up);
        }
    }
}
