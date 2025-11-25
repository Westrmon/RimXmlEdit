using System;
using System.Globalization;

namespace RimXmlEdit.Service;

public interface ILocalizationService
{
    event EventHandler<CultureInfo> OnLanguageChanged;

    void SwitchLanguage(CultureInfo culture);
}
