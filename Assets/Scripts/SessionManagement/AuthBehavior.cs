using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using UnityEngine;
using UnityEngine.Events;
using static MasterServerToolkit.MasterServer.MstAuthClient;

namespace SessionManagement
{
    public class AuthBehaviour : BaseClientBehaviour
    {
        #region INSPECTOR

        [Header("Components"), SerializeField]
        protected ClientToMasterConnector clientToMasterConnector;

        [Header("Settings"), SerializeField]
        protected bool rememberUser = true;

        [Header("Editor Settings"), SerializeField]
        protected string defaultUsername = "qwerty";
        [SerializeField]
        protected string defaultEmail = "qwerty@mail.com";
        [SerializeField]
        protected string defaultPassword = "qwerty123!@#";
        [SerializeField]
        protected bool useDefaultCredentials = false;
        [SerializeField]
        protected bool autoSignInAsGuest = false;

        public UnityEvent OnSignedUpEvent;
        public UnityEvent OnSignedInEvent;
        public UnityEvent OnSignedOutEvent;
        public UnityEvent OnEmailConfirmedEvent;
        public UnityEvent OnPasswordChangedEvent;

        #endregion

        protected string outputMessage = string.Empty;
        /// <summary>
        /// Master server connector
        /// </summary>
        public virtual ClientToMasterConnector Connector
        {
            get
            {
                if (!clientToMasterConnector)
                    clientToMasterConnector = FindObjectOfType<ClientToMasterConnector>();

                if (!clientToMasterConnector)
                {
                    var connectorObject = new GameObject("--CLIENT_TO_MASTER_CONNECTOR");
                    clientToMasterConnector = connectorObject.AddComponent<ClientToMasterConnector>();
                }

                return clientToMasterConnector;
            }
        }
        protected static AuthBehaviour _instance;
        public static AuthBehaviour Instance
        {
            get
            {
                if (!_instance) Logs.Error("Instance of AuthBehaviour is not found");
                return _instance;
            }
        }

        protected override void Awake()
        {
            if (_instance)
            {
                Destroy(_instance.gameObject);
                return;
            }

            _instance = this;

            base.Awake();

            defaultUsername = Mst.Args.AsString("-mstDefaultUsername", defaultUsername);
            defaultEmail = Mst.Args.AsString("-mstDefaultEmail", defaultEmail);
            defaultPassword = Mst.Args.AsString("-mstDefaultPassword", defaultPassword);
            rememberUser = Mst.Args.AsBool("-mstRememberUser", rememberUser);
            useDefaultCredentials = Mst.Args.AsBool("-mstUseDefaultCredentials", useDefaultCredentials);

            // Listen to auth events
            Mst.Client.Auth.OnSignedInEvent += Auth_OnSignedInEvent;
            Mst.Client.Auth.OnSignedOutEvent += Auth_OnSignedOutEvent;
            Mst.Client.Auth.OnSignedUpEvent += Auth_OnSignedUpEvent;
            Mst.Client.Auth.OnEmailConfirmedEvent += Auth_OnEmailConfirmedEvent;
            Mst.Client.Auth.OnPasswordChangedEvent += Auth_OnPasswordChangedEvent;

        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            // unregister from connection events
            Connection?.RemoveConnectionOpenListener(OnClientConnectedToServer);
            Connection?.RemoveConnectionCloseListener(OnClientDisconnectedFromServer);

            // Unregister from listening to auth events
            Mst.Client.Auth.OnSignedInEvent -= Auth_OnSignedInEvent;
            Mst.Client.Auth.OnSignedOutEvent -= Auth_OnSignedOutEvent;
            Mst.Client.Auth.OnSignedUpEvent -= Auth_OnSignedUpEvent;
            Mst.Client.Auth.OnEmailConfirmedEvent -= Auth_OnEmailConfirmedEvent;
            Mst.Client.Auth.OnPasswordChangedEvent -= Auth_OnPasswordChangedEvent;
        }

        protected override void OnInitialize()
        {
            // If we want to use default credentials for signin or signup views
            if (useDefaultCredentials && Mst.Runtime.IsEditor)
            {
                var credentials = new MstProperties();
                credentials.Set(MstDictKeys.USER_NAME, defaultUsername);
                credentials.Set(MstDictKeys.USER_PASSWORD, defaultPassword);
                credentials.Set(MstDictKeys.USER_EMAIL, defaultEmail);
                // Set signin/up credentials
            }

            Mst.Client.Auth.RememberMe = rememberUser;

            // Listen to connection events
            Connection.AddConnectionOpenListener(OnClientConnectedToServer);
            Connection.AddConnectionCloseListener(OnClientDisconnectedFromServer, false);
        }

