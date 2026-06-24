# MultimodalEmbeddingLibrary

An ODC External Logic library that powers multimodal image search using the [Cohere Embed v4](https://docs.cohere.com/reference/embed) API. Embed images and text queries into a shared vector space, then rank candidates by dot-product similarity — all from ODC logic, with no external search infrastructure.

Designed for use with the **NativeImageSearch** Forge component.

## Actions

| Action | Description |
|---|---|
| `EmbedImage` | Converts a raw image binary to a normalized float32 vector. Call once per image at ingestion time. Store the returned bytes in your image entity. |
| `EmbedText` | Converts a natural language query to a vector in the same space as embedded images. Use for text-to-image search. |
| `SearchVector` | Ranks a list of candidate vectors against a query vector. Returns the top K matches with similarity scores. |

## Prerequisites

- An [OutSystems Developer Cloud](https://www.outsystems.com/platform/developer-cloud/) (ODC) tenant
- A [Cohere API key](https://dashboard.cohere.com/api-keys) with access to Embed v4

## Upload to ODC

1. Download `MultimodalEmbeddingLibrary.zip` from this repository.
2. In ODC Portal, go to **External Logic** and click **Upload**.
3. Upload the zip — ODC will validate and publish the library.
4. Add the library to your ODC app.

## Build from Source

Requires [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```bash
dotnet publish MultimodalEmbeddingLibrary.csproj -c Release -o publish
# Zip the publish/ output — that zip is what you upload to ODC
```

## Configuration

Configure these as **ODC Site Properties** in your app, then pass them as action parameters:

| Site Property | Value |
|---|---|
| `CohereApiKey` | Your Cohere API key |
| `CohereEmbedEndpoint` | `https://api.cohere.com/v2/embed` |
| `EmbedModelVersion` | `cohere-embed-v4` (or a dimension variant — see below) |

Never hardcode the API key in logic. Always source it from a site property.

## Model Variants

All images and queries in one corpus must use the same model variant.

| `modelVersion` value | Output dimensions | Byte length |
|---|---|---|
| `cohere-embed-v4` or `cohere-embed-v4-1536` | 1536 | 6144 |
| `cohere-embed-v4-1024` | 1024 | 4096 |
| `cohere-embed-v4-512` | 512 | 2048 |
| `cohere-embed-v4-256` | 256 | 1024 |

## Usage Pattern

### Ingestion (once per image)

1. Fetch the image binary from its source.
2. Call `EmbedImage(imageBinary, modelVersion, apiEndpoint, apiKey)`.
3. Store the returned `VectorBytes` in a `Binary Data` attribute on your image entity.

### Text-to-image search

1. Call `EmbedText(queryText, modelVersion, apiEndpoint, apiKey)` with the user's search query.
2. Fetch all image entity records (Id + VectorBytes).
3. Call `SearchVector(queryVector, candidates, topK)`.
4. Display the returned results ordered by `Score`.

### Image-to-image search

1. Retrieve the `VectorBytes` of the query image directly from the entity.
2. Call `SearchVector(queryVector, candidates, topK, excludeId: queryImageId)`.
3. The `excludeId` parameter prevents the query image from appearing as its own top match.

## SearchVector Parameters

| Parameter | Type | Required | Description |
|---|---|---|---|
| `queryVector` | Binary Data | Yes | Output of `EmbedImage` or `EmbedText` |
| `candidates` | List of VectorCandidate | Yes | Records with Id + VectorBytes |
| `topK` | Integer | Yes | Max results to return |
| `excludeId` | Text | No | Id to exclude (use for image-to-image) |
| `minScore` | Decimal | No | Minimum score threshold. Default: 0 |

## Supported Image Formats

JPEG, PNG, GIF, WebP. Pass the raw binary directly — do not base64-encode it before calling `EmbedImage`.

## License

MIT
