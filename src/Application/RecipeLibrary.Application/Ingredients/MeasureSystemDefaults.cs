using RecipeLibrary.Domain.ValueObjects;

namespace RecipeLibrary.Application.Ingredients;

/// <summary>
/// Default <see cref="MeasureSystem"/> when no preference cookie is set.
/// Independent of UI language/culture — users pick metric or imperial explicitly.
/// </summary>
public static class MeasureSystemDefaults
{
    public static MeasureSystem Default => MeasureSystem.Metric;
}
