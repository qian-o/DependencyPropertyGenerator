using System.Windows.Controls;

namespace DependencyPropertyGenerator.WPF;

[DependencyProperty<string>("Test1", "\"123\"", isNullable: false)]
[DependencyProperty<int>("Test2", "1", isNullable: true)]
public partial class TestControl : ContentControl
{
}
