using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LocationGateManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject _gatePanel;
    [SerializeField] private TextMeshProUGUI _messageText;
    [SerializeField] private Button _openSettingsBtn;
    [SerializeField] private Button _exitAppBtn;

    [Header("Settings")]
    [Tooltip("How often (seconds) to check location permission while app is running")]
    [SerializeField] private float _checkInterval = 1.5f;

    [Tooltip("Delay before re-checking after returning from Settings")]
    [SerializeField] private float _recheckDelay = 1.0f;
    [SerializeField] private LocationServiceManager _locationService;

#if UNITY_EDITOR
    [Header("Editor Testing")]
    [Tooltip("Enable this to simulate Location Services being disabled in Unity Editor.")]
    [SerializeField] private bool _simulateLocationDisabled = false;
#endif

    private bool _lastKnownState;
    private bool _hasCheckedLocation = false;

    void Start()
    {
        if (_locationService == null)
            _locationService = FindFirstObjectByType<LocationServiceManager>();

        // Connect buttons automatically
        if (_openSettingsBtn != null)
            _openSettingsBtn.onClick.AddListener(OnOpenSettings);

        if (_exitAppBtn != null)
            _exitAppBtn.onClick.AddListener(OnExitApp);

        // Start hidden
        if (_gatePanel != null)
            _gatePanel.SetActive(false);

        StartCoroutine(InitialCheck());
        StartCoroutine(ContinuousCheck());
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
            StartCoroutine(RecheckAfterReturn());
    }

    private IEnumerator InitialCheck()
    {
        yield return null;

        CheckAndUpdate();
    }

    private IEnumerator ContinuousCheck()
    {
        while (true)
        {
            yield return new WaitForSeconds(_checkInterval);
            CheckAndUpdate();
        }
    }

    private IEnumerator RecheckAfterReturn()
    {
        yield return new WaitForSeconds(_recheckDelay);
        CheckAndUpdate();
    }

    private void CheckAndUpdate()
    {
        bool isEnabled;

#if UNITY_EDITOR
        // Editor testing
        isEnabled = !_simulateLocationDisabled;
#else
        // Real Android/iOS device
        isEnabled = _locationService != null && _locationService.IsLocationEnabled;
#endif

        if (_hasCheckedLocation && isEnabled == _lastKnownState)
            return;

        _lastKnownState = isEnabled;
        _hasCheckedLocation = true;

        if (!isEnabled)
        {
            ShowGate();
        }
        else
        {
            HideGate();
            Debug.Log("[LocationGate] Location services enabled — gate dismissed.");
        }
    }

    private void ShowGate()
    {
        if (_gatePanel != null)
            _gatePanel.SetActive(true);

        if (_messageText != null)
        {
            _messageText.text = "Please enable Location in your device settings to continue.";
        }

        Debug.Log("[LocationGate] Location off — showing gate.");
    }

    private void HideGate()
    {
        if (_gatePanel != null)
            _gatePanel.SetActive(false);
    }

    private void OnOpenSettings()
    {
        Debug.Log("[LocationGate] Opening device settings...");

#if UNITY_EDITOR

        // Unity Editor cannot open Android Location Settings.
        Debug.Log("[LocationGate] Editor: Device Settings cannot be opened in Unity Editor.");

#elif UNITY_ANDROID

        using (var unityClass =
               new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        using (var activity =
               unityClass.GetStatic<AndroidJavaObject>("currentActivity"))
        using (var intent =
               new AndroidJavaObject(
                   "android.content.Intent",
                   "android.settings.LOCATION_SOURCE_SETTINGS"))
        {
            activity.Call("startActivity", intent);
        }

#elif UNITY_IOS

        Application.OpenURL("app-settings:");

#else

        Application.OpenURL("app-settings:");

#endif
    }

    private void OnExitApp()
    {
        Debug.Log("[LocationGate] User chose to exit.");

#if UNITY_EDITOR

        UnityEditor.EditorApplication.isPlaying = false;

#else

        Application.Quit();

#endif
    }
}