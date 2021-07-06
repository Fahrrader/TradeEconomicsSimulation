using System;
using System.Collections.Generic;
using System.Globalization;
using ScriptableObjects;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UI
{
    public class CellEditor : EditorPanel
    {
        //public static bool currentlyEditing;
    
        //public GameObject cellEditPanel;
        public Text coordinatesText;
        public Dropdown biomeDropdown;
        public InputField altitudeText;
        public InputField waterLevelText;
        public InputField rainfallText;
        public InputField temperatureText;
    
        public Slider[] roadSliders;
        public Slider[] riverSliders;

        public GameObject resourceTemplate;
        public GameObject resourceContentPanel;

        public Dropdown resourceDropdown;
        public InputField resourceAmountInput;
        public InputField resourceBalanceInput;
        public InputField resourceCostInput;

        private RectTransform contentRectTransform;
        private readonly List<GameObject> resourcePanelList = new List<GameObject>();
        
        private Biome biome;
        private float altitude;
        private float waterLevel;
        private float rainfall;
        private float temperature;

        private int hiddenIndex;

        public HexCell hexCell;
        
        private void Start()
        {
            Close();

            contentRectTransform = resourceContentPanel.GetComponent<RectTransform>();
            
            resourceDropdown.ClearOptions();
            foreach (var resource in ResourceHolder.resources) 
                resourceDropdown.options.Add(new Dropdown.OptionData(resource.label));
        }

        public override void Close()
        {
            base.Close();
            if (hexCell) hexCell.SetHighlight(false);
        }
        
        public override void Load()
        {
            SetActive();
            
            var cell = hexCell.dataCell;

            biome = cell.Biome.biome;
            altitude = cell.Altitude;
            waterLevel = cell.WaterLevel;
            rainfall = cell.rainfall;
            temperature = cell.temperature;

            coordinatesText.text = hexCell.dataCell.position.ToString();
            altitudeText.text = altitude.ToString(CultureInfo.CurrentCulture);
            waterLevelText.text = waterLevel.ToString(CultureInfo.CurrentCulture);
            rainfallText.text = rainfall.ToString(CultureInfo.CurrentCulture);
            temperatureText.text = temperature.ToString(CultureInfo.CurrentCulture);

            foreach (HexDirection dir in Enum.GetValues(typeof(HexDirection)))
            {
                riverSliders[(int) dir].value = cell.HexCell.GetRiverValue(dir);
                roadSliders[(int) dir].value = cell.GetRoadValue(dir);
            }

            biomeDropdown.ClearOptions();
            var i = 0;
            foreach (Biome b in Enum.GetValues(typeof(Biome)))
            {
                biomeDropdown.options.Add(new Dropdown.OptionData(b.ToString()));
                if (b == cell.Biome.biome)
                {
                    biomeDropdown.value = i;
                    break;
                }
                i++;
            }
            biomeDropdown.RefreshShownValue();

            hiddenIndex = 0;

            foreach (var panel in resourcePanelList)
                Destroy(panel);

            contentRectTransform.sizeDelta = new Vector2(contentRectTransform.sizeDelta.x, 50 * cell.resources.Count);

            resourcePanelList.Clear();
            foreach (var resource in cell.resources)
            {
                for (var j = 0; j < ResourceHolder.resources.Count; j++)// resourceData in ResourceHolder.resources)
                {
                    if (ResourceHolder.resources[j].label != resource.Data.label) continue; 
                    resourceDropdown.value = j;
                    break;
                }
                resourceDropdown.RefreshShownValue();

                AddResource(new Resource(resource.Data, resource.AmountInt, resource.Balance, resource.harvestCost), hiddenIndex++);
            }
        }
        
        public override void Save()
        {
            var cell = hexCell.dataCell;
            cell.SetBiomeCarefully(ResourceHolder.biomes[(int)biome]);
            cell.Altitude = altitude;
            cell.WaterLevel = waterLevel;
            cell.rainfall = rainfall;
            cell.temperature = temperature;

            foreach (HexDirection dir in Enum.GetValues(typeof(HexDirection)))
            {
                cell.HexCell.AddRiver(dir, (sbyte) riverSliders[(int) dir].value);
                cell.SetRoad(dir, roadSliders[(int) dir].value);
            }

            /*if (road)
            {
                if (cell.HexCell.HasRoadThroughEdge(roadDirection))
                    cell.HexCell.RemoveRoads();
                else 
                    cell.HexCell.AddRoad(roadDirection);
            }
            cell.HexCell.Walled = walled;
            cell.HexCell.UrbanLevel = walled ? Random.Range(1, 3) : 0;
            if (river)
                if (!cell.HexCell.HasOutgoingRiver(riverDirection))
                    cell.HexCell.AddOutgoingRiver(riverDirection);
                else
                    cell.HexCell.RemoveRiver(riverDirection);*/
        
            cell.resources.Clear();
            foreach (var panel in resourcePanelList)
            {
                var resourceInput = panel.GetComponentInChildren<Dropdown>();
                var label = resourceInput.options[resourceInput.value].text;
                var data = ResourceHolder.resources.Find(resourceData => label == resourceData.label);
            
                var inputs = panel.GetComponentsInChildren<InputField>();
                var amount = Mathf.Clamp(int.Parse(inputs[0].text), 0, data.maxAmount);
                var balance = Mathf.Clamp(int.Parse(inputs[1].text), 0, data.maxAmount);
                var harvestCost = float.Parse(inputs[2].text);
            
                cell.resources.Add(new Resource(data, amount, balance, harvestCost));
            }
            cell.FindAvailableRecipes();
        }

        public void AddResource()
        {
            var resource = new Resource(ResourceHolder.resources[0], 0);
            resourceDropdown.value = 0;
            AddResource(resource, hiddenIndex++);
        }

        private void AddResource(Resource resource, int index)
        {
            /*if (resources.Any(res => res.data.label == resource.data.label))
            return;*/

            resourceAmountInput.text = resource.AmountInt.ToString();
            resourceBalanceInput.text = resource.Balance.ToString();
            resourceCostInput.text = resource.harvestCost.ToString(CultureInfo.CurrentCulture);

            contentRectTransform.sizeDelta = new Vector2(contentRectTransform.sizeDelta.x, 50f * (resourcePanelList.Count + 1));

            var resourceObject = Instantiate(resourceTemplate, resourceContentPanel.transform, true);
            resourceObject.GetComponent<Text>().text = index.ToString();
            resourceObject.GetComponentInChildren<Button>().onClick
                .AddListener(() => RemoveResource(index.ToString())); //resource.data.label));

            var rectTransform = resourceObject.GetComponent<RectTransform>();
            rectTransform.Translate(Vector3.down * (rectTransform.rect.height * resourcePanelList.Count));
            
            resourceObject.SetActive(true);
            resourcePanelList.Add(resourceObject);
        }

        public void RemoveResource(string index)
        {
            var resourceFound = false;
            for (var i = 0; i < resourcePanelList.Count; i++)
            {
                //var hiddenIndex = resourcePanelList[i].GetComponentInChildren<Text>();
                if (resourcePanelList[i].GetComponent<Text>().text == index)//resources[i].data.label == label)
                {
                    Destroy(resourcePanelList[i]);
                    resourcePanelList.RemoveAt(i);
                    contentRectTransform.sizeDelta = new Vector2(contentRectTransform.sizeDelta.x, 50f * resourcePanelList.Count);
                    resourceFound = true;
                }

                if (!resourceFound || i == resourcePanelList.Count) continue;

                var rectTransform = resourcePanelList[i].GetComponent<RectTransform>();
                rectTransform.Translate(Vector3.up * rectTransform.rect.height);
            }
        }
        
        public void SetBiome()
        {
            Enum.TryParse(biomeDropdown.options[biomeDropdown.value].text, out biome);
        }

        public void SetAltitude(string s)
        {
            if (!float.TryParse(s, out var t)) return;
            altitude = t;
        }

        public void SetWaterLevel(string s)
        {
            if (!float.TryParse(s, out var t)) return;
            waterLevel = t;
        }

        public void SetRainfall(string s)
        {
            if (!float.TryParse(s, out var t)) return;
            rainfall = Mathf.Clamp(t, 0, float.MaxValue);
        }

        public void SetTemperature(string s)
        {
            if (!float.TryParse(s, out var t)) return;
            temperature = t;
        }

        public void SpawnCity()
        {
            hexCell.dataCell.manager.SpawnCity(hexCell.dataCell, hexCell.dataCell.occupants.Count > 0 ? hexCell.dataCell.occupants[0] : null);
        }

        public void SpawnTraveller()
        {
            hexCell.dataCell.manager.SpawnTraveller(hexCell.dataCell, hexCell.dataCell.OccupyingCity);
        }
    }
}
