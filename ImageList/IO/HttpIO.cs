using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net;
using System.Net.Http.Headers;
using Newtonsoft.Json;

namespace ImageList
{
    public enum HttpVerb
    {
        GET = 0,
        POST,
        PUT,
        DELETE
    }
    public enum DataType
    {
        JSON = 0,
        XML
    }
    public enum AuthType
    {
        NONE = 0,
        BASIC,
        OAUTH2
    }
    class BasicAuth
    {
        private string _username;
        private string _password;
        BasicAuth(string user, string pwd)
        {
            _username = user;
            _password = pwd;
        }
        public string Username { get; set; }
        public string Password { get; set; }
    }
    public class HttpIOException : Exception
    {
        public HttpIOException(string notification) : base(notification) { }
    }
    class HttpIORequest
    {
        public string URI { get; set; }
        //public string BaseURI { get; set; }
        //public string Path { get; set; }
        public Dictionary<string, string> QueryString { get; set; }
        public HttpVerb Verb { get; set; }
        public DataType ContentType { get; set; }
        public AuthType Authentication { get; set; }
        public BasicAuth BasicAuthCreds { get; set; }
        public string AuthToken { get; set; }
        public int Timeout { get; set; }
    }
    class HttpIOResponse
    {
        public int StatusCode { get; set; }
    }
    public delegate void HttpAppException(string msg);
    public delegate void HttpFatalException(string msg);
    class HttpIO
    {
        public event HttpAppException OnHttpAppException;
        public event HttpFatalException OnHttpFatalException;

        private string ToQueryString(Dictionary<string, string> pairs)
        {
            if (pairs == null || 0 == pairs.Count())
                return "";
            List<string> qryString = new List<string>();
            foreach (var pair in pairs)
            {
                qryString.Add(string.Format("{0}={1}", HttpUtility.UrlEncode(pair.Key), HttpUtility.UrlEncode(pair.Value)));
            }
            return string.Join("&", qryString.ToArray());
        }
        public async Task<T> ExecAsync<T>(HttpIORequest request) where T : new()
        {
            string url = request.URI;
            if (request.QueryString != null)
            {
                var builder = new UriBuilder(request.URI);
                builder.Port = -1; // Use default
                builder.Query = ToQueryString(request.QueryString);
                url = builder.ToString();
            }
            switch (request.Verb)
            {
                case HttpVerb.GET:
                    return await ExecAsyncGet<T>(request, url).ConfigureAwait(false);
            }
            return default(T);
        }
        private async Task<T> ExecAsyncGet<T>(HttpIORequest request, string url) where T : new()
        {
            T deserializedObj = default(T);
            using (var client = new HttpClient())
            {
                HttpResponseMessage response = null;
                client.Timeout = new TimeSpan(0, 0, 9000);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                if (request.Authentication == AuthType.BASIC)
                {
                    string sAuth = request.BasicAuthCreds.Username + ":" + request.BasicAuthCreds.Password;
                    var byteArray = Encoding.UTF8.GetBytes(sAuth);
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
                }
                else if (request.Authentication == AuthType.OAUTH2)
                {
                    /*
                     *  To generate oauth2 header following items are needed
                     *  1. Consumer key
                     *  2. Consumer secret
                     *  3. Token (optional)
                     *  4. Token secret (optional)
                     */
                    //client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("OAuth", authHeader);
                }
                try
                {
                    response = await client.GetAsync(url).ConfigureAwait(false);
                    if (response.IsSuccessStatusCode)
                    {
                        string strRspns = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        deserializedObj = await Task.Run
                            (
                                () => JsonConvert.DeserializeObject<T>(strRspns)
                            ).ConfigureAwait(false);
                    }
                    else
                    {
                        if (OnHttpAppException != null)
                        {
                            string msg = "Http Server threw exception";
                            if (response != null)
                                msg += string.Format(": HTTP {0}\n{1}",
                                    response.StatusCode, Convert.ToString(response));
                            OnHttpAppException(msg);
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Most likely Severe System or Networkr related exception
                    if (OnHttpFatalException != null)
                    {
                        string msg = "Http fatal error: " + ex.Message;
                        OnHttpFatalException(msg);
                    }
                    //throw new HttpIOException("Http operation failed with message:" + ex.Message);
                }
            }
            return deserializedObj;
        }
    }
}
