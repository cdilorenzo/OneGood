using System;
using Microsoft.Maui.Controls;

namespace OneGood.Maui.Views;

public partial class FooterView : ContentView
{
    public FooterView()
    {
        InitializeComponent();
        BindingContext = new FooterViewModel();
    }
}

public class FooterViewModel
{
    public int CurrentYear => DateTime.Now.Year;
    public string Imprint => Resources.Strings.legal_imprint;
    public string Privacy => Resources.Strings.legal_privacy;
    public string External => Resources.Strings.legal_external;
    public TapGestureRecognizer ImprintTap { get; }
    public TapGestureRecognizer PrivacyTap { get; }
    public TapGestureRecognizer ExternalTap { get; }

    public FooterViewModel()
    {
        ImprintTap = new TapGestureRecognizer { Command = new Command(() => Shell.Current.GoToAsync("//imprint")) };
        PrivacyTap = new TapGestureRecognizer { Command = new Command(() => Shell.Current.GoToAsync("//privacy")) };
        ExternalTap = new TapGestureRecognizer { Command = new Command(() => Shell.Current.GoToAsync("//external")) };
    }
}
