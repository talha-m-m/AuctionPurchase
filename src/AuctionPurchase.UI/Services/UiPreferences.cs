using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
using AuctionPurchase.Infrastructure;

namespace AuctionPurchase.UI.Services;

public sealed class UiPreferences : INotifyPropertyChanged
{
    public static UiPreferences Current { get; } = new();
    public event PropertyChangedEventHandler? PropertyChanged;
    public string DateFormat { get; private set; } = "dd/MM/yyyy";
    public bool IsDarkMode { get; private set; } = true;
    private static readonly List<WeakReference<DatePicker>> DatePickers = new();

    public static void Apply(AppSettings settings)
    {
        Current.IsDarkMode = settings.IsDarkMode;
        Current.DateFormat = settings.DateFormat is "MM/dd/yyyy" or "yyyy-MM-dd" ? settings.DateFormat : "dd/MM/yyyy";
        using var stream = typeof(UiPreferences).Assembly.GetManifestResourceStream("AuctionPurchase.UI.Assets.theme-palette.json")!;
        using var palette = JsonDocument.Parse(stream);
        foreach (var entry in palette.RootElement.EnumerateObject())
        {
            var color = (Color)ColorConverter.ConvertFromString(entry.Value.GetProperty(settings.IsDarkMode ? "Dark" : "Light").GetString()!);
            if (entry.Name.StartsWith("ThemeColor_")) Application.Current.Resources[entry.Name] = color;
            else
            {
                var brush = new SolidColorBrush(color); brush.Freeze();
                Application.Current.Resources[entry.Name] = brush;
            }
        }
        Current.PropertyChanged?.Invoke(Current, new PropertyChangedEventArgs(nameof(DateFormat)));
        Current.PropertyChanged?.Invoke(Current, new PropertyChangedEventArgs(nameof(IsDarkMode)));
        DatePickers.RemoveAll(reference => !reference.TryGetTarget(out _));
        foreach (var reference in DatePickers)
            if (reference.TryGetTarget(out var picker)) UpdateDatePicker(picker);
    }

    public static string FormatDate(DateTime value) => value.ToString(Current.DateFormat, CultureInfo.InvariantCulture);

    public static readonly DependencyProperty UseDatePreferenceProperty = DependencyProperty.RegisterAttached(
        "UseDatePreference", typeof(bool), typeof(UiPreferences), new PropertyMetadata(false, DatePreferenceAttached));
    public static void SetUseDatePreference(DependencyObject target, bool value) => target.SetValue(UseDatePreferenceProperty, value);
    public static bool GetUseDatePreference(DependencyObject target) => (bool)target.GetValue(UseDatePreferenceProperty);

    private static void DatePreferenceAttached(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        if (target is not DatePicker picker || args.NewValue is not true) return;
        DatePickers.Add(new WeakReference<DatePicker>(picker));
        picker.Loaded += (_, _) => UpdateDatePicker(picker);
        picker.SelectedDateChanged += (_, _) => UpdateDatePicker(picker);
    }

    private static void UpdateDatePicker(DatePicker picker)
    {
        picker.SetCurrentValue(FrameworkElement.LanguageProperty, XmlLanguage.GetLanguage(
            Current.DateFormat == "MM/dd/yyyy" ? "en-US" : Current.DateFormat == "yyyy-MM-dd" ? "en-CA" : "en-GB"));
        if (picker.SelectedDate is DateTime date)
        {
            picker.ApplyTemplate();
            if (picker.Template.FindName("PART_TextBox", picker) is DatePickerTextBox editor)
                editor.SetCurrentValue(TextBox.TextProperty, FormatDate(date));
        }
    }

    public static BindingBase DateBinding(string property) => new MultiBinding
    {
        Converter = new PreferredDateConverter(),
        Bindings = { new Binding(property), new Binding(nameof(DateFormat)) { Source = Current } }
    };
}

public class PreferredDateExtension : MarkupExtension
{
    public PreferredDateExtension() { }
    public PreferredDateExtension(string path) => Path = path;
    [ConstructorArgument("path")] public string Path { get; set; } = "";
    public override object ProvideValue(IServiceProvider serviceProvider) => UiPreferences.DateBinding(Path).ProvideValue(serviceProvider);
}

public class PreferredDateConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) =>
        values.Length > 1 && values[0] is DateTime date && values[1] is string format
            ? date.ToString(format, CultureInfo.InvariantCulture) : "";
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
