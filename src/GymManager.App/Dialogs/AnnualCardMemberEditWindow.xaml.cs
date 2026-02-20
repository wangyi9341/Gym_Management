using System.Windows;

namespace GymManager.App.Dialogs;

public partial class AnnualCardMemberEditWindow : Window
{
    public AnnualCardMemberEditWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is DialogViewModelBase vm)
        {
            vm.RequestClose += (_, result) =>
            {
                DialogResult = result;
                Close();
            };
        }
    }
}

