﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace DesktopBackgroundScribbler
{
    public class FontInfo
    {
        // ちなみに、2018年現在の環境では、
        // GenericSerif    : Times New Roman
        // GenericSansSerif: Microsoft Sans Serif
        // GenericMonospace: Courier New
        // だった。
        static FontFamily[] genericFontFamilies = new[] { FontFamily.GenericSerif, FontFamily.GenericSansSerif };

        static FontFamily[] japaneseFontFamilies;
        static FontFamily[] allFontFamilies;

        // 斜体はあまりかっこよくないので4回に1回しか出ないようにする。
        static FontStyle[] fontStyles = new[]
        {
            FontStyle.Regular,
            FontStyle.Bold,
            FontStyle.Regular,
            FontStyle.Bold,
            FontStyle.Regular,
            FontStyle.Bold,
            FontStyle.Italic,
            FontStyle.Bold | FontStyle.Italic
        };

        public FontFamily Family { get; }
        public FontStyle Style { get; }

        static FontInfo()
        {
            var japaneseFontFamilies = GetFontFamilies(
                "メイリオ",
                "メイリオ",
                "游ゴシック",
                "游ゴシック",
                "游明朝",
                "游明朝",
                "UD デジタル 教科書体 NK-R",
                "UD デジタル 教科書体 NK-B",
                "BIZ UDPゴシック",
                "BIZ UDPゴシック",
                "BIZ UDP明朝",
                "BIZ UDP明朝");
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
            FontInfo.japaneseFontFamilies = japaneseFontFamilies.ToArray();
        }

        public FontInfo(FontFamily family, FontStyle style)
        {
            Family = family;
            Style = style;
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

        internal static FontInfo GenerateRandomFontInfo(string text, Random random)
        {
            var fontFamilies = text.All(c => c < 256)
                ? allFontFamilies
                : japaneseFontFamilies;

            var family = fontFamilies[random.Next(fontFamilies.Length)];
            var style = fontStyles[random.Next(fontStyles.Length)];

            return new FontInfo(family, style);
        }
    }
}