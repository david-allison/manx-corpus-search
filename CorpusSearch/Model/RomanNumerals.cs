namespace CorpusSearch.Model;

/// <summary>Chapter numbers arrive as roman numerals in headings ("CAB. II.") and
/// citations ("Jud. xii. 6"): additive and subtractive forms both occur in the
/// sources (old printings write iiii as happily as iv), so both parse.</summary>
internal static class RomanNumerals
{
    public static int? TryParse(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }
        var total = 0;
        var previous = 0;
        foreach (var c in value.ToLowerInvariant())
        {
            var digit = c switch
            {
                'i' => 1,
                'v' => 5,
                'x' => 10,
                'l' => 50,
                'c' => 100,
                'd' => 500,
                'm' => 1000,
                _ => 0,
            };
            if (digit == 0)
            {
                return null;
            }
            total += digit;
            if (previous < digit)
            {
                // a smaller digit before a larger one subtracts (iv, ix, xl, cxix)
                total -= 2 * previous;
            }
            previous = digit;
        }
        return total;
    }
}
