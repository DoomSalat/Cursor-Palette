# 📤 Exportar preajustes

## 🖼️ Miniaturas — haz clic en un preajuste para seleccionarlo/deseleccionarlo (borde azul = seleccionado).
- "Seleccionar todo" / "No seleccionar nada" — botones arriba.

🎨 Miniaturas de grupo en color — clic en la miniatura de un grupo selecciona/deselecciona todos sus miembros. Un grupo se incluye en la exportación solo cuando todos sus preajustes están seleccionados.

📝 Nombre del archivo — opcional; escribe un nombre para el archivo exportado, o déjalo vacío para usar el nombre predeterminado.

## 🗂️ Paquete vs. archivo ZIP — los dos botones de exportación generan archivos distintos:
- Paquete (.cursorpalette) — copia completa: se conservan todos los roles, los roles bloqueados, el tamaño del cursor y el indicador de escalado, y los roles tomados de otros preajustes se copian dentro, así que el archivo es autónomo. Los grupos también se guardan y se restauran al importar. Pensado para volver a importarse por completo en esta app más adelante.
- Archivo ZIP — una carpeta por preajuste con los archivos .cur/.ani originales, más un archivo cursor-palette.json con metadatos completos (roles, roles bloqueados, tamaño del cursor, indicador de escalado, grupos) y un README.txt con instrucciones de instalación. Se puede reimportar a esta app igual que un paquete, o usarse fuera de ella con otras herramientas.

🐧 Linux — la pequeña flecha "▾" junto al botón de archivo ZIP ofrece dos formatos más: un tema Xcursor (index.theme + una carpeta cursors, listo para ~/.icons) o los mismos archivos .cur/.ani sin metadatos adicionales. Ambos se pueden arrastrar de nuevo a esta app más tarde, comprimidos o ya extraídos — el botón de descarga del editor de preajustes tiene la misma flecha para exportar solo el preajuste que estás editando.

📄 Esa misma flecha "▾" también tiene "Descargar README" — guarda solo el README.txt por separado (enlace a la app e instrucciones de instalación), sin exportar ningún preajuste.

{img:DownloadIcon32} Todos se guardan en la carpeta Descargas, y el Explorador se abre con el nuevo archivo seleccionado.