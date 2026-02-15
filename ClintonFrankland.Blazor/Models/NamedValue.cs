namespace ClintonFrankland.Models;

public class NamedValue
{
    public int Id { get; set; } = -1;
    public string Name { get; set; } = string.Empty;
    public object? Value { get; set; }
    public bool IsBinary { get; set; } = false;

    public event Action<string, object?>? ValueUpdated;

    public NamedValue()
    {
        Value = string.Empty;
        ValueUpdated?.Invoke(Name, Value);
    }

    public NamedValue(string name)
    {
        Name = name;
        Value = string.Empty;
        ValueUpdated?.Invoke(Name, Value);
    }

    public NamedValue(string name, object? value)
    {
        Name = name;
        Value = value;
        ValueUpdated?.Invoke(Name, Value);
    }

    public NamedValue(int id, string name, object? value)
    {
        Id = id;
        Name = name;
        Value = value;
        ValueUpdated?.Invoke(Name, Value);
    }

    public NamedValue(string name, object? value, bool isBinary)
    {
        Name = name;
        Value = value;
        IsBinary = isBinary;
        ValueUpdated?.Invoke(Name, Value);
    }
}
