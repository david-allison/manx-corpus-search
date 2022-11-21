using System;

namespace CorpusSearch.Utils;

public static class AnonymousAnalytics
{
    private static bool _hasInit;
    public static bool Init()
    {
        var key = Environment.GetEnvironmentVariable("CORPUS_SEARCH_SEGMENT_KEY");

        if (key == null)
        {
            return false;
        }

        try
        {
            Segment.Analytics.Initialize(key);
            _hasInit = true;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static void Track(string eventName)
    {
        if (!_hasInit)
        {
            return;
        }
        Segment.Analytics.Client.Track("Anon", eventName);
    }
}