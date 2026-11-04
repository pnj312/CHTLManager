namespace CHTLManager.WPF.Helpers;

public static class MoneyParser
{
    /// <summary>Đọc số tiền VNĐ từ chuỗi người dùng nhập ("100000", "100.000", "100,000 đ"...).</summary>
    public static bool TryParse(string? text, out decimal value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;

        string digits = new string(text.Where(char.IsDigit).ToArray());
        if (digits.Length == 0 || digits.Length > 15) return false;

        return decimal.TryParse(digits, out value);
    }

    public static string Format(decimal value) => value.ToString("N0") + " đ";
}
