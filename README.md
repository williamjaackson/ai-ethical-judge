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

## Project layout

```
AiEthicalJudge.slnx          Solution file
src/AiEthicalJudge/          MVC web app
  Controllers/               Request handlers
  Models/                    View models
  Views/                     Razor views
  wwwroot/                   Static assets (CSS, JS, Bootstrap, jQuery)
  Program.cs                 App entry point and service configuration
  appsettings.json           Configuration
```

## Contributing

All changes land via pull request — direct pushes to `master` are blocked. Pull requests are merged with **squash and merge** only, and the branch is deleted afterwards.

```bash
git checkout -b feature/my-change
# ...make changes...
git push -u origin feature/my-change
gh pr create --base master
```
