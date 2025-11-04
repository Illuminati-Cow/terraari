#!/bin/bash

# Terraari Mod Development Setup Script
# This script sets up the necessary tooling for tModLoader mod development

# Initialize result tracking
RESULTS=()
STEP_COUNT=0

# Function to track step results
track_result() {
    local step_name="$1"
    local success="$2"
    local description="$3"
    
    if [[ "$success" == "true" ]]; then
        RESULTS+=("✅ $step_name")
    else
        RESULTS+=("❌ $step_name")
    fi
    ((STEP_COUNT++))
}

echo "🚀 Setting up Terraari Mod Development Environment"
echo "=================================================="

# Function to check if a command exists
command_exists() {
    command -v "$1" >/dev/null 2>&1
}

# Function to detect OS
detect_os() {
    if [[ "$OSTYPE" == "linux-gnu"* ]]; then
        echo "linux"
    elif [[ "$OSTYPE" == "darwin"* ]]; then
        echo "macos"
    elif [[ "$OSTYPE" == "msys" ]] || [[ "$OSTYPE" == "win32" ]]; then
        echo "windows"
    else
        echo "unknown"
    fi
}

OS=$(detect_os)
echo "Detected OS: $OS"

# Check and install .NET SDK
echo ""
echo "📦 Checking .NET SDK..."
DOTNET_SUCCESS=false
if command_exists dotnet; then
    DOTNET_VERSION=$(dotnet --version)
    echo "✅ .NET SDK found: $DOTNET_VERSION"

    # Check if version is sufficient for tModLoader (needs .NET 6.0+)
    if [[ "$(printf '%s\n' "$DOTNET_VERSION" "6.0" | sort -V | head -n1)" != "6.0" ]]; then
        echo "⚠️  .NET version might be too old for tModLoader. Consider upgrading to .NET 6.0 or later."
    fi
    DOTNET_SUCCESS=true
else
    echo "❌ .NET SDK not found."
    echo "Installing .NET SDK..."

    case $OS in
        linux)
            # Install .NET on Linux
            wget https://packages.microsoft.com/config/ubuntu/20.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
            sudo dpkg -i packages-microsoft-prod.deb
            rm packages-microsoft-prod.deb
            sudo apt-get update
            sudo apt-get install -y dotnet-sdk-8.0
            ;;
        macos)
            # Install .NET on macOS
            if command_exists brew; then
                brew install --cask dotnet-sdk
            else
                echo "Please install Homebrew first: https://brew.sh/"
                echo "Then run: brew install --cask dotnet-sdk"
                exit 1
            fi
            ;;
        windows)
            echo "Please download and install .NET SDK from: https://dotnet.microsoft.com/download"
            echo "Or use winget: winget install Microsoft.DotNet.SDK.8"
            ;;
        *)
            echo "Please install .NET SDK manually from: https://dotnet.microsoft.com/download"
            ;;
    esac
fi

# Verify .NET installation
if command_exists dotnet; then
    echo "✅ .NET SDK verification successful"
    dotnet --info | head -10 || true
    DOTNET_SUCCESS=true
else
    echo "❌ .NET SDK installation failed"
    DOTNET_SUCCESS=false
    exit 1
fi

track_result "Verified .NET SDK availability" "$DOTNET_SUCCESS"

# Check for Git
echo ""
echo "📦 Checking Git..."
GIT_SUCCESS=false
if command_exists git; then
    GIT_VERSION=$(git --version)
    echo "✅ Git found: $GIT_VERSION"
    GIT_SUCCESS=true
else
    echo "❌ Git not found."
    echo "Installing Git..."

    case $OS in
        linux)
            sudo apt-get update
            sudo apt-get install -y git
            ;;
        macos)
            if command_exists brew; then
                brew install git
            else
                echo "Please install Homebrew first: https://brew.sh/"
                echo "Then run: brew install git"
                xcode-select --install 2>/dev/null || true
            fi
            ;;
        windows)
            echo "Please download and install Git from: https://git-scm.com/download/win"
            ;;
        *)
            echo "Please install Git manually from: https://git-scm.com/"
            ;;
    esac
    
    # Check if installation succeeded
    if command_exists git; then
        GIT_SUCCESS=true
        echo "✅ Git installed successfully"
    else
        GIT_SUCCESS=false
    fi
fi

track_result "Verified Git availability" "$GIT_SUCCESS"

