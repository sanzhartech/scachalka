using System.Windows.Data;
using System.Windows.Markup;
using YtDlpGui.Abstractions.Localization;

namespace YtDlpGui.UI.Localization;

/// <summary>
/// XAML markup extension: <c>{loc:Str Some.Key}</c> yields the localized string and
/// updates live when the language changes. It binds to the localizer's <c>Language</c>
/// property (which is raised on every switch) and resolves the key through
/// <see cref="LocConverter"/> — avoiding indexer paths so dotted keys are safe.
/// </summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class StrExtension : MarkupExtension
{
    public StrExtension()
    {
    }

    public StrExtension(string key) => Key = key;

    [ConstructorArgument("key")]
    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new Binding(nameof(ILocalizer.Language))
        {
            Source = Loc.Current,
            Mode = BindingMode.OneWay,
            Converter = LocConverter.Instance,
            ConverterParameter = Key,
            FallbackValue = Key
        };

        return binding.ProvideValue(serviceProvider);
    }
}
