using Framework.SimpleJSON;
using System;

namespace Server
{
    public static class JsonExtension
    {
        public static void PostRequestServer(this JSONNode json, Action<string> res)
        {
            HTTPClientBase.Post(ServerConfig.HttpURL, json, res, DataAuth.Header);
        }
    }
}
