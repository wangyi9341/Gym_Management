using Microsoft.Win32;

namespace GymManager.App.Services;

public sealed class FileDialogService : IFileDialogService
{
    private const string ExcelFilter = "Excel 文件 (*.xlsx)|*.xlsx";

    public string? ShowOpenExcelFileDialog(string title)
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = ExcelFilter,
            CheckFileExists = true,
            Multiselect = false
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? ShowSaveExcelFileDialog(string title, string suggestedFileName)
    {
        var dialog = new SaveFileDialog
        {
            Title = title,
            Filter = ExcelFilter,
            DefaultExt = ".xlsx",
            AddExtension = true,
            FileName = suggestedFileName,
            OverwritePrompt = true
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}

