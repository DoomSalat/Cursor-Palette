# 🖌️ Editor de pintura

Mueve la imagen del cursor dentro de su lienzo, cambia el tamaño del lienzo, pinta píxeles o desplázate y haz zoom por la vista.

🧰 Herramientas (barra superior):
✥ Mover — arrastra la imagen directamente, o usa las flechas/cuadrícula de ajuste.
✋ Mano — mantén pulsado el botón izquierdo del ratón para desplazarte. El botón central del ratón funciona con cualquier herramienta.
{img:PencilIcon48} Pincel — mantén pulsado el botón izquierdo para pintar píxeles. Un contorno blanco muestra el píxel bajo el cursor. Mantén Mayús y haz clic para dibujar una línea recta desde tu último punto; añade Ctrl para ajustar el ángulo a pasos de 45°. Elige un color a la derecha — el interruptor ◐/■ (arriba a la derecha del selector) alterna entre una rueda cromática y un cuadrado al estilo Photoshop; ajusta tono/brillo y opacidad, o escribe/pega un código hexadecimal directamente.
{img:EraseIcon32} Borrador — mantén pulsado el botón izquierdo para borrar píxeles (hacerlos transparentes). Mayús y Mayús+Ctrl funcionan igual que con el pincel, para líneas rectas al borrar.
{img:FillIcon32} Rellenar — haz clic en un área de un color para rellenarla con el color seleccionado. Usa la misma rueda cromática que el pincel.
{img:EyedropperIcon48} Cuentagotas — haz clic en el botón situado sobre el selector de color, o mantén Alt y haz clic con Pincel/Rellenar, para tomar un color de cualquier punto de la pantalla. El botón se pone azul y el cursor cambia mientras está activo; Esc cancela.
⛶ Lienzo — arrastra los tiradores de los bordes/esquinas del lienzo para cambiar su tamaño, luego pulsa "Aplicar" para confirmar. Cambiar de herramienta sin confirmar revierte el cambio.
🎯 Hotspot — arrastra el marcador sobre el lienzo, o haz clic en el punto deseado; los 9 botones de ajuste rápido saltan a posiciones típicas (esquinas, bordes, centro).
{img:ImageRefIcon32} Referencia — muestra una imagen de referencia detrás del sprite del cursor para calcar. Ajusta opacidad, margen, desplazamiento y filtrado bilineal, o arrastra y suelta tu propia imagen. "Ocultar imagen principal" oculta temporalmente el dibujo, dejando visible solo la referencia. Si la referencia está animada, en modo vinculado su fotograma sigue al fotograma activo de la línea de tiempo; activa "Control manual de referencia" para recorrer sus fotogramas de forma independiente con ◀/▶/⟲. No se guarda en el cursor.
↶ Deshacer — revertir el último cambio (Ctrl+Z).
↷ Rehacer — reaplicar un cambio revertido (Ctrl+Y o Ctrl+Shift+Z).

🎞️ Línea de tiempo de animación (barra bajo el lienzo) — "+"/"−" añaden y eliminan fotogramas, los números de fotograma cambian el activo. ▶/⏹ reproduce/detiene la vista previa; el campo "ms" define la duración del fotograma activo. "Para todos" aplica esa duración a todos los fotogramas a la vez; al desmarcarlo se restauran los valores anteriores de cada fotograma. "Control manual de referencia" desvincula la referencia de la línea de tiempo para explorarla de forma independiente. Límite — 60 fotogramas, mínimo 17 ms por fotograma (límite del formato .ani).
{img:DownloadIcon32} ".gif" (junto a ".png") — aparece cuando hay más de un fotograma, exporta la animación como GIF a Descargas.

{img:SizeChangeIcon32} Sub-tamaños (herramienta "Tamaños" en el panel derecho) — gestiona tamaños adicionales del cursor dentro de un único archivo (.cur o .ani). Si el archivo no tiene sub-tamaños, aparece el botón "Generar sub-tamaños predeterminados" en lugar de "Aplicar a todos" — crea tamaños estándar (32, 48, 64, 96, 128, 256) a partir de la imagen actual. Puedes añadir y eliminar tamaños manualmente, y elegir el modo de escalado (vecino más cercano / ponderado por área) para cada tamaño. "Aplicar a todos" regenera todos los sub-tamaños a partir de la imagen maestra con el modo seleccionado. Para cursores animados, todos los fotogramas se escalan sincrónicamente. El modo de edición permite ajustar los píxeles de cada sub-tamaño individualmente.

🕹️ Flechas (herramienta Mover) — desplazan la imagen 1 píxel; se desactivan cuando la imagen llega a ese borde.
⚡ Cuadrícula de ajuste (herramienta Mover) — 9 botones para pegar la imagen a un borde/esquina o centrarla.
📐 "Tamaño del lienzo" (arriba a la derecha) — define un ancho/alto exacto, elige un preajuste y un punto de anclaje desde el que crece o se reduce el lienzo.
{img:DownloadIcon32} "Cargar imagen" (arriba a la derecha) — carga un archivo .png/.jpg/.bmp/.gif o .cur/.ani (primer fotograma; un GIF animado se convierte en toda la línea de tiempo de fotogramas) mediante el selector de archivos, o arrástralo al botón/lienzo; elige "Superponer" para añadirlo sobre el sprite actual (el lienzo se expande para ajustarse) o "Reemplazar" para sustituir todo el contenido del lienzo.

🔍 Zoom — Ctrl + rueda del ratón, o los botones −/+, centrado en el cursor. Un par −/+ independiente escala la interfaz del editor.
☑️ "Límites del sprite" — dibuja un contorno alrededor de los píxeles opacos de la imagen.
{img:DownloadIcon32} ".png" — exporta el lienzo como PNG a Descargas (nombre por preajuste + rol + tamaño).

Coordenadas mostradas abajo a la izquierda.
💾 "Guardar" — aplicar los cambios.

