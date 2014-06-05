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

namespace ImageList.Source
{
    public static class DataSource
    {
        public static async Task<List<Image>> GetImagesHttp()
        {
            HttpIORequest request = new HttpIORequest();
            request.URI = "http://md5.jsontest.com/";
            request.Verb = HttpVerb.GET;
            request.ContentType = DataType.JSON;
            request.Authentication = AuthType.NONE;
            request.QueryString = new Dictionary<string, string>();
            request.QueryString["text"] = "example_text";

            HttpIO httpIO = new HttpIO();
            httpIO.OnHttpAppException += ((msg) =>
            {
                //Log(msg);
            });
            httpIO.OnHttpFatalException += ((msg) =>
            {
                //Log(msg);
            });
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
        public static List<Image> GetImages()
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
}
