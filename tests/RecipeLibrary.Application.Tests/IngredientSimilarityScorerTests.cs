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
    public void Score_ReturnsZero_WhenEitherSideIsEmpty()
    {
        Assert.Equal(0m, _sut.Score("", "gember"));
        Assert.Equal(0m, _sut.Score("gember", ""));
        Assert.Equal(0m, _sut.Score("", ""));
    }

    [Fact]
    public void Score_ReturnsOne_ForExactMatch()
    {
        Assert.Equal(1m, _sut.Score("tomaat", "tomaat"));
    }

    [Fact]
    public void Score_IsAtLeastSharedTokenBoost_WhenInputMatchesCandidateToken()
    {
        var score = _sut.Score("gehakt", "runder gehakt");
        Assert.True(score >= IngredientSimilarityScorer.SharedExactTokenBoost);
    }

    [Fact]
    public void Score_IsAtLeastSubsetBoost_WhenCandidateTokensAreContainedInInput()
    {
        var score = _sut.Score("runder gehakt", "gehakt");
        Assert.True(score >= IngredientSimilarityScorer.CandidateSubsetBoost);
    }

    [Fact]
    public void Score_ReturnsOne_WhenSharedExactTokenExists()
    {
        // Exact token overlap yields 1 via per-token similarity (boost constants are dominated by Max).
        Assert.Equal(1m, _sut.Score("gehakt", "runder gehakt"));
        Assert.Equal(1m, _sut.Score("runder gehakt", "gehakt"));
    }

    [Fact]
    public void BoostConstants_MatchDocumentedValues()
    {
        Assert.Equal(0.72m, IngredientSimilarityScorer.SharedExactTokenBoost);
        Assert.Equal(0.78m, IngredientSimilarityScorer.CandidateSubsetBoost);
    }

    [Fact]
    public void Score_ReturnsZero_ForUnrelatedStrings()
    {
        var score = _sut.Score("xyzabc123", "gember");
        Assert.True(score < IngredientMatcher.SuggestionMinScore);
    }

    [Fact]
    public void StringSimilarity_ReturnsOne_ForIdenticalStrings()
    {
        Assert.Equal(1m, IngredientSimilarityScorer.StringSimilarity("gember", "gember"));
    }

    [Fact]
    public void StringSimilarity_ReturnsZero_WhenEitherSideIsEmpty()
    {
        Assert.Equal(0m, IngredientSimilarityScorer.StringSimilarity("", "gember"));
        Assert.Equal(0m, IngredientSimilarityScorer.StringSimilarity("gember", ""));
    }
}
