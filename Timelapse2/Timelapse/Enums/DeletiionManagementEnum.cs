namespace Timelapse.Enums
{
    // Directs how the Delete Folder is managed by Timelapse
    public enum DeleteFolderManagementEnum : int
    {
        ManualDelete = 0,
        AskToDeleteOnExit = 1,
        AutoDeleteOnExit = 2,
    }
}
