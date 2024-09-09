using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Utilities;

public class LoadingScene : MonoBehaviour
{
    [SerializeField] float loadingDuration = 1;
    [SerializeField] GameObject loadingPopup;
    AudioListener[] aL;
    EventSystem[] eS;
    private void Awake()
    {
        GetComponent<Image>().DOFade(0, loadingDuration).OnComplete(() =>
        {
        });
        InvokeRepeating(nameof(CheckMultipleAudioListener), 0, 0.1f);
    }
    private void Start()
    {
        SceneController.Instance.Load(ESceneName.Auth, loadingPopup);
    }
    public void CheckMultipleAudioListener()
    {
        aL = FindObjectsOfType<AudioListener>();
        eS = FindObjectsOfType<EventSystem>();
        if (aL.Length >= 2)
        {
            DestroyImmediate(aL[0]);
        }
        if (eS.Length >= 2)
        {
            DestroyImmediate(eS[0]);
        }
    }
}
