using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Navigation;
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using Windows.UI.Popups;
using System.Threading.Tasks;
using System.Threading;
using Windows.Storage;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.Reflection;
using System.ComponentModel;
using Newtonsoft.Json;
using System.IO;
using ImageList.Models;
using ImageList.IO;

namespace ImageList.Source
{
    public interface ILogger
    {
        void Log(string msg);
    }
    enum TransactionState
    {
        _Unknown = 0,
        _Sending = 1,
        _Receiving = 2,
        _pendingDueToSendOrReceiveFailed = 3,
        _DoneWithSuccess = 4,
        _DoneWithFailureDueToNetwork = 5,
        _DoneWithFailureDueToApplication = 6
    }
    internal class TransactionCookie
    {
        private int _transactionId;
        private TransactionState _state;
        private Object _request;
        private Object _response;
        private Dictionary<string, string> _keyResults;
        private int _totalBytesRead { get; set; }
        public ManualResetEvent _transactionCompletedEvent;
        private ILogger _logger;
        public TransactionCookie(int _transactionId, ILogger logImplement)
        {
            this._transactionId = _transactionId;
            _keyResults = new Dictionary<string, string>();
            _state = TransactionState._Unknown;
            _transactionCompletedEvent = new ManualResetEvent(false);
            _request = null;
            _response = null;
            _logger = logImplement;
        }
        public TransactionState State
        {
            get { return _state; }
            set { _state = value; }
        }
        public int TransactionId { get { return _transactionId; } }
        public Object Request
        {
            get { return _request; }
            set { _request = value; }
        }
        public Object Response
        {
            get { return _response; }
            set { _response = value; }
        }
        public int TotalBytes
        {
            get { return this._totalBytesRead; }
            set { this._totalBytesRead = value; }
        }
        //public int GetTotalBytes(int _nChunk) { this._totalBytesRead += _nChunk; return this._totalBytesRead; }
        public string GetTransactionValue(string key)
        {
            return _keyResults[key];
        }
        public void SetTransactionValue(string key, string val)
        {
            _keyResults[key] = val;
        }
        public void Log(string msg)
        {
            this._logger.Log(msg);
        }
        public bool isAlive()
        {
            return this._state != TransactionState._DoneWithFailureDueToNetwork &&
                    this._state != TransactionState._DoneWithFailureDueToApplication &&
                    this._state != TransactionState._DoneWithSuccess;
        }
        public bool isSuccess()
        {
            return this._state == TransactionState._DoneWithSuccess;
        }
    }
    public class DataSource: ILogger
    {
        public void Log(string log)
        {
        }
        public async Task<List<Image>> GetImagesHttp()
        {
            ImageList.IO.HttpIORequest request = new ImageList.IO.HttpIORequest();
            request.URI = "http://md5.jsontest.com/";
            request.Verb = ImageList.IO.HttpVerb.GET;
            request.ContentType = ImageList.IO.DataType.JSON;
            request.Authentication = ImageList.IO.AuthType.NONE;
            request.QueryString = new Dictionary<string, string>();
            request.QueryString["text"] = "example_text";

            TransactionCookie httpTransaction = new TransactionCookie(1, this);
            httpTransaction.Request = request;
            HttpIO<HttpIOHandler> httpIO = new HttpIO<HttpIOHandler>(httpTransaction);
            //httpIO.OnHttpAppException += ((msg) =>
            //{
            //    //Log(msg);
            //});
            //httpIO.OnHttpFatalException += ((msg) =>
            //{
            //    //Log(msg);
            //});
            List<Image> imgLst = await httpIO.ExecAsync<List<Image>>(request).ConfigureAwait(false);
            return imgLst;
            // JSON parsing without serialization...
            if (imgLst != null)
            {
                string md5 = "";
                bool bReadNxt = false;
                JsonTextReader reader = new JsonTextReader(new StringReader(imgLst.ToString()));
                while (reader.Read())
                {
                    /*
                     * TokenType "PropertyName" means its a JSON Key
                     * Corresponding Value represents the key Name
                     * TokenType for a JSON value is the data type like String, Boolean, Integer...
                     * Corresponding Value is the value of the JSON Value
                     */
                    if (reader.Value != null)
                    {
                        //Log(string.Format("Token: {0}, Value: {1}", reader.TokenType, reader.Value));
                        if (bReadNxt)
                        {
                            md5 = reader.Value.ToString();
                            bReadNxt = false;
                            //Log(string.Format("VALUE: {0}", md5));
                        }
                        if (reader.TokenType.ToString() == "PropertyName"
                            && reader.Value.ToString() == "md5")
                            bReadNxt = true;
                    }
                    else
                    {
                        //Log(string.Format("Token: {0}", reader.TokenType));
                    }
                }
            }
        }
        public List<Image> GetImages()
        {
            List<Image> imageList = new List<Image>();
            for (int i = 1; i <= 144; i++)
            {
                Image imageData = new Image()
                {
                    _comment = "abcd",
                    _location = new Uri(String.Format("/Images/1.jpg", i), UriKind.Relative),
                    _name = i.ToString(),
                    _dateTime = DateTime.Today.AddDays(-1)
                };

                imageList.Add(imageData);
            }

            return imageList;
        }
    }
    class HttpIOHandler : I_IOHandler
    {
        public void OnConnected(IClientService asyncClient, Object userCookie)
        {
            (userCookie as TransactionCookie).Log("OnConnected: Success...");
            (userCookie as TransactionCookie).TotalBytes = 0;
            // Disconnect happened during transaction?
            if ((userCookie as TransactionCookie).State == TransactionState._pendingDueToSendOrReceiveFailed)
            {
                // Process pending transaction if request is valid
                if ((userCookie as TransactionCookie).Request != null)
                {
                    // Process pending transaction...
                }
            }
        }
        public void OnSendChunk(IClientService nwClientSrvc, int percent, Object userCookie)
        {
            (userCookie as TransactionCookie).State = TransactionState._Sending;
            (userCookie as TransactionCookie).Log("OnSendChunk: Sent " + percent.ToString());
        }
        public void OnSendComplete(IClientService nwClientSrvc, Object userCookie)
        {
            (userCookie as TransactionCookie).Log("OnSendCompleted: Now receiving...");
        }
        public void OnReceiveChunk(IClientService nwClientSrvc, int bytesRecvd, Object userCookie)
        {
            (userCookie as TransactionCookie).State = TransactionState._Receiving;
            lock (this)
            {
                (userCookie as TransactionCookie).TotalBytes += bytesRecvd;
            }
            (userCookie as TransactionCookie).Log("OnReceiveChunk:" + bytesRecvd + "/" + (userCookie as TransactionCookie).TotalBytes);
        }
        public void OnReceiveComplete(IClientService nwClientSrvc, string msg, Object userCookie)
        {
            if (msg.Length > 0)
            {
                (userCookie as TransactionCookie).Log("OnReceiveCompleted:" + msg.Substring(0, 108));
                (userCookie as TransactionCookie).State = TransactionState._DoneWithSuccess;
                if (userCookie is TransactionCookie)
                {
                    // Transaction completed - To whom so ever it may concern
                    (userCookie as TransactionCookie)._transactionCompletedEvent.Set();
                }
            }
        }
        public void OnBusinessError(IClientService nwClientSrvc, string msg, Object userCookie)
        {
            (userCookie as TransactionCookie).Log("Business error:" + msg);
        }
        public void OnConnectionAttempts(IClientService nwClientSrvc, int iAttmpt, Object userCookie)
        {
            (userCookie as TransactionCookie).Log("Connection attempts:" + iAttmpt);
        }
        public void OnAbort(IClientService nwClientSrvc, Exception tcpExcp, Object userCookie)
        {
            string transId = "";
            if (userCookie is TransactionCookie)
                transId = (userCookie as TransactionCookie).TransactionId.ToString();
            (userCookie as TransactionCookie).Log("Aborting transaction " + transId + " with mesaage:" + tcpExcp.Message);
            (userCookie as TransactionCookie).State = TransactionState._DoneWithFailureDueToNetwork;
            // Transaction failed - To whom so ever it may concern
            (userCookie as TransactionCookie)._transactionCompletedEvent.Set();
        }
        public void OnNetworkError(IClientService nwClientSrvc, string msg, AbortSource abortSource, AbortReason abortReason, Object userCookie)
        {
            if (abortSource == AbortSource.Server)
            {
                (userCookie as TransactionCookie).Log("Network Error: Server not responding");
            }
            else if (abortSource == AbortSource.Client)
            {
                (userCookie as TransactionCookie).Log("Network Error: Your device isn't connected to a network");
            }
        }
        public void OnPending(IClientService nwClientSrvc, Object userCookie)
        {
            string transId = "";
            if (userCookie is TransactionCookie)
                transId = (userCookie as TransactionCookie).TransactionId.ToString();
            (userCookie as TransactionCookie).Log("Pending transaction:" + transId);
            (userCookie as TransactionCookie).State = TransactionState._pendingDueToSendOrReceiveFailed;
        }
    }
}
