using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Media;
using System.Windows.Data;
using System.Globalization;
using System.Windows.Media.Imaging;

namespace IMS_client
{
    [ValueConversion(typeof(string), typeof(ImageSource))]
    public class Status_Converter : IValueConverter
    {

        public object Convert(object value, Type targetType,
            object parameter, CultureInfo culture)
        {
            BitmapImage myBitmapImage = new BitmapImage();

                    try
                    {
                        String toConvert = (String)value;
                        string image_name = "";
                        switch (toConvert)
                        {   
                            case "open":
                                image_name = "Resources/Status_Images/available.png";
                                break;
                            case "closed":
                                image_name = "Resources/Status_Images/Offline.png";
                                break;
                            default:
                                image_name = "Resources/Status_Images/Unknown.png";
                                break;
                        }
                        myBitmapImage.BeginInit();
                        myBitmapImage.UriSource = new Uri(image_name, UriKind.Relative);
                        myBitmapImage.DecodePixelWidth = 50;
                        myBitmapImage.EndInit();
                        

                        return myBitmapImage;
                    }
                    catch (Exception exception)
                    {
                        myBitmapImage = new System.Windows.Media.Imaging.BitmapImage();
                    }
                    return myBitmapImage;
        }

        public object ConvertBack(object value, Type targetType,
            object parameter, CultureInfo culture)
        {
            return null;
        }
    }
}
