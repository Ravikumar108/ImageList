using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using System.Threading;

namespace ImageList.IO
{
    public enum AbortSource
    {
        Unknown = 0,
        Client = 1,
        Server = 2
    }
    public enum AbortReason
    {
        NotSpecified = 0
    }
    /*
     *  IClientService => Define needed behavior for any Network Client.
     */
    public interface IClientService : IDisposable
    {
        Task Reattempt();
        Task<T> ExecAsync<T>(HttpIORequest request) where T : new();
    }
    /*
     *  I_IOHandler => A general puropose Event sinking for business/domain layer.
     */
    public interface I_IOHandler
    {
        void OnConnected(IClientService nwClientSrvc, Object _userCookie);
        void OnConnectionAttempts(IClientService nwClientSrvc, int iAttmpt, Object _userCookie);

        void OnSendChunk(IClientService nwClientSrvc, int percent, Object _userCookie);
        void OnSendComplete(IClientService nwClientSrvc, Object _userCookie);

        void OnReceiveChunk(IClientService nwClientSrvc, int bytesRecvd, Object _userCookie);
        void OnReceiveComplete(IClientService nwClientSrvc, string msg, Object _userCookie);

        void OnBusinessError(IClientService nwClientSrvc, string msg, Object _userCookie);
        void OnNetworkError(IClientService nwClientSrvc, string msg, AbortSource abortSource, AbortReason abortReason, Object _userCookie);
        //void OnNetworkTimeOut(IClientService nwClientSrvc, Object _userCookie);

        void OnPending(IClientService nwClientSrvc, Object _userCookie);
        void OnAbort(IClientService nwClientSrvc, Exception tcpExcp, Object _userCookie);
    }
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
    public class BasicAuth
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
    public class HttpIORequest
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
    public class HttpIOResponse
    {
        public int StatusCode { get; set; }
    }
    public class HttpIO<T> : IDisposable, IClientService where T : I_IOHandler
    {
        private Object _consumerCookie;
        private T _handlerObject;
        private int _reconnectTried;
        private int _reconnectionAttempts;
        private TimeSpan _delayBetweenReconnectionAttempts;

        public HttpIO(Object consumerCookie, int reconnectionAttempts = 10, int delayBetweenReconnectionAttemptsInSec = 4)
        {
            this._consumerCookie = consumerCookie;
            this._reconnectionAttempts = reconnectionAttempts;
            this._delayBetweenReconnectionAttempts = new TimeSpan(0, 0, 
                                                        delayBetweenReconnectionAttemptsInSec);
            // Handler factory
            _handlerObject = (T) Activator.CreateInstance(typeof(T));
        }
        public async Task Reattempt()
        {
            // Reattempt logic...
            await Task.Delay(_delayBetweenReconnectionAttempts);
            Interlocked.Increment(ref _reconnectTried);
        }
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
        public async Task<U> ExecAsync<U>(HttpIORequest request) where U : new()
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
                    return await ExecAsyncGet<U>(request, url).ConfigureAwait(false);
            }
            return default(U);
        }
        private async Task<U> ExecAsyncGet<U>(HttpIORequest request, string url) where U : new()
        {
            U deserializedObj = default(U);
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
                                () => JsonConvert.DeserializeObject<U>(strRspns)
                            ).ConfigureAwait(false);
                    }
                    else
                    {

                        string msg = "Http Server threw exception";
                        if (response != null)
                            msg += string.Format(": HTTP {0}\n{1}",
                                response.StatusCode, Convert.ToString(response));
                        (_handlerObject as I_IOHandler).OnBusinessError(this, msg, _consumerCookie);
                    }
                }
                catch (Exception ex)
                {
                    // Most likely Severe System or Networkr related exception
                    string msg = "Http fatal error: " + ex.Message;
                    (_handlerObject as I_IOHandler).OnNetworkError(this, msg, AbortSource.Server,
                                                        AbortReason.NotSpecified, _consumerCookie);
                    //throw new HttpIOException("Http operation failed with message:" + ex.Message);
                }
            }
            return deserializedObj;
        }
        public void Dispose()
        {
        }
    }
}
