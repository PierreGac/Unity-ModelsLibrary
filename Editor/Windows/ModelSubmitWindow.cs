using UnityEditor;

namespace ModelLibrary.Editor.Windows
{
    /// <summary>
    /// Wizard window for submitting new models or updating existing models to the repository.
    /// Provides a comprehensive form for entering model metadata, selecting assets, and managing versions.
    /// Includes validation for changelogs, paths, and version numbers.
    /// Only accessible to users with the Artist role.
    /// 
    /// This class is split into partial classes for better maintainability:
    /// - ModelSubmitWindow.State.cs: Field declarations, enums, and constants
    /// - ModelSubmitWindow.Lifecycle.cs: Unity lifecycle methods (OnEnable, OnGUI entry point)
    /// - ModelSubmitWindow.UI.cs: All UI rendering methods
    /// - ModelSubmitWindow.Validation.cs: Validation logic
    /// - ModelSubmitWindow.Images.cs: Image handling methods
    /// - ModelSubmitWindow.Draft.cs: Draft save/load functionality
    /// - ModelSubmitWindow.Operations.cs: Business logic (submit, load, etc.)
    /// </summary>
    public partial class ModelSubmitWindow : EditorWindow
    {
    }
}
