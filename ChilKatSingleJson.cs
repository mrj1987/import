using Chilkat;
using StandardUtils;
using StringBuilder = System.Text.StringBuilder;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;

namespace GCRealTime
{
    class ChilkatSingleJson
    {
        public string APIKey { get; set; }
        public string APIBaseURL { get; set; }
        public string APILoginBaseURL { get; set; }
        public string APICLID { get; set; }
        public string APICLSC { get; set; }
        public string APISockURL { get; set; }
        public DateTime APILastAPICall { get; set; }
        public string APIProxyAddress { get; set; }
        public int APIProxyPort { get; set; }
        public string APIProxyUserName { get; set; }
        public string APIProxyPassword { get; set; }
        public string CustomerKeyID { get; set; }
        Global ChilGlob = new Global();

        public bool Initialize()
        {
            Utils UCAUtils = new Utils();

            CustomerKeyID = UCAUtils.ReadSetting("CSG_CUSTOMERKEYID");

            Console.WriteLine("ChilKat: Unlocking ChilkKat Code");
            if (UnlockChilKat() != true)
            {
                Console.WriteLine("ChilKat: Cannot Unlock CHilkat - Do Not Proceed");
                return false;
            }

            APICLID = CSG.Adapter.Compatability.LegacyOptions.GetOption("CSG_GENESYS_USERID");
            APICLSC = CSG.Adapter.Compatability.LegacyOptions.GetOption("CSG_GENESYS_SECRET");
            APIBaseURL = CSG.Adapter.Compatability.LegacyOptions.GetOption("CSG_GENESYS_URL");

            APILoginBaseURL = "https://login.mypurecloud.com.au/oauth/token";
            APISockURL = "streaming.mypurecloud.com.au";

            Console.WriteLine("ChilKat: Getting API Key");
            if (GetAuthAPIKey() != true)
            {
                Console.WriteLine("ChilKat: Cannot Obtain API Key - Do Not Proceed");
                return false;
            }

            return true;
        }

        public string ReturnJson(string APIURI)
        {
            Chilkat.Rest ChilRest = new Chilkat.Rest();
            Chilkat.Socket ChilSocket = new Chilkat.Socket();

            if (APIProxyAddress != null)
            {
                ChilSocket.HttpProxyHostname = APIProxyAddress;
                ChilSocket.HttpProxyPort = APIProxyPort;

                if (APIProxyUserName != null)
                {
                    ChilSocket.HttpProxyUsername = APIProxyUserName;
                    ChilSocket.HttpProxyPassword = APIProxyPassword;
                }
            }

            bool AutoReconnect = true;
            bool TLS = true;
            int Port = 443;
            int MaxWaitMs = 5000;

            bool success = ChilSocket.Connect(APIBaseURL, Port, TLS, MaxWaitMs);

            if (success != true)
            {
                Console.WriteLine("ChilKat GET : Connect Error {0}", ChilSocket.LastErrorText);
                return null;
            }

            success = ChilRest.UseConnection(ChilSocket, AutoReconnect);

            if (success != true)
            {
                Console.WriteLine("ChilKat GET : Connect Error {0}", ChilRest.LastErrorText);
                return null;
            }

            StringBuilder JSONResponse = new StringBuilder();
            string SingleJsonResponse = String.Empty;

            int Attempts = 1;

            while (Attempts < 6)
            {
                if ((APILastAPICall.AddSeconds(86400) - DateTime.Now).TotalSeconds < 43200)
                {
                    Console.WriteLine("ChilKat GET : Reset API Needed");
                    if (GetAuthAPIKey() == false)
                    {
                        throw new Exception("ChilKat GET : Cannot Get New API Key");
                    }
                }
                Chilkat.StringBuilder AuthHeaderVal = new Chilkat.StringBuilder();
                AuthHeaderVal.Append("Bearer ");
                AuthHeaderVal.Append(APIKey);
                ChilRest.Authorization = AuthHeaderVal.GetAsString();

                ChilRest.AddHeader("Content-Type", "application/json; charset=UTF-8");
                SingleJsonResponse = ChilRest.FullRequestNoBody("GET", APIURI);

                if (ChilRest.ResponseStatusCode == 200 && SingleJsonResponse.ToString().Contains("gateway.timeout") == false && SingleJsonResponse.ToString().Contains("service.unavailable") == false)
                {
                    ChilRest.Dispose();
                    ChilSocket.Dispose();

                    return SingleJsonResponse.ToString();
                }
                else
                {
                    Attempts++;

                    JSONResponse.Append(SingleJsonResponse);
                    JSONResponse.Append(", ");

                    switch (ChilRest.ResponseStatusCode)
                    {
                        case 429:
                            try
                            {
                                // JObject SingleJsonResponseObj = JObject.Parse(SingleJsonResponse);

                                // JToken ExceptionMessage = SingleJsonResponseObj.SelectToken("$.message");

                                // Match Match = Regex.Match(ExceptionMessage.ToString(), "\\[([^\\[\\]]*)\\]");

                                // string SleepTime = Match.Value.TrimStart('[').TrimEnd(']');

                                // Console.WriteLine("{0} QRT:: Too Many Requests: Wait {1} seconds before retrying", DateTime.UtcNow, SleepTime);

                                // System.Threading.Thread.Sleep(int.Parse(SleepTime)*1000);
                                Console.WriteLine("Retrying Query");
                                GetAuthAPIKey();
                                
                            }      
                            catch
                            {
                                System.Threading.Thread.Sleep(60000);
                            }                          
                            break;
                        case 503:
                        //TODO Resolve the 504 handling 
                        case 504:
                            JSONResponse.Append("{}");
                            Attempts = 7;
                            break;
                    }
                }
            }

            ChilRest.Dispose();
            ChilSocket.Dispose();

            return JSONResponse.ToString();
        }

