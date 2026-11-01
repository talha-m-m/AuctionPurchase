using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;

namespace AuctionPurchase.UI.Services;

public static class TableLayout
{
    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
        "Enabled", typeof(bool), typeof(TableLayout), new PropertyMetadata(false, Changed));
    private static readonly DependencyProperty PendingProperty = DependencyProperty.RegisterAttached("Pending", typeof(bool), typeof(TableLayout));
    public static void SetEnabled(DependencyObject target, bool value) => target.SetValue(EnabledProperty, value);
    public static bool GetEnabled(DependencyObject target) => (bool)target.GetValue(EnabledProperty);

    private static void Changed(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        if (target is not DataGrid grid || args.NewValue is not true) return;
        grid.Loaded += (_, _) => Apply(grid);
        ((INotifyCollectionChanged)grid.Columns).CollectionChanged += (_, _) =>
        {
            if ((bool)grid.GetValue(PendingProperty)) return;
            grid.SetValue(PendingProperty, true);
            grid.Dispatcher.BeginInvoke(new Action(() => { grid.SetValue(PendingProperty, false); Apply(grid); }));
        };
    }

    public static void Apply(DataGrid grid)
    {
        grid.RowHeight = 38;
        grid.ColumnHeaderHeight = 44;
        grid.CanUserResizeColumns = true;
        ScrollViewer.SetHorizontalScrollBarVisibility(grid, ScrollBarVisibility.Auto);
        foreach (var column in grid.Columns)
        {
            string header = column.Header?.ToString() ?? "";
            string key = header.ToLowerInvariant();
            bool numeric = key.Contains("bag") || key.Contains("kg") || key.Contains("weight") ||
                key.Contains("price") || key.Contains("value") || key.Contains("entries");
            bool action = key.Contains("action");
            double minimum = Math.Max(76, header.Length * 6.8 + 26);
            double weight = 1;
            if (key.Contains("garden")) { minimum = 150; weight = 1.8; }
            else if (key.Contains("account")) { minimum = 140; weight = 1.6; }
            else if (key.Contains("brand")) { minimum = 130; weight = 1.5; }
            else if (key.Contains("broker")) { minimum = 100; weight = 1.1; }
            else if (key.Contains("invoice")) { minimum = 100; weight = 1.1; }
            else if (key.Contains("date")) minimum = 112;
            else if (key.Contains("value")) minimum = Math.Max(minimum, 132);
            else if (key.Contains("kg") || key.Contains("price") || key.Contains("weight")) minimum = Math.Max(minimum, 106);
            else if (action) minimum = 142;
            column.MinWidth = minimum;
            column.MaxWidth = action || numeric || key.Contains("date") ? Math.Max(minimum, 210) : 360;
            column.Width = action || numeric || key.Contains("date")
                ? new DataGridLength(minimum) : new DataGridLength(weight, DataGridLengthUnitType.Star);
            column.HeaderStyle = new Style(typeof(DataGridColumnHeader), grid.ColumnHeaderStyle ??
                Application.Current.TryFindResource(typeof(DataGridColumnHeader)) as Style)
            {
                Setters =
                {
                    new Setter(Control.HorizontalContentAlignmentProperty, numeric ? HorizontalAlignment.Right : HorizontalAlignment.Left),
                    new Setter(Control.PaddingProperty, new Thickness(10, 8, 10, 8))
                }
            };
            if (column is DataGridTextColumn text)
            {
                text.ElementStyle = new Style(typeof(TextBlock))
                {
                    Setters =
                    {
                        new Setter(FrameworkElement.MarginProperty, new Thickness(10, 0, 10, 0)),
                        new Setter(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center),
                        new Setter(TextBlock.TextAlignmentProperty, numeric ? TextAlignment.Right : TextAlignment.Left),
                        new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis),
                        new Setter(FrameworkElement.ToolTipProperty, new Binding("Text") { RelativeSource = RelativeSource.Self })
                    }
                };
                if (text.Binding is MultiBinding multi && multi.Bindings.FirstOrDefault() is Binding binding && string.IsNullOrWhiteSpace(text.SortMemberPath))
                    text.SortMemberPath = binding.Path.Path;
            }
        }
    }
}
