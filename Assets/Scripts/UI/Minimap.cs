using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using ScriptableObjects;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

namespace UI
{
    public class Minimap : MonoBehaviour
    {
        //public static bool currentlyEditing;
    
        /*public GameObject cellEditPanel;
        public Text coordinatesText;
        public Dropdown biomeDropdown;
        public InputField altitudeText;
        public InputField waterLevelText;
        public InputField rainfallText;
        public InputField temperatureText;
    
        public Slider[] riverSliders;
        public Slider[] roadSliders;

        public GameObject resourceTemplate;
        public List<GameObject> resourcePanelList;
        public GameObject resourceContentPanel;

        public Dropdown resourceDropdown;
        public InputField resourceAmountInput;
        public InputField resourceBalanceInput;
        public InputField resourceCostInput;*/

        /*public Button playButton;
        public Button pauseButton;
        public Slider speedSlider;
        public InputField speedInput;
        public Text dateText;
        public Text timeText;
    
        private int currentSeason = -1;*/

        // private bool isPaused = false;

        /*private RectTransform contentRectTransform;

        private Biome biome;
        private float altitude;
        private float waterLevel;
        private float rainfall;
        private float temperature;
        private readonly List<Resource> resources = new List<Resource>();*/

        //private HexCell hexCell;

        public WorldManager manager;

        public Color resourceColor;
    
        public RawImage minimap;
        public Dropdown minimapSelection;
        public RectTransform minimapRT;

        private static bool _updateMap = false;

        private void Awake()
        {
            /*cellEditPanel.SetActive(false);

            contentRectTransform = resourceContentPanel.GetComponent<RectTransform>();*/
        
            //resourceDropdown.ClearOptions();
            
        }

        private void Start()
        {
            minimapSelection.ClearOptions();
            minimapSelection.options.Add(new Dropdown.OptionData("Default"));
            minimapSelection.options.Add(new Dropdown.OptionData("Rainfall"));
            minimapSelection.options.Add(new Dropdown.OptionData("Water Level"));
            minimapSelection.options.Add(new Dropdown.OptionData("Temperature"));
            foreach (var resource in ResourceHolder.resources)
            {
                //resourceDropdown.options.Add(new Dropdown.OptionData(resource.label));
                minimapSelection.options.Add(new Dropdown.OptionData(resource.label));
            }
            
            UpdateMinimap();
        }
        
        private void Update()
        {
            //UpdateDate();

            /*if (
                Input.GetMouseButtonUp(0) &&
                !EventSystem.current.IsPointerOverGameObject()
            )
                HandleInput();
        
            if (Input.GetButtonUp("Cancel"))
                CloseEditPanel();
        
            if (Input.GetButtonUp("Submit") && cellEditPanel.activeSelf)
                SaveCell();
            else */
            
            if (_updateMap/* && minimapSelection.value == 0*/) // update values only if the default layout is selected
            {
                UpdateMinimap(); 
                _updateMap = false;   
            }

            /*if (Input.GetButtonDown("Jump"))
                SetPaused(!isPaused);*/
        }
        
        public static void RefreshMinimap()
        {
            _updateMap = true;
        }

        private void UpdateMinimap()
        {
            var colorMap = new Color[manager.WorldSizeX * manager.WorldSizeZ];
            var count = 0;

            for (int z = 0, i = 0; z < manager.cells.GetLength(1); z++)
            {
                for (var x = 0; x < manager.cells.GetLength(0); x++)
                {
                    var cell = manager.cells[x, z];
                    if (minimapSelection.value == 0)
                    {
                        if (cell.occupants.Count > 0)
                            colorMap[i] = Color.magenta;
                        else if (cell.HexCell.Walled)
                            colorMap[i] = Color.red;
                        else colorMap[i] = cell.Color;
                    }
                    else if (minimapSelection.value == 1) colorMap[i] = GetRainfallColor(cell.rainfall);
                    else if (minimapSelection.value == 2) colorMap[i] = GetRainfallColor(cell.WaterLevel * 100);
                    else if (minimapSelection.value == 3) colorMap[i] = GetTemperatureColor(cell.temperature);
                    else
                    {
                        colorMap[i] = Color.black;
                        if (cell.resources.Any(resource =>
                            resource.Data.label == minimapSelection.options[minimapSelection.value].text))
                        {
                            colorMap[i] = resourceColor;
                            count++;
                        }
                    }

                    i++;
                }
            }

            //Debug.Log(minimapSelection.options[minimapSelection.value].text + ": " + count);
            var texture = TextureGenerator.TextureFromColorMap(colorMap, manager.WorldSizeX, manager.WorldSizeZ);
            minimap.texture = texture;
            var smallest = (float) Mathf.Min(texture.width, texture.height);
            minimapRT.sizeDelta = new Vector2(minimapRT.sizeDelta.x, minimap.rectTransform.rect.height * texture.height / smallest);
            minimap.rectTransform.localScale = new Vector3(texture.width / smallest, texture.height / smallest);
        }