        public string ReturnJson(string APIURI, string Body)
        {
            Chilkat.Rest ChilRest = new Chilkat.Rest();
            Chilkat.Socket ChilSocket = new Chilkat.Socket();

            if (APIProxyAddress != null)
            {
                //Console.WriteLine("ChilKat POST :  Using Proxy For Connection Address: {0}", APIProxyAddress);
                ChilSocket.HttpProxyHostname = APIProxyAddress;
                ChilSocket.HttpProxyPort = APIProxyPort;

                if (APIProxyUserName != null)
                {
                    ChilSocket.HttpProxyUsername = APIProxyUserName;
                    ChilSocket.HttpProxyPassword = APIProxyPassword;
                }
            }

            bool AutoReconnect = true;
            bool TLS = true;
            int Port = 443;
            int MaxWaitMs = 5000;

            bool success = ChilSocket.Connect(APIBaseURL, Port, TLS, MaxWaitMs);

            if (success != true)
            {
                Console.WriteLine("ChilKat POST : Connect Error {0}", ChilSocket.LastErrorText);
                return null;
            }

            success = ChilRest.UseConnection(ChilSocket, AutoReconnect);

            if (success != true)
            {
                Console.WriteLine("ChilKat POST : Connect Error {0}", ChilRest.LastErrorText);
                return null;
            }

            StringBuilder JSONResponse = new StringBuilder();
            string SingleJsonResponse = String.Empty;
            if (success != true)
            {
                Console.WriteLine(ChilRest.LastErrorText);
                return "Connect Error";
            }

            int Attempts = 1;

            while (Attempts < 6)
            {
                if ((APILastAPICall.AddSeconds(86400) - DateTime.Now).TotalSeconds < 43200)
                {
                    Console.WriteLine("ChilKat POST : Reset API Needed");
                    if (GetAuthAPIKey() == false)
                    {
                        throw new Exception("ChilKat POST : Cannot Get New API Key");
                    }

                    APILastAPICall = DateTime.Now;

                }

                Chilkat.StringBuilder AuthHeaderVal = new Chilkat.StringBuilder();
                AuthHeaderVal.Append("Bearer ");
                AuthHeaderVal.Append(APIKey);
                ChilRest.Authorization = AuthHeaderVal.GetAsString();

                ChilRest.AddHeader("Content-Type", "application/json; charset=UTF-8");
                SingleJsonResponse = ChilRest.FullRequestString("POST", APIURI, Body);

                if (ChilRest.ResponseStatusCode == 200 && SingleJsonResponse.ToString().Contains("gateway.timeout") == false && JSONResponse.ToString().Contains("service.unavailable") == false)
                {
                    ChilRest.Dispose();
                    ChilSocket.Dispose();

                    return SingleJsonResponse.ToString();
                }
                else
                {
                    Attempts++;

                    JSONResponse.Append(SingleJsonResponse);

                    switch (ChilRest.ResponseStatusCode)
                    {
                        case 400:
                            throw new System.Net.WebException(
                                string.Format(
                                    "Received HTTP 400 Bad Request from POST {0}",
                                    APIURI
                                )
                            );   
                        case 429:
                            try
                            {
                                // JObject SingleJsonResponseObj = JObject.Parse(SingleJsonResponse);

                                // JToken ExceptionMessage = SingleJsonResponseObj.SelectToken("$.message");

                                // Match Match = Regex.Match(ExceptionMessage.ToString(), "\\[([^\\[\\]]*)\\]");

                                // string SleepTime = Match.Value.TrimStart('[').TrimEnd(']');

                                // Console.WriteLine("{0} QRT:: Too Many Requests: Wait {1} seconds before retrying", DateTime.UtcNow, SleepTime);

                                // System.Threading.Thread.Sleep(int.Parse(SleepTime)*1000);
                                Console.WriteLine("Retrying Query");
                                GetAuthAPIKey();
                            }
                            catch
                            {
                                System.Threading.Thread.Sleep(60000);
                            }     
                            break;
                        case 503:
                        //TODO Resolve the 504 handling 
                        case 504:
                        default:
                            Attempts = 7;
                            break;

                    }

                }


            }

            ChilRest.Dispose();
            ChilSocket.Dispose();

            return JSONResponse.ToString();
        }

