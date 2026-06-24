using System.Buffers;
using System.Buffers.Binary;
using MultimodalEmbeddingLibrary.Interfaces;
using MultimodalEmbeddingLibrary.Models;

namespace MultimodalEmbeddingLibrary.Services;

public class MultimodalEmbeddingLibraryService : IMultimodalEmbeddingLibrary
{
    private static readonly Dictionary<string, int> KnownModelDims = new(StringComparer.OrdinalIgnoreCase)
    {
        ["cohere-embed-v4"]      = 1536,
        ["cohere-embed-v4-1536"] = 1536,
        ["cohere-embed-v4-1024"] = 1024,
        ["cohere-embed-v4-512"]  = 512,
        ["cohere-embed-v4-256"]  = 256,
    };

    public byte[] EmbedImage(byte[] imageBinary, string modelVersion, string apiEndpoint, string apiKey)
    {
        if (imageBinary == null || imageBinary.Length == 0)
            throw new ArgumentException("imageBinary must not be null or empty.", nameof(imageBinary));
        if (string.IsNullOrWhiteSpace(modelVersion))
            throw new ArgumentException("modelVersion must not be null or whitespace.", nameof(modelVersion));
        if (string.IsNullOrWhiteSpace(apiEndpoint))
            throw new ArgumentException("apiEndpoint must not be null or whitespace.", nameof(apiEndpoint));
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("apiKey must not be null or whitespace.", nameof(apiKey));

        string mimeType = DetectImageMimeType(imageBinary);
        string dataUri = $"data:{mimeType};base64,{Convert.ToBase64String(imageBinary)}";
        float[] floats = Task.Run(async () => await EmbeddingClient.EmbedImageAsync(dataUri, apiEndpoint, apiKey))
            .GetAwaiter().GetResult();

        return ValidateNormalizeAndPack(floats, modelVersion);
    }

    public byte[] EmbedText(string queryText, string modelVersion, string apiEndpoint, string apiKey)
    {
        if (string.IsNullOrWhiteSpace(queryText))
            throw new ArgumentException("queryText must not be null or whitespace.", nameof(queryText));
        if (string.IsNullOrWhiteSpace(modelVersion))
            throw new ArgumentException("modelVersion must not be null or whitespace.", nameof(modelVersion));
        if (string.IsNullOrWhiteSpace(apiEndpoint))
            throw new ArgumentException("apiEndpoint must not be null or whitespace.", nameof(apiEndpoint));
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("apiKey must not be null or whitespace.", nameof(apiKey));

        float[] floats = Task.Run(async () => await EmbeddingClient.EmbedTextAsync(queryText, apiEndpoint, apiKey))
            .GetAwaiter().GetResult();

        return ValidateNormalizeAndPack(floats, modelVersion);
    }

    public List<ImageSearchResult> SearchVector(byte[] queryVector, List<VectorCandidate> candidates, int topK, string excludeId = "", decimal minScore = 0m)
    {
        if (candidates == null || candidates.Count == 0)
            return new List<ImageSearchResult>();

        if (queryVector == null || queryVector.Length == 0)
            throw new ArgumentException("queryVector must not be null or empty.", nameof(queryVector));

        if (queryVector.Length % 4 != 0)
            throw new ArgumentException(
                $"queryVector length {queryVector.Length} is not divisible by 4. Blob is corrupt.", nameof(queryVector));

        int queryDims = queryVector.Length / 4;
        var queryFloats = new float[queryDims];
        for (int i = 0; i < queryDims; i++)
            queryFloats[i] = BinaryPrimitives.ReadSingleLittleEndian(queryVector.AsSpan(i * 4, 4));

        List<(string Id, float Score)> results = [];

        foreach (var candidate in candidates)
        {
            if (!string.IsNullOrEmpty(excludeId) && candidate.Id == excludeId)
                continue;

            if (candidate.VectorBytes == null)
            {
                Console.Error.WriteLine($"[MultimodalEmbeddingLibrary] Skipping candidate {candidate.Id}: VectorBytes is null.");
                continue;
            }
            if (candidate.VectorBytes.Length % 4 != 0)
            {
                Console.Error.WriteLine($"[MultimodalEmbeddingLibrary] Skipping candidate {candidate.Id}: byte length {candidate.VectorBytes.Length} not divisible by 4.");
                continue;
            }
            if (candidate.VectorBytes.Length / 4 != queryDims)
            {
                Console.Error.WriteLine($"[MultimodalEmbeddingLibrary] Skipping candidate {candidate.Id}: dims {candidate.VectorBytes.Length / 4} does not match query dims {queryDims}.");
                continue;
            }

            float[] candidateFloats = ArrayPool<float>.Shared.Rent(queryDims);
            try
            {
                for (int i = 0; i < queryDims; i++)
                    candidateFloats[i] = BinaryPrimitives.ReadSingleLittleEndian(candidate.VectorBytes.AsSpan(i * 4, 4));

                float dot = 0f;
                for (int i = 0; i < queryDims; i++)
                    dot += queryFloats[i] * candidateFloats[i];

                results.Add((candidate.Id, dot));
            }
            finally
            {
                ArrayPool<float>.Shared.Return(candidateFloats);
            }
        }

        results.Sort((a, b) =>
        {
            int cmp = b.Score.CompareTo(a.Score);
            return cmp != 0 ? cmp : string.CompareOrdinal(a.Id, b.Id);
        });

        int firstBelowMin = results.FindIndex(r => (decimal)r.Score < minScore);
        if (firstBelowMin >= 0)
            results.RemoveRange(firstBelowMin, results.Count - firstBelowMin);

        int take = Math.Min(topK, results.Count);
        var output = new List<ImageSearchResult>(take);
        for (int i = 0; i < take; i++)
            output.Add(new ImageSearchResult { Id = results[i].Id, Score = (decimal)results[i].Score });

        return output;
    }

    private static string DetectImageMimeType(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            return "image/jpeg";
        if (bytes.Length >= 4 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
            return "image/png";
        if (bytes.Length >= 4 && bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x38)
            return "image/gif";
        if (bytes.Length >= 12 && bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46 &&
            bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50)
            return "image/webp";
        return "image/jpeg";
    }

    private static byte[] ValidateNormalizeAndPack(float[] floats, string modelVersion)
    {
        int dims = floats.Length;

        if (KnownModelDims.TryGetValue(modelVersion, out int expectedDims) && dims != expectedDims)
            throw new InvalidOperationException(
                $"Parsed dimension count {dims} does not match expected {expectedDims} for model '{modelVersion}'.");

        for (int i = 0; i < dims; i++)
        {
            if (float.IsNaN(floats[i]) || float.IsInfinity(floats[i]))
                throw new InvalidOperationException(
                    $"Embedding contains NaN or Infinity at index {i}. Reject this record at ingestion.");
        }

        float norm = MathF.Sqrt(floats.Sum(f => f * f));
        if (norm == 0f)
            throw new InvalidOperationException("Zero vector after parsing. Cannot normalize.");

        for (int i = 0; i < dims; i++)
            floats[i] /= norm;

        var result = new byte[dims * 4];
        for (int i = 0; i < dims; i++)
            BinaryPrimitives.WriteSingleLittleEndian(result.AsSpan(i * 4, 4), floats[i]);

        return result;
    }
}
