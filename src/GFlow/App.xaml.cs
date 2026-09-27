namespace GFlow;

public partial class App : Microsoft.Maui.Controls.Application
{
    public App() => InitializeComponent();

    protected override Window CreateWindow(IActivationState? activationState)
		=> new Window(new MainPage()) { Title = "GFlow" };
}
