# DeletePackageVersionsAction

Löscht gezielt Paket-Versionen aus **GitHub Packages** (z. B. NuGet), gesteuert über **Glob-Filter**.  
Dabei werden paginierte API-Antworten **streamend** verarbeitet.

## Highlights

- **Streaming via `IAsyncEnumerable`**
- **Stabile Pagination**: traversiert **rückwärts** (von `last` → `prev`), damit Löschungen keine Seiten verschieben.
- **Glob-Filter**: `*`, `?`, `[]`, `[!]`, `{a,b,c}` inkl. Include/Exclude.
- **Sicher**: Jeder Versions-ID wird nur einmal versucht zu löschen (Schutz gegen doppelte Versuche).

---

## Funktionsweise (kurz)

1. `FetchAllPackageVersions.StreamVersions(...)`:
   - Ruft zuerst **nur die Link-Header** der ersten Seite ab, um `last`/`prev` zu bekommen.
   - Läuft dann **von der letzten Seite rückwärts** bis zur ersten (verhindert „Verrutschen“ der Seiten nach Löschungen).
   - Streamt jede gefundene Version (`GitHubPackage`) weiter.

2. `DeleteVersionsWithVersionNameFilter.Execute(...)`:
   - Erzeugt einmalig einen **Matcher** aus Include/Exclude-Globs.
   - Streamt alle Versionen und löscht nur jene, die matchen.
   - Zählt Erfolge/Skips/Fehlschläge und loggt am Ende eine **Summary**.

---

## Voraussetzungen

- Personal Access Token (**PAT**) mit Rechten für GitHub Packages.  
  In der Praxis brauchst du:
  - mindestens `delete:packages` (zum Löschen) (schließt auch `packages:read` ein)
  - ggf. `repo`, wenn das Paket privat ist  
  (Je nach Org/Repo-Sichtbarkeit können weitere Rechte nötig sein.)

---

## Konfiguration über Umgebungsvariablen

Diese Variablen werden von `GitHubInputs` gelesen:

| Variable                       | Pflicht | Beschreibung                                                                                 | Standard |
|-------------------------------|:------:|----------------------------------------------------------------------------------------------|:--------:|
| `INPUT_GITHUB_API_KEY`        |   ✅   | PAT mit benötigten Scopes (siehe oben).                                                      | –        |
| `INPUT_PACKAGE_TYPE`          |   ❌   | Pakettyp bei GitHub Packages (z. B. `nuget`).                                                | `nuget`  |
| `INPUT_USERNAME`              |   ⚠️   | Benutzername, **oder** `INPUT_ORGNAME` setzen.                                               | –        |
| `INPUT_ORGNAME`               |   ⚠️   | Organisationsname, **oder** `INPUT_USERNAME` setzen.                                         | –        |
| `INPUT_PACKAGE_NAME`          |   ✅   | Paketname.                                                                                   | –        |
| `INPUT_VERSION_FILTER`        |   ❌   | **Include-Globs**, `;`-getrennt (z. B. `*alpha*;*pr-*`).                                     | –        |
| `INPUT_VERSION_EXCLUDE_FILTER`|   ❌   | **Exclude-Globs**, `;`-getrennt (z. B. `*stable*`).                                          | –        |

> **Hinweis:** Es muss **mindestens** `INPUT_USERNAME` **oder** `INPUT_ORGNAME` gesetzt sein.

### Glob-Syntax (Kurzüberblick)

- `*` beliebig viele Zeichen  
- `?` genau ein Zeichen  
- `[abc]` eine von `a,b,c`  
- `[!abc]` **nicht** `a,b,c`  
- `{foo,bar}` Alternative (entspricht `(foo|bar)`)

**Beispiele**

- `*alpha*` → alle Pre-Releases mit „alpha“  
- `1.2.*` → alle 1.2.x  
- `*pr-*;*build*` → mehrere Includes (durch `;`)  
- Exclude: `INPUT_VERSION_EXCLUDE_FILTER="*stable*"`

---

## Lokale Entwicklung

### `launchSettings.json` (Beispiel)

```json
{
  "profiles": {
    "Local (Dev)": {
      "commandName": "Project",
      "environmentVariables": {
        "DOTNETCORE_ENVIRONMENT": "Development",
        "INPUT_PACKAGE_TYPE": "nuget",
        "INPUT_USERNAME": "jk-atbas",
        "INPUT_ORGNAME": "",
        "INPUT_PACKAGE_NAME": "Very_Useful_Reasons",
        "INPUT_VERSION_FILTER": "*pr-9999*;*alpha",
        "INPUT_VERSION_EXCLUDE_FILTER": ""
      }
    },
    "WSL (Dev)": {
      "commandName": "WSL2",
      "environmentVariables": {
        "DOTNETCORE_ENVIRONMENT": "Development",
        "INPUT_GITHUB_API_KEY": "some_token",
        "INPUT_PACKAGE_TYPE": "nuget",
        "INPUT_USERNAME": "jk-atbas",
        "INPUT_ORGNAME": "",
        "INPUT_PACKAGE_NAME": "Very_Useful_Reasons",
        "INPUT_VERSION_FILTER": "*pr-9999*",
        "INPUT_VERSION_EXCLUDE_FILTER": ""
      }
    },
    "Container (Dockerfile)": {
      "commandName": "Docker",
      "environmentVariables": {
        "DOTNETCORE_ENVIRONMENT": "Development",
        "INPUT_GITHUB_API_KEY": "some_token",
        "INPUT_PACKAGE_TYPE": "nuget",
        "INPUT_USERNAME": "jk-atbas",
        "INPUT_ORGNAME": "",
        "INPUT_PACKAGE_NAME": "Very_Useful_Reasons",
        "INPUT_VERSION_FILTER": "*pr-9999*",
        "INPUT_VERSION_EXCLUDE_FILTER": ""
      }
    }
  }
}
```

> Das PAT (INPUT_GITHUB_API_KEY) kann alternativ benutzerkontospezifisch in einer Shell/OS – oder (für lokale Tests) als Prozess-EnvVar in den launchSettings gesetzt werden

### Setzen des PAT als User-EnvVar

Beispiele:

Powershell

```pwsh
[System.Environment]::SetEnvironmentVariable("INPUT_GITHUB_API_KEY","<PAT>","User")
```

bash

```bash
export INPUT_GITHUB_API_KEY="<PAT>"
```