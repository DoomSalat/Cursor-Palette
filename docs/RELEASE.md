# Сборка релиза

Релизная сборка — это один self-contained `.exe` (не требует установленного
.NET на машине пользователя), который кладётся в `dist/` (папка в
`.gitignore`, не коммитится).

## Команда

Из корня репозитория:

```powershell
dotnet publish Cursor-Palette/Cursor-Palette.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o dist
```

Результат: `dist/Cursor-Palette.exe` (~73 МБ, self-contained, сжатый) и
`dist/Cursor-Palette.pdb` (символы отладки, для релиза пользователю не
нужен — можно не распространять).

## Флаги

- `-c Release` — релизная конфигурация.
- `-r win-x64` — таргет платформы (нужен для single-file/self-contained).
- `--self-contained true` — рантайм .NET упаковывается внутрь exe.
- `-p:PublishSingleFile=true` — всё собирается в один exe-файл.
- `-p:IncludeNativeLibrariesForSelfExtract=true` — нативные библиотеки тоже
  упаковываются внутрь, а не лежат рядом отдельными файлами.
- `-p:EnableCompressionInSingleFile=true` — сжимает упакованные внутрь exe
  файлы, размер получается примерно вдвое меньше (без него — ~165 МБ, с
  ним — ~73 МБ). Из-за этого при первом запуске exe немного медленнее
  распаковывается во временную папку.

## Версия

Перед сборкой релиза обнови `<Version>` в
[Cursor-Palette.csproj](../Cursor-Palette/Cursor-Palette.csproj).
