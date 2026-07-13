namespace CorpusSearch.Model;

public enum SearchType
{
    Manx,
    English,

    /// <summary>The verse/chapter reference field: an internal side-query run
    /// alongside Manx searches, never user-selected directly</summary>
    Reference,
}