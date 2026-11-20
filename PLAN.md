# Plan: generator danych i plików Excel

## Cel

Stworzyć pakiet NuGet dla .NET, który szybko generuje duże ilości fikcyjnych danych i realistycznie wyglądających plików Excel. Pakiet ma umożliwiać tworzenie plików zwykłych, plików z kontrolowanymi scenariuszami prompt injection oraz plików zabezpieczonych losowym hasłem.

Pakiet nie będzie implementował endpointów ani integracji z konkretną aplikacją. Aplikacja korzystająca z biblioteki decyduje, gdzie i w jaki sposób udostępnić wygenerowane pliki.

## Założenia

- Wszystkie generowane dane są fikcyjne. Nie korzystamy z prawdziwych danych osobowych, haseł ani sekretów.
- Pliki są poprawnymi skoroszytami `.xlsx`, z realistyczną strukturą i formatowaniem.
- Hasło do każdego zabezpieczanego pliku jest generowane losowo, używane do zaszyfrowania pliku, a następnie nie jest zapisywane, logowane ani zwracane użytkownikowi biblioteki.
- Hasło zabezpiecza **otwarcie pliku**, a nie tylko edycję arkusza lub skoroszytu.
- Zawartość zaszyfrowanego pliku nie będzie dostępna użytkownikowi biblioteki bez odtworzenia hasła. Nie obiecujemy możliwości jego odzyskania.
- Prompt injection służy do kontrolowanych testów i korzysta z nieszkodliwych znaczników. Pliki nie zawierają makr ani prawdziwych instrukcji wykradania danych.
- Tematy wrażliwe są osobnym, opcjonalnym profilem danych, a nie synonimem prompt injection.
- Samo wygenerowanie lub pobranie pliku nie dowodzi, kto go otworzył ani czy został otwarty.

## Zakres funkcjonalny

### 1. Dane i skoroszyty

- Generować dane za pomocą Bogus (lokalizacja konfigurowalna, domyślnie `pl`).
- Przygotować co najmniej jeden realistyczny szablon, np. raport z arkuszami podsumowania i transakcji.
- Dodać generator danych IT (`ItFakeData`) łączący Bogus z BCL: hashe (MD5/SHA-1/SHA-256 z losowych bajtów), SHA commitów, IP/MAC/porty, User-Agent, connection stringi, wartości w stylu JWT, klucze publiczne SSH/PEM, GUID v7.
- Fałszywe sekrety (klucze API, tokeny, hasła w danych) domyślnie mają prefiks jednoznacznie wskazujący fikcyjność, np. `FAKE-` lub `bam_test_`. Realistyczne formaty (np. `AKIA…`, `ghp_…`) tylko jako jawna opcja, oznaczana w manifeście, i wyłącznie generowane w czasie działania — nigdy przechowywane w repozytorium.
- Umożliwić ustawienie liczby plików, liczby rekordów i ziarna generatora danych (`seed`).
- Zapewnić poprawne typy komórek, spójne dane oraz praktyczne formatowanie, np. nagłówki i filtry.
- Weryfikować konfigurację i zgłaszać błędy zamiast tworzyć pozornie poprawny wynik.

### 2. Profile i scenariusze

- **Zwykły plik:** fikcyjne dane bez celowo mylących instrukcji.
- **Prompt injection:** fikcyjne dane oraz instrukcja z unikalnym, nieszkodliwym znacznikiem pozwalającym sprawdzić, czy agent wykonał polecenie z dokumentu.
- **Zabezpieczony plik:** skoroszyt zaszyfrowany losowym hasłem do otwarcia.
- Umożliwić łączenie profili, w tym generowanie zaszyfrowanego skoroszytu zawierającego prompt injection. Taka zawartość nie będzie dostępna bez odtworzenia hasła.
- Opcjonalnie dodać scenariusze mylących danych oraz tematów wrażliwych, oddzielone od prompt injection.
- Określić wspierane miejsca umieszczania scenariuszy, zaczynając od komórek; w kolejnych etapach: komentarze, ukryte i „very hidden” arkusze, ukryte wiersze/kolumny, tekst niewidoczny (np. biały na białym), właściwości dokumentu, nazwy zdefiniowane, walidacja danych, nagłówki i stopki.

### 2a. Biblioteka prompt injection

