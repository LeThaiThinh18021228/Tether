using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEngine;

[System.Serializable]
[CreateAssetMenu(fileName = "MasterBuilder", menuName = "ScriptableObjects/Master Builder", order = 1)]
public class BuildConfig : ScriptableObject
{
    public enum ProgramType
    {
        Client,
        GameServer,
        MasterServer
    }
    public bool isEnable;
    public ProgramType type;
    public string[] scenes;
    public string buildFolder;
    public string buildFile;
    public string roomPath;
    public BuildTarget buildTarget;
    public BuildOptions buildOptions;
    public ScriptingImplementation scriptingBackend;
    public BuildTargetGroup buildTargetGroup;
    public bool isHeadless;
    public string[] extraScriptingDefines;
    public List<ConfigProperty> configFileProperties = new List<ConfigProperty>();
    public BuildProfile profile;

    [System.Serializable]
    public class ConfigProperty
    {
        public string key;
        public string value;

        public ConfigProperty(string key, string value)
        {
            this.key = key;
            this.value = value;
        }
    }
}
