using Framework;
using MasterServerToolkit.MasterServer;
using TMPro;
using UnityEngine;
using Utilities;
namespace SessionManagement.UI
{
    public class SignUpView : MonoBehaviour
    {
        [Header("Components"), SerializeField]
        private TMP_InputField usernameInputField;
        [SerializeField]
        private TMP_InputField emailInputField;
        [SerializeField]
        private TMP_InputField passwordInputField;
        [SerializeField]
        private TMP_InputField confirmPasswordInputField;

        public void SignUp()
        {
            if (AuthBehaviour.Instance)
                AuthBehaviour.Instance.SignUp(usernameInputField.text, emailInputField.text, passwordInputField.text, (isSuccessful, error) =>
                {
                    if (isSuccessful)
                    {
                        PDebug.LogError(Mst.Localization["signUpSuccessResult"]);
                        SceneController.Instance.Load(ESceneName.Home);
                    }
                    else
                    {
                        string outputMessage = Mst.Localization[$"signUpErrorResult {error}"];
                        PDebug.LogError(outputMessage);
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
