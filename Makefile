# Makefile for running the end-to-end flow: generate JSON with the C# tool, build the C++ runtime, and run it.

.PHONY: all test build run clean
all: test build run

# Run the test suite (requires .NET SDK)
test:
	dotnet test Forge.Tests --nologo -v q

# Define include paths for vcpkg dependencies (e.g. nlohmann/json)
VCPKG_ROOT ?= D:/Desktop/git-stuff/vcpkg
VCPKG_INCLUDE ?= $(VCPKG_ROOT)/installed/x64-windows/include

# Check if the path exists, otherwise fall back to WSL mount folder
ifeq ($(wildcard $(VCPKG_INCLUDE)),)
    VCPKG_INCLUDE = /mnt/d/Desktop/git-stuff/vcpkg/installed/x64-windows/include
endif

ifneq ($(wildcard $(VCPKG_INCLUDE)),)
    VCPKG_FLAGS = -I$(VCPKG_INCLUDE)
endif

ifeq ($(OS),Windows_NT)
    LIB_EXT = dll
    EXE_EXT = .exe
else
    LIB_EXT = so
    EXE_EXT = 
endif

# Build the C++ runtime shared dynamic library
build:
	g++ -shared -fPIC -static -static-libgcc -static-libstdc++ -std=c++17 PriceBlueprint/src/library.cpp -IPriceBlueprint/include $(VCPKG_FLAGS) -o price_blueprint.$(LIB_EXT)

# Run studio desktop app
run: build
	dotnet run --project Forge/Forge.csproj

app:
	dotnet run --project Forge/Forge.csproj

clean:
	rm -f price_blueprint.dll price_blueprint.so
