using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Linq;
using GymManager.App.ViewModels;
using GymManager.Domain.Entities;

namespace GymManager.App.Views;

public partial class CoachesView : UserControl
{
    public CoachesView()
    {
        InitializeComponent();
        Loaded += (_, _) => UpdateSelectAllState();
    }

    private void SelectAllCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (CoachesDataGrid is null)
        {
            return;
        }

        if (SelectAllCheckBox?.IsChecked == true)
        {
            CoachesDataGrid.SelectAll();
        }
        else
        {
            CoachesDataGrid.UnselectAll();
        }

        UpdateSelectAllState();
    }

    private void CoachesDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateSelectAllState();

        if (DataContext is CoachesViewModel vm && CoachesDataGrid is not null)
        {
            vm.SelectedCoaches = CoachesDataGrid.SelectedItems.OfType<Coach>().ToList();
        }
    }

    private void RowSelectCheckBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (CoachesDataGrid is null || sender is not CheckBox checkbox)
        {
            return;
        }

        var item = checkbox.DataContext;
        if (item is null)
        {
            return;
        }

        if (CoachesDataGrid.ItemContainerGenerator.ContainerFromItem(item) is DataGridRow row)
        {
            row.IsSelected = !row.IsSelected;
            e.Handled = true;
        }
    }

    private void UpdateSelectAllState()
    {
        if (CoachesDataGrid is null || SelectAllCheckBox is null)
        {
            return;
        }

        var total = CoachesDataGrid.Items.Count;
        var selected = CoachesDataGrid.SelectedItems.Count;

        SelectAllCheckBox.IsChecked = total <= 0 || selected <= 0
            ? false
            : selected >= total
                ? true
                : null;
    }
}
