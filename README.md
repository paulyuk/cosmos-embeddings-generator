# Cosmos DB Embeddings Generator

This sample shows how to use a Cosmos DB Trigger and Output Binding in Azure Functions to automatically generate vector embeddings on new or updated data.

## Details (more to come)
- This sample demonstrates in both C# and Python
- It uses the native Azure OpenAI SDK to generate embeddings
- To prevent the Cosmos DB Trigger from endless loops it generates a hash of the document and stores it with the generated embeddings. Then checks it when the trigger is called when initially generating and storing the vector.

## To Do:
- azd deployment. Modify the `parameters.bicep` to create the account, database and container with a vector index and container policy. The output is also injected into the local.settings.json for both the C# and Python samples allowing you to F5 the sample immediately.

## Getting Started:

- azd init
- azd up
- Open VS Code and navigate to either csharp or python directory.
- F5
- Open a browser and navigate to the container in Cosmos Data Explorer
- Create a new document and save.
