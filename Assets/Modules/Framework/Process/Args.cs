using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Framework
{
    public class Args
    {
        [SerializeField] public static Dictionary<string, string> cmdArgs= new();
        [SerializeField] public static Dictionary<string, string> envArgs= new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] 
        private static void InitArgs()
        {         
            IDictionary environmentVariables = Environment.GetEnvironmentVariables();
            foreach (DictionaryEntry variable in environmentVariables)
            {
                envArgs.Add((string)variable.Key, (string)variable.Value);
            }
            cmdArgs = ExtractArguments(System.Environment.GetCommandLineArgs());
        }

        public static Dictionary<string, string> ExtractArguments(string[] arguments)
        {
            var result = new Dictionary<string, string>();

            if (arguments == null || arguments.Length == 0)
            {
                return result;
            }

            foreach (var pair in arguments)
            {
                // Split each pair by '=' to separate the key and value
                var keyValue = pair.Split('=');
                if (keyValue.Length == 2)
                {
                    result[keyValue[0]] = keyValue[1];
                }
            }

            return result;
        }

        public static string FormatArguments(Dictionary<string, string> args)
        {
            if (args == null || args.Count == 0)
            {
                return string.Empty;
            }

            // Convert each key-value pair into a string with the format: "key=value"
            var formattedArguments = args.Select(arg => $"{arg.Key}={arg.Value}");

            // Join all arguments into a single string with a space separator
            return string.Join(" ", formattedArguments);
        }
    }
}
