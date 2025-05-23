﻿#if UNITY_EDITOR

using System.Reflection;

namespace Utilities
{
    public static class EditorHelper
    {
        public static void ClearLog()
        {
            var assembly = Assembly.GetAssembly(typeof(UnityEditor.Editor));
            var type = assembly.GetType("UnityEditor.LogEntries");
            var method = type.GetMethod("Clear");
            method.Invoke(new object(), null);
        }
    }
}

#endif