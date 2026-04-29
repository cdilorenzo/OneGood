#!/bin/sh
set -e

dotnet OneGood.Api.dll &
dotnet OneGood.Workers.dll
