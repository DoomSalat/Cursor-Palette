# 📤 Presets exportieren

## 🖼️ Kacheln — auf ein Preset klicken, um es aus-/abzuwählen (blauer Rahmen = ausgewählt).
- "Alle auswählen" / "Auswahl aufheben" — Schaltflächen oben.

🎨 Farbige Gruppenkacheln — Klick auf eine Gruppenkachel wählt/abwählt alle ihre Mitglieder. Eine Gruppe wird nur in den Export einbezogen, wenn alle ihre Presets ausgewählt sind.

📝 Dateiname — optional; einen Namen für die exportierte Datei eingeben oder leer lassen, um den Standardnamen zu verwenden.

## 🗂️ Bundle vs. ZIP-Archiv — die beiden Export-Schaltflächen erzeugen unterschiedliche Dateien:
- Bundle (.cursorpalette) — vollständige Kopie: alle Rollen, gesperrte Rollen und die Cursorgröße bleiben erhalten, und aus anderen Presets geliehene Rollen werden mitkopiert, sodass die Datei in sich geschlossen ist. Gruppen werden ebenfalls gespeichert und beim Import wiederhergestellt. Gedacht zum späteren vollständigen Re-Import in diese App.
- ZIP-Archiv — ein Ordner pro Preset mit den rohen .cur/.ani-Dateien, plus eine cursor-palette.json-Datei mit vollständigen Metadaten (Rollen, gesperrte Rollen, Cursorgröße, Gruppen) und eine README.txt mit Installationsanleitung. Kann wie ein Bundle in diese App re-importiert oder außerhalb davon mit anderen Tools verwendet werden.

🐧 Linux — der kleine Pfeil "▾" neben der ZIP-Archiv-Schaltfläche bietet zwei weitere Formate: ein Xcursor-Theme (index.theme + ein cursors-Ordner, bereit für ~/.icons) oder dieselben rohen .cur/.ani-Dateien ohne Zusatzdaten. Beide können später wieder in diese App gezogen werden, gezippt oder bereits entpackt — der Download-Button des Preset-Editors hat denselben Pfeil, um nur das gerade bearbeitete Preset zu exportieren.

{img:ReadmeIcon32} README — lädt nur die README.txt für sich herunter (App-Link und Installationsanleitung), ohne ein Preset zu exportieren.

{img:DownloadIcon32} Alle werden im Downloads-Ordner gespeichert, und der Explorer öffnet sich mit der neuen Datei markiert.