# Ensure we're at the git repository root
echo ""
echo "📁 Checking repository location..."
REPO_SUCCESS=false
if [[ -d ".git" ]]; then
    REPO_NAME=$(basename "$(git rev-parse --show-toplevel)")
    if [[ "$REPO_NAME" == "Terraari" ]] || [[ "$REPO_NAME" == "terraari" ]]; then
        echo "✅ At Terraari repository root"
        REPO_SUCCESS=true
    else
        echo "❌ Not in Terraari repository. Current repo: $REPO_NAME"
        echo "Please navigate to the Terraari repository root directory and run this script again."
        REPO_SUCCESS=false
        exit 1
    fi
else
    echo "❌ Not in a git repository"
    echo "Please navigate to the Terraari repository root directory and run this script again."
    REPO_SUCCESS=false
    exit 1
fi

track_result "Confirmed Git repository location" "$REPO_SUCCESS"

# Create dotnet-tools.json manifest if it doesn't exist
echo ""
echo "🔧 Setting up .NET tools manifest..."
MANIFEST_SUCCESS=false
if [[ ! -f ".config/dotnet-tools.json" ]]; then
    mkdir -p .config
    cat > .config/dotnet-tools.json << 'EOF'
{
  "version": 1,
  "isRoot": true,
  "tools": {
    "husky": {
      "version": "0.7.4",
      "commands": [
        "husky"
      ]
    },
    "csharpier": {
      "version": "0.28.2",
      "commands": [
        "dotnet-csharpier"
      ]
    }
  }
}
EOF
    echo "✅ Created .NET tools manifest"
    MANIFEST_SUCCESS=true
else
    echo "✅ .NET tools manifest already exists"
    MANIFEST_SUCCESS=true
fi

track_result "Set up .NET tools manifest" "$MANIFEST_SUCCESS"

# Restore .NET tools
echo ""
echo "📦 Installing .NET tools..."
TOOLS_SUCCESS=false
if dotnet tool restore; then
    echo "✅ .NET tools installed successfully"
    TOOLS_SUCCESS=true
else
    echo "❌ Failed to install .NET tools"
    TOOLS_SUCCESS=false
    exit 1
fi

track_result "Installed Husky.Net and CSharpier" "$TOOLS_SUCCESS"
# Set up Husky.Net git hooks
echo ""
echo "🪝 Setting up Husky.Net git hooks..."
HUSKY_SUCCESS=false
if dotnet husky install; then
    echo "✅ Husky.Net installed successfully"
    HUSKY_SUCCESS=true
else
    echo "❌ Failed to install Husky.Net"
    HUSKY_SUCCESS=false
    exit 1
fi

track_result "Registered git hooks with Husky" "$HUSKY_SUCCESS"

# Test the hooks
echo ""
echo "🧪 Testing git hooks..."
PRECOMMIT_SUCCESS=false
echo "Testing pre-commit hook..."
if dotnet husky run --group pre-commit; then
    echo "✅ Pre-commit hook test passed"
    PRECOMMIT_SUCCESS=true
else
    echo "❌ Pre-commit hook test failed"
    PRECOMMIT_SUCCESS=false
fi

PREPUSH_SUCCESS=false
echo "Testing pre-push hook..."
if dotnet husky run --group pre-push; then
    echo "✅ Pre-push hook test passed"
    PREPUSH_SUCCESS=true
else
    echo "❌ Pre-push hook test failed"
    PREPUSH_SUCCESS=false
fi

track_result "Set up pre-commit hook (runs CSharpier)" "$PRECOMMIT_SUCCESS"
track_result "Set up pre-push hook (builds project)" "$PREPUSH_SUCCESS"
track_result "Verified hook functionality" "$([[ "$PRECOMMIT_SUCCESS" == "true" && "$PREPUSH_SUCCESS" == "true" ]] && echo "true" || echo "false")"

# Print results
NO_FAILURES=true
echo ""
echo "Setup Results:"
echo "=================="
for result in "${RESULTS[@]}"; do
    echo "$result"
    if [[ "$result" == *"❌"* ]]; then
        NO_FAILURES=false
    fi
done

if [[ "$NO_FAILURES" == "false" ]]; then
    echo ""
    echo "⚠️  Some steps failed during setup. Please review the results above and address any issues."
    exit 1
fi
echo ""
echo "🎉 Setup complete!"
echo ""
echo "Your development environment is now configured with:"
echo "- Code formatting on commit (CSharpier)"
echo "- Build verification on push"
echo "- Automated tooling management"
echo ""
echo "Happy coding! 🚀"
