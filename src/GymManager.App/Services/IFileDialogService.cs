namespace GymManager.App.Services;

public interface IFileDialogService
{
    string? ShowOpenExcelFileDialog(string title);

    string? ShowSaveExcelFileDialog(string title, string suggestedFileName);
}