        public bool GetAuthAPIKey()
        {

            Chilkat.Http ChilHTTP = new Chilkat.Http();
            Chilkat.HttpRequest ChilTokenReq = new Chilkat.HttpRequest();
            Chilkat.HttpResponse ChilResp = new Chilkat.HttpResponse();
            Chilkat.JsonObject ChilJSON = new Chilkat.JsonObject();

            ChilTokenReq.HttpVerb = "POST";
            ChilTokenReq.AddParam("grant_type", "client_credentials");
            ChilTokenReq.AddParam("client_id", APICLID);
            ChilTokenReq.AddParam("client_secret", APICLSC);


            if (APIProxyAddress != null)
            {
                Console.WriteLine("ChilKat: API Creation. Using Proxy For Connection Address: {0}", APIProxyAddress);
                ChilHTTP.ProxyDomain = APIProxyAddress;
                ChilHTTP.ProxyPort = APIProxyPort;

                if (APIProxyUserName != null)
                {
                    ChilHTTP.ProxyLogin = APIProxyUserName;
                    ChilHTTP.ProxyPassword = APIProxyPassword;
                }
            }

            ChilResp = ChilHTTP.PostUrlEncoded(APILoginBaseURL, ChilTokenReq);
            if (ChilHTTP.LastMethodSuccess == false)
            {
                Console.WriteLine(ChilHTTP.LastErrorText);
                return false;
            }


            // Make sure we got a 200 response status code, otherwise it's an error.
            if (ChilResp.StatusCode != 200)
            {
                Console.WriteLine("ChilKat: POST to token endpoint failed.");
                Console.WriteLine("ChilKat: Received response status code " + Convert.ToString(ChilResp.StatusCode));
                Console.WriteLine("ChilKat: Response body containing error text or JSON:");
                Console.WriteLine(ChilResp.BodyStr);

                return false;

            }

            Boolean Success = ChilJSON.Load(ChilResp.BodyStr);
            string AccessToken = ChilJSON.StringOf("access_token");


            ChilHTTP.Dispose();
            ChilTokenReq.Dispose();
            ChilResp.Dispose();
            ChilJSON.Dispose();

            APILastAPICall = DateTime.Now;

            APIKey = AccessToken;

            return true;

        }

        private bool UnlockChilKat()
        {

            bool unlockSuccess = ChilGlob.UnlockBundle("PLWLSN.CB1072023_tU7XBBn395nV");
            if ((unlockSuccess != true))
            {
                Console.WriteLine("ChilKat: Unlock Failed");
                Console.WriteLine(ChilGlob.LastErrorText);
                return false;
            }
            else
            {
                Console.WriteLine("ChilKat: Unlock Succeeded");
                return true;
            }

        }

    }
}
// spell-checker: ignore: Chilkat, chil, chilk, resp, plwlsn
