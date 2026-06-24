using OutSystems.ExternalLibraries.SDK;

namespace MultimodalEmbeddingLibrary.Models;

[OSStructure(Description = "A single ranked result returned by SearchVector, representing one image that matched the query.")]
public struct ImageSearchResult
{
    [OSStructureField(Description = "The unique Id of the matching image entity record. Use this to fetch the full image record or navigate to the image detail screen.")]
    public string Id { get; set; }

    [OSStructureField(Description = "Dot-product similarity score between the query vector and this image's vector. Range is -1.0 to 1.0. Values near 1.0 indicate a strong visual or semantic match. Values near 0.0 indicate unrelated content. Values below 0.0 are rare and indicate opposite content.")]
    public decimal Score { get; set; }
}
