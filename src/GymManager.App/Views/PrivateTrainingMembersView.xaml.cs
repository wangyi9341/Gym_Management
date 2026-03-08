using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GymManager.App.Views;

public partial class PrivateTrainingMembersView : UserControl
{
    public PrivateTrainingMembersView()
    {
        InitializeComponent();
        Loaded += (_, _) => UpdateSelectAllState();
    }

    private void SelectAllCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (MembersDataGrid is null)
        {
            return;
        }

        if (SelectAllCheckBox?.IsChecked == true)
        {
            MembersDataGrid.SelectAll();
        }
        else
        {
            MembersDataGrid.UnselectAll();
        }

        UpdateSelectAllState();
    }

    private void MembersDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateSelectAllState();
    }

    private void RowSelectCheckBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (MembersDataGrid is null || sender is not CheckBox checkbox)
        {
            return;
        }

        var item = checkbox.DataContext;
        if (item is null)
        {
            return;
        }

        if (MembersDataGrid.ItemContainerGenerator.ContainerFromItem(item) is DataGridRow row)
        {
            row.IsSelected = !row.IsSelected;
            e.Handled = true;
        }
    }

    private void UpdateSelectAllState()
    {
        if (MembersDataGrid is null || SelectAllCheckBox is null)
        {
            return;
        }

        var total = MembersDataGrid.Items.Count;
        var selected = MembersDataGrid.SelectedItems.Count;

        SelectAllCheckBox.IsChecked = total <= 0 || selected <= 0
            ? false
            : selected >= total
                ? true
                : null;
    }
}
