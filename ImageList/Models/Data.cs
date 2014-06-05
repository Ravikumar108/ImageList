using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
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


namespace ImageList.Models
{
    public class Image
    {
        public string _name { get; set; }
        public DateTime _dateTime { get; set; }
        public Uri _location { get; set; }
        public string _comment { get; set; }
    }

    public class ImageJason
    {
        public List<Image> _imageListFromHttp { get; set; }
    }
}