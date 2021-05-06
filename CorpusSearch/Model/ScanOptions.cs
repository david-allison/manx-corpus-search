namespace Codex_API.Model
{
    public class ScanOptions
    {
        public static ScanOptions Default => new();

        /// <summary>
        /// Whether we want 'ç' to match 'c' (and other diacritics)
        /// </summary>
        public bool NormalizeDiacritics { get; set; } = true;
    }
}