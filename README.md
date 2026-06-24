# MultimodalEmbeddingLibrary

ODC External Logic library for image search using the [Cohere Embed v4](https://docs.cohere.com/reference/embed) API. Embeds images and text queries into a shared vector space, ranks candidates by dot-product similarity — no external search infrastructure required.

Powers the **NativeImageSearch** Forge component.

## Actions

| Action | Description |
|---|---|
| `EmbedImage` | Converts a raw image binary to a normalized float32 vector. Call once per image at ingestion time. |
| `EmbedText` | Converts a natural language query to a vector in the same space as embedded images. |
| `SearchVector` | Ranks candidate vectors against a query vector. Returns top K matches with similarity scores. |

## Prerequisites

- [OutSystems Developer Cloud](https://www.outsystems.com/platform/developer-cloud/) tenant
- [Cohere API key](https://dashboard.cohere.com/api-keys) with Embed v4 access

## Install

1. Download `MultimodalEmbeddingLibrary.zip` from the [latest release](https://github.com/michaeldeguzman/MultimodalEmbeddingLibrary-Forge/releases/latest).
2. In ODC Portal, go to **External Logic** and click **Upload**.
3. Upload the zip — ODC will validate and publish the library.
4. Add the library to your ODC app.

## Build from Source

Requires [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```bash
dotnet publish MultimodalEmbeddingLibrary.csproj -c Release -o publish
cd publish && zip -r ../MultimodalEmbeddingLibrary.zip . && cd ..
```

Upload the resulting zip to ODC Portal → **External Logic** → **Upload**.

## Configuration

Set these as **ODC Site Properties** in the consuming app, then pass as action parameters:

| Site Property | Value |
|---|---|
| `CohereApiKey` | Your Cohere API key |
| `CohereEmbedEndpoint` | `https://api.cohere.com/v2/embed` |
| `EmbedModelVersion` | `cohere-embed-v4` (or a dimension variant — see below) |

Never hardcode the API key. Always source from a site property.

## Model Variants

All images and queries in one corpus must use the same model variant.

| `modelVersion` | Output dimensions | Byte length |
|---|---|---|
| `cohere-embed-v4` or `cohere-embed-v4-1536` | 1536 | 6144 |
| `cohere-embed-v4-1024` | 1024 | 4096 |
| `cohere-embed-v4-512` | 512 | 2048 |
| `cohere-embed-v4-256` | 256 | 1024 |

## Usage

### Ingestion (once per image)

1. Fetch the image binary.
2. Call `EmbedImage(imageBinary, modelVersion, apiEndpoint, apiKey)`.
3. Store the returned `VectorBytes` in a `Binary Data` attribute on your image entity.

### Text-to-image search

1. Call `EmbedText(queryText, modelVersion, apiEndpoint, apiKey)`.
2. Fetch all image entity records (Id + VectorBytes).
3. Call `SearchVector(queryVector, candidates, topK)`.
4. Display results ordered by `Score`.

### Image-to-image search

1. Retrieve `VectorBytes` from the query image entity record.
2. Call `SearchVector(queryVector, candidates, topK, excludeId: queryImageId)`.
3. `excludeId` prevents the query image appearing as its own top match.

## SearchVector Parameters

| Parameter | Type | Required | Default | Description |
|---|---|---|---|---|
| `queryVector` | Binary Data | Yes | — | Output of `EmbedImage` or `EmbedText` |
| `candidates` | List of VectorCandidate | Yes | — | Records with Id + VectorBytes |
| `topK` | Integer | Yes | — | Max results to return |
| `excludeId` | Text | No | `""` | Id to exclude from results |
| `minScore` | Decimal | No | `0` | Minimum similarity threshold (-1.0 to 1.0) |

## Supported Image Formats

JPEG, PNG, GIF, WebP. Pass the raw binary — do not pre-encode to base64.

## License

MIT
