using System;
using System.Globalization;
using System.Linq;
using System.Text;

public static class LanPlayerNames
{
    public const int MaxLength = 20;
    public const string DefaultName = "Người chơi";

    public static string Normalize(string value)
    {
        string name = (value ?? "").Normalize(NormalizationForm.FormC).Trim();
        if (name.Length == 0) throw new ArgumentException("Nhập tên hiển thị của bạn.");
        if (new StringInfo(name).LengthInTextElements > MaxLength)
            throw new ArgumentException("Tên hiển thị tối đa 20 ký tự.");
        if (name.Any(c => !char.IsLetterOrDigit(c) && c != ' ' && c != '_' && c != '-' && c != '.'))
            throw new ArgumentException("Tên chỉ dùng chữ, số, khoảng trắng và dấu . _ -");
        return name;
    }

    public static string ForGuest(string name, string hostName)
    {
        name = Normalize(name);
        if (!string.Equals(name, hostName, StringComparison.OrdinalIgnoreCase)) return name;
        int[] elements = StringInfo.ParseCombiningCharacters(name);
        if (elements.Length > MaxLength - 2) name = name.Substring(0, elements[MaxLength - 2]);
        return name.TrimEnd() + " 2";
    }
}
