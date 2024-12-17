import os
import logging
import hashlib
import json
import azure.functions as func
from openai import AzureOpenAI


# Retrieve application setings from local.settings.json
OPENAI_ENDPOINT = os.environ["OpenAiEndpoint"]
OPENAI_KEY = os.environ["OpenAiKey"]
OPENAI_MODEL = os.environ["OpenAiDeploymentName"]
OPENAI_DIMENSIONS = int(os.environ["OpenAiDimensions"])
COSMOS_DB_CONNECTION = os.environ["CosmosDBConnection"]
COSMOS_VECTOR_PROPERTY = os.environ["VectorProperty"]
COSMOS_HASH_PROPERTY = os.environ["HashProperty"]

# Initialize OpenAI client
OPENAI_CLIENT = AzureOpenAI(
    azure_endpoint = OPENAI_ENDPOINT, 
    api_key = OPENAI_KEY,
    api_version = "2024-02-01"
    )


# Initialize the function app
app = func.FunctionApp()

@app.function_name(name="cosmos-embedding-generator")
@app.cosmos_db_output(
    arg_name="output", 
    database_name="%DatabaseName%", 
    container_name="%ContainerName%", 
    connection="CosmosDBConnection")
@app.cosmos_db_trigger(
    arg_name="input", 
    database_name="%DatabaseName%", 
    container_name="%ContainerName%",
    connection="CosmosDBConnection")
async def cosmos_embedding_generator(input: func.DocumentList, output: func.Out[func.Document]):
    logging.info('Python CosmosDB triggered.')

    if input:
        logging.info('Documents modified: %s', len(input))
        for document in input:
            json_document = document.to_dict()

            # Cleanse the document of system and vector properties
            json_document = remove_system_and_vector_properties(json_document)

            # Check hash value to see if document is new or modified
            if is_document_new_or_modified(json_document):
                # Generate a hash of the new/modified document
                hash_value = compute_json_hash(json_document)

                # Generate embeddings on the document
                embeddings = get_embeddings(json.dumps(json_document))

                # Add the embeddings to the document
                json_document[COSMOS_VECTOR_PROPERTY] = embeddings

                # Add the hash to the document
                json_document[COSMOS_HASH_PROPERTY] = hash_value


                # Upsert the document back to Cosmos DB
                output.set(func.Document.from_json(json.dumps(json_document)))
                

def get_embeddings(input_text: str) -> list:
    
    response = OPENAI_CLIENT.embeddings.create(
        input = input_text, 
        dimensions = OPENAI_DIMENSIONS,
        model = OPENAI_MODEL)
    
    embeddings = response.model_dump()
    return embeddings['data'][0]['embedding']
    

def remove_system_and_vector_properties(document: dict) -> dict:
    document.pop(COSMOS_VECTOR_PROPERTY, None)
    document.pop("_rid", None)
    document.pop("_self", None)
    document.pop("_etag", None)
    document.pop("_attachments", None)
    document.pop("_lsn", None)
    document.pop("_ts", None)
    return document

def is_document_new_or_modified(json_document: dict) -> bool:
    # No hash property, document is new
    if COSMOS_HASH_PROPERTY not in json_document:
        return True

    # Save the existing hash
    existing_hash = json_document[COSMOS_HASH_PROPERTY]

    # Generate a hash of the document to compare to the existing hash (removes the existing hash when computed)
    new_hash = compute_json_hash(json_document)

    # Document has changed, process it
    if new_hash != existing_hash:
        return True

    # Document has not changed, skip processing
    return False

def compute_json_hash(json_document: dict) -> str:
    # Remove the hash property if it exists
    if COSMOS_HASH_PROPERTY in json_document:
        json_document.pop(COSMOS_HASH_PROPERTY)

    # Re-serialize the JSON to canonical form (sorted keys, no extra whitespace)
    canonical_json = json.dumps(json_document, sort_keys=True)

    # Compute SHA256 hash
    hash_object = hashlib.sha256(canonical_json.encode())
    return hash_object.hexdigest()