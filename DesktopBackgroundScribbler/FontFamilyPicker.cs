using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace DesktopBackgroundScribbler
{
    internal class FontFamilyPicker
    {
        // ちなみに、2018年現在の環境では、
        // GenericSerif    : Times New Roman
        // GenericSansSerif: Microsoft Sans Serif
        // GenericMonospace: Courier New
        // だった。
        static FontFamily[] genericFontFamilies = new[] { FontFamily.GenericSerif, FontFamily.GenericSansSerif };

        static FontFamily[] japaneseFontFamilies;
        static FontFamily[] allFontFamilies;

        static FontFamilyPicker()
        {
            var japaneseFontFamilies = GetFontFamilies(
                "メイリオ",
                "游ゴシック",
                "游明朝");
            var latinFontFamilies = GetFontFamilies(
                "Comic Sans MS",
                "Georgia",
                "Impact",
                "Segoe Print",
                "Segoe Script",
                "Times New Roman");

            if (latinFontFamilies.Count == 0)
            {
                latinFontFamilies.AddRange(genericFontFamilies);
            }
            latinFontFamilies.AddRange(japaneseFontFamilies);
            allFontFamilies = latinFontFamilies.ToArray();

            if (japaneseFontFamilies.Count == 0)
            {
                japaneseFontFamilies.AddRange(genericFontFamilies);
            }
            FontFamilyPicker.japaneseFontFamilies = japaneseFontFamilies.ToArray();
        }

        private static List<FontFamily> GetFontFamilies(params string[] names)
        {
            var fontFamilies = new List<FontFamily>();
            foreach (var name in names)
            {
                try
                {
                    // try の中で yield return は使えない。
                    fontFamilies.Add(new FontFamily(name));
                }
                catch { }
            }

            return fontFamilies;
        }

        internal static FontFamily Pick(string text, Random random)
        {
            var fontFamilies = text.All(c => c < 256)
                ? allFontFamilies
                : japaneseFontFamilies;

            return fontFamilies[random.Next(fontFamilies.Length)];
        }
    }
}