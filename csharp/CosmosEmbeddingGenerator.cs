using Azure;
using Azure.AI.OpenAI;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using OpenAI.Embeddings;
using System.Security.Cryptography;
using System.Text;

namespace CosmosEmbeddingGenerator
{
    public class CosmosEmbeddingGeneratorFunction
    {
        private readonly ILogger _logger;
        private readonly AzureOpenAIClient _openAiClient;
        private readonly EmbeddingClient _embeddingClient;
        private readonly int _dimensions;
        private readonly string _vectorProperty;
        private readonly string _hashProperty;
        private readonly string _propertyToEmbed;

        public CosmosEmbeddingGeneratorFunction(ILoggerFactory loggerFactory, IConfiguration configuration)
        {
            _logger = loggerFactory.CreateLogger<CosmosEmbeddingGeneratorFunction>();

            string openAiEndpoint = configuration["OpenAiEndpoint"] ?? throw new InvalidOperationException("'OpenAiEndpoint' must be defined.");
            string openAiKey = configuration["OpenAiKey"] ?? throw new InvalidOperationException("'OpenAiKey' must be defined.");
            string deploymentName = configuration["OpenAiDeploymentName"] ?? throw new InvalidOperationException("'OpenAiDeploymentName' must be defined.");
            string openAiDimensions = configuration["OpenAiDimensions"] ?? throw new InvalidOperationException("'OpenAiDimensions' must be defined.");
            string vectorProperty = configuration["VectorProperty"] ?? throw new InvalidOperationException("'VectorProperty' must be defined.");
            string hashProperty = configuration["HashProperty"] ?? throw new InvalidOperationException("'HashProperty' must be defined.");
            string propertyToEmbed = configuration["PropertyToEmbed"] ?? throw new InvalidOperationException("'PropertyToEmbed' must be defined.");

            _openAiClient = new AzureOpenAIClient(new Uri(openAiEndpoint), new AzureKeyCredential(openAiKey));
            _embeddingClient = _openAiClient.GetEmbeddingClient(deploymentName);
            _dimensions = int.Parse(openAiDimensions);
            _vectorProperty = vectorProperty;
            _hashProperty = hashProperty;
            _propertyToEmbed = propertyToEmbed;
        }

        [Function(nameof(CosmosEmbeddingGeneratorFunction))]
        [CosmosDBOutput(
            databaseName: "%DatabaseName%",
            containerName: "%ContainerName%",
            Connection = "CosmosDBConnection")]
        public async Task<object?> Run(
            [CosmosDBTrigger(
                databaseName: "%DatabaseName%",
                containerName: "%ContainerName%",
                Connection = "CosmosDBConnection",
                LeaseContainerName = "leases",
                CreateLeaseContainerIfNotExists = true)] IReadOnlyList<dynamic> input,
            FunctionContext context)
        {
            var toBeUpdated = new List<(JObject doc, string hash, string toEmbed)>();
            if (input?.Count > 0)
            {
                _logger.LogInformation("Documents modified: {count}", input.Count);
                for (int i = 0; i < input.Count; i++)
                {
                    var document = input[i];
                    //parse document into a json object
                    JObject jsonDocument = JObject.Parse(document.ToString());

                    // Check hash value to see if document is new or modified
                    if (IsDocumentNewOrModified(jsonDocument, out var newHash))
                    {
                        toBeUpdated.Add((jsonDocument, newHash, jsonDocument.Property(_propertyToEmbed)?.ToString() ?? string.Empty));
                    }
                }
            }

            if (toBeUpdated.Count > 0)
            {
                _logger.LogInformation("Updating embeddings for: {count}", toBeUpdated.Count);

                // Generate all the embeddings in a single batch.
                var embeddings = await GetEmbeddingsAsync(
                    toBeUpdated.Select(tbu => tbu.toEmbed));

                var outputDocuments = new List<string>(toBeUpdated.Count);
                for (int i = 0; i < toBeUpdated.Count; i++)
                {
                    var (jsonDocument, hash, toEmbed) = toBeUpdated[i];

                    //Cleanse the document of system and vector properties
                    jsonDocument = RemoveSystemAndVectorProperties(jsonDocument);

                    // Add the embeddings to the document
                    jsonDocument[_vectorProperty] = JArray.FromObject(embeddings[i]);

                    // Add the hash to the document
                    jsonDocument[_hashProperty] = hash;

                    // Serialize the result and add it to our output.
                    outputDocuments.Add(jsonDocument.ToString());
                }

                return outputDocuments;
            }

            return null;
        }

        private async Task<List<float[]>> GetEmbeddingsAsync(IEnumerable<string> inputs)
        {
            var options = new EmbeddingGenerationOptions
            {
                Dimensions = _dimensions
            };

            var response = await _embeddingClient.GenerateEmbeddingsAsync(inputs, options);
            var results = new List<float[]>(response.Value.Count);
            foreach (var e in response.Value)
            {
                results.Add(e.ToFloats().ToArray());
            }
            return results;
        }

        private JObject RemoveSystemAndVectorProperties(JObject document)
        {
            document.Remove(_vectorProperty);
            document.Remove("_rid");
            document.Remove("_self");
            document.Remove("_etag");
            document.Remove("_attachments");
            document.Remove("_lsn");
            document.Remove("_ts");
            return document;
        }

        private bool IsDocumentNewOrModified(JObject jsonDocument, out string newHash)
        {
            // Generate a hash of the property to be embedded.
            newHash = ComputeJsonHash(jsonDocument);

            var existingProperty = jsonDocument.Property(_hashProperty);
            // No hash property, document is new
            if (existingProperty is null)
            {
                return true;
            }

            // Document has changed, process it
            if (newHash != existingProperty.ToString())
                return true;

            // Document has not changed, skip processing
            return false;
        }

        private string ComputeJsonHash(JObject jsonDocument)
        {
            // Re-serialize the JSON to canonical form (sorted keys, no extra whitespace)
            var property = jsonDocument.Property(_propertyToEmbed)?.ToString() ?? string.Empty;

            // Compute SHA256 hash
            byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(property));
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        }
    }
}
