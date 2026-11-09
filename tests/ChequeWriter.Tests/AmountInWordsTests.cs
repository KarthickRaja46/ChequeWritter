using ChequeWriter.Core;

namespace ChequeWriter.Tests;

public class AmountInWordsTests
{
    [Theory]
    [InlineData(0, "Zero")]
    [InlineData(7, "Seven")]
    [InlineData(19, "Nineteen")]
    [InlineData(20, "Twenty")]
    [InlineData(54, "Fifty Four")]
    [InlineData(100, "One Hundred")]
    [InlineData(115, "One Hundred Fifteen")]
    [InlineData(1000, "One Thousand")]
    [InlineData(1354, "One Thousand Three Hundred Fifty Four")]
    [InlineData(4523, "Four Thousand Five Hundred Twenty Three")]
    [InlineData(100000, "One Hundred Thousand")]
    [InlineData(1000001, "One Million One")]
    [InlineData(2500000000, "Two Billion Five Hundred Million")]
    public void Number_in_words(long n, string expected) => Assert.Equal(expected, AmountInWords.Number(n));

    // Same wording as the RAK test prints: "Four Thousand Five Hundred Twenty Three And Fils Twenty"
    [Theory]
    [InlineData("4523.20", "Four Thousand Five Hundred Twenty Three And Fils Twenty")]
    [InlineData("1354.50", "One Thousand Three Hundred Fifty Four And Fils Fifty")]
    [InlineData("1250", "One Thousand Two Hundred Fifty")]
    [InlineData("0.75", "Zero And Fils Seventy Five")]
    [InlineData("10.05", "Ten And Fils Five")]
    public void Cheque_words_with_fils(string amount, string expected) =>
        Assert.Equal(expected, AmountInWords.ForCheque(decimal.Parse(amount), new AppSettings()));

    [Fact]
    public void Fils_are_rounded_to_two_decimals()
    {
        Assert.Equal((10L, 1), AmountInWords.Split(10.005m));
        Assert.Equal((11L, 0), AmountInWords.Split(10.999m));
    }

    [Theory]
    [InlineData("4523.2", "4,523.20")]
    [InlineData("1000000", "1,000,000.00")]
    [InlineData("0.5", "0.50")]
    public void Figures_format(string amount, string expected) =>
        Assert.Equal(expected, AmountInWords.Figures(decimal.Parse(amount)));

    [Fact]
    public void Negative_or_huge_amounts_are_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => AmountInWords.Split(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => AmountInWords.Split(AmountInWords.MaxAmount + 1));
    }

    [Fact]
    public void Full_cheque_text_uses_wrappers()
    {
        var bank = DefaultLayouts.RakBusiness();
        var t = ChequeText.Build(new ChequeData { Payee = " Target Signs Llc ", Amount = 4523.20m, Date = new DateTime(2026, 11, 9) },
            bank, new AppSettings());
        Assert.Equal("**TARGET SIGNS LLC**", t.Payee);
        Assert.Equal("**4,523.20**", t.Figures);
        Assert.Equal("**Four Thousand Five Hundred Twenty Three And Fils Twenty Only**", t.Words);
        Assert.Equal("09-11-2026", t.Date);
    }
}
