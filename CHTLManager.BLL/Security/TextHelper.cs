using System.Globalization;
using System.Text;

namespace CHTLManager.BLL.Security;

public static class TextHelper
{
    /// <summary>Bỏ dấu tiếng Việt và đưa về chữ thường để tìm kiếm không phân biệt hoa/thường, có/không dấu.</summary>
    public static string RemoveDiacritics(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        string normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (char c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }

        return sb.ToString()
                 .Normalize(NormalizationForm.FormC)
                 .Replace('đ', 'd').Replace('Đ', 'D')
                 .ToLowerInvariant();
    }
}
