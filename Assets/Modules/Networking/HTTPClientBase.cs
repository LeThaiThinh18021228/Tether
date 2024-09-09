using Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class HTTPClientBase
{
    static public IEnumerator Get(string url, Action<string> callback, List<KeyValuePair<string, string>> headers = null)
    {
        using UnityWebRequest webRequest = UnityWebRequest.Get(url);
        for (int i = 0; i < headers?.Count; i++)
        {
            webRequest.SetRequestHeader(headers[i].Key, headers[i].Value);
        }

        yield return webRequest.SendWebRequest();

        if (webRequest.result == UnityWebRequest.Result.Success)
        {
            string response = webRequest.downloadHandler.text;
            callback?.Invoke(response);
            PDebug.Log("Response: {0}", response);
        }
        else
        {
            PDebug.LogError("Error: {0}", webRequest.error);
        }
    }

    static public IEnumerator Post(string url, string data, Action<string> callback, List<KeyValuePair<string, string>> headers = null)
    {
        /**/
        byte[] bodyRaw = UTF8Encoding.UTF8.GetBytes(data);
        using UnityWebRequest webRequest = new(url, "POST");
        webRequest.SetRequestHeader("Content-Type", "application/json");

        for (int i = 0; i < headers?.Count; i++)
        {
            webRequest.SetRequestHeader(headers[i].Key, headers[i].Value);
        }

        PDebug.Log("{0}", url + data);
        webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
        webRequest.downloadHandler = new DownloadHandlerBuffer();
        yield return webRequest.SendWebRequest();
        if (webRequest.result == UnityWebRequest.Result.Success)
        {
            string response = webRequest.downloadHandler.text;
            PDebug.Log("Response: {0}", response);
            callback?.Invoke(response);
        }
        else
        {
            PDebug.LogError("Error: {0}", webRequest.error);
        }
    }
    static public IEnumerator LoadAudioFromURL(string url, AudioType audioType, Callback<AudioClip> callback)
    {
        using UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(url, audioType);
        yield return www.SendWebRequest();

        if (www.result == UnityWebRequest.Result.Success)
        {
            AudioClip audioClip = DownloadHandlerAudioClip.GetContent(www);
            callback?.Invoke(audioClip);
        }
        else
        {
            PDebug.LogError("Error downloading audio: " + www.error);
        }
    }

    static public IEnumerator LoadTextureFromURL(string url, Callback<Texture2D> callback)
    {
        using UnityWebRequest www = UnityWebRequestTexture.GetTexture(url);
        yield return www.SendWebRequest();

        if (www.result == UnityWebRequest.Result.Success)
        {
            Texture2D texture = DownloadHandlerTexture.GetContent(www);
            callback?.Invoke(texture);
        }
        else
        {
            PDebug.Log("Error dowmloading sprite: " + www.error);
        }
    }
}
