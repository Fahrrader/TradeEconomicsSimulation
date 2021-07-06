using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.EventSystems;
using Debug = UnityEngine.Debug;

namespace UI
{
    public class EditorHandler : MonoBehaviour
    {
        public static bool currentlyEditing;

        public HexGrid hexGrid;

        public CellEditor cellEditor;
        public CityEditor cityEditor;
        public TravellerEditor travellerEditor;
        
        public EditorPanel currentEditor;

        private HexCell hexCell;
        
        private readonly List<HexDirection> pathDirections = new List<HexDirection>();

        private bool citySelected;
        
        void Update()
        {
            if (
                Input.GetMouseButtonUp(0) &&
                !EventSystem.current.IsPointerOverGameObject()
            )
                HandleInput();
        
            if (Input.GetButtonUp("Cancel"))
                currentEditor.Close();
        
            if (Input.GetButtonUp("Submit") && currentEditor.IsActive)
                currentEditor.Save();
        }
        
        public static void StartEditing()
        {
            currentlyEditing = true;
        }

        public static void StopEditing()
        {
            currentlyEditing = false;
        }

        private void HandleInput()
        {
            var inputRay = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(inputRay, out var hit)) return;
            var newHexCell = hexGrid.GetCell(hit.point);

            if (hexCell)
            {
                var dir = hexCell.dataCell.GetNeighborDirection(newHexCell.dataCell);
                if (dir.IsValid()) Debug.Log(hexCell.dataCell.moveCostTo[(int) dir] + " " + dir);   
            }

            if (currentEditor.IsActive) currentEditor.Close();
            if (hexCell) ClearPath();

            var traveller = hit.collider.gameObject.GetComponent<Traveller>();

            if (traveller)
            {
                currentEditor = travellerEditor;
                travellerEditor.agent = traveller;
                // if (hexCell) LayPathForTraveller(traveller);
                citySelected = false;
            } 
            else if (newHexCell.Walled && !citySelected)
            {
                currentEditor = cityEditor;
                hexCell = newHexCell;
                cityEditor.hexCell = hexCell;

                citySelected = true;
                hexCell.SetHighlight(true);
            }
            else
            {
                currentEditor = cellEditor;
                hexCell = newHexCell;
                cellEditor.hexCell = hexCell;
                
                citySelected = false;
                hexCell.SetHighlight(true);
            }
            
            currentEditor.Load();
            
            StopEditing();
        }

        private void ClearPath()
        {
            var cell = hexCell;
            
            cell.SetHighlight(false);
            foreach (var dir in pathDirections)
            {
                cell = cell.GetNeighbor(dir);
                cell.SetHighlight(false);
            }
            pathDirections.Clear();
        }

        private void LayPathForTraveller(Traveller traveller)
        {
            /*var moveCost = "";
            var dire = HexDirection.NE;
            foreach (var mc in hexCell.dataCell.moveCostTo)
            {
                moveCost += dire + ": " + mc + " ";
                dire = dire.Next();
            }
            Debug.Log(moveCost);*/
            
            // clear path highlights
            /*var cell = traveller.occupiedCell;
            
            cell.HexCell.SetHighlight(false);
            foreach (var dir in traveller.path.GetRange(traveller.pathIndex, traveller.path.Count - traveller.pathIndex))
            {
                cell = cell.GetNeighbor(dir);
                cell.HexCell.SetHighlight(false);
            }
        
            cell = traveller.occupiedCell;
            for (var pathIndex = traveller.pathIndex - 1; pathIndex >= 0; pathIndex--)
            {
                var dir = traveller.path[pathIndex];
                cell = cell.GetNeighbor(dir.Opposite());
                cell.HexCell.SetHighlight(false);
            }*/
        
            var sw = new Stopwatch();
            sw.Start();
            traveller.CurrentDestination = hexCell.dataCell;
            sw.Stop();
            Debug.Log(sw.Elapsed);
            var currentNode = hexCell.dataCell;
            while (currentNode != traveller.occupiedCell)
            {
                currentNode.HexCell.SetHighlight(true, Color.cyan);
                pathDirections.Add(currentNode.PathFrom);
                currentNode = currentNode.GetNeighbor(currentNode.PathFrom);
            }
            //traveller.LineThroughHexes(traveller.occupiedCell.HexCell, hexCell);
            //Debug.Log(traveller.occupiedCell.HexCell.coordinates + " " + hexCell.coordinates + " " + hexCell.dataCell.moveCostTo);
        }
    }
}
