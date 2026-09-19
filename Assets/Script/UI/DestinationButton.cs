using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DestinationButton : MonoBehaviour
{
    [SerializeField] private TMP_Text label;

    private int buildingId;
    private string buildingNodeId;
    private DestinationButtonController controller;

    public void Setup(
        int buildingId,
        string buildingNodeId,
        string displayName,
        DestinationButtonController controller)
    {
        this.buildingId = buildingId;
        this.buildingNodeId = buildingNodeId;
        this.controller = controller;

        label.text = displayName;
    }

    public void OnClick()
    {
        controller.SelectDestination(
            buildingId,
            buildingNodeId
        );
    }
}