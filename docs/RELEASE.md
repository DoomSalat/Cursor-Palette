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

## Контрольная сумма (обязательно)

Автообновление ([UpdateChecker.cs](../Cursor-Palette/Services/UpdateChecker.cs),
[UpdateWindow.xaml.cs](../Cursor-Palette/Views/UpdateWindow.xaml.cs)) скачивает
exe из GitHub-релиза и сверяет его SHA256 с суммой, опубликованной рядом в
том же релизе. Если сумма не опубликована — кнопка «Auto update» в приложении
откажется ставить обновление (ручное скачивание в Downloads по-прежнему
работает, но с предупреждением, что целостность не проверена).

Поэтому после `dotnet publish` и переименования exe в
`Cursor-Palette-vX.Y.Z.exe` нужно посчитать хеш и сохранить его в файл
`Cursor-Palette-vX.Y.Z.exe.sha256` рядом с exe:

```powershell
$exe = "dist/Cursor-Palette-vX.Y.Z.exe"
(Get-FileHash $exe -Algorithm SHA256).Hash.ToLower() + "  " + (Split-Path $exe -Leaf) |
    Out-File "$exe.sha256" -Encoding ascii -NoNewline
```

При создании релиза на GitHub загрузи **оба** файла как assets: сам exe и
`.exe.sha256`. Формат строки — `<hex-хеш>  <имя-файла>` (как у стандартного
`sha256sum`), это позволяет также использовать один общий файл
`SHA256SUMS.txt` со строками для всех exe вместо отдельного `.sha256` на
каждый файл — приложение поддерживает оба варианта.
