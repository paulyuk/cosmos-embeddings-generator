# Cosmos DB Embeddings Generator

This sample shows how to use a Cosmos DB Trigger and Output Binding in Azure Functions to automatically generate vector embeddings on new or updated data.

## Details (more to come)
- This sample demonstrates in both C# and Python
- It uses the native Azure OpenAI SDK to generate embeddings
- To prevent the Cosmos DB Trigger from endless loops it generates a hash of the document and stores it with the generated embeddings. Then checks it when the trigger is called when initially generating and storing the vector.

## To Do:
- azd deployment. Modify the `parameters.bicep` to create the account, database and container with a vector index and container policy. The output is also injected into the local.settings.json for both the C# and Python samples allowing you to F5 the sample immediately.

## Getting Started:

### Deployment

1. Open a terminal and navigate to where you would like to clone this solution

1. From the terminal, navigate to either the `csharp` or `python` directory in this solution.

1. Enable scripts to create local settings file after deployment

    Mac/Linux:
    
    ```bash
    chmod +x ./infra/scripts/*.sh 
    ```
    
    Windows:
    
    ```Powershell
    set-executionpolicy remotesigned
    ```

1. Provision the Azure services, build your local solution container, and deploy the application.

   ```bash
   azd up
   ```

1. Validate that the post install script generated valid `local.settings.json` in the same folder (csharp or python).  

    Python:
    ```json
    {
      "IsEncrypted": false,
      "Values": {
        "AzureWebJobsStorage": "UseDevelopmentStorage=true",
        "FUNCTIONS_WORKER_RUNTIME": "python",
        "COSMOS_CONNECTION__accountEndpoint": "https://{my-cosmos-account}.documents.azure.com:443/",
        "COSMOS_DATABASE_NAME": "{database-name}",
        "COSMOS_CONTAINER_NAME": "{container-name}",
        "COSMOS_VECTOR_PROPERTY": "{vector-property-name}",
        "COSMOS_HASH_PROPERTY": "{hash-property-name}",
        "COSMOS_PROPERTY_TO_EMBED": "{property-to-generate-vectors-for}",
        "OPENAI_ENDPOINT": "https://{my-open-ai-account}.openai.azure.com/",
        "OPENAI_DEPLOYMENT_NAME": "{my-embeddings-deployment-name}",
        "OPENAI_DIMENSIONS": "1536"
      }
    }
    ```

    C# (.NET):
    ```json
    {
      "IsEncrypted": false,
      "Values": {
        "AzureWebJobsStorage": "UseDevelopmentStorage=true",
        "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
        "COSMOS_CONNECTION__accountEndpoint": "https://{my-cosmos-account}.documents.azure.com:443/",
        "COSMOS_DATABASE_NAME": "{database-name}",
        "COSMOS_CONTAINER_NAME": "{container-name}",
        "COSMOS_VECTOR_PROPERTY": "{vector-property-name}",
        "COSMOS_HASH_PROPERTY": "{hash-property-name}",
        "COSMOS_PROPERTY_TO_EMBED": "{property-to-generate-vectors-for}",
        "OPENAI_ENDPOINT": "https://{my-open-ai-account}.openai.azure.com/",
        "OPENAI_DEPLOYMENT_NAME": "{my-embeddings-deployment-name}",
        "OPENAI_DIMENSIONS": "1536"
      }
    }
    ```

- F5
- Open a browser and navigate to the container in Cosmos Data Explorer
- Create a new document and save.      """

The expected document/item has at least these 3 properties, and note that 'text' 
is the property that gets embedded.

Example document:
```json
{
   "id": "cosmosdb_overview_1",
   "customerId": "1",
   "text": "Azure Cosmos DB is a fully managed NoSQL, relational, and vector database. It offers single-digit millisecond response times, automatic and instant scalability, along with guaranteed speed at any scale. Business continuity is assured with SLA-backed availability and enterprise-grade security."
}
```
