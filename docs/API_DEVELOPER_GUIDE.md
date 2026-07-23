# Forge Price Blueprint Studio - Developer API Guide

Welcome to the Forge Price Blueprint API integration guide. This document explains how developers can run the pricing web service, download the API specification, and integrate the calculation and validation routines directly into external custom apps and scripts.

---

## 🚀 Running the API Server

The pricing engine API is hosted by the C# ASP.NET Core assembler project `Forge`. 

To launch the server, execute the following from the repository root:
```bash
dotnet run --project Forge/Forge.csproj --web --port 5000
```
This boots the HTTP server on `http://localhost:5000/` and hosts the visual designer web UI.

---

## 📄 OpenAPI Specification

The server automatically hosts a static OpenAPI 3.0.0 JSON specification mapping all endpoints, payloads, and validation formats.
* **Spec Endpoint**: `GET http://localhost:5000/openapi.json`

You can import this file directly into tools like **Postman**, **Swagger UI**, or standard SDK generator clients.

---

## 🛠️ Endpoints Reference

### 1. `GET /api/blueprint`
Downloads the active pricing blueprint JSON structure currently loaded on the server.
* **Response `200 OK`**: A JSON object matching the [blueprint schema](file:///d:/Desktop/sln/ForgePriceBlueprint/blueprint_schema.json).

### 2. `POST /api/blueprint`
Overwrites the active blueprint configuration on disk.
* **Payload**: Blueprint JSON.
* **Response `200 OK`**: `{ "status": "success", "message": "Blueprint saved successfully." }`
* **Response `400 Bad Request`**: Schema validation failed. Returns `{ "error": "Validation failed", "details": [ ... ] }`.

### 3. `POST /api/simulate`
Executes simulation contexts against the active blueprint using the compiled pricing engine.
* **Payload**: An array of scenario objects:
  ```json
  [
    {
      "scenario_name": "Standard Promotion Check",
      "context": {
        "base_price": 200,
        "quantity": 15,
        "region": "EU",
        "is_partner": true
      }
    }
  ]
  ```
* **Response `200 OK`**: An array containing final calculations and detailed step-by-step audit records:
  ```json
  [
    {
      "scenarioName": "Standard Promotion Check",
      "basePrice": 200,
      "finalPrice": 171,
      "auditTrail": [
        {
          "ruleName": "Base Configuration Rate",
          "description": "Initialized base price from context/default.",
          "inputPrice": 0,
          "outputPrice": 200,
          "adjustment": 200
        },
        {
          "ruleName": "Partner Discount",
          "description": "Partner Channel 10% discount",
          "inputPrice": 200,
          "outputPrice": 180,
          "adjustment": -20
        },
        {
          "ruleName": "International Tax Surcharge",
          "description": "International regional sales tax of 15% for orders of 10 or more units",
          "inputPrice": 180,
          "outputPrice": 171,
          "adjustment": -9
        }
      ]
    }
  ]
  ```

### 4. `POST /api/validate`
Validates a blueprint JSON schema and runs scenario testing grids to check for anomalies (negative pricing, volume discounts inversion, extreme markups).
* **Payload**: (Optional) JSON blueprint payload to check. If payload is empty, validates the active blueprint file on disk.
* **Response `200 OK`**: Returns validation details:
  ```json
  {
    "valid": true,
    "scenariosTested": 72,
    "anomalies": []
  }
  ```

---

## 💻 Developer Code Examples

Here are code snippets showing how to connect to the pricing simulation endpoints programmatically:

### A. Python Client
```python
import requests
import json

url = "http://localhost:5000/api/simulate"
payload = [
    {
        "scenario_name": "VIP Large Order",
        "context": {
            "base_price": 250,
            "quantity": 120,
            "region": "EU",
            "is_partner": True
        }
    }
]

headers = {
    "Content-Type": "application/json"
}

response = requests.post(url, headers=headers, data=json.dumps(payload))

if response.status_code == 200:
    results = response.json()
    for run in results:
        print(f"Scenario: {run['scenarioName']}")
        print(f"  Base Price:  ${run['basePrice']:.2f}")
        print(f"  Final Price: ${run['finalPrice']:.2f}")
        print("  Audit Trail:")
        for idx, step in enumerate(run['auditTrail']):
            print(f"    {idx+1}. [{step['ruleName']}] Adjustment: {step['adjustment']:+}")
else:
    print(f"Simulation failed with status: {response.status_code}")
    print(response.text)
```

### B. JavaScript / Node.js Client
```javascript
const url = 'http://localhost:5000/api/simulate';

const payload = [
  {
    scenario_name: "Regular Small Order",
    context: {
      base_price: 150,
      quantity: 5,
      region: "US",
      is_partner: false
    }
  }
];

fetch(url, {
  method: 'POST',
  headers: {
    'Content-Type': 'application/json'
  },
  body: JSON.stringify(payload)
})
.then(res => {
  if (!res.ok) throw new Error(`HTTP Error ${res.status}`);
  return res.json();
})
.then(runs => {
  runs.forEach(run => {
    console.log(`Scenario: ${run.scenarioName}`);
    console.log(`  Final calculated price: $${run.finalPrice}`);
  });
})
.catch(err => console.error("Request failed:", err));
```

### C. Bash Curl Request
```bash
curl -X POST http://localhost:5000/api/simulate \
  -H "Content-Type: application/json" \
  -d '[{"scenario_name": "API Curl Test", "context": {"base_price": 100, "quantity": 1}}]'
```
