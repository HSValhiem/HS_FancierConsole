//Holi.cs
using System.Linq;
using System.Runtime.InteropServices;
using System;
using System.IO;
using BepInEx;

public static class Holi
{
    const int STD_OUTPUT_HANDLE = -11;
    const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 4;

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll")]
    static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

    [DllImport("kernel32.dll")]
    static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

    public const string RESET = "\x1B[0m";
    public const string UNDERLINE = "\x1B[4m";
    public const string BOLD = "\x1B[1m";
    public const string ITALIC = "\x1B[3m";
    public const string BLINK = "\x1B[5m";
    public const string BLINKRAPID = "\x1B[6m";
    public const string DEFAULTFORE = "\x1B[39m";
    public const string DEFAULTBACK = "\x1B[49m";

    static Holi()
    {
        var handle = GetStdHandle(STD_OUTPUT_HANDLE);
        uint mode;
        GetConsoleMode(handle, out mode);
        mode |= ENABLE_VIRTUAL_TERMINAL_PROCESSING;
        SetConsoleMode(handle, mode);
    }

    public static byte[] HexToRgb(string hexcolor)
    {
        hexcolor = hexcolor.Remove(0, 1);

        if (hexcolor.Length != 6)
            throw new Exception("Not a valid hex color");

        string[] rgb = hexcolor.Select((obj, index) => new { obj, index })
            .GroupBy(o => o.index / 2)
            .Select(g => new string(g.Select(a => a.obj)
            .ToArray())).ToArray<string>();

        return new byte[] { Convert.ToByte(rgb[0], 16), Convert.ToByte(rgb[1], 16), Convert.ToByte(rgb[2], 16) };
    }
    public static string ForeColor(this string text, byte red, byte green, byte blue)
    {
        return $"\x1B[38;2;{red};{green};{blue}m{text}";
    }

    public static string ForeColor(this string text, string hexrgb)
    {
        byte[] rgb = HexToRgb(hexrgb);

        return ForeColor(text, rgb[0], rgb[1], rgb[2]);
    }

    public static string ForeColor(this string text, int id)
    {
        return $"\u001b[38;5;{id}m{text}\u001b[0m";
    }

    public static string BackColor(this string text, byte red, byte green, byte blue)
    {
        return $"\x1B[48;2;{red};{green};{blue}m{text}";
    }

    public static string BackColor(this string text, string hexrgb)
    {
        byte[] rgb = HexToRgb(hexrgb);

        return BackColor(text, rgb[0], rgb[1], rgb[2]);
    }

    public static string ResetColor(this string text)
    {
        return $"{RESET}{text}";
    }

    public static string Bold(this string text)
    {
        return $"{BOLD}{text}";
    }

    public static string Italic(this string text)
    {
        return $"{ITALIC}{text}";
    }

    public static string Underline(this string text)
    {
        return $"{UNDERLINE}{text}";
    }

    public static string Add(this string text, string addText)
    {
        return $"{text}{RESET}{addText}";
    }

    public static void Print(this string text)
    {

        ConsoleManager.ConsoleStream?.Write($"{text}{RESET}");
    }

    public static void Printf(this string text, byte red = 0, byte green = 0, byte blue = 0)
    {
        ConsoleManager.ConsoleStream?.Write($"{ForeColor(red, green, blue)}{text}{RESET}");
    }

    public static void Printf(this string text, string hexColor)
    {
        byte[] rgb = HexToRgb(hexColor);

        ConsoleManager.ConsoleStream?.Write($"{ForeColor(rgb[0], rgb[1], rgb[2])}{text}{RESET}");
    }



    public static string ForeColor(params byte[] rgb)
    {
        if (rgb == null || rgb.Length == 0)
            return "\x1B[0m";

        if (rgb.Length == 3)
            return $"\x1B[38;2;{rgb[0]};{rgb[1]};{rgb[2]}m";

        if (rgb.Length == 2)
            return $"\x1B[38;2;{rgb[0]};{rgb[1]};0m";
        if (rgb.Length == 2)
            return $"\x1B[38;2;{rgb[0]};0;0m";

        return "\x1B[0m";

    }


    public static string BackColor(params byte[] rgb)
    {
        if (rgb == null || rgb.Length == 0)
            return "\x1B[0m";

        if (rgb.Length == 3)
            return $"\x1B[48;2;{rgb[0]};{rgb[1]};{rgb[2]}m";

        if (rgb.Length == 2)
            return $"\x1B[48;2;{rgb[0]};{rgb[1]};0m";
        if (rgb.Length == 2)
            return $"\x1B[48;2;{rgb[0]};0;0m";

        return "\x1B[0m";
    }


}
