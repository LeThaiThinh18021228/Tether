#if FISHNET
using Framework;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MasterServerToolkit.MasterServer.Examples.FishNetworking
{
    public class MasterBuilder
    {
        #region Nix
        [MenuItem(MstConstants.ToolMenu + "Linux/Room(Normal)")]
        private static void BuildRoomForLinuxsNormal()
        {
            BuildRoomForLinuxs(false);
        }
        [MenuItem(MstConstants.ToolMenu + "Linux/Room(Headless)")]
        private static void BuildRoomForLinuxsHeadless()
        {
            BuildRoomForLinuxs(true);
        }
        private static void BuildRoomForLinuxs(bool isHeadless)
        {
            string buildFolder = Path.Combine("Builds", "App", "Nix", "Room");

            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = new[] {
                    "Assets/Scenes/CoreIngame.unity"
                },
                locationPathName = Path.Combine(buildFolder, "Room.x84_64"),
                target = BuildTarget.StandaloneLinux64,
#if UNITY_2021_1_OR_NEWER
                options = BuildOptions.ShowBuiltPlayer | BuildOptions.Development,
                subtarget = isHeadless ? (int)StandaloneBuildSubtarget.Server : (int)StandaloneBuildSubtarget.Player
#else
                options = isHeadless ? BuildOptions.ShowBuiltPlayer | BuildOptions.EnableHeadlessMode : BuildOptions.ShowBuiltPlayer
#endif
            };
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.Mono2x);

            BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                string appConfig = Mst.Args.AppConfigFile(buildFolder);

                MstProperties properties = new MstProperties();
                properties.Add(Mst.Args.Names.StartClientConnection, true);
                properties.Add(Mst.Args.Names.MasterIp, Mst.Args.MasterIp);
                properties.Add(Mst.Args.Names.MasterPort, Mst.Args.MasterPort);
                properties.Add(Mst.Args.Names.RoomIp, Mst.Args.RoomIp);
                properties.Add(Mst.Args.Names.RoomPort, Mst.Args.RoomPort);

                File.WriteAllText(appConfig, properties.ToReadableString("\n", "="));

                PDebug.Log("Room build succeeded: " + (summary.totalSize / 1024) + " kb");
            }

            if (summary.result == BuildResult.Failed)
            {
                PDebug.Log("Room build failed");
            }
        }
        [MenuItem(MstConstants.ToolMenu + "Linux/Master Server")]
        private static void BuildMasterAndSpawnerForLinuxs()
        {
            string buildFolder = Path.Combine("Builds", "App", "Nix", "MasterAndSpawner");
            string roomExePath = Path.Combine(Directory.GetCurrentDirectory(), "Builds", "App", "Nix", "Room", "Room.exe");
            roomExePath = roomExePath.Replace("\\", "/").Replace("C:/", "/");

            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/MasterServer.unity" },
                locationPathName = Path.Combine(buildFolder, "MasterServer.x84_64"),
                target = BuildTarget.StandaloneLinux64,
#if UNITY_2021_1_OR_NEWER
                options = BuildOptions.ShowBuiltPlayer | BuildOptions.Development,
                subtarget = (int)StandaloneBuildSubtarget.Server
#else
                options = BuildOptions.EnableHeadlessMode | BuildOptions.ShowBuiltPlayer | BuildOptions.Development
#endif
            };

            BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                MstProperties properties = new MstProperties();
                properties.Add(Mst.Args.Names.StartMaster, true);
                properties.Add(Mst.Args.Names.StartSpawner, true);
                properties.Add(Mst.Args.Names.StartClientConnection, true);
                properties.Add(Mst.Args.Names.MasterIp, Mst.Args.MasterIp);
                properties.Add(Mst.Args.Names.MasterPort, Mst.Args.MasterPort);
                properties.Add(Mst.Args.Names.RoomExecutablePath, roomExePath);
                properties.Add(Mst.Args.Names.RoomIp, Mst.Args.RoomIp);
                properties.Add(Mst.Args.Names.RoomRegion, Mst.Args.RoomRegion);

                File.WriteAllText(Path.Combine(buildFolder, "application.cfg"), properties.ToReadableString("\n", "="));

                PDebug.Log("Master Server build succeeded: " + (summary.totalSize / 1024) + " kb");
            }

            if (summary.result == BuildResult.Failed)
            {
                PDebug.Log("Master Server build failed");
            }
        }

        [MenuItem(MstConstants.ToolMenu + "Linux/Spawner")]
        private static void BuildSpawnerForLinuxs()
        {
            string buildFolder = Path.Combine("Builds", "App", "Nix", "Spawner");
            string roomExePath = Path.Combine(Directory.GetCurrentDirectory(), "Builds", "App", "Nix", "Room", "Room.exe");
            roomExePath = roomExePath.Replace("\\", "/").Replace("C:/", "/");
            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = new[] {
                    "Assets/Scenes/Spawner.unity"
                },
                locationPathName = Path.Combine(buildFolder, "Spawner.x84_64"),
                target = BuildTarget.StandaloneLinux64,
#if UNITY_2021_1_OR_NEWER
                options = BuildOptions.ShowBuiltPlayer | BuildOptions.Development,
                subtarget = (int)StandaloneBuildSubtarget.Server
#else
                options = BuildOptions.EnableHeadlessMode | BuildOptions.ShowBuiltPlayer | BuildOptions.Development
#endif
            };

            BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                string appConfig = Mst.Args.AppConfigFile(buildFolder);

                MstProperties properties = new MstProperties();
                properties.Add(Mst.Args.Names.StartSpawner, true);
                properties.Add(Mst.Args.Names.StartClientConnection, true);
                properties.Add(Mst.Args.Names.MasterIp, Mst.Args.MasterIp);
                properties.Add(Mst.Args.Names.MasterPort, Mst.Args.MasterPort);
                properties.Add(Mst.Args.Names.RoomExecutablePath, roomExePath);
                properties.Add(Mst.Args.Names.RoomIp, Mst.Args.RoomIp);
                properties.Add(Mst.Args.Names.RoomRegion, Mst.Args.RoomRegion);

                File.WriteAllText(appConfig, properties.ToReadableString("\n", "="));

                PDebug.Log("Spawner build succeeded: " + (summary.totalSize / 1024) + " kb");
            }

            if (summary.result == BuildResult.Failed)
            {
                PDebug.Log("Spawner build failed");
            }
        }

        [MenuItem(MstConstants.ToolMenu + "Linux/Client")]
        private static void BuildClientForLinuxs()
        {
            string buildFolder = Path.Combine("Builds", "App", "Nix", "Client");

            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = new[] {
                   "Assets/Scenes/Loading.unity",
                    "Assets/Scenes/Auth.unity",
                    "Assets/Scenes/Home.unity",
                    "Assets/Scenes/GameSelection.unity",
                    "Assets/Scenes/CoreIngame.unity",                },
                locationPathName = Path.Combine(buildFolder, "Client.x84_64"),
                target = BuildTarget.StandaloneLinux64,
                options = BuildOptions.ShowBuiltPlayer | BuildOptions.Development
            };

            BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                string appConfig = Mst.Args.AppConfigFile(buildFolder);

                MstProperties properties = new MstProperties();
                properties.Add(Mst.Args.Names.StartClientConnection, true);
                properties.Add(Mst.Args.Names.MasterIp, Mst.Args.MasterIp);
                properties.Add(Mst.Args.Names.MasterPort, Mst.Args.MasterPort);

                File.WriteAllText(appConfig, properties.ToReadableString("\n", "="));

                PDebug.Log("Client build succeeded: " + (summary.totalSize / 1024) + " kb");
            }

            if (summary.result == BuildResult.Failed)
            {
                Debug.Log("Client build failed");
            }
        }
        #endregion
        #region Win
        [MenuItem(MstConstants.ToolMenu + "Win/Room(Headless)")]
        private static void BuildRoomForWindowsHeadless()
        {
            BuildRoomForWindows(true);
        }
        [MenuItem(MstConstants.ToolMenu + "Win/Room(Normal)")]
        private static void BuildRoomForWindowsNormal()
        {
            BuildRoomForWindows(false);
        }
        private static void BuildRoomForWindows(bool isHeadless)
        {
            string buildFolder = Path.Combine("Builds", "App", "Win", "Room");

            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = new[] {
                    "Assets/Scenes/CoreIngame.unity"
                },
                locationPathName = Path.Combine(buildFolder, "Room.exe"),
#if UNITY_2021_1_OR_NEWER
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.ShowBuiltPlayer | BuildOptions.Development | BuildOptions.AllowDebugging,
                extraScriptingDefines = new string[] { "UNITY_SERVER", "GAME_SERVER" }
#else
                options = isHeadless ? BuildOptions.ShowBuiltPlayer | BuildOptions.EnableHeadlessMode : BuildOptions.ShowBuiltPlayer
#endif
            };

            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.Mono2x);

            BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                string appConfig = Mst.Args.AppConfigFile(buildFolder);

                MstProperties properties = new MstProperties();
                properties.Add(Mst.Args.Names.StartClientConnection, true);
                properties.Add(Mst.Args.Names.MasterIp, Mst.Args.MasterIp);
                properties.Add(Mst.Args.Names.MasterPort, Mst.Args.MasterPort);
                properties.Add(Mst.Args.Names.RoomIp, Mst.Args.RoomIp);
                properties.Add(Mst.Args.Names.RoomPort, Mst.Args.RoomPort);

                File.WriteAllText(appConfig, properties.ToReadableString("\n", "="));

                PDebug.Log("Room build succeeded: " + (summary.totalSize / 1024) + " kb");
            }

            if (summary.result == BuildResult.Failed)
            {
                PDebug.Log("Room build failed");
            }
        }

        [MenuItem(MstConstants.ToolMenu + "Win/Master Server")]
        private static void BuildMasterAndSpawnerForWindows()
        {
            string buildFolder = Path.Combine("Builds", "App", "Win", "MasterServer");
            string roomExePath = Path.Combine(Directory.GetCurrentDirectory(), "Builds", "App", "Win", "Room", "Room.exe");

            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/MasterServer.unity" },
                locationPathName = Path.Combine(buildFolder, "MasterServer.exe"),
                target = BuildTarget.StandaloneWindows64,
#if UNITY_2021_1_OR_NEWER
                options = BuildOptions.ShowBuiltPlayer | BuildOptions.Development,
                subtarget = (int)StandaloneBuildSubtarget.Server
#else
                options = BuildOptions.EnableHeadlessMode | BuildOptions.ShowBuiltPlayer | BuildOptions.Development
#endif
            };

            BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                MstProperties properties = new MstProperties();
                properties.Add(Mst.Args.Names.StartMaster, true);
                properties.Add(Mst.Args.Names.StartSpawner, true);
                properties.Add(Mst.Args.Names.StartClientConnection, true);
                properties.Add(Mst.Args.Names.MasterIp, Mst.Args.MasterIp);
                properties.Add(Mst.Args.Names.MasterPort, Mst.Args.MasterPort);
                properties.Add(Mst.Args.Names.RoomExecutablePath, roomExePath);
                properties.Add(Mst.Args.Names.RoomIp, Mst.Args.RoomIp);
                properties.Add(Mst.Args.Names.RoomRegion, Mst.Args.RoomRegion);

                File.WriteAllText(Path.Combine(buildFolder, "application.cfg"), properties.ToReadableString("\n", "="));

                PDebug.Log("Master Server build succeeded: " + (summary.totalSize / 1024) + " kb");
            }

            if (summary.result == BuildResult.Failed)
            {
                PDebug.Log("Master Server build failed");
            }
        }

        [MenuItem(MstConstants.ToolMenu + "Win/Spawner")]
        private static void BuildSpawnerForWindows()
        {
            string buildFolder = Path.Combine("Builds", "App", "Win", "Spawner");
            string roomExePath = Path.Combine(Directory.GetCurrentDirectory(), "Builds", "App", "Win", "Room", "Room.exe");

            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = new[] {
                    "Assets/Scenes/Spawner.unity"
                },
                locationPathName = Path.Combine(buildFolder, "Spawner.exe"),
                target = BuildTarget.StandaloneWindows64,
#if UNITY_2021_1_OR_NEWER
                options = BuildOptions.ShowBuiltPlayer | BuildOptions.Development,
                subtarget = (int)StandaloneBuildSubtarget.Server
#else
                options = BuildOptions.EnableHeadlessMode | BuildOptions.ShowBuiltPlayer | BuildOptions.Development
#endif
            };

            BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                string appConfig = Mst.Args.AppConfigFile(buildFolder);

                MstProperties properties = new MstProperties();
                properties.Add(Mst.Args.Names.StartSpawner, true);
                properties.Add(Mst.Args.Names.StartClientConnection, true);
                properties.Add(Mst.Args.Names.MasterIp, Mst.Args.MasterIp);
                properties.Add(Mst.Args.Names.MasterPort, Mst.Args.MasterPort);
                properties.Add(Mst.Args.Names.RoomExecutablePath, roomExePath);
                properties.Add(Mst.Args.Names.RoomIp, Mst.Args.RoomIp);
                properties.Add(Mst.Args.Names.RoomRegion, Mst.Args.RoomRegion);

                File.WriteAllText(appConfig, properties.ToReadableString("\n", "="));

                PDebug.Log("Spawner build succeeded: " + (summary.totalSize / 1024) + " kb");
            }

            if (summary.result == BuildResult.Failed)
            {
                PDebug.Log("Spawner build failed");
            }
        }

        [MenuItem(MstConstants.ToolMenu + "Win/Client")]
        private static void BuildClientForWindows()
        {
            string buildFolder = Path.Combine("Builds", "App", "Win", "Client");

            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = new[] {
                    "Assets/Scenes/Loading.unity",
                    "Assets/Scenes/Auth.unity",
                    "Assets/Scenes/Home.unity",
                    "Assets/Scenes/GameSelection.unity",
                    "Assets/Scenes/CoreIngame.unity",
                },
                locationPathName = Path.Combine(buildFolder, "Client.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.ShowBuiltPlayer | BuildOptions.Development
            };

            BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                string appConfig = Mst.Args.AppConfigFile(buildFolder);

                MstProperties properties = new MstProperties();
                properties.Add(Mst.Args.Names.StartClientConnection, true);
                properties.Add(Mst.Args.Names.MasterIp, Mst.Args.MasterIp);
                properties.Add(Mst.Args.Names.MasterPort, Mst.Args.MasterPort);

                File.WriteAllText(appConfig, properties.ToReadableString("\n", "="));

                PDebug.Log("Client build succeeded: " + (summary.totalSize / 1024) + " kb");
            }

            if (summary.result == BuildResult.Failed)
            {
                Debug.Log("Client build failed");
            }
        }
        #endregion
    }
}
#endif