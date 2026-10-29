using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace AFMI_Montage_projet_2.Helpers
{
    /// <summary>
    /// تطبيع متقدم للنصوص العربية والفرنسية والإنجليزية
    /// لإتاحة بحث دقيق ومرن عبر الكلمات المفتاحية.
    /// </summary>
    public static class TextNormalizer
    {
        private static readonly Regex ArabicDiacriticsRegex = new(@"[\u064B-\u065F\u0670]", RegexOptions.Compiled);

        public static string Normalize(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;

            string text = input.Trim();

            // 1. التطبيع العربي (إزالة التشكيل وتوحيد الحروف)
            text = NormalizeArabic(text);

            // 2. التطبيع اللاتيني والفرنسي (إزالة النبرات التشكيلية Diacritics)
            text = NormalizeLatin(text);

            return text.ToLowerInvariant();
        }

        public static string NormalizeArabic(string text)
        {
            // إزالة حركات التشكيل (فتحة، ضمة، كسرة، سكون، تنوين، شدة)
            string noDiacritics = ArabicDiacriticsRegex.Replace(text, "");

            var sb = new StringBuilder(noDiacritics.Length);
            foreach (char c in noDiacritics)
            {
                switch (c)
                {
                    // توحيد الألفات (أ، إ، آ، ٱ -> ا)
                    case '\u0622': // آ
                    case '\u0623': // أ
                    case '\u0625': // إ
                    case '\u0671': // ٱ
                        sb.Append('\u0627'); // ا
                        break;

                    // توحيد التاء المربوطة والهاء (ة -> ه)
                    case '\u0629': // ة
                        sb.Append('\u0647'); // ه
                        break;

                    // توحيد الألف المقصورة والياء (ى -> ي)
                    case '\u0649': // ى
                        sb.Append('\u064A'); // ي
                        break;

                    // الكشيدة (التطويل)
                    case '\u0640':
                        break;

                    default:
                        sb.Append(c);
                        break;
                }
            }

            return sb.ToString();
        }

        public static string NormalizeLatin(string text)
        {
            // إزالة اللواحق التشكيلية الفرنسية (é, è, ê, à, ç, etc.)
            string normalizedString = text.Normalize(NormalizationForm.FormD);
            var stringBuilder = new StringBuilder(normalizedString.Length);

            foreach (char c in normalizedString)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }

            return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
        }
    }
}
