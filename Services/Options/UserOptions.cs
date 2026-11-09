namespace Studium_Users.Services.Options;

/// <summary>
/// Configuration options for user standing policies and temporary suspension durations.
/// </summary>
public class UserOptions
{
    public const string SectionName = "Users";

    public int MaxNoShowsBeforeSuspension { get; set; } = 3;
    public int SuspensionDays { get; set; } = 7;
}
