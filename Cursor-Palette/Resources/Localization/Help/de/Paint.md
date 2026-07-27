# 🖌️ Mal-Editor

Verschiebt das Cursorbild innerhalb seiner Leinwand, ändert die Leinwandgröße, malt Pixel oder verschiebt/zoomt die Ansicht.

🧰 Werkzeuge (obere Leiste):
✥ Bewegen — Bild direkt ziehen oder die Pfeile/das Ausrichtungsraster verwenden.
✋ Hand — linke Maustaste halten, um die Ansicht zu verschieben. Die mittlere Maustaste funktioniert in jedem Werkzeug.
{img:PencilIcon48} Pinsel — linke Maustaste halten, um Pixel zu malen. Eine weiße Umrandung zeigt das Pixel unter dem Cursor. Umschalt gedrückt halten und klicken zeichnet eine gerade Linie ab dem letzten Punkt; mit Strg zusätzlich wird der Winkel auf 45°-Schritte eingerastet. Farbe rechts auswählen — der ◐/■-Schalter (oben rechts im Farbwähler) wechselt zwischen Farbrad und einem Quadrat im Photoshop-Stil; Farbton/Helligkeit und Deckkraft anpassen oder direkt einen Hex-Code eingeben/einfügen.
{img:EraseIcon32} Radierer — linke Maustaste halten, um Pixel zu löschen (transparent machen). Umschalt und Umschalt+Strg funktionieren wie beim Pinsel, für gerade radierte Linien.
{img:FillIcon32} Füllen — auf ein einfarbiges Gebiet klicken, um es mit der gewählten Farbe zu füllen. Verwendet dasselbe Farbrad wie der Pinsel.
{img:EyedropperIcon48} Pipette — Schaltfläche über dem Farbwähler klicken, oder Alt gedrückt halten und mit Pinsel/Füllen klicken, um eine Farbe von einer beliebigen Stelle des Bildschirms aufzunehmen. Die Schaltfläche wird blau und der Cursor ändert sich, solange sie aktiv ist; Esc bricht ab.
⛶ Leinwand — an den Griffen an Kanten/Ecken der Leinwand ziehen, um die Größe zu ändern, dann "Anwenden" klicken. Ein Werkzeugwechsel ohne Bestätigung macht die Änderung rückgängig.
🎯 Hotspot — die Markierung auf der Leinwand ziehen oder die gewünschte Stelle anklicken; die 9 Schnellwahl-Schaltflächen springen zu typischen Positionen (Ecken, Kanten, Mitte).
{img:ImageRefIcon32} Referenz — zeigt ein Referenzbild hinter dem Cursor-Sprite zum Abzeichnen an. Passen Sie Deckkraft, Rand, Versatz und bilineare Filterung an, oder ziehen Sie Ihr eigenes Bild per Drag & Drop. „Hauptbild ausblenden" blendet die Zeichnung vorübergehend aus, sodass nur die Referenz sichtbar bleibt. Ist die Referenz animiert, folgt ihr Frame im verknüpften Modus dem aktiven Timeline-Frame; aktivieren Sie „Manuelle Referenzsteuerung", um ihre Frames unabhängig mit ◀/▶/⟲ durchzublättern. Wird nicht im Cursor gespeichert.
↶ Rückgängig — letzte Änderung zurücknehmen (Ctrl+Z).
↷ Wiederherstellen — rückgängig gemachte Änderung erneut anwenden (Ctrl+Y oder Ctrl+Shift+Z).

🎞️ Animations-Timeline (Leiste unter der Leinwand) — „+"/„−" fügen Frames hinzu bzw. entfernen sie, die Framenummern wechseln den aktiven Frame. ▶/⏹ spielt die Vorschau ab/stoppt sie; das „ms"-Feld legt die Dauer des aktiven Frames fest. „Für alle" wendet diese Dauer auf alle Frames gleichzeitig an; beim Deaktivieren werden die vorherigen Werte pro Frame wiederhergestellt. „Manuelle Referenzsteuerung" löst die Referenz von der Timeline, um sie unabhängig durchzublättern. Limit — 60 Frames, mindestens 17 ms pro Frame (Grenze des .ani-Formats).
{img:DownloadIcon32} „.gif" (neben „.png") — erscheint ab mehr als einem Frame und exportiert die Animation als GIF in die Downloads.

{img:SizeChangeIcon32} Untergrößen (Werkzeug „Größen" im rechten Panel) — verwaltet zusätzliche Cursor-Größen innerhalb einer einzelnen Datei (.cur oder .ani). Wenn die Datei keine Untergrößen enthält, erscheint statt „Auf alle anwenden" die Schaltfläche „Standard-Untergrößen generieren" — sie erstellt Standardgrößen (32, 48, 64, 96, 128, 256) aus dem aktuellen Bild. Größen können manuell hinzugefügt und entfernt werden; pro Größe lässt sich der Skalierungsmodus (nächster Nachbar / flächengewichtet) wählen. „Auf alle anwenden" generiert alle Untergrößen aus dem Master-Bild mit dem gewählten Modus neu. Bei animierten Cursorn werden alle Frames synchron skaliert. Der Bearbeitungsmodus erlaubt das pixelgenaue Anpassen jeder Untergröße einzeln.

🕹️ Pfeile (Werkzeug „Bewegen") — verschieben das Bild um 1 Pixel; werden deaktiviert, sobald das Bild diese Kante erreicht.
⚡ Ausrichtungsraster (Werkzeug „Bewegen") — 9 Schaltflächen, um das Bild an eine Kante/Ecke zu drücken oder zu zentrieren.
📐 "Leinwandgröße" (oben rechts) — genaue Breite/Höhe festlegen, ein Preset wählen und einen Ankerpunkt bestimmen, von dem aus die Leinwand wächst oder schrumpft.
{img:DownloadIcon32} "Bild laden" (oben rechts) — eine .png/.jpg/.bmp/.gif- oder .cur/.ani-Datei (erstes Bild; ein animiertes GIF wird zur kompletten Frame-Timeline) über die Dateiauswahl laden, oder auf die Schaltfläche/Leinwand ziehen; "Überlagern" legt das Bild über das aktuelle Sprite (die Leinwand wächst entsprechend), "Ersetzen" tauscht den gesamten Leinwandinhalt aus.

🔍 Zoom — Strg + Mausrad oder die −/+ Schaltflächen, zentriert auf den Mauszeiger. Ein separates −/+ Paar skaliert die Oberfläche des Editors.
☑️ "Sprite-Grenzen" — zeichnet einen Umriss um die undurchsichtigen Pixel des Bildes.
{img:DownloadIcon32} ".png" — exportiert die Leinwand als PNG in die Downloads (benannt nach Preset + Rolle + Größe).

Koordinaten unten links angezeigt.
💾 "Speichern" — Änderungen übernehmen.

