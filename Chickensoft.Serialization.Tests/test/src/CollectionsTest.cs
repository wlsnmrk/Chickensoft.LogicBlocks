namespace Chickensoft.Serialization.Tests;

using System.Collections.Generic;
using System.Text.Json;
using Chickensoft.Collections;
using Chickensoft.Introspection;
using DeepEqual.Syntax;
using Shouldly;
using Xunit;

public partial class CollectionsTest {
  [Meta, Id("book")]
  public partial record Book {
    [Save("title")]
    public string Title { get; set; } = default!;

    [Save("author")]
    public string Author { get; set; } = default!;

    [Save("related_books")]
    public Dictionary<string, List<HashSet<string>>>? RelatedBooks { get; set; }
      = default!;
  }

  [Fact]
  public void SerializesCollections() {
    var book = new Book {
      Title = "The Book",
      Author = "The Author",
      RelatedBooks = new Dictionary<string, List<HashSet<string>>> {
        ["Title A"] = new List<HashSet<string>> {
          new() { "Author A", "Author B" },
          new() { "Author C", "Author D" },
        },
        ["Title B"] = new List<HashSet<string>> {
          new() { "Author E", "Author F" },
          new() { "Author G", "Author H" },
          new()
        },
        ["Title C"] = new()
      },
    };

    var options = new JsonSerializerOptions {
      WriteIndented = true,
      TypeInfoResolver = new SerializableTypeResolver(),
      Converters = { new IdentifiableTypeConverter(new Blackboard()) }
    };

    var json = JsonSerializer.Serialize(book, options);

    json.ShouldBe(
      /*lang=json,strict*/
      """
      {
        "$type": "book",
        "$v": 1,
        "author": "The Author",
        "related_books": {
          "Title A": [
            [
              "Author A",
              "Author B"
            ],
            [
              "Author C",
              "Author D"
            ]
          ],
          "Title B": [
            [
              "Author E",
              "Author F"
            ],
            [
              "Author G",
              "Author H"
            ],
            []
          ],
          "Title C": []
        },
        "title": "The Book"
      }
      """,
      options: StringCompareShould.IgnoreLineEndings
    );

    var bookAgain = JsonSerializer.Deserialize<Book>(json, options);

    bookAgain.ShouldDeepEqual(book);
  }

  [Fact]
  public void DeserializesMissingCollectionsToEmptyOnes() {
    var options = new JsonSerializerOptions {
      TypeInfoResolver = new SerializableTypeResolver(),
      Converters = { new IdentifiableTypeConverter(new Blackboard()) },
      WriteIndented = true
    };

    var json =
      /*lang=json,strict*/
      """
      {
        "$type": "book",
        "$v": 1,
        "author": "The Author",
        "title": "The Book"
      }
      """;

    var book = JsonSerializer.Deserialize<Book>(json, options)!;

    book.RelatedBooks.ShouldBeEmpty();
  }


  [Fact]
  public void DeserializesExplicitlyNullCollectionsToNull() {
    var options = new JsonSerializerOptions {
      TypeInfoResolver = new SerializableTypeResolver(),
      Converters = { new IdentifiableTypeConverter(new Blackboard()) },
      WriteIndented = true
    };

    var json =
      /*lang=json,strict*/
      """
      {
        "$type": "book",
        "$v": 1,
        "author": "The Author",
        "related_books": null,
        "title": "The Book"
      }
      """;

    var book = JsonSerializer.Deserialize<Book>(json, options)!;

    book.RelatedBooks.ShouldBeNull();
  }
}
