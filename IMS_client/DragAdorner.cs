using System.Windows.Documents;
using System.Windows.Shapes;
using System.Windows;
using System.Windows.Media;

namespace IMS_client
{
  
        /// <summary>
        /// Displays a semi transparent preview of an element being dragged
        /// </summary>
        public class DragAdorner : Adorner
        {
            private readonly Rectangle _visual;
            private Point _location;

            /// <summary>
            /// Initializes a new instance of DragAdorner
            /// </summary>
            /// <param name="element"></param>
            public DragAdorner(UIElement element)
                : base(element)
            {
                VisualBrush brush = new VisualBrush(element);
                _visual = new Rectangle
                              {
                                  Width = element.RenderSize.Width,
                                  Height = element.RenderSize.Height,
                                  Fill = brush,
                                  Opacity = 0.6
                              };

            }

            #region Method overrides

            /// <summary>
            /// Measures the contents of the adorner
            /// </summary>
            /// <param name="constraint"></param>
            /// <returns></returns>
            protected override Size MeasureOverride(Size constraint)
            {
                _visual.Measure(constraint);
                return _visual.DesiredSize;
            }

            /// <summary>
            /// Arranges the contents of the adorner
            /// </summary>
            /// <param name="finalSize"></param>
            /// <returns></returns>
            protected override Size ArrangeOverride(Size finalSize)
            {
                _visual.Arrange(new Rect(finalSize));
                return finalSize;
            }

            /// <summary>
            /// Gets the visual child to display
            /// </summary>
            /// <param name="index"></param>
            /// <returns></returns>
            protected override Visual GetVisualChild(int index)
            {
                return _visual;
            }

            /// <summary>
            /// Retrieves the transform required for displaying the adorner
            /// </summary>
            /// <param name="transform"></param>
            /// <returns></returns>
            public override GeneralTransform GetDesiredTransform(GeneralTransform transform)
            {
                var result = new GeneralTransformGroup();

                result.Children.Add(base.GetDesiredTransform(transform));
                result.Children.Add(new TranslateTransform(_location.X, _location.Y));

                return result;
            }

            #endregion

            #region Public properties

            /// <summary>
            /// Gets the number of visual childs displayed
            /// </summary>
            protected override int VisualChildrenCount
            {
                get { return 1; }
            }

            /// <summary>
            /// Gets or sets the location of the adorner
            /// </summary>
            public Point Location
            {
                get { return _location; }
                set
                {
                    _location = value;
                    UpdateLocation();
                }
            }

            #endregion

            #region Private methods

            /// <summary>
            /// Updates the location of the adorner
            /// </summary>
            private void UpdateLocation()
            {
                AdornerLayer layer = (AdornerLayer)Parent;
                layer.Update(AdornedElement);
            }

            #endregion
        }
    }
