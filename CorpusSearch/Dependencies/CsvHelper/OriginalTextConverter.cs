using CsvHelper;

namespace CorpusSearch.Dependencies.CsvHelper;

internal sealed class OriginalTextConverter : ICsvConverter<string?>
{
    public string? Read(CsvReader reader, int index)
    {
        var value = index < reader.FieldCount ? reader.GetString(index) : null;
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    public void Write(CsvWriter writer, string? value) => writer.WriteField(value);
}