- Każde injection to osobny plik JSON w katalogu `Injections/<kategoria>/<id>.json`, dołączany do pakietu jako `EmbeddedResource` (wzorzec `Injections\**\*.json`). Dodanie nowego injection to commit z nowym plikiem, bez zmian w kodzie.
- Format pliku opisany w `Injections/schema.json` (podpowiedzi i walidacja w edytorze). Pola: `id`, `category`, `language`, `text`, `placements`, `tags`.
- `text` musi zawierać `{{marker}}`; generator podstawia unikalny znacznik (np. `BAM-<guid>`), zapisywany w manifeście razem z miejscem umieszczenia.
- Pliki zawierają czytelny tekst; poziom trudności realizuje kod w czasie generowania.
- Poziomy trudności (`Difficulty`): `Plain` oraz `Obfuscated` (domyślny). Transformacje obfuskacji wybierane deterministycznie z seeda: podział na kilka komórek/wierszy, Base64 z instrukcją dekodowania, znaki o zerowej szerokości i homoglify, rozbicie znacznika na fragmenty.
- Użytkownik biblioteki może dołączyć własne pliki w tym samym formacie (`WithInjectionsFrom(path)`).
- Opcjonalny ewaluator: przyjmuje odpowiedź agenta i manifest, zwraca, które injection zostały wykonane (wykrycie znacznika).

### 3. Generowanie haseł i szyfrowanie

- ClosedXML nie obsługuje szyfrowania wymagającego hasła do otwarcia. Szyfrowanie realizuje NPOI (Apache 2.0, ECMA-376 Agile Encryption): skoroszyt zapisany przez ClosedXML / OpenXML jest szyfrowany w strumieniu przez NPOI. Potwierdzić zgodność z Excelem i zachowanie przy dużych plikach.
- Generować osobne hasło dla każdego zabezpieczanego pliku za pomocą kryptograficznie bezpiecznego generatora losowego. Długość hasła określić w projekcie API.
- Nie umieszczać hasła w skoroszycie, nazwie pliku, manifeście, logach ani komunikatach błędów.
- Nie używać seeda danych do generowania haseł. Powtarzalność danych nie oznacza identycznych zaszyfrowanych bajtów.
- Przekazywać błędy szyfrowania użytkownikowi biblioteki; nie zwracać niezabezpieczonego pliku jako wyniku operacji szyfrowania.

### 4. Publiczne API i wynik

- Zaprojektować typowane opcje generowania oraz wynik opisujący utworzone pliki.
- API w stylu fluent, np.:

  ```csharp
  var result = await WorkbookGenerator.Create()
      .WithTemplate(Templates.SalesReport)
      .WithRows(50_000).WithSeed(42)
      .WithInjection()      // jedno losowe injection z puli, Difficulty.Obfuscated
      .Encrypted()
      .GenerateAsync(outputDir, count: 100);
  ```

  - `.WithInjection()` — jedno losowe (z seeda) injection z puli w każdym pliku; domyślnie `Difficulty.Obfuscated`.
  - `.WithInjections()` — wszystkie injection z puli w każdym pliku; domyślnie `Difficulty.Obfuscated`.
  - Oba warianty przyjmują opcjonalną konfigurację (filtr kategorii/tagów, trudność, miejsca umieszczenia).
- Logowanie przez `ILogger` z `Microsoft.Extensions.Logging.Abstractions` (domyślnie `NullLogger`); hasła nigdy nie trafiają do logów.
- Obsługiwać generowanie pojedynczego pliku i zestawu plików; rozważyć zapis do strumieni lub katalogu docelowego bez wymuszania konkretnego hosta aplikacji.
- Zwracać manifest zawierający identyfikatory plików, zastosowane profile i informacje potrzebne do oceny scenariuszy, bez haseł.
- Manifest pozostaje poza generowanymi skoroszytami; aplikacja decyduje, czy i gdzie go zapisać.
- Jednoznacznie określić zachowanie przy kolizjach nazw, nieprawidłowych opcjach, przerwaniu generowania i błędach zapisu.
- Archiwizacja ZIP oraz integracje z ASP.NET Core nie należą do pierwszej wersji pakietu.

## Struktura rozwiązania i paczki

Wszystkie projekty: `net10.0`, licencje paczek MIT / Apache 2.0 / BSD (bez EPPlus, Aspose, FluentAssertions 8+).

