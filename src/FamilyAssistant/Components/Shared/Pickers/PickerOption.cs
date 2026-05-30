using MudBlazor;

namespace FamilyAssistant.Components.Shared.Pickers;

public sealed record PickerOption<TValue>
{
    public required TValue Value { get; init; }
    public required string Title { get; init; }
    public string? Subtitle { get; init; }
    public string? Detail { get; init; }
    public string? Icon { get; init; }
    public string? IconText { get; init; }
    public string? IconMarkup { get; init; }
    public string? IconColor { get; init; }
    public string? ChipText { get; init; }
    public Color ChipColor { get; init; } = Color.Default;
    public string? SearchText { get; init; }
    public bool Disabled { get; init; }
}
