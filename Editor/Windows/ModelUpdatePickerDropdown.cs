using System;
using System.Collections.Generic;
using ModelLibrary.Data;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace ModelLibrary.Editor.Windows
{
    /// <summary>
    /// Searchable dropdown for selecting an existing catalog model to update.
    /// </summary>
    public sealed class ModelUpdatePickerDropdown : AdvancedDropdown
    {
        /// <summary>Minimum dropdown width so names and versions remain readable.</summary>
        private const float __MIN_WIDTH = 280f;

        /// <summary>Minimum dropdown height for the searchable list.</summary>
        private const float __MIN_HEIGHT = 320f;

        /// <summary>Root group label in the searchable picker.</summary>
        private const string __ROOT_LABEL = "Models";

        private readonly IReadOnlyList<ModelIndex.Entry> _models;
        private readonly Action<int> _onSelected;

        /// <summary>
        /// Creates a searchable model picker.
        /// </summary>
        /// <param name="state">IMGUI dropdown state (search text, scroll).</param>
        /// <param name="models">Catalog entries to list.</param>
        /// <param name="onSelected">Callback invoked with the selected catalog index.</param>
        public ModelUpdatePickerDropdown(
            AdvancedDropdownState state,
            IReadOnlyList<ModelIndex.Entry> models,
            Action<int> onSelected)
            : base(state)
        {
            _models = models;
            _onSelected = onSelected;
            minimumSize = new Vector2(__MIN_WIDTH, __MIN_HEIGHT);
        }

        /// <summary>
        /// Formats a catalog entry for the picker and the selected-model label.
        /// </summary>
        /// <param name="entry">Catalog entry to format.</param>
        /// <returns>Display label including name and latest version.</returns>
        public static string FormatModelOption(ModelIndex.Entry entry)
        {
            if (entry == null)
            {
                return string.Empty;
            }

            return $"{entry.name} (latest v{entry.latestVersion})";
        }

        /// <inheritdoc />
        protected override AdvancedDropdownItem BuildRoot()
        {
            AdvancedDropdownItem root = new AdvancedDropdownItem(__ROOT_LABEL);
            if (_models == null)
            {
                return root;
            }

            for (int i = 0; i < _models.Count; i++)
            {
                ModelIndex.Entry entry = _models[i];
                string label = FormatModelOption(entry);
                AdvancedDropdownItem item = new AdvancedDropdownItem(label)
                {
                    id = i
                };
                root.AddChild(item);
            }

            return root;
        }

        /// <inheritdoc />
        protected override void ItemSelected(AdvancedDropdownItem item)
        {
            if (item == null || _onSelected == null)
            {
                return;
            }

            _onSelected(item.id);
        }
    }
}
