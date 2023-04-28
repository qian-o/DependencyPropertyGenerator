using Microsoft.UI.Xaml.Controls;

namespace DependencyPropertyGenerator.WinUI;

[DependencyProperty<string>("Test1", "\"123\"", isNullable: false)]
public partial class TestControl : ContentControl
{
    partial void OnTest1Changed(string oldValue, string newValue)
    {
    }
}
