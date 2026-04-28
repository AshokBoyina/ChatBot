# NASS AI Chatbot

A Blazor Server web application providing an AI-powered chat interface using Azure AI Foundry with RAG (Retrieval-Augmented Generation) support.

## Quick Start (Mock Mode)

Run immediately without Azure credentials:

```powershell
dotnet run --project .\ChatBot.Web\ChatBot.Web.csproj
```

The chat widget appears in the bottom-right corner. Mock mode provides canned responses for testing the UI.

---

## Azure AI Foundry Setup (Real Mode)

### Prerequisites

- Azure subscription
- Azure OpenAI resource deployed
- Azure AI Search resource deployed
- Documents uploaded to a search index

### Step 1: Create Azure Resources

#### 1. Azure OpenAI Service (AI Foundry)

1. Go to [Azure Portal](https://portal.azure.com)
2. Search **"Azure OpenAI"** → Create
3. Select region (e.g., East US, West Europe)
4. Pricing tier: Standard
5. After deployment, go to **Azure AI Foundry portal**: `https://ai.azure.com`
6. Navigate to **Models** → Deploy **gpt-4** or **gpt-4o**
7. Note the **Deployment name** you chose

**Collect these values:**
| Azure Portal Location | appsettings.json Key | Example |
|------------------------|---------------------|---------|
| Keys and Endpoint → Endpoint | `Endpoint` | `https://nass-openai.openai.azure.com` |
| Keys and Endpoint → KEY 1 | `ApiKey` | `abc123def456...` |
| Model deployment name | `DeploymentName` in Applications | `gpt-4-nass` |

#### 2. Azure AI Search

1. In Azure Portal, search **"Azure AI Search"** → Create
2. Select same region as OpenAI resource
3. Pricing tier: Basic or Standard
4. After deployment, go to resource

**Collect these values:**
| Azure Portal Location | appsettings.json Key | Example |
|------------------------|---------------------|---------|
| Overview → URL | `SearchEndpoint` | `https://nass-search.search.windows.net` |
| Settings → Keys → Primary admin key | `SearchApiKey` | `xyz789abc...` |

#### 3. Create Search Index with Your Documents

**Option A: Azure AI Foundry Portal (Recommended)**

1. Go to [Azure AI Foundry](https://ai.azure.com)
2. Navigate to your project → **Chat playground**
3. Click **"Add your data"** tab
4. Select **"Add a new data source"**
5. Choose **Azure AI Search** as target
6. Upload files (PDF, Word, TXT, HTML, CSV supported)
7. Configure:
   - Index name: `nass-reports` (note this for appsettings)
   - Chunk size: 512 tokens (default)
   - Semantic search: Enabled
8. Wait for indexing to complete (status shows "Ready")

**Option B: Programmatic Indexing**
```bash
# Use Azure AI Search SDK or REST API to push documents
# See: https://docs.microsoft.com/azure/search/search-get-started-python
```

---

### Step 2: Configure appsettings.json

Edit `/ChatBot.Web/appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "AzureFoundry": {
    "UseMock": false,
    "Endpoint": "https://YOUR-RESOURCE.openai.azure.com",
    "ApiKey": "YOUR-AZURE-OPENAI-KEY",
    "ApiVersion": "2024-08-01-preview",
    "SearchEndpoint": "https://YOUR-SEARCH.search.windows.net",
    "SearchApiKey": "YOUR-SEARCH-ADMIN-KEY",
    "Applications": [
      {
        "Id": "nass-reports",
        "Name": "NASS Reports",
        "Description": "Agricultural statistics reports and publications",
        "DeploymentName": "gpt-4",
        "SearchIndexName": "nass-reports-index"
      }
    ]
  }
}
```

#### Field Mapping Reference

| appsettings.json | Azure Portal Source | Example |
|------------------|---------------------|---------|
| `UseMock` | N/A | `false` for real Azure, `true` for mock mode |
| `Endpoint` | Azure OpenAI → Keys and Endpoint → Endpoint | `https://nass-openai.openai.azure.com` |
| `ApiKey` | Azure OpenAI → Keys and Endpoint → KEY 1 | `abc123...` |
| `ApiVersion` | N/A | Keep as `2024-08-01-preview` |
| `SearchEndpoint` | Azure AI Search → Overview → URL | `https://nass-search.search.windows.net` |
| `SearchApiKey` | Azure AI Search → Settings → Keys → Primary admin key | `xyz789...` |

#### Application Fields

| Field | Purpose | Required |
|-------|---------|----------|
| `Id` | Unique identifier for this app | Yes |
| `Name` | Display name in dropdown | Yes |
| `Description` | Shown below header | Yes |
| `DeploymentName` | Must match your Azure OpenAI deployment name | Yes |
| `SearchIndexName` | Azure AI Search index name; set `null` for no RAG | No |

---

## Multiple Applications

You can configure multiple knowledge bases that users switch between:

```json
{
  "AzureFoundry": {
    "UseMock": false,
    "Endpoint": "https://nass-openai.openai.azure.com",
    "ApiKey": "your-openai-key",
    "ApiVersion": "2024-08-01-preview",
    "SearchEndpoint": "https://nass-search.search.windows.net",
    "SearchApiKey": "your-search-key",
    "Applications": [
      {
        "Id": "nass-reports",
        "Name": "NASS Reports",
        "Description": "Agricultural statistics reports and publications",
        "DeploymentName": "gpt-4",
        "SearchIndexName": "nass-reports-index"
      },
      {
        "Id": "census-data",
        "Name": "Census Data",
        "Description": "Census of Agriculture historical data",
        "DeploymentName": "gpt-4",
        "SearchIndexName": "census-index"
      },
      {
        "Id": "general",
        "Name": "General Assistant",
        "Description": "General purpose AI without document search",
        "DeploymentName": "gpt-4",
        "SearchIndexName": null
      }
    ]
  }
}
```

The UI shows a dropdown selector when `Applications.Count > 1`.

### Index per Application Strategy

| Use Case | SearchIndexName | Behavior |
|----------|----------------|----------|
| Different document sets | Different index names | Each app queries its own documents |
| Same model, no RAG | `null` | General AI answers without documents |
| Same documents, different model | Same index, different `DeploymentName` | Use cheaper model for some queries |

---

## How RAG Works

```
User: "What was corn production in Iowa 2023?"
        ↓
Azure AI Search: Find top 5 chunks from nass-reports-index
        ↓
Inject into GPT-4 prompt:
"Context: [chunk1] [chunk2] [chunk3] [chunk4] [chunk5]

Question: What was corn production in Iowa 2023?
Answer:"
        ↓
GPT-4 generates answer with citations
        ↓
Display answer + [1][2] citation links
```

**Document Chunking:** Azure automatically splits your PDFs into ~512 token chunks with overlap for context preservation.

**Semantic Search:** Uses vector embeddings to find conceptually related content, not just keyword matches.

---

## Project Structure

```
ChatBot/
├── ChatBot.Web/                 # Blazor Server application
│   ├── Components/
│   │   ├── Pages/Home.razor      # Landing page
│   │   ├── Shared/ChatWidget.razor   # Chat UI component
│   │   └── App.razor             # Root HTML
│   ├── Services/
│   │   ├── AzureFoundryService.cs   # Real Azure API calls
│   │   ├── MockAzureFoundryService.cs  # Mock for testing
│   │   └── IAzureFoundryService.cs    # Interface
│   ├── Models/
│   │   └── ChatModels.cs         # Data models
│   ├── appsettings.json          # Configuration
│   └── Program.cs                # DI registration
└── ChatBot.sln
```

---

## Troubleshooting

### "Azure AI Foundry is not yet configured" error
- `UseMock` is `false` but endpoint still shows `REPLACE_...`
- Fill in actual endpoint values from Azure Portal

### "Connection failed" or timeout
- Check firewall rules in Azure OpenAI (allow your IP)
- Verify `Endpoint` has no trailing slash
- Ensure region matches between OpenAI and Search resources

### No citations appearing
- Verify `SearchIndexName` is not `null`
- Check `SearchEndpoint` and `SearchApiKey` are filled
- Confirm index contains documents (Azure Portal → Search explorer)

### Index not found
- Index names are case-sensitive: `nass-reports` ≠ `NASS-Reports`
- Wait for indexing status to show "Ready" in Azure portal

---

## Environment-Specific Settings

Use `appsettings.Development.json` for local development overrides:

```json
{
  "AzureFoundry": {
    "UseMock": true
  }
}
```

Use `appsettings.Production.json` for production (excluded from source control):

```json
{
  "AzureFoundry": {
    "UseMock": false,
    "Endpoint": "https://prod-openai.openai.azure.com",
    "ApiKey": "${AZURE_OPENAI_KEY}",
    "SearchEndpoint": "https://prod-search.search.windows.net",
    "SearchApiKey": "${AZURE_SEARCH_KEY}"
  }
}
```

For production secrets, use:
- Azure Key Vault
- Environment variables
- .NET User Secrets (`dotnet user-secrets init`)

---

## Supported Document Formats for RAG

Upload these to Azure AI Search:
- **PDF** (.pdf) - Most common for reports
- **Microsoft Word** (.docx)
- **Text files** (.txt)
- **HTML** (.html)
- **Markdown** (.md)
- **CSV** (.csv) - For tabular data
- **JSON** (.json)

**Not supported:** Scanned/image PDFs without OCR (run through OCR first)

---

## Additional Resources

- [Azure OpenAI Service documentation](https://docs.microsoft.com/azure/cognitive-services/openai/)
- [Azure AI Search documentation](https://docs.microsoft.com/azure/search/)
- [RAG pattern explanation](https://docs.microsoft.com/azure/ai-services/openai/concepts/use-your-data)
- [Azure AI Foundry portal](https://ai.azure.com)
