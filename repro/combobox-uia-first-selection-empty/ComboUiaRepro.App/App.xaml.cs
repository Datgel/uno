using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace ComboUiaRepro;

public partial class App : Application
{
    private Window? _window;

    public App() => InitializeComponent();

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // A plain ComboBox with string items, first item selected, nothing else on the page.
        var combo = new ComboBox { Width = 240 };
        for (var i = 0; i < 20; i++)
            combo.Items.Add($"Item {i}");
        combo.SelectedIndex = 0;
        AutomationProperties.SetAutomationId(combo, "Combo");
        AutomationProperties.SetName(combo, "Repro combo");

        combo.SelectionChanged += (_, _) =>
            Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} app: SelectionChanged -> {combo.SelectedItem}");
        combo.Loaded += (_, _) => combo.Focus(FocusState.Programmatic);

        _window = new Window
        {
            Title = "ComboUiaRepro",
            Content = new StackPanel { Padding = new Thickness(24), Children = { combo } },
        };
        _window.Activate();
    }
}
