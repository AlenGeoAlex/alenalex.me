using AlenAlex.Api.Features.Guestbook.CreateEntry;
using AlenAlex.Api.Features.Guestbook.Shared;

namespace AlenAlex.Api.Tests.Features.Guestbook;

public sealed class CreateEntryValidatorTests
{
    [Fact]
    public void Trims_and_returns_values() =>
        Assert.Equal(("Ada", "hello there"), CreateEntryValidator.Validate("  Ada ", "  hello there "));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Rejects_empty_names(string? name)
    {
        var ex = Assert.Throws<GuestbookValidationException>(() => CreateEntryValidator.Validate(name, "hello"));
        Assert.Equal("name must be 1–40 characters", ex.Message);
    }

    [Fact]
    public void Checks_name_length()
    {
        Assert.Equal(40, CreateEntryValidator.Validate(new string('x', 40), "hello").Name.Length);
        Assert.Throws<GuestbookValidationException>(() => CreateEntryValidator.Validate(new string('x', 41), "hello"));
    }

    [Fact]
    public void Checks_message_length()
    {
        var ex = Assert.Throws<GuestbookValidationException>(() => CreateEntryValidator.Validate("Ada", "  hi  "));
        Assert.Equal("message must be 3–200 characters", ex.Message);
        CreateEntryValidator.Validate("Ada", new string('x', 200));
        Assert.Throws<GuestbookValidationException>(() => CreateEntryValidator.Validate("Ada", new string('x', 201)));
    }

    [Fact]
    public void Counts_characters_not_utf16_units()
    {
        // 3 emoji = 3 characters, although they are 6 UTF-16 units and 12 UTF-8 bytes.
        CreateEntryValidator.Validate("Ada", "🦀🦀🦀");
        CreateEntryValidator.Validate(string.Concat(Enumerable.Repeat("🦀", 40)), "hello");
        Assert.Throws<GuestbookValidationException>(() => CreateEntryValidator.Validate(string.Concat(Enumerable.Repeat("🦀", 41)), "hello"));
        Assert.Throws<GuestbookValidationException>(() => CreateEntryValidator.Validate("Ada", "🦀🦀"));
    }

    [Fact]
    public void Generates_21_character_url_safe_ids()
    {
        var id = NanoId.New();
        Assert.Equal(21, id.Length);
        Assert.All(id, c => Assert.True(char.IsAsciiLetterOrDigit(c) || c is '_' or '-'));
    }
}
