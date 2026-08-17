#!/bin/bash
# verify-refactor.sh - Verify controller refactoring
# Usage: ./scripts/verify-refactor.sh Expense

CONTROLLER=$1
SERVICE_INTERFACE="I${CONTROLLER}Service"
CONTROLLER_FILE=""

if [ -z "$CONTROLLER" ]; then
    echo "Usage: ./scripts/verify-refactor.sh <ControllerName>"
    echo "Example: ./scripts/verify-refactor.sh Expense"
    exit 1
fi

# Find controller file
if [ -f "MEP_Management_System/Controllers/${CONTROLLER}Controller.cs" ]; then
    CONTROLLER_FILE="MEP_Management_System/Controllers/${CONTROLLER}Controller.cs"
elif [ -f "MEP_Management_System/Controllers/${CONTROLLER}/${CONTROLLER}Controller.cs" ]; then
    CONTROLLER_FILE="MEP_Management_System/Controllers/${CONTROLLER}/${CONTROLLER}Controller.cs"
else
    echo "❌ Controller file not found: ${CONTROLLER}Controller"
    exit 1
fi

echo "🔍 Verifying refactor for: ${CONTROLLER}"

# 1. Check Controller NO LONGER has MEPDatabaseContext
if grep -q "private readonly MEPDatabaseContext _context" "$CONTROLLER_FILE"; then
    echo "❌ ${CONTROLLER}Controller still has _context - NOT refactored"
    exit 1
else
    echo "✅ ${CONTROLLER}Controller no longer has _context"
fi

# 2. Check Controller HAS Service injection
if grep -q "private readonly I${CONTROLLER}Service" "$CONTROLLER_FILE"; then
    echo "✅ ${CONTROLLER}Controller has service injection"
elif grep -q "private readonly.*Service" "$CONTROLLER_FILE"; then
    echo "✅ ${CONTROLLER}Controller has service injection (different name)"
else
    echo "❌ ${CONTROLLER}Controller missing service injection"
    exit 1
fi

# 3. Check Service file exists
SERVICE_FILE="MEP_Management_System/Services/${SERVICE_INTERFACE}.cs"
if [ -f "$SERVICE_FILE" ]; then
    echo "✅ ${SERVICE_INTERFACE}.cs exists"
else
    echo "❌ ${SERVICE_INTERFACE}.cs missing"
    exit 1
fi

# 4. Check Service has MEPDatabaseContext (expected pattern)
if grep -q "private readonly MEPDatabaseContext _context" "$SERVICE_FILE"; then
    echo "✅ ${SERVICE_INTERFACE} has _context (correct pattern)"
else
    echo "❌ ${SERVICE_INTERFACE} missing _context"
    exit 1
fi

# 5. Check Service has Transaction management (optional for read-only)
if grep -q "BeginTransactionAsync" "$SERVICE_FILE"; then
    echo "✅ ${SERVICE_INTERFACE} has transaction management"
else
    echo "⚠️  ${SERVICE_INTERFACE} missing transaction management (may be OK for read-only services)"
fi

echo "✅ ${CONTROLLER} refactor verified!"
exit 0
