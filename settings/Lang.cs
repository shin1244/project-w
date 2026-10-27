using Godot;
using System;
using System.Globalization;

// Interface text in the player's language. Code keeps writing Korean, the source language, and
// passes it through here; locale/en.po holds the English for each Korean msgid. A msgid missing
// from en.po shows in Korean, so a screen not translated yet stays readable (docs/settings.md).
// Controls do not translate their own text (Settings turns auto-translation off), so a player
// or room named like a known phrase stays as typed.
public static class Lang
{
    public const string English = "en", Korean = "ko", Default = English;
    // In the order the settings list shows them, each in its own language.
    public static readonly (string Code, string Name)[] Supported = { (English, "English"), (Korean, "한국어") };

    public static bool IsSupported(string code) => Array.Exists(Supported, language => language.Code == code);

    public static string T(string text) => TranslationServer.Translate(text);

    // The same Korean that means two things in English (입장: Enter the lobby, Join a room).
    public static string T(string text, string context) => TranslationServer.Translate(text, context);

    public static string F(string format, params object[] args) => string.Format(CultureInfo.InvariantCulture, T(format), args);
}
