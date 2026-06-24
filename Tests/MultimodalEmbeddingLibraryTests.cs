using System.Buffers.Binary;
using MultimodalEmbeddingLibrary.Models;
using MultimodalEmbeddingLibrary.Services;
using Xunit;

namespace MultimodalEmbeddingLibrary.Tests;

public class MultimodalEmbeddingLibraryTests
{
    private readonly MultimodalEmbeddingLibraryService _svc = new();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static float[] DecodeVector(byte[] bytes)
    {
        var floats = new float[bytes.Length / 4];
        for (int i = 0; i < floats.Length; i++)
            floats[i] = BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(i * 4, 4));
        return floats;
    }

    private static void AssertUnitLength(float[] floats, float tolerance = 1e-5f)
    {
        float norm = MathF.Sqrt(floats.Sum(f => f * f));
        Assert.InRange(norm, 1.0f - tolerance, 1.0f + tolerance);
    }

    private static byte[] PackKnown(float[] floats)
    {
        float norm = MathF.Sqrt(floats.Sum(f => f * f));
        var result = new byte[floats.Length * 4];
        for (int i = 0; i < floats.Length; i++)
            BinaryPrimitives.WriteSingleLittleEndian(result.AsSpan(i * 4, 4), floats[i] / norm);
        return result;
    }

    // Minimal valid 1×1 white JPEG — sufficient for the Cohere API to accept
    private static readonly byte[] MinimalJpeg = Convert.FromBase64String(
        "/9j/4AAQSkZJRgABAQAAAQABAAD/2wBDAAgGBgcGBQgHBwcJCQgKDBQNDAsLDB" +
        "kSEw8UHRofHh0aHBwgJC4nICIsIxwcKDcpLDAxNDQ0Hyc5PTgyPC4zNDL/wAAR" +
        "CAABAAEDASIAAhEBAxEB/8QAFAABAAAAAAAAAAAAAAAAAAAACf/EABQQAQAAAAAA" +
        "AAAAAAAAAAAAAP/EABQBAQAAAAAAAAAAAAAAAAAAAAD/xAAUEQEAAAAAAAAAAAAA" +
        "AAAAAAAA/9oADAMBAAIRAxEAPwCwABmX/9k=");

    private static (string apiEndpoint, string apiKey) GetCohereCredentials()
    {
        string endpoint = Environment.GetEnvironmentVariable("COHERE_API_ENDPOINT")
                          ?? "https://api.cohere.com/v2/embed";
        string? key = Environment.GetEnvironmentVariable("COHERE_API_KEY");
        return (endpoint, key ?? string.Empty);
    }

    // ── EmbedImage integration tests ──────────────────────────────────────────

    [Fact]
    [Trait("Category", "Integration")]
    public void EmbedImage_ValidImage_ReturnsCorrectByteLength()
    {
        var (endpoint, apiKey) = GetCohereCredentials();
        if (string.IsNullOrEmpty(apiKey)) return;

        var result = _svc.EmbedImage(MinimalJpeg, "cohere-embed-v4-1536", endpoint, apiKey);

        Assert.Equal(6144, result.Length); // 1536 dims × 4 bytes
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void EmbedImage_IsNormalized()
    {
        var (endpoint, apiKey) = GetCohereCredentials();
        if (string.IsNullOrEmpty(apiKey)) return;

        var result = _svc.EmbedImage(MinimalJpeg, "cohere-embed-v4-1536", endpoint, apiKey);

        AssertUnitLength(DecodeVector(result));
    }

    [Fact]
    public void EmbedImage_NullImageThrows()
    {
        Assert.Throws<ArgumentException>(() =>
            _svc.EmbedImage(null!, "cohere-embed-v4-1536", "https://api.cohere.com/v2/embed", "key"));
    }

    [Fact]
    public void EmbedImage_NullApiKeyThrows()
    {
        Assert.Throws<ArgumentException>(() =>
            _svc.EmbedImage(MinimalJpeg, "cohere-embed-v4-1536", "https://api.cohere.com/v2/embed", null!));
    }

    // ── EmbedText integration tests ───────────────────────────────────────────

    [Fact]
    [Trait("Category", "Integration")]
    public void EmbedText_ValidText_ReturnsCorrectByteLength()
    {
        var (endpoint, apiKey) = GetCohereCredentials();
        if (string.IsNullOrEmpty(apiKey)) return;

        var result = _svc.EmbedText("a dog on a beach", "cohere-embed-v4-1536", endpoint, apiKey);

        Assert.Equal(6144, result.Length); // 1536 dims × 4 bytes
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void EmbedText_IsNormalized()
    {
        var (endpoint, apiKey) = GetCohereCredentials();
        if (string.IsNullOrEmpty(apiKey)) return;

        var result = _svc.EmbedText("a dog on a beach", "cohere-embed-v4-1536", endpoint, apiKey);

        AssertUnitLength(DecodeVector(result));
    }

    [Fact]
    public void EmbedText_NullTextThrows()
    {
        Assert.Throws<ArgumentException>(() =>
            _svc.EmbedText(null!, "cohere-embed-v4-1536", "https://api.cohere.com/v2/embed", "key"));
    }

    // ── SearchVectors tests ───────────────────────────────────────────────────

    private static (byte[] query, List<VectorCandidate> candidates) MakeSearchFixture(int dims, int count)
    {
        var rng = new Random(42);
        var queryRaw = Enumerable.Range(0, dims).Select(_ => (float)rng.NextDouble()).ToArray();
        var query = PackKnown(queryRaw);

        var candidates = new List<VectorCandidate>();
        for (int c = 0; c < count; c++)
        {
            var raw = Enumerable.Range(0, dims).Select(_ => (float)rng.NextDouble()).ToArray();
            candidates.Add(new VectorCandidate { Id = $"id-{c}", VectorBytes = PackKnown(raw) });
        }
        return (query, candidates);
    }

    [Fact]
    public void SearchVectors_EmptyCandidates_ReturnsEmpty()
    {
        var (query, _) = MakeSearchFixture(4, 0);
        var result = _svc.SearchVector(query, new List<VectorCandidate>(), 10, "");
        Assert.Empty(result);
    }

    [Fact]
    public void SearchVectors_NullCandidates_ReturnsEmpty()
    {
        var (query, _) = MakeSearchFixture(4, 0);
        var result = _svc.SearchVector(query, null!, 10, "");
        Assert.Empty(result);
    }

    [Fact]
    public void SearchVectors_TopKClamped()
    {
        var (query, candidates) = MakeSearchFixture(64, 3);
        var result = _svc.SearchVector(query, candidates, 10, "");
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void SearchVectors_SelfMatchExcluded()
    {
        var (query, candidates) = MakeSearchFixture(64, 5);
        string excludeId = candidates[2].Id;
        var result = _svc.SearchVector(query, candidates, 10, excludeId);
        Assert.DoesNotContain(result, r => r.Id == excludeId);
        Assert.Equal(4, result.Count);
    }

    [Fact]
    public void SearchVectors_EmptyExcludeId_NoExclusion()
    {
        var (query, candidates) = MakeSearchFixture(64, 3);
        var result = _svc.SearchVector(query, candidates, 10, "");
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void SearchVectors_CorruptBlobSkipped()
    {
        var (query, candidates) = MakeSearchFixture(64, 3);
        candidates[1] = new VectorCandidate { Id = "corrupt", VectorBytes = null! };
        var result = _svc.SearchVector(query, candidates, 10, "");
        Assert.DoesNotContain(result, r => r.Id == "corrupt");
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void SearchVectors_WrongDimsSkipped()
    {
        var (query, candidates) = MakeSearchFixture(64, 3);
        candidates[0] = new VectorCandidate { Id = "wrong-dims", VectorBytes = new byte[128] };
        var result = _svc.SearchVector(query, candidates, 10, "");
        Assert.DoesNotContain(result, r => r.Id == "wrong-dims");
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void SearchVectors_SortedDescending()
    {
        // query = [1,0,0,0] (unit)
        var query = new byte[16];
        BinaryPrimitives.WriteSingleLittleEndian(query.AsSpan(0, 4), 1.0f);

        // A: [1,0,0,0] → dot = 1.0
        var vecA = new byte[16];
        BinaryPrimitives.WriteSingleLittleEndian(vecA.AsSpan(0, 4), 1.0f);

        // B: [0,1,0,0] → dot = 0.0
        var vecB = new byte[16];
        BinaryPrimitives.WriteSingleLittleEndian(vecB.AsSpan(4, 4), 1.0f);

        // C: [-1,0,0,0] → dot = -1.0
        var vecC = new byte[16];
        BinaryPrimitives.WriteSingleLittleEndian(vecC.AsSpan(0, 4), -1.0f);

        var candidates = new List<VectorCandidate>
        {
            new() { Id = "C", VectorBytes = vecC },
            new() { Id = "A", VectorBytes = vecA },
            new() { Id = "B", VectorBytes = vecB },
        };

        var result = _svc.SearchVector(query, candidates, 10, "", minScore: -1m);

        Assert.Equal(3, result.Count);
        Assert.Equal("A", result[0].Id);
        Assert.Equal("B", result[1].Id);
        Assert.Equal("C", result[2].Id);
        Assert.True(result[0].Score > result[1].Score);
        Assert.True(result[1].Score > result[2].Score);
    }

    [Fact]
    public void SearchVectors_StableTiebreak()
    {
        var query = new byte[16];
        BinaryPrimitives.WriteSingleLittleEndian(query.AsSpan(0, 4), 1.0f);

        var vec = new byte[16];
        BinaryPrimitives.WriteSingleLittleEndian(vec.AsSpan(0, 4), 1.0f);

        var candidates = new List<VectorCandidate>
        {
            new() { Id = "z-second", VectorBytes = (byte[])vec.Clone() },
            new() { Id = "a-first",  VectorBytes = (byte[])vec.Clone() },
        };

        var result = _svc.SearchVector(query, candidates, 10, "");

        Assert.Equal(2, result.Count);
        Assert.Equal("a-first", result[0].Id);
        Assert.Equal("z-second", result[1].Id);
    }

    [Fact]
    public void SearchVectors_IdenticalVectors_ScoreNearOne()
    {
        var (query, _) = MakeSearchFixture(256, 0);
        var candidates = new List<VectorCandidate>
        {
            new() { Id = "self", VectorBytes = (byte[])query.Clone() }
        };

        var result = _svc.SearchVector(query, candidates, 1, "");

        Assert.Single(result);
        Assert.InRange((float)result[0].Score, 1.0f - 1e-5f, 1.0f + 1e-5f);
    }

    [Fact]
    public void SearchVector_MinScoreFiltersResults()
    {
        // query = [1,0,0,0]; A dot=1.0, B dot=0.0, C dot=-1.0
        var query = new byte[16];
        BinaryPrimitives.WriteSingleLittleEndian(query.AsSpan(0, 4), 1.0f);

        var vecA = new byte[16];
        BinaryPrimitives.WriteSingleLittleEndian(vecA.AsSpan(0, 4), 1.0f);

        var vecB = new byte[16];
        BinaryPrimitives.WriteSingleLittleEndian(vecB.AsSpan(4, 4), 1.0f);

        var vecC = new byte[16];
        BinaryPrimitives.WriteSingleLittleEndian(vecC.AsSpan(0, 4), -1.0f);

        var candidates = new List<VectorCandidate>
        {
            new() { Id = "A", VectorBytes = vecA },
            new() { Id = "B", VectorBytes = vecB },
            new() { Id = "C", VectorBytes = vecC },
        };

        var result = _svc.SearchVector(query, candidates, 10, "", minScore: 0.5m);

        Assert.Single(result);
        Assert.Equal("A", result[0].Id);
    }

    [Fact]
    public void SearchVectors_NullQueryThrows()
    {
        var (_, candidates) = MakeSearchFixture(64, 3);
        Assert.Throws<ArgumentException>(() => _svc.SearchVector(null!, candidates, 10, ""));
    }

    [Fact]
    public void SearchVectors_CorruptQueryThrows()
    {
        var (_, candidates) = MakeSearchFixture(64, 3);
        var badQuery = new byte[13];
        var ex = Assert.Throws<ArgumentException>(() => _svc.SearchVector(badQuery, candidates, 10, ""));
        Assert.Contains("13", ex.Message);
    }
}
