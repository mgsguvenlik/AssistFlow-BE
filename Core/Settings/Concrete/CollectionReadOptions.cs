namespace Core.Settings.Concrete;

/// <summary>Keep disabled until model registration, database deployment and menu grants are verified.</summary>
public sealed class CollectionReadOptions
{
    public const string SectionName = "CollectionRead";
    public bool Enabled { get; set; }
}
