using UnityEngine;
using UnityEngine.Serialization;

namespace UI
{
    public class EditorPanel : MonoBehaviour
    {
        public Canvas canvas;
        
        private void Start()
        {
            Close();
        }

        private void Update()
        {
            if (Input.GetButtonUp("Cancel"))
                Close();
        
            if (Input.GetButtonUp("Submit") && IsActive)
                Save();
        }

        public bool IsActive => canvas.enabled;

        protected void SetActive()
        {
            canvas.enabled = true;
        }

        public virtual void Close()
        {
            EditorHandler.StopEditing();
            canvas.enabled = false;
        }

        public virtual void Load()
        {
        }

        public virtual void Save()
        {
        }
    }
}
