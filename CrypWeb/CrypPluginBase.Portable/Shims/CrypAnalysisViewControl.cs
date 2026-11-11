// Web port of LibSource/CrypAnalysisViewControl: the result view shared by the analyzer plugins.
//
// The desktop library defines these controls through control/data templates (Themes/Generic.xaml). The web WPF runtime
// does not instantiate control templates, so the same layout is built in code here. Public API (types, properties,
// events) is identical to the original, so the analyzers' XAML and code-behind compile unchanged.
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;

namespace CrypTool.CrypAnalysisViewControl
{
    /// <summary>A caption with a value, shown in the header and progress sections.</summary>
    public class ViewLabel : FrameworkContentElement
    {
        public static readonly DependencyProperty CaptionProperty = DependencyProperty.Register(
            "Caption", typeof(string), typeof(ViewLabel), new PropertyMetadata(""));

        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
            "Value", typeof(string), typeof(ViewLabel), new PropertyMetadata(""));

        public string Caption
        {
            get => (string)GetValue(CaptionProperty);
            set => SetValue(CaptionProperty, value);
        }

        public string Value
        {
            get => (string)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }
    }

    /// <summary>Lists view labels in two columns (kept for API compatibility; the sections build their labels directly).</summary>
    public class SectionViewLabelsControl : ItemsControl { }

    /// <summary>A bordered section with a vertical caption on the left and content on the right.</summary>
    public class SectionControl : ContentControl
    {
        public static readonly DependencyProperty SectionHeaderCaptionProperty = DependencyProperty.Register(
            "SectionHeaderCaption", typeof(string), typeof(SectionControl), new PropertyMetadata(null));

        public static readonly DependencyProperty SectionHeaderBackgroundProperty = DependencyProperty.Register(
            "SectionHeaderBackground", typeof(Brush), typeof(SectionControl), new PropertyMetadata(Brushes.Transparent));

        public static readonly DependencyProperty SectionBackgroundProperty = DependencyProperty.Register(
            "SectionBackground", typeof(Brush), typeof(SectionControl), new PropertyMetadata(Brushes.Transparent));

        public static readonly DependencyProperty IsSectionVisibleProperty = DependencyProperty.Register(
            "IsSectionVisible", typeof(bool), typeof(SectionControl), new PropertyMetadata(true, OnIsSectionVisibleChanged));

        private bool _built;

        public SectionControl()
        {
            Loaded += (sender, e) => Build();
        }

        public string SectionHeaderCaption
        {
            get => (string)GetValue(SectionHeaderCaptionProperty);
            set => SetValue(SectionHeaderCaptionProperty, value);
        }

        public Brush SectionHeaderBackground
        {
            get => (Brush)GetValue(SectionHeaderBackgroundProperty);
            set => SetValue(SectionHeaderBackgroundProperty, value);
        }

        public Brush SectionBackground
        {
            get => (Brush)GetValue(SectionBackgroundProperty);
            set => SetValue(SectionBackgroundProperty, value);
        }

        public bool IsSectionVisible
        {
            get => (bool)GetValue(IsSectionVisibleProperty);
            set => SetValue(IsSectionVisibleProperty, value);
        }

        private static void OnIsSectionVisibleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((SectionControl)d).Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>Wraps the content into the section frame (what the desktop content template does).</summary>
        private void Build()
        {
            if (_built)
            {
                return;
            }
            _built = true;
            if (SectionHeaderCaption == null || !IsSectionVisible)
            {
                Visibility = Visibility.Collapsed;
            }

            object content = Content;
            Content = null;

            var caption = new TextBlock
            {
                FontSize = 11,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                RenderTransform = new RotateTransform(270),
                RenderTransformOrigin = new Point(0.5, 0.5),
            };
            caption.SetBinding(TextBlock.TextProperty, new Binding("SectionHeaderCaption") { Source = this });

            var header = new Border { BorderThickness = new Thickness(1), BorderBrush = Brushes.Black, Child = caption };
            header.SetBinding(Border.BackgroundProperty, new Binding("SectionHeaderBackground") { Source = this });

            var grid = new Grid();
            grid.SetBinding(Panel.BackgroundProperty, new Binding("SectionBackground") { Source = this });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(26) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.Children.Add(header);

            var presenter = new ContentControl { Content = content };
            Grid.SetColumn(presenter, 1);
            grid.Children.Add(presenter);

            Content = new Border { BorderBrush = Brushes.Silver, BorderThickness = new Thickness(2, 1, 2, 1), Child = grid };
        }
    }

    /// <summary>
    /// The analyzer result view: a header section with labels, the result list (the content), a progress section
    /// with labels, and optional additional sections.
    /// </summary>
    public class CrypAnalysisViewControl : ContentControl
    {
        public static readonly DependencyProperty ResultHeaderCaptionProperty = DependencyProperty.Register(
            "ResultHeaderCaption", typeof(string), typeof(CrypAnalysisViewControl), new PropertyMetadata(null));

        public static readonly DependencyProperty ResultListCaptionProperty = DependencyProperty.Register(
            "ResultListCaption", typeof(string), typeof(CrypAnalysisViewControl), new PropertyMetadata(null));

        public static readonly DependencyProperty ResultProgressCaptionProperty = DependencyProperty.Register(
            "ResultProgressCaption", typeof(string), typeof(CrypAnalysisViewControl), new PropertyMetadata(null));

        private bool _built;

        public CrypAnalysisViewControl()
        {
            Loaded += (sender, e) => Build();
        }

        public string ResultHeaderCaption
        {
            get => (string)GetValue(ResultHeaderCaptionProperty);
            set => SetValue(ResultHeaderCaptionProperty, value);
        }

        public string ResultListCaption
        {
            get => (string)GetValue(ResultListCaptionProperty);
            set => SetValue(ResultListCaptionProperty, value);
        }

        public string ResultProgressCaption
        {
            get => (string)GetValue(ResultProgressCaptionProperty);
            set => SetValue(ResultProgressCaptionProperty, value);
        }

        public List<ViewLabel> ResultHeaderLabels { get; } = new List<ViewLabel>();

        public List<SectionControl> AdditionalHeaders { get; } = new List<SectionControl>();

        public List<ViewLabel> ResultProgressLabels { get; } = new List<ViewLabel>();

        public List<SectionControl> AdditionalSections { get; } = new List<SectionControl>();

        private void Build()
        {
            if (_built)
            {
                return;
            }
            _built = true;

            // the labels are not drawn themselves, but their bindings need the data context of this control
            foreach (ViewLabel label in ResultHeaderLabels)
            {
                AddLogicalChild(label);
            }
            foreach (ViewLabel label in ResultProgressLabels)
            {
                AddLogicalChild(label);
            }

            object resultList = Content;
            Content = null;

            var stack = new StackPanel();
            stack.Children.Add(Section("ResultHeaderCaption", "#AFFFD4C1", "#FFE56B00", LabelGrid(ResultHeaderLabels)));
            foreach (SectionControl section in AdditionalHeaders)
            {
                stack.Children.Add(section);
            }
            stack.Children.Add(Section("ResultListCaption", "#AFE2FFCE", "#FF479308", new ContentControl { Content = resultList }));
            stack.Children.Add(Section("ResultProgressCaption", "#AFE2E2E2", "#FF525252", LabelGrid(ResultProgressLabels)));
            foreach (SectionControl section in AdditionalSections)
            {
                stack.Children.Add(section);
            }

            var frame = new Grid { Width = 646 };
            frame.Children.Add(stack);
            Content = new Viewbox { Child = frame };
        }

        private SectionControl Section(string captionProperty, string background, string headerBackground, UIElement content)
        {
            var section = new SectionControl
            {
                SectionBackground = (Brush)new BrushConverter().ConvertFromString(background),
                SectionHeaderBackground = (Brush)new BrushConverter().ConvertFromString(headerBackground),
                Content = content,
            };
            section.SetBinding(SectionControl.SectionHeaderCaptionProperty, new Binding(captionProperty) { Source = this });
            return section;
        }

        /// <summary>Caption/value pairs in two columns, the values right-aligned.</summary>
        private static UIElement LabelGrid(List<ViewLabel> labels)
        {
            var grid = new UniformGrid { Columns = 2, MinHeight = 60 };
            foreach (ViewLabel label in labels)
            {
                var caption = new TextBlock { Margin = new Thickness(5) };
                caption.SetBinding(TextBlock.TextProperty, new Binding("Caption") { Source = label });

                var value = new TextBlock { Margin = new Thickness(5, 5, 20, 5), HorizontalAlignment = HorizontalAlignment.Right };
                value.SetBinding(TextBlock.TextProperty, new Binding("Value") { Source = label });
                Grid.SetColumn(value, 1);

                var row = new Grid();
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.Children.Add(caption);
                row.Children.Add(value);
                grid.Children.Add(row);
            }
            return grid;
        }
    }

    /// <summary>The list of analysis results; a double click on an entry raises <see cref="ResultItemAction"/>.</summary>
    public class CrypAnalysisResultListView : ListView
    {
        public delegate void ResultItemActionHandler(ICrypAnalysisResultListEntry item);

        public event ResultItemActionHandler ResultItemAction;

        public CrypAnalysisResultListView()
        {
            FontFamily = new FontFamily("Courier New");
            FontSize = 10;
            Height = 250;
        }

        protected override void OnItemActivated(object item)
        {
            if (item is ICrypAnalysisResultListEntry entry)
            {
                ResultItemAction?.Invoke(entry);
            }
        }
    }
}
