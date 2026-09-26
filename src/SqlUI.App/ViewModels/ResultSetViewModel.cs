using System.Data;

namespace SqlUI.App.ViewModels;

public class ResultSetViewModel
{
    public DataTable Table { get; }
    public DataView View { get; }
    public int RowCount => View.Count;
    public string? EditableTable { get; set; } // dolu ise DataGrid duzenlenebilir ve UPDATE gonderilir
    public bool IsEditable => EditableTable != null;

    public ResultSetViewModel(DataTable table)
    {
        Table = table;
        View = table.DefaultView;
    }

    // Sadece EditableTable doluyken cagrilir. DataTable zaten doldurulmus haldeyken abone
    // oluyoruz, bu yuzden ColumnChanged sadece kullanicinin DataGrid uzerinden yaptigi
    // gercek duzenlemelerde tetiklenir (ilk veri yuklemesinde degil).
    public void EnableEditing(Func<DataRow, string, object?, Task> onCellChanged)
    {
        Table.ColumnChanged += (s, e) => { if (e.Row != null) _ = onCellChanged(e.Row, e.Column.ColumnName, e.ProposedValue); };
    }
}
