using System;
using System.Threading.Tasks;
using ModelLibrary.Data;
using ModelLibrary.Editor.Identity;
using ModelLibrary.Editor.Utils;
using UnityEditor;

namespace ModelLibrary.Editor.Windows
{
    /// <summary>
    /// Partial class containing notes management functionality for ModelDetailsWindow.
    /// </summary>
    public partial class ModelDetailsWindow
    {
        private async Task SubmitNote()
        {
            try
            {
                ModelNote note = new ModelNote
                {
                    author = new Identity.SimpleUserIdentityProvider().GetUserName(),
                    message = _newNoteMessage,
                    createdTimeTicks = DateTime.Now.Ticks,
                    tag = _newNoteTag
                };

                _meta.notes.Add(note);
                await SaveMeta();
                _newNoteMessage = string.Empty;
                Repaint();
            }
            catch (Exception ex)
            {
                ErrorHandler.ShowError("Note Submission Failed", $"Failed to add note: {ex.Message}", ex);
            }
        }
    }
}


