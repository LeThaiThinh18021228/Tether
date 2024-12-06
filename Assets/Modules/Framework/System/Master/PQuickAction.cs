using UnityEngine;

namespace Framework
{
    public class PQuickAction : MonoBehaviour
    {
        void Update()
        {
            if (Input.GetKeyDown(KeyCode.R))
            {
                Time.timeScale = 1 - Time.timeScale;
                PDebug.Log("Press R");
            }
        }
#if UNITY_EDITOR
        [UnityEditor.MenuItem("PFramework/Clear Data")]
        public static void ClearData()
        {
            PGameMaster.ClearData();
        }
#endif
    }
}

