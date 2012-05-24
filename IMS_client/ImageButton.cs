using System.Windows;


namespace IMS_client
{
    public class ImageButton : System.Windows.Controls.Button // Taken from example by Alex Yakhnin
    {
        static ImageButton()
        {
            //This OverrideMetadata call tells the system that this element wants to provide a style that is different than its base class.

            DefaultStyleKeyProperty.OverrideMetadata(typeof(ImageButton), new FrameworkPropertyMetadata(typeof(ImageButton)));
        }


        #region properties

        public string ImageOver
        {
            get { return (string)GetValue(ImageOverProperty); }
            set { SetValue(ImageOverProperty, value); }
        }

        public string ImageNormal
        {
            get { return (string)GetValue(ImageNormalProperty); }
            set { SetValue(ImageNormalProperty, value); }
        }

        public string ImageDown
        {
            get { return (string)GetValue(ImageDownProperty); }
            set { SetValue(ImageDownProperty, value); }
        }

        public string Text
        {
            get { return (string)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }


        #endregion

        #region dependency properties

        public static readonly DependencyProperty ImageNormalProperty =
           DependencyProperty.Register(
               "ImageNormal", typeof(string), typeof(ImageButton));


        public static readonly DependencyProperty ImageOverProperty =
          DependencyProperty.Register(
              "ImageOver", typeof(string), typeof(ImageButton));

        public static readonly DependencyProperty ImageDownProperty =
        DependencyProperty.Register(
            "ImageDown", typeof(string), typeof(ImageButton));

        public static readonly DependencyProperty TextProperty =
           DependencyProperty.Register(
               "Text", typeof(string), typeof(ImageButton));

        #endregion

    }
}
