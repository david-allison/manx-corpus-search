using System;
namespace CorpusSearch.Extensions;

public static class DateTimeUtil
{
    public static DateTime FromYear(int year)
    {
        if (year <= 0)
        {
            year = 1;
        }
        return new DateTime(year, 1, 1);
    }

    public static DateTime FromYearMax(int year)
    {
        if (year <= 0)
        {
            year = 1;
        }
        return new DateTime(year, 12, 31);
    }
}