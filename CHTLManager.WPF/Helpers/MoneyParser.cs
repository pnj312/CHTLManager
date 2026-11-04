using System.Globalization;
using System.Text.RegularExpressions;

namespace CHTLManager.WPF.Helpers;

public static class MoneyParser
{
    // Số nguyên không dấu: "100000", hoặc có dấu phân cách hàng nghìn đúng vị trí: "100.000", "100,000".
    private static readonly Regex MoneyPattern = new(@"^(\d+|\d{1,3}(\.\d{3})+|\d{1,3}(,\d{3})+)$", RegexOptions.Compiled);

    /// <summary>
    /// Đọc số tiền VNĐ (số nguyên, không âm) từ chuỗi người dùng nhập: "100000", "100.000", "100,000", "100 000 đ".
    /// Từ chối số thập phân, số âm hoặc chuỗi có chữ ("100.000,50", "-5000", "1,5tr").
    /// </summary>
    public static bool TryParse(string? text, out decimal value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;

        string s = text.Trim();
        if (s.EndsWith('đ') || s.EndsWith('Đ')) s = s[..^1];
        s = s.Replace(" ", string.Empty).Replace(" ", string.Empty);

        if (!MoneyPattern.IsMatch(s)) return false;

        string digits = s.Replace(".", string.Empty).Replace(",", string.Empty);
        if (digits.Length > 15) return false;

        return decimal.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }

    public static string Format(decimal value) => value.ToString("N0") + " đ";
}
