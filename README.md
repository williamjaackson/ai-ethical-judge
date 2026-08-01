# ai-ethical-judge

An ASP.NET Core Web App (MVC) targeting .NET 10.

## Prerequisites

The .NET 10 SDK (10.0.302 or later).

**macOS (Homebrew)**

```bash
brew install dotnet
```

The Homebrew formula does not set `DOTNET_ROOT`, which some tooling needs. Add this to your `~/.zshrc`:

```bash
export DOTNET_ROOT="/opt/homebrew/opt/dotnet/libexec"
```

Alternatively, `brew install --cask dotnet-sdk` installs to `/usr/local/share/dotnet` (the layout Microsoft's docs assume) but requires `sudo`.

**Other platforms**

Download from [dotnet.microsoft.com/download](https://dotnet.microsoft.com/download), or use your package manager.

Verify the install:

```bash
dotnet --version
```

## Setup

```bash
git clone https://github.com/williamjaackson/ai-ethical-judge.git
cd ai-ethical-judge
dotnet restore
```

## Build

```bash
dotnet build
```

## Run

```bash
dotnet run --project src/AiEthicalJudge
```

The app is then served at:

- http://localhost:5039
- https://localhost:7274 — use `--launch-profile https`

To trust the local HTTPS development certificate:

```bash
dotnet dev-certs https --trust
```

The browser app is at `/app` — for example <http://localhost:5039/app/>.

## Configuration

Judging needs an OpenAI API key. Without one the app starts, but any request
that touches the session fails. Keep the key out of `appsettings.json`:

```bash
dotnet user-secrets --project src/AiEthicalJudge set "OpenAI:ApiKey" "sk-..."
```

Or set the `OpenAI__ApiKey` environment variable. The model and reasoning effort
are set in `appsettings.json` under `OpenAI`.

## Project layout

```
AiEthicalJudge.slnx          Solution file
src/AiEthicalJudge/          MVC web app
  Controllers/               Request handlers
  Hubs/                      SignalR hubs
  Models/                    Domain and view models
  Services/                  Session, judging and LLM services
  Views/                     (empty) Razor views
  wwwroot/app/               The browser app, served at /app
  Program.cs                 App entry point and service configuration
  appsettings.json           Configuration
```

## How a session flows

The browser app is three pages under `/app`, sharing one session id:

1. **`index.html`** starts a session and resets the server's state.
2. **`configure.html`** posts the speech and looks criteria to judge against.
3. **`judge.html`** streams the transcript over a WebSocket and posts a camera
   frame every few seconds.

The server judges the session every five seconds, scoring each configured
criterion from 1 to 5. Each judgement is pushed to any open **`results.html`**
over the `/resultsHub` SignalR hub, which also polls
`GET /api/app/{sessionId}/results` so it keeps up if the live feed is
unavailable.

Judging calls OpenAI, so it needs an API key — see [Configuration](#configuration).

## Contributing

All changes land via pull request — direct pushes to `master` are blocked. Pull requests are merged with **squash and merge** only, and the branch is deleted afterwards.

```bash
git checkout -b feature/my-change
# ...make changes...
git push -u origin feature/my-change
gh pr create --base master
```
