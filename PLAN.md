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

- Wykorzystać dodanego fakera do generowania danych, po potwierdzeniu jego konfiguracji w projekcie.
- Przygotować co najmniej jeden realistyczny szablon, np. raport z arkuszami podsumowania i transakcji.
- Umożliwić ustawienie liczby plików, liczby rekordów i ziarna generatora danych (`seed`).
- Zapewnić poprawne typy komórek, spójne dane oraz praktyczne formatowanie, np. nagłówki i filtry.
- Weryfikować konfigurację i zgłaszać błędy zamiast tworzyć pozornie poprawny wynik.

### 2. Profile i scenariusze

- **Zwykły plik:** fikcyjne dane bez celowo mylących instrukcji.
- **Prompt injection:** fikcyjne dane oraz instrukcja z unikalnym, nieszkodliwym znacznikiem pozwalającym sprawdzić, czy agent wykonał polecenie z dokumentu.
- **Zabezpieczony plik:** skoroszyt zaszyfrowany losowym hasłem do otwarcia.
- Umożliwić łączenie profili, w tym generowanie zaszyfrowanego skoroszytu zawierającego prompt injection. Taka zawartość nie będzie dostępna bez odtworzenia hasła.
- Opcjonalnie dodać scenariusze mylących danych oraz tematów wrażliwych, oddzielone od prompt injection.
- Określić wspierane miejsca umieszczania scenariuszy, zaczynając od komórek; komentarze i ukryte arkusze można dodać w kolejnym etapie.

### 3. Generowanie haseł i szyfrowanie

- Wybrać bibliotekę, która obsługuje szyfrowanie XLSX wymagające hasła do otwarcia w Excelu; sprawdzić jej licencję, zgodność z docelowym .NET i obsługę wymaganej skali danych.
- Generować osobne hasło dla każdego zabezpieczanego pliku za pomocą kryptograficznie bezpiecznego generatora losowego. Długość hasła określić w projekcie API.
- Nie umieszczać hasła w skoroszycie, nazwie pliku, manifeście, logach ani komunikatach błędów.
- Nie używać seeda danych do generowania haseł. Powtarzalność danych nie oznacza identycznych zaszyfrowanych bajtów.
- Przekazywać błędy szyfrowania użytkownikowi biblioteki; nie zwracać niezabezpieczonego pliku jako wyniku operacji szyfrowania.

### 4. Publiczne API i wynik

- Zaprojektować typowane opcje generowania oraz wynik opisujący utworzone pliki.
- Obsługiwać generowanie pojedynczego pliku i zestawu plików; rozważyć zapis do strumieni lub katalogu docelowego bez wymuszania konkretnego hosta aplikacji.
- Zwracać manifest zawierający identyfikatory plików, zastosowane profile i informacje potrzebne do oceny scenariuszy, bez haseł.
- Manifest pozostaje poza generowanymi skoroszytami; aplikacja decyduje, czy i gdzie go zapisać.
- Jednoznacznie określić zachowanie przy kolizjach nazw, nieprawidłowych opcjach, przerwaniu generowania i błędach zapisu.
- Archiwizacja ZIP oraz integracje z ASP.NET Core nie należą do pierwszej wersji pakietu.

## Wydajność

- Określić reprezentatywne rozmiary zestawów i mierzalne cele czasu oraz zużycia pamięci przed optymalizacją.
- Zmierzyć osobno generowanie zwykłych i szyfrowanych plików; wskazać środowisko oraz konfigurację pomiaru.
- Ograniczać zbędne kopie danych i sprawdzić, czy wybrane biblioteki umożliwiają przetwarzanie dużych zbiorów bez nadmiernego zużycia pamięci.

## Weryfikacja

1. Sprawdzić poprawność struktury XLSX, typów komórek i wygenerowanych danych.
2. Sprawdzić, że plik szyfrowany nie otwiera się bez hasła ani z błędnym hasłem. W kontrolowanym teście wewnętrznym potwierdzić też otwarcie z hasłem użytym podczas szyfrowania, bez dodawania go do publicznego wyniku.
3. Zweryfikować, że manifest i publiczne wyniki nie zawierają haseł.
4. Sprawdzić scenariusze prompt injection, ich położenie i znaczniki względem manifestu.
5. Sprawdzić powtarzalność fikcyjnych danych dla tego samego seeda oraz niezależność generowania haseł od tego seeda.
6. Przetestować błędną konfigurację, błędy zapisu i szyfrowania, a także generowanie większej liczby rekordów.
7. Zbudować pakiet i sprawdzić jego użycie w prostej aplikacji konsolowej, bez dodawania integracji API.

## Kolejność realizacji

1. Potwierdzić konfigurację fakera oraz wybrać bibliotekę XLSX i mechanizm szyfrowania.
2. Zaprojektować API, opcje, manifest i zakres pierwszego szablonu.
3. Zaimplementować generowanie danych i zwykłych skoroszytów.
4. Dodać profile prompt injection oraz odpowiadające im dane w manifeście.
5. Dodać generowanie losowych haseł i szyfrowanie plików.
6. Dodać testy, pomiary wydajności i przykładowe użycie.
7. Uzupełnić metadane, dokumentację i konfigurację pakowania NuGet.