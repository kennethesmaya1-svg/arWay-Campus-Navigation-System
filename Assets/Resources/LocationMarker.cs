using UnityEngine;
using TMPro;

public class LocationMarker : MonoBehaviour
{
    [SerializeField] private TMP_Text destinationNameText;

    public void SetMarkerName(string text)
    {
        if (destinationNameText != null)
        {
           destinationNameText.text = text;
            Debug.Log($"[LocationMarker] Successfully updated marker label to: '{text}' on object '{gameObject.name}'.");
        }
        else
        {
            Debug.LogWarning($"[LocationMarker] Failed to update text! destinationNameText field is not assigned in the Inspector on '{gameObject.name}'.");
        }
    }
}