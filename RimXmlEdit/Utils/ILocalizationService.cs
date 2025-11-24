using System;
using System.Globalization;

namespace RimXmlEdit.Utils;

public interface ILocalizationService
{
    event EventHandler<CultureInfo> OnLanguageChanged;

    void SwitchLanguage(CultureInfo culture);
}
