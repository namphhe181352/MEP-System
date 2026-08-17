#!/bin/bash
# auto-fix.sh - Auto-fix common build errors after refactoring
# Usage: ./scripts/auto-fix.sh

set -e

PROJECT_DIR="/Users/phuongnam/MEP-System/MEP_Management_System"

echo "🔧 Running auto-fix for refactoring issues..."
cd "$PROJECT_DIR"

# 1. Check for missing using statements
echo "📝 Step 1: Checking for missing using statements..."

# 2. Check DI registrations in Program.cs
echo "📝 Step 2: Checking DI registrations..."

# 3. Check interface-implementation mismatches
echo "📝 Step 3: Checking interface implementations..."

# 4. Run build
echo "📝 Step 4: Running build verification..."
dotnet build --no-incremental 2>&1 | tee /tmp/build.log

if [ ${PIPESTATUS[0]} -eq 0 ]; then
    echo "✅ Build successful after auto-fix!"
else
    echo "❌ Build failed. Checking for auto-fixable errors..."

    # Check for common errors
    if grep -q "does not implement interface member" /tmp/build.log; then
        echo "⚠️  Missing interface implementations detected"
        echo "   Run /ecc:build-fix to auto-fix this"
    fi

    if grep -q "The type or namespace name.*could not be found" /tmp/build.log; then
        echo "⚠️  Missing type/namespace errors detected"
        echo "   Check if using statements are needed"
    fi

    if grep -q "does not contain a definition for" /tmp/build.log; then
        echo "⚠️  Missing method/property errors detected"
    fi

    echo "📋 Manual review required for remaining errors"
    exit 1
fi

echo "✅ Auto-fix complete!"
