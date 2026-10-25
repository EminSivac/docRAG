# docRAG

Lokales **RAG-System (Retrieval-Augmented Generation)** für große Dokumentmengen. Du stellst eine
Frage in natürlicher Sprache, docRAG findet die passenden Stellen in deinen PDF-, Office- und
Textdateien und lässt ein lokales Sprachmodell daraus eine Antwort **mit Quellenangaben**
formulieren. Alles läuft auf deinem Rechner, es verlassen keine Daten das Gerät.

Das Projekt ersetzt einen früheren GPT4ALL + SQLite Stack, der bei ca. 27 GB Dokumenten instabil
wurde. Stattdessen übernehmen **Qdrant** (Vektor-Datenbank) und **LM Studio** (lokale
Inferenz-Engine) die schwere Arbeit. Server und Admin-Tool sind in .NET geschrieben und laufen als
eigenständige Windows-EXEs, zur Laufzeit wird kein Python benötigt.

---

## Inhaltsverzeichnis

1. [Funktionsumfang](#funktionsumfang)
2. [Wie es funktioniert](#wie-es-funktioniert)
3. [Architektur](#architektur)
4. [Projektstruktur](#projektstruktur)
5. [Voraussetzungen](#voraussetzungen)
6. [Einmaliges Setup](#einmaliges-setup)
7. [Bedienung im Alltag](#bedienung-im-alltag)
8. [Konfiguration](#konfiguration)
9. [Unterstützte Dateiformate](#unterstützte-dateiformate)
10. [Die Indexierung im Detail](#die-indexierung-im-detail)
11. [Die Beantwortung einer Frage im Detail](#die-beantwortung-einer-frage-im-detail)
12. [HTTP-API](#http-api)
13. [Speicherorte und Daten](#speicherorte-und-daten)
14. [Skalierung auf große Datenmengen](#skalierung-auf-große-datenmengen)
15. [Build und Verteilung](#build-und-verteilung)
16. [Fehlerbehebung](#fehlerbehebung)
17. [Sicherheit und Datenschutz](#sicherheit-und-datenschutz)
18. [Bekannte Einschränkungen](#bekannte-einschränkungen)

---

## Funktionsumfang

- **Semantische Suche** über PDF, Word, Excel, PowerPoint, Text und Markdown.
- **Antworten mit Quellen**: Dateiname, Fundstelle (Seite / Folie / Blatt) und Textausschnitt
  samt Relevanzwert.
- **Inkrementelle Indexierung**: nur neue oder geänderte Dateien werden verarbeitet, gelöschte
  Dateien werden automatisch aus dem Index entfernt.
- **Robust bei großen Beständen**: Vektoren liegen in Qdrant (für Millionen Punkte ausgelegt), die
  lokale SQLite-Datei enthält nur ein kleines Manifest.
- **Admin-Tray-App** (WinForms) zum Starten/Stoppen aller Komponenten, für Einstellungen,
  Reindexierung und das Leeren der Datenbank.
- **Web-UI** im Browser, ausgeliefert vom Server selbst.
- **Zustandslose Chat-Anfragen**: jede Frage wird unabhängig beantwortet, es gibt keine
  Gesprächshistorie.
- **Vollständig lokal**: Embeddings und Antworten kommen von deinem LM Studio, die Vektoren
  liegen in deinem lokalen Qdrant.

## Wie es funktioniert

```
 Dokumente ──► Parser ──► Chunker ──► Embedding (LM Studio) ──► Qdrant
                                                                  ▲
 Frage ──► Embedding (LM Studio) ──► Ähnlichkeitssuche ───────────┘
                                          │
                                          ▼
                       Top-K Textstellen + Frage ──► Chat-Modell (LM Studio)
                                                          │
                                                          ▼
                                            Antwort + Quellenliste
```

1. **Indexierung**: Dokumente werden gelesen, in Textabschnitte (Chunks) zerlegt, von einem
   Embedding-Modell in Vektoren umgewandelt und in Qdrant gespeichert.
2. **Abfrage**: Die Frage wird mit demselben Embedding-Modell vektorisiert. Qdrant liefert die
   ähnlichsten Chunks.
3. **Antwort**: Diese Chunks gehen zusammen mit der Frage an das Chat-Modell, das ausschließlich
   auf Basis der Auszüge antworten soll. Die Treffer werden als Quellen mitgeliefert.

## Architektur

| Komponente | Rolle | Technik |
|---|---|---|
| **DocRag.Server** | Indexierung, Retrieval, REST-API, Web-UI | ASP.NET Core (Kestrel), .NET 10 |
| **DocRag.Admin** | Steuerzentrale: Prozesse starten/stoppen, Einstellungen, Reindex | WinForms Tray-App |
| **DocRag.Shared** | Gemeinsame Konfiguration, Pfade und DTOs | .NET-Klassenbibliothek |
| **Qdrant** | Speichert Vektoren und Chunk-Texte | `qdrant.exe`, von der Admin-App als Kindprozess gestartet |
| **LM Studio** | Liefert Embeddings und Chat-Antworten über eine OpenAI-kompatible API | Separate Anwendung |

Die Admin-App schreibt die Konfiguration als JSON-Datei, der Server liest sie beim Start. Die
Admin-App startet Qdrant und den Server als Kindprozesse und leitet deren Konsolenausgabe in ihr
Log um.

Verwendete Bibliotheken im Server:

- `Qdrant.Client` für die gRPC-Anbindung an Qdrant
- `UglyToad.PdfPig` für PDF-Textextraktion
- `DocumentFormat.OpenXml` für DOCX, XLSX und PPTX
- `Microsoft.Data.Sqlite` für das Index-Manifest

## Projektstruktur

```
docRAG/
├── DocRag.slnx                    Solution
├── README.md
├── tools/
│   └── qdrant/                    Hier gehört qdrant.exe hin (nicht im Repository)
└── src/
    ├── DocRag.Shared/
    │   ├── AppConfig.cs           Konfigurationsmodell, Pfade (ConfigPaths)
    │   ├── ConfigStore.cs         Laden/Speichern der config.json
    │   └── Dtos.cs                ChatRequest/-Response, SourceCitation, IngestStatus
    ├── DocRag.Server/
    │   ├── Program.cs             Start, Dependency Injection, Kestrel
    │   ├── Api/Endpoints.cs       REST-Endpunkte
    │   ├── Clients/
    │   │   ├── LmStudioClient.cs  Embeddings + Chat Completions
    │   │   └── QdrantService.cs   Collection-Verwaltung, Upsert, Suche, Löschen
    │   ├── Ingestion/
    │   │   ├── FileCrawler.cs     Rekursive Dateisuche
    │   │   ├── IngestionService.cs Orchestrierung der Indexierung
    │   │   ├── IndexManifest.cs   SQLite-Manifest (Pfad, Hash, Zeitstempel)
    │   │   ├── Chunker.cs         Zerlegung in überlappende Chunks
    │   │   ├── ParsedDocument.cs  Zwischenformat der Parser
    │   │   └── Parsers/           PdfParser, OfficeParser, TextParser
    │   ├── Retrieval/
    │   │   ├── RetrievalService.cs Frage → Suche → Antwort
    │   │   └── PromptBuilder.cs   System- und User-Prompt
    │   └── wwwroot/               Web-UI (index.html, app.js, styles.css)
    └── DocRag.Admin/
        ├── MainForm.cs            Hauptfenster + Tray-Icon
        ├── SettingsForm.cs        Einstellungsdialog
        └── ProcessManager.cs      Start/Stop von Qdrant und Server
```

## Voraussetzungen

- **Windows** (die Admin-App ist WinForms, Qdrant wird als Windows-Binary gestartet)
- **.NET 10 SDK** zum Bauen (für die self-contained Variante braucht die Zielmaschine keine
  .NET-Runtime)
- **LM Studio** mit mindestens einem Embedding- und einem Chat-Modell
- **Qdrant** für Windows (`qdrant.exe`)
- Ausreichend Festplattenplatz: der Index (Vektoren plus Chunk-Texte) wächst mit der
  Dokumentmenge. Plane großzügig und lege die Daten auf eine schnelle Platte (SSD empfohlen).

## Einmaliges Setup

### 1. LM Studio einrichten

1. LM Studio installieren: https://lmstudio.ai
2. Ein **Embedding-Modell** laden, z. B. `nomic-embed-text` oder
   `text-embedding-nomic-embed-text-v1.5`.
3. Ein **Chat-Modell** laden (beliebiges instruct-fähiges Modell, passend zu GPU und RAM).
4. Unter **Local Server** den Server starten (Standard-Port `1234`).

### 2. Qdrant besorgen

1. Windows-Release von https://github.com/qdrant/qdrant/releases herunterladen
   (`qdrant-x86_64-pc-windows-msvc.zip` o. ä.).
2. Entpacken nach `tools/qdrant/qdrant.exe` im Projektverzeichnis. Die Admin-App sucht dort
   automatisch. Alternativ kann der Pfad in den Einstellungen gesetzt werden.

### 3. Projekt bauen

```bash
dotnet build DocRag.slnx
```

### 4. Admin-App starten und konfigurieren

`DocRag.Admin` starten und unter **Einstellungen**:

- **Dokumentordner** hinzufügen (z. B. die 27-GB-Quelle). Unterordner werden rekursiv
  durchsucht.
- **LM Studio URL**, **Embedding-Modell** und **Chat-Modell** prüfen. Die Modellnamen müssen
  **exakt** denen entsprechen, die in LM Studio geladen sind.
- Speichern.

### 5. Alles starten

Auf **Start Alles** klicken. Das startet Qdrant und den Server als Kindprozesse.

### 6. Erste Indexierung

Auf **Reindex jetzt** klicken. Der Fortschritt (verarbeitete, übersprungene und fehlgeschlagene
Dateien) erscheint im Statusbereich und im Log. Beim nächsten Klick werden nur neue oder
geänderte Dateien verarbeitet.

### 7. Fragen stellen

Auf **Web-UI öffnen** klicken. Das öffnet `http://localhost:<ServerPort>` (Standard `5178`) im
Browser. Frage eingeben, die Antwort erscheint mit einer Liste der Quelldokumente.

## Bedienung im Alltag

### Die Schaltflächen der Admin-App

| Schaltfläche | Wirkung |
|---|---|
| **Start Alles** | Startet Qdrant und danach den Server |
| **Stop Alles** | Beendet Server und Qdrant (samt Prozessbaum) |
| **Reindex jetzt** | Stößt einen inkrementellen Indexierungslauf im Server an |
| **DB komplett leeren** | Löscht alle docRAG-Collections in Qdrant **und** das lokale Manifest. Danach ist ein vollständiger Neuaufbau nötig. Nicht möglich, solange eine Indexierung läuft |
| **Einstellungen** | Öffnet den Konfigurationsdialog |
| **Web-UI öffnen** | Öffnet das Web-UI im Standardbrowser |

Die Statuszeilen zeigen, ob Qdrant und Server laufen und wie weit die Indexierung ist. Die App
sitzt außerdem als Symbol im Infobereich der Taskleiste (Tray) mit den Einträgen **Öffnen** und
**Beenden**.

Wird ein Kindprozess unerwartet beendet, schreibt die Admin-App eine deutliche Meldung mit
Exit-Code ins Log (häufigste Ursache: ein bereits belegter Port).

### Typische Abläufe

- **Neue Dokumente hinzugefügt**: *Reindex jetzt* klicken. Nur die neuen Dateien werden verarbeitet.
- **Dokument geändert**: *Reindex jetzt*. Die alten Chunks der Datei werden ersetzt.
- **Dokument gelöscht**: *Reindex jetzt*. Die zugehörigen Chunks werden aus Qdrant entfernt.
- **Embedding-Modell gewechselt**: Einstellungen anpassen, speichern, Server neu starten,
  dann *Reindex jetzt* (siehe [Embedding-Modellwechsel](#embedding-modellwechsel)).
- **Alles zurücksetzen**: *DB komplett leeren*, danach *Reindex jetzt*.

## Konfiguration

Die Konfiguration wird von der Admin-App gepflegt und liegt als JSON-Datei unter
`%ProgramData%\DocRag\config.json`. Der Server liest sie **beim Start**. Nach Änderungen muss der
Server daher neu gestartet werden.

| Einstellung | Standard | Bedeutung |
|---|---|---|
| `DocumentRoots` | `[]` | Liste der zu indexierenden Ordner (rekursiv) |
| `LmStudioBaseUrl` | `http://localhost:1234` | Basis-URL der LM-Studio-API |
| `EmbeddingModel` | `nomic-embed-text` | Name des Embedding-Modells (exakt wie in LM Studio) |
| `ChatModel` | `local-model` | Name des Chat-Modells (exakt wie in LM Studio) |
| `QdrantHost` | `localhost` | Host von Qdrant |
| `QdrantHttpPort` | `6333` | HTTP-Port von Qdrant |
| `QdrantGrpcPort` | `6334` | gRPC-Port von Qdrant (wird vom Server genutzt) |
| `QdrantCollectionName` | `docrag_chunks` | Präfix der Collection, siehe unten |
| `ServerPort` | `5178` | Port des docRAG-Servers und des Web-UI |
| `QdrantExePath` | leer | Expliziter Pfad zu `qdrant.exe`, leer = automatische Suche |
| `ServerExePath` | leer | Expliziter Pfad zu `DocRag.Server.exe`, leer = automatische Suche |
| `ChunkSizeChars` | `1000` | Maximale Chunk-Größe in Zeichen |
| `ChunkOverlapChars` | `150` | Überlappung zwischen aufeinanderfolgenden Chunks |
| `EmbeddingBatchSize` | `16` | Anzahl Chunks pro Embedding-Anfrage |
| `EmbeddingMaxParallelism` | `2` | Aktuell **ohne Wirkung**, siehe [Einschränkungen](#bekannte-einschränkungen) |
| `RetrievalTopK` | `6` | Anzahl der Textstellen, die als Kontext verwendet werden |

Im Einstellungsdialog der Admin-App sind die Dokumentordner, LM-Studio-URL, beide Modelle,
Server-Port, Qdrant-Ports, Chunk-Größe, Overlap, Top-K sowie die beiden optionalen EXE-Pfade
einstellbar.

### Auffindung der Programme

Ist kein expliziter Pfad gesetzt, sucht die Admin-App:

- **`qdrant.exe`** unter `<Admin-Verzeichnis>\tools\qdrant\` sowie relativ zum Projektverzeichnis
  unter `tools\qdrant\`.
- **`DocRag.Server.exe`** unter `<Admin-Verzeichnis>\Server\` sowie in den Build-Ausgaben
  `DocRag.Server\bin\Debug\net10.0\` und `bin\Release\net10.0\`.

Qdrant bekommt seinen Speicherort und die Ports über Umgebungsvariablen
(`QDRANT__STORAGE__STORAGE_PATH`, `QDRANT__SERVICE__HTTP_PORT`, `QDRANT__SERVICE__GRPC_PORT`).

### Chunk-Größe wählen

- **Kleiner** (400 bis 800 Zeichen): präzisere Treffer, aber mehr Chunks und weniger Kontext je Treffer.
- **Größer** (1000 bis 2000 Zeichen): mehr Zusammenhang je Treffer, dafür unschärfere Suche.
- Die Chunk-Größe sollte zum Kontextfenster deines Chat-Modells passen: Top-K × Chunk-Größe plus
  Frage und Antwort müssen hineinpassen.
- Eine Änderung wirkt nur auf **neu verarbeitete** Dateien. Für einheitliche Chunks danach
  *DB komplett leeren* und neu indexieren.

### Embedding-Modellwechsel

Der Name der genutzten Qdrant-Collection setzt sich aus Präfix und Modellname zusammen:

```
<QdrantCollectionName>__<EmbeddingModel mit Sonderzeichen als _>
z. B. docrag_chunks__nomic_embed_text
```

Verschiedene Embedding-Modelle erzeugen Vektoren mit unterschiedlicher Dimension und Semantik. Je
Modell gibt es deshalb eine eigene Collection und einen eigenen Manifest-Eintrag, damit
inkompatible Vektoren nie vermischt werden. Nach einem Modellwechsel wird der Bestand beim
nächsten Reindex komplett neu für das neue Modell aufgebaut. Alte Collections bleiben bestehen,
bis sie mit *DB komplett leeren* entfernt werden (das löscht alle Collections mit dem
konfigurierten Präfix, nicht nur die aktuelle).

## Unterstützte Dateiformate

| Endung | Parser | Fundstelle in den Quellen |
|---|---|---|
| `.pdf` | PdfPig, seitenweise | `Seite N` |
| `.docx` | OpenXML | `Dokument` |
| `.xlsx` | OpenXML, je Tabellenblatt | `Blatt 'Name'` |
| `.pptx` | OpenXML, je Folie | `Folie N` |
| `.txt`, `.md` | Direktes Einlesen | `Dokument` |

Andere Endungen werden ignoriert. Die Liste steht in `AppConfig.SupportedExtensions`.

> **Hinweis zu PDFs:** Es wird der vorhandene Textlayer gelesen. Gescannte PDFs ohne Textlayer
> (reine Bilder) liefern keinen Text, es findet **kein OCR** statt.

## Die Indexierung im Detail

Ein Lauf (`IngestionService.RunAsync`) arbeitet so:

1. **Dateien sammeln.** Der `FileCrawler` durchläuft alle `DocumentRoots` rekursiv und liefert
   Dateien mit unterstützter Endung. Nicht vorhandene Ordner und Verzeichnisse ohne
   Leseberechtigung werden mit einer Warnung übersprungen, der Lauf bricht dadurch nicht ab.
2. **Änderungen erkennen**, je Datei und je Embedding-Modell:
   - Unveränderter Zeitstempel (`LastWriteTimeUtc`): Datei wird **übersprungen**, ohne sie zu
     hashen (schneller Pfad).
   - Zeitstempel anders, aber gleicher **SHA-256-Hash**: nur das Manifest wird aktualisiert,
     kein erneutes Embedding.
   - Neu oder wirklich geändert: wird verarbeitet.
3. **Alte Chunks entfernen.** Bei geänderten Dateien werden zuerst alle bisherigen Chunks dieser
   Datei in Qdrant gelöscht.
4. **Parsen.** Der passende Parser liefert Abschnitte mit Fundstelle (Seite, Folie, Blatt).
5. **Chunking.** Siehe unten.
6. **Embedding in Batches.** Je `EmbeddingBatchSize` Chunks eine Anfrage an LM Studio
   (`/v1/embeddings`).
7. **Speichern.** Die Collection wird bei Bedarf angelegt (die Vektordimension ergibt sich aus
   dem ersten Embedding), dann werden die Punkte hochgeladen. Jeder Punkt enthält Vektor,
   Dateipfad, Dateiname, Fundstelle und Chunk-Text.
8. **Manifest aktualisieren** (Pfad, Hash, Zeitstempel, Chunk-Anzahl).
9. **Aufräumen.** Dateien, die im Manifest stehen, aber nicht mehr auf der Platte liegen, werden
   aus Qdrant und dem Manifest entfernt.

Fehler bei einzelnen Dateien (defekte PDFs, gesperrte Dateien, …) stoppen den Lauf nicht. Die
Datei wird als fehlgeschlagen gezählt und die Meldung in `RecentErrors` abgelegt (die letzten 50
bleiben erhalten). Es kann immer nur **ein** Lauf gleichzeitig laufen.

### Chunking

Der `Chunker` arbeitet pro Abschnitt eines Dokuments (z. B. pro PDF-Seite):

- Der Text wird an **Leerzeilen** in Absätze getrennt.
- Absätze werden zu einem Chunk zusammengefasst, bis `ChunkSizeChars` erreicht ist.
- Beim Abschluss eines Chunks werden die letzten `ChunkOverlapChars` Zeichen in den nächsten
  übernommen, damit Information an der Grenze nicht verloren geht.
- Ein einzelner Absatz, der länger als die Chunk-Größe ist, wird hart in überlappende Stücke
  zerschnitten.
- Chunks gehören immer genau zu **einem** Abschnitt, die Fundstelle (z. B. `Seite 12`) bleibt
  dadurch exakt.

## Die Beantwortung einer Frage im Detail

`RetrievalService.AnswerAsync`:

1. Leere Frage → Hinweis „Bitte eine Frage eingeben.“
2. Die Frage wird mit dem Embedding-Modell vektorisiert.
3. Qdrant liefert die `RetrievalTopK` ähnlichsten Chunks.
4. Gibt es keine Treffer, lautet die Antwort, dass in den indizierten Dokumenten nichts gefunden
   wurde. Das Chat-Modell wird dann **nicht** befragt.
5. Sonst baut der `PromptBuilder` den Prompt: nummerierte Dokumentauszüge mit Quelle und
   Fundstelle, danach die Frage.
6. Das Chat-Modell antwortet mit `temperature = 0.2` (wenig Streuung, faktennah).
7. Die Antwort kommt zusammen mit den Quellen zurück. Jede Quelle enthält Dateiname, Pfad,
   Fundstelle, einen auf 300 Zeichen gekürzten Snippet und den Ähnlichkeitswert.

Der System-Prompt weist das Modell an, **ausschließlich** auf Basis der Auszüge zu antworten,
klar zu sagen, wenn die Antwort dort fehlt, nichts zu erfinden und auf Deutsch zu antworten. Du
kannst den Prompt in `src/DocRag.Server/Retrieval/PromptBuilder.cs` anpassen.

## HTTP-API

Der Server lauscht ausschließlich auf `localhost:<ServerPort>`.

| Methode | Pfad | Beschreibung |
|---|---|---|
| `POST` | `/api/chat` | Stellt eine Frage |
| `POST` | `/api/ingest/start` | Startet einen Indexierungslauf im Hintergrund (`202 Accepted`) |
| `GET` | `/api/ingest/status` | Aktueller Stand der Indexierung |
| `POST` | `/api/ingest/clear` | Leert Qdrant-Collections und Manifest (`409 Conflict`, wenn gerade indexiert wird) |
| `GET` | `/api/config` | Aktive Konfiguration |
| `GET` | `/api/health` | Lebenszeichen: `{ "status": "ok" }` |

### Beispiele

Frage stellen:

```bash
curl -X POST http://localhost:5178/api/chat \
  -H "Content-Type: application/json" \
  -d '{ "question": "Welche Kündigungsfrist gilt im Mietvertrag?" }'
```

Antwort:

```json
{
  "answer": "Laut Mietvertrag beträgt die Kündigungsfrist drei Monate [1].",
  "sources": [
    {
      "fileName": "Mietvertrag.pdf",
      "filePath": "D:\\Dokumente\\Mietvertrag.pdf",
      "location": "Seite 4",
      "snippet": "Das Mietverhältnis kann von beiden Seiten mit einer Frist von drei Monaten …",
      "score": 0.82
    }
  ]
}
```

Indexierung starten und beobachten:

```bash
curl -X POST http://localhost:5178/api/ingest/start
curl http://localhost:5178/api/ingest/status
```

Statusobjekt:

```json
{
  "isRunning": true,
  "totalFiles": 1250,
  "processedFiles": 310,
  "skippedFiles": 12,
  "failedFiles": 2,
  "totalChunksIndexed": 9840,
  "currentFile": "D:\\Dokumente\\Bericht.docx",
  "startedAt": "2026-01-01T10:00:00Z",
  "finishedAt": null,
  "recentErrors": []
}
```

## Speicherorte und Daten

Alles liegt unter `%ProgramData%\DocRag\` (normalerweise `C:\ProgramData\DocRag\`):

| Pfad | Inhalt |
|---|---|
| `config.json` | Konfiguration |
| `data\manifest.db` | SQLite-Manifest: Pfad, Hash, Zeitstempel, Chunk-Anzahl je Datei und Modell (**keine** Vektoren) |
| `data\qdrant-storage\` | Qdrant-Speicher mit allen Vektoren und Chunk-Texten |
| `logs\` | Logverzeichnis |

**Backup:** Zum Sichern oder Umziehen den Ordner `data\` (Manifest **und** Qdrant-Speicher
gemeinsam) kopieren, während Qdrant und Server gestoppt sind. Manifest und Qdrant müssen zusammen
zum selben Stand gehören, sonst weicht der Index von der Realität ab.

Die Quelldokumente werden von docRAG **nur gelesen**, nie verändert.

## Skalierung auf große Datenmengen

- Die Ingestion ist **inkrementell**. Das SQLite-Manifest merkt sich Pfad, Hash, Zeitstempel und
  Chunk-Anzahl, **nicht** die Vektoren. Das ist der entscheidende Unterschied zum alten
  GPT4ALL-Setup, in dem SQLite selbst die Vektorlast tragen musste.
- Alle Vektoren und Chunk-Texte liegen in Qdrant, das für Millionen Punkte ausgelegt ist.
- Embedding-Aufrufe laufen in Batches (`EmbeddingBatchSize`), um den lokalen Modell-Server nicht
  zu überlasten. Bei viel VRAM kann der Wert erhöht werden, bei Speicherproblemen oder Timeouts
  gesenkt.
- Ein Lauf kann jederzeit unterbrochen werden. Bereits verarbeitete Dateien sind im Manifest
  vermerkt und werden beim nächsten Lauf übersprungen.
- **Empfehlung:** Vor dem vollen Lauf erst mit einem Unterordner von einigen hundert MB testen, um
  Durchsatz, Speicherverbrauch und Antwortqualität der eigenen Hardware einzuschätzen.
- Der erste Vollindex ist der zeitaufwendige Teil und hängt vor allem von der
  Embedding-Geschwindigkeit deines LM Studio ab. Spätere Läufe sind dank des Zeitstempel-Schnellpfads
  deutlich schneller.

## Build und Verteilung

Entwicklungsbuild:

```bash
dotnet build DocRag.slnx
```

Eigenständige Verteilung ohne installierte .NET-Runtime:

```bash
dotnet publish src/DocRag.Server -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
dotnet publish src/DocRag.Admin  -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

Empfohlenes Verzeichnislayout für die Auslieferung, damit die automatische Suche funktioniert:

```
docRAG/
├── DocRag.Admin.exe
├── Server/
│   ├── DocRag.Server.exe
│   └── wwwroot/            Web-UI
└── tools/
    └── qdrant/
        └── qdrant.exe
```

Das Web-UI (`wwwroot`) muss neben der Server-EXE liegen, sonst liefert der Server keine
Oberfläche aus. Alternativ können die Pfade in den Einstellungen explizit gesetzt werden.

## Fehlerbehebung

| Problem | Mögliche Ursache und Lösung |
|---|---|
| „qdrant.exe wurde nicht gefunden“ | Datei nach `tools/qdrant/qdrant.exe` legen oder den Pfad in den Einstellungen setzen |
| „DocRag.Server.exe wurde nicht gefunden“ | Zuerst bauen (`dotnet build`) oder Pfad in den Einstellungen setzen |
| Prozess „unerwartet beendet“ | Meist ein belegter Port. Ports in den Einstellungen ändern oder den blockierenden Prozess beenden. Die Fehlermeldung steht im Log direkt davor |
| Embeddings schlagen fehl (HTTP-Fehler) | LM Studio Server läuft nicht, falsche URL oder der Embedding-Modellname stimmt nicht exakt mit dem geladenen Modell überein |
| Chat-Antwort schlägt fehl | Chat-Modell in LM Studio nicht geladen oder Name falsch |
| Zeitüberschreitung bei Anfragen | Der HTTP-Timeout zu LM Studio beträgt 5 Minuten. Bei sehr langsamen Modellen `EmbeddingBatchSize` verkleinern oder ein kleineres Modell wählen |
| „Ich habe dazu nichts … gefunden“ | Index leer (noch nicht indexiert), falsches Embedding-Modell konfiguriert oder die Frage passt inhaltlich nicht zu den Dokumenten |
| Änderungen an Einstellungen wirken nicht | Der Server liest die Konfiguration nur beim Start. Server neu starten |
| Viele „fehlgeschlagene“ Dateien | Details in `recentErrors` (`/api/ingest/status`) oder im Log. Typisch: beschädigte oder passwortgeschützte Dateien |
| PDF liefert keinen Text | Gescanntes PDF ohne Textlayer, OCR wird nicht unterstützt |
| „DB komplett leeren“ geht nicht | Es läuft gerade eine Indexierung. Warten, bis sie fertig ist |
| Antworten wirken ungenau | `RetrievalTopK` erhöhen, Chunk-Größe anpassen, besseres Chat- oder Embedding-Modell verwenden |

## Sicherheit und Datenschutz

- Der Server bindet nur an **localhost** und ist damit nicht aus dem Netzwerk erreichbar.
- Es werden keine Daten an externe Dienste gesendet, solange LM Studio und Qdrant lokal laufen.
- Die API hat **keine Authentifizierung**. Jeder lokale Prozess und jede Webseite im Browser des
  Nutzers kann sie ansprechen, da CORS offen konfiguriert ist (`AllowAnyOrigin`). Der Endpunkt
  `/api/ingest/clear` kann den Index löschen. Für den Einsatz über die lokale Nutzung hinaus
  sollten CORS eingeschränkt und eine Authentifizierung ergänzt werden.
- `/api/config` gibt die komplette Konfiguration zurück, inklusive der Dokumentpfade.
- Chunk-Texte liegen unverschlüsselt im Qdrant-Speicher unter `%ProgramData%\DocRag\`. Wer
  diesen Ordner lesen kann, kann auch die Inhalte lesen. Für sensible Dokumente ein
  verschlüsseltes Laufwerk verwenden.

## Bekannte Einschränkungen

- **Windows only** (WinForms-Admin, Windows-Qdrant-Binary).
- **Kein OCR** für gescannte Dokumente.
- **Keine Gesprächshistorie**: Rückfragen wie „und was ist mit dem zweiten Punkt?“ funktionieren
  nicht, jede Frage muss für sich verständlich sein.
- **Keine Antwort-Streaming**: die Antwort erscheint erst, wenn sie vollständig generiert ist.
- **Nur Vektorsuche**: keine Stichwort- oder Hybridsuche. Exakte Bezeichnungen oder Nummern
  werden daher unter Umständen schlechter gefunden als semantische Inhalte.
- **`EmbeddingMaxParallelism`** ist in der Konfiguration vorhanden, wird aber im Code derzeit
  nicht ausgewertet. Embeddings werden sequenziell pro Batch erzeugt.
- Konfigurationsänderungen erfordern einen Neustart des Servers.
- `.doc`, `.xls`, `.ppt` (alte Office-Formate) und weitere Formate wie HTML oder RTF werden nicht
  indexiert.
