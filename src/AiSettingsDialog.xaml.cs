// READU.md — Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ReadU.Helpers;
using ReadU.Models;

namespace ReadU;

public sealed partial class AiSettingsDialog : ContentDialog
{
    private readonly Func<AiConfig, CancellationToken, Task<string>> _testConnectionAsync;
    private readonly AiUiText _uiText;
    private CancellationTokenSource _testConnectionCts;

    public AiConfig ResultConfig { get; private set; }

    public AiSettingsDialog(
        AiConfig initialConfig,
        Func<AiConfig, CancellationToken, Task<string>> testConnectionAsync)
    {
        _testConnectionAsync = testConnectionAsync ?? throw new ArgumentNullException(nameof(testConnectionAsync));
        _uiText = AiUiTextService.Current;

        InitializeComponent();
        ApplyLocalizedText();

        ResultConfig = initialConfig ?? new AiConfig();

        EnableAiCheckBox.IsChecked = ResultConfig.Enabled;
        EndpointTextBox.Text = ResultConfig.Endpoint;
        ApiKeyPasswordBox.Password = ResultConfig.ApiKey;
        ModelTextBox.Text = string.IsNullOrWhiteSpace(ResultConfig.Model)
            ? AiConfig.DefaultModel
            : ResultConfig.Model;
        SelectSummaryLanguage(ResultConfig.SummaryLanguage);

        Closed += AiSettingsDialog_Closed;
    }

    private void AiSettingsDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var config = BuildConfig();
        if (!ValidateForSave(config, out var errorMessage))
        {
            args.Cancel = true;
            ShowStatus(errorMessage);
            return;
        }

        ResultConfig = config;
    }

    private async void TestConnectionButton_Click(object sender, RoutedEventArgs e)
    {
        var config = BuildConfig();
        if (!ValidateForTest(config, out var errorMessage))
        {
            ShowStatus(errorMessage);
            return;
        }

        _testConnectionCts?.Cancel();
        _testConnectionCts?.Dispose();
        _testConnectionCts = new CancellationTokenSource();

        TestConnectionButton.IsEnabled = false;
        TestProgressRing.Visibility = Visibility.Visible;
        TestProgressRing.IsActive = true;
        StatusTextBlock.Visibility = Visibility.Collapsed;

        try
        {
            var responseText = await _testConnectionAsync(config, _testConnectionCts.Token);
            ShowStatus(AiUiTextService.FormatConnectionSuccess(responseText));
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            ShowStatus(AiUiTextService.FormatConnectionError(ex));
        }
        finally
        {
            TestProgressRing.IsActive = false;
            TestProgressRing.Visibility = Visibility.Collapsed;
            TestConnectionButton.IsEnabled = true;
        }
    }

    private AiConfig BuildConfig()
    {
        return new AiConfig
        {
            Enabled = EnableAiCheckBox.IsChecked == true,
            Endpoint = EndpointTextBox.Text?.Trim() ?? string.Empty,
            ApiKey = ApiKeyPasswordBox.Password?.Trim() ?? string.Empty,
            Model = string.IsNullOrWhiteSpace(ModelTextBox.Text)
                ? AiConfig.DefaultModel
                : ModelTextBox.Text.Trim(),
            SummaryLanguage = GetSelectedSummaryLanguage()
        };
    }

    private bool ValidateForSave(AiConfig config, out string errorMessage)
    {
        if (!config.Enabled)
        {
            errorMessage = string.Empty;
            return true;
        }

        return ValidateForTest(config, out errorMessage);
    }

    private bool ValidateForTest(AiConfig config, out string errorMessage)
    {
        if (!config.HasRequiredFields)
        {
            errorMessage = _uiText.ConnectionInvalidConfig;
            return false;
        }

        if (!config.TryGetEndpointUri(out _))
        {
            errorMessage = _uiText.ConnectionEndpointInvalid;
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private void ShowStatus(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            StatusTextBlock.Visibility = Visibility.Collapsed;
            StatusTextBlock.Text = string.Empty;
            return;
        }

        StatusTextBlock.Text = message;
        StatusTextBlock.Visibility = Visibility.Visible;
    }

    private void ApplyLocalizedText()
    {
        Title = _uiText.SettingsTitle;
        PrimaryButtonText = _uiText.SaveButtonText;
        CloseButtonText = _uiText.CancelButtonText;

        EnableAiCheckBox.Content = _uiText.EnableAiSummarization;
        EndpointLabelTextBlock.Text = _uiText.EndpointLabel;
        EndpointTextBox.PlaceholderText = _uiText.EndpointPlaceholder;
        ApiKeyLabelTextBlock.Text = _uiText.ApiKeyLabel;
        ApiKeyPasswordBox.PlaceholderText = _uiText.ApiKeyPlaceholder;
        ModelLabelTextBlock.Text = _uiText.ModelLabel;
        ModelTextBox.PlaceholderText = _uiText.ModelPlaceholder;
        SummaryLanguageLabelTextBlock.Text = _uiText.SummaryLanguageLabel;
        SummaryLanguageSystemComboBoxItem.Content = _uiText.SummaryLanguageSystemOption;
        SummaryLanguageEnglishComboBoxItem.Content = _uiText.SummaryLanguageEnglishOption;
        SummaryLanguageTraditionalChineseComboBoxItem.Content = _uiText.SummaryLanguageTraditionalChineseOption;
        SummaryLanguageSimplifiedChineseComboBoxItem.Content = _uiText.SummaryLanguageSimplifiedChineseOption;
        SummaryLanguageJapaneseComboBoxItem.Content = _uiText.SummaryLanguageJapaneseOption;
        SummaryLanguageKoreanComboBoxItem.Content = _uiText.SummaryLanguageKoreanOption;
        TestConnectionButton.Content = _uiText.TestConnection;
    }

    private string GetSelectedSummaryLanguage()
    {
        if (SummaryLanguageComboBox.SelectedItem is ComboBoxItem item
            && item.Tag is string value
            && !string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return AiConfig.UseSystemLanguage;
    }

    private void SelectSummaryLanguage(string summaryLanguage)
    {
        var normalized = string.IsNullOrWhiteSpace(summaryLanguage)
            ? AiConfig.UseSystemLanguage
            : summaryLanguage.Trim();

        foreach (var entry in SummaryLanguageComboBox.Items)
        {
            if (entry is ComboBoxItem item
                && item.Tag is string value
                && string.Equals(value, normalized, StringComparison.OrdinalIgnoreCase))
            {
                SummaryLanguageComboBox.SelectedItem = item;
                return;
            }
        }

        SummaryLanguageComboBox.SelectedItem = SummaryLanguageSystemComboBoxItem;
    }

    private void AiSettingsDialog_Closed(ContentDialog sender, ContentDialogClosedEventArgs args)
    {
        _testConnectionCts?.Cancel();
        _testConnectionCts?.Dispose();
        _testConnectionCts = null;
    }
}