        /// <summary>
        /// 
        /// </summary>
        protected virtual void OnClientConnectedToServer(IClientSocket client)
        {
            Mst.Events.Invoke(MstEventKeys.hideLoadingInfo);

            if (Mst.Client.Auth.IsSignedIn)
            {
                Auth_OnSignedInEvent();
            }
            else
            {
                if (Mst.Client.Auth.HasAuthToken())
                {
                    SignInWithToken();
                }
                else
                {
                    if (autoSignInAsGuest)
                    {
                        SignInAsGuest();
                    }
                    else
                    {
                        Mst.Events.Invoke(MstEventKeys.showSignInView);
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        protected virtual void OnClientDisconnectedFromServer(IClientSocket client)
        {
            Connection?.RemoveConnectionOpenListener(OnClientConnectedToServer);
            Connection?.RemoveConnectionCloseListener(OnClientDisconnectedFromServer);
            SignOut();
            OnInitialize();
        }

        /// <summary>
        /// Invokes when user signed in
        /// </summary>
        protected virtual void Auth_OnSignedInEvent()
        {
            if (Mst.Client.Auth.AccountInfo.IsEmailConfirmed || Mst.Client.Auth.AccountInfo.IsGuest)
                OnSignedInEvent?.Invoke();
        }

        /// <summary>
        /// Invokes when user signed up
        /// </summary>
        protected virtual void Auth_OnSignedUpEvent()
        {
            OnSignedUpEvent?.Invoke();
        }

        /// <summary>
        /// Invokes when user signed out
        /// </summary>
        protected virtual void Auth_OnSignedOutEvent()
        {
            OnSignedOutEvent?.Invoke();
        }

        /// <summary>
        /// Invokes when user changed his password
        /// </summary>
        protected virtual void Auth_OnPasswordChangedEvent()
        {
            OnPasswordChangedEvent?.Invoke();
        }

        /// <summary>
        /// Invokes when user has confirmed his email
        /// </summary>
        protected virtual void Auth_OnEmailConfirmedEvent()
        {
            OnEmailConfirmedEvent?.Invoke();
        }

        /// <summary>
        /// Sends sign in request to master server
        /// </summary>
        /// <param name="username"></param>
        /// <param name="password"></param>
        public virtual void SignIn(string username, string password, SignInCallback signInCallback = null)
        {
            Logger.Debug(Mst.Localization["signInProgress"]);
            Mst.Client.Auth.SignInWithLoginAndPassword(username, password, (accountInfo, error) =>
            {
                signInCallback?.Invoke(accountInfo, error);

            }, Connection);
        }

        /// <summary>
        /// Sends sign in as guest request to master server
        /// </summary>
        public virtual void SignInAsGuest(SignInCallback callback = null)
        {
            Logger.Debug(Mst.Localization["signInProgress"]);
            Mst.Client.Auth.SignInAsGuest((accountInfo, error) =>
            {
                callback?.Invoke(accountInfo, error);
                Logger.Debug("Auto sign in successfully");
                Logger.Debug($"This is Bot {Mst.Args.IsBotClient}");
                MstProperties prop = new MstProperties();
                prop.Add(Mst.Args.Names.IsBotClient, Mst.Args.IsBotClient);
                Mst.Client.Auth.BindExtraProperties(prop, null);
                if (Mst.Args.IsBotClient)
                {
                    MstTimer.WaitForSeconds(1, () =>
                    {
                        Logger.Debug($"Auto-connecting {Mst.Args.GameId}");
                        MasterServerToolkit.Bridges.FishNetworking.RoomClientManager.Instance.AutoConnectGame(Mst.Args.GameId);

                    });
                }
            });
        }

        /// <summary>
        /// Sends request to master server to signed in with token
        /// </summary>
        public virtual void SignInWithToken(SignInCallback callback = null)
        {
            Logger.Debug(Mst.Localization["signInProgress"]);
            Mst.Client.Auth.SignInWithToken((accountInfo, error) =>
            {
                callback?.Invoke(accountInfo, error);
                if (accountInfo != null)
                {
                    if (accountInfo.IsGuest || accountInfo.IsEmailConfirmed)
                    {
                        Logger.Debug($"{Mst.Localization["signInSuccessResult"]} {Mst.Client.Auth.AccountInfo}");
                    }
                    else
                    {

                    }
                }
                else
                {
                    outputMessage = $"{Mst.Localization["signInErrorResult"]} {error}";
                    Logger.Error(outputMessage);
                }
            });
        }

        /// <summary>
        /// Sends sign up request to master server
        /// </summary>
        public virtual void SignUp(string username, string useremail, string userpassword, SuccessCallback callback = null)
        {
            Mst.Events.Invoke(MstEventKeys.showLoadingInfo, Mst.Localization["signUpProgress"]);

            Logger.Debug(Mst.Localization["signUpProgress"]);

            var credentials = new MstProperties();
            credentials.Set(MstDictKeys.USER_NAME, username);
            credentials.Set(MstDictKeys.USER_EMAIL, useremail);
            credentials.Set(MstDictKeys.USER_PASSWORD, userpassword);
            Mst.Client.Auth.SignUp(credentials, (isSuccessful, error) =>
            {
                callback?.Invoke(isSuccessful, error);
            });
        }

        /// <summary>
        /// Send request to master server to change password
        /// </summary>
        /// <param name="userEmail"></param>
        /// <param name="resetCode"></param>
        /// <param name="newPassword"></param>
        public virtual void ResetPassword(string userEmail, string resetCode, string newPassword, SuccessCallback callback = null)
        {
            Logger.Debug(Mst.Localization["changePasswordProgress"]);
            Mst.Client.Auth.ChangePassword(userEmail, resetCode, newPassword, (isSuccessful, error) =>
            {
                callback?.Invoke(isSuccessful, error);
                if (isSuccessful)
                {

                }
                else
                {
                    string outputMessage = $"{Mst.Localization["changePasswordErrorResult"]} {error}";
                    Logger.Error(outputMessage);
                }
            });
        }

        /// <summary>
        /// Sends request to master to generate rest password code and send it to user email
        /// </summary>
        /// <param name="userEmail"></param>
        public virtual void RequestResetPasswordCode(string userEmail, SuccessCallback callback = null)
        {
            Logger.Debug(Mst.Localization["changePasswordCodeSuccessResult"]);

            Mst.Client.Auth.RequestPasswordReset(userEmail, (isSuccessful, error) =>
            {
                callback?.Invoke(isSuccessful, error);
                if (isSuccessful)
                {
                    Mst.Options.Set(MstDictKeys.RESET_PASSWORD_EMAIL, userEmail);
                }
                else
                {
                    string outputMessage = $"{Mst.Localization["changePasswordCodeErrorResult"]} {error}";
                    Logger.Error(outputMessage);
                }
            });
        }

        /// <summary>
        /// Sign out user
        /// </summary>
        public virtual void SignOut()
        {
            Logger.Debug("Sign out");
            Mst.Client.Auth.SignOut(true);
        }

        /// <summary>
        /// Sends request to get confirmation code
        /// </summary>
        public virtual void RequestConfirmationCode(SuccessCallback successCallback = null)
        {
            Logger.Debug(Mst.Localization["confirmationCodeSendingProcess"]);
            Mst.Client.Auth.RequestEmailConfirmationCode((isSuccessful, error) =>
            {
                successCallback?.Invoke(isSuccessful, error);
                if (isSuccessful)
                {
                    //Mst.Events.Invoke(MstEventKeys.showOkDialogBox, new OkDialogBoxEventMessage($"{Mst.Localization["confirmationCodeSendingSuccessResult"]} '{Mst.Client.Auth.AccountInfo.Email}'", null));
                }
                else
                {
                    string outputMessage = $"{Mst.Localization["confirmationCodeSendingErrorResult"]}: {error}";
                    Logger.Error(outputMessage);
                }
            });
        }

        /// <summary>
        /// Sends request to confirm account with confirmation code
        /// </summary>
        public virtual void ConfirmAccount(string confirmationCode, SuccessCallback callback = null)
        {
            Logger.Debug(Mst.Localization["accountConfirmationProgress"]);
            Mst.Client.Auth.ConfirmEmail(confirmationCode, (isSuccessful, error) =>
            {
                callback?.Invoke(isSuccessful, error);
                if (isSuccessful)
                {
                }
                else
                {
                    string outputMessage = $"{Mst.Localization["accountConfirmationErrorResult"]} {error}";
                    Logger.Error(outputMessage);
                }
            });
        }

        /// <summary>
        /// Quits the application
        /// </summary>
        public virtual void Quit()
        {
            Mst.Runtime.Quit();
        }


    }
}