#!/bin/bash
# compare-responses.sh - Compare API responses before/after refactoring
# Usage: ./scripts/compare-responses.sh <endpoint> [token]

ENDPOINT=$1
TOKEN=${2:-$TOKEN}
BASE_URL="http://localhost:5100"

if [ -z "$ENDPOINT" ]; then
    echo "Usage: ./scripts/compare-responses.sh <endpoint> [token]"
    echo "Example: ./scripts/compare-responses.sh /api/suppliers eyJhbGciOiJI..."
    exit 1
fi

RESPONSE_DIR="/Users/phuongnam/MEP-System/tests/responses"
BEFORE_FILE="${RESPONSE_DIR}${ENDPOINT}_before.json"
AFTER_FILE="${RESPONSE_DIR}${ENDPOINT}_after.json"

mkdir -p "$RESPONSE_DIR"

echo "🔍 Comparing responses for: ${ENDPOINT}"

# Check if before response exists
if [ ! -f "$BEFORE_FILE" ]; then
    echo "❌ Before response not found: $BEFORE_FILE"
    echo "   Run this script BEFORE refactoring to capture baseline"
    exit 1
fi

# Capture current response
echo "📡 Fetching current response..."
curl -s -w "\n%{http_code}" "${BASE_URL}${ENDPOINT}" \
    -H "Authorization: Bearer $TOKEN" \
    -o "$AFTER_FILE" 2>/dev/null

HTTP_CODE=$(tail -n1 "$AFTER_FILE")
echo "   HTTP Status: $HTTP_CODE"

# Remove HTTP code from file
sed -i '' '$ d' "$AFTER_FILE"

# Compare key fields (ignoring timestamps, IDs)
echo "🔍 Comparing response structure..."

# Extract and compare keys
BEFORE_KEYS=$(cat "$BEFORE_FILE" | jq -r 'keys | sort | .[]' 2>/dev/null || echo "")
AFTER_KEYS=$(cat "$AFTER_FILE" | jq -r 'keys | sort | .[]' 2>/dev/null || echo "")

if [ "$BEFORE_KEYS" != "$AFTER_KEYS" ]; then
    echo "❌ Response structure CHANGED!"
    echo ""
    echo "Expected keys:"
    echo "$BEFORE_KEYS"
    echo ""
    echo "Actual keys:"
    echo "$AFTER_KEYS"
    echo ""
    echo "⚠️  WARNING: Response schema changed - Frontend may break!"
    exit 1
fi

# Compare data structure (first item)
BEFORE_DATA_KEYS=$(cat "$BEFORE_FILE" | jq -r '.data.items[0] | keys | sort | .[]' 2>/dev/null || echo "")
AFTER_DATA_KEYS=$(cat "$AFTER_FILE" | jq -r '.data.items[0] | keys | sort | .[]' 2>/dev/null || echo "")

if [ "$BEFORE_DATA_KEYS" != "$AFTER_DATA_KEYS" ]; then
    echo "⚠️  Data item structure changed (different fields)"
fi

# Check success flag
BEFORE_SUCCESS=$(cat "$BEFORE_FILE" | jq -r '.success' 2>/dev/null || echo "unknown")
AFTER_SUCCESS=$(cat "$AFTER_FILE" | jq -r '.success' 2>/dev/null || echo "unknown")

if [ "$BEFORE_SUCCESS" != "$AFTER_SUCCESS" ]; then
    echo "❌ Success flag changed: $BEFORE_SUCCESS -> $AFTER_SUCCESS"
    exit 1
fi

echo "✅ Response schema matches!"
echo "   Success: $AFTER_SUCCESS"
exit 0
