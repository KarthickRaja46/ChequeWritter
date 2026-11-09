using ChequeWriter.Core;

namespace ChequeWriter.Tests;

public class TextWrapTests
{
    // 1 unit per character keeps the tests independent of fonts
    static double Len(string s) => s.Length;

    [Fact]
    public void Fills_first_line_then_second()
    {
        var r = TextWrap.TwoLines("aaa bbb ccc ddd", 8, 20, Len);
        Assert.Equal("aaa bbb", r.Line1);
        Assert.Equal("ccc ddd", r.Line2);
        Assert.False(r.Overflow);
    }

    [Fact]
    public void Short_text_stays_on_line_one()
    {
        var r = TextWrap.TwoLines("**Ten Only**", 50, 50, Len);
        Assert.Equal("**Ten Only**", r.Line1);
        Assert.Equal("", r.Line2);
    }

    [Fact]
    public void Too_long_text_is_reported()
    {
        var r = TextWrap.TwoLines("aaaa bbbb cccc dddd eeee", 9, 9, Len);
        Assert.True(r.Overflow);
    }
}

public class DataStoreTests : IDisposable
{
    readonly string _dir = Path.Combine(Path.GetTempPath(), "cw-tests-" + Guid.NewGuid().ToString("N"));
    string FilePath => Path.Combine(_dir, "data.json");

    [Fact]
    public void New_data_has_only_the_rak_layout()
    {
        var store = new DataStore(FilePath);
        var bank = Assert.Single(store.Data.Banks);
        Assert.Equal(DefaultLayouts.RakBusinessId, bank.Id);
        Assert.Equal(7, bank.Fields.Count);
        Assert.Equal(2, bank.Lines.Count);
    }

    [Fact]
    public void Data_survives_save_and_reload()
    {
        var store = new DataStore(FilePath);
        store.Data.Banks[0].OffsetXmm = 1.5;
        store.Data.Banks[0].Fields.First(f => f.Kind == FieldKind.AmountFigures).Y = 44.1;
        store.Data.Cheques.Add(new ChequeRecord { ChequeNo = "988076", Payee = "TEST", Amount = 1354.50m });
        store.RememberPayee("Desert Star");
        store.RememberPayee("desert star");   // duplicate, different case
        store.Save();
        store.Save();                          // second save exercises the .bak replace path

        var again = new DataStore(FilePath);
        Assert.Equal(1.5, again.Data.Banks[0].OffsetXmm);
        Assert.Equal(44.1, again.Data.Banks[0].Fields.First(f => f.Kind == FieldKind.AmountFigures).Y);
        Assert.Equal(1354.50m, Assert.Single(again.Data.Cheques).Amount);
        Assert.Single(again.Data.Payees);
        Assert.True(File.Exists(FilePath + ".bak"));
    }

    [Fact]
    public void Backup_and_restore()
    {
        var store = new DataStore(FilePath);
        store.Data.Payees.Add(new Payee { Name = "Kept" });
        var backup = Path.Combine(_dir, "backup.json");
        store.BackupTo(backup);
        store.Data.Payees.Clear();
        store.Save();
        store.RestoreFrom(backup);
        Assert.Equal("Kept", Assert.Single(store.Data.Payees).Name);
    }

    [Theory]
    [InlineData("988076", "988077")]
    [InlineData("000099", "000100")]
    [InlineData("", "")]
    [InlineData("A12", "A12")]
    public void Next_cheque_number(string current, string expected) => Assert.Equal(expected, DataStore.NextNumber(current));

    [Fact]
    public void Csv_quotes_and_blocks_formulas()
    {
        var csv = CsvExport.Register(new[] { new ChequeRecord { ChequeNo = "1", Payee = "=HYPERLINK(\"x\")", Amount = 5 } });
        Assert.Contains("\"'=HYPERLINK(\"\"x\"\")\"", csv);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }
}
