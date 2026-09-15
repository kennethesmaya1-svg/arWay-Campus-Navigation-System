using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

using Firebase;
using Firebase.Firestore;
using Firebase.Storage;
using Firebase.Extensions;

public class BuildingDataService : MonoBehaviour
{
    [Header("Firebase")]
    [Tooltip("Firestore collection containing building documents.")]
    [SerializeField] private string buildingsCollection = "buildings";

    [Header("Image Settings")]
    [Tooltip("Maximum image size downloaded from Firebase Storage or Cloudinary.")]
    [SerializeField] private long maxImageDownloadBytes = 5 * 1024 * 1024;
    [Tooltip("Optional Cloudinary delivery URL prefix. Leave empty when Firestore stores the complete URL.")]
    [SerializeField] private string cloudinaryUrlPrefix = "";

    [Header("Editor Testing")]
    [Tooltip("When enabled, the component prints detailed Firebase/Firestore logs in the Unity Editor.")]
    [SerializeField] private bool editorDebugLogs = true;

    private FirebaseApp firebaseApp;
    private FirebaseFirestore firestore;
    private FirebaseStorage storage;

    private bool firebaseReady = false;


    // =========================================================
    // UNITY
    // =========================================================

    private void Awake()
    {
        InitializeFirebase();
    }


    // =========================================================
    // FIREBASE INITIALIZATION
    // =========================================================

