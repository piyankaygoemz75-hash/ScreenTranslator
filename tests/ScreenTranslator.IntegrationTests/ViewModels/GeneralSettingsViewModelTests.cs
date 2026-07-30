using ScreenTranslator.App.ViewModels;

namespace ScreenTranslator.IntegrationTests.ViewModels;

public sealed class GeneralSettingsViewModelTests
{
    [Fact]
    public void Continuous_Command_Raises_Request()
    {
        var viewModel = new GeneralSettingsViewModel();
        var raised = false;
        viewModel.StartContinuousCaptureRequested +=
            (_, _) => raised = true;

        viewModel.StartContinuousCaptureCommand.Execute(null);

        Assert.True(raised);
    }

    [Fact]
    public void Active_Continuous_Session_Disables_Both_Start_Commands()
    {
        var viewModel = new GeneralSettingsViewModel
        {
            IsContinuousCaptureActive = true,
        };

        Assert.False(viewModel.StartCaptureCommand.CanExecute(null));
        Assert.False(viewModel.StartContinuousCaptureCommand.CanExecute(null));
    }

    [Fact]
    public void Status_Shows_Pending_Count()
    {
        var viewModel = new GeneralSettingsViewModel
        {
            IsContinuousCaptureActive = true,
            ContinuousPendingCount = 3,
        };

        Assert.Equal(
            "连续框选中 · 待处理 3",
            viewModel.ContinuousCaptureStatusText);
    }
}
