#if UNITY_EDITOR

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Framework
{
    public static class ScriptableObjectHelper
    {
        public static T CreateAsset<T>(string path, string fileName) where T : ScriptableObject
        {
            string filePath = string.Format("{0}/{1}.asset", path, fileName);

            T asset = ScriptableObject.CreateInstance<T>();

            AssetDatabase.CreateAsset(asset, filePath);
            SaveAssetsDatabase();

            return AssetDatabase.LoadAssetAtPath(filePath, typeof(T)) as T;
        }

        public static void SaveAsset(ScriptableObject asset)
        {
            EditorUtility.SetDirty(asset);
            SaveAssetsDatabase();
        }

        public static T LoadOrCreateNewAsset<T>(string path, string fileName = null) where T : ScriptableObject
        {
            string filePath = fileName == null ? string.Format("{0}", path) : string.Format("{0}/{1}.asset", path, fileName);

            T asset = AssetDatabase.LoadAssetAtPath(filePath, typeof(T)) as T;
            if (asset == null)
            {
                asset = CreateAsset<T>(path, fileName);
            }

            return asset;
        }

        public static T CopyAsset<T>(string oldPath, string newPath) where T : ScriptableObject
        {
            DeleteAsset<T>(newPath);

            AssetDatabase.CopyAsset(oldPath, newPath);
            SaveAssetsDatabase();

            return AssetDatabase.LoadAssetAtPath(newPath, typeof(T)) as T;
        }

        public static void RenameAsset(string originalPath, string fileNane)
        {
            AssetDatabase.RenameAsset(originalPath, fileNane);
            SaveAssetsDatabase();
        }

        public static void DeleteAsset<T>(string assetPath)
        {
            if (AssetDatabase.LoadAssetAtPath(assetPath, typeof(T)) != null)
            {
                AssetDatabase.DeleteAsset(assetPath);
            }
        }

        public static List<T> FindAssetsByType<T>(out List<string> paths) where T : ScriptableObject
        {
            paths = new List<string>();
            List<T> assets = new List<T>();
            string[] guids = AssetDatabase.FindAssets(string.Format("t:{0}", typeof(T)));
            for (int i = 0; i < guids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                paths.Add(assetPath);
                T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
                if (asset != null)
                {
                    assets.Add(asset);
                }
            }
            return assets;
        }
        public static List<T> FindAssetsByType<T>() where T : ScriptableObject
        {
            List<T> assets = new List<T>();
            string[] guids = AssetDatabase.FindAssets(string.Format("t:{0}", typeof(T)));
            for (int i = 0; i < guids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
                if (asset != null)
                {
                    assets.Add(asset);
                }
            }
            return assets;
        }
        static void SaveAssetsDatabase()
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}

#endif