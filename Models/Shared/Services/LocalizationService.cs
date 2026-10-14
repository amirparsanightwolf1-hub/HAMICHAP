using System.Globalization;
using System.Resources;
using HamiChap_MES_ERP.Resources;

namespace HamiChap_MES_ERP.Services;

public class LocalizationService
{
    private readonly ResourceManager _resourceManager;

    public LocalizationService()
    {
        _resourceManager = new ResourceManager(
            "HamiChap_MES_ERP.Resources.SharedResource",
            typeof(SharedResource).Assembly);
    }

    public string this[string key]
    {
        get
        {
            return _resourceManager.GetString(
                key,
                CultureInfo.CurrentUICulture) ?? key;
        }
    }

    public string CurrentLanguage =>
        CultureInfo.CurrentUICulture.Name.StartsWith(
            "fa",
            StringComparison.OrdinalIgnoreCase)
            ? "fa"
            : "en";

    public bool IsPersian =>
        CurrentLanguage == "fa";

    //Enum local//

    public string TranslateEnum<TEnum>(TEnum value)
    where TEnum : struct, Enum
    {
        return this[value.ToString()!];
    }
}
