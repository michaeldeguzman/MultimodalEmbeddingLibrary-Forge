using MultimodalEmbeddingLibrary.Models;
using OutSystems.ExternalLibraries.SDK;

namespace MultimodalEmbeddingLibrary.Interfaces;

[OSInterface(
    Name = "MultimodalEmbeddingLibrary",
    Description = "Produces multimodal embeddings using the Cohere Embed v4 API and performs vector similarity search. Supports both image-to-image and text-to-image search by embedding images and text queries into a shared vector space. Use EmbedImage during ingestion to generate and store vectors, EmbedText or EmbedImage at query time to generate a query vector, then SearchVector to rank stored candidates by similarity. API credentials are passed as parameters sourced from ODC site properties.",
    IconResourceName = "MultimodalEmbeddingLibrary.MultimodalEmbeddingLibrary.png")]
public interface IMultimodalEmbeddingLibrary
{
    [OSAction(
        Description = "Converts a raw image binary into a normalized embedding vector using the Cohere Embed v4 API. The vector is packed as a little-endian binary blob ready to store in the VectorBytes attribute of your image entity. Call this once per image during ingestion, before saving the entity record.",
        ReturnName = "VectorBytes",
        ReturnDescription = "Normalized little-endian float32 vector packed as a byte array. Store this in the VectorBytes attribute of your image entity. Pass it as the queryVector in SearchVector to find visually similar images.")]
    byte[] EmbedImage(
        [OSParameter(Description = "Raw binary content of the image file to embed. Pass the binary directly — do not base64-encode it, the action handles encoding internally. Supported formats: JPEG, PNG, GIF, WebP.")] byte[] imageBinary,
        [OSParameter(Description = "Cohere embedding model variant that determines the output vector dimension. Supported values: 'cohere-embed-v4' or 'cohere-embed-v4-1536' (1536 dimensions), 'cohere-embed-v4-1024' (1024 dimensions), 'cohere-embed-v4-512' (512 dimensions), 'cohere-embed-v4-256' (256 dimensions). All images and queries in the same corpus must use the same model variant.")] string modelVersion,
        [OSParameter(Description = "Full URL of the Cohere Embed v4 REST endpoint. Standard value: https://api.cohere.com/v2/embed. Source this from an ODC site property so it can be changed without redeployment.")] string apiEndpoint,
        [OSParameter(Description = "Cohere API key used for Bearer token authentication. Source this from an ODC site property — never hardcode it in logic.")] string apiKey
    );

    [OSAction(
        Description = "Converts a text description into a normalized embedding vector using the Cohere Embed v4 API, in the same vector space as images embedded with EmbedImage. Use this to enable text-to-image search, where a user types a description to find visually matching images. Pass the returned vector as the queryVector in SearchVector.",
        ReturnName = "VectorBytes",
        ReturnDescription = "Normalized little-endian float32 vector packed as a byte array. Pass this directly as the queryVector parameter in SearchVector to find images matching the text description.")]
    byte[] EmbedText(
        [OSParameter(Description = "Natural language description of what the user is searching for. For example: 'a golden retriever running on a beach at sunset'. More specific descriptions generally produce better results.")] string queryText,
        [OSParameter(Description = "Cohere embedding model variant. Must exactly match the modelVersion used when the image corpus was ingested, so that text and image vectors are comparable in the same vector space.")] string modelVersion,
        [OSParameter(Description = "Full URL of the Cohere Embed v4 REST endpoint. Standard value: https://api.cohere.com/v2/embed. Source this from an ODC site property.")] string apiEndpoint,
        [OSParameter(Description = "Cohere API key used for Bearer token authentication. Source this from an ODC site property.")] string apiKey
    );

    [OSAction(
        Description = "Ranks a list of candidate image vectors against a query vector using dot-product similarity and returns the top matches. Because all vectors produced by EmbedImage and EmbedText are unit-normalized, dot product equals cosine similarity. Candidates with corrupt or mismatched vectors are skipped without throwing. Optionally filters out the query image itself and results below a minimum score.",
        ReturnName = "Results",
        ReturnDescription = "List of matching images sorted from most to least similar. Each result contains the entity Id and a similarity Score in the range -1.0 to 1.0, where 1.0 means identical and 0.0 means unrelated. Typical strong matches score above 0.80.")]
    List<ImageSearchResult> SearchVector(
        [OSParameter(Description = "The query vector to search with. Pass the VectorBytes from an image entity record for image-to-image search, or the output of EmbedText for text-to-image search. Must have been produced by EmbedImage or EmbedText using the same model variant as the stored candidates.")] byte[] queryVector,
        [OSParameter(Description = "List of candidate image records to score against the query. Each record must contain a non-empty Id and a VectorBytes blob produced by EmbedImage with the same model variant. Candidates with null or mismatched vectors are skipped automatically.")] List<VectorCandidate> candidates,
        [OSParameter(Description = "Maximum number of results to return. Pass 5 to get the 5 most similar images. If fewer valid candidates exist than topK, all valid candidates are returned.")] int topK,
        [OSParameter(Description = "Id of the query image record if it is present in the candidates list, so it is excluded from results. This prevents an image from appearing as its own top match. Leave empty if the query image is not in the candidate list or when searching by text. Defaults to empty string.")] string excludeId = "",
        [OSParameter(Description = "Minimum similarity score a candidate must reach to be included in results. Range is -1.0 to 1.0. Default is 0, which excludes unrelated and opposite images. Raise this (e.g. to 0.5) to return only strong matches.")] decimal minScore = 0m
    );
}
