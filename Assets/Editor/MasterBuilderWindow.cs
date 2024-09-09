using Framework;
using MasterServerToolkit.MasterServer;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public class CustomBuildWindow : EditorWindow
{
    private static List<string> pathBuildConfigs = new();

    private static List<BuildConfig> buildConfigs = new List<BuildConfig>();
    private Vector2 scrollPosition;
    [MenuItem("Tools/Custom Build Window")]
    public static void ShowWindow()
    {
        if (buildConfigs.IsNullOrEmpty())
            LoadBuildConfigs();
        GetWindow<CustomBuildWindow>("Custom Build Window");
    }
    private void OnGUI()
    {
        if (buildConfigs.IsNullOrEmpty())
            LoadBuildConfigs();
        EditorGUILayout.LabelField("Custom Build Configurations", EditorStyles.boldLabel);

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        for (int i = 0; i < buildConfigs.Count; i++)
        {
            int _i = i;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.LabelField($"Build Configuration {_i + 1}", EditorStyles.boldLabel);
            buildConfigs[_i] = EditorGUILayout.ObjectField("New BuildConfig", buildConfigs[_i], typeof(BuildConfig), false) as BuildConfig;
            //buildConfigs[i].Draw();

            if (GUILayout.Button("Remove Configuration"))
            {
                buildConfigs.RemoveAt(_i);
                i--;
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }

        EditorGUILayout.EndScrollView();

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Add Build Configuration"))
        {
            buildConfigs.Add(null);
        }

        if (GUILayout.Button("Build All"))
        {
            BuildAll();
        }
        EditorGUILayout.EndHorizontal();
    }

    private void BuildAll()
    {
        var configs = buildConfigs;
        for (int i = 0; i < configs.Count; i++)
        {
            if (configs[i].isEnable)
            {
                PerformBuild(configs[i]);
                Thread.Sleep(1000);
            }
        }
    }

    private void PerformBuild(BuildConfig config)
    {
        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
        {
            scenes = config.scenes,
            locationPathName = Path.Combine(config.buildFolder, config.buildFile),
            target = config.buildTarget,
            extraScriptingDefines = config.extraScriptingDefines,
            options = config.buildOptions,
            subtarget = (int)(config.isHeadless ? StandaloneBuildSubtarget.Server : StandaloneBuildSubtarget.Player),
        };
        PlayerSettings.SetScriptingBackend(config.buildTargetGroup, config.scriptingBackend);

        BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            MstProperties properties = new MstProperties();
            foreach (var prop in config.configFileProperties)
            {
                if (prop.value == "true")
                {
                    properties.Add(prop.key, true);
                }
                else if (prop.value == "false")
                {
                    properties.Add(prop.key, false);
                }
                else if (int.TryParse(prop.value, out int value))
                {
                    properties.Add(prop.key, value);
                }
                else
                {
                    properties.Add(prop.key, prop.value);
                }
            }
            switch (config.type)
            {
                case BuildConfig.ProgramType.Client:
                    properties.Add(Mst.Args.Names.StartClientConnection, true);
                    properties.Add(Mst.Args.Names.MasterIp, Mst.Args.MasterIp);
                    properties.Add(Mst.Args.Names.MasterPort, Mst.Args.MasterPort);
                    break;
                case BuildConfig.ProgramType.GameServer:
                    properties.Add(Mst.Args.Names.StartClientConnection, true);
                    properties.Add(Mst.Args.Names.MasterIp, Mst.Args.MasterIp);
                    properties.Add(Mst.Args.Names.MasterPort, Mst.Args.MasterPort);
                    properties.Add(Mst.Args.Names.RoomIp, Mst.Args.RoomIp);
                    properties.Add(Mst.Args.Names.RoomPort, Mst.Args.RoomPort);
                    break;
                case BuildConfig.ProgramType.MasterServer:
                    properties.Add(Mst.Args.Names.StartMaster, true);
                    properties.Add(Mst.Args.Names.StartSpawner, true);
                    properties.Add(Mst.Args.Names.StartClientConnection, true);
                    properties.Add(Mst.Args.Names.MasterIp, Mst.Args.MasterIp);
                    properties.Add(Mst.Args.Names.MasterPort, Mst.Args.MasterPort);
                    properties.Add(Mst.Args.Names.RoomExecutablePath, Path.Combine(Directory.GetCurrentDirectory(), config.roomPath));
                    properties.Add(Mst.Args.Names.RoomIp, Mst.Args.RoomIp);
                    properties.Add(Mst.Args.Names.RoomRegion, Mst.Args.RoomRegion);
                    break;
                default:
                    break;
            }


            File.WriteAllText(Mst.Args.AppConfigFile(config.buildFolder), properties.ToReadableString("\n", "="));

            PDebug.Log(config.buildFile + " build succeeded: " + (summary.totalSize / 1024) + " kb");
        }

        if (summary.result == BuildResult.Failed)
        {
            PDebug.Log(config.buildFile + " build failed");
        }
    }

    private static void LoadBuildConfigs()
    {
        var paths = EditorPrefs.GetString("buildConfigs");

        if (pathBuildConfigs.IsNullOrEmpty())
        {
            buildConfigs = ScriptableObjectHelper.FindAssetsByType<BuildConfig>();
        }
        else
        {
            pathBuildConfigs = paths.Split(",").ToList();
            for (int i = 0; i < pathBuildConfigs.Count; i++)
            {
                buildConfigs.Add(ScriptableObjectHelper.LoadOrCreateNewAsset<BuildConfig>(pathBuildConfigs[i]));
            }
        }
    }

    private static void SaveBuildConfigs()
    {
        string paths = string.Join(",", pathBuildConfigs);
        EditorPrefs.SetString("buildConfigs", paths);
    }
}
