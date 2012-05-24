using System;
using System.Windows.Media;
using System.Windows.Data;
using System.Globalization;
using System.Windows.Media.Imaging;

namespace IMS_client
{
    [ValueConversion(typeof(string), typeof(ImageSource))]
    public class StatusConverter : IValueConverter
    {

        public object Convert(object value, Type targetType,
            object parameter, CultureInfo culture)
        {
            BitmapImage myBitmapImage = new BitmapImage();

                    try
                    {
                        String toConvert = (String)value;
                        string imageName;
                        switch (toConvert)
                        {   
                            case "open":
                                imageName = "Resources/Status_Images/available.png";
                                break;
                            case "closed":
                                imageName = "Resources/Status_Images/Offline.png";
                                break;
                            default:
                                imageName = "Resources/Status_Images/Unknown.png";
                                break;
                        }
                        myBitmapImage.BeginInit();
                        myBitmapImage.UriSource = new Uri(imageName, UriKind.Relative);
                        myBitmapImage.DecodePixelWidth = 50;
                        myBitmapImage.EndInit();
                        

                        return myBitmapImage;
                    }
                    catch (Exception exception)
                    {
                        myBitmapImage = new BitmapImage();
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