        private static readonly Color[] RainfallColors = {
            new Color(0, 0, 0.5f), 
            new Color(0, 0, 1),
            new Color(0, 0.5f, 1), 
            new Color(0.25f, 0.75f, 1f),
            new Color(1, 1, 1), 
            new Color(1, 1, 0f),
            new Color(1, 0.5f, 0),
            new Color(1, 0, 0)
        };

        private static readonly float[] RainfallColorThresholds =
        {
            8000,
            5000,
            2800,
            1400,
            900,
            500,
            200,
            50
        };
    
        private static Color GetRainfallColor(float value)
        {
            if (value > RainfallColorThresholds[0]) return RainfallColors[0];
        
            for (var i = 1; i < RainfallColors.Length; i++)
                if (value > RainfallColorThresholds[i])
                    return Color.Lerp(RainfallColors[i], RainfallColors[i - 1],
                        (value - RainfallColorThresholds[i]) / (RainfallColorThresholds[i - 1] - RainfallColorThresholds[i]));

            return RainfallColors[RainfallColors.Length - 1];
        }

        private static readonly Color[] TemperatureColors = {
            new Color(1, 0, 0),
            new Color(1, 1, 0), 
            new Color(0, 1, 0),
            new Color(0, 1, 1), 
            new Color(0, 0, 1),
            new Color(0.67f, 0, 1),
            new Color(1, 1, 1)
        };

        private static readonly float[] TemperatureColorThresholds =
        {
            50,
            35,
            20,
            10,
            0,
            -20,
            -40
        };

        private static Color GetTemperatureColor(float value)
        {
            if (value > TemperatureColorThresholds[0]) return TemperatureColors[0];
        
            for (var i = 1; i < TemperatureColors.Length; i++)
                if (value > TemperatureColorThresholds[i])
                    return Color.Lerp(TemperatureColors[i], TemperatureColors[i - 1],
                        (value - TemperatureColorThresholds[i]) / (TemperatureColorThresholds[i - 1] - TemperatureColorThresholds[i]));

            return TemperatureColors[TemperatureColors.Length - 1];
        }

        /*public static void StartEditing()
        {
            currentlyEditing = true;
        }

        public static void StopEditing()
        {
            currentlyEditing = false;
        }

        public void CloseEditPanel()
        {
            StopEditing();
            hexCell.SetHighlight(false);
            cellEditPanel.SetActive(false);
        }*/

        /*public void SetSpeed(float speed)
        {
            speedInput.text = speed.ToString(CultureInfo.CurrentCulture);
            //speedSlider.value = speed;
            if (!isPaused)
                Time.timeScale = speed;
        }
    
        public void SetSpeed(string speedString)
        {
            if (!float.TryParse(speedString, out var speed)) return; 
            speedSlider.value = speed;
            speedInput.text = speed.ToString(CultureInfo.CurrentCulture);//speedSlider.value.ToString(CultureInfo.CurrentCulture);
            if (!isPaused)
                Time.timeScale = speed; //speedSlider.value;
        }

        public void SetPaused(bool state)
        {
            playButton.gameObject.SetActive(state);
            pauseButton.gameObject.SetActive(!state);
            isPaused = state;
            if (float.TryParse(speedInput.text, out var speed)) 
                Time.timeScale = isPaused ? 0f : speed;
            else 
                Time.timeScale = isPaused ? 0f : speedSlider.value;
        }*/

        /*public void SetBiome()
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
        }*/

        /*private const string DateFormat = "Season {0}, {1} AD";
        private void UpdateDate()
        {
            var ts = TimeSpan.FromSeconds(WorldManager.timeElapsedSinceBeginning);
            timeText.text = ts.ToString("h\\:mm\\:ss\\.fff");
            var newSeason = Mathf.FloorToInt(WorldManager.timeElapsedSinceBeginning / WorldManager.SeasonDuration);
            if (newSeason != currentSeason)
            {
                currentSeason = newSeason;
                dateText.text = string.Format(DateFormat, (currentSeason % 4) + 1, currentSeason / 4);
            }
        }*/

