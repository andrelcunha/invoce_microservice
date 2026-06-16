#!/bin/sh
set -e

echo "Applying database migrations..."
./efbundle
echo "Migrations done. Starting application..."
exec dotnet InvoiceMicroservice.Api.dll
