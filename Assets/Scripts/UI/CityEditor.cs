using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ScriptableObjects;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class CityEditor : EditorPanel
    {
        public Text coordinatesText;
        public Text creationTimeText;
        public InputField nameField;
        public InputField popField;
        public Text healthField;
        public Text happinessField;

        public Slider[] needSliders;
        public Text[] needTexts;
        public Text[] needInfo;

        public Text planText;

        public GameObject wareTemplate;
        public GameObject wareContentPanel;
        
        public Dropdown wareDropdown;
        public InputField wareAmountInput;
        public InputField warePriceInput;
        public InputField wareStateInput;

        private RectTransform contentRectTransform;
        private readonly List<GameObject> warePanelList = new List<GameObject>();

        private City agent;

        public HexCell hexCell;

        private int hiddenIndex;
        
        private void Start()
        {
            Close();

            contentRectTransform = wareContentPanel.GetComponent<RectTransform>();
            
            wareDropdown.ClearOptions();
            foreach (var ware in ResourceHolder.wares)
                wareDropdown.options.Add(new Dropdown.OptionData(ware.label));
                
            foreach (var infrastructure in ResourceHolder.infrastructure)
                wareDropdown.options.Add(new Dropdown.OptionData(infrastructure.label));
        }

        public override void Load()
        {
            if (!hexCell.dataCell.OccupyingCity)
            {
                agent = null;
                Close();
                return;
            }
            SetActive();

            agent = hexCell.dataCell.OccupyingCity;

            coordinatesText.text = hexCell.dataCell.position.ToString();
            creationTimeText.text = "created @ " + TimeSpan.FromSeconds(agent.creationTime).ToString("h\\:mm\\:ss\\.fff");
            nameField.text = agent.moniker;
            popField.text = agent.state.PopulationInt.ToString(); 
            healthField.text = agent.state.CalculateHealth().ToString(CultureInfo.CurrentCulture);
            happinessField.text = agent.state.CalculateHappiness().ToString(CultureInfo.CurrentCulture);
            planText.text = string.Join("", agent.actionSequence);

            foreach (NeedType need in Enum.GetValues(typeof(NeedType)))
            {
                needSliders[(int) need].value = agent.state.needs[(int) need].Value;// / (agent.needs[(int) need].max - agent.needs[(int) need].min);
                needTexts[(int) need].text = agent.state.needs[(int) need].Value.ToString(CultureInfo.CurrentCulture);
                needInfo[(int) need].text =
                    $"[x{agent.state.needs[(int) need].happinessWeight:F2}, -{agent.state.needs[(int) need].depletionRate * WorldManager.SeasonDuration / Need.DepletionBase:F2}/S]";
            }
            
            foreach (var panel in warePanelList) 
                Destroy(panel);
            warePanelList.Clear();

            hiddenIndex = 0;

            var wareCount = agent.state.manufacturables.Sum(pair => pair.Value.Count);
            contentRectTransform.sizeDelta = new Vector2(contentRectTransform.sizeDelta.x, 50f * wareCount);

            foreach (var manufacturable in agent.state.manufacturables.SelectMany(pair => pair.Value))
            {
                var found = false;
                
                for (var j = 0; j < ResourceHolder.wares.Count; j++)
                {
                    if (ResourceHolder.wares[j].label != manufacturable.Data.label) continue; 
                    wareDropdown.value = j;
                    found = true;
                    break;
                }

                if (!found)
                {
                    for (var j = 0; j < ResourceHolder.infrastructure.Count; j++)
                    {
                        if (ResourceHolder.infrastructure[j].label != manufacturable.Data.label) continue; 
                        wareDropdown.value = ResourceHolder.wares.Count + j;
                        break;
                    }   
                }
                
                wareDropdown.RefreshShownValue();

                AddManufacturable(new Manufacturable(manufacturable), hiddenIndex++, agent.state.priceFinder.prices[manufacturable.Data]);
            }
        }

        public override void Save()
        {
            agent.moniker = nameField.text;
            //agent.Health = int.Parse(healthField.text);
            var population = int.Parse(popField.text);
            if (population != agent.state.PopulationInt)
            {
                agent.state.Population = population;
                agent.AdjustUrbanLevel();
            }

            for (var i = 0; i < needSliders.Length; i++)
            {
                agent.state.needs[i].Set(needSliders[i].value);// * (agent.needs[i].max - agent.needs[i].min));
                agent.state.AdjustPassiveNeedSatisfaction(i, -agent.state.passiveNeedSatisfaction[i]);
            }

            foreach (var vehicleSet in agent.state.vehicleSets) vehicleSet.Clear();
            agent.state.manufacturables.Clear();
            agent.state.manufacturablesCount.Clear();
            agent.state.lastAddedTo = null;
            agent.state.weight = 0;
            agent.state.carryingCapacity = agent.state.Population * Agent.BaseCarryingCapacity;
            agent.state.infrastructureSpaceLeft = agent.occupiedCells.Count * City.InfrastructureSpacePerCell;
            agent.state.manufacturableIndex = 0;
            foreach (var panel in warePanelList)
            {
                var wareInput = panel.GetComponentInChildren<Dropdown>();
                var label = wareInput.options[wareInput.value].text;
                ManufacturableData data = ResourceHolder.wares.Find(wareData => label == wareData.label);
                if (data == null) 
                    data = ResourceHolder.infrastructure.Find(infrastructureData => label == infrastructureData.label);

                var inputs = panel.GetComponentsInChildren<InputField>();
                var amount = Mathf.Clamp(int.Parse(inputs[0].text), 0, data.maxAmount);
                var state = Mathf.Clamp(float.Parse(inputs[2].text), 0, 100);

                if (amount <= 0 || state <= 0) continue;

                if (!agent.state.manufacturables.ContainsKey(data))
                {
                    agent.state.manufacturables.Add(data, new List<Manufacturable>());
                    agent.state.manufacturablesCount.Add(data, 0);
                }
                agent.state.manufacturables[data].Add(new Manufacturable(data, amount, state, agent.state.manufacturableIndex++));
                agent.state.manufacturablesCount[data] += amount;
                agent.state.weight += data.mass * amount;
                agent.state.carryingCapacity += Math.Min(agent.state.Population, amount * data.maxUsers) * data.carryingCapacity;
                if (data is InfrastructureData) agent.state.infrastructureSpaceLeft -= amount * data.mass;
                // might account for infrastructure and wares that exceed the limit
                foreach (var satisfaction in data.needSatisfactionOnHaving)
                {
                    agent.state.AdjustPassiveNeedSatisfaction((int) satisfaction.need, satisfaction.value * amount);
                }
            }
            
            /*for (var i = 0; i < agent.state.speedMultipliers.Length; i++)
                agent.state.FindBestVehicle(i);*/
            
            agent.waresChanged = true;
        }

        public void Destroy()
        {
            agent.MakeFinalPreparations();
            Close();
        }

        public void OnNeedSliderChange()
        {
            for (var i = 0; i < needSliders.Length; i++)
                needTexts[i].text = needSliders[i].value.ToString(CultureInfo.CurrentCulture);
        }

        public void AddManufacturable()
        {
            var ware = new Ware(ResourceHolder.wares[0], 0, 0);
            wareDropdown.value = 0;
            AddManufacturable(ware, hiddenIndex++, 0);
        }

        private void AddManufacturable(Manufacturable ware, int index, float price)
        {
            wareAmountInput.text = ware.AmountInt.ToString();
            warePriceInput.text = price.ToString(CultureInfo.CurrentCulture);
            wareStateInput.text = ware.State.ToString(CultureInfo.CurrentCulture);

            contentRectTransform.sizeDelta = new Vector2(contentRectTransform.sizeDelta.x, 50f * (warePanelList.Count + 1));

            var wareObject = Instantiate(wareTemplate, wareContentPanel.transform, true);
            wareObject.GetComponent<Text>().text = index.ToString();
            wareObject.GetComponentInChildren<Button>().onClick
                .AddListener(() => RemoveWare(index.ToString())); //resource.data.label));

            var rectTransform = wareObject.GetComponent<RectTransform>();
            rectTransform.Translate(Vector3.down * (rectTransform.rect.height * warePanelList.Count));
            
            wareObject.SetActive(true);
            warePanelList.Add(wareObject);
        }

        private void RemoveWare(string index)
        {
            var wareFound = false;
            for (var i = 0; i < warePanelList.Count; i++)
            {
                //var hiddenIndex = resourcePanelList[i].GetComponentInChildren<Text>();
                if (warePanelList[i].GetComponent<Text>().text == index)//resources[i].data.label == label)
                {
                    Destroy(warePanelList[i]);
                    warePanelList.RemoveAt(i);
                    contentRectTransform.sizeDelta = new Vector2(contentRectTransform.sizeDelta.x, 50f * warePanelList.Count);
                    wareFound = true;
                }

                if (!wareFound || i == warePanelList.Count) continue;

                var rectTransform = warePanelList[i].GetComponent<RectTransform>();
                rectTransform.Translate(Vector3.up * rectTransform.rect.height);
            }
        }
    }
}
