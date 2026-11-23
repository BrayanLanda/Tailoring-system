#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd -- "$script_dir/.." && pwd)"
cd "$repo_root"

if [[ ! -f .env ]]; then
    echo "Missing .env. Copy .env.example to .env and fill in local values." >&2
    exit 1
fi

set -a
source .env
set +a

: "${WORK_ORDER_DB_PASSWORD:?Set WORK_ORDER_DB_PASSWORD in .env}"
: "${RABBITMQ_USER:?Set RABBITMQ_USER in .env}"
: "${RABBITMQ_PASSWORD:?Set RABBITMQ_PASSWORD in .env}"

set_secret() {
    local project="$1"
    local key="$2"
    local value="$3"
    dotnet user-secrets set "$key" "$value" --project "$project" >/dev/null
}

work_order_connection="Host=localhost;Port=5433;Database=work_order_db;Username=${WORK_ORDER_DB_USER:-sastreria};Password=${WORK_ORDER_DB_PASSWORD}"
tailor_connection="Host=localhost;Port=5434;Database=tailor_db;Username=${TAILOR_DB_USER:-sastreria};Password=${TAILOR_DB_PASSWORD:-$WORK_ORDER_DB_PASSWORD}"
identity_connection="Host=localhost;Port=5435;Database=identity_db;Username=${IDENTITY_DB_USER:-sastreria};Password=${IDENTITY_DB_PASSWORD:-$WORK_ORDER_DB_PASSWORD}"

work_order_project="src/Services/WorkOrderService/WorkOrderService.Api/WorkOrderService.Api.csproj"
tailor_project="src/Services/TailorService/TailorService.Api/TailorService.Api.csproj"
identity_project="src/Services/IdentityService/IdentityService.Api/IdentityService.Api.csproj"

set_secret "$work_order_project" "ConnectionStrings:WorkOrderDb" "$work_order_connection"
set_secret "$work_order_project" "RABBITMQ_HOST" "${RABBITMQ_HOST:-localhost}"
set_secret "$work_order_project" "RABBITMQ_PORT" "${RABBITMQ_PORT:-5672}"
set_secret "$work_order_project" "RABBITMQ_USER" "$RABBITMQ_USER"
set_secret "$work_order_project" "RABBITMQ_PASSWORD" "$RABBITMQ_PASSWORD"
set_secret "$work_order_project" "RABBITMQ_MAX_RETRIES" "${RABBITMQ_MAX_RETRIES:-3}"
set_secret "$work_order_project" "RABBITMQ_RETRY_DELAY_MILLISECONDS" "${RABBITMQ_RETRY_DELAY_MILLISECONDS:-5000}"
set_secret "$tailor_project" "ConnectionStrings:TailorDb" "$tailor_connection"
set_secret "$tailor_project" "RABBITMQ_HOST" "${RABBITMQ_HOST:-localhost}"
set_secret "$tailor_project" "RABBITMQ_PORT" "${RABBITMQ_PORT:-5672}"
set_secret "$tailor_project" "RABBITMQ_USER" "$RABBITMQ_USER"
set_secret "$tailor_project" "RABBITMQ_PASSWORD" "$RABBITMQ_PASSWORD"
set_secret "$identity_project" "ConnectionStrings:IdentityDb" "$identity_connection"

if [[ -n "${AdminSeed__Email:-}" && -n "${AdminSeed__Password:-}" ]]; then
    set_secret "$identity_project" "AdminSeed:Email" "$AdminSeed__Email"
    set_secret "$identity_project" "AdminSeed:Password" "$AdminSeed__Password"
fi

if [[ -n "${Jwt__SigningKey:-}" ]]; then
    set_secret "$identity_project" "Jwt:SigningKey" "$Jwt__SigningKey"
fi

echo "Local .NET user secrets configured for WorkOrderService, TailorService, and IdentityService."
