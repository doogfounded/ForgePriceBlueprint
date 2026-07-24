# Makefile for running the end-to-end flow: generate JSON with the C# tool, build the C++ runtime, and run it.

.PHONY: all generate build run clean
all: run

# Run the C# generator (requires .NET SDK)
generate:
	dotnet run --project Forge/Forge.csproj

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

# Build the C++ runtime (both the CLI executable and the shared dynamic library)
build:
	g++ -std=c++17 PriceBlueprint/src/main.cpp PriceBlueprint/src/addons.cpp -IPriceBlueprint/include $(VCPKG_FLAGS) -o price_blueprint$(EXE_EXT)
	g++ -shared -fPIC -std=c++17 PriceBlueprint/src/library.cpp PriceBlueprint/src/addons.cpp -IPriceBlueprint/include $(VCPKG_FLAGS) -o price_blueprint.$(LIB_EXT)

# Generate then build then run
run: generate build
	./price_blueprint$(EXE_EXT)

web:
	dotnet run --project Forge/Forge.csproj --web

clean:
	rm -f price_blueprint price_blueprint.exe price_blueprint.dll price_blueprint.so
