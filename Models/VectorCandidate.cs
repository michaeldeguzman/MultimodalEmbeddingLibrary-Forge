using OutSystems.ExternalLibraries.SDK;

namespace MultimodalEmbeddingLibrary.Models;

[OSStructure(Description = "A candidate image record passed to SearchVector for similarity scoring. Populate a list of these from your image entity and pass it to SearchVector.")]
public struct VectorCandidate
{
    [OSStructureField(DataType = OSDataType.Text, Description = "The unique Id of the image entity record. Returned in SearchVector results so you can identify which image matched.")]
    public string Id { get; set; }

    [OSStructureField(DataType = OSDataType.BinaryData, Description = "The embedding vector for this image, stored as a little-endian float32 byte array. This is the value produced by EmbedImage and stored in the VectorBytes attribute of your image entity.")]
    public byte[] VectorBytes { get; set; }
}