| Projekt | Paczka | Wersja | Po co |
|---|---|---|---|
| `Blow.AI.Mind` (biblioteka NuGet) | Bogus | 35.6.5 | Fikcyjne dane |
| | ClosedXML | 0.105.1 | Budowa skoroszytów i formatowanie |
| | DocumentFormat.OpenXml | 3.5.1 | Strumieniowy zapis dużych arkuszy (`OpenXmlWriter`) |
| | NPOI | 2.8.1 | Szyfrowanie hasłem do otwarcia |
| | Microsoft.Extensions.Logging.Abstractions | 11.0.0 | `ILogger` bez wymuszania implementacji |
| | MinVer (`PrivateAssets=all`) | 8.0.0 | Wersja pakietu z tagów git |
| `Blow.AI.Mind.Tests` | NUnit | 5.0.0 | Framework testowy |
| | NUnit3TestAdapter | 6.3.1 | Uruchamianie testów w VS / `dotnet test` |
| | Microsoft.NET.Test.Sdk | 18.12.0 | Host testów |
| | NUnit.Analyzers | 4.15.0 | Analiza poprawności testów |
| | Shouldly | 4.3.0 | Czytelne asercje |
| | Verify.NUnit | 33.7.1 | Testy snapshotowe |
| | Verify.OpenXml | 2.2.0 | Snapshoty zawartości XLSX (powtarzalność seeda) |
| | ExcelDataReader | 3.9.0 | Niezależny odczyt wygenerowanych plików |
| `Blow.AI.Mind.Benchmarks` | BenchmarkDotNet | 0.15.8 | Pomiary czasu i alokacji |
| `Blow.AI.Mind.Cli` (`dotnet tool`) | Spectre.Console | 0.57.2 | Interfejs konsolowy, pasek postępu |
| | Spectre.Console.Cli | 0.57.2 | Komendy i parsowanie argumentów |

Walidacja JSON, generowanie haseł (`RandomNumberGenerator`), hashe i klucze realizowane przez BCL (`System.Text.Json`, `System.Security.Cryptography`).

## Wydajność

- Określić reprezentatywne rozmiary zestawów i mierzalne cele czasu oraz zużycia pamięci przed optymalizacją.
- Zmierzyć osobno generowanie zwykłych i szyfrowanych plików; wskazać środowisko oraz konfigurację pomiaru.
- Ograniczać zbędne kopie danych i sprawdzić, czy wybrane biblioteki umożliwiają przetwarzanie dużych zbiorów bez nadmiernego zużycia pamięci.

## Weryfikacja

1. Sprawdzić poprawność struktury XLSX, typów komórek i wygenerowanych danych.
2. Sprawdzić, że plik szyfrowany nie otwiera się bez hasła ani z błędnym hasłem. W kontrolowanym teście wewnętrznym potwierdzić też otwarcie z hasłem użytym podczas szyfrowania, bez dodawania go do publicznego wyniku.
3. Zweryfikować, że manifest i publiczne wyniki nie zawierają haseł.
4. Sprawdzić scenariusze prompt injection, ich położenie i znaczniki względem manifestu.
5. Test biblioteki injection: każdy wbudowany plik JSON się wczytuje, `id` są unikalne, `text` zawiera `{{marker}}`, `placements` mają tylko wspierane wartości.
6. Sprawdzić `.WithInjection()` (dokładnie jedno injection na plik) i `.WithInjections()` (wszystkie injection w każdym pliku) oraz domyślną trudność `Obfuscated`.
7. Sprawdzić, że fałszywe sekrety domyślnie mają prefiks fikcyjności.
8. Sprawdzić powtarzalność fikcyjnych danych dla tego samego seeda (snapshoty zawartości komórek, nie bajtów) oraz niezależność generowania haseł od tego seeda.
9. Przetestować błędną konfigurację, błędy zapisu i szyfrowania, a także generowanie większej liczby rekordów.
10. Zbudować pakiet i sprawdzić jego użycie przez `Blow.AI.Mind.Cli`, bez dodawania integracji API.

## Kolejność realizacji

1. Utworzyć strukturę rozwiązania (biblioteka, testy, benchmarki, CLI) i dodać paczki z tabeli.
2. Zaprojektować fluent API, opcje, manifest i zakres pierwszego szablonu.
3. Zaimplementować generowanie danych (w tym `ItFakeData`) i zwykłych skoroszytów.
4. Dodać bibliotekę injection (JSON + schemat + loader), `.WithInjection()` / `.WithInjections()`, obfuskację i znaczniki w manifeście.
5. Dodać generowanie losowych haseł i szyfrowanie plików przez NPOI.
6. Dodać testy, ewaluator znaczników, pomiary wydajności i CLI.
7. Uzupełnić metadane, dokumentację, MinVer i konfigurację pakowania NuGet.