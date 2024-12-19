using Azure;
using Azure.AI.OpenAI;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
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
            if (input != null && input.Count > 0)
            {
                _logger.LogInformation("Documents modified: {count}", input.Count);
                foreach (var document in input)
                {
                    //parse document into a json object
                    JObject jsonDocument = JObject.Parse(document.ToString());
                    
                    //Cleanse the document of system and vector properties
                    jsonDocument = RemoveSystemAndVectorProperties(jsonDocument);

                    // Check hash value to see if document is new or modified
                    if(IsDocumentNewOrModified(jsonDocument))
                    { 
                        
                        // Generate a hash of the new/modified document
                        string hash = ComputeJsonHash(jsonDocument);

                        // Generate embeddings on the document
                        float[] embeddings = await GetEmbeddingsAsync(jsonDocument.Property(_propertyToEmbed)?.ToString() ?? string.Empty);

                        // Add the embeddings to the document
                        jsonDocument[_vectorProperty] = JArray.FromObject(embeddings);

                        // Add the hash to the document
                        jsonDocument[_hashProperty] = hash;

                        //Serialize and return the document
                        return jsonDocument.ToString();
                    }
                }
            }
            return null;
        }

        private async Task<float[]> GetEmbeddingsAsync(string input)
        {
            var options = new EmbeddingGenerationOptions
            {
                Dimensions = _dimensions
            };

            var response = await _embeddingClient.GenerateEmbeddingsAsync([input], options);

            var embeddings = response.Value[0].ToFloats();

            // Convert ReadOnlyMemory<float> to float[]
            float[] embedding = embeddings.ToArray();

            return embedding;
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

        private bool IsDocumentNewOrModified(JObject jsonDocument)
        {
            // No hash property, document is new
            if (jsonDocument[_hashProperty] == null)
                return true;

            // Save the existing hash
            string existingHash = jsonDocument[_hashProperty]!.ToString();

            // Generate a hash of the property to be embedded.
            string newHash = ComputeJsonHash(jsonDocument);

            // Document has changed, process it
            if (newHash != existingHash)
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
