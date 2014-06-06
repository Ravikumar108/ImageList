using System.Collections.Generic;
using System.Linq;
using ImageList.Models;
using ImageList.Source;

namespace ImageList.ViewModels
{
    public class ImageViewModel
    {
        public List<Image> _Images
        {
            get
            {

                var Images = new DataSource().GetImages();
                return new List<Image>(Images);
            }
        }
    }
}
