# OneGood

OneGood is an open source platform for surfacing one impactful action per day, built with .NET 10, .NET MAUI, and a modern API/worker architecture. It is designed to be privacy-friendly, extensible, and easy to contribute to.

## Features

- .NET MAUI mobile app (cross-platform)
- ASP.NET Core API backend
- Background worker for scheduled tasks
- AI-powered content (supports Groq, Gemini, Anthropic, and more)
- PostgreSQL database support
- Privacy-first: only anonymous data for streak/progress tracking
- Localized UI (EN/DE)
- Open source under the MIT License

## Project Structure

- `src/OneGood.Core` - Shared models and interfaces
- `src/OneGood.Api` - ASP.NET Core Web API
- `src/OneGood.Maui` - .NET MAUI cross-platform app
- `src/OneGood.Infrastructure` - Data access, AI, and external integrations
- `src/OneGood.Workers` - .NET Worker Service for background jobs
- `tests/OneGood.Tests.Unit` - Unit tests
- `tests/OneGood.Tests.Integration` - Integration tests

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/)
- [Node.js](https://nodejs.org/) (for frontend assets, if you want to build them)
- PostgreSQL (local or cloud)
- (Optional) Android/iOS/Mac/Windows device for MAUI app

### Setup

1. **Clone the repository:**
```sh
git clone https://github.com/cdilorenzo/OneGood.git
cd OneGood
```

2. **Configure environment variables:**

   The following secrets must be set as environment variables (never committed to the repo):

   - `ConnectionStrings__DefaultConnection` (PostgreSQL connection string)
   - `AI__Groq__ApiKey` (Groq AI key, if using Groq)
   - `AI__Gemini__ApiKey` (Gemini AI key, if using Gemini)
   - `AI__Anthropic__ApiKey` (Anthropic AI key, if using Anthropic)

   Example for local development (PowerShell/bash):
```sh
$env:ConnectionStrings__DefaultConnection="Host=...;Database=...;Username=...;Password=..."
$env:AI__Groq__ApiKey="your-groq-key"
```

   Or set these in your cloud provider's environment (e.g., Render, Azure, etc).

3. **Restore and build:**
```sh
dotnet restore
dotnet build
```

4. **Run the API:**
```sh
dotnet run --project src/OneGood.Api/OneGood.Api.csproj
```

5. **Run the MAUI app:**
```sh
dotnet build src/OneGood.Maui/OneGood.Maui.csproj
# Then deploy to your device/emulator via Visual Studio or CLI
```

6. **Run the worker service:**
```sh
dotnet run --project src/OneGood.Workers/OneGood.Workers.csproj
```

7. **Run tests:**
```sh
dotnet test
```

## Contributing

Contributions are welcome! Please open issues or pull requests for bugs, features, or improvements.

- Follow .NET and C# best practices.
- Do not commit secrets or API keys.
- Keep the code privacy-friendly and accessible.

## License

This project is licensed under the [MIT License](LICENSE).
You are free to use, modify, and distribute this software with attribution.
