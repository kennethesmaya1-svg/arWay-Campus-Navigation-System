using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DestinationButton : MonoBehaviour
{
    [SerializeField] private TMP_Text label;

    private int buildingId;
    private string buildingNodeId;
    private string destinationType;
    private DestinationButtonController controller;

    public void Setup(
        int buildingId,
        string buildingNodeId,
        string displayName,
        string destinationType,
        DestinationButtonController controller)
    {
        this.buildingId = buildingId;
        this.buildingNodeId = buildingNodeId;
        this.destinationType = destinationType;
        this.controller = controller;

        label.text = displayName;
        // Debug.Log(
        // $"[DestinationButton] " +
        // $"{displayName} | TYPE = '{destinationType}'"
    // );
    }

    public void OnClick()
    {
        controller.SelectDestination(
            buildingId,
            buildingNodeId
        );
    }

    public bool MatchesSearch(string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
            return true;

        if (label == null)
            return false;

        return label.text.IndexOf(
            searchText,
            System.StringComparison.OrdinalIgnoreCase
        ) >= 0;
    }

    public bool MatchesType(string selectedType)
    {
        if (string.IsNullOrWhiteSpace(selectedType) ||
            selectedType == "All")
        {
            return true;
        }

        return string.Equals(
            destinationType,
            selectedType,
            System.StringComparison.OrdinalIgnoreCase
        );
    }
}