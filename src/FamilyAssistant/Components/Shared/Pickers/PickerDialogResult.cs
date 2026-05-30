namespace FamilyAssistant.Components.Shared.Pickers;

public sealed record PickerDialogResult<TValue>(IReadOnlyList<TValue> Values, string? ActionKey = null)
{
    public TValue? Value => Values.Count > 0 ? Values[0] : default;
}
