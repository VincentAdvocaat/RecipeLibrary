using Xunit;
using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Ingredients;
using RecipeLibrary.Domain.Entities;

namespace RecipeLibrary.Application.Tests;

public sealed class IngredientSimilarityScorerTests
{
    private readonly IngredientSimilarityScorer _sut = new();

    [Fact]
    public void Score_ReturnsHighScore_ForTypoOnSingleWord()
    {
        var score = _sut.Score("gembre", "gember");
        Assert.True(score > IngredientMatcher.FuzzyMatchScore);
    }

    [Fact]
    public void Score_ReturnsHighScore_WhenInputTypoMatchesCandidateToken()
    {
        var score = _sut.Score("gehak", "runder gehakt");
        Assert.True(score >= IngredientMatcher.SuggestionMinScore);
    }

    [Fact]
    public void Score_ReturnsSharedTokenBoost_WhenInputMatchesCandidateToken()
    {
        var score = _sut.Score("gehakt", "runder gehakt");
        Assert.True(score >= IngredientSimilarityScorer.SharedExactTokenBoost);
    }

    [Fact]
    public void Score_ReturnsSubsetBoost_WhenCandidateTokensAreContainedInInput()
    {
        var score = _sut.Score("runder gehakt", "gehakt");
        Assert.True(score >= IngredientSimilarityScorer.CandidateSubsetBoost);
    }

    [Fact]
    public void Score_ReturnsZero_ForUnrelatedStrings()
    {
        var score = _sut.Score("xyzabc123", "gember");
        Assert.True(score < IngredientMatcher.SuggestionMinScore);
    }
}
