using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class BuildingApiResponse
{
    public bool success;
    public BuildingApiData buildings;
}

[Serializable]
public class BuildingApiData
{
    public int id;
    public string building_node_id;
    
    public string name;
    public string description;
    public string image;
    public List<string> facilities;
}

[Serializable]
public class FacilityEntry
{
    public string label;
    public Sprite icon;
}

public class BuildingInfo
{
    public int buildingId;
    public string buildingNodeId;

    public string title;
    public string description;
    public Sprite buildingImage;
    public List<FacilityEntry> facilities;
}