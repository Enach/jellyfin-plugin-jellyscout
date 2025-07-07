#!/bin/bash

# JellyScout Plugin Build Script
# This script builds the plugin and creates a distribution package

set -e

echo "🔨 Building JellyScout Plugin..."

# Configuration
PROJECT_NAME="Jellyfin.Plugin.JellyScout"
VERSION="0.0.3"
BUILD_DIR="bin/Release/net8.0"
DIST_DIR="dist"
PACKAGE_NAME="JellyScout-v${VERSION}"

# Clean previous builds
echo "🧹 Cleaning previous builds..."
rm -rf bin/ obj/ ${DIST_DIR}/

# Restore dependencies
echo "📦 Restoring dependencies..."
dotnet restore

# Build the project
echo "🏗️  Building project..."
dotnet build --configuration Release --no-restore

# Create distribution directory
echo "📁 Creating distribution package..."
mkdir -p ${DIST_DIR}/${PACKAGE_NAME}

# Copy built files
echo "📋 Copying files..."
cp ${BUILD_DIR}/*.dll ${DIST_DIR}/${PACKAGE_NAME}/
cp ${BUILD_DIR}/*.pdb ${DIST_DIR}/${PACKAGE_NAME}/
cp manifest.json ${DIST_DIR}/${PACKAGE_NAME}/

# Create zip package
echo "🗜️  Creating zip package..."
cd ${DIST_DIR}
zip -r ${PACKAGE_NAME}.zip ${PACKAGE_NAME}/
cd ..

# Create checksums
echo "🔐 Creating checksums..."
cd ${DIST_DIR}
sha256sum ${PACKAGE_NAME}.zip > ${PACKAGE_NAME}.zip.sha256
cd ..

# Display results
echo "✅ Build completed successfully!"
echo ""
echo "📦 Package: ${DIST_DIR}/${PACKAGE_NAME}.zip"
echo "🔐 Checksum: ${DIST_DIR}/${PACKAGE_NAME}.zip.sha256"
echo ""
echo "Installation Instructions:"
echo "1. Extract the zip file"
echo "2. Copy all files to your Jellyfin plugins directory:"
echo "   - Windows: C:\\ProgramData\\Jellyfin\\Server\\plugins\\JellyScout\\"
echo "   - Linux: /var/lib/jellyfin/plugins/JellyScout/"
echo "   - macOS: ~/.local/share/jellyfin/plugins/JellyScout/"
echo "3. Restart Jellyfin server"
echo "4. Configure the plugin in Jellyfin Admin → Plugins → JellyScout"
echo ""
echo "📚 For detailed installation instructions, see INSTALLATION.md" 