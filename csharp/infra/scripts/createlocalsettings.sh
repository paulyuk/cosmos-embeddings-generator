#!/bin/bash

set -e

if [ ! -f "./app/local.settings.json" ]; then

    expected_keys=(
        "COSMOS_CONNECTION__accountEndpoint"
        "COSMOS_CONTAINER_NAME"
        "COSMOS_DATABASE_NAME"
        "COSMOS_HASH_PROPERTY"
        "COSMOS_PROPERTY_TO_EMBED"
        "COSMOS_VECTOR_PROPERTY"
        "OPENAI_DEPLOYMENT_NAME"
        "OPENAI_DIMENSIONS"
        "OPENAI_ENDPOINT"
    )

    output=$(azd env get-values)

    while IFS= read -r line; do
        for key in "${expected_keys[@]}"; do
            if [[ $line == $key=* ]]; then
                export "$key"="${line#*=}"
            fi
        done
    done <<< "$output

    cat <<EOF > ./app/local.settings.json
{
    "IsEncrypted": "false",
    "Values": {
        "AzureWebJobsStorage": "UseDevelopmentStorage=true",
        "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
            "COSMOS_CONNECTION__accountEndpoint" = "$COSMOS_CONNECTION__accountEndpoint";
            "COSMOS_CONTAINER_NAME" = "$COSMOS_CONTAINER_NAME";
            "COSMOS_DATABASE_NAME" = "$COSMOS_DATABASE_NAME";
            "COSMOS_HASH_PROPERTY" = "$COSMOS_HASH_PROPERTY";
            "COSMOS_PROPERTY_TO_EMBED" = "$COSMOS_PROPERTY_TO_EMBED";
            "COSMOS_VECTOR_PROPERTY" = "$COSMOS_VECTOR_PROPERTY";
            "OPENAI_DEPLOYMENT_NAME" = "$OPENAI_DEPLOYMENT_NAME";
            "OPENAI_DIMENSIONS" = "$OPENAI_DIMENSIONS";
            "OPENAI_ENDPOINT" = "$OPENAI_ENDPOINT";
    }
}
EOF

fi