        /*private Traveller traveller;

        private void HandleInput()
        {
            var inputRay = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(inputRay, out var hit)) return;
        
            // clear path highlights
            var hexCell = hexGrid.GetCell(hit.point);
            /*if (this.hexCell && this.hexCell == hexCell && cellEditPanel.activeSelf)
            return;#1#
        
            var moveCost = "";
            var dire = HexDirection.NE;
            foreach (var mc in hexCell.dataCell.moveCostTo)
            {
                moveCost += dire + ": " + mc + " ";
                dire = dire.Next();
            }
            //Debug.Log(moveCost);

            traveller = FindObjectOfType<Traveller>();
            var travelers = FindObjectsOfType<Traveller>();
            foreach (var traveler in travelers)
            {
                var cell = traveler.occupiedCell;
                cell.HexCell.SetHighlight(false);
                foreach (var dir in traveler.path.GetRange(traveler.pathIndex, traveler.path.Count - traveler.pathIndex))
                {
                    cell = cell.GetNeighbor(dir);
                    cell.HexCell.SetHighlight(false);
                }
            
                cell = traveler.occupiedCell;
                for (var pathIndex = traveler.pathIndex - 1; pathIndex >= 0; pathIndex--)
                {
                    var dir = traveler.path[pathIndex];
                    cell = cell.GetNeighbor(dir.Opposite());
                    cell.HexCell.SetHighlight(false);
                }
            
                Stopwatch sw = new Stopwatch();
                sw.Start();
                traveler.CurrentDestination = hexCell.dataCell;
                sw.Stop();
                Debug.Log(sw.Elapsed);
                var currentNode = hexCell.dataCell;
                while (currentNode != traveler.occupiedCell)
                {
                    currentNode.HexCell.SetHighlight(true, Color.cyan);
                    currentNode = currentNode.PathFrom;
                }
            }
            //traveller.LineThroughHexes(traveller.occupiedCell.HexCell, hexCell);
            //Debug.Log(traveller.occupiedCell.HexCell.coordinates + " " + hexCell.coordinates + " " + hexCell.dataCell.moveCostTo);

            // add travel to selected cell for debug purposes
            if (cellEditPanel.activeSelf)
                CloseEditPanel();
            
            LoadCellData(hexCell);
            cellEditPanel.SetActive(true);
            this.hexCell = hexCell;
            hexCell.SetHighlight(true);
            StopEditing();
        }*/

        /*private void LoadCellData(HexCell hexCell)
        {
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

            foreach (var panel in resourcePanelList)
                Destroy(panel);

            contentRectTransform.sizeDelta = new Vector2(contentRectTransform.sizeDelta.x, 50f * cell.resources.Count);

            resourcePanelList.Clear();
            resources.Clear();
            foreach (var resource in cell.resources)
            {
                var j = 0;
                foreach (var resourceData in ResourceHolder.resources)
                {
                    if (resourceData.label == resource.data.label)
                        resourceDropdown.value = j;
                    j++;
                }
                resourceDropdown.RefreshShownValue();

                AddResource(new Resource(resource.data, resource.AmountInt, resource.Balance, resource.harvestCost));
            }
        }

        private void AddResource(Resource resource)
        {
            /*if (resources.Any(res => res.data.label == resource.data.label))
            return;#1#

            resourceAmountInput.text = resource.amount.ToString();
            resourceBalanceInput.text = resource.Balance.ToString();
            resourceCostInput.text = resource.harvestCost.ToString(CultureInfo.CurrentCulture);

            contentRectTransform.sizeDelta = new Vector2(contentRectTransform.sizeDelta.x, 50f * (resources.Count + 1));

            var resourceObject = Instantiate(resourceTemplate, resourceContentPanel.transform, true);
            resourcePanelList.Add(resourceObject);
            resourceObject.GetComponentInChildren<Button>().onClick.AddListener(() => RemoveResource(resource.data.label));

            var rectTransform = resourceObject.GetComponent<RectTransform>();
            rectTransform.Translate(Vector3.down * (rectTransform.rect.height * resources.Count));
            resourceObject.SetActive(true);

            resources.Add(resource);
        }

        public void AddResource()
        {
            var resource = new Resource(ResourceHolder.resources[0], 0);
            resourceDropdown.value = 0;
            AddResource(resource);
        }

        public void RemoveResource(string label)
        {
            var resourceFound = false;
            for (var i = 0; i < resources.Count; i++)
            {
                if (resources[i].data.label == label)
                {
                    Destroy(resourcePanelList[i]);
                    resourcePanelList.RemoveAt(i);
                    resources.RemoveAt(i);
                    contentRectTransform.sizeDelta = new Vector2(contentRectTransform.sizeDelta.x, 50f * resources.Count);
                    resourceFound = true;
                }

                if (!resourceFound || i == resources.Count) continue;

                var rectTransform = resourcePanelList[i].GetComponent<RectTransform>();
                rectTransform.Translate(Vector3.up * rectTransform.rect.height);
            }
        }*/

        /*public HexDirection roadDirection;
        public bool road;
        public HexDirection riverDirection;
        public bool river;
        public bool walled;*/
        /*public void SaveCell()
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
                cell.HexCell.RemoveRiver(riverDirection);#1#
        
            cell.resources.Clear();
            for (var i = 0; i < resources.Count; i++)
            {
                var resourceInput = resourcePanelList[i].GetComponentInChildren<Dropdown>();
                var label = resourceInput.options[resourceInput.value].text;
                var data = ResourceHolder.resources.Find(resourceData => label == resourceData.label);
            
                var inputs = resourcePanelList[i].GetComponentsInChildren<InputField>();
                var amount = Mathf.Clamp(int.Parse(inputs[0].text), 0, data.maxAmount);
                var balance = Mathf.Clamp(int.Parse(inputs[1].text), 0, data.maxAmount);
                var harvestCost = float.Parse(inputs[2].text);
            
                cell.resources.Add(new Resource(data, amount, balance, harvestCost));
            }
        
            UpdateMinimap();
        }*/
    }
}