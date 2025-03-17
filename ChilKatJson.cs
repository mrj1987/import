using Chilkat;
using StandardUtils;
using StringBuilder = System.Text.StringBuilder;

namespace GCRealTime
{
    // Ensure the class name matches the file name exactly
    public class ChilKatJson
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

            int Attempts = 1;

            while (Attempts < 6)
            {

                Console.Write("ConGet:");

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
                JSONResponse.Append(ChilRest.FullRequestNoBody("GET", APIURI));

                if (ChilRest.ResponseStatusCode == 200 && JSONResponse.ToString().Contains("gateway.timeout") == false && JSONResponse.ToString().Contains("service.unavailable") == false)
                {
                    break;
                }
                else
                {
                    Attempts++;

                    switch (ChilRest.ResponseStatusCode)
                    {
                        case 429:
                            Console.WriteLine("ChilKat GET : Too Many Requests: Shifting API Key");
                            GetAuthAPIKey();
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
            if (success != true)
            {
                Console.WriteLine(ChilRest.LastErrorText);
                return "Connect Error";
            }

            int Attempts = 1;

            while (Attempts < 6)
            {

                Console.Write("ConPost:");
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
                JSONResponse.Append(ChilRest.FullRequestString("POST", APIURI, Body));

                if (ChilRest.ResponseStatusCode == 200 && JSONResponse.ToString().Contains("gateway.timeout") == false && JSONResponse.ToString().Contains("service.unavailable") == false)
                {
                    break;
                }
                else
                {
                    Attempts++;

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
                            Console.WriteLine("ChilKat POST : Too Many Requests: Pause & Shifting API Key");
                            System.Threading.Thread.Sleep(60000);
                            GetAuthAPIKey();
                            JSONResponse.Clear();
                            JSONResponse.Append("{}");
                            break;
                        case 503:
                        //TODO Resolve the 504 handling 
                        case 504:
                        default:
                            JSONResponse.Clear();
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

    // Move extension method inside the GCRealTime namespace
    // and update it to match the ChilKatJson class name
    public static class ChilKatJsonExtensions
    {
        public static string ReturnJson(this ChilKatJson chilkat, string url, string jsonBody = "", string method = "POST")
        {
            Chilkat.Rest ChilRest = new Chilkat.Rest();
            Chilkat.Socket ChilSocket = new Chilkat.Socket();

            // Configure proxy if needed
            if (chilkat.APIProxyAddress != null)
            {
                ChilSocket.HttpProxyHostname = chilkat.APIProxyAddress;
                ChilSocket.HttpProxyPort = chilkat.APIProxyPort;

                if (chilkat.APIProxyUserName != null)
                {
                    ChilSocket.HttpProxyUsername = chilkat.APIProxyUserName;
                    ChilSocket.HttpProxyPassword = chilkat.APIProxyPassword;
                }
            }

            bool AutoReconnect = true;
            bool TLS = true;
            int Port = 443;
            int MaxWaitMs = 5000;

            bool success = ChilSocket.Connect(chilkat.APIBaseURL, Port, TLS, MaxWaitMs);
            if (success != true)
            {
                Console.WriteLine("ChilKat API : Connect Error {0}", ChilSocket.LastErrorText);
                return null;
            }

            success = ChilRest.UseConnection(ChilSocket, AutoReconnect);
            if (success != true)
            {
                Console.WriteLine("ChilKat API : Connect Error {0}", ChilRest.LastErrorText);
                return null;
            }

            StringBuilder JSONResponse = new StringBuilder();

            int Attempts = 1;
            while (Attempts < 6)
            {
                // Check if API key needs refresh
                if ((chilkat.APILastAPICall.AddSeconds(86400) - DateTime.Now).TotalSeconds < 43200)
                {
                    Console.WriteLine("ChilKat API : Reset API Needed");
                    if (chilkat.GetAuthAPIKey() == false)
                    {
                        throw new Exception("ChilKat API : Cannot Get New API Key");
                    }
                }
                
                // Set authorization header
                Chilkat.StringBuilder AuthHeaderVal = new Chilkat.StringBuilder();
                AuthHeaderVal.Append("Bearer ");
                AuthHeaderVal.Append(chilkat.APIKey);
                ChilRest.Authorization = AuthHeaderVal.GetAsString();

                ChilRest.AddHeader("Content-Type", "application/json; charset=UTF-8");
                
                string response = "";
                
                // Handle different HTTP methods
                switch (method.ToUpper())
                {
                    case "GET":
                        response = ChilRest.FullRequestNoBody("GET", url);
                        break;
                    case "POST":
                        response = ChilRest.FullRequestString("POST", url, jsonBody);
                        break;
                    case "DELETE":
                        response = ChilRest.FullRequestNoBody("DELETE", url);
                        break;
                    default:
                        throw new ArgumentException($"Unsupported HTTP method: {method}");
                }
                
                JSONResponse.Append(response);

                if (ChilRest.ResponseStatusCode == 200 && 
                    !response.Contains("gateway.timeout") && 
                    !response.Contains("service.unavailable"))
                {
                    break;
                }
                else
                {
                    Attempts++;

                    switch (ChilRest.ResponseStatusCode)
                    {
                        case 400:
                            throw new System.Net.WebException(
                                string.Format(
                                    "Received HTTP 400 Bad Request from {0} {1}",
                                    method, url
                                )
                            );
                        case 404:
                            if (method.ToUpper() == "GET" && url.Contains("/channels/"))
                            {
                                Console.WriteLine("ChilKat API : Channel not found (404). This could be because the channel has expired, the API key is invalid, or the 20 channel limit has been reached.");
                            }
                            else
                            {
                                Console.WriteLine("ChilKat API : Resource not found (404): {0}", url);
                            }
                            JSONResponse.Clear();
                            JSONResponse.Append("{}");
                            Attempts = 7; // Stop retrying
                            break;
                        case 429:
                            Console.WriteLine("ChilKat API : Too Many Requests: Pause & Shifting API Key");
                            Thread.Sleep(60000);
                            chilkat.GetAuthAPIKey();
                            JSONResponse.Clear();
                            JSONResponse.Append("{}");
                            break;
                        case 503:
                        case 504:
                        default:
                            JSONResponse.Clear();
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
    }
}
// spell-checker: ignore: Chilkat, chil, chilk, resp, plwlsn
