# FuzzyScorer

[![NuGet](https://img.shields.io/nuget/v/FuzzyScorer)](https://www.nuget.org/packages/FuzzyScorer)
[![NuGet Downloads](https://img.shields.io/nuget/dt/FuzzyScorer)](https://www.nuget.org/packages/FuzzyScorer)
[![License: MIT](https://img.shields.io/github/license/lukaszow/FuzzyScorer)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/10.0)

> [English documentation](README.md)

FuzzyScorer to biblioteka .NET, która zlicza słowa w nieczystym tekście i łączy literówki z ich poprawnymi formami — np. "aple" i "Apple" liczy jako "apple" — dając dokładne częstotliwości w jednym wywołaniu.

Użyj statycznej klasy `WordScorer` do szybkiej analizy częstotliwości/podobieństwa, lub wstrzyknij `IFuzzyScorer` do asynchronicznego wykrywania literówek — bez konfiguracji i zależności.

## Spis treści

- [Szybki start](#szybki-start)
- [Jak to działa](#jak-to-działa)
- [API](#api)
- [Limity i bezpieczeństwo](#limity-i-bezpieczeństwo)
- [FAQ](#faq)
- [Licencja](#licencja)

## Szybki start

### Instalacja

```bash
dotnet add package FuzzyScorer
```

Wymaga [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0). Pakiet zawiera dokumentację XML dla pełnego IntelliSense.

### Zliczanie słów z łączeniem literówek

```csharp
using FuzzyScorer;

string text = "apple aple Apple";
var results = WordScorer.GroupSimilarWords(text, maxEditDistance: 1);

foreach (var word in results)
    Console.WriteLine($"{word.Text}: {word.Score}");

// Wynik:
// apple: 3
```

To cała biblioteka w jednym wywołaniu — bez konfiguracji i dodatkowych ustawień.

## Jak to działa

1. **Normalizacja** — tekst jest czyszczony (usuwane są znaki niebędące literami, cyframi, myślnikami lub spacjami), a słowa są dzielone na podstawie białych znaków.
2. **Zliczanie** — słowa są porównywane bez rozróżniania wielkości liter.
3. **Łączenie** — słowa o odległości Levenshteina ≤ `maxEditDistance` są grupowane, a pierwsze wystąpienie staje się reprezentantem grupy.

**Uwaga:** FuzzyScorer działa strukturalnie, nie semantycznie. `"TIGER"` i `"TlGER"` zostaną połączone, ale `"cat"` i `"dog"` nigdy.

### Przykłady zastosowań

- **Ankiety i opinie** — scal literówki w wynikach ankiet (np. `"Excelent"` i `"Excellent"`), aby zobaczyć prawdziwy konsensus w chmurze słów.
- **Czyszczenie OCR** — napraw tekst, w którym `"l"` (małe L) jest mylone z `"I"` (duże I) w zeskanowanych dokumentach.
- **Deduplikacja rekordów** — wykryj duplikaty takie jak `"John"` i `"Jon"` w bazie klientów.
- **Filtrowanie spamu** — wyłapuj zniekształcone słowa, które omijają proste filtry (np. `"M0ney"`, `"W4tch"`).

## API

### Statyczne metody (`WordScorer`)

Wszystkie statyczne metody zwracają niezmienne instancje `WordScore` (tekst słowa + wynik) i nigdy nie zwracają `null`; kolekcje wyników są eksponowane jako `IReadOnlyList<T>`.

#### `GetWordFrequencies(string? text[, CancellationToken ct])`

Dokładne zliczanie częstotliwości bez łączenia literówek (bez rozróżniania wielkości liter).

```csharp
var results = WordScorer.GetWordFrequencies("Apple apple APPLE");
// results: [ apple: 3 ]
```

#### `GroupSimilarWords(string? text, int maxEditDistance[, CancellationToken ct])`

Zliczanie **plus** łączenie literówek na podstawie odległości Levenshteina. Słowa w odległości ≤ `maxEditDistance` od pierwszego wystąpienia są scalane.

- **Pierwsze wystąpienie** słowa staje się reprezentantem grupy — wyniki zależą od kolejności wejściowej.
- `maxEditDistance` musi być w zakresie 0–50.

```csharp
var results = WordScorer.GroupSimilarWords("apple aple apple", maxEditDistance: 1);
// results: [ apple: 3 ]
```

#### `AreWordsSimilar(string a, string b, int maxEditDistance)`

Szybkie sprawdzenie, czy `b` jest w odległości ≤ `maxEditDistance` od `a` (bez rozróżniania wielkości liter). Rzuca `ArgumentNullException` dla `null` i `ArgumentException` dla odległości spoza zakresu 0–50.

```csharp
bool isTypo = WordScorer.AreWordsSimilar("aple", "apple", maxEditDistance: 1); // true
```

#### `GroupWordsBySimilarity(IEnumerable<string> words, int maxEditDistance, CancellationToken ct)`

Grupuje **wcześniej znormalizowaną listę słów** (bez parsowania tekstu). Każda wewnętrzna lista to jedna grupa; pierwsze wystąpienie jest liderem. Waliduje `words` (nie-`null`) oraz `maxEditDistance` (0–50).

```csharp
var groups = WordScorer.GroupWordsBySimilarity(
    new List<string> { "apple", "aple", "banana" },
    maxEditDistance: 1,
    CancellationToken.None);
// groups: [ [apple, aple], [banana] ]
```

### Metody instancyjne (`IFuzzyScorer` / `FuzzyScorer`)

DI-friendly asynchroniczne API z wykrywaniem literówek. Zarejestruj `IFuzzyScorer` → `FuzzyScorer` w kontenerze DI lub użyj `new FuzzyScorer()` bezpośrednio.

#### `ScoreAsync(string text, double sensitivity, CancellationToken ct)`

```csharp
using FuzzyScorer;

IFuzzyScorer scorer = new FuzzyScorer();
string text = "apple aple apple\nbanana cherry";

var result = await scorer.ScoreAsync(text, sensitivity: 0.02, CancellationToken.None);

Console.WriteLine($"Oryginalnych słów: {result.OriginalSize}");   // 5
Console.WriteLine($"Po kompresji:      {result.CompressedSize}");  // 4 (aple scalone z apple)

foreach (var error in result.Errors)
    Console.WriteLine($"Literówka '{error.ErrorText}' (x{error.RepetitionCount}) w liniach: {string.Join(",", error.LineNumbers)}");
// Wynik:
// Literówka 'aple' (x1) w liniach: 1
```

`sensitivity` (0.0–1.0) mapuje liniowo na maksymalną odległość edycji: `maxEditDistance = round(sensitivity × 50)`.

| sensitivity | max edit distance | zachowanie |
|---|---|---|
| 0.0 | 0 | tylko dokładne dopasowanie (bez rozróżniania wielkości liter) |
| 0.02 | 1 | łapie typowe literówki (`"aple"` → `"apple"`) |
| 0.5 | 25 | agresywne łączenie |
| 1.0 | 50 | maksymalna fuzziness |

Zwraca `FuzzyScorerResult`:
- **OriginalSize** — całkowita liczba słów po normalizacji
- **CompressedSize** — liczba unikalnych grup po łączeniu
- **Errors** — wykryte potencjalne literówki: każdy członek grupy, który nie jest najczęstszym słowem w grupie, raportowany jako `ErrorEntry` (`ErrorText`, `RepetitionCount`, 1‑based `LineNumbers`)

### Reguły normalizacji

Przed analizą tekst jest:
- Oczyszczany z wszystkiego, co nie jest literą Unicode, cyfrą, myślnikiem lub białym znakiem (`"hello!"` → `"hello"`, `"café"` pozostaje, `"well-known"` pozostaje, `"don't"` → `"dont"`)
- Dzielony na słowa na podstawie białych znaków
- Porównywany bez rozróżniania wielkości liter

## Limity i bezpieczeństwo

- Wejście jest walidowane przed przetwarzaniem: maks. **1 000 000** znaków, **10 000** słów na tekst, **256** znaków na słowo, odległość edycji ograniczona do **50**.
- Grupowanie jest ograniczone twardym budżetem **10 000 000** porównań odległości edycji (`WordScorer.MaxSimilarityComparisons`); przekroczenie rzuca `ArgumentException` zamiast niezwiązanego czasowo przetwarzania kwadratowego.
- Odległość Levenshteina liczona jest w pasmach, z wczesnym przerwaniem po przekroczeniu progu i pamięcią `O(min(n, m))`.
- Słowa dłuższe niż 256 znaków są **cicho pomijane** (nie jest to błąd).
- Limity są publicznymi stałymi (`const`) na `WordScorer` (`MaxInputLength`, `MaxWordsPerText`, `MaxWordLength`, `MaxEditDistanceLimit`, `MaxSimilarityComparisons`) — są to stałe czasu kompilacji i nie można ich zmieniać w runtime.
- Brak niebezpiecznego kodu, brak niezarządzanej pamięci, brak zależności runtime poza .NET BCL.
- Anulowanie: każda długotrwała ścieżka akceptuje `CancellationToken` — przez przeciążenia metod statycznych oraz jako wymagany parametr w `ScoreAsync` / `GroupWordsBySimilarity`.
- Wyniki są niezmienne (`WordScore`, `FuzzyScorerResult`, `ErrorEntry`) — walidowane przy konstrukcji, tylko do odczytu później.

Pełny model zagrożeń i proces zgłaszania: [SECURITY.md](SECURITY.md).

## FAQ

**Czy FuzzyScorer rozumie znaczenie?**
Nie. Działa strukturalnie (Levenshtein), nie semantycznie. `"TIGER"` i `"TlGER"` pasują; `"cat"` i `"dog"` nigdy.

**Jak wykrywać literówki?**
Użyj `IFuzzyScorer.ScoreAsync` — raportuje każdego członka grupy, który nie jest najczęstszym słowem w grupie, z numerami linii (1‑based).

**Co się dzieje ze słowami dłuższymi niż 256 znaków?**
Są cicho pomijane.

**Czy wymagana jest konfiguracja lub DI?**
Nie. Metody statyczne działają od razu; API instancyjne to po prostu `new FuzzyScorer()`.

## Licencja

MIT — zobacz [LICENSE](LICENSE).