    private void InitializeFirebase()
    {
        Debug.Log("[BuildingData] Checking Firebase dependencies...");

        FirebaseApp.CheckAndFixDependenciesAsync()
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                {
                    Debug.LogError(
                        "[BuildingData] Firebase dependency check failed: "
                        + task.Exception
                    );

                    return;
                }

                if (task.IsCanceled)
                {
                    Debug.LogError(
                        "[BuildingData] Firebase dependency check was cancelled."
                    );

                    return;
                }

                DependencyStatus status = task.Result;

                if (status != DependencyStatus.Available)
                {
                    Debug.LogError(
                        "[BuildingData] Firebase dependencies are not available: "
                        + status
                    );

                    return;
                }

                firebaseApp = FirebaseApp.DefaultInstance;

                firestore = FirebaseFirestore.DefaultInstance;

                storage = FirebaseStorage.DefaultInstance;

                firebaseReady = true;

                Debug.Log(
                    "[BuildingData] Firebase initialized successfully."
                );

                if (Application.isEditor && editorDebugLogs)
                {
                    Debug.Log(
                        "[BuildingData] Unity Editor Firebase testing is enabled."
                    );
                }
            });
    }


    // =========================================================
    // PUBLIC: GET BUILDING NAME FROM CACHE
    // =========================================================

    public string GetBuildingNameFromCache(int buildingId)
    {
        string cacheKey =
            $"Cached_Building_{buildingId}";

        if (!PlayerPrefs.HasKey(cacheKey))
            return string.Empty;

        string rawJson =
            PlayerPrefs.GetString(cacheKey);

        if (string.IsNullOrEmpty(rawJson))
            return string.Empty;

        // Current cache entries store the name directly.
        if (!rawJson.TrimStart().StartsWith("{"))
            return rawJson;

        try
        {
            BuildingApiResponse response =
                JsonUtility.FromJson<BuildingApiResponse>(rawJson);

            if (response != null &&
                response.success &&
                response.buildings != null)
            {
                return response.buildings.name ?? string.Empty;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning(
                $"[BuildingData] Failed to parse cached name " +
                $"for building ID {buildingId}: {ex.Message}"
            );
        }

        return string.Empty;
    }

    public void FetchBuildingName(
        int buildingId,
        Action<string> onSuccess,
        Action<string> onError)
    {
        string cachedName = GetBuildingNameFromCache(buildingId);

        if (Application.internetReachability == NetworkReachability.NotReachable &&
            !string.IsNullOrEmpty(cachedName))
        {
            Debug.Log(
                $"[BuildingData] Offline. Using cached name for building {buildingId}."
            );
            onSuccess?.Invoke(cachedName);
            return;
        }

        FetchBuilding(
            buildingId,
            info =>
            {
                string name = info != null ? info.title : string.Empty;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    onSuccess?.Invoke(name);
                }
                else if (!string.IsNullOrEmpty(cachedName))
                {
                    onSuccess?.Invoke(cachedName);
                }
                else
                {
                    onError?.Invoke(
                        $"[BuildingData] Building {buildingId} has no name."
                    );
                }
            },
            error =>
            {
                if (!string.IsNullOrEmpty(cachedName))
                {
                    Debug.Log(
                        $"[BuildingData] Using cached name for building {buildingId}."
                    );
                    onSuccess?.Invoke(cachedName);
                    return;
                }

                onError?.Invoke(error);
            }
        );
    }


    // =========================================================
    // PUBLIC: FETCH BUILDING USING INTEGER ID
    // =========================================================

    public void FetchBuilding(
        int buildingId,
        Action<BuildingInfo> onSuccess,
        Action<string> onError)
    {
        FetchBuilding(
            buildingId.ToString(),
            onSuccess,
            onError
        );
    }


    // =========================================================
    // PUBLIC: FETCH BUILDING USING FIRESTORE DOCUMENT ID
    // =========================================================

    public void FetchBuilding(
        string buildingId,
        Action<BuildingInfo> onSuccess,
        Action<string> onError)
    {
        StartCoroutine(
            FetchBuildingRoutine(
                buildingId,
                onSuccess,
                onError
            )
        );
    }


    // =========================================================
    // FETCH BUILDING
    // =========================================================

    private IEnumerator FetchBuildingRoutine(
        string buildingId,
        Action<BuildingInfo> onSuccess,
        Action<string> onError)
    {
        // -----------------------------------------------------
        // WAIT FOR FIREBASE
        // -----------------------------------------------------

        float timeout = 15f;
        float elapsed = 0f;

        while (!firebaseReady && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!firebaseReady)
        {
            string error =
                "[BuildingData] Firebase is not ready.";

            Debug.LogError(error);

            onError?.Invoke(error);

            yield break;
        }


        // -----------------------------------------------------
        // FIRESTORE DOCUMENT
        // -----------------------------------------------------

        // Debug.Log(
        //     $"[BuildingData] Requesting Firestore document: " +
        //     $"buildings/{buildingId}"
        // );

        DocumentReference document =
            firestore
                .Collection(buildingsCollection)
                .Document(buildingId);


        var firestoreTask =
            document.GetSnapshotAsync();


        // -----------------------------------------------------
        // WAIT FOR FIRESTORE
        // -----------------------------------------------------

        while (!firestoreTask.IsCompleted)
        {
            yield return null;
        }


        // -----------------------------------------------------
        // FIRESTORE ERROR
        // -----------------------------------------------------

        if (firestoreTask.IsFaulted)
        {
            string error =
                GetFirebaseTaskError(
                    firestoreTask.Exception
                );

            Debug.LogError(
                "[BuildingData] Firestore request failed: "
                + error
            );

            onError?.Invoke(error);

            yield break;
        }


        if (firestoreTask.IsCanceled)
        {
            string error =
                "[BuildingData] Firestore request was cancelled.";

            Debug.LogError(error);

            onError?.Invoke(error);

            yield break;
        }


        // -----------------------------------------------------
        // GET SNAPSHOT
        // -----------------------------------------------------

        DocumentSnapshot snapshot =
            firestoreTask.Result;


        if (!snapshot.Exists)
        {
            string error =
                $"[BuildingData] Building document " +
                $"'{buildingId}' does not exist.";

            Debug.LogWarning(error);

            onError?.Invoke(error);

            yield break;
        }


        // Debug.Log(
        //     $"[BuildingData] Successfully received " +
        //     $"buildings/{buildingId}"
        // );


        // -----------------------------------------------------
        // CONVERT FIRESTORE DATA
        // -----------------------------------------------------

        Dictionary<string, object> data =
            snapshot.ToDictionary();


        // -----------------------------------------------------
        // BUILDING INFORMATION
        // -----------------------------------------------------

        string name =
            GetString(data, "name");

        string description =
            GetString(data, "description");


        // -----------------------------------------------------
        // FACILITIES
        // -----------------------------------------------------

        List<FacilityEntry> facilities =
            new List<FacilityEntry>();


        if (data.TryGetValue("facilities", out object facilitiesObject))
        {
            if (facilitiesObject is List<object> facilityList)
            {
                foreach (object facility in facilityList)
                {
                    if (facility == null)
                        continue;

                    string facilityName =
                        facility.ToString();

                    if (!string.IsNullOrWhiteSpace(facilityName))
                    {
                        facilities.Add(
                            new FacilityEntry
                            {
                                label = facilityName,
                                icon = null
                            }
                        );
                    }
                }
            }
        }


        // -----------------------------------------------------
        // CREATE BUILDING INFO
        // -----------------------------------------------------

        BuildingInfo info =
            new BuildingInfo
            {
                title = name,
                description = description,
                facilities = facilities
            };


        // -----------------------------------------------------
        // IMAGE
        // -----------------------------------------------------

        string imagePath =
            GetImageUrl(data);


        if (!string.IsNullOrWhiteSpace(imagePath))
        {
            yield return StartCoroutine(
                DownloadBuildingImage(
                    imagePath,
                    buildingId,
                    info,
                    onError
                )
            );
        }


        // -----------------------------------------------------
        // SAVE BASIC CACHE
        // -----------------------------------------------------

        SaveBuildingNameToCache(
            buildingId,
            name
        );


        // -----------------------------------------------------
        // SUCCESS
        // -----------------------------------------------------

        onSuccess?.Invoke(info);
    }


    // =========================================================
    // DOWNLOAD BUILDING IMAGE
    // =========================================================

    private IEnumerator DownloadBuildingImage(
        string imagePath,
        string buildingId,
        BuildingInfo info,
        Action<string> onError)
    {
        string localImagePath =
            Path.Combine(
                Application.persistentDataPath,
                $"building_{buildingId}.png"
            );


        // -----------------------------------------------------
        // FIREBASE STORAGE PATH
        // -----------------------------------------------------

        string storagePath =
            imagePath.Trim();


        // -----------------------------------------------------
        // IF FIRESTORE STORES A FULL HTTPS URL
        // -----------------------------------------------------

        if (storagePath.StartsWith("http://") ||
            storagePath.StartsWith("https://"))
        {
            if (IsCloudinaryUrl(storagePath))
            {
                // // Debug.Log(
                //     $"[BuildingData] Downloading building image from Cloudinary: " +
                //     $"{storagePath}"
                // );
            }

            yield return StartCoroutine(
                DownloadImageFromUrl(
                    storagePath,
                    localImagePath,
                    info
                )
            );

            yield break;
        }

        if (!string.IsNullOrWhiteSpace(cloudinaryUrlPrefix))
        {
            string cloudinaryUrl =
                cloudinaryUrlPrefix.TrimEnd('/') + "/" +
                storagePath.TrimStart('/');

            Debug.Log(
                $"[BuildingData] Downloading Cloudinary image: " +
                $"{cloudinaryUrl}"
            );

            yield return StartCoroutine(
                DownloadImageFromUrl(
                    cloudinaryUrl,
                    localImagePath,
                    info
                )
            );

            yield break;
        }


        // -----------------------------------------------------
        // IF FIRESTORE STORES gs:// URL
        // -----------------------------------------------------

        if (storagePath.StartsWith("gs://"))
        {
            yield return StartCoroutine(
                DownloadImageFromStorageReference(
                    storagePath,
                    localImagePath,
                    info,
                    onError
                )
            );

            yield break;
        }


        // -----------------------------------------------------
        // OTHERWISE TREAT AS FIREBASE STORAGE PATH
        // -----------------------------------------------------

        // Debug.Log(
        //     $"[BuildingData] Downloading Storage file: " +
        //     $"{storagePath}"
        // );

                Debug.Log(
            $"[BuildingData] Firebase Storage download attempt:\n" +
            $"Building ID: {buildingId}\n" +
            $"Storage Path: '{storagePath}'\n" +
            $"Max Bytes: {maxImageDownloadBytes}"
        );


        StorageReference storageReference =
            storage
                .GetReference(storagePath);


        var downloadTask =
            storageReference.GetBytesAsync(
                maxImageDownloadBytes
            );


        while (!downloadTask.IsCompleted)
        {
            yield return null;
        }


        // if (downloadTask.IsFaulted ||
        //     downloadTask.IsCanceled)
        // {
        //     // string error =
        //     //     "[BuildingData] Firebase Storage image " +
        //     //     "download failed.";

        //     // Debug.LogWarning(error);

        //     string firebaseError = "Unknown Firebase Storage error.";

        //     if (downloadTask.Exception != null)
        //     {
        //         firebaseError =
        //             downloadTask.Exception.GetBaseException().Message;
        //     }

        //     Debug.LogError(
        //         "[BuildingData] Firebase Storage image download failed.\n" +
        //         $"Building ID: {buildingId}\n" +
        //         $"Storage Path: {storagePath}\n" +
        //         $"Error: {firebaseError}"
        //     );


        //     // Try offline image cache.

        //     if (File.Exists(localImagePath))
        //     {
        //         info.buildingImage =
        //             LoadSpriteFromDisk(
        //                 localImagePath
        //             );
        //     }

        //     yield break;
        // }

        if (downloadTask.IsFaulted || downloadTask.IsCanceled)
        {
            string firebaseError = "Unknown Firebase Storage error.";

            if (downloadTask.Exception != null)
            {
                firebaseError =
                    downloadTask.Exception.GetBaseException().ToString();
            }

            Debug.LogError(
                $"[BuildingData] FIREBASE STORAGE DOWNLOAD FAILED\n" +
                $"Building ID: {buildingId}\n" +
                $"Storage Path: {storagePath}\n" +
                $"Max Download Bytes: {maxImageDownloadBytes}\n" +
                $"Firebase Error:\n{firebaseError}"
            );

            // Try offline image cache.
            if (File.Exists(localImagePath))
            {
                info.buildingImage =
                    LoadSpriteFromDisk(localImagePath);

                Debug.Log(
                    $"[BuildingData] Using cached image for building {buildingId}."
                );
            }

            yield break;
        }


        byte[] imageBytes =
            downloadTask.Result;


        File.WriteAllBytes(
            localImagePath,
            imageBytes
        );


        Texture2D texture =
            new Texture2D(2, 2);


        if (!texture.LoadImage(imageBytes))
        {
            Debug.LogWarning(
                "[BuildingData] Firebase image could not be decoded."
            );

            yield break;
        }


        info.buildingImage =
            Sprite.Create(
                texture,
                new Rect(
                    0,
                    0,
                    texture.width,
                    texture.height
                ),
                new Vector2(
                    0.5f,
                    0.5f
                )
            );
    }


    // =========================================================
    // DOWNLOAD STORAGE gs:// URL
    // =========================================================

    private IEnumerator DownloadImageFromStorageReference(
        string gsUrl,
        string localImagePath,
        BuildingInfo info,
        Action<string> onError)
    {
        StorageReference reference;

        try
        {
            reference =
                storage.GetReferenceFromUrl(
                    gsUrl
                );
        }
        catch (Exception ex)
        {
            string error =
                $"[BuildingData] Invalid Firebase Storage URL: " +
                $"{ex.Message}";

            Debug.LogError(error);

            onError?.Invoke(error);

            yield break;
        }


        var task =
            reference.GetBytesAsync(
                maxImageDownloadBytes
            );


        while (!task.IsCompleted)
        {
            yield return null;
        }


        if (task.IsFaulted ||
            task.IsCanceled)
        {
            Debug.LogWarning(
                "[BuildingData] Could not download image from gs:// URL."
            );

            yield break;
        }


        byte[] bytes =
            task.Result;


        File.WriteAllBytes(
            localImagePath,
            bytes
        );


        Texture2D texture =
            new Texture2D(2, 2);


        if (texture.LoadImage(bytes))
        {
            info.buildingImage =
                Sprite.Create(
                    texture,
                    new Rect(
                        0,
                        0,
                        texture.width,
                        texture.height
                    ),
                    new Vector2(
                        0.5f,
                        0.5f
                    )
                );
        }
    }


    // =========================================================
    // DOWNLOAD IMAGE FROM HTTPS
    // =========================================================

    private IEnumerator DownloadImageFromUrl(
        string url,
        string localImagePath,
        BuildingInfo info)
    {
        using (UnityWebRequest request =
               UnityWebRequestTexture.GetTexture(url))
        {
            request.timeout = 10;

            yield return request.SendWebRequest();


            if (request.result ==
                UnityWebRequest.Result.Success)
            {
                Texture2D texture =
                    DownloadHandlerTexture.GetContent(
                        request
                    );


                byte[] bytes =
                    texture.EncodeToPNG();


                File.WriteAllBytes(
                    localImagePath,
                    bytes
                );


                info.buildingImage =
                    Sprite.Create(
                        texture,
                        new Rect(
                            0,
                            0,
                            texture.width,
                            texture.height
                        ),
                        new Vector2(
                            0.5f,
                            0.5f
                        )
                    );
            }
            else
            {
                Debug.LogWarning(
                    $"[BuildingData] Image download failed: " +
                    $"{request.error}"
                );


                if (File.Exists(localImagePath))
                {
                    info.buildingImage =
                        LoadSpriteFromDisk(
                            localImagePath
                        );
                }
            }
        }
    }


    // =========================================================
    // LOAD IMAGE FROM LOCAL CACHE
    // =========================================================

    private Sprite LoadSpriteFromDisk(
        string filePath)
    {
        try
        {
            byte[] fileData =
                File.ReadAllBytes(filePath);


            Texture2D texture =
                new Texture2D(2, 2);


            if (texture.LoadImage(fileData))
            {
                return Sprite.Create(
                    texture,
                    new Rect(
                        0,
                        0,
                        texture.width,
                        texture.height
                    ),
                    new Vector2(
                        0.5f,
                        0.5f
                    )
                );
            }
        }
        catch (Exception ex)
        {
            Debug.LogError(
                $"[BuildingData] Failed loading local " +
                $"image: {ex.Message}"
            );
        }


        return null;
    }


    // =========================================================
    // CACHE NAME
    // =========================================================

    private void SaveBuildingNameToCache(
        string buildingId,
        string buildingName)
    {
        if (string.IsNullOrEmpty(buildingName))
            return;

        string cacheKey =
            $"Cached_Building_{buildingId}";

        PlayerPrefs.SetString(
            cacheKey,
            buildingName
        );

        PlayerPrefs.Save();
    }


    // =========================================================
    // FIRESTORE STRING HELPER
    // =========================================================

    private string GetString(
        Dictionary<string, object> data,
        string field)
    {
        if (!data.TryGetValue(
                field,
                out object value))
        {
            return string.Empty;
        }

        return value?.ToString() ?? string.Empty;
    }

    private string GetImageUrl(
        Dictionary<string, object> data)
    {
        string[] imageFields =
            {
                "imageUrl",
                "image_url",
                "cloudinaryUrl",
                "cloudinary_url",
                "image"
            };

        foreach (string field in imageFields)
        {
            if (!data.TryGetValue(field, out object value) ||
                value == null)
            {
                continue;
            }

            if (value is Dictionary<string, object> imageData)
            {
                string nestedUrl =
                    GetString(imageData, "url");

                if (string.IsNullOrWhiteSpace(nestedUrl))
                {
                    nestedUrl =
                        GetString(imageData, "secure_url");
                }

                if (!string.IsNullOrWhiteSpace(nestedUrl))
                    return nestedUrl;

                continue;
            }

            string imageUrl = value.ToString();

            if (!string.IsNullOrWhiteSpace(imageUrl))
                return imageUrl;
        }

        return string.Empty;
    }

    private bool IsCloudinaryUrl(
        string url)
    {
        return Uri.TryCreate(
                   url,
                   UriKind.Absolute,
                   out Uri parsedUri)
               && parsedUri.Host.IndexOf(
                      "cloudinary.com",
                      StringComparison.OrdinalIgnoreCase) >= 0;
    }


    // =========================================================
    // FIREBASE ERROR
    // =========================================================

    private string GetFirebaseTaskError(
        AggregateException exception)
    {
        if (exception == null)
            return "Unknown Firebase error.";

        Exception inner =
            exception.GetBaseException();

        return inner != null
            ? inner.Message
            : exception.Message;
    }
}