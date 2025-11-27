namespace Ratatosk.Infrastructure.Configuration;

public class DatabaseOptions
{
    public const string SectionName = "Database";

    public string ConnectionString { get; set; } = default!;
}
