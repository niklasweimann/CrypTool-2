using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Animation;
using System.Windows.Rendering;

namespace System.Windows.Hosting
{
    /// <summary>
    /// Shows one element tree (a plugin presentation) in the browser: produces its HTML whenever something changed
    /// and delivers the input events that the browser reports back.
    /// </summary>
    public sealed class PresentationHost
    {
        private static int _hostCount;

        private readonly ConditionalWeakTable<UIElement, object> _ids = new ConditionalWeakTable<UIElement, object>();
        private readonly Dictionary<int, WeakReference<UIElement>> _elements = new Dictionary<int, WeakReference<UIElement>>();
        private readonly HtmlRenderer _renderer;
        private int _nextId = 1;
        private long _renderedVersion = -1;
        private string _renderedHtml;

        public PresentationHost(UIElement root)
        {
            Root = root ?? throw new ArgumentNullException(nameof(root));
            _renderer = new HtmlRenderer(IdOf, "h" + (++_hostCount));
        }

        public UIElement Root { get; }

        /// <summary>
        /// Makes <c>IsVisible</c> of the presentation false for its plugin (it is still drawn). Needed for plugins
        /// that block their execution while the presentation is visible, waiting for the user to step through it.
        /// </summary>
        public bool ReportsHidden
        {
            get => Root.ReportsHidden;
            set => Root.ReportsHidden = value;
        }

        /// <summary>Name under which the rendered HTML travels among the presentation values of a component.</summary>
        public const string HtmlKey = "$html";

        /// <summary>True when <paramref name="element"/> is a real element tree (generated stand-ins have no children).</summary>
        public static bool HasContent(UIElement element)
        {
            foreach (UIElement child in element.ChildElements)
            {
                return true;
            }
            return false;
        }

        /// <summary>The HTML of the tree as it is now.</summary>
        public string Render()
        {
            (Root as FrameworkElement)?.RaiseLoaded();
            _renderedVersion = RenderVersion.Current;
            _renderedHtml = _renderer.Render(Root);
            return _renderedHtml;
        }

        /// <summary>The HTML if it differs from what the last call returned; otherwise null.</summary>
        public string RenderIfChanged()
        {
            if (_renderedHtml != null && _renderedVersion == RenderVersion.Current)
            {
                return null;
            }
            string previous = _renderedHtml;
            string html = Render();
            return html == previous ? null : html;
        }

        /// <summary>
        /// Delivers an input event from the browser. <paramref name="kind"/> is "click", "toggle", "text", "range" or
        /// "select"; <paramref name="value"/> carries the text, number or index.
        /// </summary>
        public void Dispatch(int elementId, string kind, string value, double x = 0, double y = 0)
        {
            if (!_elements.TryGetValue(elementId, out WeakReference<UIElement> reference) || !reference.TryGetTarget(out UIElement element))
            {
                return;
            }

            switch (kind)
            {
                case "click":
                    if (element is TabItem tab && tab.ParentElement is TabControl tabs)
                    {
                        tabs.SelectedItem = tab;
                    }
                    else
                    {
                        int clicks = int.TryParse(value, out int count) ? Math.Max(1, count) : 1;
                        element.RaiseClick(new Point(x, y), clicks);
                    }
                    break;

                case "toggle":
                    (element as Expander)?.Toggle();
                    break;

                case "text":
                    (element as TextBox)?.SetTextFromUser(value ?? "");
                    (element as PasswordBox)?.SetPasswordFromUser(value ?? "");
                    break;

                case "range":
                    if (element is RangeBase range && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double number))
                    {
                        range.SetValueFromUser(number);
                    }
                    break;

                case "select":
                    if (element is Selector selector && int.TryParse(value, out int index))
                    {
                        selector.SelectFromUser(index);
                    }
                    break;
            }
            RenderVersion.Bump();
        }

        /// <summary>Sizes measured by the browser, so that ActualWidth/ActualHeight and SizeChanged work.</summary>
        public void ReportSize(int elementId, double width, double height)
        {
            if (_elements.TryGetValue(elementId, out WeakReference<UIElement> reference) && reference.TryGetTarget(out UIElement element))
            {
                (element as FrameworkElement)?.ReportSize(new Size(width, height));
            }
        }

        private int IdOf(UIElement element)
        {
            if (_ids.TryGetValue(element, out object boxed))
            {
                return (int)boxed;
            }
            int id = _nextId++;
            _ids.Add(element, id);
            _elements[id] = new WeakReference<UIElement>(element);
            return id;
        }
    }

    /// <summary>Entry points for the host process (the engine worker).</summary>
    public static class WpfRuntime
    {
        /// <summary>Advances animations and timers; call it regularly (about every 50 ms) while presentations are shown.</summary>
        public static void Tick() => AnimationScheduler.Tick();

        /// <summary>True while animations or timers are running.</summary>
        public static bool HasPendingWork => AnimationScheduler.HasWork;

        /// <summary>Stops all animations and timers (when the workspace stops).</summary>
        public static void Reset() => AnimationScheduler.Reset();
    }
}
