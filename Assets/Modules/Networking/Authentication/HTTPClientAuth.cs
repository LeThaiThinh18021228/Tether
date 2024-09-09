using Framework;
using Framework.SimpleJSON;
using Server;
using System.Collections.Generic;
using UnityEngine;

namespace SocialAuthClient
{
    public class HTTPClientAuth : Singleton<HTTPClientAuth>
    {
        #region LOGIN
        private static void HTTPPostLogin(JSONNode json, string loginRoute, Callback<string> handleResponse)
        {
            PCoroutine.PStartCoroutine(HTTPClientBase.Post(ServerConfig.HttpURL + "/authentication/login" + loginRoute, json.ToString()
                , (res) =>
                {
                    handleResponse(res);
                })

            );
        }
        private static void HTTPPostRegister(JSONNode json, string loginRoute, Callback<string> handleResponse)
        {
            PCoroutine.PStartCoroutine(HTTPClientBase.Post(ServerConfig.HttpURL + "/authentication/register" + loginRoute, json.ToString()
                , (res) =>
                {
                    handleResponse(res);
                })

            );
        }
        public static void LoginByGuest(string token, Callback<string> handleResponse)
        {
            JSONNode json = new JSONClass()
            {
                {"token", token}
            };
            HTTPPostLogin(json, "/guest", handleResponse);
        }
        public static void LoginByCredential(string username, string password, Callback<string> handleResponse)
        {
            JSONNode json = new JSONClass()
            {
                {"username", username},
                {"password", password}
            };
            HTTPPostLogin(json, "", handleResponse);
        }
        public static void RegisterByCredential(string username, string password, Callback<string> handleResponse)
        {
            JSONNode json = new JSONClass()
            {
                {"username", username},
                {"password", password}
            };
            HTTPPostRegister(json, "", handleResponse);
        }
        public static void LoginGoogle(string idToken, Callback<string> handleResponse)
        {
            PDebug.Log("LoginGoogle");
            JSONNode json = new JSONClass()
            {
                {"token",  idToken},
            };
            HTTPPostLogin(json, "/google", handleResponse);
        }

        public static void LoginApple(string authentication, Callback<string> handleResponse)
        {
            PDebug.Log("LoginApple");
            JSONNode json = new JSONClass()
            {
                {"token",  authentication},
            };

            HTTPPostLogin(json, "/apple", handleResponse);
        }
        #endregion

        #region LINK ACCOUNT
        public static void CheckLinkedAccount(Callback<string> handleResponse)
        {
            var header = GenHeaderUserIdAndToken();

            PCoroutine.PStartCoroutine(HTTPClientBase.Get(ServerConfig.HttpURL + "/authentication/link/check",
                (res) =>
                {
                    handleResponse(res);
                }
                , header)
            );
        }

        public static void LinkAccount(string idToken, Callback<string> onLinkedAccount, string route)
        {
            var header = GenHeaderUserIdAndToken();

            JSONNode json = new JSONClass()
            {
                { "token", idToken },
            };

            PCoroutine.PStartCoroutine(HTTPClientBase.Post(ServerConfig.HttpURL + "/authentication/link" + route, json.ToString(),
                (res) =>
                {
                    onLinkedAccount?.Invoke(res);
                }
                , header)
            );
        }
        public static void LinkGoogleAccount(string idToken, Callback<string> handleResponse)
        {
            LinkAccount(idToken, handleResponse, "/google");
        }

        public static void LinkAppleAccount(string idToken, Callback<string> handleResponse)
        {
            LinkAccount(idToken, handleResponse, "/apple");
        }
        #endregion

        #region LOGOUT-DISABLE-DELETE ACCOUNT
        public static void Logout(Callback<string> handleResponse)
        {
            var header = GenHeaderUserIdAndToken();

            PCoroutine.PStartCoroutine(HTTPClientBase.Get(ServerConfig.HttpURL + "/authentication/logout"
                , (res) =>
                {
                    handleResponse(res);
                }
                , header));
        }

        public static void DisableAccount(Callback<string> handleResponse)
        {
            var header = GenHeaderUserIdAndToken();

            PCoroutine.PStartCoroutine(HTTPClientBase.Get(ServerConfig.HttpURL + "/authentication/disable"
                , (res) =>
                {
                    handleResponse(res);
                }
                , header));
        }

        public static void DeleteAccount(Callback<string> handleResponse)
        {
            var header = GenHeaderUserIdAndToken();

            PCoroutine.PStartCoroutine(HTTPClientBase.Get(ServerConfig.HttpURL + "/authentication/delete"
                , (res) =>
                {
                    handleResponse(res);
                }
                , header));
        }
        #endregion
        public static List<KeyValuePair<string, string>> GenHeaderUserIdAndToken()
        {
            List<KeyValuePair<string, string>> header = new()
            {
                new("userId", DataAuth.UserId.ToString()),
                new("token", DataAuth.Token.ToString())
            };
            return header;
        }
    }

}
