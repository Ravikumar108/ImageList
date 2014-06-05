// (c) Copyright Microsoft Corporation.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System.Windows;
using Microsoft.Phone.Controls;
using ImageList.ViewModels;

namespace ImageList
{
    public partial class MainPage : PhoneApplicationPage
    {
        public MainPage()
        {
            InitializeComponent();
            this.Loaded += new RoutedEventHandler(AppStart);
        }

        private void AppStart(object sender, RoutedEventArgs e)
        {
            var viewModel = new ImageViewModel();
            DataContext = viewModel;
        }
    }
}