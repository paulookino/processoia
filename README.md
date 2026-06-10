# ProcessIA — Legal Document Analysis with AI

> Análise de processos jurídicos em minutos, não horas.

[![.NET](https://img.shields.io/badge/.NET%208-512BD4?style=flat-square&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com)
[![License](https://img.shields.io/badge/license-MIT-blue?style=flat-square)](LICENSE)
[![Docker](https://img.shields.io/badge/Docker-ready-2496ED?style=flat-square&logo=docker&logoColor=white)](docker-compose.yml)

Full-stack B2B SaaS that processes legal documents using AI — built with ASP.NET Core, Azure, and LLM integration.

---

## The Problem

Legal professionals spend hours manually reading, extracting, and summarizing process documents. ProcessIA automates that pipeline: upload a PDF, get a structured, AI-generated analysis in minutes.

## Architecture

```
Client (Razor Pages)
      │
      ▼
REST API (ASP.NET Core)
      │
      ├── AuthController    — JWT auth, role-based authorization
      ├── ProcessesController — upload, status, history
      ├── ReportsController   — download structured PDF reports
      └── WebhooksController  — async processing callbacks
            │
            ▼
      Processing Pipeline
            │
            ├── OcrService          — extract text from scanned PDFs
            ├── AiAnalysisService   — LLM-powered analysis
            ├── BlobStorageService  — Azure Blob Storage
            └── PdfExporter         — generate structured reports
```

## Tech Stack

| Layer | Technology |
|-------|-----------|
| API | ASP.NET Core 8, Web API, JWT |
| ORM | Entity Framework Core, SQL Server |
| AI | LLM integration, OCR pipeline |
| Storage | Azure Blob Storage |
| Frontend | ASP.NET Razor Pages |
| Infra | Docker, Docker Compose |

## Running Locally

```bash
git clone https://github.com/paulookino/processoia
cd processoia

# Configure environment
cp src/ProcessIA.API/appsettings.json src/ProcessIA.API/appsettings.Development.json
# Add your Azure Blob connection string and LLM API key

# Run with Docker Compose
docker-compose up --build
```

API: `http://localhost:5000`
Web: `http://localhost:5001`

## Project Structure

```
src/
├── ProcessIA.API/          # REST API
│   ├── Controllers/        # Auth, Processes, Reports, Webhooks
│   ├── Services/           # OCR, AI Analysis, Blob Storage, PDF Export
│   ├── Models/             # User, LegalProcess, Report
│   └── Data/               # EF Core DbContext
└── ProcessIA.Web/          # Razor Pages frontend
    ├── Pages/
    │   ├── Auth/           # Login, Register
    │   ├── Dashboard/      # Process list, metrics
    │   ├── Upload/         # Document upload
    │   └── Report/         # Analysis results
    └── Services/           # API client
```

## License

MIT
