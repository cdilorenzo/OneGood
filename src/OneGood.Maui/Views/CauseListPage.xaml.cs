using Microsoft.Maui.Controls;
using OneGood.Maui.Services;
using System.Collections.ObjectModel;

namespace OneGood.Maui.Views;

public partial class CauseListPage : ContentPage
{
    public ObservableCollection<CauseListItem> Causes { get; set; } = new();
    private readonly ApiService _apiService = new();

    public CauseListPage()
    {
        InitializeComponent();
        BindingContext = this;
        LoadCauses();
    }

    private async void LoadCauses()
    {
        var causes = await _apiService.GetCausesAsync();
        Causes.Clear();
        foreach (var cause in causes)
        {
            Causes.Add(new CauseListItem(cause));
        }
    }

    private void OnCauseSelected(object sender, SelectionChangedEventArgs e)
    {
        // TODO: Navigate to detail page
        CausesCollection.SelectedItem = null;
    }
}

public class CauseListItem
{
    public string Headline { get; set; }
    public string Description { get; set; }
    public double Progress { get; set; }
    public string ProgressText { get; set; }

    public CauseListItem(CauseSummaryDto dto)
    {
        Headline = dto.Title;
        Description = dto.Summary;
        if (dto.FundingGoal.HasValue && dto.FundingGoal > 0 && dto.FundingCurrent.HasValue)
        {
            Progress = (double)(dto.FundingCurrent.Value / dto.FundingGoal.Value);
            ProgressText = $"{dto.FundingCurrent:C0} of {dto.FundingGoal:C0} raised";
        }
        else
        {
            Progress = 0;
            ProgressText = string.Empty;
        }
    }
}
