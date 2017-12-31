using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace Supremacy.Client.Controls
{
    internal class NonLogicalAdornerDecorator : AdornerDecorator
    {
        private UIElement _child;

        public override UIElement Child
        {
            get
            {
                return _child;
            }
            set
            {
                if (_child == value)
                    return;

                RemoveVisualChild(_child);
                RemoveVisualChild(AdornerLayer);

                _child = value;

                if (value != null)
                {
                    AddVisualChild(value);
                    AddVisualChild(AdornerLayer);
                }

                InvalidateMeasure();
            }
        }

        protected override Visual GetVisualChild(int index)
        {
            return _child;
        }

        protected override int VisualChildrenCount
        {
            get { return _child == null ? 0 : 1; }
        }

/*
        protected override void OnRender(DrawingContext drawingContext)
        {
            drawingContext.DrawRectangle(
                new SolidColorBrush(Color.FromArgb(0x3F, 0x1E, 0x90, 0xFF)),
                null,
                new Rect(this.RenderSize));
        }
*/
    }
}