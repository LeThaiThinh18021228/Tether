using Framework;
using System.Collections.Generic;
using UnityEngine;

namespace Server
{
    public class DataAuth : PDataBlock<DataAuth>
    {
        [SerializeField] private int userId; public static int UserId { get { return Instance.userId; } set { Instance.userId = value; } }
        [SerializeField] private string username; public static string Username { get { return Instance.username; } set { Instance.username = value; } }
        [SerializeField] private string token; public static string Token { get { return Instance.token; } set { Instance.token = value; } }
        [SerializeField] private string refresh_token; public static string Refresh_token { get { return Instance.refresh_token; } set { Instance.refresh_token = value; } }
        [SerializeField] private List<KeyValuePair<string, string>> header; public static List<KeyValuePair<string, string>> Header { get { return Instance.header; } set { Instance.header = value; } }
        [SerializeField] private ObservableData<bool> isLinkedGoogleAccount; public static ObservableData<bool> IsLinkedGoogleAccount { get { return Instance.isLinkedGoogleAccount; } set { Instance.isLinkedGoogleAccount = value; } }
        [SerializeField] private ObservableData<bool> isLinkedAppleAccount; public static ObservableData<bool> IsLinkedAppleAccount { get { return Instance.isLinkedAppleAccount; } set { Instance.isLinkedAppleAccount = value; } }
        protected override void Init()
        {
            base.Init();
            Instance.header ??= new List<KeyValuePair<string, string>>();
            Instance.isLinkedAppleAccount ??= new ObservableData<bool>(false);
            Instance.isLinkedGoogleAccount ??= new ObservableData<bool>(false);
        }
    }
}

