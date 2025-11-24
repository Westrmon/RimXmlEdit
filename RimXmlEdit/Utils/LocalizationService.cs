using Avalonia;
using Avalonia.Markup.Xaml.Styling;
using Microsoft.Extensions.Logging;
using RimXmlEdit.Core.Extensions;
using System;
using System.Globalization;
using System.Linq;

namespace RimXmlEdit.Utils;

public class LocalizationService : ILocalizationService
{
    private readonly ILogger _logger;
    private const string BaseUri = "avares://RimXmlEdit/Lanuages/";

    public event EventHandler<CultureInfo> OnLanguageChanged;

    public LocalizationService()
    {
        _logger = this.Log();
    }

    public void SwitchLanguage(CultureInfo culture)
    {
        _logger.LogInformation("Switching language to {}", culture.Name);

        var app = Application.Current;
        if (app is null)
        {
            _logger.LogError("Application.Current is null. Cannot switch language.");
            return;
        }

        var currentLanguageDict = app.Resources.MergedDictionaries
            .OfType<ResourceInclude>()
            .FirstOrDefault(d => d.Source.ToString().StartsWith(BaseUri));

        if (currentLanguageDict != null)
        {
            app.Resources.MergedDictionaries.Remove(currentLanguageDict);
            _logger.LogDebug("Removed existing language resource: {}", currentLanguageDict.Source);
        }

        var newLanguageUri = new Uri($"{BaseUri}{culture.Name}.axaml");

        var newDict = new ResourceInclude(newLanguageUri)
        {
            Source = newLanguageUri
        };

        app.Resources.MergedDictionaries.Add(newDict);
        _logger.LogInformation("Successfully loaded language resource: {}", newLanguageUri);
        OnLanguageChanged?.Invoke(this, culture);
    }
}
