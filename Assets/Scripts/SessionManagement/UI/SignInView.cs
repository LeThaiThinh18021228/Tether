using Framework;
using MasterServerToolkit.MasterServer;
using TMPro;
using UnityEngine;
using Utilities;
namespace SessionManagement.UI
{
    public class SignInView : MonoBehaviour
    {
        [Header("Components"), SerializeField]
        private TMP_InputField usernameInputField;
        [SerializeField]
        private TMP_InputField passwordInputField;
        [SerializeField] PopupBehaviour popupBehaviour;
        void Awake()
        {
            popupBehaviour = GetComponent<PopupBehaviour>();
        }

        public void SignIn()
        {
            if (AuthBehaviour.Instance)
                AuthBehaviour.Instance.SignIn(usernameInputField.text, passwordInputField.text, (accountInfo, error) =>
                {
                    if (accountInfo != null)
                    {
                        if (accountInfo.IsEmailConfirmed)
                        {
                            PDebug.Log($"{Mst.Localization["signInSuccessResult"]} {Mst.Client.Auth.AccountInfo.ToString()}");
                            popupBehaviour.Close();
                            SceneController.Instance.Load(ESceneName.Home);
                        }
                        else
                        {
                        }
                    }
                    else
                    {
                        PDebug.LogError($"{Mst.Localization["signInErrorResult"]} {error}");
                    }
                });
            else
                PDebug.LogError($"No instance of {nameof(AuthBehaviour)} found. Please add {nameof(AuthBehaviour)} to scene to be able to use auth logic");
        }

        public void SignInAsGuest()
        {
            if (AuthBehaviour.Instance)
                AuthBehaviour.Instance.SignInAsGuest((accountInfo, error) =>
                {
                    if (accountInfo != null)
                    {
                        PDebug.Log($"0 \n 1", Mst.Localization["signInSuccessResult"], Mst.Client.Auth.AccountInfo);
                        popupBehaviour.Close();
                        SceneController.Instance.Load(ESceneName.Home);
                    }
                    else
                    {
                        PDebug.LogError($"{Mst.Localization["signInErrorResult"]} {error}");
                    }
                });
            else
                PDebug.LogError($"No instance of {nameof(AuthBehaviour)} found. Please add {nameof(AuthBehaviour)} to scene to be able to use auth logic");
        }

        public void Quit()
        {
            Mst.Runtime.Quit();
        }
    }
}