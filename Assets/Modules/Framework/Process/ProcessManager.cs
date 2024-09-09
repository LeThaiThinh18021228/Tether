using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using UnityEngine;

namespace Framework
{
    public class ProcessManager
    {
        protected static object processLock = new object();
        static Dictionary<int, Process> processes = new();
        public static void RunProcess(string executablePath, Dictionary<string, string> processArguments)
        {
            var startProcessInfo = new ProcessStartInfo(executablePath)
            {
                CreateNoWindow = false,
                UseShellExecute = true,
                Arguments = Args.FormatArguments(processArguments)
            };

            PDebug.LogError($"Starting process with args: {processArguments}");
            try
            {
                new Thread(() =>
                {
                    int pid = -1;
                    try
                    {
                        using (var process = Process.Start(startProcessInfo))
                        {
                            pid = process.Id;
                            PDebug.LogError($"Process [{startProcessInfo.FileName}] started. pid: {process.Id}");
                            lock (processLock)
                            {
                                processes[pid] = process;
                            }
                            process.WaitForExit();
                        }
                    }
                    catch (Exception e)
                    {
                        PDebug.LogError($"Failed to start process at [{startProcessInfo.FileName}]. Check 'SpawnerBehaviour' or 'application.cfg'. Exception: {e}");
                    }
                    finally
                    {
                        lock (processLock)
                        {
                            processes.Remove(pid);
                        }
                        PDebug.Log($"Process with spawn id [{pid}] exited.");
                    }

                }).Start();
            }
            catch (Exception e)
            {
                PDebug.LogError(e.Message);
            }
        }
        public static void RunProcess(string executablePath, string processArguments)
        {
            var startProcessInfo = new ProcessStartInfo(executablePath)
            {
                CreateNoWindow = false,
                UseShellExecute = true,
                Arguments = processArguments
            };

            PDebug.Log($"Starting process with args: {processArguments}");
            try
            {
                new Thread(() =>
                {
                    int pid = -1;
                    try
                    {
                        using (var process = Process.Start(startProcessInfo))
                        {
                            pid = process.Id;
                            PDebug.Log($"Process [{startProcessInfo.FileName}] started. pid: {process.Id}");
                            lock (processLock)
                            {
                                processes[pid] = process;
                            }
                            process.WaitForExit();
                        }
                    }
                    catch (Exception e)
                    {
                        PDebug.LogError($"Failed to start process at [{startProcessInfo.FileName}]. Check 'SpawnerBehaviour' or 'application.cfg'. Exception: {e}");
                    }
                    finally
                    {
                        lock (processLock)
                        {
                            processes.Remove(pid);
                        }
                        PDebug.Log($"Process with spawn id [{pid}] exited.");
                    }

                }).Start();
            }
            catch (Exception e)
            {
                PDebug.LogError(e.Message);
            }
        }
        public static void KillProcess(int id)
        {
            try
            {
                // Get the process by ID
                Process process = Process.GetProcessById(id);

                // Kill the process
                process.Kill();

                // Optionally wait for the process to exit
                process.WaitForExit();

                Console.WriteLine($"Process with ID {id} has been terminated.");
            }
            catch (ArgumentException)
            {
                Console.WriteLine($"No process with ID {id} is currently running.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while trying to kill process with ID {id}: {ex.Message}");
            }
        }



    }
}
