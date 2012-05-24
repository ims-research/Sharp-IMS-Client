using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace IMS_client
{
    public class TitleBar : Control
    {
        ImageButton _closeButton;
        ImageButton _maxButton;
        ImageButton _minButton;

        public TitleBar()
        {
            MouseLeftButtonDown += OnTitleBarLeftButtonDown;
            MouseDoubleClick += TitleBarMouseDoubleClick;
            Loaded += TitleBarLoaded;
        }

        void TitleBarMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            MaxButtonClick(sender, e);
        }

        void TitleBarLoaded(object sender, RoutedEventArgs e)
        {
            _closeButton = (ImageButton)Template.FindName("CloseButton", this);
            _minButton = (ImageButton)Template.FindName("MinButton", this);
            _maxButton = (ImageButton)Template.FindName("MaxButton", this);

            _closeButton.Click += CloseButtonClick;
            _minButton.Click += MinButtonClick;
            _maxButton.Click += MaxButtonClick;
        }


        static TitleBar()
        {
            //This OverrideMetadata call tells the system that this element wants to provide a style that is different than its base class.
            //This style is defined in themes\generic.xaml
            DefaultStyleKeyProperty.OverrideMetadata(typeof(TitleBar), new FrameworkPropertyMetadata(typeof(TitleBar)));
        }

        #region event handlers

        void OnTitleBarLeftButtonDown(object sender, MouseEventArgs e)
        {
            Window window = TemplatedParent as Window;
            if (window != null)
            {
                window.DragMove();
            }
        }

        void CloseButtonClick(object sender, RoutedEventArgs e)
        {
            Window window = TemplatedParent as Window;
            if (window != null)
            {
                window.Close();
            }
        }

        void MinButtonClick(object sender, RoutedEventArgs e)
        {
            Window window = TemplatedParent as Window;
            if (window != null)
            {
                window.WindowState = WindowState.Minimized;
            }
        }

        void MaxButtonClick(object sender, RoutedEventArgs e)
        {
            Window window = TemplatedParent as Window;
            if (window != null)
            {
                if (window.WindowState == WindowState.Maximized)
                {
                    _maxButton.ImageDown = "Images/maxpressed_n.png";
                    _maxButton.ImageNormal = "Images/max_n.png";
                    _maxButton.ImageOver = "Images/maxhot_n.png";

                    window.WindowState = WindowState.Normal;
                }
                else
                {
                    _maxButton.ImageDown = "Images/normalpress.png";
                    _maxButton.ImageNormal = "Images/normal.png";
                    _maxButton.ImageOver = "Images/normalhot.png";

                    window.WindowState = WindowState.Maximized;
                }
            }
        }

        #endregion

        #region properties

        public string Title
        {
            get { return (string)GetValue(TitleProperty); }
            set { SetValue(TitleProperty, value); }
        }

        public ImageSource Icon
        {
            get { return (ImageSource)GetValue(IconProperty); }
            set { SetValue(IconProperty, value); }
        }


        #endregion

        #region dependency properties

        public static readonly DependencyProperty TitleProperty =
           DependencyProperty.Register(
               "Title", typeof(string), typeof(TitleBar));

        public static readonly DependencyProperty IconProperty =
           DependencyProperty.Register(
               "Icon", typeof(ImageSource), typeof(TitleBar));

        #endregion
    }
}
