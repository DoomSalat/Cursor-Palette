# Сборка релиза (Linux)

Релизная сборка Linux-версии — self-contained (не требует установленного
.NET на машине пользователя). Проект `Cursor-Palette.Linux` собирается
отдельно от Windows-версии (`Cursor-Palette`).

## Важно: UseAppHost

В [Cursor-Palette.Linux.csproj](../Cursor-Palette.Linux/Cursor-Palette.Linux.csproj)
стоит `<UseAppHost>false</UseAppHost>` (нужно для обычной разработческой
сборки на Windows). Для self-contained публикации нужен нативный apphost,
поэтому при `dotnet publish` это свойство переопределяется флагом
`-p:UseAppHost=true`. Без этого флага публикация упадёт с ошибкой
`NETSDK1067`.

## Команда

Из корня репозитория:

```powershell
dotnet publish Cursor-Palette.Linux/Cursor-Palette.Linux.csproj -c Release -r linux-x64 --self-contained true -p:UseAppHost=true -o Cursor-Palette.Linux/bin/Release/net8.0/linux-x64/publish
```

Результат — папка
`Cursor-Palette.Linux/bin/Release/net8.0/linux-x64/publish/` (~95 МБ
распакованная), содержащая:

- `Cursor-Palette-Linux` — исполняемый файл (apphost, нативный ELF, но сам
  по себе не содержит .NET-рантайм — он в соседних файлах).
- `libcoreclr.so`, `libhostfxr.so`, `System.*.dll` и т.д. — self-contained
  .NET-рантайм.
- `libSkiaSharp.so`, `libHarfBuzzSharp.so` — нативные зависимости Avalonia.
- `Resources/` — иконки, локализация, дефолтные курсоры.

Все эти файлы должны распространяться вместе (одной папкой/архивом) — по
отдельности исполняемый файл не запустится.

## Флаги

- `-c Release` — релизная конфигурация.
- `-r linux-x64` — таргет платформы (нужен для self-contained). Для ARM —
  `linux-arm64`.
- `--self-contained true` — рантайм .NET упаковывается вместе с приложением.
- `-p:UseAppHost=true` — переопределяет `UseAppHost=false` из csproj, иначе
  publish с self-contained падает с NETSDK1067.

`PublishSingleFile` для Linux-сборки не используется — приложение
распространяется как папка (не как один exe, в отличие от Windows-версии),
чтобы не тащить SkiaSharp/HarfBuzzSharp через self-extract.

## Упаковка в архив

Перед отправкой пользователю упакуй **содержимое** папки `publish/` (не
саму папку с промежуточными путями `bin/Release/net8.0/...`):

```powershell
Compress-Archive -Path "Cursor-Palette.Linux/bin/Release/net8.0/linux-x64/publish/*" -DestinationPath "Cursor-Palette.Linux/bin/Release/Cursor-Palette-Linux-vX.Y.Z-linux-x64.zip"
```

или через WinRAR (сохраняет права на исполнение при распаковке unrar на
Linux лучше, чем zip):

```powershell
& "C:\Program Files\WinRAR\Rar.exe" a -r -ep1 "Cursor-Palette.Linux/bin/Release/Cursor-Palette-Linux-vX.Y.Z-linux-x64.rar" "Cursor-Palette.Linux/bin/Release/net8.0/linux-x64/publish/*"
```

**Не путать** с `Cursor-Palette.Core/bin/Release` — там при обычной сборке
solution лежит только `.dll`/`.pdb` библиотеки `Cursor-Palette.Core`
(десятки КБ), это не рабочее приложение и отправлять его пользователю
бессмысленно.

## Запуск (инструкция для получателя)

```bash
unrar x Cursor-Palette-Linux-vX.Y.Z-linux-x64.rar CursorPalette/
cd CursorPalette
chmod +x Cursor-Palette-Linux
./Cursor-Palette-Linux
```

`chmod +x` обязателен — архиваторы на Windows (RAR/ZIP) не сохраняют unix
права на выполнение.

## Версия

Перед сборкой релиза обнови `<Version>` в
[Cursor-Palette.Linux.csproj](../Cursor-Palette.Linux/Cursor-Palette.Linux.csproj).

## Контрольная сумма

В отличие от Windows-версии
([RELEASE.md](RELEASE.md#контрольная-сумма-обязательно)),
[UpdateChecker.cs](../Cursor-Palette.Linux/Services/UpdateChecker.cs) в
Linux-версии пока не проверяет SHA256 скачанного релиза — он только
сравнивает номер версии и отдаёт ссылку на страницу GitHub Releases для
ручного скачивания. Публиковать `.sha256` для Linux-архива не обязательно,
но желательно для консистентности с Windows-релизами.
