using Avalonia.Controls;
using Avalonia.Interactivity;
using Лаб3ПА.ViewModels;

namespace Лаб3ПА;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
    }

    private void OpenTable_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            if (vm.LastExperimentStats != null && vm.LastExperimentStats.Count > 0)
            {
                var tableWin = new Лаб3ПА.TableWindow(vm.LastExperimentStats);
                tableWin.Show();
            }
            else
            {
                // Якщо експеримент ще не проводили
                new Лаб3ПА.TableWindow(new System.Collections.Generic.List<Лаб3ПА.Models.SearchStat>()).Show();
            }
        }
    }
}