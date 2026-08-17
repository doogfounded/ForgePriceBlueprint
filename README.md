# ForgePriceBlueprint: The Pricing Operating System

[![License](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Build Status](https://img.shields.io/badge/Build-Passing-brightgreen.svg)](./scripts/run_end_to_end.sh)
[![API Docs](https://img.shields.io/badge/API-OpenAPI%203.0-blue.svg)](docs/API_DEVELOPER_GUIDE.md)

ForgePriceBlueprint is a sophisticated, extensible pricing engine designed to act as a centralized "Pricing Operating System." It provides a robust, rule-based framework for calculating complex pricing structures by assembling, validating, and simulating pricing blueprints against real-world business rules.

## 💡 Overview

The system decouples pricing logic from application code. Instead of hardcoding pricing rules, developers define a structured **Blueprint** (JSON schema) that dictates how base prices are adjusted by various factors (discounts, surcharges, tiers, caps). The engine then processes these blueprints through a series of configurable rules to produce accurate, auditable final prices.

### Key Features:
*   **Rule-Based Pricing:** Supports multiple, composable pricing rules (e.g., `PercentageAdjustment`, `TieredPricing`, `PriceCap`).
*   **Simulation & Validation:** Provides an API endpoint to simulate pricing scenarios and validate blueprints for anomalies (e.g., negative pricing, extreme markups).
*   **Extensibility:** The core logic is built using a fluent builder pattern, making it easy to add new rule types and business logic.
*   **Audit Trail:** Every calculation generates a detailed, step-by-step audit trail, ensuring full transparency for compliance and debugging.

## 🚀 Getting Started

Follow these steps to set up and run the pricing engine locally.

### Prerequisites
*   .NET SDK (Targeting net10.0)
*   Node.js (for web UI/client testing)
*   A local database/environment for running simulations (if applicable).

### 1. Setup and Build
Use the provided scripts to set up the environment and compile the core engine.

```bash
# Run the end-to-end setup and build process
./scripts/run_end_to_end.sh
```

### 2. Running the Server
The core engine runs as an ASP.NET Core web service, exposing the pricing API.

```bash
# Start the web server (runs on http://localhost:5000)
dotnet run --project Forge/Forge.csproj --web
```
The server will automatically open the web UI in your browser.

## 📚 API Reference

The primary interface for interacting with the pricing engine is the REST API, which is fully documented in the [Developer API Guide](docs/API_DEVELOPER_GUIDE.md).

### Key Endpoints:
*   **`GET /api/blueprint`**: Retrieves the currently active pricing blueprint JSON.
*   **`POST /api/blueprint`**: Overwrites the active blueprint with a new JSON payload.
*   **`POST /api/simulate`**: Runs a list of pricing scenarios against the active blueprint, returning detailed results and audit trails.
*   **`POST /api/validate`**: Validates the blueprint schema and runs a comprehensive grid of test scenarios to check for pricing anomalies.

## 🏗️ Architecture Deep Dive

The system is composed of several key parts:

1.  **`Forge/Program.cs`**: The main entry point. It handles command-line arguments to determine whether to run the web server or to assemble a blueprint programmatically.
2.  **`BlueprintBuilder`**: A fluent builder class responsible for constructing the `PriceBlueprintDto` object by chaining rule additions.
3.  **`PriceBlueprintDto`**: The core data model representing the entire pricing structure, containing a list of various `PricingRuleDto` implementations.
4.  **`docs/API_DEVELOPER_GUIDE.md`**: Contains detailed examples and specifications for integrating the engine into external applications (Python, JavaScript, Curl).

## 🧪 Testing

The project includes unit and integration tests for the pricing logic.

*   **Unit Tests**: Located in `Forge.Tests/`.
*   **End-to-End Tests**: Use the `scripts/run_end_to_end.sh` script to execute a full cycle test.
