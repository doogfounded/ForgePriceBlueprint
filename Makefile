# Makefile for running the end-to-end flow: generate JSON with the C# tool, build the C++ runtime, and run it.

.PHONY: all generate build run clean
all: run

# Run the C# generator (requires .NET SDK)
generate:
	dotnet run --project Forge/Forge.csproj

# Build the C++ runtime (requires g++ and nlohmann/json header available in the include path)
build:
	g++ -std=c++17 PriceBlueprint/src/main.cpp -IPriceBlueprint/include -o price_blueprint

# Generate then build then run
run: generate build
	./price_blueprint

clean:
	rm -f price_blueprint
