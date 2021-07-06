using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class TimeControl : MonoBehaviour
    {
        public Button playButton;
        public Button pauseButton;
        public Slider speedSlider;
        public InputField speedInput;
        public Text dateText;
        public Text timeText;
        public Text fpsText;
    
        private bool isPaused = false;
        
        private const string DateFormat = "Season {0}, {1} AD";

        private bool fpsStdOn;
        private float fps;
        private float fpsWorst = 144;
        private int timesWorse;
        private float fpsVar;
        private float fpsStd;
        private int counter;
        private float time;
        
        private void Update()
        {
            UpdateDate();

            var currentFps = 1 / Time.deltaTime;
            if (currentFps < fpsWorst)
            {
                if (timesWorse >= 30)
                {
                    fpsWorst = currentFps;
                    timesWorse = 0;
                }
                else timesWorse++;
            }
            if (WorldManager.spawnedAgents >= 1)
            {
                counter++;
                time += Time.deltaTime;
                fps = counter / time;
                if (fpsStdOn) fpsVar += Mathf.Pow(fps - currentFps, 2);
                fpsText.text = $"FPS: {currentFps:F2} {fps:F2} {fpsWorst:F2} {fpsStd:F2}";
            }

            if (counter % 100 == 99)
            {
                fpsStdOn = true;
                fpsStd = Mathf.Sqrt(fpsVar / (counter - 98));
            } 
            else if (counter < 99)
            {
                fpsText.text = $"FPS: {currentFps:F2} {fpsWorst:F2}";
            }
        
            if (!EditorHandler.currentlyEditing && Input.GetButtonDown("Jump"))
                SetPaused(!isPaused);
        }
        
        private void UpdateDate()
        {
            var ts = TimeSpan.FromSeconds(WorldManager.timeElapsedSinceBeginning);
            timeText.text = ts.ToString("h\\:mm\\:ss\\.fff");
            //var newSeason = Mathf.FloorToInt(WorldManager.timeElapsedSinceBeginning / WorldManager.SeasonDuration);
            if (WorldManager.newSeason)
            {
                dateText.text = string.Format(DateFormat, WorldManager.season % 4 + 1, WorldManager.season / 4);
            }
        }
        
        public void SetSpeed(float speed)
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
        }
    }
}
