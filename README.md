# docRAG

Lokales RAG-System für große Dokumentmengen (PDF, Word/Excel/PowerPoint, Text/Markdown).

Ersetzt den bisherigen GPT4ALL + SQLite Stack, der bei ca. 27GB Dokumenten instabil wurde,
durch **Qdrant** als Vector-DB und **LM Studio** als lokale Inferenz-Engine. Server und
Admin-Tool sind in .NET geschrieben und laufen als eigenständige Windows-EXEs (kein Python
zur Laufzeit nötig).

## Architektur

- **DocRag.Server** – ASP.NET Core App: durchsucht die konfigurierten Ordner, extrahiert Text
  aus PDF/DOCX/XLSX/PPTX/TXT/MD, chunked ihn, holt Embeddings von LM Studio, speichert sie in
  Qdrant. Beantwortet Chat-Anfragen zustandslos (keine Historie) mit Quellenangaben und hostet
  das Web-UI.
- **DocRag.Admin** – WinForms Tray-App zum Starten/Stoppen von Qdrant und dem Server, zum
  Konfigurieren (Dokumentordner, LM-Studio-URL, Modelle, Ports, Chunk-Größe) und zum Anstoßen
  einer (Re-)Indexierung.
- **Qdrant** – separates Windows-Binary (`qdrant.exe`), wird von der Admin-App als Kindprozess
  gestartet.
- **LM Studio** – läuft separat, stellt eine OpenAI-kompatible API für Embeddings und
  Chat-Completions bereit.

## Einmaliges Setup

1. **LM Studio installieren** (falls noch nicht vorhanden): https://lmstudio.ai
   - Ein Embedding-Modell laden, z.B. `nomic-embed-text` oder `text-embedding-nomic-embed-text-v1.5`
   - Ein Chat-Modell laden (beliebiges instruct-fähiges Modell, je nach GPU/RAM)
   - Unter "Local Server" den Server starten (Standard-Port `1234`)

2. **Qdrant-Binary besorgen**:
   - Windows-Release von https://github.com/qdrant/qdrant/releases herunterladen
     (`qdrant-x86_64-pc-windows-msvc.zip` o.ä.)
   - Entpacken nach `tools/qdrant/qdrant.exe` in diesem Projektverzeichnis
     (die Admin-App sucht dort automatisch danach, alternativ Pfad in den Einstellungen setzen)

3. **Projekte bauen**:
   ```bash
   dotnet build DocRag.slnx
   ```

   Für eine eigenständige Verteilung (ohne .NET-Runtime auf der Zielmaschine):
   ```bash
   dotnet publish src/DocRag.Server -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
   dotnet publish src/DocRag.Admin -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
   ```

4. **DocRag.Admin starten**, in den Einstellungen:
   - Dokumentordner hinzufügen (die 27GB-Quelle)
   - LM Studio URL / Embedding-Modell / Chat-Modell prüfen (Modellnamen müssen exakt denen
     entsprechen, die in LM Studio geladen sind)
   - Speichern

5. Auf **"Start Alles"** klicken → startet Qdrant + Server als Kindprozesse.

6. Auf **"Reindex jetzt"** klicken → durchsucht die Dokumentordner und befüllt Qdrant. Der
   Fortschritt (verarbeitete/übersprungene/fehlgeschlagene Dateien) wird im Statusbereich und
   im Log angezeigt. Läuft inkrementell: bei erneutem Klick werden nur neue/geänderte Dateien
   verarbeitet.

7. **"Web-UI öffnen"** klicken → öffnet `http://localhost:<ServerPort>` im Browser. Frage
   stellen, Antwort erscheint mit Liste der Quelldokumente (Dateiname + Seite/Abschnitt +
   Snippet).

## Hinweis zur Skalierung auf 27GB

- Die Ingestion ist inkrementell: ein kleines SQLite-Manifest (nur Dateipfad, Hash, Zeitstempel,
  Chunk-Anzahl – **keine Vektoren**) merkt sich, welche Dateien bereits indiziert sind. Das ist
  der entscheidende Unterschied zum alten GPT4ALL-Setup, bei dem SQLite selbst die Vektorlast
  tragen musste.
- Alle Vektoren und Chunk-Texte liegen in Qdrant, das für Millionen von Punkten ausgelegt ist
  und Daten on-disk halten kann.
- Embedding-Aufrufe an LM Studio laufen in konfigurierbaren Batches (`EmbeddingBatchSize` in
  den Einstellungen), um den lokalen Modell-Server nicht zu überlasten.
- Vor dem vollen 27GB-Lauf empfiehlt es sich, zunächst mit einem Unterordner (einige hundert MB)
  zu testen, um Durchsatz und Speicherverbrauch der eigenen Hardware einzuschätzen.
