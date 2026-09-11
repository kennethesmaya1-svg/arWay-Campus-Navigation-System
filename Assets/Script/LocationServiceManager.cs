using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Owns Unity's device-location lifecycle. Other navigation components may
/// read GPS state through this component, but only this component starts or
/// stops the service.
/// </summary>
public class LocationServiceManager : MonoBehaviour
{
    [Header("GPS Settings")]
    [SerializeField] private bool _startOnAwake = true;
    [SerializeField, Min(1f)] private float _desiredAccuracyMeters = 1f;
    [SerializeField, Min(0.1f)] private float _updateDistanceMeters = 0.5f;
    [SerializeField, Min(1f)] private float _startupTimeoutSeconds = 20f;

    private Coroutine _startupRoutine;

    public bool IsRunning => Input.location.status == LocationServiceStatus.Running;
    public bool IsStarting => Input.location.status == LocationServiceStatus.Initializing;
    public bool IsLocationEnabled => Input.location.isEnabledByUser;
    public bool HasUsableFix => IsRunning && Input.location.lastData.horizontalAccuracy >= 0f;
    public float HorizontalAccuracy => HasUsableFix ? Input.location.lastData.horizontalAccuracy : -1f;
    public LocationInfo LastLocation => Input.location.lastData;
    public string LastFailure { get; private set; }

    public event Action OnLocationReady;
    public event Action<string> OnLocationFailed;

    private void Awake()
    {
        if (_startOnAwake)
            StartLocationService();
    }

    public void StartLocationService()
    {
        if (IsRunning || _startupRoutine != null)
            return;

        LastFailure = null;
        _startupRoutine = StartCoroutine(StartLocationRoutine());
    }

    public void StopLocationService()
    {
        if (_startupRoutine != null)
        {
            StopCoroutine(_startupRoutine);
            _startupRoutine = null;
        }

        if (IsRunning || IsStarting)
            Input.location.Stop();
    }

    private IEnumerator StartLocationRoutine()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(
                UnityEngine.Android.Permission.FineLocation))
        {
            UnityEngine.Android.Permission.RequestUserPermission(
                UnityEngine.Android.Permission.FineLocation);

            float permissionWait = 0f;
            while (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(
                       UnityEngine.Android.Permission.FineLocation) &&
                   permissionWait < _startupTimeoutSeconds)
            {
                permissionWait += Time.unscaledDeltaTime;
                yield return null;
            }

            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(
                    UnityEngine.Android.Permission.FineLocation))
            {
                Fail("Location permission was not granted.");
                yield break;
            }
        }
#endif

        if (!Input.location.isEnabledByUser)
        {
            Fail("Location services are disabled on this device.");
            yield break;
        }

        Input.location.Start(_desiredAccuracyMeters, _updateDistanceMeters);
        float elapsed = 0f;
        while (Input.location.status == LocationServiceStatus.Initializing &&
               elapsed < _startupTimeoutSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!IsRunning)
        {
            Fail("Unable to acquire a GPS location fix.");
            yield break;
        }

        _startupRoutine = null;
        OnLocationReady?.Invoke();
    }

    private void Fail(string message)
    {
        LastFailure = message;
        _startupRoutine = null;
        Debug.LogWarning($"LocationServiceManager: {message}");
        OnLocationFailed?.Invoke(message);
    }

    private void OnDestroy() => StopLocationService();
}
