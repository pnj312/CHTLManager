using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CHTLManager.DAL.Entities;

namespace CHTLManager.WPF.Helpers;

public static class DataGridHelper
{
    /// <summary>Bấm một lần là sửa được ô, và chọn sẵn chữ cũ để gõ đè; mặc định của DataGrid phải bấm hai lần.</summary>
    public static void EnableSingleClickEdit(DataGrid grid)
    {
        grid.PreviewMouseLeftButtonDown += (_, e) =>
        {
            var cell = FindParent<DataGridCell>(e.OriginalSource as DependencyObject);
            if (cell == null || cell.IsEditing || cell.IsReadOnly) return;

            grid.SelectedItem = cell.DataContext;
            grid.CurrentCell = new DataGridCellInfo(cell);
            cell.Focus();
            grid.BeginEdit();
            e.Handled = true;
        };

        grid.PreparingCellForEdit += (_, e) =>
        {
            if (e.EditingElement is TextBox box)
            {
                box.Focus();
                box.SelectAll();
            }
        };
    }

    /// <summary>Lưới sản phẩm chọn cả dòng nên Ctrl+C mặc định chép cả dòng. Ở đây Ctrl+C và chuột phải chỉ chép mã vạch.</summary>
    public static void EnableBarcodeCopy(DataGrid grid)
    {
        void Copy()
        {
            if (grid.SelectedItem is not Product product) return;
            try
            {
                Clipboard.SetText(product.Barcode);
            }
            catch (ExternalException)
            {
                MessageBox.Show("Không sao chép được vì chương trình khác đang dùng bộ nhớ tạm. Hãy thử lại.", "Thông báo",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        grid.CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy, (_, e) =>
        {
            Copy();
            e.Handled = true;
        }));

        var item = new MenuItem { Header = "Sao chép mã vạch", InputGestureText = "Ctrl+C" };
        item.Click += (_, _) => Copy();
        grid.ContextMenu = new ContextMenu { Items = { item } };

        // Chuột phải vào dòng nào thì chép mã của dòng đó.
        grid.PreviewMouseRightButtonDown += (_, e) =>
        {
            var row = FindParent<DataGridRow>(e.OriginalSource as DependencyObject);
            if (row != null) grid.SelectedItem = row.Item;
        };
    }

    private static T? FindParent<T>(DependencyObject? element) where T : DependencyObject
    {
        while (element != null && element is not T)
            element = element is Visual ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element);
        return element as T;
    }
}
