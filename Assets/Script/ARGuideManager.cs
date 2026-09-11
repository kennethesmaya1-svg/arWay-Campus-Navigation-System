using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ARGuideManager : MonoBehaviour
{
    [Header("Guide")]
    [SerializeField] private GameObject guideCharacterPrefab;

    [Header("Character Guide UI")]
    [SerializeField] private Image CharacterBtnImage;
    [SerializeField] private Text CharacterBtnLabel;
    [SerializeField] private Sprite CharacterON;
    [SerializeField] private Sprite CharacterOFF;

    [Header("Spawn")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private float spawnDistance = 2f;
    [SerializeField] private float spawnHeightOffset = 0f;

    private GameObject activeGuide;
    private ARGuideCharacter guideController;

    public bool HasGuide => activeGuide != null;

    public void SpawnGuide()
    {
        RemoveGuide();

        if (guideCharacterPrefab == null)
        {
            Debug.LogError("ARGuideManager: Guide Character Prefab is not assigned.");
            return;
        }

        if (cameraTransform == null)
        {
            Camera mainCamera = Camera.main;

            if (mainCamera != null)
                cameraTransform = mainCamera.transform;
        }

        if (cameraTransform == null)
        {
            Debug.LogError("ARGuideManager: Camera Transform is missing.");
            return;
        }

        Vector3 spawnPosition =
            cameraTransform.position +
            cameraTransform.forward * spawnDistance;

        spawnPosition.y += spawnHeightOffset;

        Quaternion spawnRotation = cameraTransform.rotation;
        spawnRotation.x = 0f;
        spawnRotation.z = 0f;

        activeGuide = Instantiate(
            guideCharacterPrefab,
            spawnPosition,
            spawnRotation
        );

        guideController =
            activeGuide.GetComponent<ARGuideCharacter>();

        if (guideController == null)
        {
            guideController =
                activeGuide.AddComponent<ARGuideCharacter>();
        }

        activeGuide.SetActive(true);
        UpdateCharacterButton(true);
        Debug.Log("ARGuideManager: Guide character spawned.");
    }

    public void CharacterButtonClicked()
    {
        if (activeGuide == null)
        {
            Debug.LogWarning("ARGuideManager: Cannot toggle character because it has not been spawned.");
            return;
        }

        bool isVisible = !activeGuide.activeSelf;
        activeGuide.SetActive(isVisible);
        UpdateCharacterButton(isVisible);

        Debug.Log(
            isVisible
                ? "ARGuideManager: Guide character shown."
                : "ARGuideManager: Guide character hidden."
        );
    }

    public void StartGuide(List<Transform> path)
    {
        if (guideController == null)
        {
            SpawnGuide();
        }

        if (guideController == null)
            return;

        guideController.StartGuiding(path);
    }

    public void RemoveGuide()
    {
        if (activeGuide != null)
        {
            Destroy(activeGuide);
        }

        activeGuide = null;
        guideController = null;
        UpdateCharacterButton(false);
    }

    private void UpdateCharacterButton(bool isVisible)
    {
        if (CharacterBtnImage != null)
        {
            CharacterBtnImage.sprite = isVisible ? CharacterON : CharacterOFF;
        }

        if (CharacterBtnLabel != null)
        {
            CharacterBtnLabel.text = isVisible ? "Hide Character" : "Show Character";
        }
    }
}