#if FISHNET 
using UnityEditor;
namespace Framework
{
    public class FishnetScript
    {
        [UnityEditor.MenuItem("Assets/Create/Scripting/NetworkBehaviour")]
        public static void CreateCustomScript()
        {
            // Here you can use a script template or instantiate an asset.
            string scriptTemplate =
            @"#if FISHNET
using FishNet;
using FishNet.Object;
public class NewNetworkBehaviourScript : NetworkBehaviour
{
    public override void OnStartServer()
    {
        base.OnStartServer();
    }
    public override void OnStopServer()
    {
        base.OnStopServer();
    }
    public override void OnStartClient()
    {
        base.OnStartClient();
    }
    public override void OnStopClient()
    {
        base.OnStopClient();
    }
}
#endif";

            string path = GetCurrentSelectedPath();

            if (string.IsNullOrEmpty(path))
            {
                path = "Assets"; // Default path if no folder is selected
            }
            string filePath = AssetDatabase.GenerateUniqueAssetPath(path + "/NewNetworkBehaviourScript.cs");
            System.IO.File.WriteAllText(filePath, scriptTemplate);
            AssetDatabase.Refresh(); // Refresh the asset database to show the new script in Unity
        }

        private static string GetCurrentSelectedPath()
        {
            // Check if a folder is selected in the project window
            if (Selection.activeObject != null)
            {
                string selectedPath = AssetDatabase.GetAssetPath(Selection.activeObject);

                // Check if the selected path is a folder
                if (System.IO.Directory.Exists(selectedPath))
                {
                    return selectedPath;  // The user selected a folder
                }
                else
                {
                    // If an asset is selected, get the directory containing the asset
                    return System.IO.Path.GetDirectoryName(selectedPath);
                }
            }

            // If nothing is selected, return null (we'll default to "Assets" folder later)
            return null;
        }
    }
}
#endif