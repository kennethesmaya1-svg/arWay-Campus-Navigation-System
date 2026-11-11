
using UnityEngine;

public class ExitBtn : MonoBehaviour
{
    [Header("Exit Confirmation Panel")]
    [SerializeField] private GameObject exitConfirmationPanel;

    private void Start()
    {
        // Hide the confirmation panel when the app starts.
        if (exitConfirmationPanel != null)
        {
            exitConfirmationPanel.SetActive(false);
        }
    }

    // Called when the main Exit button is clicked.
    public void OnClickExitButton()
    {
        if (exitConfirmationPanel != null)
        {
            exitConfirmationPanel.SetActive(true);
        }
    }

    // Called when the Cancel button is clicked.
    public void OnClickCancelButton()
    {
        if (exitConfirmationPanel != null)
        {
            exitConfirmationPanel.SetActive(false);
        }
    }

    // Called when the confirmation Exit button is clicked.
    public void OnClickConfirmExitButton()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
