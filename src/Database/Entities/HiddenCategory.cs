namespace Database.Entities;

/// <summary>
/// Category indicating how a file was hidden.
/// </summary>
public enum HiddenCategory
{
    /// <summary>
    /// User explicitly hid this file.
    /// </summary>
    Manual,

    /// <summary>
    /// Hidden due to folder path rule.
    /// </summary>
    FolderRule
}
