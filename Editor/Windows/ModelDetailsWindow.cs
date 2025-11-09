using UnityEditor;

namespace ModelLibrary.Editor.Windows
{
    /// <summary>
    /// Detailed view window for a specific model version.
    /// Displays comprehensive metadata including description, tags, structure, changelog, and notes.
    /// Allows Artists to edit metadata (description and tags) and delete versions.
    /// Allows all users to add feedback notes and import the model to their project.
    /// 
    /// This class is split into partial classes for better maintainability:
    /// - ModelDetailsWindow.State.cs: Field declarations
    /// - ModelDetailsWindow.Lifecycle.cs: Unity lifecycle and initialization methods
    /// - ModelDetailsWindow.UI.cs: Main UI rendering (OnGUI, structure, changelog)
    /// - ModelDetailsWindow.Editing.cs: Metadata editing functionality
    /// - ModelDetailsWindow.Notes.cs: Notes management
    /// - ModelDetailsWindow.VersionManagement.cs: Version deletion and import operations
    /// </summary>
    public partial class ModelDetailsWindow : EditorWindow
    {
    }
}
