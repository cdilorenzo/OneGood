using Microsoft.Maui.Controls;
using System.Windows.Input;
using OneGood.Maui.Services;

namespace OneGood.Maui.Views;

public partial class CauseDetailPage : ContentPage
{
    public string Headline { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double Progress { get; set; }
    public string ProgressText { get; set; } = string.Empty;
    public string ImpactStatement { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? SourceUrl { get; set; }
    public ICommand TakeActionCommand { get; set; }
    public ICommand ShareCommand { get; set; }
    public ICommand OpenSourceCommand { get; set; }

    public CauseDetailPage(CauseSummaryDto cause)
    {
        InitializeComponent();
        Headline = cause.Title;
        Description = cause.Summary;
        ImpactStatement = string.Empty;
        Status = string.Empty;
        SourceUrl = cause.OrganisationUrl;
        if (cause.FundingGoal.HasValue && cause.FundingGoal > 0 && cause.FundingCurrent.HasValue)
        {
            Progress = (double)(cause.FundingCurrent.Value / cause.FundingGoal.Value);
            ProgressText = $"{cause.FundingCurrent:C0} of {cause.FundingGoal:C0} raised";
        }
        else
        {
            Progress = 0;
            ProgressText = string.Empty;
        }
        TakeActionCommand = new Command(() => OnTakeAction());
        ShareCommand = new Command(() => OnShare());
        OpenSourceCommand = new Command(() => OnOpenSource());
        BindingContext = this;
    }

    private void OnTakeAction()
    {
        if (!string.IsNullOrEmpty(SourceUrl))
            Launcher.Default.OpenAsync(SourceUrl);
    }

    private void OnShare()
    {
        if (!string.IsNullOrEmpty(SourceUrl))
            Share.Default.RequestAsync(new ShareTextRequest
            {
                Uri = SourceUrl,
                Title = Headline
            });
    }

    private void OnOpenSource()
    {
        if (!string.IsNullOrEmpty(SourceUrl))
            Launcher.Default.OpenAsync(SourceUrl);
    }
}
