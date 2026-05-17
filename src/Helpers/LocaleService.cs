// READU.md — Licensed under the MIT License.

using System;
using System.Globalization;
using ReadU.Models;

namespace ReadU.Helpers;

public static class LocaleService
{
    public static string GetPromptLanguageName(string configuredLanguage)
    {
        if (!string.IsNullOrWhiteSpace(configuredLanguage)
            && !string.Equals(configuredLanguage, AiConfig.UseSystemLanguage, StringComparison.OrdinalIgnoreCase))
        {
            return configuredLanguage switch
            {
                "en" => "English",
                "zh-Hant" => "Traditional Chinese",
                "zh-Hans" => "Simplified Chinese",
                "ja" => "Japanese",
                "ko" => "Korean",
                _ => "English"
            };
        }

        var culture = CultureInfo.CurrentUICulture;

        return culture.Name switch
        {
            "zh-TW" or "zh-HK" or "zh-MO" or "zh-Hant" => "Traditional Chinese",
            "zh-CN" or "zh-SG" or "zh-Hans" => "Simplified Chinese",
            "ja-JP" or "ja" => "Japanese",
            "ko-KR" or "ko" => "Korean",
            _ => culture.TwoLetterISOLanguageName switch
            {
                "zh" => "Traditional Chinese",
                "ja" => "Japanese",
                "ko" => "Korean",
                _ => culture.EnglishName
            }
        };
    }
}