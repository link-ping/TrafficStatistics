using System.Windows.Controls;
using TrafficStatistics.App.ViewModels;

namespace TrafficStatistics.App.Views;

/// <summary>
/// Interaction logic for StatisticsView.xaml
/// </summary>
public partial class StatisticsView : UserControl
{
    public StatisticsView()
    {
        InitializeComponent();
    }

    private void DataGrid_Sorting(object sender, DataGridSortingEventArgs e)
    {
        e.Handled = true;
        if (DataContext is StatisticsViewModel vm)
        {
            var columnName = e.Column.SortMemberPath;
            if (string.IsNullOrEmpty(columnName)) return;

            vm.SetSort(columnName);

            // Clear sort directions for all other columns
            var dg = (DataGrid)sender;
            foreach (var col in dg.Columns)
            {
                if (col != e.Column)
                {
                    col.SortDirection = null;
                }
            }

            // Set new sort direction
            e.Column.SortDirection = vm.SortDescending 
                ? System.ComponentModel.ListSortDirection.Descending 
                : System.ComponentModel.ListSortDirection.Ascending;
        }
    }
}
